using Repas.Domaine.Mesures;
using Repas.Domaine.Recettes;

namespace Repas.Domaine.Epicerie;

/// <summary>Les articles d'une catégorie, dans l'ordre du trajet en magasin.</summary>
public sealed record GroupeCategorie(string Categorie, int Ordre, IReadOnlyList<ArticleListe> Articles);

/// <summary>
/// Présentation de la liste d'épicerie : regroupement par catégorie (ordre générique) et unités lisibles.
/// Les quantités enregistrées ne changent pas ; seul l'affichage est adapté.
/// </summary>
public static class AffichageListe
{
    /// <summary>Facteur des grandes unités métriques (kg, l) par rapport à leur unité de base (g, ml).</summary>
    private const decimal FacteurMille = 1000m;

    /// <summary>Facteur de la douzaine par rapport à l'unité.</summary>
    private const decimal FacteurDouzaine = 12m;

    /// <summary>
    /// Les catégories dans l'ordre générique (puis par nom), chacune avec ses articles dans l'ordre reçu.
    /// Une catégorie sans article n'apparaît pas.
    /// </summary>
    public static IReadOnlyList<GroupeCategorie> GrouperParCategorie(IEnumerable<ArticleListe> articles)
    {
        ArgumentNullException.ThrowIfNull(articles);

        return articles
            .GroupBy(a => (a.OrdreCategorie, a.Categorie))
            .OrderBy(g => g.Key.OrdreCategorie)
            .ThenBy(g => g.Key.Categorie, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fr-CA"), ignoreCase: true))
            .Select(g => new GroupeCategorie(g.Key.Categorie, g.Key.OrdreCategorie, g.ToList()))
            .ToList();
    }

    /// <summary>
    /// L'ordre en magasin dans une catégorie : ce qui reste à prendre d'abord, puis ce qui est déjà dans le panier,
    /// chacun dans l'ordre reçu. Le pouce trouve toujours le prochain article en haut.
    /// </summary>
    public static IReadOnlyList<ArticleListe> OrdonnerPourMagasin(IEnumerable<ArticleListe> articles)
    {
        ArgumentNullException.ThrowIfNull(articles);

        // OrderBy est stable : l'ordre d'origine est gardé dans chaque moitié.
        return articles.OrderBy(a => a.EstCoche).ToList();
    }

    /// <summary>
    /// La quantité dans une unité plus lisible, quand il y en a une : 1500 ml → 1,5 l, 1250 g → 1,25 kg,
    /// 24 unités → 2 douzaines. Seules les unités de base (g, ml, unité) changent ; les autres (tasse, lb…)
    /// restent comme elles ont été choisies.
    /// </summary>
    /// <param name="unites">Le référentiel des unités.</param>
    public static (decimal Quantite, Unite Unite) RendreLisible(decimal quantite, Unite unite, IEnumerable<Unite> unites)
    {
        ArgumentNullException.ThrowIfNull(unite);
        ArgumentNullException.ThrowIfNull(unites);

        if (unite.FacteurVersReference != 1m)
        {
            return (quantite, unite);
        }

        var memeType = unites.Where(u => u.TypeMesure == unite.TypeMesure).ToList();
        if (unite.TypeMesure == TypeMesure.Unite)
        {
            // Les œufs s'achètent à la douzaine : seulement pour un nombre exact de douzaines.
            var douzaine = memeType.FirstOrDefault(u => u.FacteurVersReference == FacteurDouzaine);
            return douzaine is not null && quantite >= FacteurDouzaine && quantite % FacteurDouzaine == 0
                ? (quantite / FacteurDouzaine, douzaine)
                : (quantite, unite);
        }

        var grande = memeType.FirstOrDefault(u => u.FacteurVersReference == FacteurMille);
        return grande is not null && quantite >= FacteurMille
            ? (quantite / FacteurMille, grande)
            : (quantite, unite);
    }

    /// <summary>La quantité d'un article prête à afficher (« 1,5 l », « 2 douzaines », « 3 ») ; « — » sans quantité.</summary>
    public static string Quantite(ArticleListe article, IReadOnlyDictionary<int, Unite> unites)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(unites);

        if (article.Quantite is not { } quantite || article.UniteId is not { } uniteId)
        {
            return "—";
        }

        if (!unites.TryGetValue(uniteId, out var unite))
        {
            return AffichageQuantite.Formater(quantite, article.CodeUnite ?? "?");
        }

        var (lisible, uniteLisible) = RendreLisible(quantite, unite, unites.Values);
        return AffichageQuantite.Formater(lisible, uniteLisible.Code);
    }
}
