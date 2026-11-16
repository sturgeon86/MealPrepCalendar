using Repas.Domaine.Epicerie;
using Repas.Domaine.Mesures;

namespace Repas.Domaine.Tests.Epicerie;

public sealed class AffichageListeTests
{
    // Mêmes unités et facteurs que les données de base.
    private static readonly Unite G = new(1, "g", TypeMesure.Poids, 1);
    private static readonly Unite Kg = new(2, "kg", TypeMesure.Poids, 1000);
    private static readonly Unite Lb = new(3, "lb", TypeMesure.Poids, 453.59237m);
    private static readonly Unite Ml = new(5, "ml", TypeMesure.Volume, 1);
    private static readonly Unite L = new(6, "l", TypeMesure.Volume, 1000);
    private static readonly Unite Tasse = new(7, "tasse", TypeMesure.Volume, 250);
    private static readonly Unite Piece = new(10, "unité", TypeMesure.Unite, 1);
    private static readonly Unite Douzaine = new(11, "douzaine", TypeMesure.Unite, 12);

    private static readonly Unite[] Unites = [G, Kg, Lb, Ml, L, Tasse, Piece, Douzaine];
    private static readonly Dictionary<int, Unite> ParId = Unites.ToDictionary(u => u.Id);

    private static ArticleListe Article(int id, string categorie, int ordre, decimal? quantite = null, Unite? unite = null) =>
        new(id, 100 + id, $"Article {id}", categorie, ordre, quantite, unite?.Id, unite?.Code, 1, false, false, false, new MesuresIngredient(G.Id));

    private static string Texte(string attendu) => attendu.Replace('_', ' ');

    [Theory]
    [InlineData(1500, "ml", "1,5_l")]     // ml → l à partir de 1000
    [InlineData(1000, "ml", "1_l")]
    [InlineData(999, "ml", "999_ml")]
    [InlineData(1250, "g", "1,25_kg")]
    [InlineData(680.39, "g", "680_g")]
    [InlineData(24, "unité", "2_douzaines")]
    [InlineData(12, "unité", "1_douzaine")]
    [InlineData(18, "unité", "18")]       // pas un nombre exact de douzaines
    [InlineData(6, "unité", "6")]
    [InlineData(3, "tasse", "3_tasses")]  // une unité choisie (alerte, ajout manuel) reste telle quelle
    [InlineData(2, "lb", "2_lb")]
    [InlineData(2500, "l", "2500_l")]     // déjà une grande unité
    public void Quantite_DansUneUniteLisible(decimal quantite, string code, string attendu)
    {
        var unite = Unites.Single(u => u.Code == code);

        Assert.Equal(Texte(attendu), AffichageListe.Quantite(Article(1, "Divers", 10, quantite, unite), ParId));
    }

    [Fact]
    public void Quantite_1500Ml_Donne1VirguleCinqLitres()
    {
        // L'exemple de la story.
        Assert.Equal(Texte("1,5_l"), AffichageListe.Quantite(Article(1, "Produits laitiers", 50, 1500m, Ml), ParId));
    }

    [Fact]
    public void Quantite_SansQuantite_UnTiret()
    {
        Assert.Equal("—", AffichageListe.Quantite(Article(1, "Hygiène", 120), ParId));
    }

    [Fact]
    public void Quantite_UniteInconnue_GardeLeCodeEnregistre()
    {
        var article = Article(1, "Divers", 10, 2m, Tasse) with { UniteId = 999 };

        Assert.Equal(Texte("2_tasses"), AffichageListe.Quantite(article, ParId));
    }

    [Fact]
    public void RendreLisible_SansGrandeUniteDansLeReferentiel_GardeLUniteDeBase()
    {
        Assert.Equal((1500m, Ml), AffichageListe.RendreLisible(1500m, Ml, [Ml, Tasse]));
    }

    [Fact]
    public void GrouperParCategorie_SelonLOrdreGenerique()
    {
        var groupes = AffichageListe.GrouperParCategorie(
        [
            Article(1, "Fruits et légumes", 10),
            Article(2, "Fruits et légumes", 10),
            Article(3, "Viandes", 30),
            Article(4, "Hygiène", 120),
            Article(5, "Produits laitiers", 50),
        ]);

        Assert.Equal(["Fruits et légumes", "Viandes", "Produits laitiers", "Hygiène"], groupes.Select(g => g.Categorie));
        Assert.Equal([1, 2], groupes[0].Articles.Select(a => a.Id));
    }

    [Fact]
    public void GrouperParCategorie_MemeOrdre_ParNom()
    {
        var groupes = AffichageListe.GrouperParCategorie([Article(1, "Surgelés", 80), Article(2, "Épicerie sèche", 80)]);

        Assert.Equal(["Épicerie sèche", "Surgelés"], groupes.Select(g => g.Categorie));
    }

    [Fact]
    public void GrouperParCategorie_ListeVide()
    {
        Assert.Empty(AffichageListe.GrouperParCategorie([]));
    }
}

public sealed class OrdreMagasinTests
{
    private static ArticleListe Article(int id, bool coche) =>
        new(id, 100 + id, $"Article {id}", "Divers", 10, null, null, null, 1, false, false, coche, new MesuresIngredient(1));

    [Fact]
    public void LesArticlesCochesPassentEnBas_DansLeurOrdre()
    {
        var ordre = AffichageListe.OrdonnerPourMagasin(
            [Article(1, true), Article(2, false), Article(3, true), Article(4, false)]);

        Assert.Equal([2, 4, 1, 3], ordre.Select(a => a.Id));
    }

    [Fact]
    public void SansArticleCoche_LOrdreNeChangePas()
    {
        Assert.Equal([3, 1, 2], AffichageListe.OrdonnerPourMagasin([Article(3, false), Article(1, false), Article(2, false)]).Select(a => a.Id));
    }
}
