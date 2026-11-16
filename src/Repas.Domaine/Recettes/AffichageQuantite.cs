using System.Globalization;

namespace Repas.Domaine.Recettes;

/// <summary>
/// Quantités d'une recette ramenées au nombre de portions voulu, et écrites pour la lecture en cuisine :
/// fractions (« 1 ½ tasse ») pour les mesures de cuisine et les pièces, décimales arrondies pour le métrique.
/// Le calcul reste exact (<see cref="decimal"/>) ; seul l'affichage arrondit.
/// </summary>
public static class AffichageQuantite
{
    /// <summary>Espace insécable : la quantité et son unité ne sont jamais séparées par un retour à la ligne.</summary>
    private const char Insecable = ' ';

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-CA");

    /// <summary>Unités écrites en décimales ; toutes les autres le sont en fractions.</summary>
    private static readonly HashSet<string> UnitesMetriques = new(StringComparer.OrdinalIgnoreCase) { "g", "kg", "ml", "l" };

    /// <summary>Unité sous-entendue : « 3 Œufs » plutôt que « 3 unité Œufs ».</summary>
    private const string UniteSousEntendue = "unité";

    /// <summary>Unités écrites en toutes lettres, qui prennent la marque du pluriel (à partir de 2, comme en français).</summary>
    private static readonly Dictionary<string, string> Pluriels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tasse"] = "tasses",
        ["douzaine"] = "douzaines",
    };

    private static readonly (decimal Valeur, string Symbole)[] Fractions =
    [
        (0m, ""),
        (1m / 8, "⅛"), (1m / 4, "¼"), (1m / 3, "⅓"), (3m / 8, "⅜"), (1m / 2, "½"),
        (5m / 8, "⅝"), (2m / 3, "⅔"), (3m / 4, "¾"), (7m / 8, "⅞"),
        (1m, ""),
    ];

    /// <summary>Au-delà, on arrondit à la demie : « 12 ⅜ œufs » n'aide personne.</summary>
    private const decimal SeuilDemies = 10m;

    /// <summary>
    /// Quantité de la recette × (portions voulues ÷ portions de la recette). <c>null</c> reste <c>null</c> (« au goût »).
    /// </summary>
    public static decimal? MettreALEchelle(decimal? quantite, int portionsRecette, int portionsVoulues)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(portionsRecette);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(portionsVoulues);

        return quantite is { } valeur ? valeur * portionsVoulues / portionsRecette : null;
    }

    /// <summary>La quantité et son unité, prêtes à afficher : « 1 ½ tasse », « 250 ml », « 3 » (unité sous-entendue).</summary>
    public static string Formater(decimal quantite, string codeUnite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeUnite);

        var nombre = UnitesMetriques.Contains(codeUnite) ? EcrireDecimales(quantite) : EcrireFraction(quantite);
        if (string.Equals(codeUnite, UniteSousEntendue, StringComparison.OrdinalIgnoreCase))
        {
            return nombre;
        }

        var unite = quantite >= 2 && Pluriels.TryGetValue(codeUnite, out var pluriel) ? pluriel : codeUnite;
        return $"{nombre}{Insecable}{unite}";
    }

    /// <summary>Décimales arrondies selon la grandeur : 1234 g, 12,5 g, 2,25 kg.</summary>
    public static string EcrireDecimales(decimal quantite)
    {
        var decimales = quantite >= 100 ? 0 : quantite >= 10 ? 1 : 2;
        var arrondi = Math.Round(quantite, decimales, MidpointRounding.AwayFromZero);
        if (arrondi == 0 && quantite > 0)
        {
            // Une quantité minuscule ne s'affiche jamais « 0 ».
            arrondi = 0.01m;
        }

        return arrondi.ToString("0.##", Culture);
    }

    /// <summary>Entier et fraction usuelle la plus proche (huitièmes et tiers) : « 1 ½ », « ⅔ », « 3 ».</summary>
    public static string EcrireFraction(decimal quantite)
    {
        var entier = Math.Floor(quantite);
        var reste = quantite - entier;

        var candidats = quantite >= SeuilDemies
            ? Fractions.Where(f => f.Valeur is 0m or 0.5m or 1m)
            : Fractions;
        var (valeur, symbole) = candidats.MinBy(f => Math.Abs(f.Valeur - reste));
        if (valeur == 1m)
        {
            entier++;
        }

        if (entier == 0 && symbole.Length == 0)
        {
            // Moins d'un seizième : une fraction serait « 0 », on garde les décimales.
            return EcrireDecimales(quantite);
        }

        var partieEntiere = entier.ToString("0", Culture);
        return (entier, symbole.Length) switch
        {
            (0, _) => symbole,
            (_, 0) => partieEntiere,
            _ => $"{partieEntiere}{Insecable}{symbole}",
        };
    }
}
