using System.Net;
using Repas.Domaine.Import;

namespace Repas.Domaine.Tests.Import;

public sealed class AdresseImportTests
{
    [Theory]
    [InlineData("https://www.exemple-recettes.ca/recettes/pate-chinois/")]
    [InlineData("http://exemple.com/recette?id=12")]
    [InlineData("  https://exemple.com/  ")]
    public void AdressesPubliquesAcceptees(string texte)
    {
        Assert.Null(AdresseImport.Valider(texte, out var adresse));
        Assert.NotNull(adresse);
    }

    [Fact]
    public void SansSchema_HttpsEstAjoute()
    {
        Assert.Null(AdresseImport.Valider("www.exemple.com/recette", out var adresse));
        Assert.Equal("https://www.exemple.com/recette", adresse?.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("https://")]
    public void AdresseVideOuInvalide(string? texte)
    {
        Assert.NotNull(AdresseImport.Valider(texte, out var adresse));
        Assert.Null(adresse);
    }

    [Theory]
    [InlineData("ftp://exemple.com/recette.txt")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void AutresSchemasRefuses(string texte) => Assert.NotNull(AdresseImport.Valider(texte, out _));

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("http://LOCALHOST./")]
    [InlineData("http://app.localhost/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://127.1.2.3/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://172.16.0.1/")]
    [InlineData("http://172.31.255.255/")]
    [InlineData("http://192.168.1.10/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[fd00::1]/")]
    [InlineData("http://[fe80::1]/")]
    [InlineData("http://[::ffff:192.168.1.10]/")]
    [InlineData("http://unraid/")]
    [InlineData("http://tower.local/")]
    [InlineData("http://nas.lan/")]
    [InlineData("http://routeur.home.arpa/")]
    [InlineData("http://2130706433/")]
    [InlineData("http://0x7f000001/")]
    public void ReseauLocalRefuse(string texte)
    {
        Assert.Equal(AdresseImport.MessageReseauLocal, AdresseImport.Valider(texte, out var adresse));
        Assert.Null(adresse);
    }

    [Theory]
    [InlineData("http://exemple.com:8096/")]
    [InlineData("https://exemple.com:22/")]
    [InlineData("https://moi:secret@exemple.com/")]
    public void PortOuIdentifiantsRefuses(string texte) => Assert.NotNull(AdresseImport.Valider(texte, out _));

    [Fact]
    public void AdresseTropLongue() =>
        Assert.NotNull(AdresseImport.Valider("https://exemple.com/" + new string('a', AdresseImport.LongueurMax), out _));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("142.250.65.67")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2607:f8b0:4020:807::2004")]
    [InlineData("64:ff9b::808:808")]
    public void IpPubliques(string ip) => Assert.True(AdresseImport.EstPublique(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::127.0.0.1")]
    [InlineData("64:ff9b::c0a8:10a")]
    [InlineData("2001:db8::1")]
    public void IpLocalesOuReservees(string ip) => Assert.False(AdresseImport.EstPublique(IPAddress.Parse(ip)));
}
