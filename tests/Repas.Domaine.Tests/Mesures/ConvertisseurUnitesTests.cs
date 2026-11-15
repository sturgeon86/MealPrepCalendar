using Repas.Domaine.Mesures;

namespace Repas.Domaine.Tests.Mesures;

public sealed class ConvertisseurUnitesTests
{
    // Mêmes unités et facteurs que les données de base (db/seed/0002_unites.sql).
    private static readonly Unite G = new(1, "g", TypeMesure.Poids, 1);
    private static readonly Unite Kg = new(2, "kg", TypeMesure.Poids, 1000);
    private static readonly Unite Lb = new(3, "lb", TypeMesure.Poids, 453.59237m);
    private static readonly Unite Ml = new(5, "ml", TypeMesure.Volume, 1);
    private static readonly Unite L = new(6, "l", TypeMesure.Volume, 1000);
    private static readonly Unite Tasse = new(7, "tasse", TypeMesure.Volume, 250);
    private static readonly Unite CSoupe = new(8, "c. à soupe", TypeMesure.Volume, 15);
    private static readonly Unite CThe = new(9, "c. à thé", TypeMesure.Volume, 5);
    private static readonly Unite Piece = new(10, "unité", TypeMesure.Unite, 1);
    private static readonly Unite Douzaine = new(11, "douzaine", TypeMesure.Unite, 12);

    private static readonly ConvertisseurUnites Convertisseur =
        new([G, Kg, Lb, Ml, L, Tasse, CSoupe, CThe, Piece, Douzaine]);

    // Ingrédients types.
    private static readonly MesuresIngredient Farine = new(G.Id, densiteGParMl: 0.5m);
    private static readonly MesuresIngredient Lait = new(Ml.Id, densiteGParMl: 1.03m);
    private static readonly MesuresIngredient Oignon = new(Piece.Id, poidsMoyenUniteG: 150m);
    private static readonly MesuresIngredient Poulet = new(G.Id, poidsMoyenUniteG: 225m);
    private static readonly MesuresIngredient Cumin = new(Ml.Id);
    private static readonly MesuresIngredient OignonComplet = new(Piece.Id, densiteGParMl: 0.6m, poidsMoyenUniteG: 150m);

    private static decimal Reussir(ResultatConversion resultat)
    {
        Assert.True(resultat.Reussie, $"Conversion échouée : {resultat.Raisons}");
        return resultat.Quantite;
    }

    // --- Même type de mesure : les facteurs suffisent, sans densité ni poids moyen.

    [Theory]
    [InlineData(1, "tasse", "ml", 250)]
    [InlineData(2, "c. à soupe", "ml", 30)]
    [InlineData(3, "c. à thé", "c. à soupe", 1)]
    [InlineData(1500, "ml", "l", 1.5)]
    [InlineData(1.2, "kg", "g", 1200)]
    [InlineData(1, "lb", "g", 453.59237)]
    [InlineData(2, "douzaine", "unité", 24)]
    [InlineData(0, "tasse", "ml", 0)]
    public void MemeType_ParLesFacteurs(decimal quantite, string codeSource, string codeCible, decimal attendu)
    {
        var source = TrouverUnite(codeSource);
        var cible = TrouverUnite(codeCible);

        var resultat = Convertisseur.Convertir(quantite, source.Id, cible.Id, Cumin);

        Assert.Equal(attendu, Reussir(resultat));
    }

    [Fact]
    public void ExempleDeLaSpecification_250mlPlusUneTasseDeLait_Font500ml()
    {
        var total = Reussir(Convertisseur.VersReference(250, Ml.Id, Lait))
            + Reussir(Convertisseur.VersReference(1, Tasse.Id, Lait));

        Assert.Equal(500m, total);
    }

    [Fact]
    public void MemeUnite_RendLaMemeQuantite()
    {
        Assert.Equal(3.75m, Reussir(Convertisseur.Convertir(3.75m, Tasse.Id, Tasse.Id, Cumin)));
    }

    // --- Volume ↔ poids : par la densité.

    [Fact]
    public void VolumeVersPoids_ParLaDensite()
    {
        // Spécification : 1 tasse de farine = 125 g.
        Assert.Equal(125m, Reussir(Convertisseur.VersReference(1, Tasse.Id, Farine)));
    }

    [Fact]
    public void PoidsVersVolume_ParLaDensite()
    {
        Assert.Equal(1m, Reussir(Convertisseur.Convertir(125, G.Id, Tasse.Id, Farine)));
    }

    [Fact]
    public void VolumeVersPoids_CombineLesFacteursDesDeuxUnites()
    {
        // 2 l de lait = 2000 ml × 1,03 g/ml = 2060 g = 2,06 kg.
        Assert.Equal(2.06m, Reussir(Convertisseur.Convertir(2, L.Id, Kg.Id, Lait)));
    }

    [Fact]
    public void VolumeVersPoids_SansDensite_EchoueEnLeSignalant()
    {
        var resultat = Convertisseur.Convertir(1, Tasse.Id, G.Id, Cumin);

        Assert.False(resultat.Reussie);
        Assert.Equal(RaisonEchecConversion.DensiteManquante, resultat.Raisons);
        Assert.Equal(0m, resultat.Quantite);
    }

    [Fact]
    public void PoidsVersVolume_SansDensite_EchoueEnLeSignalant()
    {
        var resultat = Convertisseur.VersReference(10, G.Id, Cumin);

        Assert.Equal(RaisonEchecConversion.DensiteManquante, resultat.Raisons);
    }

    // --- Pièce ↔ poids : par le poids moyen.

    [Fact]
    public void PoidsVersPieces_ParLePoidsMoyen()
    {
        // 450 g d'oignons, à 150 g l'oignon = 3 oignons.
        Assert.Equal(3m, Reussir(Convertisseur.VersReference(450, G.Id, Oignon)));
    }

    [Fact]
    public void PiecesVersPoids_ParLePoidsMoyen()
    {
        // 2 poitrines de poulet à 225 g = 450 g.
        Assert.Equal(450m, Reussir(Convertisseur.VersReference(2, Piece.Id, Poulet)));
    }

    [Fact]
    public void DouzaineVersPoids_CombineLeFacteurEtLePoidsMoyen()
    {
        var oeufs = new MesuresIngredient(Piece.Id, poidsMoyenUniteG: 50m);

        Assert.Equal(0.6m, Reussir(Convertisseur.Convertir(1, Douzaine.Id, Kg.Id, oeufs)));
    }

    [Fact]
    public void PiecesVersPoids_SansPoidsMoyen_EchoueEnLeSignalant()
    {
        var resultat = Convertisseur.VersReference(2, Piece.Id, Farine);

        Assert.Equal(RaisonEchecConversion.PoidsMoyenManquant, resultat.Raisons);
    }

    // --- Volume ↔ pièce : en passant par le poids.

    [Fact]
    public void VolumeVersPieces_PasseParLePoids()
    {
        // 1 tasse d'oignon haché = 250 ml × 0,6 g/ml = 150 g = 1 oignon.
        Assert.Equal(1m, Reussir(Convertisseur.VersReference(1, Tasse.Id, OignonComplet)));
    }

    [Fact]
    public void PiecesVersVolume_PasseParLePoids()
    {
        Assert.Equal(1m, Reussir(Convertisseur.Convertir(1, Piece.Id, Tasse.Id, OignonComplet)));
    }

    [Fact]
    public void VolumeVersPieces_SansDensite_SignaleLaDensite()
    {
        var resultat = Convertisseur.VersReference(1, Tasse.Id, Oignon);

        Assert.Equal(RaisonEchecConversion.DensiteManquante, resultat.Raisons);
    }

    [Fact]
    public void VolumeVersPieces_SansRien_SignaleLesDeuxManques()
    {
        var resultat = Convertisseur.Convertir(1, Tasse.Id, Piece.Id, new MesuresIngredient(Piece.Id));

        Assert.Equal(
            RaisonEchecConversion.DensiteManquante | RaisonEchecConversion.PoidsMoyenManquant,
            resultat.Raisons);
    }

    // --- Unités inconnues et entrées invalides.

    [Fact]
    public void UniteSourceInconnue_EchoueEnLeSignalant()
    {
        var resultat = Convertisseur.VersReference(1, uniteSourceId: 999, Farine);

        Assert.Equal(RaisonEchecConversion.UniteInconnue, resultat.Raisons);
    }

    [Fact]
    public void UniteDeReferenceInconnue_EchoueEnLeSignalant()
    {
        var resultat = Convertisseur.VersReference(1, G.Id, new MesuresIngredient(uniteReferenceId: 999));

        Assert.Equal(RaisonEchecConversion.UniteInconnue, resultat.Raisons);
    }

    [Fact]
    public void QuantiteNegative_EstRefusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Convertisseur.Convertir(-1, G.Id, Kg.Id, Farine));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-250)]
    public void FacteurDUniteNonPositif_EstRefuse(decimal facteur)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Unite(1, "x", TypeMesure.Volume, facteur));
    }

    [Fact]
    public void DensiteOuPoidsMoyenNonPositif_EstRefuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MesuresIngredient(1, densiteGParMl: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MesuresIngredient(1, poidsMoyenUniteG: -5));
    }

    [Fact]
    public void UnEchecDoitAvoirUneRaison()
    {
        Assert.Throws<ArgumentException>(() => ResultatConversion.Echec(RaisonEchecConversion.Aucune));
    }

    [Fact]
    public void DeuxUnitesAvecLeMemeId_SontRefusees()
    {
        Assert.Throws<ArgumentException>(() => new ConvertisseurUnites([G, new Unite(G.Id, "autre", TypeMesure.Poids, 2)]));
    }

    // --- Précision : aucun arrondi pendant le calcul.

    [Fact]
    public void AllerRetour_ConserveLaQuantite()
    {
        var enGrammes = Reussir(Convertisseur.Convertir(1, Lb.Id, G.Id, Farine));
        var retour = Reussir(Convertisseur.Convertir(enGrammes, G.Id, Lb.Id, Farine));

        Assert.Equal(1m, retour);
    }

    private static Unite TrouverUnite(string code) =>
        new[] { G, Kg, Lb, Ml, L, Tasse, CSoupe, CThe, Piece, Douzaine }.Single(u => u.Code == code);
}
