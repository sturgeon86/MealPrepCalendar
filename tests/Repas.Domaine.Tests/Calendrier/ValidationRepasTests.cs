using Repas.Domaine.Calendrier;

namespace Repas.Domaine.Tests.Calendrier;

public sealed class ValidationRepasTests
{
    private static readonly DateOnly Lundi = new(2026, 11, 16);

    [Fact]
    public void RepasAvecRecette_EstValide()
    {
        Assert.Empty(ValidationRepas.Valider(RepasPlanifie.PourRecette(Lundi, Plage.Souper, 1, 4)));
    }

    [Fact]
    public void RepasLibre_EstValide()
    {
        Assert.Empty(ValidationRepas.Valider(RepasPlanifie.Libre(Lundi, Plage.Diner, "Restaurant")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NiRecetteNiTexte_EstRefuse(string? texte)
    {
        var erreurs = ValidationRepas.Valider(new RepasPlanifie(0, Lundi, Plage.Diner, null, texte, null));

        Assert.Equal(["Choisissez une recette ou décrivez le repas (ex. : Restaurant, Restes)."], erreurs);
    }

    [Fact]
    public void RecetteEtTexte_EstRefuse()
    {
        var erreurs = ValidationRepas.Valider(new RepasPlanifie(0, Lundi, Plage.Diner, 1, "Restes", 4));

        Assert.Contains("Un repas est une recette ou un texte libre, pas les deux.", erreurs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(101)]
    public void RecetteAvecPortionsInvalides_EstRefusee(int? portions)
    {
        var erreurs = ValidationRepas.Valider(new RepasPlanifie(0, Lundi, Plage.Souper, 1, null, portions));

        Assert.Equal(["Le nombre de portions doit être entre 1 et 100."], erreurs);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void PortionsAuxLimites_SontAcceptees(int portions)
    {
        Assert.Empty(ValidationRepas.Valider(RepasPlanifie.PourRecette(Lundi, Plage.Souper, 1, portions)));
    }

    [Fact]
    public void RepasLibreAvecPortions_EstRefuse()
    {
        var erreurs = ValidationRepas.Valider(new RepasPlanifie(0, Lundi, Plage.Diner, null, "Restaurant", 2));

        Assert.Equal(["Les portions ne s'appliquent qu'à une recette."], erreurs);
    }

    [Fact]
    public void TexteTropLong_EstRefuse()
    {
        var erreurs = ValidationRepas.Valider(RepasPlanifie.Libre(Lundi, Plage.Diner, new string('x', ValidationRepas.LongueurMaxTexteLibre + 1)));

        Assert.Equal(["La description ne doit pas dépasser 200 caractères."], erreurs);
    }

    [Fact]
    public void TexteALaLongueurMaximale_ApresRetraitDesEspaces_EstAccepte()
    {
        var texte = "  " + new string('x', ValidationRepas.LongueurMaxTexteLibre) + "  ";

        Assert.Empty(ValidationRepas.Valider(RepasPlanifie.Libre(Lundi, Plage.Diner, texte)));
    }

    [Fact]
    public void PlageInconnue_EstRefusee()
    {
        var erreurs = ValidationRepas.Valider(RepasPlanifie.Libre(Lundi, (Plage)7, "Collation"));

        Assert.Equal(["Choisissez le dîner ou le souper."], erreurs);
    }

    [Theory]
    [InlineData(Plage.Diner, "Dîner")]
    [InlineData(Plage.Souper, "Souper")]
    public void Libelle(Plage plage, string attendu)
    {
        Assert.Equal(attendu, plage.Libelle());
    }

    [Fact]
    public void Titre_EstLeTexteLibreOuLeNomDeLaRecette()
    {
        var recette = RepasPlanifie.PourRecette(Lundi, Plage.Diner, 1, 4) with { NomRecette = "Pâté chinois" };

        Assert.Equal("Restes", RepasPlanifie.Libre(Lundi, Plage.Diner, "Restes").Titre);
        Assert.Equal("Pâté chinois", recette.Titre);
    }
}
