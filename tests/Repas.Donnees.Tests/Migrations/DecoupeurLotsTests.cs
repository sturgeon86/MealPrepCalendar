using Repas.Donnees.Migrations;

namespace Repas.Donnees.Tests.Migrations;

public sealed class DecoupeurLotsTests
{
    [Fact]
    public void SansGo_RetourneUnSeulLot()
    {
        var lots = DecoupeurLots.Decouper("SELECT 1;\nSELECT 2;");

        Assert.Equal(["SELECT 1;\nSELECT 2;"], lots);
    }

    [Theory]
    [InlineData("SELECT 1;\nGO\nSELECT 2;")]
    [InlineData("SELECT 1;\r\nGO\r\nSELECT 2;")]
    [InlineData("SELECT 1;\n  go  \nSELECT 2;")]
    [InlineData("SELECT 1;\nGO;\nSELECT 2;\nGO\n")]
    public void DecoupeSurLesLignesGo(string script)
    {
        var lots = DecoupeurLots.Decouper(script);

        Assert.Equal(["SELECT 1;", "SELECT 2;"], lots);
    }

    [Fact]
    public void GoAuMilieuDUneLigne_NEstPasUnSeparateur()
    {
        var lots = DecoupeurLots.Decouper("SELECT 'GO' AS Mot;\nSELECT Categorie FROM dbo.Essai;");

        Assert.Single(lots);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n")]
    [InlineData("GO\nGO\n")]
    public void ScriptSansInstruction_NeRetourneAucunLot(string script)
    {
        Assert.Empty(DecoupeurLots.Decouper(script));
    }
}
