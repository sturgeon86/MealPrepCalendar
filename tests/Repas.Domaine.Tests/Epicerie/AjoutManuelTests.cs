using Repas.Domaine.Epicerie;

namespace Repas.Domaine.Tests.Epicerie;

public sealed class AjoutManuelTests
{
    [Fact]
    public void SansQuantite_EstValide()
    {
        Assert.Empty(new AjoutManuel(12, null, null).Valider());
    }

    [Fact]
    public void AvecQuantiteEtUnite_EstValide()
    {
        Assert.Empty(new AjoutManuel(12, 2m, 6).Valider());
    }

    [Fact]
    public void SansIngredient_EstRefuse()
    {
        Assert.Equal(["Choisissez un article du référentiel."], new AjoutManuel(0, null, null).Valider());
    }

    [Fact]
    public void QuantiteSansUnite_EstRefusee()
    {
        Assert.Equal(["Choisissez l'unité de la quantité, ou laissez la quantité vide."], new AjoutManuel(12, 2m, null).Valider());
    }

    [Fact]
    public void UniteSansQuantite_EstRefusee()
    {
        Assert.Equal(["Indiquez la quantité, ou retirez l'unité."], new AjoutManuel(12, null, 6).Valider());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    public void QuantiteHorsLimites_EstRefusee(decimal quantite)
    {
        // En français, quelle que soit la culture de la machine (les milliers séparés par une espace).
        Assert.Equal(
            ["La quantité doit être plus grande que 0 et au plus 100 000."],
            new AjoutManuel(12, quantite, 6).Valider().Select(e => e.Replace(' ', ' ').Replace(' ', ' ')));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, 2.0, 2.0)]   // l'article était là sans quantité : la quantité s'ajoute
    [InlineData(1.0, null, 1.0)]   // ajouté de nouveau sans quantité : rien ne change
    [InlineData(1.0, 2.0, 3.0)]
    public void Additionner(double? existante, double? ajout, double? attendu)
    {
        Assert.Equal((decimal?)attendu, AjoutManuel.Additionner((decimal?)existante, (decimal?)ajout));
    }
}
