using Repas.Domaine.Recettes;

namespace Repas.Domaine.Tests.Recettes;

public sealed class CriteresRechercheRecettesTests
{
    [Fact]
    public void Aucun_EstVide()
    {
        Assert.True(CriteresRechercheRecettes.Aucun.EstVide);
    }

    [Fact]
    public void DesTextesBlancs_NeFiltrentRien()
    {
        Assert.True(new CriteresRechercheRecettes(Nom: " ", Ingredient: "", Etiquette: "\t").EstVide);
    }

    [Theory]
    [MemberData(nameof(CriteresActifs))]
    public void UnSeulCritere_SuffitPourFiltrer(CriteresRechercheRecettes criteres)
    {
        Assert.False(criteres.EstVide);
    }

    [Theory]
    [InlineData(0, "0_min")]
    [InlineData(45, "45_min")]
    [InlineData(60, "1_h")]
    [InlineData(65, "1_h_05")]
    [InlineData(85, "1_h_25")]
    [InlineData(120, "2_h")]
    public void AffichageDuree_Ecrire(int minutes, string attendu)
    {
        Assert.Equal(attendu.Replace('_', ' '), AffichageDuree.Ecrire(minutes));
    }

    public static TheoryData<CriteresRechercheRecettes> CriteresActifs =>
    [
        new(Nom: "crêpes"),
        new(Ingredient: "poulet"),
        new(Etiquette: "Dessert"),
        new(TempsTotalMaxMin: 30),
    ];
}
