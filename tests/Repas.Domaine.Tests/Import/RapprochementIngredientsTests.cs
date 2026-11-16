using Repas.Domaine.Import;

namespace Repas.Domaine.Tests.Import;

public sealed class RapprochementIngredientsTests
{
    // Un extrait du référentiel de base, avec quelques synonymes.
    private static readonly IngredientConnu[] Referentiel =
    [
        new(1, "Oignon jaune", ["Oignon espagnol"]),
        new(2, "Oignon rouge", []),
        new(3, "Oignon vert", ["Échalote verte"]),
        new(4, "Bœuf haché", ["Boeuf haché mi-maigre"]),
        new(5, "Porc haché", []),
        new(6, "Lait 2 %", []),
        new(7, "Lait de coco", []),
        new(8, "Pâte de tomates", []),
        new(9, "Pois chiches", []),
        new(10, "Brocoli", []),
        new(11, "Farine tout usage", []),
        new(12, "Œufs", []),
        new(13, "Crème 35 %", []),
        new(14, "Huile d'olive", []),
        new(15, "Bouillon de poulet", []),
        new(16, "Poitrine de poulet", []),
        new(17, "Chou-fleur", []),
    ];

    private static readonly RapprochementIngredients Rapprochement = new(Referentiel);

    [Theory]
    [InlineData("Oignon jaune", 1)]
    [InlineData("oignons jaunes", 1)]
    [InlineData("OIGNON JAUNE", 1)]
    [InlineData("boeuf hache", 4)]
    [InlineData("pates de tomate", 8)]
    [InlineData("pâte tomates", 8)]
    [InlineData("pois chiche", 9)]
    [InlineData("oeufs", 12)]
    [InlineData("oeuf", 12)]
    [InlineData("crème 35%", 13)]
    [InlineData("huile d’olive", 14)]
    [InlineData("chou fleur", 17)]
    [InlineData("choux-fleurs", 17)]
    [InlineData("lait 2%", 6)]
    public void NomIdentique_SansEgardALaCasseAuxAccentsNiAuPluriel(string nom, int idAttendu)
    {
        var resultat = Rapprochement.Rapprocher(nom);

        Assert.Equal(StatutRapprochement.Exact, resultat.Statut);
        Assert.Equal(idAttendu, resultat.Propose?.IngredientId);
        Assert.Null(resultat.Propose?.Synonyme);
        Assert.Equal(1m, resultat.Propose?.Score);
    }

    [Fact]
    public void ParSynonyme()
    {
        var resultat = Rapprochement.Rapprocher("échalotes vertes");

        Assert.Equal(StatutRapprochement.Exact, resultat.Statut);
        Assert.Equal(3, resultat.Propose?.IngredientId);
        Assert.Equal("Oignon vert", resultat.Propose?.Nom);
        Assert.Equal("Échalote verte", resultat.Propose?.Synonyme);
    }

    [Fact]
    public void LeNomPasseAvantUnSynonymeDuMemeIngredient()
    {
        var resultat = Rapprochement.Rapprocher("boeuf haché mi-maigre");

        Assert.Equal(StatutRapprochement.Exact, resultat.Statut);
        Assert.Equal(4, resultat.Propose?.IngredientId);
        Assert.Equal("Boeuf haché mi-maigre", resultat.Propose?.Synonyme);
    }

    [Fact]
    public void Approche_UnMotDePlus()
    {
        var resultat = Rapprochement.Rapprocher("oignon jaune moyen");

        Assert.Equal(StatutRapprochement.Approche, resultat.Statut);
        Assert.Equal(1, resultat.Propose?.IngredientId);
        Assert.True(resultat.Propose?.Score < 1m);
    }

    [Fact]
    public void Approche_FauteDeFrappe()
    {
        var resultat = Rapprochement.Rapprocher("brocolli");

        Assert.Equal(StatutRapprochement.Approche, resultat.Statut);
        Assert.Equal(10, resultat.Propose?.IngredientId);
        Assert.True(resultat.Propose?.Score < 1m);
    }

    [Fact]
    public void Approche_LesAutresSuggestionsSuivent()
    {
        var resultat = Rapprochement.Rapprocher("poitrines de poulet désossées");

        Assert.Equal(StatutRapprochement.Approche, resultat.Statut);
        Assert.Equal(16, resultat.Propose?.IngredientId);
        Assert.Equal(16, resultat.Suggestions[0].IngredientId);
        Assert.All(resultat.Suggestions.Skip(1), s => Assert.True(s.Score < resultat.Suggestions[0].Score));
    }

    [Fact]
    public void Ambigu_DeuxIngredientsConviennentAutant()
    {
        // « oignons » va aussi bien au jaune, au rouge qu'au vert : aucun choix silencieux.
        var resultat = Rapprochement.Rapprocher("oignons");

        Assert.Equal(StatutRapprochement.Ambigu, resultat.Statut);
        Assert.Null(resultat.Propose);
        Assert.Equal([1, 2, 3], resultat.Suggestions.Select(s => s.IngredientId).Order());
    }

    [Fact]
    public void Ambigu_LaitSansPrecision()
    {
        var resultat = Rapprochement.Rapprocher("lait");

        Assert.Equal(StatutRapprochement.Ambigu, resultat.Statut);
        Assert.Null(resultat.Propose);
        Assert.Contains(resultat.Suggestions, s => s.IngredientId == 6);
        Assert.Contains(resultat.Suggestions, s => s.IngredientId == 7);
    }

    [Fact]
    public void Ambigu_DeuxNomsIdentiquesSansLesAccents()
    {
        // « Pâte » et « Pâté » se confondent sans les accents : on demande plutôt que de choisir.
        var rapprochement = new RapprochementIngredients(
        [
            new(1, "Pâte", []),
            new(2, "Pâté", []),
        ]);

        var resultat = rapprochement.Rapprocher("pate");

        Assert.Equal(StatutRapprochement.Ambigu, resultat.Statut);
        Assert.Null(resultat.Propose);
        Assert.Equal(2, resultat.Suggestions.Count);
        Assert.All(resultat.Suggestions, s => Assert.Equal(1m, s.Score));
    }

    [Fact]
    public void Ambigu_DeuxSynonymesDeuxIngredients()
    {
        var rapprochement = new RapprochementIngredients(
        [
            new(1, "Fromage cheddar", ["Fromage fort"]),
            new(2, "Fromage parmesan", ["Fromages forts"]),
        ]);

        Assert.Equal(StatutRapprochement.Ambigu, rapprochement.Rapprocher("fromage fort").Statut);
    }

    [Theory]
    [InlineData("safran")]
    [InlineData("sirop de maïs")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("de la")]
    public void SansCorrespondance_ACreer(string? nom)
    {
        var resultat = Rapprochement.Rapprocher(nom);

        Assert.Equal(StatutRapprochement.ACreer, resultat.Statut);
        Assert.Null(resultat.Propose);
        Assert.Empty(resultat.Suggestions);
    }

    [Fact]
    public void UnNombreDifferentNEstPasUneFauteDeFrappe()
    {
        var rapprochement = new RapprochementIngredients(
        [
            new(1, "Lait 2 %", []),
            new(2, "Lait 3,25 %", []),
        ]);

        Assert.Equal(2, rapprochement.Rapprocher("lait 3.25 %").Propose?.IngredientId);
        Assert.Equal(1, rapprochement.Rapprocher("Lait 2%").Propose?.IngredientId);
        Assert.Equal(StatutRapprochement.Exact, rapprochement.Rapprocher("lait 2 %").Statut);
    }

    [Fact]
    public void AuPlusCinqSuggestions()
    {
        var rapprochement = new RapprochementIngredients(
            Enumerable.Range(1, 8).Select(i => new IngredientConnu(i, $"Fromage sorte{i}", [])));

        var resultat = rapprochement.Rapprocher("fromage");

        Assert.Equal(StatutRapprochement.Ambigu, resultat.Statut);
        Assert.Equal(RapprochementIngredients.NombreSuggestions, resultat.Suggestions.Count);
    }

    [Theory]
    [InlineData("Pâtes de tomates", new[] { "pate", "tomate" })]
    [InlineData("Œufs", new[] { "oeuf" })]
    [InlineData("Lait 3,25 %", new[] { "lait", "3.25%" })]
    [InlineData("Huile d'olive", new[] { "huile", "olive" })]
    [InlineData("Chou-fleur", new[] { "chou", "fleur" })]
    [InlineData("Riz", new[] { "riz" })]
    public void Mots(string nom, string[] attendus) =>
        Assert.Equal(attendus, RapprochementIngredients.Mots(nom));
}
