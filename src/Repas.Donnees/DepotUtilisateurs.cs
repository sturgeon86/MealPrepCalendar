using System.Data;
using Microsoft.Data.SqlClient;
using Repas.Domaine.Utilisateurs;

namespace Repas.Donnees;

/// <summary>Accès à la table <c>Utilisateur</c>.</summary>
public sealed class DepotUtilisateurs : DepotBase
{
    private const string Colonnes = "Id, Courriel, NomAffiche, MotDePasseHash, Actif, DateCreation";

    // Numéros d'erreur SQL Server : violation d'une contrainte ou d'un index unique.
    private const int ErreurContrainteUnique = 2627;
    private const int ErreurIndexUnique = 2601;

    public DepotUtilisateurs(BaseDeDonnees baseDeDonnees) : base(baseDeDonnees)
    {
    }

    /// <summary>Crée un compte actif. Le courriel est normalisé avant l'enregistrement.</summary>
    /// <exception cref="CourrielDejaUtiliseException">Un compte existe déjà avec ce courriel.</exception>
    public async Task<Utilisateur> AjouterAsync(
        string courriel,
        string nomAffiche,
        string motDePasseHash,
        SessionSql? session = null,
        CancellationToken annulation = default)
    {
        const string sql = """
            INSERT INTO dbo.Utilisateur (Courriel, NomAffiche, MotDePasseHash)
            OUTPUT INSERTED.Id, INSERTED.Courriel, INSERTED.NomAffiche,
                   INSERTED.MotDePasseHash, INSERTED.Actif, INSERTED.DateCreation
            VALUES (@courriel, @nomAffiche, @motDePasseHash);
            """;

        var courrielNormalise = Courriel.Normaliser(courriel);
        try
        {
            var cree = await PremierOuDefautAsync(
                session,
                sql,
                Lire,
                p => p
                    .Ajouter("@courriel", SqlDbType.NVarChar, Courriel.LongueurMax, courrielNormalise)
                    .Ajouter("@nomAffiche", SqlDbType.NVarChar, Utilisateur.LongueurMaxNomAffiche, nomAffiche)
                    .Ajouter("@motDePasseHash", SqlDbType.NVarChar, 500, motDePasseHash),
                annulation);
            return cree!;
        }
        catch (SqlException ex) when (ex.Number is ErreurContrainteUnique or ErreurIndexUnique)
        {
            throw new CourrielDejaUtiliseException(courrielNormalise, ex);
        }
    }

    /// <summary>
    /// Crée le tout premier compte, seulement si la table est vide. La vérification et l'insertion
    /// se font dans une seule instruction, sous verrou exclusif : deux demandes simultanées
    /// ne peuvent pas créer deux « premiers » comptes.
    /// </summary>
    /// <returns>Le compte créé, ou <c>null</c> si un compte existait déjà.</returns>
    public Task<Utilisateur?> AjouterPremierAsync(
        string courriel,
        string nomAffiche,
        string motDePasseHash,
        SessionSql? session = null,
        CancellationToken annulation = default)
    {
        const string sql = """
            INSERT INTO dbo.Utilisateur (Courriel, NomAffiche, MotDePasseHash)
            OUTPUT INSERTED.Id, INSERTED.Courriel, INSERTED.NomAffiche,
                   INSERTED.MotDePasseHash, INSERTED.Actif, INSERTED.DateCreation
            SELECT @courriel, @nomAffiche, @motDePasseHash
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Utilisateur WITH (TABLOCKX, HOLDLOCK));
            """;

        return PremierOuDefautAsync(
            session,
            sql,
            Lire,
            p => p
                .Ajouter("@courriel", SqlDbType.NVarChar, Courriel.LongueurMax, Courriel.Normaliser(courriel))
                .Ajouter("@nomAffiche", SqlDbType.NVarChar, Utilisateur.LongueurMaxNomAffiche, nomAffiche)
                .Ajouter("@motDePasseHash", SqlDbType.NVarChar, 500, motDePasseHash),
            annulation);
    }

    public Task<Utilisateur?> TrouverParIdAsync(
        int id, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = $"SELECT {Colonnes} FROM dbo.Utilisateur WHERE Id = @id;";
        return PremierOuDefautAsync(session, sql, Lire, p => p.Ajouter("@id", SqlDbType.Int, id), annulation);
    }

    /// <summary>Cherche un compte par courriel, sans tenir compte de la casse ni des espaces autour.</summary>
    public Task<Utilisateur?> TrouverParCourrielAsync(
        string courriel, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = $"SELECT {Colonnes} FROM dbo.Utilisateur WHERE Courriel = @courriel;";
        return PremierOuDefautAsync(
            session,
            sql,
            Lire,
            p => p.Ajouter("@courriel", SqlDbType.NVarChar, Courriel.LongueurMax, Courriel.Normaliser(courriel)),
            annulation);
    }

    /// <summary>Tous les comptes, actifs ou non, par nom affiché.</summary>
    public Task<List<Utilisateur>> ListerAsync(SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = $"SELECT {Colonnes} FROM dbo.Utilisateur ORDER BY NomAffiche, Id;";
        return ListerAsync(session, sql, Lire, annulation: annulation);
    }

    /// <summary>Nombre de comptes, actifs ou non. Zéro signifie que l'app n'a pas encore de premier compte.</summary>
    public Task<int> CompterAsync(SessionSql? session = null, CancellationToken annulation = default)
    {
        return ScalaireAsync<int>(session, "SELECT COUNT(*) FROM dbo.Utilisateur;", annulation: annulation);
    }

    /// <returns><c>true</c> si le compte existe et a été modifié.</returns>
    public async Task<bool> ModifierMotDePasseHashAsync(
        int id, string motDePasseHash, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = "UPDATE dbo.Utilisateur SET MotDePasseHash = @motDePasseHash WHERE Id = @id;";
        var lignes = await ExecuterAsync(
            session,
            sql,
            p => p
                .Ajouter("@motDePasseHash", SqlDbType.NVarChar, 500, motDePasseHash)
                .Ajouter("@id", SqlDbType.Int, id),
            annulation);
        return lignes == 1;
    }

    /// <summary>Active ou désactive un compte.</summary>
    /// <returns><c>true</c> si le compte existe et a été modifié.</returns>
    public async Task<bool> ModifierActifAsync(
        int id, bool actif, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = "UPDATE dbo.Utilisateur SET Actif = @actif WHERE Id = @id;";
        var lignes = await ExecuterAsync(
            session,
            sql,
            p => p
                .Ajouter("@actif", SqlDbType.Bit, actif)
                .Ajouter("@id", SqlDbType.Int, id),
            annulation);
        return lignes == 1;
    }

    private static Utilisateur Lire(SqlDataReader lecteur) => new(
        Id: lecteur.GetInt32(0),
        Courriel: lecteur.GetString(1),
        NomAffiche: lecteur.GetString(2),
        MotDePasseHash: lecteur.GetString(3),
        Actif: lecteur.GetBoolean(4),
        DateCreation: DateTime.SpecifyKind(lecteur.GetDateTime(5), DateTimeKind.Utc));
}
