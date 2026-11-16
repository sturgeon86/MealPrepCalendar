using Repas.Domaine.Import;
using Repas.Domaine.Recettes;

namespace Repas.Domaine.Tests.Import;

public sealed class LectureTexteRecetteTests
{
    [Fact]
    public void RecetteAvecIntertitres()
    {
        const string texte = """
            Pâté chinois

            Un classique du mardi soir.
            Portions : 6
            Préparation : 20 min
            Cuisson : 1 h 15
            Temps total : 1 h 35

            Ingrédients
            1 lb de boeuf haché
            1 oignon, haché
            Purée :
            6 pommes de terre
            1 boîte (398 ml) de maïs en crème

            Préparation
            1. Préchauffer le four à 375 °F.
            2) Cuire le boeuf et l'oignon.
            Étape 3
            Monter le pâté et cuire 45 minutes.
            """;

        var brouillon = LectureTexteRecette.Lire(texte);

        Assert.Equal("Pâté chinois", brouillon.Nom);
        Assert.Equal(6, brouillon.Portions);
        Assert.Equal(20, brouillon.TempsPreparationMin);
        Assert.Equal(75, brouillon.TempsCuissonMin);
        Assert.Equal("Un classique du mardi soir.", brouillon.Notes);
        Assert.Equal(
            ["1 lb de boeuf haché", "1 oignon, haché", "6 pommes de terre", "1 boîte (398 ml) de maïs en crème"],
            brouillon.Ingredients);
        Assert.Equal(
            ["Préchauffer le four à 375 °F.", "Cuire le boeuf et l'oignon.", "Monter le pâté et cuire 45 minutes."],
            brouillon.Etapes);
        Assert.Equal(SourceRecette.Texte, brouillon.Source);
    }

    [Fact]
    public void SansIntertitres_LesLignesAvecQuantiteSontDesIngredients()
    {
        const string texte = """
            Vinaigrette maison
            3 c. à soupe d'huile d'olive
            1 c. à soupe de vinaigre balsamique
            ½ c. à thé de moutarde de Dijon
            Mélanger tous les ingrédients dans un pot.
            Agiter avant de servir.
            """;

        var brouillon = LectureTexteRecette.Lire(texte);

        Assert.Equal("Vinaigrette maison", brouillon.Nom);
        Assert.Equal(3, brouillon.Ingredients.Count);
        Assert.Equal(["Mélanger tous les ingrédients dans un pot.", "Agiter avant de servir."], brouillon.Etapes);
        Assert.Null(brouillon.Portions);
        Assert.Null(brouillon.Notes);
    }

    [Theory]
    [InlineData("Portions : 4", 4)]
    [InlineData("Portions: 8", 8)]
    [InlineData("Donne 6 portions", 6)]
    [InlineData("Pour 4 personnes", 4)]
    [InlineData("8 portions", 8)]
    [InlineData("Servings: 2", 2)]
    [InlineData("Rendement : 12", 12)]
    public void Portions(string ligne, int attendues)
    {
        var brouillon = LectureTexteRecette.Lire($"Muffins\n{ligne}\nIngrédients\n2 tasses de farine");

        Assert.Equal(attendues, brouillon.Portions);
        Assert.Equal(["2 tasses de farine"], brouillon.Ingredients);
    }

    [Theory]
    [InlineData("45 min", 45)]
    [InlineData("45 minutes", 45)]
    [InlineData("1 h 30", 90)]
    [InlineData("1h30", 90)]
    [InlineData("1 h 30 min", 90)]
    [InlineData("2 h", 120)]
    [InlineData("1 heure 15 min", 75)]
    [InlineData("2 heures", 120)]
    [InlineData("90", 90)]
    public void Durees(string texte, int minutes) => Assert.Equal(minutes, LectureTexteRecette.LireDuree(texte));

    [Theory]
    [InlineData("")]
    [InlineData("environ une heure")]
    [InlineData("h")]
    [InlineData("200 h")]
    public void DureesIllisibles(string texte) => Assert.Null(LectureTexteRecette.LireDuree(texte));

    [Fact]
    public void IntertitresAnglaisEtMajuscules()
    {
        const string texte = """
            Pancakes
            INGREDIENTS:
            2 cups flour
            1 egg
            DIRECTIONS:
            Step 1: Mix everything.
            Cook on a griddle.
            """;

        var brouillon = LectureTexteRecette.Lire(texte);

        Assert.Equal(["2 cups flour", "1 egg"], brouillon.Ingredients);
        Assert.Equal(["Mix everything.", "Cook on a griddle."], brouillon.Etapes);
    }

    [Fact]
    public void EspacesInsecablesEtFinsDeLigneWindows()
    {
        var brouillon = LectureTexteRecette.Lire("Soupe\r\nIngrédients\r\n1 l de bouillon\r\nPréparation\r\nChauffer.\r\n");

        Assert.Equal("Soupe", brouillon.Nom);
        Assert.Equal(["1 l de bouillon"], brouillon.Ingredients);
        Assert.Equal(["Chauffer."], brouillon.Etapes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n  ")]
    public void TexteVide(string? texte)
    {
        var brouillon = LectureTexteRecette.Lire(texte);

        Assert.Equal(string.Empty, brouillon.Nom);
        Assert.Empty(brouillon.Ingredients);
        Assert.Empty(brouillon.Etapes);
    }

    [Fact]
    public void PreparerLitEtRapprocheChaqueLigne()
    {
        var rapprochement = new RapprochementIngredients(
        [
            new IngredientConnu(1, "Oignon jaune", []),
            new IngredientConnu(2, "Oignon rouge", []),
            new IngredientConnu(3, "Bœuf haché", []),
        ]);

        var importes = PreparationImport.Preparer(
            ["1 lb de boeuf haché", "2 oignons, hachés", "", "1 tasse de safran", "   "],
            rapprochement);

        Assert.Equal(3, importes.Count);
        Assert.Equal((StatutRapprochement.Exact, 3), (importes[0].Rapprochement.Statut, importes[0].Rapprochement.Propose!.IngredientId));
        Assert.Equal("lb", importes[0].Lue.CodeUnite);
        Assert.Equal(StatutRapprochement.Ambigu, importes[1].Rapprochement.Statut);
        Assert.Equal("hachés", importes[1].Lue.Precision);
        Assert.Equal(StatutRapprochement.ACreer, importes[2].Rapprochement.Statut);
        Assert.Equal("safran", importes[2].Lue.Nom);
    }
}
