using Repas.Domaine.Mesures;
using Repas.Domaine.Referentiel;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests;

/// <summary>Tests d'intégration du dépôt des ingrédients, sur les données de base.</summary>
public sealed class DepotIngredientsTests : IAsyncLifetime
{
    private BaseDeTest _baseDeTest = null!;
    private DepotIngredients _depot = null!;
    private int _idViandes;
    private int _idEpicerie;
    private int _idGramme;
    private int _idMillilitre;

    public async Task InitializeAsync()
    {
        _baseDeTest = await BaseDeTest.CreerAvecSchemaAsync(avecDonneesDeBase: true);
        var baseDeDonnees = new BaseDeDonnees(_baseDeTest.ChaineConnexion);
        _depot = new DepotIngredients(baseDeDonnees);

        var categories = await new DepotCategories(baseDeDonnees).ListerAsync();
        _idViandes = categories.Single(c => c.Nom == "Viandes").Id;
        _idEpicerie = categories.Single(c => c.Nom == "Épicerie sèche").Id;
        var unites = await new DepotUnites(baseDeDonnees).ListerAsync();
        _idGramme = unites.Single(u => u.Code == "g").Id;
        _idMillilitre = unites.Single(u => u.Code == "ml").Id;
    }

    public async Task DisposeAsync() => await _baseDeTest.DisposeAsync();

    private FicheIngredient NouvelleFiche(string nom, params string[] synonymes) =>
        new(0, nom, _idEpicerie, TypeMesure.Poids, _idGramme, 0.6m, null, true, true, synonymes);

    private async Task<List<string>> ChercherAsync(string? texte, int? categorieId = null, bool inclureInactifs = false) =>
        (await _depot.RechercherAsync(texte, categorieId, inclureInactifs)).Select(r => r.Nom).ToList();

    // --- Recherche

    [Fact]
    public async Task Rechercher_SansTexte_ListeTousLesActifsParNom()
    {
        var noms = await ChercherAsync(null);

        // Le tri suit la collation de la base (l'apostrophe n'y est pas classée comme en .NET).
        Assert.Equal(133, noms.Count);
        Assert.Equal("Ail", noms[0]);
        Assert.Equal("Yogourt nature", noms[^1]);
    }

    [Fact]
    public async Task Rechercher_TrouveUnePartieDuNom()
    {
        var noms = await ChercherAsync("poulet");

        Assert.Equal(["Bouillon de poulet", "Cuisse de poulet désossée", "Poitrine de poulet", "Poulet entier"], noms);
    }

    [Theory]
    [InlineData("creme")]
    [InlineData("CRÈME")]
    public async Task Rechercher_IgnoreLaCasseEtLesAccents(string texte)
    {
        var noms = await ChercherAsync(texte);

        Assert.Contains("Crème 35 %", noms);
        Assert.Contains("Crème sure", noms);
    }

    [Fact]
    public async Task Rechercher_TrouveParSynonyme()
    {
        await _depot.AjouterAsync(NouvelleFiche("Quinoa", "Riz inca"));

        Assert.Equal(["Quinoa"], await ChercherAsync("inca"));
    }

    [Fact]
    public async Task Rechercher_FiltreParCategorie()
    {
        var resultats = await _depot.RechercherAsync(null, _idViandes);

        Assert.Equal(13, resultats.Count);
        Assert.All(resultats, r => Assert.Equal("Viandes", r.Categorie));
    }

    [Fact]
    public async Task Rechercher_CombineTexteEtCategorie()
    {
        Assert.Equal(["Bœuf haché", "Dinde hachée", "Porc haché"], await ChercherAsync("hach", _idViandes));
    }

    [Fact]
    public async Task Rechercher_LesCaracteresSpeciauxSontCherchesTelsQuels()
    {
        // « % » ne doit pas agir comme un joker : seuls les noms qui contiennent vraiment « 2 % ».
        Assert.Equal(["Lait 2 %"], await ChercherAsync("2 %"));
        Assert.Empty(await ChercherAsync("_"));
    }

    [Fact]
    public async Task Rechercher_ExclutLesInactifsSaufSurDemande()
    {
        var id = await _depot.AjouterAsync(NouvelleFiche("Quinoa") with { Actif = false });

        Assert.Empty(await ChercherAsync("quinoa"));
        var inclus = await _depot.RechercherAsync("quinoa", inclureInactifs: true);
        Assert.Equal(id, Assert.Single(inclus).Id);
        Assert.False(inclus[0].Actif);
    }

    [Fact]
    public async Task Rechercher_DonneLaCategorieLUniteEtLesSynonymes()
    {
        await _depot.AjouterAsync(NouvelleFiche("Quinoa", "Riz inca", "Graine de quinoa"));

        var quinoa = Assert.Single(await _depot.RechercherAsync("quinoa"));

        Assert.Equal("Épicerie sèche", quinoa.Categorie);
        Assert.Equal("g", quinoa.UniteReference);
        Assert.Equal("Graine de quinoa, Riz inca", quinoa.Synonymes);
    }

    // --- Ajout et modification

    [Fact]
    public async Task Ajouter_PuisTrouver_RedonneLaFicheComplete()
    {
        var fiche = NouvelleFiche("Quinoa", "Riz inca", "Graine de quinoa") with { PoidsMoyenUniteG = 12.5m };

        var id = await _depot.AjouterAsync(fiche);
        var relue = await _depot.TrouverAsync(id);

        Assert.NotNull(relue);
        Assert.Equal(fiche with { Id = id, Synonymes = [] }, relue with { Synonymes = [] });
        Assert.Equal(["Riz inca", "Graine de quinoa"], relue.Synonymes);
    }

    [Fact]
    public async Task Trouver_LesDecimalesNOntPasDeZerosDeFin()
    {
        var id = await _depot.AjouterAsync(NouvelleFiche("Quinoa") with { DensiteGParMl = 0.75m, PoidsMoyenUniteG = 150m });

        var relue = await _depot.TrouverAsync(id);

        // La colonne DECIMAL(10,4) rendrait « 0.7500 » ; l'affichage doit montrer « 0.75 ».
        Assert.Equal("0.75", relue!.DensiteGParMl!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("150", relue.PoidsMoyenUniteG!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Trouver_IngredientInexistant_RetourneNull()
    {
        Assert.Null(await _depot.TrouverAsync(99999));
    }

    [Fact]
    public async Task Modifier_RemplaceLaFicheEtSesSynonymes()
    {
        var id = await _depot.AjouterAsync(NouvelleFiche("Quinoa", "Riz inca", "Graine de quinoa"));
        var modifiee = new FicheIngredient(
            id, "Quinoa blanc", _idViandes, TypeMesure.Volume, _idMillilitre, null, 3m, false, false, ["Quinoa"]);

        Assert.True(await _depot.ModifierAsync(modifiee));

        Assert.Equal(modifiee, await _depot.TrouverAsync(id) is { } relue ? relue with { Synonymes = modifiee.Synonymes } : null);
        Assert.Equal(["Quinoa"], (await _depot.TrouverAsync(id))!.Synonymes);
    }

    [Fact]
    public async Task Modifier_IngredientInexistant_RetourneFalse()
    {
        Assert.False(await _depot.ModifierAsync(NouvelleFiche("Fantôme") with { Id = 99999 }));
    }

    [Fact]
    public async Task Modifier_PeutGarderSonPropreNom()
    {
        var id = await _depot.AjouterAsync(NouvelleFiche("Quinoa", "Riz inca"));

        Assert.True(await _depot.ModifierAsync(NouvelleFiche("Quinoa", "Riz inca") with { Id = id, DensiteGParMl = 0.7m }));
    }

    // --- Un texte mène à un seul ingrédient

    [Fact]
    public async Task Ajouter_NomDejaPris_EstRefuse()
    {
        var erreur = await Assert.ThrowsAsync<TexteIngredientDejaUtiliseException>(
            () => _depot.AjouterAsync(NouvelleFiche("farine TOUT usage")));

        Assert.Equal("Farine tout usage", erreur.IngredientExistant);
    }

    [Fact]
    public async Task Ajouter_SynonymeQuiEstLeNomDUnAutre_EstRefuseEtRienNEstEcrit()
    {
        var erreur = await Assert.ThrowsAsync<TexteIngredientDejaUtiliseException>(
            () => _depot.AjouterAsync(NouvelleFiche("Quinoa", "Riz basmati")));

        Assert.Equal("Riz basmati", erreur.Texte);
        Assert.Equal("Riz basmati", erreur.IngredientExistant);
        Assert.Empty(await ChercherAsync("quinoa", inclureInactifs: true));
    }

    [Fact]
    public async Task Ajouter_NomQuiEstLeSynonymeDUnAutre_EstRefuse()
    {
        await _depot.AjouterAsync(NouvelleFiche("Quinoa", "Riz inca"));

        var erreur = await Assert.ThrowsAsync<TexteIngredientDejaUtiliseException>(
            () => _depot.AjouterAsync(NouvelleFiche("riz INCA")));

        Assert.Equal("Quinoa", erreur.IngredientExistant);
    }

    [Fact]
    public async Task Modifier_SynonymePrisParUnAutre_EstRefuseEtLAncienneFicheReste()
    {
        await _depot.AjouterAsync(NouvelleFiche("Quinoa", "Riz inca"));
        var id = await _depot.AjouterAsync(NouvelleFiche("Boulgour", "Blé concassé"));

        await Assert.ThrowsAsync<TexteIngredientDejaUtiliseException>(
            () => _depot.ModifierAsync(NouvelleFiche("Boulgour fin", "Riz inca") with { Id = id }));

        var relue = await _depot.TrouverAsync(id);
        Assert.Equal("Boulgour", relue!.Nom);
        Assert.Equal(["Blé concassé"], relue.Synonymes);
    }
}
