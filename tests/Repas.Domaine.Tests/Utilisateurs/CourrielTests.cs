using Repas.Domaine.Utilisateurs;

namespace Repas.Domaine.Tests.Utilisateurs;

public sealed class CourrielTests
{
    [Theory]
    [InlineData("marie@exemple.com", "marie@exemple.com")]
    [InlineData("Marie@Exemple.COM", "marie@exemple.com")]
    [InlineData("  marie@exemple.com\t", "marie@exemple.com")]
    [InlineData("ÉMILIE@exemple.com", "émilie@exemple.com")]
    public void Normaliser_RetireLesEspacesEtMetEnMinuscules(string saisie, string attendu)
    {
        Assert.Equal(attendu, Courriel.Normaliser(saisie));
    }

    [Fact]
    public void Normaliser_RefuseNull()
    {
        Assert.Throws<ArgumentNullException>(() => Courriel.Normaliser(null!));
    }
}
