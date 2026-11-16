using Repas.Domaine.Epicerie;
using Repas.Domaine.Mesures;

namespace Repas.Domaine.Tests.Epicerie;

public sealed class AlertesListeTests
{
    private static readonly Unite G = new(1, "g", TypeMesure.Poids, 1);
    private static readonly Unite Ml = new(5, "ml", TypeMesure.Volume, 1);
    private static readonly Unite Tasse = new(7, "tasse", TypeMesure.Volume, 250);
    private static readonly ConvertisseurUnites Convertisseur = new([G, Ml, Tasse]);

    private static ArticleListe Article(int? uniteId, MesuresIngredient mesures) =>
        new(1, 103, "Farine tout usage", "Épicerie sèche", 60, uniteId is null ? null : 2m, uniteId, null, 1, false, false, false, mesures);

    [Fact]
    public void LigneDansLUniteDeReference_SansAlerte()
    {
        Assert.Equal(RaisonEchecConversion.Aucune, AlertesListe.Verifier(Article(G.Id, new(G.Id)), Convertisseur));
    }

    [Fact]
    public void LigneSansQuantite_SansAlerte()
    {
        Assert.Equal(RaisonEchecConversion.Aucune, AlertesListe.Verifier(Article(null, new(G.Id)), Convertisseur));
    }

    [Fact]
    public void VolumeSansDensite_AlerteDensite()
    {
        Assert.Equal(RaisonEchecConversion.DensiteManquante, AlertesListe.Verifier(Article(Tasse.Id, new(G.Id)), Convertisseur));
    }

    [Fact]
    public void VolumeAvecDensite_SansAlerte()
    {
        // La fiche a été complétée depuis la génération : la regénération regroupera la ligne.
        Assert.Equal(RaisonEchecConversion.Aucune, AlertesListe.Verifier(Article(Tasse.Id, new(G.Id, densiteGParMl: 0.5m)), Convertisseur));
    }

    [Theory]
    [InlineData(RaisonEchecConversion.Aucune, "")]
    [InlineData(RaisonEchecConversion.DensiteManquante, "Pour regrouper cette ligne, il manque la densité dans la fiche de l'ingrédient.")]
    [InlineData(RaisonEchecConversion.DensiteManquante | RaisonEchecConversion.PoidsMoyenManquant,
        "Pour regrouper cette ligne, il manque la densité et le poids moyen d'une unité dans la fiche de l'ingrédient.")]
    public void Decrire(RaisonEchecConversion raisons, string attendu)
    {
        Assert.Equal(attendu, AlertesListe.Decrire(raisons));
    }
}
