using Repas.Domaine.Import;
using Repas.Domaine.Recettes;

namespace Repas.Domaine.Tests.Import;

/// <summary>
/// Pages construites sur le modèle de ce que publient les sites de recettes québécois : un blogue WordPress (extension
/// de recettes, tout dans un <c>@graph</c>), un grand site de recettes (objet <c>Recipe</c> seul, étapes en sections)
/// et un magazine (liste d'objets, étapes en un seul texte avec du HTML).
/// </summary>
public sealed class LectureRecetteWebTests
{
    private static readonly Uri Adresse = new("https://www.exemple-recettes.ca/recettes/pate-chinois/");

    private static string Page(params string[] blocsJson) =>
        "<!doctype html><html lang=\"fr-CA\"><head><title>Recette</title>" +
        string.Concat(blocsJson.Select(j => $"<script type=\"application/ld+json\">{j}</script>")) +
        "</head><body><h1>Recette</h1></body></html>";

    private const string BlogueWordPress = """
        {
          "@context": "https://schema.org",
          "@graph": [
            { "@type": "Organization", "@id": "https://www.exemple-recettes.ca/#organization", "name": "Exemple" },
            { "@type": "WebPage", "@id": "https://www.exemple-recettes.ca/recettes/pate-chinois/", "name": "Pâté chinois" },
            {
              "@type": "Recipe",
              "name": "Pâté chinois de grand-maman",
              "description": "Le classique du mardi soir, avec du maïs en crème.",
              "image": [
                "https://www.exemple-recettes.ca/wp-content/uploads/pate-chinois.jpg",
                "https://www.exemple-recettes.ca/wp-content/uploads/pate-chinois-500x500.jpg"
              ],
              "recipeYield": ["6", "6 portions"],
              "prepTime": "PT20M",
              "cookTime": "PT1H15M",
              "totalTime": "PT1H35M",
              "recipeIngredient": [
                "1 lb (454 g) de bœuf haché",
                "1 oignon, haché",
                "1 boîte (398 ml) de maïs en crème",
                "6 pommes de terre, pelées et coupées en cubes"
              ],
              "recipeInstructions": [
                {
                  "@type": "HowToSection",
                  "name": "Purée",
                  "itemListElement": [
                    { "@type": "HowToStep", "text": "Cuire les pommes de terre dans l&#039;eau salée." },
                    { "@type": "HowToStep", "text": "Réduire en purée avec le beurre et le lait." }
                  ]
                },
                {
                  "@type": "HowToSection",
                  "name": "Montage",
                  "itemListElement": [
                    { "@type": "HowToStep", "text": "Dorer le bœuf et l'oignon." },
                    { "@type": "HowToStep", "text": "Étager le bœuf, le maïs et la purée. Cuire 45 minutes à 375 °F." }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string GrandSite = """
        {
          "@context": "http://schema.org/",
          "@type": "Recipe",
          "name": "Soupe aux pois &amp; jambon",
          "image": { "@type": "ImageObject", "url": "/images/soupe-pois.jpg", "width": 1200, "height": 800 },
          "recipeYield": "8 portions",
          "prepTime": "PT15M",
          "cookTime": "P0DT3H0M0S",
          "recipeIngredient": ["2 tasses de pois jaunes secs", "1 os de jambon", "<b>8 tasses</b> d'eau"],
          "recipeInstructions": [
            { "@type": "HowToStep", "name": "Étape 1", "text": "Rincer les pois." },
            { "@type": "HowToStep", "text": "Mettre tous les ingrédients dans une grande casserole et laisser mijoter 3 heures." }
          ]
        }
        """;

    private const string Magazine = """
        [
          { "@context": "https://schema.org", "@type": "BreadcrumbList", "itemListElement": [] },
          {
            "@context": "https://schema.org",
            "@type": ["Recipe", "NewsArticle"],
            "name": "Muffins aux bleuets",
            "recipeYield": 12,
            "cookTime": "PT25M",
            "recipeIngredient": ["2 tasses de farine tout usage", "1 tasse de bleuets"],
            "recipeInstructions": "<p>Préchauffer le four à 375 °F.</p><p>Mélanger les ingrédients secs.<br>Ajouter les bleuets.</p>"
          }
        ]
        """;

    [Fact]
    public void BlogueWordPress_RecetteDansUnGraph()
    {
        var brouillon = LectureRecetteWeb.Lire(Page(BlogueWordPress), Adresse);

        Assert.NotNull(brouillon);
        Assert.Equal("Pâté chinois de grand-maman", brouillon.Nom);
        Assert.Equal(6, brouillon.Portions);
        Assert.Equal(20, brouillon.TempsPreparationMin);
        Assert.Equal(75, brouillon.TempsCuissonMin);
        Assert.Equal("Le classique du mardi soir, avec du maïs en crème.", brouillon.Notes);
        Assert.Equal(4, brouillon.Ingredients.Count);
        Assert.Equal("1 lb (454 g) de bœuf haché", brouillon.Ingredients[0]);
        Assert.Equal(
            [
                "Purée : Cuire les pommes de terre dans l'eau salée.",
                "Réduire en purée avec le beurre et le lait.",
                "Montage : Dorer le bœuf et l'oignon.",
                "Étager le bœuf, le maïs et la purée. Cuire 45 minutes à 375 °F.",
            ],
            brouillon.Etapes);
        Assert.Equal("https://www.exemple-recettes.ca/wp-content/uploads/pate-chinois.jpg", brouillon.PhotoUrl);
        Assert.Equal(Adresse.AbsoluteUri, brouillon.SourceUrl);
        Assert.Equal(SourceRecette.Web, brouillon.Source);
    }

    [Fact]
    public void GrandSite_RecetteSeuleAvecImageObjectRelative()
    {
        var brouillon = LectureRecetteWeb.Lire(Page(GrandSite), Adresse);

        Assert.NotNull(brouillon);
        Assert.Equal("Soupe aux pois & jambon", brouillon.Nom);
        Assert.Equal(8, brouillon.Portions);
        Assert.Equal(15, brouillon.TempsPreparationMin);
        Assert.Equal(180, brouillon.TempsCuissonMin);
        Assert.Equal(["2 tasses de pois jaunes secs", "1 os de jambon", "8 tasses d'eau"], brouillon.Ingredients);
        Assert.Equal(
            ["Rincer les pois.", "Mettre tous les ingrédients dans une grande casserole et laisser mijoter 3 heures."],
            brouillon.Etapes);
        Assert.Equal("https://www.exemple-recettes.ca/images/soupe-pois.jpg", brouillon.PhotoUrl);
        Assert.Null(brouillon.Notes);
    }

    [Fact]
    public void Magazine_ListeEtTypeMultipleEtEtapesEnHtml()
    {
        var brouillon = LectureRecetteWeb.Lire(Page(Magazine), Adresse);

        Assert.NotNull(brouillon);
        Assert.Equal("Muffins aux bleuets", brouillon.Nom);
        Assert.Equal(12, brouillon.Portions);
        Assert.Null(brouillon.TempsPreparationMin);
        Assert.Equal(25, brouillon.TempsCuissonMin);
        Assert.Equal(["Préchauffer le four à 375 °F.", "Mélanger les ingrédients secs.", "Ajouter les bleuets."], brouillon.Etapes);
        Assert.Null(brouillon.PhotoUrl);
    }

    [Fact]
    public void UneSeuleSection_SonNomNePrecedePasLesEtapes()
    {
        const string json = """
            {
              "@type": "Recipe", "name": "Pâté chinois", "recipeIngredient": ["1 lb de bœuf haché"],
              "recipeInstructions": [
                { "@type": "HowToSection", "name": "Pâté chinois", "itemListElement": [
                  { "@type": "HowToStep", "text": "Cuire les pommes de terre." },
                  { "@type": "HowToStep", "text": "Monter le pâté." } ] }
              ]
            }
            """;

        Assert.Equal(["Cuire les pommes de terre.", "Monter le pâté."], LectureRecetteWeb.Lire(Page(json), Adresse)?.Etapes);
    }

    [Fact]
    public void UnBlocMalFormeNEmpechePasDeLireLesAutres()
    {
        var brouillon = LectureRecetteWeb.Lire(Page("{ ceci n'est pas du JSON", GrandSite), Adresse);

        Assert.Equal("Soupe aux pois & jambon", brouillon?.Nom);
    }

    [Fact]
    public void JsonDansUnCommentaireHtml()
    {
        var brouillon = LectureRecetteWeb.Lire(Page($"<!--{GrandSite}-->"), Adresse);

        Assert.Equal("Soupe aux pois & jambon", brouillon?.Nom);
    }

    [Theory]
    [InlineData("<html><body><h1>Pas de recette ici</h1></body></html>")]
    [InlineData("")]
    [InlineData(null)]
    public void PageSansRecette(string? html) => Assert.Null(LectureRecetteWeb.Lire(html, Adresse));

    [Fact]
    public void PageAvecDautresDonneesMaisSansRecette()
    {
        var html = Page("""{ "@context": "https://schema.org", "@type": "Article", "name": "10 idées de soupers" }""");

        Assert.Null(LectureRecetteWeb.Lire(html, Adresse));
    }

    [Theory]
    [InlineData("PT1H30M", 90)]
    [InlineData("PT45M", 45)]
    [InlineData("PT90M", 90)]
    [InlineData("P0DT1H0M0S", 60)]
    [InlineData("PT1H", 60)]
    [InlineData("pt20m", 20)]
    [InlineData("PT0.5H", 30)]
    [InlineData("PT1500S", 25)]
    public void DureesIso(string texte, int minutes) => Assert.Equal(minutes, LectureRecetteWeb.LireDureeIso(texte));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PT0M")]
    [InlineData("P")]
    [InlineData("20 minutes")]
    [InlineData("P30D")]
    public void DureesIsoAbsentesOuIllisibles(string? texte) => Assert.Null(LectureRecetteWeb.LireDureeIso(texte));
}
