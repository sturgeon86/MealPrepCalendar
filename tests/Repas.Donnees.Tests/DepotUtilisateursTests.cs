using Repas.Domaine.Utilisateurs;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests;

/// <summary>Tests d'intégration sur une base où les vraies migrations ont été appliquées.</summary>
public sealed class DepotUtilisateursTests : IAsyncLifetime
{
    private BaseDeTest _baseDeTest = null!;
    private BaseDeDonnees _base = null!;
    private DepotUtilisateurs _depot = null!;

    public async Task InitializeAsync()
    {
        _baseDeTest = await BaseDeTest.CreerAvecSchemaAsync();
        _base = new BaseDeDonnees(_baseDeTest.ChaineConnexion);
        _depot = new DepotUtilisateurs(_base);
    }

    public async Task DisposeAsync() => await _baseDeTest.DisposeAsync();

    [Fact]
    public async Task Ajouter_CreeUnCompteActifAvecUneDateDeCreationUtc()
    {
        var avant = DateTime.UtcNow.AddSeconds(-5);

        var cree = await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1");

        Assert.True(cree.Id > 0);
        Assert.Equal("marie@exemple.com", cree.Courriel);
        Assert.Equal("Marie", cree.NomAffiche);
        Assert.Equal("hash-1", cree.MotDePasseHash);
        Assert.True(cree.Actif);
        Assert.Equal(DateTimeKind.Utc, cree.DateCreation.Kind);
        Assert.InRange(cree.DateCreation, avant, DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Ajouter_NormaliseLeCourriel()
    {
        var cree = await _depot.AjouterAsync("  Marie@Exemple.COM ", "Marie", "hash-1");

        Assert.Equal("marie@exemple.com", cree.Courriel);
    }

    [Fact]
    public async Task TrouverParId_RetourneLeCompteOuNull()
    {
        var cree = await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1");

        Assert.Equal(cree, await _depot.TrouverParIdAsync(cree.Id));
        Assert.Null(await _depot.TrouverParIdAsync(cree.Id + 1));
    }

    [Fact]
    public async Task TrouverParCourriel_IgnoreLaCasseEtLesEspaces()
    {
        var cree = await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1");

        Assert.Equal(cree, await _depot.TrouverParCourrielAsync(" MARIE@exemple.com "));
        Assert.Null(await _depot.TrouverParCourrielAsync("autre@exemple.com"));
    }

    [Fact]
    public async Task Ajouter_CourrielDejaUtilise_EstRefuse()
    {
        await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1");

        var erreur = await Assert.ThrowsAsync<CourrielDejaUtiliseException>(
            () => _depot.AjouterAsync("Marie@Exemple.com", "Autre Marie", "hash-2"));

        Assert.Equal("marie@exemple.com", erreur.Courriel);
        Assert.Equal(1, await _depot.CompterAsync());
    }

    [Fact]
    public async Task Compter_VautZeroSurUneBaseNeuve()
    {
        Assert.Equal(0, await _depot.CompterAsync());
    }

    [Fact]
    public async Task Lister_RetourneTousLesComptesParNomAffiche()
    {
        await _depot.AjouterAsync("zoe@exemple.com", "Zoé", "hash-1");
        var alex = await _depot.AjouterAsync("alex@exemple.com", "Alex", "hash-2");
        await _depot.ModifierActifAsync(alex.Id, false);

        var comptes = await _depot.ListerAsync();

        Assert.Equal(["Alex", "Zoé"], comptes.Select(c => c.NomAffiche));
        Assert.False(comptes[0].Actif);
    }

    [Fact]
    public async Task ModifierMotDePasseHash_RemplaceLeHash()
    {
        var cree = await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1");

        Assert.True(await _depot.ModifierMotDePasseHashAsync(cree.Id, "hash-2"));
        Assert.False(await _depot.ModifierMotDePasseHashAsync(cree.Id + 1, "hash-3"));

        Assert.Equal("hash-2", (await _depot.TrouverParIdAsync(cree.Id))!.MotDePasseHash);
    }

    [Fact]
    public async Task ModifierActif_DesactivePuisReactiveLeCompte()
    {
        var cree = await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1");

        Assert.True(await _depot.ModifierActifAsync(cree.Id, false));
        Assert.False((await _depot.TrouverParIdAsync(cree.Id))!.Actif);

        Assert.True(await _depot.ModifierActifAsync(cree.Id, true));
        Assert.True((await _depot.TrouverParIdAsync(cree.Id))!.Actif);

        Assert.False(await _depot.ModifierActifAsync(cree.Id + 1, false));
    }

    [Fact]
    public async Task AjouterPremier_SurUneBaseVide_CreeLeCompte()
    {
        var cree = await _depot.AjouterPremierAsync(" Marie@Exemple.com ", "Marie", "hash-1");

        Assert.NotNull(cree);
        Assert.Equal("marie@exemple.com", cree.Courriel);
        Assert.True(cree.Actif);
        Assert.Equal(cree, await _depot.TrouverParIdAsync(cree.Id));
    }

    [Fact]
    public async Task AjouterPremier_QuandUnCompteExiste_NeCreeRien()
    {
        await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1");

        var cree = await _depot.AjouterPremierAsync("intrus@exemple.com", "Intrus", "hash-2");

        Assert.Null(cree);
        Assert.Equal(1, await _depot.CompterAsync());
    }

    [Fact]
    public async Task AjouterPremier_UnCompteDesactiveCompteAussi()
    {
        var marie = await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1");
        await _depot.ModifierActifAsync(marie.Id, false);

        Assert.Null(await _depot.AjouterPremierAsync("intrus@exemple.com", "Intrus", "hash-2"));
    }

    [Fact]
    public async Task AjouterPremier_DemandesSimultanees_UnSeulCompteEstCree()
    {
        var demandes = Enumerable.Range(1, 12)
            .Select(i => Task.Run(() => _depot.AjouterPremierAsync($"personne{i}@exemple.com", $"Personne {i}", "hash")))
            .ToArray();

        var resultats = await Task.WhenAll(demandes);

        Assert.Single(resultats, r => r is not null);
        Assert.Equal(1, await _depot.CompterAsync());
    }

    [Fact]
    public async Task Ajouter_DansUneTransactionAnnulee_NeLaisseRien()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _base.DansTransactionAsync(async session =>
        {
            await _depot.AjouterAsync("marie@exemple.com", "Marie", "hash-1", session);
            throw new InvalidOperationException("Annulation voulue.");
        }));

        Assert.Equal(0, await _depot.CompterAsync());
    }
}
