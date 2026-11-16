using Microsoft.Data.SqlClient;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests.Migrations;

/// <summary>Vérifie que les contraintes de la migration 0005 (calendrier) protègent les données.</summary>
public sealed class SchemaCalendrierTests : IAsyncLifetime
{
    private BaseDeTest _base = null!;

    public async Task InitializeAsync()
    {
        _base = await BaseDeTest.CreerAvecSchemaAsync();
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

    private Task InsererAsync(string plage = "'Souper'", string recetteId = "1", string texteLibre = "NULL", string portions = "4") =>
        _base.ExecuterAsync(
            $"""
            INSERT INTO dbo.RepasPlanifie ([Date], Plage, RecetteId, TexteLibre, Portions, CreePar)
            VALUES ('2026-11-16', {plage}, {recetteId}, {texteLibre}, {portions}, 1);
            """);

    private static async Task<string> ErreurAsync(Func<Task> action) => (await Assert.ThrowsAsync<SqlException>(action)).Message;

    [Fact]
    public async Task UnRepasAvecRecette_EstAccepteAvecSaDateDeCreation()
    {
        await InsererAsync();

        Assert.Equal(1, await _base.ScalaireAsync<int>(
            "SELECT COUNT(*) FROM dbo.RepasPlanifie WHERE DateCreation IS NOT NULL AND Portions = 4;"));
    }

    [Fact]
    public async Task UnRepasLibre_EstAccepteSansPortions()
    {
        await InsererAsync(recetteId: "NULL", texteLibre: "N'Restaurant'", portions: "NULL");

        Assert.Equal(1, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.RepasPlanifie WHERE RecetteId IS NULL;"));
    }

    [Fact]
    public async Task PlusieursRepasDansLaMemePlage_SontAcceptes()
    {
        await InsererAsync();
        await InsererAsync(recetteId: "NULL", texteLibre: "N'Gâteau de fête'", portions: "NULL");

        Assert.Equal(2, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM dbo.RepasPlanifie;"));
    }

    [Theory]
    [InlineData("NULL", "NULL", "NULL")]            // ni recette ni texte
    [InlineData("1", "N'Restes'", "4")]             // les deux
    [InlineData("NULL", "N'   '", "NULL")]          // texte blanc
    public async Task RecetteOuTexte_UnSeulDesDeux(string recetteId, string texteLibre, string portions)
    {
        var message = await ErreurAsync(() => InsererAsync(recetteId: recetteId, texteLibre: texteLibre, portions: portions));

        Assert.Contains("CK_RepasPlanifie_RecetteOuTexte", message);
    }

    [Theory]
    [InlineData("1", "NULL", "NULL")]               // recette sans portions
    [InlineData("1", "NULL", "0")]
    [InlineData("1", "NULL", "101")]
    [InlineData("NULL", "N'Restaurant'", "2")]      // portions sur un repas libre
    public async Task Portions_SeulementPourUneRecetteEtEntre1Et100(string recetteId, string texteLibre, string portions)
    {
        var message = await ErreurAsync(() => InsererAsync(recetteId: recetteId, texteLibre: texteLibre, portions: portions));

        Assert.Contains("CK_RepasPlanifie_Portions", message);
    }

    [Theory]
    [InlineData("plage", "'Déjeuner'", "CK_RepasPlanifie_Plage")]
    [InlineData("plage", "'Dîner'", "CK_RepasPlanifie_Plage")] // la valeur stockée est sans accent
    [InlineData("recette", "999", "FK_RepasPlanifie_Recette")]
    public async Task ValeurInvalide_EstRefusee(string colonne, string valeur, string contrainte)
    {
        var message = colonne == "plage"
            ? await ErreurAsync(() => InsererAsync(plage: valeur))
            : await ErreurAsync(() => InsererAsync(recetteId: valeur));

        Assert.Contains(contrainte, message);
    }

    [Fact]
    public async Task UneRecettePlanifiee_NePeutPasEtreSupprimeeEnSilence()
    {
        await InsererAsync();

        var message = await ErreurAsync(() => _base.ExecuterAsync("DELETE FROM dbo.Recette WHERE Id = 1;"));

        Assert.Contains("FK_RepasPlanifie_Recette", message);
    }
}
