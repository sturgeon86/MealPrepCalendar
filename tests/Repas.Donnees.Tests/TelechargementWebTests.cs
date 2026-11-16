using Repas.Domaine.Import;
using Repas.Domaine.Recettes;
using Repas.Donnees.Tests.Outils;
using Repas.Donnees.Web;

namespace Repas.Donnees.Tests;

/// <summary>
/// Le téléchargement des pages et des photos, contre un serveur local. Les vérifications d'adresse saisie sont
/// désactivées pour que le serveur de test soit joignable, sauf dans les tests qui prouvent le refus du réseau local.
/// </summary>
public sealed class TelechargementWebTests : IAsyncDisposable
{
    private static readonly byte[] DebutJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private readonly ServeurHttpDeTest _serveur = new();

    // Joint la boucle locale, comme si c'était un site public.
    private readonly TelechargementWeb _telechargement = new(_ => true, verifierAdresses: false);

    public async ValueTask DisposeAsync()
    {
        _telechargement.Dispose();
        await _serveur.DisposeAsync();
    }

    [Fact]
    public async Task LitUnePageEtSonAdresse()
    {
        _serveur.Repondre("/recette", "<html><body>Pâté chinois</body></html>");

        var page = await _telechargement.LirePageAsync(_serveur.Adresse("/recette"));

        Assert.Contains("Pâté chinois", page.Html);
        Assert.Equal(_serveur.Adresse("/recette"), page.Adresse);
    }

    [Fact]
    public async Task SuitLesRedirectionsEtDonneLAdresseFinale()
    {
        _serveur.Repondre("/ancienne", 301, "text/html", [], "/nouvelle");
        _serveur.Repondre("/nouvelle", "<html>ici</html>");

        var page = await _telechargement.LirePageAsync(_serveur.Adresse("/ancienne"));

        Assert.Equal(_serveur.Adresse("/nouvelle"), page.Adresse);
    }

    [Fact]
    public async Task TropDeRedirections()
    {
        _serveur.Repondre("/boucle", 302, "text/html", [], "/boucle");

        var refus = await Assert.ThrowsAsync<ImportWebException>(() => _telechargement.LirePageAsync(_serveur.Adresse("/boucle")));
        Assert.Contains("redirige", refus.Message);
    }

    [Fact]
    public async Task PageIntrouvable()
    {
        var refus = await Assert.ThrowsAsync<ImportWebException>(() => _telechargement.LirePageAsync(_serveur.Adresse("/absente")));
        Assert.Contains("404", refus.Message);
    }

    [Fact]
    public async Task PageTropGrosse()
    {
        _serveur.Repondre("/geante", 200, "text/html", new byte[LectureRecetteWeb.TaillePageMax + 1]);

        var refus = await Assert.ThrowsAsync<ImportWebException>(() => _telechargement.LirePageAsync(_serveur.Adresse("/geante")));
        Assert.Contains("taille", refus.Message);
    }

    [Fact]
    public async Task UnFichierQuiNEstPasUnePage()
    {
        _serveur.Repondre("/fichier.pdf", 200, "application/pdf", [1, 2, 3]);

        await Assert.ThrowsAsync<ImportWebException>(() => _telechargement.LirePageAsync(_serveur.Adresse("/fichier.pdf")));
    }

    [Fact]
    public async Task LitUnePhotoValide()
    {
        var jpeg = DebutJpeg.Concat(new byte[500]).ToArray();
        _serveur.Repondre("/photo.jpg", 200, "image/jpeg", jpeg);

        Assert.Equal(jpeg, await _telechargement.LireImageAsync(_serveur.Adresse("/photo.jpg")));
    }

    [Fact]
    public async Task RefuseUnePhotoQuiNEnEstPasUne()
    {
        _serveur.Repondre("/photo.jpg", 200, "image/jpeg", "<html>pas une image</html>"u8.ToArray());

        await Assert.ThrowsAsync<ImportWebException>(() => _telechargement.LireImageAsync(_serveur.Adresse("/photo.jpg")));
    }

    [Fact]
    public async Task RefuseUnePhotoTropGrosse()
    {
        _serveur.Repondre("/geante.jpg", 200, "image/jpeg", DebutJpeg.Concat(new byte[PhotoRecette.TailleMaxOctets]).ToArray());

        await Assert.ThrowsAsync<ImportWebException>(() => _telechargement.LireImageAsync(_serveur.Adresse("/geante.jpg")));
    }

    [Fact]
    public async Task ReseauLocal_RefuseALaConnexion()
    {
        // Même quand l'adresse passe la première vérification, la connexion vers la boucle locale est refusée :
        // c'est ce qui protège contre un nom public qui pointe vers la maison, ou une redirection.
        _serveur.Repondre("/recette", "<html>secret</html>");
        using var protege = new TelechargementWeb(AdresseImport.EstPublique, verifierAdresses: false);

        var refus = await Assert.ThrowsAsync<ImportWebException>(() => protege.LirePageAsync(_serveur.Adresse("/recette")));
        Assert.Equal(AdresseImport.MessageReseauLocal, refus.Message);
    }

    [Fact]
    public async Task RedirectionVersLeReseauLocal_Refusee()
    {
        // Le serveur de test (127.0.0.1) joue le site public ; il redirige vers une autre adresse, interdite.
        _serveur.Repondre("/piege", 302, "text/html", [], $"http://127.0.0.2:{_serveur.Port}/");
        using var protege = new TelechargementWeb(
            ip => ip.Equals(System.Net.IPAddress.Loopback), verifierAdresses: false);

        var refus = await Assert.ThrowsAsync<ImportWebException>(() => protege.LirePageAsync(_serveur.Adresse("/piege")));
        Assert.Equal(AdresseImport.MessageReseauLocal, refus.Message);
    }

    [Fact]
    public async Task ParDefaut_LeReseauLocalEstRefuse()
    {
        using var parDefaut = new TelechargementWeb();

        var refus = await Assert.ThrowsAsync<ImportWebException>(() => parDefaut.LirePageAsync(new Uri("http://127.0.0.1/")));
        Assert.Equal(AdresseImport.MessageReseauLocal, refus.Message);
    }

    [Fact]
    public async Task SeulsHttpEtHttps()
    {
        await Assert.ThrowsAsync<ImportWebException>(() => _telechargement.LirePageAsync(new Uri("ftp://exemple.com/recette")));
    }
}
