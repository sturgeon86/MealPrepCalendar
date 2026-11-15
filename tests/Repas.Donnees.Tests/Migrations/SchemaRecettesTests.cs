using Microsoft.Data.SqlClient;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests.Migrations;

/// <summary>
/// Vérifie que les contraintes de la migration 0004 (recettes) protègent les données,
/// sur une base où les vraies migrations et les données de base sont appliquées.
/// </summary>
public sealed class SchemaRecettesTests : IAsyncLifetime
{
    private BaseDeTest _base = null!;

    public async Task InitializeAsync()
    {
        _base = await BaseDeTest.CreerAvecSchemaAsync(avecDonneesDeBase: true);
        await _base.ExecuterAsync(
            """
            SET IDENTITY_INSERT dbo.Utilisateur ON;
            INSERT INTO dbo.Utilisateur (Id, Courriel, NomAffiche, MotDePasseHash) VALUES (1, N'marie@exemple.com', N'Marie', N'hash');
            SET IDENTITY_INSERT dbo.Utilisateur OFF;

            SET IDENTITY_INSERT dbo.Recette ON;
            INSERT INTO dbo.Recette (Id, Nom, Portions, CreePar) VALUES (1, N'Pâté chinois', 6, 1);
            SET IDENTITY_INSERT dbo.Recette OFF;
            """);
    }

    public async Task DisposeAsync() => await _base.DisposeAsync();

    private Task<int> CompterAsync(string table) => _base.ScalaireAsync<int>($"SELECT COUNT(*) FROM dbo.{table};");

    private Task AjouterLigneAsync(int ordre, string quantite = "1", string unite = "(SELECT Id FROM dbo.Unite WHERE Code = N'tasse')") =>
        _base.ExecuterAsync(
            $"""
            INSERT INTO dbo.RecetteIngredient (RecetteId, IngredientId, Quantite, UniteId, Ordre)
            VALUES (1, (SELECT Id FROM dbo.Ingredient WHERE Nom = N'Farine tout usage'), {quantite}, {unite}, {ordre});
            """);

    private static async Task<string> ErreurAsync(Func<Task> action) => (await Assert.ThrowsAsync<SqlException>(action)).Message;

    [Fact]
    public async Task UneRecetteValide_ARecuSesValeursParDefaut()
    {
        Assert.Equal(1, await _base.ScalaireAsync<int>(
            """
            SELECT COUNT(*) FROM dbo.Recette
            WHERE Source = 'Manuelle' AND DateCreation IS NOT NULL AND DateModification IS NOT NULL
              AND PhotoChemin IS NULL AND TempsPreparationMin IS NULL;
            """));
    }

    [Theory]
    [InlineData("Portions = 0", "CK_Recette_Portions")]
    [InlineData("TempsPreparationMin = -5", "CK_Recette_TempsPreparationMin")]
    [InlineData("TempsCuissonMin = -1", "CK_Recette_TempsCuissonMin")]
    [InlineData("Source = 'Papier'", "CK_Recette_Source")]
    [InlineData("CreePar = 999", "FK_Recette_Utilisateur")]
    public async Task ValeurInvalideDansUneRecette_EstRefusee(string affectation, string contrainte)
    {
        Assert.Contains(contrainte, await ErreurAsync(() => _base.ExecuterAsync($"UPDATE dbo.Recette SET {affectation} WHERE Id = 1;")));
    }

    [Fact]
    public async Task UneLigneDIngredient_AvecQuantiteEtUnite_EstAcceptee()
    {
        await AjouterLigneAsync(1, quantite: "2.5");

        Assert.Equal(1, await CompterAsync("RecetteIngredient WHERE EstOptionnel = 0 AND Quantite = 2.5"));
    }

    [Fact]
    public async Task UneLigneAuGout_SansQuantiteNiUnite_EstAcceptee()
    {
        await AjouterLigneAsync(1, quantite: "NULL", unite: "NULL");

        Assert.Equal(1, await CompterAsync("RecetteIngredient"));
    }

    [Theory]
    [InlineData("NULL", "(SELECT Id FROM dbo.Unite WHERE Code = N'tasse')")]
    [InlineData("2", "NULL")]
    public async Task QuantiteSansUniteOuUniteSansQuantite_EstRefusee(string quantite, string unite)
    {
        Assert.Contains("CK_RecetteIngredient_QuantiteEtUnite", await ErreurAsync(() => AjouterLigneAsync(1, quantite, unite)));
    }

    [Fact]
    public async Task QuantiteNulleOuNegative_EstRefusee()
    {
        Assert.Contains("CK_RecetteIngredient_Quantite", await ErreurAsync(() => AjouterLigneAsync(1, quantite: "0")));
    }

    [Fact]
    public async Task DeuxLignesAuMemeRang_SontRefusees()
    {
        await AjouterLigneAsync(1);

        Assert.Contains("UQ_RecetteIngredient_Ordre", await ErreurAsync(() => AjouterLigneAsync(1)));
    }

    [Fact]
    public async Task LeMemeIngredientPeutParaitreDeuxFois()
    {
        // Ex. : du beurre pour la pâte et du beurre pour la garniture.
        await AjouterLigneAsync(1);
        await AjouterLigneAsync(2);

        Assert.Equal(2, await CompterAsync("RecetteIngredient"));
    }

    [Fact]
    public async Task DeuxEtapesAuMemeRang_SontRefusees()
    {
        await _base.ExecuterAsync("INSERT INTO dbo.RecetteEtape (RecetteId, Ordre, Texte) VALUES (1, 1, N'Cuire le bœuf.');");

        Assert.Contains("UQ_RecetteEtape_Ordre", await ErreurAsync(() => _base.ExecuterAsync(
            "INSERT INTO dbo.RecetteEtape (RecetteId, Ordre, Texte) VALUES (1, 1, N'Ajouter le maïs.');")));
    }

    [Fact]
    public async Task UnIngredientUtiliseDansUneRecette_NePeutPasEtreSupprime()
    {
        await AjouterLigneAsync(1);

        Assert.Contains("FK_RecetteIngredient_Ingredient", await ErreurAsync(() => _base.ExecuterAsync(
            "DELETE FROM dbo.Ingredient WHERE Nom = N'Farine tout usage';")));
    }

    [Fact]
    public async Task EtiquetteEnDouble_EstRefuseeSansEgardALaCasse()
    {
        await _base.ExecuterAsync("INSERT INTO dbo.Etiquette (Nom) VALUES (N'Végétarien');");

        Assert.Contains("UQ_Etiquette_Nom", await ErreurAsync(() => _base.ExecuterAsync(
            "INSERT INTO dbo.Etiquette (Nom) VALUES (N'VÉGÉTARIEN');")));
    }

    [Fact]
    public async Task SupprimerUneRecette_SupprimeSesLignesEtapesEtEtiquettesMaisPasLesEtiquettes()
    {
        await AjouterLigneAsync(1);
        await _base.ExecuterAsync(
            """
            INSERT INTO dbo.RecetteEtape (RecetteId, Ordre, Texte) VALUES (1, 1, N'Cuire le bœuf.');
            INSERT INTO dbo.Etiquette (Nom) VALUES (N'Souper rapide');
            INSERT INTO dbo.RecetteEtiquette (RecetteId, EtiquetteId) VALUES (1, SCOPE_IDENTITY());
            """);

        await _base.ExecuterAsync("DELETE FROM dbo.Recette WHERE Id = 1;");

        Assert.Equal(0, await CompterAsync("RecetteIngredient"));
        Assert.Equal(0, await CompterAsync("RecetteEtape"));
        Assert.Equal(0, await CompterAsync("RecetteEtiquette"));
        Assert.Equal(1, await CompterAsync("Etiquette"));
    }

    [Fact]
    public async Task SupprimerUneEtiquette_LaRetireDesRecettes()
    {
        await _base.ExecuterAsync(
            """
            INSERT INTO dbo.Etiquette (Nom) VALUES (N'Souper rapide');
            INSERT INTO dbo.RecetteEtiquette (RecetteId, EtiquetteId) VALUES (1, SCOPE_IDENTITY());
            """);

        await _base.ExecuterAsync("DELETE FROM dbo.Etiquette;");

        Assert.Equal(0, await CompterAsync("RecetteEtiquette"));
        Assert.Equal(1, await CompterAsync("Recette"));
    }
}
