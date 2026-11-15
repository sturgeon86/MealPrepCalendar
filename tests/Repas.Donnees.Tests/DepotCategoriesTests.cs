using Repas.Domaine.Mesures;
using Repas.Domaine.Referentiel;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests;

/// <summary>Tests d'intégration des dépôts de catégories et d'unités, sur les données de base.</summary>
public sealed class DepotCategoriesTests : IAsyncLifetime
{
    private BaseDeTest _baseDeTest = null!;
    private DepotCategories _depot = null!;

    public async Task InitializeAsync()
    {
        _baseDeTest = await BaseDeTest.CreerAvecSchemaAsync(avecDonneesDeBase: true);
        _depot = new DepotCategories(new BaseDeDonnees(_baseDeTest.ChaineConnexion));
    }

    public async Task DisposeAsync() => await _baseDeTest.DisposeAsync();

    private async Task<List<string>> NomsAsync() => (await _depot.ListerAsync()).Select(c => c.Nom).ToList();

    [Fact]
    public async Task Lister_RespecteLOrdreDuMagasin()
    {
        var noms = await NomsAsync();

        Assert.Equal(12, noms.Count);
        Assert.Equal("Fruits et légumes", noms[0]);
        Assert.Equal("Hygiène", noms[^1]);
    }

    [Fact]
    public async Task Ajouter_PlaceLaCategorieALaFin()
    {
        var ajoutee = await _depot.AjouterAsync("  Collations ");

        Assert.Equal("Collations", ajoutee.Nom);
        Assert.Equal(130, ajoutee.OrdreGenerique);
        Assert.Equal("Collations", (await NomsAsync())[^1]);
    }

    [Fact]
    public async Task Ajouter_NomDejaUtilise_EstRefuseSansEgardALaCasse()
    {
        var erreur = await Assert.ThrowsAsync<NomCategorieDejaUtiliseException>(() => _depot.AjouterAsync("VIANDES"));

        Assert.Equal("VIANDES", erreur.Nom);
    }

    [Fact]
    public async Task Renommer_ChangeLeNom()
    {
        var viandes = (await _depot.ListerAsync()).Single(c => c.Nom == "Viandes");

        Assert.True(await _depot.RenommerAsync(viandes.Id, "Boucherie"));
        Assert.False(await _depot.RenommerAsync(9999, "Inexistante"));

        Assert.Contains("Boucherie", await NomsAsync());
    }

    [Fact]
    public async Task Renommer_VersLeNomDUneAutre_EstRefuse()
    {
        var viandes = (await _depot.ListerAsync()).Single(c => c.Nom == "Viandes");

        await Assert.ThrowsAsync<NomCategorieDejaUtiliseException>(() => _depot.RenommerAsync(viandes.Id, "Poissons"));
    }

    [Fact]
    public async Task Deplacer_EchangeAvecLaVoisine()
    {
        var poissons = (await _depot.ListerAsync()).Single(c => c.Nom == "Poissons");

        Assert.True(await _depot.DeplacerAsync(poissons.Id, SensDeplacement.Haut));
        Assert.Equal(["Fruits et légumes", "Boulangerie", "Poissons", "Viandes"], (await NomsAsync()).Take(4));

        Assert.True(await _depot.DeplacerAsync(poissons.Id, SensDeplacement.Bas));
        Assert.Equal(["Fruits et légumes", "Boulangerie", "Viandes", "Poissons"], (await NomsAsync()).Take(4));
    }

    [Fact]
    public async Task Deplacer_AuxExtremites_NeFaitRien()
    {
        var categories = await _depot.ListerAsync();

        Assert.False(await _depot.DeplacerAsync(categories[0].Id, SensDeplacement.Haut));
        Assert.False(await _depot.DeplacerAsync(categories[^1].Id, SensDeplacement.Bas));
        Assert.False(await _depot.DeplacerAsync(9999, SensDeplacement.Haut));

        Assert.Equal(categories, await _depot.ListerAsync());
    }

    [Fact]
    public async Task Unites_SontListeesParTypePuisParGrandeur()
    {
        var unites = await new DepotUnites(new BaseDeDonnees(_baseDeTest.ChaineConnexion)).ListerAsync();

        Assert.Equal(11, unites.Count);
        Assert.Equal(["g", "oz", "lb", "kg"], unites.Where(u => u.TypeMesure == TypeMesure.Poids).Select(u => u.Code));
        Assert.Equal(250m, unites.Single(u => u.Code == "tasse").FacteurVersReference);
    }
}
