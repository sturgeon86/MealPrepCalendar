using Repas.Domaine.Mesures;
using Repas.Domaine.Referentiel;

namespace Repas.Domaine.Tests.Referentiel;

public sealed class ValidationIngredientTests
{
    private static readonly Unite G = new(1, "g", TypeMesure.Poids, 1);
    private static readonly Unite Ml = new(2, "ml", TypeMesure.Volume, 1);
    private static readonly Unite[] Unites = [G, Ml];

    private static FicheIngredient Fiche(
        string nom = "Farine tout usage",
        int categorieId = 6,
        TypeMesure typeMesure = TypeMesure.Poids,
        int uniteReferenceId = 1,
        decimal? densite = 0.5m,
        decimal? poidsMoyen = null,
        params string[] synonymes) =>
        new(0, nom, categorieId, typeMesure, uniteReferenceId, densite, poidsMoyen, true, true, synonymes);

    [Fact]
    public void FicheValide_AucuneErreur()
    {
        Assert.Empty(ValidationIngredient.Valider(Fiche(synonymes: "Farine blanche"), Unites));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NomVide_EstRefuse(string nom)
    {
        Assert.Contains("Le nom est requis.", ValidationIngredient.Valider(Fiche(nom: nom), Unites));
    }

    [Fact]
    public void NomTropLong_EstRefuse()
    {
        var erreurs = ValidationIngredient.Valider(Fiche(nom: new string('a', 151)), Unites);

        Assert.Single(erreurs);
    }

    [Fact]
    public void CategorieAbsente_EstRefusee()
    {
        Assert.Contains("La catégorie est requise.", ValidationIngredient.Valider(Fiche(categorieId: 0), Unites));
    }

    [Fact]
    public void UniteDeReferenceInconnue_EstRefusee()
    {
        Assert.Contains("L'unité de référence est requise.", ValidationIngredient.Valider(Fiche(uniteReferenceId: 99), Unites));
    }

    [Fact]
    public void UniteDUnAutreTypeDeMesure_EstRefusee()
    {
        var erreurs = ValidationIngredient.Valider(Fiche(typeMesure: TypeMesure.Poids, uniteReferenceId: Ml.Id), Unites);

        Assert.Contains(erreurs, e => e.Contains("« ml »", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(500)]
    public void DensiteHorsBornes_EstRefusee(decimal densite)
    {
        Assert.Single(ValidationIngredient.Valider(Fiche(densite: densite), Unites));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_000)]
    public void PoidsMoyenHorsBornes_EstRefuse(decimal poids)
    {
        Assert.Single(ValidationIngredient.Valider(Fiche(poidsMoyen: poids), Unites));
    }

    [Fact]
    public void DensiteEtPoidsMoyenSontFacultatifs()
    {
        Assert.Empty(ValidationIngredient.Valider(Fiche(densite: null, poidsMoyen: null), Unites));
    }

    [Fact]
    public void PlusieursProblemes_SontTousSignales()
    {
        var erreurs = ValidationIngredient.Valider(Fiche(nom: "", categorieId: 0, densite: 0), Unites);

        Assert.Equal(3, erreurs.Count);
    }

    [Fact]
    public void Synonymes_SontNettoyes()
    {
        var synonymes = ValidationIngredient.NormaliserSynonymes(
            ["  Farine blanche ", "", null, "farine BLANCHE", "Farine tout usage", "Fleur", "   "],
            nom: "Farine tout usage");

        // Espaces retirés, vides et doublons (sans égard à la casse) enlevés, nom de l'ingrédient exclu.
        Assert.Equal(["Farine blanche", "Fleur"], synonymes);
    }

    [Fact]
    public void Synonymes_LesAccentsDistinguentDeuxTextes()
    {
        var synonymes = ValidationIngredient.NormaliserSynonymes(["pâte", "pâté"], nom: "Autre");

        Assert.Equal(2, synonymes.Count);
    }
}
