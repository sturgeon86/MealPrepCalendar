using Repas.Domaine.Import;

namespace Repas.Domaine.Tests.Import;

public sealed class LectureLigneIngredientTests
{
    private static LigneLue Lire(string ligne) =>
        LectureLigneIngredient.Lire(ligne) ?? throw new InvalidOperationException("Ligne vide.");

    [Fact]
    public void ExempleDeLaStory()
    {
        var ligne = Lire("1 ½ tasse de farine tout usage, tamisée");

        Assert.Equal(1.5m, ligne.Quantite);
        Assert.Equal("tasse", ligne.CodeUnite);
        Assert.Equal("farine tout usage", ligne.Nom);
        Assert.Equal("tamisée", ligne.Precision);
        Assert.False(ligne.EstOptionnel);
        Assert.Equal("1 ½ tasse de farine tout usage, tamisée", ligne.Texte);
    }

    [Theory]
    [InlineData("1/2 tasse de lait", 0.5)]
    [InlineData("1 1/2 tasse de lait", 1.5)]
    [InlineData("1½ tasse de lait", 1.5)]
    [InlineData("¾ tasse de lait", 0.75)]
    [InlineData("2,5 tasses de lait", 2.5)]
    [InlineData("2.5 cups milk", 2.5)]
    [InlineData("une tasse de lait", 1)]
    public void Fractions(string texte, decimal attendue)
    {
        var ligne = Lire(texte);

        Assert.Equal(attendue, ligne.Quantite);
        Assert.Equal("tasse", ligne.CodeUnite);
        Assert.Equal(texte.Contains("milk") ? "milk" : "lait", ligne.Nom);
    }

    [Theory]
    [InlineData("2 tasses de sucre", "tasse")]
    [InlineData("2 c. à soupe d'huile", "c. à soupe")]
    [InlineData("2 c. à s. d'huile", "c. à soupe")]
    [InlineData("2 c.à.s. d'huile", "c. à soupe")]
    [InlineData("2 cuillères à soupe d'huile", "c. à soupe")]
    [InlineData("2 c. à table d'huile", "c. à soupe")]
    [InlineData("2 tbsp oil", "c. à soupe")]
    [InlineData("2 c. à thé de sel", "c. à thé")]
    [InlineData("2 c. à t. de sel", "c. à thé")]
    [InlineData("2 cuillère à café de sel", "c. à thé")]
    [InlineData("2 tsp salt", "c. à thé")]
    [InlineData("2 cups flour", "tasse")]
    [InlineData("2 ml de vanille", "ml")]
    [InlineData("2 l de bouillon", "l")]
    [InlineData("2 litres de bouillon", "l")]
    [InlineData("2 g de levure", "g")]
    [InlineData("2 kg de pommes de terre", "kg")]
    [InlineData("2 lb de boeuf haché", "lb")]
    [InlineData("2 lbs ground beef", "lb")]
    [InlineData("2 livres de boeuf", "lb")]
    [InlineData("2 oz de fromage", "oz")]
    [InlineData("2 douzaines d'oeufs", "douzaine")]
    public void UnitesPlurielAbregeesEtAnglaises(string texte, string codeAttendu)
    {
        var ligne = Lire(texte);

        Assert.Equal(2m, ligne.Quantite);
        Assert.Equal(codeAttendu, ligne.CodeUnite);
    }

    [Theory]
    [InlineData("2 c. à soupe d'huile d'olive", "huile d'olive")]
    [InlineData("1 tasse de la crème 35 %", "crème 35 %")]
    [InlineData("500 g de boeuf haché mi-maigre", "boeuf haché mi-maigre")]
    [InlineData("2 tbsp of olive oil", "olive oil")]
    public void LaLiaisonNeFaitPasPartieDuNom(string texte, string nomAttendu) =>
        Assert.Equal(nomAttendu, Lire(texte).Nom);

    [Fact]
    public void UniteColleeALaQuantite()
    {
        var ligne = Lire("500g de boeuf haché");

        Assert.Equal(500m, ligne.Quantite);
        Assert.Equal("g", ligne.CodeUnite);
        Assert.Equal("boeuf haché", ligne.Nom);
    }

    [Fact]
    public void UneLettreSeuleNEstPasUneUnite()
    {
        // « l » et « g » sont des unités, mais pas au début de « limes » ou de « gros ».
        var limes = Lire("2 limes");
        Assert.Equal(LectureLigneIngredient.CodeUnitePiece, limes.CodeUnite);
        Assert.Equal("limes", limes.Nom);

        var carottes = Lire("3 carottes, en rondelles");
        Assert.Equal(LectureLigneIngredient.CodeUnitePiece, carottes.CodeUnite);
        Assert.Equal("carottes", carottes.Nom);
        Assert.Equal("en rondelles", carottes.Precision);
    }

    [Fact]
    public void Pieces()
    {
        var ligne = Lire("2 oignons");

        Assert.Equal(2m, ligne.Quantite);
        Assert.Equal(LectureLigneIngredient.CodeUnitePiece, ligne.CodeUnite);
        Assert.Equal("oignons", ligne.Nom);
        Assert.Null(ligne.Precision);
    }

    [Theory]
    [InlineData("1 gros oignon, haché", "oignon", "gros, haché")]
    [InlineData("1 oignon moyen, haché", "oignon", "moyen, haché")]
    [InlineData("2 petites courgettes", "courgettes", "petites")]
    public void LaTailleVaDansLaPrecision(string texte, string nomAttendu, string precisionAttendue)
    {
        var ligne = Lire(texte);

        Assert.Equal(LectureLigneIngredient.CodeUnitePiece, ligne.CodeUnite);
        Assert.Equal(nomAttendu, ligne.Nom);
        Assert.Equal(precisionAttendue, ligne.Precision);
    }

    [Theory]
    [InlineData("Sel et poivre au goût", "Sel et poivre")]
    [InlineData("Sel, au goût", "Sel")]
    [InlineData("Poivre noir au besoin", "Poivre noir")]
    [InlineData("Salt to taste", "Salt")]
    public void AuGout(string texte, string nomAttendu)
    {
        var ligne = Lire(texte);

        Assert.Null(ligne.Quantite);
        Assert.Null(ligne.CodeUnite);
        Assert.Equal(nomAttendu, ligne.Nom);
        Assert.NotNull(ligne.Precision);
    }

    [Fact]
    public void AuGoutGardeLaQuantiteDonnee()
    {
        var ligne = Lire("1 c. à thé de sel, ou au goût");

        Assert.Equal(1m, ligne.Quantite);
        Assert.Equal("c. à thé", ligne.CodeUnite);
        Assert.Equal("sel", ligne.Nom);
        Assert.Equal("ou au goût", ligne.Precision);
    }

    [Theory]
    [InlineData("Une pincée de sel", "1 pincée")]
    [InlineData("pincée de muscade", "pincée")]
    [InlineData("Un filet d'huile d'olive", "1 filet")]
    public void UnePetiteQuantiteNEstPasComptee(string texte, string precisionAttendue)
    {
        var ligne = Lire(texte);

        Assert.Null(ligne.Quantite);
        Assert.Null(ligne.CodeUnite);
        Assert.Equal(precisionAttendue, ligne.Precision);
    }

    [Fact]
    public void BoiteAvecSaMesure()
    {
        var ligne = Lire("1 boîte (540 ml) de pois chiches, rincés et égouttés");

        Assert.Equal(540m, ligne.Quantite);
        Assert.Equal("ml", ligne.CodeUnite);
        Assert.Equal("pois chiches", ligne.Nom);
        Assert.Equal("1 boîte (540 ml), rincés et égouttés", ligne.Precision);
    }

    [Fact]
    public void PlusieursBoitesMultiplientLaMesure()
    {
        var ligne = Lire("2 boîtes (796 ml chacune) de tomates en dés");

        Assert.Equal(1592m, ligne.Quantite);
        Assert.Equal("ml", ligne.CodeUnite);
        Assert.Equal("tomates en dés", ligne.Nom);
        Assert.Equal("2 boîtes (796 ml chacune)", ligne.Precision);
    }

    [Fact]
    public void BoiteSuivieDeSaMesure()
    {
        // Tel qu'écrit sur un grand site de recettes québécois.
        var ligne = Lire("1 boîte de 540 ml (19 oz) de maïs en crème");

        Assert.Equal(540m, ligne.Quantite);
        Assert.Equal("ml", ligne.CodeUnite);
        Assert.Equal("maïs en crème", ligne.Nom);
        Assert.Equal("1 boîte de 540 ml, 19 oz", ligne.Precision);
    }

    [Fact]
    public void BoiteSansMesureCompteLesPieces()
    {
        var ligne = Lire("1 boîte de pâte de tomates");

        Assert.Equal(1m, ligne.Quantite);
        Assert.Equal(LectureLigneIngredient.CodeUnitePiece, ligne.CodeUnite);
        Assert.Equal("pâte de tomates", ligne.Nom);
        Assert.Equal("boîte", ligne.Precision);
    }

    [Fact]
    public void GoussesDAil()
    {
        var ligne = Lire("3 gousses d'ail, émincées");

        Assert.Equal(3m, ligne.Quantite);
        Assert.Equal(LectureLigneIngredient.CodeUnitePiece, ligne.CodeUnite);
        Assert.Equal("ail", ligne.Nom);
        Assert.Equal("gousses, émincées", ligne.Precision);
    }

    [Fact]
    public void UneParentheseApresUneVraieUniteResteUnePrecision()
    {
        var ligne = Lire("1 lb (454 g) de boeuf haché");

        Assert.Equal(1m, ligne.Quantite);
        Assert.Equal("lb", ligne.CodeUnite);
        Assert.Equal("boeuf haché", ligne.Nom);
        Assert.Equal("454 g", ligne.Precision);
    }

    [Fact]
    public void IntervallePrendLaPlusGrandeQuantite()
    {
        var ligne = Lire("2 à 3 tasses de bouillon de poulet");

        Assert.Equal(3m, ligne.Quantite);
        Assert.Equal("tasse", ligne.CodeUnite);
        Assert.Equal("bouillon de poulet", ligne.Nom);
        Assert.Equal("2 à 3", ligne.Precision);
    }

    [Theory]
    [InlineData("1 tasse de noix de Grenoble (facultatif)")]
    [InlineData("1 tasse de noix de Grenoble, facultatives")]
    [InlineData("1 cup walnuts (optional)")]
    public void Optionnel(string texte)
    {
        var ligne = Lire(texte);

        Assert.True(ligne.EstOptionnel);
        Assert.Null(ligne.Precision);
        Assert.DoesNotContain("(", ligne.Nom);
    }

    [Fact]
    public void PuceEtEspacesInsecables()
    {
        var ligne = Lire("• 250 ml de crème 35 %");

        Assert.Equal(250m, ligne.Quantite);
        Assert.Equal("ml", ligne.CodeUnite);
        Assert.Equal("crème 35 %", ligne.Nom);
    }

    [Fact]
    public void ApostropheTypographique()
    {
        var ligne = Lire("2 c. à soupe d’huile");

        Assert.Equal("huile", ligne.Nom);
    }

    [Fact]
    public void LaVirguleDUnNombreNeCoupePasLaLigne()
    {
        var ligne = Lire("1,5 kg de poulet, en cubes");

        Assert.Equal(1.5m, ligne.Quantite);
        Assert.Equal("kg", ligne.CodeUnite);
        Assert.Equal("poulet", ligne.Nom);
        Assert.Equal("en cubes", ligne.Precision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" - ")]
    public void LigneVide(string? texte)
    {
        var ligne = LectureLigneIngredient.Lire(texte);

        Assert.True(ligne is null || ligne.Nom.Length == 0);
    }

    [Fact]
    public void SansQuantiteNiUnite()
    {
        var ligne = Lire("Persil frais");

        Assert.Null(ligne.Quantite);
        Assert.Null(ligne.CodeUnite);
        Assert.Equal("Persil frais", ligne.Nom);
        Assert.Null(ligne.Precision);
    }
}
