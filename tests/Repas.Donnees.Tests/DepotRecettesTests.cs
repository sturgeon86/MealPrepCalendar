using Repas.Domaine.Recettes;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests;

/// <summary>Tests d'intégration du dépôt des recettes, sur les données de base.</summary>
public sealed class DepotRecettesTests : IAsyncLifetime
{
    private BaseDeTest _baseDeTest = null!;
    private DepotRecettes _depot = null!;
    private int _idMarie;
    private int _farine;
    private int _lait;
    private int _sel;
    private int _tasse;
    private int _ml;

    public async Task InitializeAsync()
    {
        _baseDeTest = await BaseDeTest.CreerAvecSchemaAsync(avecDonneesDeBase: true);
        var baseDeDonnees = new BaseDeDonnees(_baseDeTest.ChaineConnexion);
        _depot = new DepotRecettes(baseDeDonnees);
        _idMarie = (await new DepotUtilisateurs(baseDeDonnees).AjouterAsync("marie@exemple.com", "Marie", "hash")).Id;

        var ingredients = await new DepotIngredients(baseDeDonnees).RechercherAsync(null);
        _farine = ingredients.Single(i => i.Nom == "Farine tout usage").Id;
        _lait = ingredients.Single(i => i.Nom == "Lait 2 %").Id;
        _sel = ingredients.Single(i => i.Nom == "Sel").Id;
        var unites = await new DepotUnites(baseDeDonnees).ListerAsync();
        _tasse = unites.Single(u => u.Code == "tasse").Id;
        _ml = unites.Single(u => u.Code == "ml").Id;
    }

    public async Task DisposeAsync() => await _baseDeTest.DisposeAsync();

    private FicheRecette Crepes() => new(
        0, "Crêpes", 4, 10, 20, "Laisser reposer la pâte.", SourceRecette.Manuelle,
        [
            new LigneIngredient(_farine, 1.5m, _tasse, "tamisée", false),
            new LigneIngredient(_lait, 500m, _ml, null, false),
            new LigneIngredient(_sel, null, null, "une pincée", true),
        ],
        ["Mélanger la farine et le lait.", "Cuire à la poêle."],
        ["Déjeuner", "Végétarien"]);

    private static FicheRecette SansNoms(FicheRecette recette) =>
        recette with { Ingredients = recette.Ingredients.Select(l => l with { NomIngredient = "" }).ToList() };

    [Fact]
    public async Task Ajouter_PuisTrouver_RedonneLaRecetteComplete()
    {
        var recette = Crepes();

        var id = await _depot.AjouterAsync(recette, _idMarie);
        var relue = await _depot.TrouverAsync(id);

        Assert.NotNull(relue);
        Assert.Equal(recette.Nom, relue.Nom);
        Assert.Equal(recette.Notes, relue.Notes);
        Assert.Equal((10, 20), (relue.TempsPreparationMin!.Value, relue.TempsCuissonMin!.Value));
        Assert.Equal(recette.Ingredients, SansNoms(relue).Ingredients);
        Assert.Equal(["Farine tout usage", "Lait 2 %", "Sel"], relue.Ingredients.Select(l => l.NomIngredient));
        Assert.Equal(recette.Etapes, relue.Etapes);
        Assert.Equal(["Déjeuner", "Végétarien"], relue.Etiquettes);
    }

    [Fact]
    public async Task Ajouter_GardeLAuteurEtLaSource()
    {
        var id = await _depot.AjouterAsync(Crepes(), _idMarie);

        Assert.Equal(1, await _baseDeTest.ScalaireAsync<int>(
            $"SELECT COUNT(*) FROM dbo.Recette WHERE Id = {id} AND CreePar = {_idMarie} AND Source = 'Manuelle';"));
    }

    [Fact]
    public async Task Modifier_RemplaceToutEtRespecteLeNouvelOrdre()
    {
        var id = await _depot.AjouterAsync(Crepes(), _idMarie);
        var original = Crepes();
        var modifiee = original with
        {
            Id = id,
            Nom = "Crêpes minces",
            Portions = 6,
            TempsPreparationMin = null,
            Notes = null,
            // Le sel monte en premier, le lait disparaît.
            Ingredients = [original.Ingredients[2], original.Ingredients[0] with { Quantite = 2m }],
            Etapes = ["Cuire à la poêle.", "Servir chaud."],
            Etiquettes = ["Déjeuner"],
        };

        Assert.NotNull(await _depot.ModifierAsync(modifiee));

        var relue = SansNoms((await _depot.TrouverAsync(id))!);
        Assert.Equal("Crêpes minces", relue.Nom);
        Assert.Equal(6, relue.Portions);
        Assert.Null(relue.TempsPreparationMin);
        Assert.Null(relue.Notes);
        Assert.Equal(modifiee.Ingredients, relue.Ingredients);
        Assert.Equal(modifiee.Etapes, relue.Etapes);
        Assert.Equal(["Déjeuner"], relue.Etiquettes);
    }

    [Fact]
    public async Task Modifier_MetAJourLaDateDeModification()
    {
        var id = await _depot.AjouterAsync(Crepes(), _idMarie);
        await _baseDeTest.ExecuterAsync($"UPDATE dbo.Recette SET DateModification = '2020-01-01' WHERE Id = {id};");

        await _depot.ModifierAsync(Crepes() with { Id = id });

        Assert.Equal(1, await _baseDeTest.ScalaireAsync<int>(
            $"SELECT COUNT(*) FROM dbo.Recette WHERE Id = {id} AND DateModification > '2025-01-01';"));
    }

    [Fact]
    public async Task Modifier_RecetteInexistante_RetourneNull()
    {
        Assert.Null(await _depot.ModifierAsync(Crepes() with { Id = 99999 }));
    }

    private const string PhotoA = "0123456789abcdef0123456789abcdef.jpg";
    private const string PhotoB = "fedcba9876543210fedcba9876543210.png";

    [Fact]
    public async Task LaPhotoEstEnregistreeEtRelueAvecLaRecette()
    {
        var id = await _depot.AjouterAsync(Crepes() with { PhotoChemin = PhotoA }, _idMarie);

        Assert.Equal(PhotoA, (await _depot.TrouverAsync(id))!.PhotoChemin);
        Assert.Equal(PhotoA, (await _depot.ListerAsync()).Single().PhotoChemin);
    }

    [Fact]
    public async Task Modifier_RetourneLaPhotoRemplacee()
    {
        var id = await _depot.AjouterAsync(Crepes() with { PhotoChemin = PhotoA }, _idMarie);

        var changement = await _depot.ModifierAsync(Crepes() with { Id = id, PhotoChemin = PhotoB });
        var retrait = await _depot.ModifierAsync(Crepes() with { Id = id, PhotoChemin = null });

        Assert.Equal(PhotoA, changement!.AnciennePhotoChemin);
        Assert.Equal(PhotoB, retrait!.AnciennePhotoChemin);
        Assert.Null((await _depot.TrouverAsync(id))!.PhotoChemin);
    }

    [Fact]
    public async Task Modifier_SansPhotoAuparavant_RetourneUneAnciennePhotoNulle()
    {
        var id = await _depot.AjouterAsync(Crepes(), _idMarie);

        var modification = await _depot.ModifierAsync(Crepes() with { Id = id, PhotoChemin = PhotoA });

        Assert.NotNull(modification);
        Assert.Null(modification.AnciennePhotoChemin);
    }

    [Fact]
    public async Task Trouver_RecetteInexistante_RetourneNull()
    {
        Assert.Null(await _depot.TrouverAsync(99999));
    }

    [Fact]
    public async Task LesEtiquettesSontPartageesEntreLesRecettesSansEgardALaCasse()
    {
        await _depot.AjouterAsync(Crepes(), _idMarie);
        await _depot.AjouterAsync(Crepes() with { Nom = "Gaufres", Etiquettes = ["DÉJEUNER", "Dessert"] }, _idMarie);

        Assert.Equal(["Déjeuner", "Dessert", "Végétarien"], await _depot.ListerEtiquettesAsync());
    }

    [Fact]
    public async Task Lister_DonneLeTempsTotalEtLesEtiquettes()
    {
        await _depot.AjouterAsync(Crepes(), _idMarie);
        await _depot.AjouterAsync(Crepes() with { Nom = "Bol de céréales", TempsPreparationMin = null, TempsCuissonMin = null, Etiquettes = [] }, _idMarie);

        var recettes = await _depot.ListerAsync();

        Assert.Equal(["Bol de céréales", "Crêpes"], recettes.Select(r => r.Nom));
        Assert.Null(recettes[0].TempsTotalMin);
        Assert.Equal("", recettes[0].Etiquettes);
        Assert.Equal(30, recettes[1].TempsTotalMin);
        Assert.Equal("Déjeuner, Végétarien", recettes[1].Etiquettes);
    }

    /// <summary>Trois recettes : crêpes (30 min), gaufres (50 min, sans lait), bol de céréales (sans temps, sans farine).</summary>
    private async Task AjouterTroisRecettesAsync()
    {
        await _depot.AjouterAsync(Crepes(), _idMarie);
        await _depot.AjouterAsync(
            Crepes() with
            {
                Nom = "Gaufres légères",
                TempsPreparationMin = 15,
                TempsCuissonMin = 35,
                Ingredients = [Crepes().Ingredients[0], Crepes().Ingredients[2]],
                Etiquettes = ["Dessert"],
            },
            _idMarie);
        await _depot.AjouterAsync(
            Crepes() with
            {
                Nom = "Bol de céréales",
                TempsPreparationMin = null,
                TempsCuissonMin = null,
                Ingredients = [Crepes().Ingredients[1]],
                Etiquettes = ["Déjeuner"],
            },
            _idMarie);
    }

    private async Task<List<string>> NomsAsync(CriteresRechercheRecettes criteres) =>
        (await _depot.RechercherAsync(criteres)).Select(r => r.Nom).ToList();

    [Fact]
    public async Task Rechercher_SansCritere_DonneToutesLesRecettes()
    {
        await AjouterTroisRecettesAsync();

        Assert.Equal(["Bol de céréales", "Crêpes", "Gaufres légères"], await NomsAsync(CriteresRechercheRecettes.Aucun));
    }

    [Theory]
    [InlineData("crepe")]   // sans accent ni pluriel complet
    [InlineData("CRÊPES")]
    [InlineData("rêp")]     // au milieu du nom
    public async Task Rechercher_ParNom_IgnoreCasseEtAccents(string texte)
    {
        await AjouterTroisRecettesAsync();

        Assert.Equal(["Crêpes"], await NomsAsync(new(Nom: texte)));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("[a-z]")]
    public async Task Rechercher_ParNom_LesCaracteresSpeciauxSontCherchesTelsQuels(string texte)
    {
        await AjouterTroisRecettesAsync();

        Assert.Empty(await NomsAsync(new(Nom: texte)));
    }

    [Fact]
    public async Task Rechercher_ParIngredient_TrouveLesRecettesQuiLeContiennent()
    {
        await AjouterTroisRecettesAsync();

        Assert.Equal(["Crêpes", "Gaufres légères"], await NomsAsync(new(Ingredient: "farine")));
        Assert.Equal(["Bol de céréales", "Crêpes"], await NomsAsync(new(Ingredient: "LAIT")));
    }

    [Fact]
    public async Task Rechercher_ParIngredient_TrouveAussiParSynonyme()
    {
        await AjouterTroisRecettesAsync();
        await _baseDeTest.ExecuterAsync(
            $"INSERT INTO dbo.IngredientSynonyme (IngredientId, Synonyme) VALUES ({_farine}, N'Fleur de blé');");

        Assert.Equal(["Crêpes", "Gaufres légères"], await NomsAsync(new(Ingredient: "fleur de ble")));
    }

    [Fact]
    public async Task Rechercher_ParEtiquette_SansEgardALaCasse()
    {
        await AjouterTroisRecettesAsync();

        Assert.Equal(["Bol de céréales", "Crêpes"], await NomsAsync(new(Etiquette: "déjeuner")));
        Assert.Equal(["Gaufres légères"], await NomsAsync(new(Etiquette: "Dessert")));
        Assert.Empty(await NomsAsync(new(Etiquette: "Dé")));  // nom exact, pas une partie
    }

    [Fact]
    public async Task Rechercher_ParTempsTotal_ExclutLesRecettesSansTemps()
    {
        await AjouterTroisRecettesAsync();

        Assert.Equal(["Crêpes"], await NomsAsync(new(TempsTotalMaxMin: 30)));   // 30 min tout juste
        Assert.Equal(["Crêpes", "Gaufres légères"], await NomsAsync(new(TempsTotalMaxMin: 60)));
        Assert.Empty(await NomsAsync(new(TempsTotalMaxMin: 15)));
    }

    [Fact]
    public async Task Rechercher_LesCriteresSeCumulent()
    {
        await AjouterTroisRecettesAsync();

        Assert.Equal(["Crêpes"], await NomsAsync(new(Ingredient: "lait", Etiquette: "Déjeuner", TempsTotalMaxMin: 60)));
        Assert.Empty(await NomsAsync(new(Nom: "gaufres", Ingredient: "lait")));
    }

    [Fact]
    public async Task Rechercher_ApresUneFusion_TrouveParLeNouvelIngredient()
    {
        await AjouterTroisRecettesAsync();
        var ingredients = new DepotIngredients(new BaseDeDonnees(_baseDeTest.ChaineConnexion));
        var sucre = (await ingredients.RechercherAsync("Sucre blanc")).Single(i => i.Nom == "Sucre blanc").Id;

        // Le sel des crêpes et des gaufres devient du sucre blanc (la fusion remplace l'ingrédient dans les recettes).
        await ingredients.FusionnerAsync(_sel, sucre);

        Assert.Equal(["Crêpes", "Gaufres légères"], await NomsAsync(new(Ingredient: "sucre blanc")));
        Assert.Empty(await NomsAsync(new(Ingredient: "sel")));
    }

    [Fact]
    public async Task UneEcritureEnEchec_NeLaisseAucuneRecetteAMoitie()
    {
        // Un ingrédient inexistant fait échouer l'insertion de la deuxième ligne.
        var invalide = Crepes() with { Ingredients = [Crepes().Ingredients[0], new LigneIngredient(99999, 1m, _tasse, null, false)] };

        await Assert.ThrowsAnyAsync<Exception>(() => _depot.AjouterAsync(invalide, _idMarie));

        Assert.Equal(0, await _baseDeTest.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.Recette;"));
        Assert.Equal(0, await _baseDeTest.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.Etiquette;"));
    }
}
