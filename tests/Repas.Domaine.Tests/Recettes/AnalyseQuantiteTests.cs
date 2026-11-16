using Repas.Domaine.Recettes;

namespace Repas.Domaine.Tests.Recettes;

public sealed class AnalyseQuantiteTests
{
    [Theory]
    [InlineData("2", 2)]
    [InlineData(" 250 ", 250)]
    [InlineData("2,5", 2.5)]
    [InlineData("2.5", 2.5)]
    [InlineData(",5", 0.5)]
    [InlineData("1/2", 0.5)]
    [InlineData("3 / 4", 0.75)]
    [InlineData("1 1/2", 1.5)]
    [InlineData("2  1/4", 2.25)]
    [InlineData("11/2", 5.5)]
    [InlineData("½", 0.5)]
    [InlineData("1½", 1.5)]
    [InlineData("1 ½", 1.5)]
    [InlineData("¾", 0.75)]
    [InlineData("2⅛", 2.125)]
    public void LitLesFormesCourantes(string texte, decimal attendu)
    {
        Assert.True(AnalyseQuantite.EssayerLire(texte, out var quantite));
        Assert.Equal(attendu, quantite);
    }

    [Fact]
    public void UnTiersGardeSaPrecision()
    {
        Assert.True(AnalyseQuantite.EssayerLire("1/3", out var quantite));
        Assert.Equal(1m / 3m, quantite);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TexteVide_ValideMaisSansQuantite(string? texte)
    {
        Assert.True(AnalyseQuantite.EssayerLire(texte, out var quantite));
        Assert.Null(quantite);
    }

    [Theory]
    [InlineData("deux")]
    [InlineData("1/0")]
    [InlineData("1/")]
    [InlineData("2,5,1")]
    [InlineData("-1")]
    [InlineData("1 tasse")]
    [InlineData(",")]
    public void TexteInvalide_EstRefuse(string texte)
    {
        Assert.False(AnalyseQuantite.EssayerLire(texte, out var quantite));
        Assert.Null(quantite);
    }

    [Theory]
    [InlineData(2.5, "2,5")]
    [InlineData(250, "250")]
    [InlineData(0.75, "0,75")]
    public void Ecrire_SansZerosInutilesAvecLaVirgule(decimal quantite, string attendu)
    {
        Assert.Equal(attendu, AnalyseQuantite.Ecrire(quantite));
    }

    [Fact]
    public void Ecrire_UneValeurDeLaBaseAvecZerosDeFin()
    {
        Assert.Equal("2,5", AnalyseQuantite.Ecrire(2.5000m));
        Assert.Equal(string.Empty, AnalyseQuantite.Ecrire(null));
    }
}
