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
        // Verrouille la liste active (ou l'absence de liste active) jusqu'à la fin de la transaction.
        const string sqlActive = """
            SELECT Id, DateDebut FROM dbo.ListeEpicerie WITH (UPDLOCK, HOLDLOCK) WHERE Statut = 'Active';
            """;
        // Type nullable explicite : sans liste active, on veut null, pas un tuple (0, false).
        var active = await s.PremierOuDefautAsync(
            sqlActive, l => ((int Id, bool Generee)?)(l.GetInt32(0), !l.IsDBNull(1)), annulation: annulation);
        if (active is { Generee: true })
        {
            return new ResultatGeneration(StatutGeneration.DejaGeneree, active.Value.Id, 0, 0);
        }

        var repas = await LireRepasAsync(s, debut, fin, annulation);
        if (repas.Count == 0)
        {
            return new ResultatGeneration(StatutGeneration.AucunRepas, active?.Id, 0, 0);
        }

        var calcul = new CalculListe(
            new ConvertisseurUnites(await _unites.ListerAsync(s, annulation)),
            await LireMesuresAsync(s, debut, fin, annulation));
        var articles = calcul.Calculer(repas);

        var nom = $"Épicerie du {Semaine.LibellePeriode(debut, fin)}";
        var listeId = active is { } existante
            ? await CompleterAsync(s, existante.Id, nom, debut, fin, annulation)
            : await CreerAsync(s, nom, debut, fin, creeParId, annulation);

        foreach (var article in articles)
        {
            await EcrireArticleAsync(s, listeId, article, annulation);
        }

        return new ResultatGeneration(StatutGeneration.Generee, listeId, repas.Count, articles.Count);
    }

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
        const string sqlSource = """
            INSERT INTO dbo.ListeEpicerieArticleSource (ArticleId, RepasPlanifieId, Quantite, UniteId)
            VALUES (@articleId, @repasId, @quantite, @uniteId);
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
