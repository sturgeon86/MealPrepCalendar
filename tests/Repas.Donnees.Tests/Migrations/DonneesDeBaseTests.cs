using Repas.Donnees.Migrations;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests.Migrations;

/// <summary>Tests d'intégration des données de base (<c>db/seed</c>), appliquées sur le vrai schéma.</summary>
public sealed class DonneesDeBaseTests : IAsyncLifetime
{
    private BaseDeTest _base = null!;

    public async Task InitializeAsync() => _base = await BaseDeTest.CreerAvecSchemaAsync(avecDonneesDeBase: true);

    public async Task DisposeAsync() => await _base.DisposeAsync();

    private Task<int> CompterAsync(string sql) => _base.ScalaireAsync<int>(sql);

    private async Task<(int Categories, int Unites, int Ingredients)> CompterToutAsync() => (
        await CompterAsync("SELECT COUNT(*) FROM dbo.Categorie;"),
        await CompterAsync("SELECT COUNT(*) FROM dbo.Unite;"),
        await CompterAsync("SELECT COUNT(*) FROM dbo.Ingredient;"));

    [Fact]
    public async Task LesDonneesDeBaseSontInserees()
    {
        var (categories, unites, ingredients) = await CompterToutAsync();

        Assert.Equal(12, categories);
        Assert.Equal(11, unites);
        Assert.InRange(ingredients, 100, 200);
    }

    [Fact]
    public async Task LesCategoriesSuiventLOrdreDuMagasin()
    {
        Assert.Equal(1, await CompterAsync(
            "SELECT COUNT(*) FROM dbo.Categorie WHERE Nom = N'Fruits et légumes' AND OrdreGenerique = (SELECT MIN(OrdreGenerique) FROM dbo.Categorie);"));
        Assert.Equal(0, await CompterAsync(
            "SELECT COUNT(*) FROM (SELECT OrdreGenerique FROM dbo.Categorie GROUP BY OrdreGenerique HAVING COUNT(*) > 1) AS doublons;"));
    }

    [Fact]
    public async Task ChaqueTypeDeMesureASonUniteDeBaseDeFacteurUn()
    {
        Assert.Equal(3, await CompterAsync(
            """
            SELECT COUNT(*) FROM dbo.Unite
            WHERE FacteurVersReference = 1
              AND ((Code = N'g' AND TypeMesure = 'Poids')
                OR (Code = N'ml' AND TypeMesure = 'Volume')
                OR (Code = N'unité' AND TypeMesure = 'Unite'));
            """));
    }

    [Theory]
    [InlineData("kg", 1000)]
    [InlineData("tasse", 250)]
    [InlineData("c. à soupe", 15)]
    [InlineData("c. à thé", 5)]
    [InlineData("douzaine", 12)]
    public async Task LesFacteursDesUnitesCourantesSontJustes(string code, int facteur)
    {
        var lu = await _base.ScalaireAsync<decimal>($"SELECT FacteurVersReference FROM dbo.Unite WHERE Code = N'{code}';");

        Assert.Equal(facteur, lu);
    }

    [Fact]
    public async Task ChaqueIngredientALUniteDeBaseDeSonTypeCommeReference()
    {
        Assert.Equal(0, await CompterAsync(
            """
            SELECT COUNT(*) FROM dbo.Ingredient i
            JOIN dbo.Unite u ON u.Id = i.UniteReferenceId
            WHERE u.FacteurVersReference <> 1;
            """));
    }

    [Fact]
    public async Task LaFarineSuitLExempleDeLaSpecification()
    {
        // Spécification : 1 tasse de farine = 125 g, avec une tasse de 250 ml.
        var grammesParTasse = await _base.ScalaireAsync<decimal>(
            """
            SELECT i.DensiteGParMl * u.FacteurVersReference
            FROM dbo.Ingredient i CROSS JOIN dbo.Unite u
            WHERE i.Nom = N'Farine tout usage' AND u.Code = N'tasse';
            """);

        Assert.Equal(125m, grammesParTasse);
    }

    [Fact]
    public async Task LesArticlesNonAlimentairesSontMarquesCommeTels()
    {
        // Non alimentaire si et seulement si l'article est dans Entretien ménager ou Hygiène.
        Assert.Equal(0, await CompterAsync(
            """
            SELECT COUNT(*) FROM dbo.Ingredient i JOIN dbo.Categorie c ON c.Id = i.CategorieId
            WHERE CASE WHEN c.Nom IN (N'Entretien ménager', N'Hygiène') THEN 0 ELSE 1 END <> i.EstAlimentaire;
            """));
        Assert.Equal(1, await CompterAsync(
            "SELECT COUNT(*) FROM dbo.Ingredient WHERE Nom = N'Papier de toilette' AND EstAlimentaire = 0;"));
    }

    [Fact]
    public async Task UneDeuxiemeApplicationNeFaitRien()
    {
        var avant = await CompterToutAsync();

        var appliques = await new ExecuteurMigrations(
            _base.ChaineConnexion, BaseDeTest.DossierDonneesDeBase, ExecuteurMigrations.TableDonneesDeBase).AppliquerAsync();

        Assert.Empty(appliques);
        Assert.Equal(avant, await CompterToutAsync());
    }

    [Fact]
    public async Task RejouerLesScriptsNeCreeAucunDoublon()
    {
        // Même si le suivi était perdu, chaque script vérifie l'existence avant d'insérer.
        var avant = await CompterToutAsync();

        foreach (var script in Directory.GetFiles(BaseDeTest.DossierDonneesDeBase, "*.sql").Order())
        {
            await _base.ExecuterAsync(await File.ReadAllTextAsync(script));
        }

        Assert.Equal(avant, await CompterToutAsync());
    }

    [Fact]
    public async Task UneDonneeModifieeParLaFamilleNEstPasEcrasee()
    {
        await _base.ExecuterAsync(
            "UPDATE dbo.Ingredient SET PoidsMoyenUniteG = 180 WHERE Nom = N'Oignon jaune';");

        foreach (var script in Directory.GetFiles(BaseDeTest.DossierDonneesDeBase, "*.sql").Order())
        {
            await _base.ExecuterAsync(await File.ReadAllTextAsync(script));
        }

        Assert.Equal(180m, await _base.ScalaireAsync<decimal>(
            "SELECT PoidsMoyenUniteG FROM dbo.Ingredient WHERE Nom = N'Oignon jaune';"));
    }
}
