using Microsoft.Data.SqlClient;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests.Migrations;

/// <summary>Vérifie que les contraintes de la migration 0006 (liste d'épicerie) protègent les données.</summary>
public sealed class SchemaListeEpicerieTests : IAsyncLifetime
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

            SET IDENTITY_INSERT dbo.RepasPlanifie ON;
            INSERT INTO dbo.RepasPlanifie (Id, [Date], Plage, RecetteId, Portions, CreePar) VALUES (1, '2026-11-16', 'Souper', 1, 6, 1);
            SET IDENTITY_INSERT dbo.RepasPlanifie OFF;

            SET IDENTITY_INSERT dbo.ListeEpicerie ON;
            INSERT INTO dbo.ListeEpicerie (Id, Nom, DateDebut, DateFin, CreePar) VALUES (1, N'Semaine du 15 novembre', '2026-11-15', '2026-11-21', 1);
            SET IDENTITY_INSERT dbo.ListeEpicerie OFF;
            """);
    }

    public async Task DisposeAsync() => await _base.DisposeAsync();

    private const string Boeuf = "(SELECT Id FROM dbo.Ingredient WHERE Nom = N'Bœuf haché')";
    private const string Gramme = "(SELECT Id FROM dbo.Unite WHERE Code = N'g')";

    private Task<int> CompterAsync(string table) => _base.ScalaireAsync<int>($"SELECT COUNT(*) FROM dbo.{table};");

    /// <summary>Ajoute un article à la liste 1 ; chaque argument remplace la valeur SQL d'une colonne.</summary>
    private Task AjouterArticleAsync(
        string quantite = "450", string unite = Gramme, string nbRecettes = "1", string estReporte = "0", string reporteDe = "NULL",
        string estCoche = "0", string cocheePar = "NULL", string dateCoche = "NULL") =>
        _base.ExecuterAsync(
            $"""
            INSERT INTO dbo.ListeEpicerieArticle
                (ListeId, IngredientId, QuantiteTotale, UniteId, NbRecettes, EstReporte, ReporteDeArticleId, EstCoche, CocheePar, DateCoche)
            VALUES (1, {Boeuf}, {quantite}, {unite}, {nbRecettes}, {estReporte}, {reporteDe}, {estCoche}, {cocheePar}, {dateCoche});
            """);

    private static async Task<string> ErreurAsync(Func<Task> action) => (await Assert.ThrowsAsync<SqlException>(action)).Message;

    [Fact]
    public async Task UneNouvelleListe_EstActiveParDefaut()
    {
        Assert.Equal(1, await _base.ScalaireAsync<int>(
            "SELECT COUNT(*) FROM dbo.ListeEpicerie WHERE Statut = 'Active' AND CleActive = 0 AND DateTerminaison IS NULL AND DateCreation IS NOT NULL;"));
    }

    [Fact]
    public async Task UneDeuxiemeListeActive_EstRefusee()
    {
        var message = await ErreurAsync(() => _base.ExecuterAsync("INSERT INTO dbo.ListeEpicerie (Nom, CreePar) VALUES (N'Autre', 1);"));

        Assert.Contains("UQ_ListeEpicerie_UneSeuleActive", message);
    }

    [Fact]
    public async Task UneFoisLaListeTerminee_UneNouvelleListePeutEtreActive()
    {
        await _base.ExecuterAsync(
            "UPDATE dbo.ListeEpicerie SET Statut = 'Terminee', CleActive = Id, DateTerminaison = SYSUTCDATETIME() WHERE Id = 1;");

        // La prochaine liste peut exister avant d'être générée (sans période), puis une autre après sa terminaison.
        await _base.ExecuterAsync("INSERT INTO dbo.ListeEpicerie (Nom, CreePar) VALUES (N'Prochaine liste', 1);");
        await _base.ExecuterAsync(
            "UPDATE dbo.ListeEpicerie SET Statut = 'Terminee', CleActive = Id, DateTerminaison = SYSUTCDATETIME() WHERE Statut = 'Active';");
        await _base.ExecuterAsync("INSERT INTO dbo.ListeEpicerie (Nom, CreePar) VALUES (N'Encore une', 1);");

        Assert.Equal(1, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.ListeEpicerie WHERE Statut = 'Active';"));
        Assert.Equal(3, await CompterAsync("ListeEpicerie"));
    }

    [Theory]
    [InlineData("Statut = 'Terminee'")]                                                          // sans le reste
    [InlineData("Statut = 'Terminee', CleActive = Id")]                                          // sans date
    [InlineData("Statut = 'Terminee', DateTerminaison = SYSUTCDATETIME()")]                       // clé encore à 0
    [InlineData("CleActive = 42")]                                                                // active avec une autre clé
    [InlineData("DateTerminaison = SYSUTCDATETIME()")]                                            // active avec une date de fin
    public async Task UneTerminaisonIncoherente_EstRefusee(string affectation)
    {
        var message = await ErreurAsync(() => _base.ExecuterAsync($"UPDATE dbo.ListeEpicerie SET {affectation} WHERE Id = 1;"));

        Assert.Contains("CK_ListeEpicerie_Terminaison", message);
    }

    [Theory]
    [InlineData("DateFin = NULL")]
    [InlineData("DateDebut = NULL")]
    [InlineData("DateFin = '2026-11-14'")] // avant le début
    public async Task UnePeriodeIncoherente_EstRefusee(string affectation)
    {
        var message = await ErreurAsync(() => _base.ExecuterAsync($"UPDATE dbo.ListeEpicerie SET {affectation} WHERE Id = 1;"));

        Assert.Contains("CK_ListeEpicerie_Periode", message);
    }

    [Fact]
    public async Task UnStatutInconnu_EstRefuse()
    {
        var message = await ErreurAsync(() => _base.ExecuterAsync("UPDATE dbo.ListeEpicerie SET Statut = 'Annulee' WHERE Id = 1;"));

        // CK_ListeEpicerie_Statut et CK_ListeEpicerie_Terminaison le refusent toutes deux ; SQL Server nomme la première vérifiée.
        Assert.Matches("CK_ListeEpicerie_(Statut|Terminaison)", message);
    }

    [Fact]
    public async Task UnArticle_ReçoitSesValeursParDefaut()
    {
        await _base.ExecuterAsync($"INSERT INTO dbo.ListeEpicerieArticle (ListeId, IngredientId) VALUES (1, {Boeuf});");

        Assert.Equal(1, await _base.ScalaireAsync<int>(
            """
            SELECT COUNT(*) FROM dbo.ListeEpicerieArticle
            WHERE NbRecettes = 0 AND EstManuel = 0 AND EstReporte = 0 AND EstCoche = 0 AND QuantiteTotale IS NULL;
            """));
    }

    [Fact]
    public async Task LeMemeIngredient_PeutAvoirUneLigneParUnite()
    {
        // Conversion impossible : une ligne séparée avec l'unité d'origine.
        await AjouterArticleAsync();
        await AjouterArticleAsync(quantite: "2", unite: "(SELECT Id FROM dbo.Unite WHERE Code = N'tasse')");

        Assert.Equal(2, await CompterAsync("ListeEpicerieArticle"));
    }

    [Theory]
    [InlineData("450", "NULL", "CK_ListeEpicerieArticle_QuantiteEtUnite")]
    [InlineData("NULL", Gramme, "CK_ListeEpicerieArticle_QuantiteEtUnite")]
    [InlineData("0", Gramme, "CK_ListeEpicerieArticle_Quantite")]
    [InlineData("-1", Gramme, "CK_ListeEpicerieArticle_Quantite")]
    public async Task UneQuantiteIncoherente_EstRefusee(string quantite, string unite, string contrainte)
    {
        var message = await ErreurAsync(() => AjouterArticleAsync(quantite: quantite, unite: unite));

        Assert.Contains(contrainte, message);
    }

    [Fact]
    public async Task UnNombreDeRecettesNegatif_EstRefuse()
    {
        var message = await ErreurAsync(() => AjouterArticleAsync(nbRecettes: "-1"));

        Assert.Contains("CK_ListeEpicerieArticle_NbRecettes", message);
    }

    [Theory]
    [InlineData("1", "NULL", "NULL")]               // coché sans auteur ni date
    [InlineData("1", "1", "NULL")]                  // sans date
    [InlineData("1", "NULL", "SYSUTCDATETIME()")]   // sans auteur
    [InlineData("0", "1", "SYSUTCDATETIME()")]      // pas coché, mais avec auteur et date
    public async Task UneCocheIncoherente_EstRefusee(string estCoche, string cocheePar, string dateCoche)
    {
        var message = await ErreurAsync(() => AjouterArticleAsync(estCoche: estCoche, cocheePar: cocheePar, dateCoche: dateCoche));

        Assert.Contains("CK_ListeEpicerieArticle_Coche", message);
    }

    [Fact]
    public async Task UnArticleCoche_GardeQuiLAcocheEtQuand()
    {
        await AjouterArticleAsync(estCoche: "1", cocheePar: "1", dateCoche: "SYSUTCDATETIME()");

        Assert.Equal(1, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.ListeEpicerieArticle WHERE EstCoche = 1 AND CocheePar = 1;"));
    }

    [Fact]
    public async Task UnArticleReporte_PointeVersLArticleDOrigine()
    {
        await AjouterArticleAsync();
        var origine = await _base.ScalaireAsync<int>("SELECT MAX(Id) FROM dbo.ListeEpicerieArticle;");

        await AjouterArticleAsync(estReporte: "1", reporteDe: origine.ToString());

        Assert.Equal(1, await _base.ScalaireAsync<int>($"SELECT COUNT(*) FROM dbo.ListeEpicerieArticle WHERE ReporteDeArticleId = {origine};"));
    }

    [Theory]
    [InlineData("1", "NULL")]   // reporté sans origine
    [InlineData("0", "1")]      // origine sans être reporté
    public async Task UnReportIncoherent_EstRefuse(string estReporte, string reporteDe)
    {
        await AjouterArticleAsync(); // l'article 1, pour que l'origine existe

        var message = await ErreurAsync(() => AjouterArticleAsync(estReporte: estReporte, reporteDe: reporteDe));

        Assert.Contains("CK_ListeEpicerieArticle_Report", message);
    }

    [Fact]
    public async Task LesSources_SuiventLArticleEtLeRepas()
    {
        await AjouterArticleAsync();
        await _base.ExecuterAsync(
            $"INSERT INTO dbo.ListeEpicerieArticleSource (ArticleId, RepasPlanifieId, Quantite, UniteId) VALUES (1, 1, 450, {Gramme});");

        // Un repas retiré du calendrier emporte sa trace, sans bloquer le retrait.
        await _base.ExecuterAsync("DELETE FROM dbo.RepasPlanifie WHERE Id = 1;");

        Assert.Equal(0, await CompterAsync("ListeEpicerieArticleSource"));
        Assert.Equal(1, await CompterAsync("ListeEpicerieArticle"));
    }

    [Fact]
    public async Task SupprimerUneListe_SupprimeSesArticlesEtLeursSources()
    {
        await AjouterArticleAsync();
        await _base.ExecuterAsync(
            $"INSERT INTO dbo.ListeEpicerieArticleSource (ArticleId, RepasPlanifieId, Quantite, UniteId) VALUES (1, 1, 450, {Gramme});");

        await _base.ExecuterAsync("DELETE FROM dbo.ListeEpicerie WHERE Id = 1;");

        Assert.Equal(0, await CompterAsync("ListeEpicerieArticle"));
        Assert.Equal(0, await CompterAsync("ListeEpicerieArticleSource"));
        Assert.Equal(1, await CompterAsync("RepasPlanifie"));
    }

    [Theory]
    [InlineData("450", "NULL", "CK_ListeEpicerieArticleSource_QuantiteEtUnite")]
    [InlineData("0", Gramme, "CK_ListeEpicerieArticleSource_Quantite")]
    public async Task UneSourceIncoherente_EstRefusee(string quantite, string unite, string contrainte)
    {
        await AjouterArticleAsync();

        var message = await ErreurAsync(() => _base.ExecuterAsync(
            $"INSERT INTO dbo.ListeEpicerieArticleSource (ArticleId, RepasPlanifieId, Quantite, UniteId) VALUES (1, 1, {quantite}, {unite});"));

        Assert.Contains(contrainte, message);
    }

    [Fact]
    public async Task UneSourceAuGout_EstAcceptee()
    {
        await _base.ExecuterAsync($"INSERT INTO dbo.ListeEpicerieArticle (ListeId, IngredientId, NbRecettes) VALUES (1, {Boeuf}, 1);");

        await _base.ExecuterAsync("INSERT INTO dbo.ListeEpicerieArticleSource (ArticleId, RepasPlanifieId) VALUES (1, 1);");

        Assert.Equal(1, await CompterAsync("ListeEpicerieArticleSource"));
    }
}
