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

    // --- Regénération (story 5.4)

    private async Task<ArticleListe> ArticleAsync(int ingredientId) =>
        Assert.Single((await ActiveAsync()).Articles, a => a.IngredientId == ingredientId && !a.EstManuel && !a.EstReporte);

    private Task CocherAsync(int articleId) => _baseDeTest.ExecuterAsync(
        $"UPDATE dbo.ListeEpicerieArticle SET EstCoche = 1, CocheePar = {_idMarie}, DateCoche = SYSUTCDATETIME() WHERE Id = {articleId};");

    [Fact]
    public async Task Regenerer_SuitLesChangementsDuCalendrierEnGardantLesLignes()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        var gateau = await RecetteAsync("Gâteau", 4, Ligne(_lait, 1m, "tasse"), Ligne(_farine, 300m, "g"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        var generation = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);
        var laitAvant = await ArticleAsync(_lait);

        await PlanifierAsync(Dimanche.AddDays(3), Plage.Souper, gateau, 4);
        var resultat = await _depot.RegenererAsync(Dimanche, Samedi);

        Assert.Equal(
            new ResultatRegeneration(StatutRegeneration.Regeneree, generation.ListeId, 2, NbAjoutes: 1, NbMisAJour: 1, NbRetires: 0, NbDecoches: 0, NbConserves: 0),
            resultat);
        var lait = await ArticleAsync(_lait);
        Assert.Equal((laitAvant.Id, 750m, 2), (lait.Id, lait.Quantite!.Value, lait.NbRecettes));
        Assert.Equal(300m, (await ArticleAsync(_farine)).Quantite);
        Assert.Equal(2, await _baseDeTest.ScalaireAsync<int>($"SELECT COUNT(*) FROM dbo.ListeEpicerieArticleSource WHERE ArticleId = {lait.Id};"));
    }

    [Fact]
    public async Task Regenerer_RetireCeQuiNEstPlusPlanifie()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        var gateau = await RecetteAsync("Gâteau", 4, Ligne(_farine, 300m, "g"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        var repasGateau = await PlanifierAsync(Dimanche.AddDays(1), Plage.Diner, gateau, 4);
        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        // Le repas retiré du calendrier emporte sa trace ; la regénération retire l'article.
        Assert.True(await _calendrier.SupprimerAsync(repasGateau));
        var resultat = await _depot.RegenererAsync(Dimanche, Samedi);

        Assert.Equal((1, 1), (resultat.NbRetires, resultat.NbMisAJour));
        Assert.Equal([_lait], (await ActiveAsync()).Articles.Select(a => a.IngredientId));
    }

    [Fact]
    public async Task Regenerer_ConserveLesArticlesManuelsEtReportes()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        var generation = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);
        await _baseDeTest.ExecuterAsync(
            $"""
            INSERT INTO dbo.ListeEpicerieArticle (ListeId, IngredientId, QuantiteTotale, UniteId, EstManuel)
            VALUES ({generation.ListeId}, {_lait}, 1000, {Unite("ml")}, 1);
            INSERT INTO dbo.ListeEpicerieArticle (ListeId, IngredientId, EstReporte, ReporteDeArticleId)
            VALUES ({generation.ListeId}, {_sel}, 1, SCOPE_IDENTITY());
            """);

        var resultat = await _depot.RegenererAsync(Dimanche, Samedi);

        Assert.Equal(2, resultat.NbConserves);
        var articles = (await ActiveAsync()).Articles;
        // Le lait manuel reste à part de celui des repas, avec sa propre quantité.
        Assert.Equal(
            [(_lait, 500m, false, false), (_lait, 1000m, true, false), (_sel, (decimal?)null, false, true)],
            articles.OrderBy(a => a.IngredientId == _sel).ThenBy(a => a.EstManuel).Select(a => (a.IngredientId, a.Quantite, a.EstManuel, a.EstReporte)));
    }

    [Fact]
    public async Task Regenerer_GardeLesCasesCochees_SaufSiLaQuantiteAugmente()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"), Ligne(_farine, 200m, "g"));
        var gateau = await RecetteAsync("Gâteau", 4, Ligne(_lait, 1m, "tasse"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);
        await CocherAsync((await ArticleAsync(_lait)).Id);
        await CocherAsync((await ArticleAsync(_farine)).Id);

        await PlanifierAsync(Dimanche.AddDays(2), Plage.Souper, gateau, 4); // plus de lait, même farine
        var resultat = await _depot.RegenererAsync(Dimanche, Samedi);

        Assert.Equal(1, resultat.NbDecoches);
        Assert.False((await ArticleAsync(_lait)).EstCoche);
        Assert.True((await ArticleAsync(_farine)).EstCoche);
        Assert.Equal(1, await _baseDeTest.ScalaireAsync<int>(
            $"SELECT COUNT(*) FROM dbo.ListeEpicerieArticle WHERE IngredientId = {_lait} AND CocheePar IS NULL AND DateCoche IS NULL;"));
    }

    [Fact]
    public async Task Regenerer_AvecUneAutrePeriode_ChangeLeNomEtLesDates()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        await PlanifierAsync(Samedi.AddDays(3), Plage.Diner, crepes, 4);
        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        var resultat = await _depot.RegenererAsync(Dimanche, Samedi.AddDays(7));

        var (liste, articles) = await ActiveAsync();
        Assert.Equal(2, resultat.NbRepas);
        Assert.Equal("Épicerie du 15 au 28 novembre 2026", liste.Nom);
        Assert.Equal(Samedi.AddDays(7), liste.DateFin);
        Assert.Equal(1000m, Assert.Single(articles).Quantite);
    }

    [Fact]
    public async Task Regenerer_SansListeGeneree_NeFaitRien()
    {
        var resultat = await _depot.RegenererAsync(Dimanche, Samedi);

        Assert.Equal(StatutRegeneration.AucuneListeGeneree, resultat.Statut);
        Assert.Null(await _depot.TrouverActiveAsync());
    }

    // --- Ajout manuel (story 5.5)

    private async Task<int> IngredientAsync(string nom) =>
        await _baseDeTest.ScalaireAsync<int>($"SELECT Id FROM dbo.Ingredient WHERE Nom = N'{nom}';");

    [Fact]
    public async Task AjouterManuel_SansListe_CreeLaProchaineListeSansPeriode()
    {
        var papier = await IngredientAsync("Papier de toilette");

        var resultat = await _depot.AjouterManuelAsync(new AjoutManuel(papier, null, null), _idMarie);

        Assert.False(resultat.Additionne);
        var (liste, articles) = await ActiveAsync();
        Assert.Equal((resultat.ListeId, DepotListeEpicerie.NomProchaineListe, false), (liste.Id, liste.Nom, liste.EstGeneree));
        var article = Assert.Single(articles);
        Assert.Equal((papier, (decimal?)null, true, true), (article.IngredientId, article.Quantite, article.EstManuel, article.PeutEtreRetire));
    }

    [Fact]
    public async Task AjouterManuel_PuisGenerer_LaListeGardeLArticle()
    {
        var papier = await IngredientAsync("Papier de toilette");
        await _depot.AjouterManuelAsync(new AjoutManuel(papier, null, null), _idMarie);
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);

        var generation = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        Assert.Equal(StatutGeneration.Generee, generation.Statut);
        Assert.Equal(2, (await ActiveAsync()).Articles.Count);
    }

    [Fact]
    public async Task AjouterManuel_LeMemeArticleDansLaMemeUnite_AdditionneLesQuantites()
    {
        await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 1m, Unite("l")), _idMarie);

        var second = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 2m, Unite("l")), _idMarie);

        Assert.True(second.Additionne);
        var lait = Assert.Single((await ActiveAsync()).Articles);
        Assert.Equal((3m, "l"), (lait.Quantite!.Value, lait.CodeUnite!));
    }

    [Fact]
    public async Task AjouterManuel_UneQuantiteSurUnArticleSansQuantite_LaComplete()
    {
        var papier = await IngredientAsync("Papier de toilette");
        await _depot.AjouterManuelAsync(new AjoutManuel(papier, null, null), _idMarie);

        await _depot.AjouterManuelAsync(new AjoutManuel(papier, 2m, Unite("unité")), _idMarie);
        await _depot.AjouterManuelAsync(new AjoutManuel(papier, null, null), _idMarie); // déjà là : rien ne change

        var article = Assert.Single((await ActiveAsync()).Articles);
        Assert.Equal((2m, "unité"), (article.Quantite!.Value, article.CodeUnite!));
    }

    [Fact]
    public async Task AjouterManuel_UneAutreUnite_DonneUneAutreLigne()
    {
        await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 1m, Unite("l")), _idMarie);

        var second = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 500m, Unite("ml")), _idMarie);

        Assert.False(second.Additionne);
        Assert.Equal(2, (await ActiveAsync()).Articles.Count);
    }

    [Fact]
    public async Task AjouterManuel_UnArticleDejaCoche_DonneUneNouvelleLigne()
    {
        var premier = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 1m, Unite("l")), _idMarie);
        await CocherAsync(premier.ArticleId);

        var second = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 1m, Unite("l")), _idMarie);

        Assert.NotEqual(premier.ArticleId, second.ArticleId);
    }

    [Fact]
    public async Task AjouterManuel_NeTouchePasAuxArticlesDesRepas()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        var resultat = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 250m, Unite("ml")), _idMarie);

        Assert.False(resultat.Additionne);
        Assert.Equal([500m, 250m], (await ActiveAsync()).Articles.OrderBy(a => a.EstManuel).Select(a => a.Quantite!.Value));
    }

    [Fact]
    public async Task AjouterManuel_UnIngredientDesactive_EstRefuse()
    {
        await _baseDeTest.ExecuterAsync($"UPDATE dbo.Ingredient SET Actif = 0 WHERE Id = {_lait};");

        await Assert.ThrowsAsync<AjoutManuelRefuseException>(() => _depot.AjouterManuelAsync(new AjoutManuel(_lait, null, null), _idMarie));
        Assert.Null(await _depot.TrouverActiveAsync());
    }

    [Fact]
    public async Task AjouterManuel_InvalideEstRefuse()
    {
        await Assert.ThrowsAsync<AjoutManuelRefuseException>(() => _depot.AjouterManuelAsync(new AjoutManuel(_lait, 2m, null), _idMarie));
    }

    [Fact]
    public async Task Retirer_UnArticleManuel()
    {
        var ajout = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 1m, Unite("l")), _idMarie);

        Assert.True(await _depot.RetirerAsync(ajout.ArticleId));

        Assert.Empty((await ActiveAsync()).Articles);
        Assert.False(await _depot.RetirerAsync(ajout.ArticleId));
    }

    [Fact]
    public async Task Retirer_UnArticleDesRepas_EstRefuse()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);
        var lait = await ArticleAsync(_lait);

        Assert.False(lait.PeutEtreRetire);
        Assert.False(await _depot.RetirerAsync(lait.Id));
        Assert.Single((await ActiveAsync()).Articles);
    }

    // --- Cocher en magasin (story 6.1)

    /// <summary>Qui a coché l'article (<c>null</c> si personne) et s'il a une date de coche.</summary>
    private async Task<(int? CocheePar, bool AUneDate)> EtatCocheAsync(int articleId)
    {
        var cocheePar = await _baseDeTest.ScalaireAsync<int>(
            $"SELECT ISNULL(CocheePar, 0) FROM dbo.ListeEpicerieArticle WHERE Id = {articleId};");
        var aUneDate = await _baseDeTest.ScalaireAsync<int>(
            $"SELECT CASE WHEN DateCoche IS NULL THEN 0 ELSE 1 END FROM dbo.ListeEpicerieArticle WHERE Id = {articleId};");
        return (cocheePar == 0 ? null : cocheePar, aUneDate == 1);
    }

    [Fact]
    public async Task Cocher_GardeQuiEtQuand_PuisDecocherEfface()
    {
        var ajout = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 1m, Unite("l")), _idMarie);

        Assert.True(await _depot.CocherAsync(ajout.ArticleId, coche: true, _idMarie));
        Assert.True(Assert.Single((await ActiveAsync()).Articles).EstCoche);
        Assert.Equal((_idMarie, true), await EtatCocheAsync(ajout.ArticleId));

        Assert.True(await _depot.CocherAsync(ajout.ArticleId, coche: false, _idMarie));
        Assert.False(Assert.Single((await ActiveAsync()).Articles).EstCoche);
        Assert.Equal(((int?)null, false), await EtatCocheAsync(ajout.ArticleId));
    }

    [Fact]
    public async Task Cocher_DejaCocheParQuelquUnDAutre_GardeLePremier()
    {
        var paul = (await new DepotUtilisateurs(new BaseDeDonnees(_baseDeTest.ChaineConnexion)).AjouterAsync("paul@exemple.com", "Paul", "hash")).Id;
        var ajout = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 1m, Unite("l")), _idMarie);
        await _depot.CocherAsync(ajout.ArticleId, coche: true, _idMarie);

        Assert.True(await _depot.CocherAsync(ajout.ArticleId, coche: true, paul));

        Assert.Equal((_idMarie, true), await EtatCocheAsync(ajout.ArticleId));
    }

    [Fact]
    public async Task Cocher_UnArticleDesRepas()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        await _depot.GenererAsync(Dimanche, Samedi, _idMarie);

        Assert.True(await _depot.CocherAsync((await ArticleAsync(_lait)).Id, coche: true, _idMarie));

        Assert.True((await ArticleAsync(_lait)).EstCoche);
    }

    [Fact]
    public async Task Cocher_UnArticleInexistant_RetourneFalse()
    {
        Assert.False(await _depot.CocherAsync(99999, coche: true, _idMarie));
    }

    [Fact]
    public async Task Cocher_UnArticleDUneListeTerminee_EstRefuse()
    {
        var ajout = await _depot.AjouterManuelAsync(new AjoutManuel(_lait, 1m, Unite("l")), _idMarie);
        await _baseDeTest.ExecuterAsync(
            "UPDATE dbo.ListeEpicerie SET Statut = 'Terminee', CleActive = Id, DateTerminaison = SYSUTCDATETIME();");

        Assert.False(await _depot.CocherAsync(ajout.ArticleId, coche: true, _idMarie));
    }

    // --- Terminer la liste (story 6.3)

    /// <summary>Liste générée avec lait et farine (des repas), plus du papier de toilette (manuel) ; le lait est coché.</summary>
    private async Task<(int ListeId, int Lait, int Farine, int Papier)> ListeEnCoursAsync()
    {
        var crepes = await RecetteAsync("Crêpes", 4, Ligne(_lait, 500m, "ml"), Ligne(_farine, 200m, "g"));
        await PlanifierAsync(Dimanche, Plage.Diner, crepes, 4);
        var generation = await _depot.GenererAsync(Dimanche, Samedi, _idMarie);
        var papier = await _depot.AjouterManuelAsync(new AjoutManuel(await IngredientAsync("Papier de toilette"), 2m, Unite("unité")), _idMarie);
        var lait = await ArticleAsync(_lait);
        await _depot.CocherAsync(lait.Id, coche: true, _idMarie);
        return (generation.ListeId!.Value, lait.Id, (await ArticleAsync(_farine)).Id, papier.ArticleId);
    }

    [Fact]
    public async Task Terminer_ReporteLesArticlesChoisisSurUneNouvelleListe()
    {
        var (listeId, _, farine, papier) = await ListeEnCoursAsync();

        var resultat = await _depot.TerminerAsync(listeId, [farine, papier], _idMarie);

        Assert.Equal(StatutTerminaison.Terminee, resultat.Statut);
        Assert.Equal(2, resultat.NbReportes);
        var (nouvelle, articles) = await ActiveAsync();
        Assert.Equal((resultat.NouvelleListeId, DepotListeEpicerie.NomProchaineListe, false), (nouvelle.Id, nouvelle.Nom, nouvelle.EstGeneree));
        Assert.NotEqual(listeId, nouvelle.Id);

        // Les copies gardent la quantité et l'unité, portent « reporté » et ne sont pas cochées ; le papier reste manuel.
        Assert.Equal(
            [(_farine, 200m, "g", false), (await IngredientAsync("Papier de toilette"), 2m, "unité", true)],
            articles.OrderBy(a => a.IngredientId == _farine ? 0 : 1).Select(a => (a.IngredientId, a.Quantite!.Value, a.CodeUnite!, a.EstManuel)));
        Assert.All(articles, a => Assert.True(a.EstReporte && !a.EstCoche && a.PeutEtreRetire));
        Assert.Equal(2, await _baseDeTest.ScalaireAsync<int>(
            $"SELECT COUNT(*) FROM dbo.ListeEpicerieArticle WHERE ListeId = {nouvelle.Id} AND ReporteDeArticleId IN ({farine}, {papier});"));
    }

    [Fact]
    public async Task Terminer_LaListeTermineeGardeSesArticles()
    {
        var (listeId, lait, farine, _) = await ListeEnCoursAsync();

        await _depot.TerminerAsync(listeId, [farine], _idMarie);

        Assert.Equal(1, await _baseDeTest.ScalaireAsync<int>(
            $"SELECT COUNT(*) FROM dbo.ListeEpicerie WHERE Id = {listeId} AND Statut = 'Terminee' AND CleActive = Id AND DateTerminaison IS NOT NULL;"));
        Assert.Equal(3, await _baseDeTest.ScalaireAsync<int>($"SELECT COUNT(*) FROM dbo.ListeEpicerieArticle WHERE ListeId = {listeId};"));
        Assert.Equal(1, await _baseDeTest.ScalaireAsync<int>($"SELECT COUNT(*) FROM dbo.ListeEpicerieArticle WHERE Id = {lait} AND EstCoche = 1;"));
    }

    [Fact]
    public async Task Terminer_SansReport_NeCreePasDeNouvelleListe()
    {
        var (listeId, _, _, _) = await ListeEnCoursAsync();

        var resultat = await _depot.TerminerAsync(listeId, [], _idMarie);

        Assert.Equal(new ResultatTerminaison(StatutTerminaison.Terminee, listeId, null, 0), resultat);
        Assert.Null(await _depot.TrouverActiveAsync());
    }

    [Fact]
    public async Task Terminer_UnArticleCocheNEstPasReporte()
    {
        var (listeId, lait, farine, _) = await ListeEnCoursAsync();

        var resultat = await _depot.TerminerAsync(listeId, [lait, farine], _idMarie);

        Assert.Equal(1, resultat.NbReportes);
        Assert.Equal([_farine], (await ActiveAsync()).Articles.Select(a => a.IngredientId));
    }

    [Fact]
    public async Task LesArticlesReportes_RestentALaGenerationEtALaRegeneration()
    {
        var (listeId, _, farine, _) = await ListeEnCoursAsync();
        await _depot.TerminerAsync(listeId, [farine], _idMarie);
        var crepes = await RecetteAsync("Gaufres", 4, Ligne(_farine, 300m, "g"));
        await PlanifierAsync(Samedi.AddDays(1), Plage.Diner, crepes, 4);

        // La prochaine liste est générée pour la semaine suivante, puis regénérée.
        Assert.Equal(StatutGeneration.Generee, (await _depot.GenererAsync(Samedi.AddDays(1), Samedi.AddDays(7), _idMarie)).Statut);
        var regeneration = await _depot.RegenererAsync(Samedi.AddDays(1), Samedi.AddDays(7));

        Assert.Equal(1, regeneration.NbConserves);
        var farines = (await ActiveAsync()).Articles.Where(a => a.IngredientId == _farine).OrderBy(a => a.EstReporte).ToList();
        Assert.Equal([(300m, false), (200m, true)], farines.Select(a => (a.Quantite!.Value, a.EstReporte)));
    }

    [Fact]
    public async Task Terminer_UneListeQuiNEstPlusActive_NeFaitRien()
    {
        var (listeId, _, farine, _) = await ListeEnCoursAsync();
        await _depot.TerminerAsync(listeId, [farine], _idMarie);
        var nouvelle = (await ActiveAsync()).Liste.Id;

        // Un deuxième appareil termine la même liste, déjà terminée.
        var resultat = await _depot.TerminerAsync(listeId, [farine], _idMarie);

        Assert.Equal(StatutTerminaison.ListeNonActive, resultat.Statut);
        Assert.Equal(nouvelle, (await ActiveAsync()).Liste.Id);
        Assert.Single((await ActiveAsync()).Articles);
    }

    [Fact]
    public async Task UnArticleReporte_PeutEtreReporteDeNouveau()
    {
        var (listeId, _, farine, _) = await ListeEnCoursAsync();
        var premiere = await _depot.TerminerAsync(listeId, [farine], _idMarie);
        var reporte = Assert.Single((await ActiveAsync()).Articles);

        var seconde = await _depot.TerminerAsync(premiere.NouvelleListeId!.Value, [reporte.Id], _idMarie);

        Assert.Equal(1, seconde.NbReportes);
        Assert.Equal(1, await _baseDeTest.ScalaireAsync<int>(
            $"SELECT COUNT(*) FROM dbo.ListeEpicerieArticle WHERE ListeId = {seconde.NouvelleListeId} AND ReporteDeArticleId = {reporte.Id};"));
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
