using Repas.Domaine.Calendrier;
using Repas.Domaine.Epicerie;
using Repas.Domaine.Mesures;
using Repas.Domaine.Recettes;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests;

/// <summary>Tests d'intégration de la génération et de la lecture de la liste d'épicerie, sur les données de base.</summary>
public sealed class DepotListeEpicerieTests : IAsyncLifetime
{
    private static readonly DateOnly Dimanche = new(2026, 11, 15);
    private static readonly DateOnly Samedi = new(2026, 11, 21);

    private BaseDeTest _baseDeTest = null!;
    private DepotListeEpicerie _depot = null!;
    private DepotRecettes _recettes = null!;
    private DepotCalendrier _calendrier = null!;
    private List<Unite> _unites = null!;
    private int _idMarie;
    private int _lait;
    private int _farine;
    private int _sel;
    private int _poulet;
    private int _persil;

    public async Task InitializeAsync()
    {
        _baseDeTest = await BaseDeTest.CreerAvecSchemaAsync(avecDonneesDeBase: true);
        var baseDeDonnees = new BaseDeDonnees(_baseDeTest.ChaineConnexion);
        _depot = new DepotListeEpicerie(baseDeDonnees);
        _recettes = new DepotRecettes(baseDeDonnees);
        _calendrier = new DepotCalendrier(baseDeDonnees);
        _unites = await new DepotUnites(baseDeDonnees).ListerAsync();
        _idMarie = (await new DepotUtilisateurs(baseDeDonnees).AjouterAsync("marie@exemple.com", "Marie", "hash")).Id;

        var ingredients = await new DepotIngredients(baseDeDonnees).RechercherAsync(null);
        int Id(string nom) => ingredients.Single(i => i.Nom == nom).Id;
        _lait = Id("Lait 2 %");
        _farine = Id("Farine tout usage");
        _sel = Id("Sel");
        _poulet = Id("Poitrine de poulet");
        _persil = Id("Persil frais");
    }

    public async Task DisposeAsync() => await _baseDeTest.DisposeAsync();

    private int Unite(string code) => _unites.Single(u => u.Code == code).Id;

    private Task<int> RecetteAsync(string nom, int portions, params LigneIngredient[] lignes) =>
        _recettes.AjouterAsync(new FicheRecette(0, nom, portions, null, null, null, SourceRecette.Manuelle, lignes, [], []), _idMarie);

    private LigneIngredient Ligne(int ingredientId, decimal? quantite, string? unite, bool facultatif = false) =>
        new(ingredientId, quantite, unite is null ? null : Unite(unite), null, facultatif);

    private Task<int> PlanifierAsync(DateOnly date, Plage plage, int recetteId, int portions) =>
        _calendrier.AjouterAsync(RepasPlanifie.PourRecette(date, plage, recetteId, portions), _idMarie);

    private async Task<ListeAvecArticles> ActiveAsync() => Assert.IsType<ListeAvecArticles>(await _depot.TrouverActiveAsync());

    [Fact]
    public async Task Generer_CreeLaListeActiveAvecSesArticlesEtLeursSources()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"), Ligne(_farine, 1.5m, "tasse"));
        var gateau = await RecetteAsync("Gâteau", 8, Ligne(_lait, 1m, "tasse"), Ligne(_farine, 300m, "g"));
        var repasCrepes = await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        var repasGateau = await PlanifierAsync(Dimanche.AddDays(2), Plage.Souper, gateau, 4); // moitié de la recette

        var resultat = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        Assert.Equal(StatutGeneration.Generee, resultat.Statut);
        Assert.Equal((2, 2), (resultat.NbRepas, resultat.NbArticles));

        var (liste, articles) = await ActiveAsync();
        Assert.Equal(resultat.ListeId, liste.Id);
        Assert.Equal("Épicerie du 15 au 21 novembre 2026", liste.Nom);
        Assert.Equal((Dimanche, Samedi), (liste.DateDebut!.Value, liste.DateFin!.Value));
        Assert.NotNull(liste.DateGenerationUtc);
        Assert.True(liste.EstGeneree);

        // Lait : 500 ml + ½ tasse (125 ml). Farine : 1,5 tasse × 0,5 g/ml = 187,5 g + 150 g.
        var lait = Assert.Single(articles, a => a.IngredientId == _lait);
        Assert.Equal((625m, "ml", 2), (lait.Quantite!.Value, lait.CodeUnite!, lait.NbRecettes));
        var farine = Assert.Single(articles, a => a.IngredientId == _farine);
        Assert.Equal((337.5m, "g", 2), (farine.Quantite!.Value, farine.CodeUnite!, farine.NbRecettes));
        Assert.All(articles, a => Assert.False(a.EstManuel || a.EstReporte || a.EstCoche));

        Assert.Equal(1, await _baseDeTest.ScalaireAsync<int>(
            $"SELECT COUNT(*) FROM dbo.ListeEpicerie WHERE Id = {liste.Id} AND CreePar = {_idMarie};"));
        Assert.Equal(2, await _baseDeTest.ScalaireAsync<int>(
            $"""
            SELECT COUNT(*) FROM dbo.ListeEpicerieArticleSource
            WHERE ArticleId = {lait.Id} AND ((RepasPlanifieId = {repasCrepes} AND Quantite = 500) OR (RepasPlanifieId = {repasGateau} AND Quantite = 125));
            """));
    }

    [Fact]
    public async Task Generer_IgnoreLesRepasHorsPeriodeEtLesRepasLibres()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche.AddDays(-1), Plage.Souper, crepes, 4);   // la veille
        await PlanifierAsync(Samedi.AddDays(1), Plage.Diner, crepes, 4);       // le lendemain
        await PlanifierAsync(Samedi, Plage.Souper, crepes, 4);                 // dernier jour : compris
        await _calendrier.AjouterAsync(RepasPlanifie.Libre(Dimanche, Plage.Souper, "Restaurant"), _idMarie);

        var resultat = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        Assert.Equal(1, resultat.NbRepas);
        Assert.Equal(500m, Assert.Single((await ActiveAsync()).Articles).Quantite);
    }

    [Fact]
    public async Task Generer_SansRepasAvecRecette_NeCreeAucuneListe()
    {
        await _calendrier.AjouterAsync(RepasPlanifie.Libre(Dimanche, Plage.Souper, "Restaurant"), _idMarie);

        var resultat = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        Assert.Equal(new ResultatGeneration(StatutGeneration.AucunRepas, null, 0, 0), resultat);
        Assert.Null(await _depot.TrouverActiveAsync());
    }

    [Fact]
    public async Task Generer_UneDeuxiemeFois_NeTouchePasALaListeActive()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        var premiere = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        await PlanifierAsync(Dimanche.AddDays(1), Plage.Diner, crepes, 4);
        var seconde = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        Assert.Equal((StatutGeneration.DejaGeneree, premiere.ListeId), (seconde.Statut, seconde.ListeId));
        Assert.Equal(500m, Assert.Single((await ActiveAsync()).Articles).Quantite);
        Assert.Equal(1, await _baseDeTest.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.ListeEpicerie;"));
    }

    [Fact]
    public async Task Generer_CompleteLaListeActivePasEncoreGeneree_EnGardantSesArticles()
    {
        // La « prochaine liste » : créée sans période, avec un article reporté ou manuel.
        await _baseDeTest.ExecuterAsync(
            $"""
            INSERT INTO dbo.ListeEpicerie (Nom, CreePar) VALUES (N'Prochaine liste', {_idMarie});
            INSERT INTO dbo.ListeEpicerieArticle (ListeId, IngredientId, EstManuel) VALUES (SCOPE_IDENTITY(), {_sel}, 1);
            """);
        var listeExistante = await _baseDeTest.ScalaireAsync<int>("SELECT Id FROM dbo.ListeEpicerie;");
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);

        var resultat = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        Assert.Equal((StatutGeneration.Generee, listeExistante), (resultat.Statut, resultat.ListeId!.Value));
        var (liste, articles) = await ActiveAsync();
        Assert.True(liste.EstGeneree);
        Assert.Equal([(_lait, false), (_sel, true)], articles.OrderBy(a => a.IngredientId == _sel).Select(a => (a.IngredientId, a.EstManuel)));
    }

    [Fact]
    public async Task UneConversionImpossible_GardeSonUniteEtSignaleLAlerte()
    {
        var salade = await RecetteAsync("Salade", 4, Ligne(_persil, 2m, "c. à soupe"), Ligne(_persil, 1m, "unité"));
        await PlanifierAsync(Dimanche, Plage.Diner, salade, 4);

        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        var convertisseur = new ConvertisseurUnites(_unites);
        var persil = (await ActiveAsync()).Articles.Where(a => a.IngredientId == _persil).ToList();
        Assert.Equal([("unité", RaisonEchecConversion.Aucune), ("c. à soupe", RaisonEchecConversion.DensiteManquante | RaisonEchecConversion.PoidsMoyenManquant)],
            persil.OrderBy(a => a.CodeUnite == "c. à soupe").Select(a => (a.CodeUnite!, AlertesListe.Verifier(a, convertisseur))));
    }

    [Fact]
    public async Task UneAlerte_DisparaitQuandLaFicheEstCompletee()
    {
        var salade = await RecetteAsync("Salade", 4, Ligne(_persil, 2m, "c. à soupe"));
        await PlanifierAsync(Dimanche, Plage.Diner, salade, 4);
        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        await _baseDeTest.ExecuterAsync($"UPDATE dbo.Ingredient SET DensiteGParMl = 0.2, PoidsMoyenUniteG = 30 WHERE Id = {_persil};");

        var article = Assert.Single((await ActiveAsync()).Articles);
        Assert.Equal(RaisonEchecConversion.Aucune, AlertesListe.Verifier(article, new ConvertisseurUnites(_unites)));
    }

    [Fact]
    public async Task AuGoutEtFacultatif_SuiventLesReglesDuCalcul()
    {
        var omelette = await RecetteAsync("Omelette", 2, Ligne(_sel, null, null), Ligne(_lait, 50m, "ml", facultatif: true));
        await PlanifierAsync(Dimanche, Plage.Diner, omelette, 2);

        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        var sel = Assert.Single((await ActiveAsync()).Articles);
        Assert.Equal((_sel, (decimal?)null, (string?)null, 1), (sel.IngredientId, sel.Quantite, sel.CodeUnite, sel.NbRecettes));
    }

    [Fact]
    public async Task LesQuantitesSontArrondiesAQuatreDecimalesALEnregistrement()
    {
        // ⅓ de tasse pour 3 portions, planifié pour 1 : 250 ÷ 9 ml.
        var sauce = await RecetteAsync("Sauce", 3, Ligne(_lait, 1m / 3, "tasse"));
        await PlanifierAsync(Dimanche, Plage.Diner, sauce, 1);

        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        // La recette garde 0,3333 tasse (échelle de sa colonne) : 0,3333 × 250 ÷ 3 = 27,775.
        Assert.Equal(27.775m, Assert.Single((await ActiveAsync()).Articles).Quantite);
    }

    [Fact]
    public async Task LesArticlesSuiventLOrdreDesCategories()
    {
        var souper = await RecetteAsync("Souper", 4, Ligne(_sel, 5m, "ml"), Ligne(_lait, 250m, "ml"), Ligne(_poulet, 500m, "g"), Ligne(_persil, 1m, "unité"));
        await PlanifierAsync(Dimanche, Plage.Souper, souper, 4);

        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        // Fruits et légumes, Viandes, Produits laitiers, … Épices et condiments (données de base).
        Assert.Equal(
            ["Fruits et légumes", "Viandes", "Produits laitiers", "Épices et condiments"],
            (await ActiveAsync()).Articles.Select(a => a.Categorie));
    }

    [Fact]
    public async Task UneRecetteSansIngredient_CompteCommeRepasSansAjouterDArticle()
    {
        var vide = await RecetteAsync("À compléter", 4);
        await PlanifierAsync(Dimanche, Plage.Souper, vide, 4);

        var resultat = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        Assert.Equal((StatutGeneration.Generee, 1, 0), (resultat.Statut, resultat.NbRepas, resultat.NbArticles));
        Assert.Empty((await ActiveAsync()).Articles);
    }

    [Fact]
    public async Task TrouverActive_SansListe_RetourneNull()
    {
        Assert.Null(await _depot.TrouverActiveAsync());
    }

    [Fact]
    public async Task UnePeriodeInversee_EstRefusee()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _depot.GenererAsync(Samedi, Dimanche, _idMarie));
    }
}
