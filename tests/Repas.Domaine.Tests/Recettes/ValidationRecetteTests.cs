using Repas.Domaine.Recettes;

namespace Repas.Domaine.Tests.Recettes;

public sealed class ValidationRecetteTests
{
    private static readonly LigneIngredient Farine = new(1, 2m, 7, "tamisée", false, "Farine tout usage");
    private static readonly LigneIngredient SelAuGout = new(2, null, null, null, true, "Sel");

    private static FicheRecette Recette(
        string nom = "Crêpes",
        int portions = 4,
        int? preparation = 10,
        int? cuisson = 20,
        LigneIngredient[]? ingredients = null,
        string[]? etapes = null,
        string[]? etiquettes = null) =>
        new(0, nom, portions, preparation, cuisson, null, SourceRecette.Manuelle,
            ingredients ?? [Farine, SelAuGout], etapes ?? ["Mélanger."], etiquettes ?? ["Déjeuner"]);

    [Fact]
    public void RecetteValide_AucuneErreur()
    {
        Assert.Empty(ValidationRecette.Valider(Recette()));
    }

    [Fact]
    public void RecetteSansIngredientNiEtape_EstAcceptee()
    {
        // Une recette peut être saisie en plusieurs fois.
        Assert.Empty(ValidationRecette.Valider(Recette(ingredients: [], etapes: [], etiquettes: [])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void NomVide_EstRefuse(string nom)
    {
        Assert.Contains("Le nom de la recette est requis.", ValidationRecette.Valider(Recette(nom: nom)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PortionsHorsBornes_SontRefusees(int portions)
    {
        Assert.Single(ValidationRecette.Valider(Recette(portions: portions)));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, 20000)]
    public void TempsHorsBornes_EstRefuse(int? preparation, int? cuisson)
    {
        Assert.Single(ValidationRecette.Valider(Recette(preparation: preparation, cuisson: cuisson)));
    }

    [Fact]
    public void TempsInconnus_SontAcceptes()
    {
        Assert.Empty(ValidationRecette.Valider(Recette(preparation: null, cuisson: null)));
    }

    [Fact]
    public void QuantiteSansUnite_EstRefuseeEnNommantLIngredient()
    {
        var erreurs = ValidationRecette.Valider(Recette(ingredients: [Farine with { UniteId = null }]));

        Assert.Equal(["Choisissez l'unité de « Farine tout usage »."], erreurs);
    }

    [Fact]
    public void UniteSansQuantite_EstRefusee()
    {
        Assert.Single(ValidationRecette.Valider(Recette(ingredients: [Farine with { Quantite = null }])));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(200000)]
    public void QuantiteHorsBornes_EstRefusee(decimal quantite)
    {
        Assert.Single(ValidationRecette.Valider(Recette(ingredients: [Farine with { Quantite = quantite }])));
    }

    [Fact]
    public void LigneSansIngredient_EstRefusee()
    {
        var erreurs = ValidationRecette.Valider(Recette(ingredients: [SelAuGout, new LigneIngredient(0, null, null, null, false)]));

        Assert.Equal(["Choisissez l'ingrédient de la ligne 2."], erreurs);
    }

    [Fact]
    public void TextesTropLongs_SontRefuses()
    {
        var erreurs = ValidationRecette.Valider(Recette(
            ingredients: [Farine with { Precision = new string('x', 201) }],
            etapes: ["Ok.", new string('x', 2001)],
            etiquettes: [new string('x', 51)]));

        Assert.Equal(3, erreurs.Count);
        Assert.Contains("L'étape 2 ne doit pas dépasser 2000 caractères.", erreurs);
    }

    [Fact]
    public void Etapes_SontNettoyeesEnGardantLOrdre()
    {
        Assert.Equal(
            ["Préchauffer le four.", "Mélanger."],
            ValidationRecette.NormaliserEtapes(["  Préchauffer le four. ", "", null, "   ", "Mélanger."]));
    }

    [Fact]
    public void Etiquettes_SontNettoyeesSansDoublons()
    {
        Assert.Equal(
            ["Végétarien", "Souper rapide"],
            ValidationRecette.NormaliserEtiquettes([" Végétarien ", "végétarien", "", null, "Souper rapide"]));
    }
}
