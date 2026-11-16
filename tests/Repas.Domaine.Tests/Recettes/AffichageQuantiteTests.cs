using Repas.Domaine.Recettes;

namespace Repas.Domaine.Tests.Recettes;

public sealed class AffichageQuantiteTests
{
    private const char Nbsp = ' ';

    private static string Texte(string attendu) => attendu.Replace('_', Nbsp);

    [Theory]
    [InlineData(2, 4, 8, 4)]      // double
    [InlineData(2, 4, 2, 1)]      // moitié
    [InlineData(1.5, 4, 4, 1.5)]  // mêmes portions : inchangé
    [InlineData(500, 4, 6, 750)]
    public void MettreALEchelle_MultipliePortionsVouluesSurPortionsDeLaRecette(
        decimal quantite, int portionsRecette, int portionsVoulues, decimal attendu)
    {
        Assert.Equal(attendu, AffichageQuantite.MettreALEchelle(quantite, portionsRecette, portionsVoulues));
    }

    [Fact]
    public void MettreALEchelle_ResteExactAvantLAffichage()
    {
        // 1 tasse pour 3 portions, ramenée à 1 portion puis multipliée : aucun arrondi en chemin.
        var unePortion = AffichageQuantite.MettreALEchelle(1m, 3, 1)!.Value;

        Assert.Equal(1m, Math.Round(unePortion * 3, 20));
    }

    [Fact]
    public void MettreALEchelle_AuGout_ResteAuGout()
    {
        Assert.Null(AffichageQuantite.MettreALEchelle(null, 4, 8));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    public void MettreALEchelle_PortionsInvalides_SontRefusees(int portionsRecette, int portionsVoulues)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AffichageQuantite.MettreALEchelle(1m, portionsRecette, portionsVoulues));
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(0.5, "½")]
    [InlineData(1.5, "1_½")]
    [InlineData(0.25, "¼")]
    [InlineData(0.75, "¾")]
    [InlineData(0.125, "⅛")]
    [InlineData(2.875, "2_⅞")]
    [InlineData(0.3333333333, "⅓")]
    [InlineData(0.6666666667, "⅔")]
    [InlineData(1.33, "1_⅓")]
    [InlineData(0.97, "1")]    // arrondi à l'entier suivant
    [InlineData(1.02, "1")]
    [InlineData(0.1, "⅛")]
    [InlineData(12.3, "12_½")] // au-delà de 10 : à la demie
    [InlineData(12.2, "12")]
    [InlineData(12.8, "13")]
    [InlineData(0.05, "0,05")] // moins d'un seizième : décimales
    public void EcrireFraction(decimal quantite, string attendu)
    {
        Assert.Equal(Texte(attendu), AffichageQuantite.EcrireFraction(quantite));
    }

    [Fact]
    public void EcrireFraction_UnTiersRamenePourDeuxFoisPlusDePortions_DonneDeuxTiers()
    {
        var quantite = AffichageQuantite.MettreALEchelle(1m / 3, 2, 4)!.Value;

        Assert.Equal("⅔", AffichageQuantite.EcrireFraction(quantite));
    }

    [Theory]
    [InlineData(500, "500")]
    [InlineData(1234.4, "1234")]
    [InlineData(166.666, "167")]
    [InlineData(12.25, "12,3")]
    [InlineData(12.0, "12")]
    [InlineData(2.255, "2,26")]
    [InlineData(1.5, "1,5")]
    [InlineData(0.001, "0,01")] // jamais « 0 »
    public void EcrireDecimales(decimal quantite, string attendu)
    {
        Assert.Equal(attendu, AffichageQuantite.EcrireDecimales(quantite));
    }

    [Theory]
    [InlineData(1.5, "tasse", "1_½_tasse")]
    [InlineData(2, "tasse", "2_tasses")]
    [InlineData(2.5, "tasse", "2_½_tasses")]
    [InlineData(1.75, "tasse", "1_¾_tasse")] // singulier sous 2, comme en français
    [InlineData(2, "douzaine", "2_douzaines")]
    [InlineData(2, "c. à soupe", "2_c. à soupe")]
    [InlineData(0.5, "c. à thé", "½_c. à thé")]
    [InlineData(750, "ml", "750_ml")]
    [InlineData(1.25, "kg", "1,25_kg")]
    [InlineData(333.3333, "g", "333_g")]
    [InlineData(2, "lb", "2_lb")]
    [InlineData(3, "unité", "3")]   // unité sous-entendue
    [InlineData(1.5, "unité", "1_½")]
    public void Formater_QuantiteEtUnite(decimal quantite, string unite, string attendu)
    {
        Assert.Equal(Texte(attendu), AffichageQuantite.Formater(quantite, unite));
    }
}
