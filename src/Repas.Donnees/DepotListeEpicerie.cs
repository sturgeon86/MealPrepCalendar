using System.Data;
using Microsoft.Data.SqlClient;
using Repas.Domaine.Calendrier;
using Repas.Domaine.Epicerie;
using Repas.Domaine.Mesures;
using Repas.Domaine.Recettes;

namespace Repas.Donnees;

/// <summary>
/// Accès aux tables <c>ListeEpicerie</c>, <c>ListeEpicerieArticle</c> et <c>ListeEpicerieArticleSource</c> :
/// la liste d'épicerie active, partagée par la famille.
/// </summary>
public sealed class DepotListeEpicerie : DepotBase
{
    /// <summary>Contrainte qui empêche deux listes actives (voir la migration 0006).</summary>
    private const string ContrainteUneSeuleActive = "UQ_ListeEpicerie_UneSeuleActive";

    /// <summary>Échelle des colonnes de quantité : les sommes exactes y sont arrondies à l'enregistrement.</summary>
    private const int EchelleQuantite = 4;

    private readonly DepotUnites _unites;

    public DepotListeEpicerie(BaseDeDonnees baseDeDonnees) : base(baseDeDonnees)
    {
        _unites = new DepotUnites(baseDeDonnees);
    }

    /// <summary>La liste active et ses articles, ou <c>null</c> s'il n'y a pas de liste active.</summary>
    public Task<ListeAvecArticles?> TrouverActiveAsync(SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sqlListe = """
            SELECT Id, Nom, DateDebut, DateFin, DateGeneration, Statut
            FROM dbo.ListeEpicerie WHERE Statut = 'Active';
            """;
        const string sqlArticles = """
            SELECT a.Id, a.IngredientId, i.Nom, c.Nom, c.OrdreGenerique, a.QuantiteTotale, a.UniteId, u.Code,
                   a.NbRecettes, a.EstManuel, a.EstReporte, a.EstCoche,
                   i.UniteReferenceId, i.DensiteGParMl, i.PoidsMoyenUniteG
            FROM dbo.ListeEpicerieArticle a
            JOIN dbo.Ingredient i ON i.Id = a.IngredientId
            JOIN dbo.Categorie c ON c.Id = i.CategorieId
            LEFT JOIN dbo.Unite u ON u.Id = a.UniteId
            WHERE a.ListeId = @listeId
            ORDER BY c.OrdreGenerique, c.Nom, i.Nom, a.Id;
            """;

        return DansTransactionAsync(
            session,
            async s =>
            {
                var liste = await s.PremierOuDefautAsync(sqlListe, LireListe, annulation: annulation);
                if (liste is null)
                {
                    return null;
                }

                var articles = await s.ListerAsync(
                    sqlArticles,
                    l => new ArticleListe(
                        Id: l.GetInt32(0),
                        IngredientId: l.GetInt32(1),
                        NomIngredient: l.GetString(2),
                        Categorie: l.GetString(3),
                        OrdreCategorie: l.GetInt32(4),
                        Quantite: LireDecimal(l, 5),
                        UniteId: l.IsDBNull(6) ? null : l.GetInt32(6),
                        CodeUnite: l.IsDBNull(7) ? null : l.GetString(7),
                        NbRecettes: l.GetInt32(8),
                        EstManuel: l.GetBoolean(9),
                        EstReporte: l.GetBoolean(10),
                        EstCoche: l.GetBoolean(11),
                        Mesures: new MesuresIngredient(l.GetInt32(12), LireDecimal(l, 13), LireDecimal(l, 14))),
                    p => p.Ajouter("@listeId", SqlDbType.Int, liste.Id),
                    annulation);

                return (ListeAvecArticles?)new ListeAvecArticles(liste, articles);
            },
            annulation);
    }

    /// <summary>
    /// Génère la liste active à partir des repas avec recette de la période, dans une transaction :
    /// lecture des repas et des recettes, calcul (<see cref="CalculListe"/>), puis écriture des articles et de leurs sources.
    /// S'il existe déjà une liste active pas encore générée (celle qui a reçu les articles reportés),
    /// elle est complétée ; ses articles sont gardés.
    /// </summary>
    /// <param name="creeParId">L'utilisateur qui génère la liste.</param>
    public async Task<ResultatGeneration> GenererAsync(
        DateOnly debut, DateOnly fin, int creeParId, SessionSql? session = null, CancellationToken annulation = default)
    {
        if (fin < debut)
        {
            throw new ArgumentException("La fin de la période précède son début.", nameof(fin));
        }

        try
        {
            return await DansTransactionAsync(session, s => GenererDansAsync(s, debut, fin, creeParId, annulation), annulation);
        }
        catch (SqlException ex) when (EstViolationUnicite(ex, ContrainteUneSeuleActive))
        {
            // Quelqu'un d'autre a créé la liste active pendant ce temps : on montre la sienne.
            var active = await TrouverActiveAsync(annulation: annulation);
            return new ResultatGeneration(StatutGeneration.DejaGeneree, active?.Liste.Id, 0, active?.Articles.Count ?? 0);
        }
    }

    private async Task<ResultatGeneration> GenererDansAsync(
        SessionSql s, DateOnly debut, DateOnly fin, int creeParId, CancellationToken annulation)
    {
        var active = await VerrouillerActiveAsync(s, annulation);
        if (active is { Generee: true })
        {
            return new ResultatGeneration(StatutGeneration.DejaGeneree, active.Value.Id, 0, 0);
        }

        var (repas, articles) = await CalculerAsync(s, debut, fin, annulation);
        if (repas.Count == 0)
        {
            return new ResultatGeneration(StatutGeneration.AucunRepas, active?.Id, 0, 0);
        }

        var nom = NomListe(debut, fin);
        var listeId = active is { } existante
            ? await CompleterAsync(s, existante.Id, nom, debut, fin, annulation)
            : await CreerAsync(s, nom, debut, fin, creeParId, annulation);

        foreach (var article in articles)
        {
            await EcrireArticleAsync(s, listeId, article, annulation);
        }

        return new ResultatGeneration(StatutGeneration.Generee, listeId, repas.Count, articles.Count);
    }

    /// <summary>
    /// Regénère la liste active pour la période (la même ou une autre), dans une transaction : les articles qui
    /// viennent des repas sont recalculés selon <see cref="Regeneration"/> ; les articles manuels et reportés ne
    /// changent pas.
    /// </summary>
    public Task<ResultatRegeneration> RegenererAsync(
        DateOnly debut, DateOnly fin, SessionSql? session = null, CancellationToken annulation = default)
    {
        if (fin < debut)
        {
            throw new ArgumentException("La fin de la période précède son début.", nameof(fin));
        }

        return DansTransactionAsync(session, s => RegenererDansAsync(s, debut, fin, annulation), annulation);
    }

    /// <summary>
    /// Ajoute un article à la main à la liste active, dans une transaction ; crée la liste (sans période) s'il n'y en a pas.
    /// Si le même ingrédient est déjà sur la liste à la main, pas encore coché et dans la même unité (ou sans quantité),
    /// les quantités s'additionnent sur cette ligne (<see cref="AjoutManuel.Additionner"/>).
    /// </summary>
    /// <param name="creeParId">L'utilisateur, si la liste doit être créée.</param>
    /// <exception cref="AjoutManuelRefuseException">L'ajout n'est pas valide, ou l'ingrédient est désactivé.</exception>
    public Task<ResultatAjoutManuel> AjouterManuelAsync(
        AjoutManuel ajout, int creeParId, SessionSql? session = null, CancellationToken annulation = default)
    {
        ArgumentNullException.ThrowIfNull(ajout);
        var erreurs = ajout.Valider();
        if (erreurs.Count > 0)
        {
            throw new AjoutManuelRefuseException(string.Join(" ", erreurs));
        }

        const string sqlActif = "SELECT Actif FROM dbo.Ingredient WHERE Id = @ingredientId;";
        const string sqlCreerListe = """
            INSERT INTO dbo.ListeEpicerie (Nom, CreePar) OUTPUT INSERTED.Id VALUES (@nom, @creePar);
            """;
        // La ligne à compléter : même unité d'abord, sinon une ligne sans quantité, ou n'importe laquelle si l'ajout n'en a pas.
        const string sqlExistant = """
            SELECT TOP (1) Id, QuantiteTotale, UniteId
            FROM dbo.ListeEpicerieArticle WITH (UPDLOCK)
            WHERE ListeId = @listeId AND IngredientId = @ingredientId AND EstManuel = 1 AND EstReporte = 0 AND EstCoche = 0
              AND (UniteId = @uniteId OR UniteId IS NULL OR @uniteId IS NULL)
            ORDER BY CASE WHEN UniteId = @uniteId THEN 0 WHEN UniteId IS NULL THEN 1 ELSE 2 END, Id;
            """;
        const string sqlMettreAJour = "UPDATE dbo.ListeEpicerieArticle SET QuantiteTotale = @quantite, UniteId = @uniteId WHERE Id = @id;";
        const string sqlInserer = """
            INSERT INTO dbo.ListeEpicerieArticle (ListeId, IngredientId, QuantiteTotale, UniteId, EstManuel)
            OUTPUT INSERTED.Id
            VALUES (@listeId, @ingredientId, @quantite, @uniteId, 1);
            """;

        return DansTransactionAsync(
            session,
            async s =>
            {
                var actif = await s.PremierOuDefautAsync(
                    sqlActif, l => (bool?)l.GetBoolean(0), p => p.Ajouter("@ingredientId", SqlDbType.Int, ajout.IngredientId), annulation);
                if (actif is not true)
                {
                    throw new AjoutManuelRefuseException("Cet article n'existe pas ou a été désactivé dans le référentiel.");
                }

                var listeId = (await VerrouillerActiveAsync(s, annulation))?.Id
                    ?? await s.ScalaireAsync<int>(
                        sqlCreerListe,
                        p => p.Ajouter("@nom", SqlDbType.NVarChar, 100, NomProchaineListe).Ajouter("@creePar", SqlDbType.Int, creeParId),
                        annulation);

                void Cle(SqlParameterCollection p) => p
                    .Ajouter("@listeId", SqlDbType.Int, listeId)
                    .Ajouter("@ingredientId", SqlDbType.Int, ajout.IngredientId)
                    .Ajouter("@uniteId", SqlDbType.Int, ajout.UniteId);

                var existant = await s.PremierOuDefautAsync(
                    sqlExistant,
                    l => ((int Id, decimal? Quantite, int? UniteId)?)(l.GetInt32(0), LireDecimal(l, 1), l.IsDBNull(2) ? null : l.GetInt32(2)),
                    Cle,
                    annulation);

                if (existant is { } ligne)
                {
                    var quantite = AjoutManuel.Additionner(ligne.Quantite, ajout.Quantite);
                    var uniteId = ligne.UniteId ?? ajout.UniteId;
                    await s.ExecuterAsync(
                        sqlMettreAJour,
                        p => p
                            .Ajouter("@id", SqlDbType.Int, ligne.Id)
                            .AjouterDecimal("@quantite", 18, EchelleQuantite, Arrondir(quantite))
                            .Ajouter("@uniteId", SqlDbType.Int, quantite is null ? null : uniteId),
                        annulation);
                    return new ResultatAjoutManuel(listeId, ligne.Id, Additionne: true);
                }

                var articleId = await s.ScalaireAsync<int>(
                    sqlInserer,
                    p =>
                    {
                        Cle(p);
                        p.AjouterDecimal("@quantite", 18, EchelleQuantite, Arrondir(ajout.Quantite));
                    },
                    annulation);
                return new ResultatAjoutManuel(listeId, articleId, Additionne: false);
            },
            annulation);
    }

    /// <summary>
    /// Retire de la liste active un article ajouté à la main ou reporté. Un article qui vient des repas ne se retire pas :
    /// la prochaine regénération le remettrait.
    /// </summary>
    /// <returns><c>false</c> si l'article n'existe plus, n'est pas sur la liste active ou vient des repas.</returns>
    public async Task<bool> RetirerAsync(int articleId, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            DELETE a FROM dbo.ListeEpicerieArticle a
            JOIN dbo.ListeEpicerie l ON l.Id = a.ListeId AND l.Statut = 'Active'
            WHERE a.Id = @id AND (a.EstManuel = 1 OR a.EstReporte = 1);
            """;
        return await ExecuterAsync(session, sql, p => p.Ajouter("@id", SqlDbType.Int, articleId), annulation) > 0;
    }

    /// <summary>Nom d'une liste créée avant d'être générée (pour un ajout manuel ou des articles reportés).</summary>
    public const string NomProchaineListe = "Prochaine liste d'épicerie";

    private async Task<ResultatRegeneration> RegenererDansAsync(
        SessionSql s, DateOnly debut, DateOnly fin, CancellationToken annulation)
    {
        const string sqlExistants = """
            SELECT Id, IngredientId, UniteId, QuantiteTotale, EstCoche
            FROM dbo.ListeEpicerieArticle
            WHERE ListeId = @listeId AND EstManuel = 0 AND EstReporte = 0;
            """;
        const string sqlSupprimer = "DELETE FROM dbo.ListeEpicerieArticle WHERE Id = @id;";
        const string sqlMettreAJour = """
            UPDATE dbo.ListeEpicerieArticle
            SET QuantiteTotale = @quantite, UniteId = @uniteId, NbRecettes = @nbRecettes,
                EstCoche = CASE WHEN @decocher = 1 THEN 0 ELSE EstCoche END,
                CocheePar = CASE WHEN @decocher = 1 THEN NULL ELSE CocheePar END,
                DateCoche = CASE WHEN @decocher = 1 THEN NULL ELSE DateCoche END
            WHERE Id = @id;
            DELETE FROM dbo.ListeEpicerieArticleSource WHERE ArticleId = @id;
            """;
        const string sqlConserves = """
            SELECT COUNT(*) FROM dbo.ListeEpicerieArticle
            WHERE ListeId = @listeId AND (EstManuel = 1 OR EstReporte = 1);
            """;

        var active = await VerrouillerActiveAsync(s, annulation);
        if (active is not { Generee: true } liste)
        {
            return new ResultatRegeneration(StatutRegeneration.AucuneListeGeneree, active?.Id, 0, 0, 0, 0, 0, 0);
        }

        void Liste(SqlParameterCollection p) => p.Ajouter("@listeId", SqlDbType.Int, liste.Id);

        var (repas, calcules) = await CalculerAsync(s, debut, fin, annulation);
        var existants = await s.ListerAsync(
            sqlExistants,
            l => new ArticleExistant(l.GetInt32(0), l.GetInt32(1), l.IsDBNull(2) ? null : l.GetInt32(2), LireDecimal(l, 3), l.GetBoolean(4)),
            Liste,
            annulation);
        var plan = Regeneration.Planifier(existants, calcules);

        foreach (var id in plan.Suppressions)
        {
            await s.ExecuterAsync(sqlSupprimer, p => p.Ajouter("@id", SqlDbType.Int, id), annulation);
        }

        foreach (var miseAJour in plan.MisesAJour)
        {
            var nouveau = miseAJour.Nouveau;
            await s.ExecuterAsync(
                sqlMettreAJour,
                p => p
                    .Ajouter("@id", SqlDbType.Int, miseAJour.ArticleId)
                    .AjouterDecimal("@quantite", 18, EchelleQuantite, Arrondir(nouveau.Quantite))
                    .Ajouter("@uniteId", SqlDbType.Int, nouveau.UniteId)
                    .Ajouter("@nbRecettes", SqlDbType.Int, nouveau.NbRecettes)
                    .Ajouter("@decocher", SqlDbType.Bit, miseAJour.Decocher),
                annulation);
            await EcrireSourcesAsync(s, miseAJour.ArticleId, nouveau, annulation);
        }

        foreach (var ajout in plan.Ajouts)
        {
            await EcrireArticleAsync(s, liste.Id, ajout, annulation);
        }

        await CompleterAsync(s, liste.Id, NomListe(debut, fin), debut, fin, annulation);
        var conserves = await s.ScalaireAsync<int>(sqlConserves, Liste, annulation);

        return new ResultatRegeneration(
            StatutRegeneration.Regeneree,
            liste.Id,
            repas.Count,
            plan.Ajouts.Count,
            plan.MisesAJour.Count,
            plan.Suppressions.Count,
            plan.MisesAJour.Count(m => m.Decocher),
            conserves);
    }

    /// <summary>
    /// La liste active, verrouillée (ou l'absence de liste active) jusqu'à la fin de la transaction :
    /// deux générations simultanées s'attendent au lieu de se mélanger.
    /// </summary>
    private static Task<(int Id, bool Generee)?> VerrouillerActiveAsync(SessionSql s, CancellationToken annulation)
    {
        const string sql = "SELECT Id, DateDebut FROM dbo.ListeEpicerie WITH (UPDLOCK, HOLDLOCK) WHERE Statut = 'Active';";

        // Type nullable explicite : sans liste active, on veut null, pas un tuple (0, false).
        return s.PremierOuDefautAsync(sql, l => ((int Id, bool Generee)?)(l.GetInt32(0), !l.IsDBNull(1)), annulation: annulation);
    }

    /// <summary>Les repas avec recette de la période et les articles qu'ils demandent.</summary>
    private async Task<(List<RepasACalculer> Repas, IReadOnlyList<ArticleCalcule> Articles)> CalculerAsync(
        SessionSql s, DateOnly debut, DateOnly fin, CancellationToken annulation)
    {
        var repas = await LireRepasAsync(s, debut, fin, annulation);
        if (repas.Count == 0)
        {
            return (repas, []);
        }

        var calcul = new CalculListe(
            new ConvertisseurUnites(await _unites.ListerAsync(s, annulation)),
            await LireMesuresAsync(s, debut, fin, annulation));
        return (repas, calcul.Calculer(repas));
    }

    private static string NomListe(DateOnly debut, DateOnly fin) => $"Épicerie du {Semaine.LibellePeriode(debut, fin)}";

    /// <summary>Les repas avec recette de la période, dans l'ordre du calendrier, avec les ingrédients de leur recette.</summary>
    private static async Task<List<RepasACalculer>> LireRepasAsync(
        SessionSql s, DateOnly debut, DateOnly fin, CancellationToken annulation)
    {
        const string sql = """
            SELECT rp.Id, rp.RecetteId, r.Portions, rp.Portions,
                   ri.IngredientId, ri.Quantite, ri.UniteId, ri.EstOptionnel
            FROM dbo.RepasPlanifie rp
            JOIN dbo.Recette r ON r.Id = rp.RecetteId
            LEFT JOIN dbo.RecetteIngredient ri ON ri.RecetteId = r.Id
            WHERE rp.[Date] BETWEEN @debut AND @fin
            ORDER BY rp.[Date], CASE rp.Plage WHEN 'Diner' THEN 0 ELSE 1 END, rp.Id, ri.Ordre;
            """;

        var lignes = await s.ListerAsync(
            sql,
            l => (
                RepasId: l.GetInt32(0),
                RecetteId: l.GetInt32(1),
                PortionsRecette: l.GetInt32(2),
                PortionsPlanifiees: l.GetInt32(3),
                Ligne: l.IsDBNull(4)
                    ? null
                    : new LigneIngredient(
                        l.GetInt32(4), LireDecimal(l, 5), l.IsDBNull(6) ? null : l.GetInt32(6), null, l.GetBoolean(7))),
            p => p.Ajouter("@debut", SqlDbType.Date, debut).Ajouter("@fin", SqlDbType.Date, fin),
            annulation);

        // Une recette sans ingrédient compte comme repas, mais n'ajoute rien.
        return lignes
            .GroupBy(l => (l.RepasId, l.RecetteId, l.PortionsRecette, l.PortionsPlanifiees))
            .Select(g => new RepasACalculer(
                g.Key.RepasId,
                g.Key.RecetteId,
                g.Key.PortionsRecette,
                g.Key.PortionsPlanifiees,
                g.Where(l => l.Ligne is not null).Select(l => l.Ligne!).ToList()))
            .ToList();
    }

    /// <summary>Les mesures des ingrédients des recettes planifiées dans la période.</summary>
    private static async Task<Dictionary<int, MesuresIngredient>> LireMesuresAsync(
        SessionSql s, DateOnly debut, DateOnly fin, CancellationToken annulation)
    {
        const string sql = """
            SELECT i.Id, i.UniteReferenceId, i.DensiteGParMl, i.PoidsMoyenUniteG
            FROM dbo.Ingredient i
            WHERE i.Id IN (SELECT ri.IngredientId
                           FROM dbo.RepasPlanifie rp
                           JOIN dbo.RecetteIngredient ri ON ri.RecetteId = rp.RecetteId
                           WHERE rp.[Date] BETWEEN @debut AND @fin);
            """;

        var mesures = await s.ListerAsync(
            sql,
            l => (Id: l.GetInt32(0), Mesures: new MesuresIngredient(l.GetInt32(1), LireDecimal(l, 2), LireDecimal(l, 3))),
            p => p.Ajouter("@debut", SqlDbType.Date, debut).Ajouter("@fin", SqlDbType.Date, fin),
            annulation);
        return mesures.ToDictionary(m => m.Id, m => m.Mesures);
    }

    private static Task<int> CreerAsync(
        SessionSql s, string nom, DateOnly debut, DateOnly fin, int creeParId, CancellationToken annulation)
    {
        const string sql = """
            INSERT INTO dbo.ListeEpicerie (Nom, DateDebut, DateFin, DateGeneration, CreePar)
            OUTPUT INSERTED.Id
            VALUES (@nom, @debut, @fin, SYSUTCDATETIME(), @creePar);
            """;
        return s.ScalaireAsync<int>(
            sql,
            p => Periode(p, nom, debut, fin).Ajouter("@creePar", SqlDbType.Int, creeParId),
            annulation);
    }

    private static async Task<int> CompleterAsync(
        SessionSql s, int listeId, string nom, DateOnly debut, DateOnly fin, CancellationToken annulation)
    {
        const string sql = """
            UPDATE dbo.ListeEpicerie
            SET Nom = @nom, DateDebut = @debut, DateFin = @fin, DateGeneration = SYSUTCDATETIME()
            WHERE Id = @id;
            """;
        await s.ExecuterAsync(sql, p => Periode(p, nom, debut, fin).Ajouter("@id", SqlDbType.Int, listeId), annulation);
        return listeId;
    }

    private static async Task EcrireArticleAsync(SessionSql s, int listeId, ArticleCalcule article, CancellationToken annulation)
    {
        const string sqlArticle = """
            INSERT INTO dbo.ListeEpicerieArticle (ListeId, IngredientId, QuantiteTotale, UniteId, NbRecettes)
            OUTPUT INSERTED.Id
            VALUES (@listeId, @ingredientId, @quantite, @uniteId, @nbRecettes);
            """;

        var articleId = await s.ScalaireAsync<int>(
            sqlArticle,
            p => p
                .Ajouter("@listeId", SqlDbType.Int, listeId)
                .Ajouter("@ingredientId", SqlDbType.Int, article.IngredientId)
                .AjouterDecimal("@quantite", 18, EchelleQuantite, Arrondir(article.Quantite))
                .Ajouter("@uniteId", SqlDbType.Int, article.UniteId)
                .Ajouter("@nbRecettes", SqlDbType.Int, article.NbRecettes),
            annulation);

        await EcrireSourcesAsync(s, articleId, article, annulation);
    }

    /// <summary>Les repas d'où vient l'article, avec leur part dans l'unité de l'article.</summary>
    private static async Task EcrireSourcesAsync(SessionSql s, int articleId, ArticleCalcule article, CancellationToken annulation)
    {
        const string sqlSource = """
            INSERT INTO dbo.ListeEpicerieArticleSource (ArticleId, RepasPlanifieId, Quantite, UniteId)
            VALUES (@articleId, @repasId, @quantite, @uniteId);
            """;

        foreach (var source in article.Sources)
        {
            var quantite = Arrondir(source.Quantite);
            await s.ExecuterAsync(
                sqlSource,
                p => p
                    .Ajouter("@articleId", SqlDbType.Int, articleId)
                    .Ajouter("@repasId", SqlDbType.Int, source.RepasPlanifieId)
                    .AjouterDecimal("@quantite", 18, EchelleQuantite, quantite)
                    .Ajouter("@uniteId", SqlDbType.Int, quantite is null ? null : article.UniteId),
                annulation);
        }
    }

    /// <summary>
    /// Arrondi à l'échelle de la colonne, seulement au moment d'enregistrer. Une quantité positive plus petite
    /// (ex. : 0,00001 g) devient 0,0001 plutôt que 0, que la base refuserait.
    /// </summary>
    private static decimal? Arrondir(decimal? quantite) =>
        quantite is { } q ? Math.Max(Math.Round(q, EchelleQuantite, MidpointRounding.AwayFromZero), q > 0 ? 0.0001m : 0m) : null;

    private static SqlParameterCollection Periode(SqlParameterCollection p, string nom, DateOnly debut, DateOnly fin) => p
        .Ajouter("@nom", SqlDbType.NVarChar, 100, nom)
        .Ajouter("@debut", SqlDbType.Date, debut)
        .Ajouter("@fin", SqlDbType.Date, fin);

    private static ListeEpicerie LireListe(SqlDataReader l) => new(
        Id: l.GetInt32(0),
        Nom: l.GetString(1),
        DateDebut: l.IsDBNull(2) ? null : l.GetFieldValue<DateOnly>(2),
        DateFin: l.IsDBNull(3) ? null : l.GetFieldValue<DateOnly>(3),
        DateGenerationUtc: l.IsDBNull(4) ? null : DateTime.SpecifyKind(l.GetDateTime(4), DateTimeKind.Utc),
        Statut: Enum.Parse<StatutListe>(l.GetString(5)));
}
