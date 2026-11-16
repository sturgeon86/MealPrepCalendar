using Repas.Domaine.Recettes;
using Repas.Donnees.Photos;

namespace Repas.Donnees.Tests;

/// <summary>Tests du stockage des photos, dans un dossier temporaire propre à chaque test.</summary>
public sealed class StockagePhotosTests : IDisposable
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private readonly string _dossier = Path.Combine(Path.GetTempPath(), "repas-photos-" + Guid.NewGuid().ToString("N"));
    private readonly StockagePhotos _stockage;

    public StockagePhotosTests() => _stockage = new StockagePhotos(_dossier);

    public void Dispose()
    {
        if (Directory.Exists(_dossier))
        {
            Directory.Delete(_dossier, recursive: true);
        }
    }

    [Fact]
    public async Task Enregistrer_CreeLeDossierEtEcritLeFichierSousUnNomGenere()
    {
        var nom = await _stockage.EnregistrerAsync(Jpeg);

        Assert.True(PhotoRecette.EstNomValide(nom));
        Assert.EndsWith(".jpg", nom);
        Assert.Equal(Jpeg, await File.ReadAllBytesAsync(Path.Combine(_dossier, nom)));
    }

    [Fact]
    public async Task Enregistrer_DeuxFois_DonneDeuxFichiersDistincts()
    {
        var premier = await _stockage.EnregistrerAsync(Jpeg);
        var second = await _stockage.EnregistrerAsync(Jpeg);

        Assert.NotEqual(premier, second);
        Assert.Equal(2, Directory.GetFiles(_dossier).Length);
    }

    [Fact]
    public async Task Enregistrer_ContenuQuiNEstPasUneImage_EstRefuseSansRienEcrire()
    {
        await Assert.ThrowsAsync<PhotoInvalideException>(() => _stockage.EnregistrerAsync("<html></html>"u8.ToArray()));

        Assert.False(Directory.Exists(_dossier) && Directory.EnumerateFiles(_dossier).Any());
    }

    [Fact]
    public async Task TrouverChemin_DonneLeFichierEnregistre()
    {
        var nom = await _stockage.EnregistrerAsync(Jpeg);

        Assert.Equal(Path.Combine(_dossier, nom), _stockage.TrouverChemin(nom));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0123456789abcdef0123456789abcdef.jpg")] // nom valide, mais aucun fichier
    [InlineData("../appsettings.json")]
    [InlineData("..\\appsettings.json")]
    public void TrouverChemin_NomInconnuOuInvalide_RetourneNull(string? nom)
    {
        Assert.Null(_stockage.TrouverChemin(nom));
    }

    [Fact]
    public async Task TrouverChemin_IgnoreUnFichierAuNomNonGenere()
    {
        Directory.CreateDirectory(_dossier);
        await File.WriteAllBytesAsync(Path.Combine(_dossier, "secret.jpg"), Jpeg);

        Assert.Null(_stockage.TrouverChemin("secret.jpg"));
    }

    [Fact]
    public async Task Supprimer_RetireLeFichier()
    {
        var nom = await _stockage.EnregistrerAsync(Jpeg);

        Assert.True(_stockage.Supprimer(nom));

        Assert.Null(_stockage.TrouverChemin(nom));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0123456789abcdef0123456789abcdef.jpg")]
    [InlineData("../autre.jpg")]
    public void Supprimer_FichierAbsentOuNomInvalide_EstSansEffet(string? nom)
    {
        Assert.True(_stockage.Supprimer(nom));
    }

    [Fact]
    public async Task Supprimer_NeTouchePasAUnFichierHorsDuDossier()
    {
        var voisin = Path.Combine(Path.GetTempPath(), "repas-voisin-" + Guid.NewGuid().ToString("N") + ".jpg");
        await File.WriteAllBytesAsync(voisin, Jpeg);
        try
        {
            _stockage.Supprimer("../" + Path.GetFileName(voisin));

            Assert.True(File.Exists(voisin));
        }
        finally
        {
            File.Delete(voisin);
        }
    }
}
