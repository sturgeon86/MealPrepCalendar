using Repas.Domaine.Recettes;

namespace Repas.Domaine.Tests.Recettes;

public sealed class PhotoRecetteTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] Webp = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    [Fact]
    public void DetecterFormat_ReconnaitJpegPngEtWebp()
    {
        Assert.Equal(FormatPhoto.Jpeg, PhotoRecette.DetecterFormat(Jpeg));
        Assert.Equal(FormatPhoto.Png, PhotoRecette.DetecterFormat(Png));
        Assert.Equal(FormatPhoto.Webp, PhotoRecette.DetecterFormat(Webp));
    }

    [Fact]
    public void DetecterFormat_AutreContenu_RetourneNull()
    {
        Assert.Null(PhotoRecette.DetecterFormat("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8));
        Assert.Null(PhotoRecette.DetecterFormat("GIF89a"u8));
        Assert.Null(PhotoRecette.DetecterFormat("RIFF\0\0\0\0WAVE"u8)); // RIFF, mais un son
        Assert.Null(PhotoRecette.DetecterFormat([0xFF, 0xD8])); // trop court
        Assert.Null(PhotoRecette.DetecterFormat([]));
    }

    [Fact]
    public void Verifier_ImageAcceptee_DonneSonFormat()
    {
        Assert.Equal(FormatPhoto.Png, PhotoRecette.Verifier(Png));
    }

    [Fact]
    public void Verifier_FichierVide_EstRefuse()
    {
        var ex = Assert.Throws<PhotoInvalideException>(() => PhotoRecette.Verifier([]));
        Assert.Contains("vide", ex.Message);
    }

    [Fact]
    public void Verifier_FichierTropGros_EstRefuse()
    {
        var contenu = new byte[PhotoRecette.TailleMaxOctets + 1];
        Jpeg.CopyTo(contenu, 0);

        var ex = Assert.Throws<PhotoInvalideException>(() => PhotoRecette.Verifier(contenu));
        Assert.Contains("5 Mo", ex.Message);
    }

    [Fact]
    public void Verifier_TailleMaximaleExacte_EstAcceptee()
    {
        var contenu = new byte[PhotoRecette.TailleMaxOctets];
        Jpeg.CopyTo(contenu, 0);

        Assert.Equal(FormatPhoto.Jpeg, PhotoRecette.Verifier(contenu));
    }

    [Fact]
    public void Verifier_PasUneImage_EstRefuse()
    {
        Assert.Throws<PhotoInvalideException>(() => PhotoRecette.Verifier("%PDF-1.7"u8));
    }

    [Theory]
    [InlineData(FormatPhoto.Jpeg, ".jpg")]
    [InlineData(FormatPhoto.Png, ".png")]
    [InlineData(FormatPhoto.Webp, ".webp")]
    public void NouveauNom_EstValideAvecLExtensionDuFormat(FormatPhoto format, string extension)
    {
        var nom = PhotoRecette.NouveauNom(format);

        Assert.EndsWith(extension, nom);
        Assert.True(PhotoRecette.EstNomValide(nom));
    }

    [Fact]
    public void NouveauNom_EstUniqueAChaqueAppel()
    {
        Assert.NotEqual(PhotoRecette.NouveauNom(FormatPhoto.Jpeg), PhotoRecette.NouveauNom(FormatPhoto.Jpeg));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("photo.jpg")]
    [InlineData("../0123456789abcdef0123456789abcdef.jpg")]
    [InlineData("sous/0123456789abcdef0123456789abcdef.jpg")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF.jpg")] // majuscules : jamais générées
    [InlineData("0123456789abcdef0123456789abcdef.gif")]
    [InlineData("0123456789abcdef0123456789abcdef.jpg\n")]
    [InlineData("0123456789abcdef0123456789abcde.jpg")] // 31 caractères
    public void EstNomValide_RefuseToutNomNonGenere(string? nom)
    {
        Assert.False(PhotoRecette.EstNomValide(nom));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef.jpg", "image/jpeg")]
    [InlineData("0123456789abcdef0123456789abcdef.png", "image/png")]
    [InlineData("0123456789abcdef0123456789abcdef.webp", "image/webp")]
    public void TypeContenu_SelonLExtension(string nom, string attendu)
    {
        Assert.Equal(attendu, PhotoRecette.TypeContenu(nom));
    }
}
