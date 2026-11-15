using Microsoft.Data.SqlClient;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests.Migrations;

/// <summary>
/// Vérifie que les contraintes de la migration 0002 (référentiel d'ingrédients) protègent les données,
/// sur une base où les vraies migrations sont appliquées.
/// </summary>
public sealed class SchemaReferentielTests : IAsyncLifetime
{
    private BaseDeTest _base = null!;

    public async Task InitializeAsync()
    {
        _base = await BaseDeTest.CreerAvecSchemaAsync();
        await _base.ExecuterAsync(
            """
            SET IDENTITY_INSERT dbo.Categorie ON;
            INSERT INTO dbo.Categorie (Id, Nom, OrdreGenerique) VALUES (1, N'Viandes', 30), (2, N'Produits laitiers', 50);
            SET IDENTITY_INSERT dbo.Categorie OFF;

            SET IDENTITY_INSERT dbo.Unite ON;
            INSERT INTO dbo.Unite (Id, Code, Nom, TypeMesure, FacteurVersReference) VALUES
                (1, N'g', N'gramme', 'Poids', 1),
                (2, N'kg', N'kilogramme', 'Poids', 1000),
                (3, N'ml', N'millilitre', 'Volume', 1),
                (4, N'tasse', N'tasse', 'Volume', 250),
                (5, N'unité', N'unité', 'Unite', 1);
            SET IDENTITY_INSERT dbo.Unite OFF;
            """);
    }

    public async Task DisposeAsync() => await _base.DisposeAsync();

    private Task AjouterIngredientAsync(
        string nom, string typeMesure = "Poids", int uniteReferenceId = 1, string densite = "NULL", string poidsMoyen = "NULL")
    {
        return _base.ExecuterAsync(
            $"""
            INSERT INTO dbo.Ingredient (Nom, CategorieId, TypeMesure, UniteReferenceId, DensiteGParMl, PoidsMoyenUniteG)
            VALUES (N'{nom}', 1, '{typeMesure}', {uniteReferenceId}, {densite}, {poidsMoyen});
            """);
    }

    [Fact]
    public async Task UnIngredientValideEstAccepteAvecSesValeursParDefaut()
    {
        await AjouterIngredientAsync("Farine tout usage", "Volume", 3, densite: "0.53");

        Assert.Equal(1, await _base.ScalaireAsync<int>(
            "SELECT COUNT(*) FROM dbo.Ingredient WHERE EstAlimentaire = 1 AND Actif = 1 AND DensiteGParMl = 0.53;"));
    }

    [Fact]
    public async Task UniteDeReferenceDUnAutreTypeDeMesure_EstRefusee()
    {
        // Un ingrédient mesuré au poids ne peut pas avoir la tasse (volume) comme unité de référence.
        var erreur = await Assert.ThrowsAsync<SqlException>(() => AjouterIngredientAsync("Poulet", "Poids", uniteReferenceId: 4));

        Assert.Contains("FK_Ingredient_UniteReference_MemeType", erreur.Message);
    }

    [Fact]
    public async Task NomDIngredientEnDouble_EstRefuseSansEgardALaCasse()
    {
        await AjouterIngredientAsync("Poitrine de poulet");

        var erreur = await Assert.ThrowsAsync<SqlException>(() => AjouterIngredientAsync("POITRINE DE POULET"));

        Assert.Contains("UQ_Ingredient_Nom", erreur.Message);
    }

    [Fact]
    public async Task LesAccentsDistinguentDeuxNoms()
    {
        await AjouterIngredientAsync("Pâte");
        await AjouterIngredientAsync("Pâté");

        Assert.Equal(2, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.Ingredient;"));
    }

    [Theory]
    [InlineData("TypeMesure", "'Litres'")]
    [InlineData("DensiteGParMl", "0")]
    [InlineData("PoidsMoyenUniteG", "-150")]
    public async Task ValeurInvalide_EstRefusee(string colonne, string valeur)
    {
        var typeMesure = colonne == "TypeMesure" ? valeur : "'Poids'";
        var densite = colonne == "DensiteGParMl" ? valeur : "NULL";
        var poidsMoyen = colonne == "PoidsMoyenUniteG" ? valeur : "NULL";

        var erreur = await Assert.ThrowsAsync<SqlException>(() => _base.ExecuterAsync(
            $"""
            INSERT INTO dbo.Ingredient (Nom, CategorieId, TypeMesure, UniteReferenceId, DensiteGParMl, PoidsMoyenUniteG)
            VALUES (N'Essai', 1, {typeMesure}, 1, {densite}, {poidsMoyen});
            """));

        Assert.Contains($"CK_Ingredient_{colonne}", erreur.Message);
    }

    [Fact]
    public async Task CategorieInexistante_EstRefusee()
    {
        var erreur = await Assert.ThrowsAsync<SqlException>(() => _base.ExecuterAsync(
            "INSERT INTO dbo.Ingredient (Nom, CategorieId, TypeMesure, UniteReferenceId) VALUES (N'Essai', 99, 'Poids', 1);"));

        Assert.Contains("FK_Ingredient_Categorie", erreur.Message);
    }

    [Fact]
    public async Task FacteurDUniteNulOuNegatif_EstRefuse()
    {
        var erreur = await Assert.ThrowsAsync<SqlException>(() => _base.ExecuterAsync(
            "INSERT INTO dbo.Unite (Code, Nom, TypeMesure, FacteurVersReference) VALUES (N'lb', N'livre', 'Poids', 0);"));

        Assert.Contains("CK_Unite_FacteurVersReference", erreur.Message);
    }

    [Fact]
    public async Task CodeDUniteEnDouble_EstRefuse()
    {
        var erreur = await Assert.ThrowsAsync<SqlException>(() => _base.ExecuterAsync(
            "INSERT INTO dbo.Unite (Code, Nom, TypeMesure, FacteurVersReference) VALUES (N'KG', N'kilo', 'Poids', 1000);"));

        Assert.Contains("UQ_Unite_Code", erreur.Message);
    }

    [Fact]
    public async Task UnSynonymeNeMeneQuAUnSeulIngredient()
    {
        await AjouterIngredientAsync("Poitrine de poulet");
        await AjouterIngredientAsync("Cuisse de poulet");
        await _base.ExecuterAsync(
            "INSERT INTO dbo.IngredientSynonyme (IngredientId, Synonyme) VALUES (1, N'Poulet désossé');");

        var erreur = await Assert.ThrowsAsync<SqlException>(() => _base.ExecuterAsync(
            "INSERT INTO dbo.IngredientSynonyme (IngredientId, Synonyme) VALUES (2, N'poulet désossé');"));

        Assert.Contains("UQ_IngredientSynonyme_Synonyme", erreur.Message);
    }

    [Fact]
    public async Task SupprimerUnIngredientSupprimeSesSynonymes()
    {
        await AjouterIngredientAsync("Poitrine de poulet");
        await _base.ExecuterAsync(
            "INSERT INTO dbo.IngredientSynonyme (IngredientId, Synonyme) VALUES (1, N'Suprême de poulet'), (1, N'Poulet désossé');");

        await _base.ExecuterAsync("DELETE FROM dbo.Ingredient WHERE Id = 1;");

        Assert.Equal(0, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.IngredientSynonyme;"));
    }

    [Fact]
    public async Task LeSchemaNUtiliseAucunIndexFiltre()
    {
        // Un index filtré oblige toute modification de sa table à se faire avec QUOTED_IDENTIFIER ON,
        // ce que sqlcmd ne fait pas par défaut : un script de maintenance échouerait.
        Assert.Equal(0, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM sys.indexes WHERE has_filter = 1;"));
    }

    [Fact]
    public async Task UneCategorieUtiliseeNePeutPasEtreSupprimee()
    {
        await AjouterIngredientAsync("Poitrine de poulet");

        var erreur = await Assert.ThrowsAsync<SqlException>(() => _base.ExecuterAsync("DELETE FROM dbo.Categorie WHERE Id = 1;"));

        Assert.Contains("FK_Ingredient_Categorie", erreur.Message);
    }
}
