using Repas.Domaine.Calendrier;
using Repas.Domaine.Recettes;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests;

/// <summary>Tests d'intégration du dépôt du calendrier.</summary>
public sealed class DepotCalendrierTests : IAsyncLifetime
{
    private static readonly DateOnly Lundi = new(2026, 11, 16);

    private BaseDeTest _baseDeTest = null!;
    private DepotCalendrier _depot = null!;
    private int _idMarie;
    private int _pateChinois;
    private int _soupe;

    public async Task InitializeAsync()
    {
        _baseDeTest = await BaseDeTest.CreerAvecSchemaAsync();
        var baseDeDonnees = new BaseDeDonnees(_baseDeTest.ChaineConnexion);
        _depot = new DepotCalendrier(baseDeDonnees);
        _idMarie = (await new DepotUtilisateurs(baseDeDonnees).AjouterAsync("marie@exemple.com", "Marie", "hash")).Id;

        var recettes = new DepotRecettes(baseDeDonnees);
        _pateChinois = await recettes.AjouterAsync(Recette("Pâté chinois"), _idMarie);
        _soupe = await recettes.AjouterAsync(Recette("Soupe aux légumes"), _idMarie);
    }

    public async Task DisposeAsync() => await _baseDeTest.DisposeAsync();

    private static FicheRecette Recette(string nom) =>
        new(0, nom, 6, null, null, null, SourceRecette.Manuelle, [], [], []);

    [Fact]
    public async Task Ajouter_PuisTrouver_RedonneLeRepasAvecLeNomDeLaRecette()
    {
        var id = await _depot.AjouterAsync(RepasPlanifie.PourRecette(Lundi, Plage.Souper, _pateChinois, 4), _idMarie);

        var repas = await _depot.TrouverAsync(id);

        // 4 portions planifiées ; la recette en donne 6.
        Assert.Equal(new RepasPlanifie(id, Lundi, Plage.Souper, _pateChinois, null, 4, "Pâté chinois", 6), repas);
        Assert.Equal("Pâté chinois", repas!.Titre);
    }

    [Fact]
    public async Task Ajouter_UnRepasLibre_GardeLeTexteSansEspacesAutour()
    {
        var id = await _depot.AjouterAsync(RepasPlanifie.Libre(Lundi, Plage.Diner, "  Restaurant  "), _idMarie);

        var repas = await _depot.TrouverAsync(id);

        Assert.Equal("Restaurant", repas!.TexteLibre);
        Assert.Null(repas.RecetteId);
        Assert.Null(repas.Portions);
        Assert.Equal("Restaurant", repas.Titre);
    }

    [Fact]
    public async Task Ajouter_GardeLAuteur()
    {
        var id = await _depot.AjouterAsync(RepasPlanifie.Libre(Lundi, Plage.Diner, "Restes"), _idMarie);

        Assert.Equal(_idMarie, await _baseDeTest.ScalaireAsync<int>($"SELECT CreePar FROM dbo.RepasPlanifie WHERE Id = {id};"));
    }

    [Fact]
    public async Task Lister_DonneLaPeriodeInclusivementEnOrdreDeDateEtDePlage()
    {
        var dimanche = Lundi.AddDays(6);
        await _depot.AjouterAsync(RepasPlanifie.Libre(Lundi.AddDays(-1), Plage.Souper, "Avant"), _idMarie);
        var souperLundi = await _depot.AjouterAsync(RepasPlanifie.PourRecette(Lundi, Plage.Souper, _pateChinois, 6), _idMarie);
        var dessertLundi = await _depot.AjouterAsync(RepasPlanifie.Libre(Lundi, Plage.Souper, "Gâteau"), _idMarie);
        var dinerLundi = await _depot.AjouterAsync(RepasPlanifie.PourRecette(Lundi, Plage.Diner, _soupe, 4), _idMarie);
        var dinerDimanche = await _depot.AjouterAsync(RepasPlanifie.Libre(dimanche, Plage.Diner, "Brunch"), _idMarie);
        await _depot.AjouterAsync(RepasPlanifie.Libre(dimanche.AddDays(1), Plage.Diner, "Après"), _idMarie);

        var semaine = await _depot.ListerAsync(Lundi, dimanche);

        // Dîner avant souper ; dans une même plage, l'ordre d'ajout.
        Assert.Equal([dinerLundi, souperLundi, dessertLundi, dinerDimanche], semaine.Select(r => r.Id));
        Assert.Equal(["Soupe aux légumes", "Pâté chinois", "Gâteau", "Brunch"], semaine.Select(r => r.Titre));
    }

    [Fact]
    public async Task Lister_PeriodeVide_DonneUneListeVide()
    {
        Assert.Empty(await _depot.ListerAsync(Lundi, Lundi.AddDays(6)));
    }

    [Fact]
    public async Task Modifier_DeplaceEtRemplaceLeRepas()
    {
        var id = await _depot.AjouterAsync(RepasPlanifie.PourRecette(Lundi, Plage.Souper, _pateChinois, 4), _idMarie);

        var deplace = RepasPlanifie.PourRecette(Lundi.AddDays(2), Plage.Diner, _soupe, 2) with { Id = id };
        Assert.True(await _depot.ModifierAsync(deplace));

        Assert.Equal(deplace with { NomRecette = "Soupe aux légumes", PortionsRecette = 6 }, await _depot.TrouverAsync(id));
    }

    [Fact]
    public async Task Modifier_DeRecetteARepasLibre()
    {
        var id = await _depot.AjouterAsync(RepasPlanifie.PourRecette(Lundi, Plage.Souper, _pateChinois, 4), _idMarie);

        Assert.True(await _depot.ModifierAsync(RepasPlanifie.Libre(Lundi, Plage.Souper, "Pizza commandée") with { Id = id }));

        var repas = await _depot.TrouverAsync(id);
        Assert.Equal(("Pizza commandée", (int?)null, (int?)null), (repas!.TexteLibre, repas.RecetteId, repas.Portions));
    }

    [Fact]
    public async Task Modifier_RepasInexistant_RetourneFalse()
    {
        Assert.False(await _depot.ModifierAsync(RepasPlanifie.Libre(Lundi, Plage.Diner, "Restes") with { Id = 99999 }));
    }

    [Fact]
    public async Task Supprimer_RetireLeRepas()
    {
        var id = await _depot.AjouterAsync(RepasPlanifie.Libre(Lundi, Plage.Diner, "Restes"), _idMarie);

        Assert.True(await _depot.SupprimerAsync(id));

        Assert.Null(await _depot.TrouverAsync(id));
        Assert.False(await _depot.SupprimerAsync(id));
    }

    [Fact]
    public async Task UnRepasInvalide_EstRefuseParLaBase()
    {
        // La validation du domaine le refuse d'abord ; la base reste le dernier rempart.
        await Assert.ThrowsAnyAsync<Exception>(
            () => _depot.AjouterAsync(new RepasPlanifie(0, Lundi, Plage.Diner, _soupe, "Et un texte", 4), _idMarie));

        Assert.Equal(0, await _baseDeTest.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.RepasPlanifie;"));
    }
}
