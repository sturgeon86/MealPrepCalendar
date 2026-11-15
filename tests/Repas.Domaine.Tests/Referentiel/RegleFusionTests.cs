using Repas.Domaine.Referentiel;

namespace Repas.Domaine.Tests.Referentiel;

public sealed class RegleFusionTests
{
    private static readonly EtatFusion Doublon = new(1, "lait 2%", Actif: true, FusionneDansId: null);
    private static readonly EtatFusion Conserve = new(2, "Lait 2 %", Actif: true, FusionneDansId: null);

    [Fact]
    public void DeuxIngredientsActifs_PeuventEtreFusionnes()
    {
        Assert.Null(RegleFusion.Verifier(Doublon, Conserve));
    }

    [Fact]
    public void UnDoublonDejaDesactive_PeutEtreFusionne()
    {
        Assert.Null(RegleFusion.Verifier(Doublon with { Actif = false }, Conserve));
    }

    [Fact]
    public void IngredientInexistant_EstRefuse()
    {
        Assert.NotNull(RegleFusion.Verifier(null, Conserve));
        Assert.NotNull(RegleFusion.Verifier(Doublon, null));
    }

    [Fact]
    public void FusionAvecSoiMeme_EstRefusee()
    {
        Assert.Equal("Un ingrédient ne peut pas être fusionné avec lui-même.", RegleFusion.Verifier(Doublon, Doublon));
    }

    [Fact]
    public void DoublonDejaFusionne_EstRefuse()
    {
        var probleme = RegleFusion.Verifier(Doublon with { Actif = false, FusionneDansId = 3 }, Conserve);

        Assert.Equal("« lait 2% » a déjà été fusionné dans un autre ingrédient.", probleme);
    }

    [Fact]
    public void IngredientAGarderInactif_EstRefuse()
    {
        var probleme = RegleFusion.Verifier(Doublon, Conserve with { Actif = false });

        Assert.Equal("L'ingrédient à garder, « Lait 2 % », doit être actif.", probleme);
    }

    [Fact]
    public void IngredientAGarderLuiMemeFusionne_EstRefuse()
    {
        Assert.NotNull(RegleFusion.Verifier(Doublon, Conserve with { Actif = false, FusionneDansId = 3 }));
    }
}
