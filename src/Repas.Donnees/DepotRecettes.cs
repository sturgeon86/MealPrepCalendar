using System.Data;
using Microsoft.Data.SqlClient;
using Repas.Domaine.Recettes;

namespace Repas.Donnees;

/// <summary>Accès aux tables <c>Recette</c>, <c>RecetteIngredient</c>, <c>RecetteEtape</c>, <c>Etiquette</c> et <c>RecetteEtiquette</c>.</summary>
public sealed class DepotRecettes : DepotBase
{
    public DepotRecettes(BaseDeDonnees baseDeDonnees) : base(baseDeDonnees)
    {
    }

    /// <summary>Toutes les recettes, par nom.</summary>
    public Task<List<ResumeRecette>> ListerAsync(SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            SELECT r.Id, r.Nom, r.Portions,
                   CASE WHEN r.TempsPreparationMin IS NULL AND r.TempsCuissonMin IS NULL THEN NULL
                        ELSE ISNULL(r.TempsPreparationMin, 0) + ISNULL(r.TempsCuissonMin, 0) END,
                   ISNULL((SELECT STRING_AGG(e.Nom, N', ') WITHIN GROUP (ORDER BY e.Nom)
                           FROM dbo.RecetteEtiquette re JOIN dbo.Etiquette e ON e.Id = re.EtiquetteId
                           WHERE re.RecetteId = r.Id), N''),
                   r.PhotoChemin
            FROM dbo.Recette r
            ORDER BY r.Nom, r.Id;
            """;
        return ListerAsync(
            session,
            sql,
            l => new ResumeRecette(l.GetInt32(0), l.GetString(1), l.GetInt32(2), l.IsDBNull(3) ? null : l.GetInt32(3), l.GetString(4),
                l.IsDBNull(5) ? null : l.GetString(5)),
            annulation: annulation);
    }

    /// <summary>Noms de toutes les étiquettes existantes, pour les suggérer à la saisie.</summary>
    public Task<List<string>> ListerEtiquettesAsync(SessionSql? session = null, CancellationToken annulation = default)
    {
        return ListerAsync(session, "SELECT Nom FROM dbo.Etiquette ORDER BY Nom;", l => l.GetString(0), annulation: annulation);
    }

    /// <summary>La recette complète, ou <c>null</c> si elle n'existe pas.</summary>
    public Task<FicheRecette?> TrouverAsync(int id, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sqlRecette = """
            SELECT Id, Nom, Portions, TempsPreparationMin, TempsCuissonMin, Notes, Source, PhotoChemin
            FROM dbo.Recette WHERE Id = @id;
            """;
        const string sqlIngredients = """
            SELECT ri.IngredientId, ri.Quantite, ri.UniteId, ri.Precision, ri.EstOptionnel, i.Nom
            FROM dbo.RecetteIngredient ri JOIN dbo.Ingredient i ON i.Id = ri.IngredientId
            WHERE ri.RecetteId = @id
            ORDER BY ri.Ordre;
            """;
        const string sqlEtapes = "SELECT Texte FROM dbo.RecetteEtape WHERE RecetteId = @id ORDER BY Ordre;";
        const string sqlEtiquettes = """
            SELECT e.Nom FROM dbo.RecetteEtiquette re JOIN dbo.Etiquette e ON e.Id = re.EtiquetteId
            WHERE re.RecetteId = @id ORDER BY e.Nom;
            """;

        return DansTransactionAsync(
            session,
            async s =>
            {
                void Id(SqlParameterCollection p) => p.Ajouter("@id", SqlDbType.Int, id);

                var recette = await s.PremierOuDefautAsync(
                    sqlRecette,
                    l => new FicheRecette(
                        Id: l.GetInt32(0),
                        Nom: l.GetString(1),
                        Portions: l.GetInt32(2),
                        TempsPreparationMin: l.IsDBNull(3) ? null : l.GetInt32(3),
                        TempsCuissonMin: l.IsDBNull(4) ? null : l.GetInt32(4),
                        Notes: l.IsDBNull(5) ? null : l.GetString(5),
                        Source: Enum.Parse<SourceRecette>(l.GetString(6)),
                        Ingredients: [],
                        Etapes: [],
                        Etiquettes: [],
                        PhotoChemin: l.IsDBNull(7) ? null : l.GetString(7)),
                    Id,
                    annulation);
                if (recette is null)
                {
                    return null;
                }

                var ingredients = await s.ListerAsync(
                    sqlIngredients,
                    l => new LigneIngredient(
                        IngredientId: l.GetInt32(0),
                        Quantite: LireDecimal(l, 1),
                        UniteId: l.IsDBNull(2) ? null : l.GetInt32(2),
                        Precision: l.IsDBNull(3) ? null : l.GetString(3),
                        EstOptionnel: l.GetBoolean(4),
                        NomIngredient: l.GetString(5)),
                    Id,
                    annulation);
                var etapes = await s.ListerAsync(sqlEtapes, l => l.GetString(0), Id, annulation);
                var etiquettes = await s.ListerAsync(sqlEtiquettes, l => l.GetString(0), Id, annulation);

                return recette with { Ingredients = ingredients, Etapes = etapes, Etiquettes = etiquettes };
            },
            annulation);
    }

    /// <summary>Enregistre une nouvelle recette avec ses ingrédients, étapes et étiquettes, dans une transaction.</summary>
    /// <param name="creeParId">L'utilisateur qui saisit la recette.</param>
    /// <returns>L'<c>Id</c> de la nouvelle recette.</returns>
    public Task<int> AjouterAsync(
        FicheRecette recette, int creeParId, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            INSERT INTO dbo.Recette (Nom, Portions, TempsPreparationMin, TempsCuissonMin, Notes, Source, PhotoChemin, CreePar)
            OUTPUT INSERTED.Id
            VALUES (@nom, @portions, @tempsPreparation, @tempsCuisson, @notes, @source, @photo, @creePar);
            """;

        return DansTransactionAsync(
            session,
            async s =>
            {
                var id = await s.ScalaireAsync<int>(
                    sql, p => AjouterColonnes(p, recette).Ajouter("@creePar", SqlDbType.Int, creeParId), annulation);
                await EcrireContenuAsync(s, id, recette, annulation);
                return id;
            },
            annulation);
    }

    /// <summary>Remplace la recette, sa photo, ses ingrédients, ses étapes et ses étiquettes, dans une transaction.</summary>
    /// <returns>
    /// La photo enregistrée juste avant, lue par la même instruction que la modification (donc exacte même si
    /// quelqu'un d'autre l'a changée entre-temps), ou <c>null</c> si la recette n'existe pas.
    /// </returns>
    public Task<ModificationRecette?> ModifierAsync(
        FicheRecette recette, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            UPDATE dbo.Recette
            SET Nom = @nom, Portions = @portions, TempsPreparationMin = @tempsPreparation, TempsCuissonMin = @tempsCuisson,
                Notes = @notes, Source = @source, PhotoChemin = @photo, DateModification = SYSUTCDATETIME()
            OUTPUT DELETED.PhotoChemin
            WHERE Id = @id;
            """;
        const string sqlVider = """
            DELETE FROM dbo.RecetteIngredient WHERE RecetteId = @id;
            DELETE FROM dbo.RecetteEtape WHERE RecetteId = @id;
            DELETE FROM dbo.RecetteEtiquette WHERE RecetteId = @id;
            """;

        return DansTransactionAsync(
            session,
            async s =>
            {
                var anciennes = await s.ListerAsync(
                    sql,
                    l => l.IsDBNull(0) ? null : l.GetString(0),
                    p => AjouterColonnes(p, recette).Ajouter("@id", SqlDbType.Int, recette.Id),
                    annulation);
                if (anciennes.Count == 0)
                {
                    return null;
                }

                await s.ExecuterAsync(sqlVider, p => p.Ajouter("@id", SqlDbType.Int, recette.Id), annulation);
                await EcrireContenuAsync(s, recette.Id, recette, annulation);
                return new ModificationRecette(anciennes[0]);
            },
            annulation);
    }

    private static async Task EcrireContenuAsync(
        SessionSql session, int recetteId, FicheRecette recette, CancellationToken annulation)
    {
        const string sqlIngredient = """
            INSERT INTO dbo.RecetteIngredient (RecetteId, IngredientId, Quantite, UniteId, Precision, EstOptionnel, Ordre)
            VALUES (@recetteId, @ingredientId, @quantite, @uniteId, @precision, @estOptionnel, @ordre);
            """;
        const string sqlEtape = "INSERT INTO dbo.RecetteEtape (RecetteId, Ordre, Texte) VALUES (@recetteId, @ordre, @texte);";
        const string sqlEtiquette = "INSERT INTO dbo.RecetteEtiquette (RecetteId, EtiquetteId) VALUES (@recetteId, @etiquetteId);";

        for (var i = 0; i < recette.Ingredients.Count; i++)
        {
            var ligne = recette.Ingredients[i];
            var precision = string.IsNullOrWhiteSpace(ligne.Precision) ? null : ligne.Precision.Trim();
            var ordre = i + 1;
            await session.ExecuterAsync(
                sqlIngredient,
                p => p
                    .Ajouter("@recetteId", SqlDbType.Int, recetteId)
                    .Ajouter("@ingredientId", SqlDbType.Int, ligne.IngredientId)
                    .AjouterDecimal("@quantite", 18, 4, ligne.Quantite)
                    .Ajouter("@uniteId", SqlDbType.Int, ligne.UniteId)
                    .Ajouter("@precision", SqlDbType.NVarChar, ValidationRecette.LongueurMaxPrecision, precision)
                    .Ajouter("@estOptionnel", SqlDbType.Bit, ligne.EstOptionnel)
                    .Ajouter("@ordre", SqlDbType.Int, ordre),
                annulation);
        }

        for (var i = 0; i < recette.Etapes.Count; i++)
        {
            var texte = recette.Etapes[i];
            var ordre = i + 1;
            await session.ExecuterAsync(
                sqlEtape,
                p => p
                    .Ajouter("@recetteId", SqlDbType.Int, recetteId)
                    .Ajouter("@ordre", SqlDbType.Int, ordre)
                    .Ajouter("@texte", SqlDbType.NVarChar, ValidationRecette.LongueurMaxEtape, texte),
                annulation);
        }

        foreach (var nom in recette.Etiquettes)
        {
            var etiquetteId = await TrouverOuCreerEtiquetteAsync(session, nom, annulation);
            await session.ExecuterAsync(
                sqlEtiquette,
                p => p
                    .Ajouter("@recetteId", SqlDbType.Int, recetteId)
                    .Ajouter("@etiquetteId", SqlDbType.Int, etiquetteId),
                annulation);
        }
    }

    /// <summary>L'<c>Id</c> de l'étiquette de ce nom (sans égard à la casse), créée au besoin.</summary>
    private static async Task<int> TrouverOuCreerEtiquetteAsync(SessionSql session, string nom, CancellationToken annulation)
    {
        // Le verrou évite que deux enregistrements simultanés créent la même étiquette.
        const string sql = """
            DECLARE @id INT = (SELECT Id FROM dbo.Etiquette WITH (UPDLOCK, HOLDLOCK) WHERE Nom = @nom);
            IF @id IS NULL
            BEGIN
                INSERT INTO dbo.Etiquette (Nom) VALUES (@nom);
                SET @id = SCOPE_IDENTITY();
            END
            SELECT @id;
            """;
        return await session.ScalaireAsync<int>(
            sql, p => p.Ajouter("@nom", SqlDbType.NVarChar, ValidationRecette.LongueurMaxEtiquette, nom), annulation);
    }

    private static SqlParameterCollection AjouterColonnes(SqlParameterCollection p, FicheRecette recette) => p
        .Ajouter("@nom", SqlDbType.NVarChar, ValidationRecette.LongueurMaxNom, recette.Nom.Trim())
        .Ajouter("@portions", SqlDbType.Int, recette.Portions)
        .Ajouter("@tempsPreparation", SqlDbType.Int, recette.TempsPreparationMin)
        .Ajouter("@tempsCuisson", SqlDbType.Int, recette.TempsCuissonMin)
        .Ajouter("@notes", SqlDbType.NVarChar, -1, string.IsNullOrWhiteSpace(recette.Notes) ? null : recette.Notes.Trim())
        .Ajouter("@source", SqlDbType.VarChar, 10, recette.Source.ToString())
        .Ajouter("@photo", SqlDbType.NVarChar, 260, recette.PhotoChemin);
}
