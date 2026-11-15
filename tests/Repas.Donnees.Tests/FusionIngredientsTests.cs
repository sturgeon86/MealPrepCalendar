using Microsoft.Data.SqlClient;
using Repas.Domaine.Mesures;
using Repas.Domaine.Referentiel;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests;

/// <summary>Tests d'intégration de la fusion de doublons, sur les données de base.</summary>
public sealed class FusionIngredientsTests : IAsyncLifetime
{
    private BaseDeTest _baseDeTest = null!;
    private BaseDeDonnees _base = null!;
    private DepotIngredients _depot = null!;
    private int _idProduitsLaitiers;
    private int _idMillilitre;

    public async Task InitializeAsync()
    {
        _baseDeTest = await BaseDeTest.CreerAvecSchemaAsync(avecDonneesDeBase: true);
        _base = new BaseDeDonnees(_baseDeTest.ChaineConnexion);
        _depot = new DepotIngredients(_base);
        _idProduitsLaitiers = (await new DepotCategories(_base).ListerAsync()).Single(c => c.Nom == "Produits laitiers").Id;
        _idMillilitre = (await new DepotUnites(_base).ListerAsync()).Single(u => u.Code == "ml").Id;
    }

    public async Task DisposeAsync() => await _baseDeTest.DisposeAsync();

    private Task<int> AjouterAsync(string nom, params string[] synonymes) => _depot.AjouterAsync(
        new FicheIngredient(0, nom, _idProduitsLaitiers, TypeMesure.Volume, _idMillilitre, 1.03m, null, true, true, synonymes));

    private async Task<int> IdAsync(string nom) => Assert.Single(await _depot.RechercherAsync(nom, inclureInactifs: true), r => r.Nom == nom).Id;

    [Fact]
    public async Task Fusionner_ReporteLesSynonymesEtDesactiveLeDoublon()
    {
        var conserve = await IdAsync("Lait 2 %");
        var doublon = await AjouterAsync("lait 2%", "Lait partiellement écrémé", "Lait deux pour cent");

        var deplaces = await _depot.FusionnerAsync(doublon, conserve);

        Assert.Equal(2, deplaces);
        var ficheConservee = await _depot.TrouverAsync(conserve);
        Assert.Equal(["Lait partiellement écrémé", "Lait deux pour cent"], ficheConservee!.Synonymes);
        Assert.True(ficheConservee.Actif);

        var ficheDoublon = await _depot.TrouverAsync(doublon);
        Assert.False(ficheDoublon!.Actif);
        Assert.Empty(ficheDoublon.Synonymes);
        Assert.Equal(new CibleFusion(conserve, "Lait 2 %"), await _depot.TrouverCibleFusionAsync(doublon));
    }

    [Fact]
    public async Task Fusionner_AucuneDonneeNEstPerdue()
    {
        var conserve = await IdAsync("Lait 2 %");
        var doublon = await AjouterAsync("lait 2%", "Lait partiellement écrémé");
        var avant = await _baseDeTest.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.IngredientSynonyme;");

        await _depot.FusionnerAsync(doublon, conserve);

        // Mêmes synonymes au total, et le doublon existe toujours : son nom reste connu.
        Assert.Equal(avant, await _baseDeTest.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.IngredientSynonyme;"));
        Assert.Equal("lait 2%", (await _depot.TrouverAsync(doublon))!.Nom);
    }

    [Fact]
    public async Task Fusionner_LaRechercheMeneALIngredientConserve()
    {
        var conserve = await IdAsync("Lait 2 %");
        var doublon = await AjouterAsync("lait 2%", "Lait partiellement écrémé");

        await _depot.FusionnerAsync(doublon, conserve);

        // Le synonyme mène maintenant à l'ingrédient conservé ; le doublon n'apparaît qu'avec les désactivés.
        Assert.Equal(["Lait 2 %"], (await _depot.RechercherAsync("écrémé")).Select(r => r.Nom));
        var resume = Assert.Single(await _depot.RechercherAsync("lait 2%", inclureInactifs: true), r => r.Id == doublon);
        Assert.Equal("Lait 2 %", resume.FusionneDans);
    }

    [Fact]
    public async Task Fusionner_LesAnciensDoublonsSuiventVersLeNouvelIngredient()
    {
        // A est fusionné dans B, puis B dans C : A doit pointer directement vers C.
        var a = await AjouterAsync("Lait 2 pourcent");
        var b = await AjouterAsync("lait 2%");
        var c = await IdAsync("Lait 2 %");

        await _depot.FusionnerAsync(a, b);
        await _depot.FusionnerAsync(b, c);

        Assert.Equal(c, (await _depot.TrouverCibleFusionAsync(a))!.Id);
        Assert.Equal(c, (await _depot.TrouverCibleFusionAsync(b))!.Id);
    }

    [Fact]
    public async Task Fusionner_RegleNonRespectee_EstRefuseeSansRienModifier()
    {
        var conserve = await IdAsync("Lait 2 %");
        var doublon = await AjouterAsync("lait 2%", "Lait partiellement écrémé");
        await _depot.FusionnerAsync(doublon, conserve);

        // Le doublon est maintenant inactif et fusionné : il ne peut pas être gardé.
        var autre = await AjouterAsync("Lait 2 % sans lactose", "Lait Lactantia");
        var erreur = await Assert.ThrowsAsync<FusionImpossibleException>(() => _depot.FusionnerAsync(autre, doublon));

        Assert.Contains("doit être actif", erreur.Message);
        var ficheAutre = await _depot.TrouverAsync(autre);
        Assert.True(ficheAutre!.Actif);
        Assert.Equal(["Lait Lactantia"], ficheAutre.Synonymes);
    }

    [Fact]
    public async Task Fusionner_AvecSoiMemeOuInexistant_EstRefuse()
    {
        var lait = await IdAsync("Lait 2 %");

        await Assert.ThrowsAsync<FusionImpossibleException>(() => _depot.FusionnerAsync(lait, lait));
        await Assert.ThrowsAsync<FusionImpossibleException>(() => _depot.FusionnerAsync(99999, lait));
        Assert.True((await _depot.TrouverAsync(lait))!.Actif);
    }

    [Fact]
    public async Task Reactiver_UnIngredientFusionne_AnnuleLeLienDeFusion()
    {
        var conserve = await IdAsync("Lait 2 %");
        var doublon = await AjouterAsync("lait 2%");
        await _depot.FusionnerAsync(doublon, conserve);

        var fiche = await _depot.TrouverAsync(doublon);
        Assert.True(await _depot.ModifierAsync(fiche! with { Actif = true }));

        Assert.Null(await _depot.TrouverCibleFusionAsync(doublon));
    }

    [Fact]
    public async Task UnIngredientFusionne_DoitResterInactif()
    {
        var conserve = await IdAsync("Lait 2 %");
        var doublon = await AjouterAsync("lait 2%");

        var erreur = await Assert.ThrowsAsync<SqlException>(() => _baseDeTest.ExecuterAsync(
            $"UPDATE dbo.Ingredient SET FusionneDansId = {conserve} WHERE Id = {doublon};"));

        Assert.Contains("CK_Ingredient_FusionneDans_Inactif", erreur.Message);
    }

    [Fact]
    public async Task LaFusionTraiteToutesLesTablesQuiReferencentUnIngredient()
    {
        // Garde-fou : une nouvelle table qui référence Ingredient (recettes, listes…) doit être
        // reportée par FusionnerAsync et ajoutée à DepotIngredients.ReferencesTraiteesParFusion.
        var references = await _baseDeTest.ScalaireAsync<string>(
            """
            SELECT STRING_AGG(OBJECT_NAME(fkc.parent_object_id) + N'.' + COL_NAME(fkc.parent_object_id, fkc.parent_column_id), N'|')
            FROM sys.foreign_key_columns fkc
            WHERE fkc.referenced_object_id = OBJECT_ID(N'dbo.Ingredient');
            """);

        Assert.Equal(
            DepotIngredients.ReferencesTraiteesParFusion.Order(StringComparer.Ordinal),
            references.Split('|').Order(StringComparer.Ordinal));
    }
}
