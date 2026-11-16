using Repas.Domaine.Epicerie;
using Repas.Domaine.Mesures;
using Repas.Domaine.Recettes;

namespace Repas.Domaine.Tests.Epicerie;

public sealed class CalculListeTests
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

    private static readonly ConvertisseurUnites Convertisseur = new([G, Kg, Lb, Ml, L, Tasse, CSoupe, CThe, Piece]);

    // Ingrédients (Id → mesures), comme des fiches du référentiel.
    private const int Poulet = 101;     // en g, 1 poitrine ≈ 225 g
    private const int Lait = 102;       // en ml
    private const int Farine = 103;     // en g, densité inconnue
    private const int Oignon = 104;     // à l'unité, poids moyen inconnu
    private const int Sel = 105;        // en g, densité connue
    private const int Beurre = 106;     // en g, densité connue
    private const int Persil = 107;     // à l'unité, sans densité ni poids moyen

    private static readonly Dictionary<int, MesuresIngredient> Mesures = new()
    {
        [Poulet] = new(G.Id, poidsMoyenUniteG: 225m),
        [Lait] = new(Ml.Id, densiteGParMl: 1.03m),
        [Farine] = new(G.Id),
        [Oignon] = new(Piece.Id),
        [Sel] = new(G.Id, densiteGParMl: 1.2m),
        [Beurre] = new(G.Id, densiteGParMl: 0.91m),
        [Persil] = new(Piece.Id),
    };

    private static readonly CalculListe Calcul = new(Convertisseur, Mesures);

    private static LigneIngredient Ligne(int ingredientId, decimal? quantite, Unite? unite, bool facultatif = false) =>
        new(ingredientId, quantite, unite?.Id, null, facultatif);

    private static RepasACalculer Repas(int repasId, int recetteId, int portionsRecette, int portionsPlanifiees, params LigneIngredient[] lignes) =>
        new(repasId, recetteId, portionsRecette, portionsPlanifiees, lignes);

    private static ArticleCalcule Unique(IReadOnlyList<ArticleCalcule> articles, int ingredientId) =>
        Assert.Single(articles, a => a.IngredientId == ingredientId);

    // --- Scénarios de la story

    [Fact]
    public void TroisRecettesDePoulet_EnGrammesKilogrammesEtPoitrines_UneSeuleLigneEnGrammes()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, recetteId: 10, 4, 4, Ligne(Poulet, 500m, G)),            // Poulet au beurre
            Repas(2, recetteId: 11, 4, 4, Ligne(Poulet, 0.5m, Kg)),           // Tacos au poulet
            Repas(3, recetteId: 12, 2, 2, Ligne(Poulet, 1m, Piece)),          // Salade César : 1 poitrine
        ]);

        var poulet = Unique(articles, Poulet);
        Assert.Equal(1225m, poulet.Quantite);
        Assert.Equal(G.Id, poulet.UniteId);
        Assert.Equal(3, poulet.NbRecettes);
        Assert.False(poulet.AUneAlerte);
        Assert.Equal([new SourceArticle(1, 500m), new SourceArticle(2, 500m), new SourceArticle(3, 225m)], poulet.Sources);
    }

    [Fact]
    public void LaitEnMillilitresEtEnTasses_SAdditionneEnMillilitres()
    {
        // 250 ml + 1 tasse = 500 ml (exemple de la spécification).
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Lait, 250m, Ml)),
            Repas(2, 11, 4, 4, Ligne(Lait, 1m, Tasse)),
        ]);

        var lait = Unique(articles, Lait);
        Assert.Equal(500m, lait.Quantite);
        Assert.Equal(Ml.Id, lait.UniteId);
        Assert.Equal(2, lait.NbRecettes);
    }

    [Fact]
    public void FarineSansDensite_LeVolumeResteSurUneLigneSepareeAvecUneAlerte()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Farine, 500m, G)),       // Pain : en grammes
            Repas(2, 11, 4, 4, Ligne(Farine, 2m, Tasse)),     // Crêpes : en tasses
        ]);

        var lignes = articles.Where(a => a.IngredientId == Farine).ToList();
        Assert.Equal(2, lignes.Count);

        // La ligne convertie d'abord, puis celle à compléter, dans son unité d'origine.
        Assert.Equal((500m, G.Id, RaisonEchecConversion.Aucune, 1), (lignes[0].Quantite!.Value, lignes[0].UniteId!.Value, lignes[0].Alerte, lignes[0].NbRecettes));
        Assert.Equal((2m, Tasse.Id, RaisonEchecConversion.DensiteManquante, 1), (lignes[1].Quantite!.Value, lignes[1].UniteId!.Value, lignes[1].Alerte, lignes[1].NbRecettes));
        Assert.True(lignes[1].AUneAlerte);
    }

    [Fact]
    public void FarineSansDensite_DesVolumesDifferentsSAdditionnentQuandMeme()
    {
        // 2 tasses + 3 c. à soupe : même type de mesure, la densité n'est pas nécessaire pour les additionner.
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Farine, 2m, Tasse)),
            Repas(2, 11, 4, 4, Ligne(Farine, 3m, CSoupe)),
        ]);

        var farine = Unique(articles, Farine);
        Assert.Equal(545m, farine.Quantite);          // 500 ml + 45 ml
        Assert.Equal(Ml.Id, farine.UniteId);          // unité de base du volume
        Assert.Equal(RaisonEchecConversion.DensiteManquante, farine.Alerte);
        Assert.Equal([new SourceArticle(1, 500m), new SourceArticle(2, 45m)], farine.Sources);
    }

    [Fact]
    public void FarineSansDensite_UneSeuleUniteDOrigine_EstGardee()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Farine, 2m, Tasse)),
            Repas(2, 11, 4, 4, Ligne(Farine, 0.5m, Tasse)),
        ]);

        var farine = Unique(articles, Farine);
        Assert.Equal((2.5m, Tasse.Id), (farine.Quantite!.Value, farine.UniteId!.Value));
    }

    [Fact]
    public void OignonEnGrammesSansPoidsMoyen_AlertePoidsMoyenManquant()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Oignon, 2m, Piece)),
            Repas(2, 11, 4, 4, Ligne(Oignon, 300m, G)),
        ]);

        var lignes = articles.Where(a => a.IngredientId == Oignon).ToList();
        Assert.Equal([(2m, Piece.Id), (300m, G.Id)], lignes.Select(l => (l.Quantite!.Value, l.UniteId!.Value)));
        Assert.Equal(RaisonEchecConversion.PoidsMoyenManquant, lignes[1].Alerte);
    }

    [Fact]
    public void VolumeVersPieceSansRien_LesDeuxRaisonsSontSignalees()
    {
        var articles = Calcul.Calculer([Repas(1, 10, 4, 4, Ligne(Persil, 2m, CSoupe))]);

        Assert.Equal(
            RaisonEchecConversion.DensiteManquante | RaisonEchecConversion.PoidsMoyenManquant,
            Unique(articles, Persil).Alerte);
    }

    // --- Portions

    [Fact]
    public void LesQuantitesSuiventLesPortionsPlanifiees()
    {
        // Recette pour 4, planifiée pour 6 : × 1,5. Recette pour 4, planifiée pour 2 : × 0,5.
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, portionsRecette: 4, portionsPlanifiees: 6, Ligne(Lait, 500m, Ml)),
            Repas(2, 11, portionsRecette: 4, portionsPlanifiees: 2, Ligne(Lait, 1m, Tasse)),
        ]);

        var lait = Unique(articles, Lait);
        Assert.Equal(875m, lait.Quantite); // 750 + 125
        Assert.Equal([new SourceArticle(1, 750m), new SourceArticle(2, 125m)], lait.Sources);
    }

    [Fact]
    public void LeCalculResteExact_SansArrondi()
    {
        // ⅓ de tasse pour 3 portions, planifiée pour 1 portion : 250 ÷ 9 ml, sans arrondi.
        var articles = Calcul.Calculer([Repas(1, 10, 3, 1, Ligne(Lait, 1m / 3, Tasse))]);

        var quantite = Unique(articles, Lait).Quantite!.Value;
        Assert.Equal(27.778m, Math.Round(quantite, 3)); // 27,7777…
        Assert.True(quantite.Scale > 10);
    }

    // --- Nombre de recettes et sources

    [Fact]
    public void LaMemeRecettePlanifieeDeuxFois_CompteUneRecetteEtDeuxSources()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, recetteId: 10, 4, 4, Ligne(Poulet, 500m, G)),
            Repas(2, recetteId: 10, 4, 4, Ligne(Poulet, 500m, G)),
        ]);

        var poulet = Unique(articles, Poulet);
        Assert.Equal(1000m, poulet.Quantite);
        Assert.Equal(1, poulet.NbRecettes);
        Assert.Equal([1, 2], poulet.Sources.Select(s => s.RepasPlanifieId));
    }

    [Fact]
    public void LeMemeIngredientDeuxFoisDansUneRecette_UneSourceAvecLaSomme()
    {
        // Beurre pour la pâte et pour la garniture.
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Beurre, 100m, G), Ligne(Beurre, 2m, CSoupe)),
        ]);

        var beurre = Unique(articles, Beurre);
        Assert.Equal(127.3m, beurre.Quantite); // 100 g + 30 ml × 0,91
        Assert.Equal(1, beurre.NbRecettes);
        Assert.Equal([new SourceArticle(1, 127.3m)], beurre.Sources);
    }

    // --- Au goût et facultatif

    [Fact]
    public void AuGoutSeulement_UneLigneSansQuantite()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Sel, null, null)),
            Repas(2, 11, 4, 4, Ligne(Sel, null, null)),
        ]);

        var sel = Unique(articles, Sel);
        Assert.Null(sel.Quantite);
        Assert.Null(sel.UniteId);
        Assert.Equal(2, sel.NbRecettes);
        Assert.False(sel.AUneAlerte);
        Assert.Equal([new SourceArticle(1, null), new SourceArticle(2, null)], sel.Sources);
    }

    [Fact]
    public void AuGoutEtQuantite_SeRegroupentSurLaLigneQuantifiee()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Sel, null, null)),       // une pincée
            Repas(2, 11, 4, 4, Ligne(Sel, 1m, CThe)),         // 1 c. à thé = 6 g
        ]);

        var sel = Unique(articles, Sel);
        Assert.Equal(6m, sel.Quantite);
        Assert.Equal(G.Id, sel.UniteId);
        Assert.Equal(2, sel.NbRecettes);
        Assert.Equal([new SourceArticle(1, null), new SourceArticle(2, 6m)], sel.Sources);
    }

    [Fact]
    public void AuGout_RejointLaLigneConvertiePlutotQueCelleAvecAlerte()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Farine, 2m, Tasse)),     // non convertie
            Repas(2, 11, 4, 4, Ligne(Farine, 200m, G)),       // convertie
            Repas(3, 12, 4, 4, Ligne(Farine, null, null)),    // pour fariner
        ]);

        var lignes = articles.Where(a => a.IngredientId == Farine).ToList();
        Assert.Equal(2, lignes.Count);
        Assert.Equal([2, 3], lignes[0].Sources.Select(s => s.RepasPlanifieId));
        Assert.Equal(2, lignes[0].NbRecettes);
    }

    [Fact]
    public void LesIngredientsFacultatifs_NeSontPasAjoutes()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Lait, 250m, Ml), Ligne(Beurre, 2m, CSoupe, facultatif: true)),
        ]);

        Assert.Equal([Lait], articles.Select(a => a.IngredientId));
    }

    // --- Divers

    [Fact]
    public void AucunRepas_ListeVide()
    {
        Assert.Empty(Calcul.Calculer([]));
    }

    [Fact]
    public void LesArticlesSontTriesParIngredient()
    {
        var articles = Calcul.Calculer(
        [
            Repas(1, 10, 4, 4, Ligne(Sel, 1m, G), Ligne(Poulet, 1m, G), Ligne(Lait, 1m, Ml)),
        ]);

        Assert.Equal([Poulet, Lait, Sel], articles.Select(a => a.IngredientId));
    }

    [Fact]
    public void UneUniteInconnue_ResteSurSaLigneAvecUneAlerte()
    {
        var articles = Calcul.Calculer([Repas(1, 10, 4, 4, new LigneIngredient(Lait, 2m, 999, null, false))]);

        var lait = Unique(articles, Lait);
        Assert.Equal((2m, 999, RaisonEchecConversion.UniteInconnue), (lait.Quantite!.Value, lait.UniteId!.Value, lait.Alerte));
    }

    [Fact]
    public void UnIngredientSansMesures_EstUneErreurDeProgrammation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Calcul.Calculer([Repas(1, 10, 4, 4, Ligne(999, 1m, G))]));

        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public void UniteDeBase_EtTrouver()
    {
        Assert.Equal(Ml, Convertisseur.UniteDeBase(TypeMesure.Volume));
        Assert.Equal(G, Convertisseur.UniteDeBase(TypeMesure.Poids));
        Assert.Equal(Tasse, Convertisseur.Trouver(Tasse.Id));
        Assert.Null(Convertisseur.Trouver(999));
    }
}
