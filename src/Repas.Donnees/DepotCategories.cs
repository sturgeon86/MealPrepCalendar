using System.Data;
using Microsoft.Data.SqlClient;
using Repas.Domaine.Referentiel;

namespace Repas.Donnees;

/// <summary>Accès à la table <c>Categorie</c>.</summary>
public sealed class DepotCategories : DepotBase
{
    private const string ContrainteNomUnique = "UQ_Categorie_Nom";

    /// <summary>Écart entre deux catégories ajoutées l'une après l'autre.</summary>
    private const int PasOrdre = 10;

    public DepotCategories(BaseDeDonnees baseDeDonnees) : base(baseDeDonnees)
    {
    }

    /// <summary>Toutes les catégories, dans l'ordre du trajet en magasin.</summary>
    public Task<List<Categorie>> ListerAsync(SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = "SELECT Id, Nom, OrdreGenerique FROM dbo.Categorie ORDER BY OrdreGenerique, Id;";
        return ListerAsync(session, sql, Lire, annulation: annulation);
    }

    /// <summary>Ajoute une catégorie à la fin du trajet en magasin.</summary>
    /// <exception cref="NomCategorieDejaUtiliseException">Une catégorie porte déjà ce nom.</exception>
    public async Task<Categorie> AjouterAsync(string nom, SessionSql? session = null, CancellationToken annulation = default)
    {
        // Le verrou sur la table évite que deux ajouts simultanés reçoivent le même ordre.
        const string sql = """
            INSERT INTO dbo.Categorie (Nom, OrdreGenerique)
            OUTPUT INSERTED.Id, INSERTED.Nom, INSERTED.OrdreGenerique
            SELECT @nom, ISNULL(MAX(OrdreGenerique), 0) + @pas
            FROM dbo.Categorie WITH (UPDLOCK, HOLDLOCK);
            """;

        var nomNettoye = nom.Trim();
        try
        {
            var ajoutee = await PremierOuDefautAsync(
                session,
                sql,
                Lire,
                p => p
                    .Ajouter("@nom", SqlDbType.NVarChar, Categorie.LongueurMaxNom, nomNettoye)
                    .Ajouter("@pas", SqlDbType.Int, PasOrdre),
                annulation);
            return ajoutee!;
        }
        catch (SqlException ex) when (EstViolationUnicite(ex, ContrainteNomUnique))
        {
            throw new NomCategorieDejaUtiliseException(nomNettoye, ex);
        }
    }

    /// <returns><c>true</c> si la catégorie existe et a été renommée.</returns>
    /// <exception cref="NomCategorieDejaUtiliseException">Une autre catégorie porte déjà ce nom.</exception>
    public async Task<bool> RenommerAsync(
        int id, string nom, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = "UPDATE dbo.Categorie SET Nom = @nom WHERE Id = @id;";

        var nomNettoye = nom.Trim();
        try
        {
            var lignes = await ExecuterAsync(
                session,
                sql,
                p => p
                    .Ajouter("@nom", SqlDbType.NVarChar, Categorie.LongueurMaxNom, nomNettoye)
                    .Ajouter("@id", SqlDbType.Int, id),
                annulation);
            return lignes == 1;
        }
        catch (SqlException ex) when (EstViolationUnicite(ex, ContrainteNomUnique))
        {
            throw new NomCategorieDejaUtiliseException(nomNettoye, ex);
        }
    }

    /// <summary>Échange la catégorie avec sa voisine du dessus ou du dessous dans le trajet en magasin.</summary>
    /// <returns><c>false</c> si la catégorie n'existe pas ou est déjà la première (ou la dernière).</returns>
    public Task<bool> DeplacerAsync(
        int id, SensDeplacement sens, SessionSql? session = null, CancellationToken annulation = default)
    {
        // La voisine est la catégorie juste avant (ou après) dans l'ordre (OrdreGenerique, Id).
        const string sqlVoisineHaut = """
            SELECT TOP (1) c.OrdreGenerique, v.Id, v.OrdreGenerique
            FROM dbo.Categorie c WITH (UPDLOCK, HOLDLOCK)
            JOIN dbo.Categorie v WITH (UPDLOCK, HOLDLOCK)
              ON v.OrdreGenerique < c.OrdreGenerique OR (v.OrdreGenerique = c.OrdreGenerique AND v.Id < c.Id)
            WHERE c.Id = @id
            ORDER BY v.OrdreGenerique DESC, v.Id DESC;
            """;
        const string sqlVoisineBas = """
            SELECT TOP (1) c.OrdreGenerique, v.Id, v.OrdreGenerique
            FROM dbo.Categorie c WITH (UPDLOCK, HOLDLOCK)
            JOIN dbo.Categorie v WITH (UPDLOCK, HOLDLOCK)
              ON v.OrdreGenerique > c.OrdreGenerique OR (v.OrdreGenerique = c.OrdreGenerique AND v.Id > c.Id)
            WHERE c.Id = @id
            ORDER BY v.OrdreGenerique, v.Id;
            """;
        const string sqlEchange = """
            UPDATE dbo.Categorie
            SET OrdreGenerique = CASE Id WHEN @id THEN @ordreVoisine ELSE @ordreCourant END
            WHERE Id IN (@id, @voisineId);
            """;

        return DansTransactionAsync(
            session,
            async s =>
            {
                var voisine = await s.PremierOuDefautAsync(
                    sens == SensDeplacement.Haut ? sqlVoisineHaut : sqlVoisineBas,
                    l => (OrdreCourant: l.GetInt32(0), VoisineId: l.GetInt32(1), OrdreVoisine: l.GetInt32(2)),
                    p => p.Ajouter("@id", SqlDbType.Int, id),
                    annulation);
                if (voisine == default)
                {
                    return false;
                }

                await s.ExecuterAsync(
                    sqlEchange,
                    p => p
                        .Ajouter("@id", SqlDbType.Int, id)
                        .Ajouter("@voisineId", SqlDbType.Int, voisine.VoisineId)
                        .Ajouter("@ordreCourant", SqlDbType.Int, voisine.OrdreCourant)
                        .Ajouter("@ordreVoisine", SqlDbType.Int, voisine.OrdreVoisine),
                    annulation);
                return true;
            },
            annulation);
    }

    private static Categorie Lire(SqlDataReader lecteur) => new(lecteur.GetInt32(0), lecteur.GetString(1), lecteur.GetInt32(2));
}
