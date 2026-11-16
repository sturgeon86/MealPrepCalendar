using System.Data;
using Microsoft.Data.SqlClient;
using Repas.Domaine.Mesures;
using Repas.Domaine.Referentiel;

namespace Repas.Donnees;

/// <summary>Accès aux tables <c>Ingredient</c> et <c>IngredientSynonyme</c>.</summary>
public sealed class DepotIngredients : DepotBase
{
    private const string ContrainteNomUnique = "UQ_Ingredient_Nom";
    private const string ContrainteSynonymeUnique = "UQ_IngredientSynonyme_Synonyme";

    public DepotIngredients(BaseDeDonnees baseDeDonnees) : base(baseDeDonnees)
    {
    }

    /// <summary>
    /// Cherche les ingrédients dont le nom ou un synonyme contient <paramref name="texte"/>,
    /// sans égard à la casse ni aux accents (« creme » trouve « Crème 35 % »).
    /// </summary>
    /// <param name="texte">Texte cherché ; vide ou <c>null</c> pour tout lister.</param>
    /// <param name="categorieId">Pour ne garder qu'une catégorie ; <c>null</c> pour toutes.</param>
    /// <param name="inclureInactifs">Pour afficher aussi les ingrédients désactivés.</param>
    public Task<List<ResumeIngredient>> RechercherAsync(
        string? texte,
        int? categorieId = null,
        bool inclureInactifs = false,
        SessionSql? session = null,
        CancellationToken annulation = default)
    {
        const string sql = """
            SELECT i.Id, i.Nom, i.CategorieId, c.Nom, i.TypeMesure, u.Code, i.EstAlimentaire, i.Actif,
                   ISNULL((SELECT STRING_AGG(s.Synonyme, N', ') WITHIN GROUP (ORDER BY s.Synonyme)
                           FROM dbo.IngredientSynonyme s
                           WHERE s.IngredientId = i.Id), N''),
                   f.Nom
            FROM dbo.Ingredient i
            JOIN dbo.Categorie c ON c.Id = i.CategorieId
            JOIN dbo.Unite u ON u.Id = i.UniteReferenceId
            LEFT JOIN dbo.Ingredient f ON f.Id = i.FusionneDansId
            WHERE (@categorieId IS NULL OR i.CategorieId = @categorieId)
              AND (@inclureInactifs = 1 OR i.Actif = 1)
              AND (@motif IS NULL
                   OR i.Nom COLLATE French_CI_AI LIKE @motif ESCAPE N'\'
                   OR EXISTS (SELECT 1 FROM dbo.IngredientSynonyme s
                              WHERE s.IngredientId = i.Id
                                AND s.Synonyme COLLATE French_CI_AI LIKE @motif ESCAPE N'\'))
            ORDER BY i.Nom;
            """;

        var motif = MotifContient(texte);

        return ListerAsync(
            session,
            sql,
            l => new ResumeIngredient(
                Id: l.GetInt32(0),
                Nom: l.GetString(1),
                CategorieId: l.GetInt32(2),
                Categorie: l.GetString(3),
                TypeMesure: Enum.Parse<TypeMesure>(l.GetString(4)),
                UniteReference: l.GetString(5),
                EstAlimentaire: l.GetBoolean(6),
                Actif: l.GetBoolean(7),
                Synonymes: l.GetString(8),
                FusionneDans: l.IsDBNull(9) ? null : l.GetString(9)),
            p => p
                .Ajouter("@categorieId", SqlDbType.Int, categorieId)
                .Ajouter("@inclureInactifs", SqlDbType.Bit, inclureInactifs)
                .Ajouter("@motif", SqlDbType.NVarChar, 400, motif),
            annulation);
    }

    /// <summary>La fiche complète, avec ses synonymes, ou <c>null</c> si l'ingrédient n'existe pas.</summary>
    public Task<FicheIngredient?> TrouverAsync(int id, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sqlIngredient = """
            SELECT Id, Nom, CategorieId, TypeMesure, UniteReferenceId, DensiteGParMl, PoidsMoyenUniteG, EstAlimentaire, Actif
            FROM dbo.Ingredient
            WHERE Id = @id;
            """;
        const string sqlSynonymes = "SELECT Synonyme FROM dbo.IngredientSynonyme WHERE IngredientId = @id ORDER BY Id;";

        return DansTransactionAsync(
            session,
            async s =>
            {
                var fiche = await s.PremierOuDefautAsync(
                    sqlIngredient,
                    l => new FicheIngredient(
                        Id: l.GetInt32(0),
                        Nom: l.GetString(1),
                        CategorieId: l.GetInt32(2),
                        TypeMesure: Enum.Parse<TypeMesure>(l.GetString(3)),
                        UniteReferenceId: l.GetInt32(4),
                        DensiteGParMl: LireDecimal(l, 5),
                        PoidsMoyenUniteG: LireDecimal(l, 6),
                        EstAlimentaire: l.GetBoolean(7),
                        Actif: l.GetBoolean(8),
                        Synonymes: []),
                    p => p.Ajouter("@id", SqlDbType.Int, id),
                    annulation);
                if (fiche is null)
                {
                    return null;
                }

                var synonymes = await s.ListerAsync(
                    sqlSynonymes, l => l.GetString(0), p => p.Ajouter("@id", SqlDbType.Int, id), annulation);
                return fiche with { Synonymes = synonymes };
            },
            annulation);
    }

    /// <summary>Enregistre une nouvelle fiche et ses synonymes, dans une transaction.</summary>
    /// <returns>L'<c>Id</c> du nouvel ingrédient.</returns>
    /// <exception cref="TexteIngredientDejaUtiliseException">Le nom ou un synonyme appartient déjà à un autre ingrédient.</exception>
    public Task<int> AjouterAsync(FicheIngredient fiche, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            INSERT INTO dbo.Ingredient
                (Nom, CategorieId, TypeMesure, UniteReferenceId, DensiteGParMl, PoidsMoyenUniteG, EstAlimentaire, Actif)
            OUTPUT INSERTED.Id
            VALUES (@nom, @categorieId, @typeMesure, @uniteReferenceId, @densite, @poidsMoyen, @estAlimentaire, @actif);
            """;

        return EnregistrerAsync(
            fiche,
            session,
            async s =>
            {
                var id = await s.ScalaireAsync<int>(sql, p => AjouterColonnes(p, fiche), annulation);
                await AjouterSynonymesAsync(s, id, fiche.Synonymes, annulation);
                return id;
            },
            annulation);
    }

    /// <summary>Remplace la fiche et ses synonymes, dans une transaction.</summary>
    /// <returns><c>false</c> si l'ingrédient n'existe pas.</returns>
    /// <exception cref="TexteIngredientDejaUtiliseException">Le nom ou un synonyme appartient déjà à un autre ingrédient.</exception>
    public Task<bool> ModifierAsync(FicheIngredient fiche, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            UPDATE dbo.Ingredient
            SET Nom = @nom, CategorieId = @categorieId, TypeMesure = @typeMesure, UniteReferenceId = @uniteReferenceId,
                DensiteGParMl = @densite, PoidsMoyenUniteG = @poidsMoyen, EstAlimentaire = @estAlimentaire, Actif = @actif,
                -- Réactiver un ingrédient fusionné annule le lien de fusion.
                FusionneDansId = CASE WHEN @actif = 1 THEN NULL ELSE FusionneDansId END
            WHERE Id = @id;
            """;
        const string sqlRetirerSynonymes = "DELETE FROM dbo.IngredientSynonyme WHERE IngredientId = @id;";

        return EnregistrerAsync(
            fiche,
            session,
            async s =>
            {
                var lignes = await s.ExecuterAsync(
                    sql, p => AjouterColonnes(p, fiche).Ajouter("@id", SqlDbType.Int, fiche.Id), annulation);
                if (lignes == 0)
                {
                    return false;
                }

                await s.ExecuterAsync(sqlRetirerSynonymes, p => p.Ajouter("@id", SqlDbType.Int, fiche.Id), annulation);
                await AjouterSynonymesAsync(s, fiche.Id, fiche.Synonymes, annulation);
                return true;
            },
            annulation);
    }

    /// <summary>
    /// Colonnes qui font référence à un ingrédient et que <see cref="FusionnerAsync"/> reporte sur
    /// l'ingrédient conservé. Toute nouvelle table qui référence <c>Ingredient</c> (recettes, listes…)
    /// doit être traitée par la fusion et ajoutée ici ; un test d'intégration le vérifie.
    /// </summary>
    public static readonly IReadOnlyList<string> ReferencesTraiteesParFusion =
    [
        "Ingredient.FusionneDansId",
        "IngredientSynonyme.IngredientId",
        "RecetteIngredient.IngredientId",
    ];

    /// <summary>L'ingrédient qui remplace celui-ci, s'il a été fusionné ; sinon <c>null</c>.</summary>
    public Task<CibleFusion?> TrouverCibleFusionAsync(int id, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            SELECT f.Id, f.Nom
            FROM dbo.Ingredient i
            JOIN dbo.Ingredient f ON f.Id = i.FusionneDansId
            WHERE i.Id = @id;
            """;
        return PremierOuDefautAsync(
            session, sql, l => new CibleFusion(l.GetInt32(0), l.GetString(1)), p => p.Ajouter("@id", SqlDbType.Int, id), annulation);
    }

    /// <summary>
    /// Fusionne un doublon dans l'ingrédient conservé, dans une transaction : tout ce qui pointait vers
    /// le doublon pointe ensuite vers l'ingrédient conservé (synonymes, anciens doublons déjà fusionnés
    /// dans celui-ci, lignes de recettes), puis le doublon est désactivé et garde le lien vers l'ingrédient conservé.
    /// </summary>
    /// <returns>Le nombre de synonymes passés à l'ingrédient conservé.</returns>
    /// <exception cref="FusionImpossibleException">La fusion ne respecte pas <see cref="RegleFusion"/> ; rien n'est modifié.</exception>
    public Task<int> FusionnerAsync(
        int doublonId, int conserveId, SessionSql? session = null, CancellationToken annulation = default)
    {
        // Verrouille les deux lignes jusqu'à la fin de la transaction.
        const string sqlEtat = "SELECT Id, Nom, Actif, FusionneDansId FROM dbo.Ingredient WITH (UPDLOCK, HOLDLOCK) WHERE Id = @id;";
        const string sqlSynonymes = "UPDATE dbo.IngredientSynonyme SET IngredientId = @conserveId WHERE IngredientId = @doublonId;";
        const string sqlAnciensDoublons = "UPDATE dbo.Ingredient SET FusionneDansId = @conserveId WHERE FusionneDansId = @doublonId;";
        // Une recette peut garder deux lignes du même ingrédient (ex. : beurre pour la pâte et pour la garniture).
        const string sqlRecettes = "UPDATE dbo.RecetteIngredient SET IngredientId = @conserveId WHERE IngredientId = @doublonId;";
        const string sqlDesactiver = "UPDATE dbo.Ingredient SET Actif = 0, FusionneDansId = @conserveId WHERE Id = @doublonId;";

        return DansTransactionAsync(
            session,
            async s =>
            {
                async Task<EtatFusion?> LireEtatAsync(int id) => await s.PremierOuDefautAsync(
                    sqlEtat,
                    l => new EtatFusion(l.GetInt32(0), l.GetString(1), l.GetBoolean(2), l.IsDBNull(3) ? null : l.GetInt32(3)),
                    p => p.Ajouter("@id", SqlDbType.Int, id),
                    annulation);

                // Toujours dans le même ordre d'Id, pour éviter un interblocage entre deux fusions croisées.
                var (premier, second) = doublonId < conserveId ? (doublonId, conserveId) : (conserveId, doublonId);
                var etatPremier = await LireEtatAsync(premier);
                var etatSecond = premier == second ? etatPremier : await LireEtatAsync(second);
                var doublon = doublonId == premier ? etatPremier : etatSecond;
                var conserve = conserveId == premier ? etatPremier : etatSecond;

                var probleme = RegleFusion.Verifier(doublon, conserve);
                if (probleme is not null)
                {
                    throw new FusionImpossibleException(probleme);
                }

                void Ids(SqlParameterCollection p) => p
                    .Ajouter("@doublonId", SqlDbType.Int, doublonId)
                    .Ajouter("@conserveId", SqlDbType.Int, conserveId);

                var synonymes = await s.ExecuterAsync(sqlSynonymes, Ids, annulation);
                await s.ExecuterAsync(sqlAnciensDoublons, Ids, annulation);
                await s.ExecuterAsync(sqlRecettes, Ids, annulation);
                await s.ExecuterAsync(sqlDesactiver, Ids, annulation);
                return synonymes;
            },
            annulation);
    }

    /// <summary>
    /// Vérifie, puis écrit dans une transaction. La base garantit l'unicité des noms entre eux et des
    /// synonymes entre eux ; le croisement nom ↔ synonyme d'un autre ingrédient est vérifié ici.
    /// </summary>
    private async Task<T> EnregistrerAsync<T>(
        FicheIngredient fiche, SessionSql? session, Func<SessionSql, Task<T>> ecrire, CancellationToken annulation)
    {
        try
        {
            return await DansTransactionAsync(
                session,
                async s =>
                {
                    foreach (var texte in fiche.Synonymes.Prepend(fiche.Nom))
                    {
                        var proprietaire = await TrouverProprietaireAsync(s, texte, fiche.Id, annulation);
                        if (proprietaire is not null)
                        {
                            throw new TexteIngredientDejaUtiliseException(texte, proprietaire);
                        }
                    }

                    return await ecrire(s);
                },
                annulation);
        }
        catch (SqlException ex) when (EstViolationUnicite(ex, ContrainteNomUnique) || EstViolationUnicite(ex, ContrainteSynonymeUnique))
        {
            // Un autre enregistrement simultané a pris ce texte entre la vérification et l'écriture.
            throw new TexteIngredientDejaUtiliseException(fiche.Nom, "un autre ingrédient", ex);
        }
    }

    /// <summary>Nom de l'autre ingrédient qui porte déjà ce texte (comme nom ou synonyme), ou <c>null</c>.</summary>
    private static Task<string?> TrouverProprietaireAsync(
        SessionSql session, string texte, int idExclu, CancellationToken annulation)
    {
        const string sql = """
            SELECT TOP (1) i.Nom
            FROM dbo.Ingredient i
            WHERE i.Id <> @idExclu
              AND (i.Nom = @texte
                   OR EXISTS (SELECT 1 FROM dbo.IngredientSynonyme s WHERE s.IngredientId = i.Id AND s.Synonyme = @texte));
            """;
        return session.ScalaireAsync<string>(
            sql,
            p => p
                .Ajouter("@texte", SqlDbType.NVarChar, ValidationIngredient.LongueurMaxNom, texte)
                .Ajouter("@idExclu", SqlDbType.Int, idExclu),
            annulation);
    }

    private static async Task AjouterSynonymesAsync(
        SessionSql session, int ingredientId, IEnumerable<string> synonymes, CancellationToken annulation)
    {
        const string sql = "INSERT INTO dbo.IngredientSynonyme (IngredientId, Synonyme) VALUES (@ingredientId, @synonyme);";
        foreach (var synonyme in synonymes)
        {
            await session.ExecuterAsync(
                sql,
                p => p
                    .Ajouter("@ingredientId", SqlDbType.Int, ingredientId)
                    .Ajouter("@synonyme", SqlDbType.NVarChar, ValidationIngredient.LongueurMaxNom, synonyme),
                annulation);
        }
    }

    private static SqlParameterCollection AjouterColonnes(SqlParameterCollection p, FicheIngredient fiche) => p
        .Ajouter("@nom", SqlDbType.NVarChar, ValidationIngredient.LongueurMaxNom, fiche.Nom.Trim())
        .Ajouter("@categorieId", SqlDbType.Int, fiche.CategorieId)
        .Ajouter("@typeMesure", SqlDbType.VarChar, 10, fiche.TypeMesure.ToString())
        .Ajouter("@uniteReferenceId", SqlDbType.Int, fiche.UniteReferenceId)
        .AjouterDecimal("@densite", 10, 4, fiche.DensiteGParMl)
        .AjouterDecimal("@poidsMoyen", 10, 2, fiche.PoidsMoyenUniteG)
        .Ajouter("@estAlimentaire", SqlDbType.Bit, fiche.EstAlimentaire)
        .Ajouter("@actif", SqlDbType.Bit, fiche.Actif);
}
