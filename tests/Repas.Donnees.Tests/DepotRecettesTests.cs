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

        Assert.True(await _depot.ModifierAsync(modifiee));

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
    public async Task Modifier_RecetteInexistante_RetourneFalse()
    {
        Assert.False(await _depot.ModifierAsync(Crepes() with { Id = 99999 }));
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
