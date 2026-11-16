using Repas.Domaine.Epicerie;
using Repas.Domaine.Mesures;

namespace Repas.Domaine.Tests.Epicerie;

public sealed class RegenerationTests
{
    private const int Lait = 102;
    private const int Farine = 103;
    private const int Sel = 105;
    private const int Ml = 5;
    private const int G = 1;
    private const int Tasse = 7;

    private static ArticleCalcule Calcule(int ingredientId, decimal? quantite, int? uniteId) =>
        new(ingredientId, quantite, quantite is null ? null : uniteId, 1, RaisonEchecConversion.Aucune, []);

    [Fact]
    public void LeMemeArticle_EstMisAJourEtGardeSaCase()
    {
        var plan = Regeneration.Planifier(
            [new ArticleExistant(10, Lait, Ml, 750m, EstCoche: true)],
            [Calcule(Lait, 500m, Ml)]);

        Assert.Equal([new MiseAJourArticle(10, Calcule(Lait, 500m, Ml), Decocher: false)], plan.MisesAJour);
        Assert.Empty(plan.Ajouts);
        Assert.Empty(plan.Suppressions);
    }

    [Fact]
    public void QuantiteAugmentee_LaCaseEstDecochee()
    {
        var plan = Regeneration.Planifier(
            [new ArticleExistant(10, Lait, Ml, 500m, EstCoche: true)],
            [Calcule(Lait, 750m, Ml)]);

        Assert.True(Assert.Single(plan.MisesAJour).Decocher);
    }

    [Fact]
    public void QuantiteAugmentee_SurUnArticleNonCoche_RienADecocher()
    {
        var plan = Regeneration.Planifier(
            [new ArticleExistant(10, Lait, Ml, 500m, EstCoche: false)],
            [Calcule(Lait, 750m, Ml)]);

        Assert.False(Assert.Single(plan.MisesAJour).Decocher);
    }

    [Theory]
    [InlineData(null, 5.0, true)]      // « au goût » devenu une quantité : il en faut davantage
    [InlineData(5.0, null, false)]     // devenu « au goût » : ce qui est acheté suffit
    [InlineData(null, null, false)]
    [InlineData(5.0, 5.0, false)]      // inchangé
    public void AuGout_EtQuantitesEgales(double? avant, double? apres, bool decocher)
    {
        var plan = Regeneration.Planifier(
            [new ArticleExistant(10, Sel, avant is null ? null : Ml, (decimal?)avant, EstCoche: true)],
            [Calcule(Sel, (decimal?)apres, Ml)]);

        // La clé inclut l'unité : « au goût » (sans unité) et une quantité en ml sont deux lignes différentes.
        if ((avant is null) == (apres is null))
        {
            Assert.Equal(decocher, Assert.Single(plan.MisesAJour).Decocher);
        }
        else
        {
            Assert.Single(plan.Ajouts);
            Assert.Equal([10], plan.Suppressions);
        }
    }

    [Fact]
    public void UnArticleQuiNEstPlusDemande_EstRetire_EtUnNouveauEstAjoute()
    {
        var plan = Regeneration.Planifier(
            [new ArticleExistant(10, Lait, Ml, 500m, EstCoche: false)],
            [Calcule(Farine, 300m, G)]);

        Assert.Empty(plan.MisesAJour);
        Assert.Equal([Calcule(Farine, 300m, G)], plan.Ajouts);
        Assert.Equal([10], plan.Suppressions);
    }

    [Fact]
    public void LUniteFaitPartieDeLaCle()
    {
        // La fiche de la farine a été complétée : la ligne en tasses (alerte) devient une ligne en grammes.
        var plan = Regeneration.Planifier(
            [new ArticleExistant(10, Farine, G, 200m, false), new ArticleExistant(11, Farine, Tasse, 2m, true)],
            [Calcule(Farine, 450m, G)]);

        Assert.Equal(10, Assert.Single(plan.MisesAJour).ArticleId);
        Assert.Empty(plan.Ajouts);
        Assert.Equal([11], plan.Suppressions);
    }

    [Fact]
    public void DeuxLignesDeMemeCle_LaPlusAncienneEstReprise_LAutreRetiree()
    {
        // Après une fusion d'ingrédients.
        var plan = Regeneration.Planifier(
            [new ArticleExistant(12, Lait, Ml, 250m, false), new ArticleExistant(10, Lait, Ml, 500m, true)],
            [Calcule(Lait, 750m, Ml)]);

        var miseAJour = Assert.Single(plan.MisesAJour);
        Assert.Equal((10, true), (miseAJour.ArticleId, miseAJour.Decocher));
        Assert.Equal([12], plan.Suppressions);
    }

    [Fact]
    public void AucunRepas_ToutEstRetire()
    {
        var plan = Regeneration.Planifier(
            [new ArticleExistant(11, Farine, G, 200m, false), new ArticleExistant(10, Lait, Ml, 500m, true)],
            []);

        Assert.Equal([10, 11], plan.Suppressions);
        Assert.Empty(plan.MisesAJour);
        Assert.Empty(plan.Ajouts);
    }

    [Fact]
    public void ListeVide_ToutEstAjoute()
    {
        var plan = Regeneration.Planifier([], [Calcule(Lait, 500m, Ml), Calcule(Farine, 300m, G)]);

        Assert.Equal(2, plan.Ajouts.Count);
    }
}
