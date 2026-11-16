using System.Globalization;
using System.Text.RegularExpressions;

namespace Repas.Domaine.Recettes;

/// <summary>
/// Lit une quantité écrite comme dans une recette : « 2 », « 2,5 », « 2.5 », « 1/2 », « 1 1/2 », « ½ », « 1½ ».
/// </summary>
public static partial class AnalyseQuantite
{
    private static readonly Dictionary<char, (int Numerateur, int Denominateur)> FractionsUnicode = new()
    {
        ['¼'] = (1, 4), ['½'] = (1, 2), ['¾'] = (3, 4),
        ['⅓'] = (1, 3), ['⅔'] = (2, 3),
        ['⅛'] = (1, 8), ['⅜'] = (3, 8), ['⅝'] = (5, 8), ['⅞'] = (7, 8),
    };

    // Fraction, précédée d'un entier et d'une espace au besoin : « 1/2 », « 1 1/2 ». (« 11/2 » vaut 5,5.)
    [GeneratedRegex(@"^(?:(?<entier>\d+)\s+)?(?<num>\d+)\s*/\s*(?<den>\d+)$")]
    private static partial Regex FormatFraction();

    // Nombre décimal avec virgule ou point : « 2,5 », « .5 ».
    [GeneratedRegex(@"^(?<entier>\d*)(?:[.,](?<decimales>\d+))?$")]
    private static partial Regex FormatDecimal();

    /// <summary>Essaie de lire la quantité.</summary>
    /// <param name="texte">Le texte saisi.</param>
    /// <param name="quantite">La quantité lue ; <c>null</c> si le texte est vide (« au goût »).</param>
    /// <returns><c>false</c> si le texte n'est pas une quantité valide.</returns>
    public static bool EssayerLire(string? texte, out decimal? quantite)
    {
        quantite = null;
        var nettoye = (texte ?? string.Empty).Trim();
        if (nettoye.Length == 0)
        {
            return true;
        }

        // « 1½ » ou « 1 ½ » devient « 1 1/2 ».
        foreach (var (symbole, (numerateur, denominateur)) in FractionsUnicode)
        {
            var position = nettoye.IndexOf(symbole);
            if (position >= 0)
            {
                var avant = nettoye[..position].TrimEnd();
                nettoye = $"{avant}{(avant.Length > 0 ? " " : string.Empty)}{numerateur}/{denominateur}{nettoye[(position + 1)..]}";
                break;
            }
        }

        var fraction = FormatFraction().Match(nettoye);
        if (fraction.Success)
        {
            var denominateur = decimal.Parse(fraction.Groups["den"].Value, CultureInfo.InvariantCulture);
            if (denominateur == 0)
            {
                return false;
            }

            var entier = fraction.Groups["entier"].Success
                ? decimal.Parse(fraction.Groups["entier"].Value, CultureInfo.InvariantCulture)
                : 0m;
            quantite = entier + decimal.Parse(fraction.Groups["num"].Value, CultureInfo.InvariantCulture) / denominateur;
            return true;
        }

        var nombre = FormatDecimal().Match(nettoye);
        if (nombre.Success && (nombre.Groups["entier"].Value.Length > 0 || nombre.Groups["decimales"].Success))
        {
            var invariant = $"{(nombre.Groups["entier"].Value.Length > 0 ? nombre.Groups["entier"].Value : "0")}" +
                            (nombre.Groups["decimales"].Success ? "." + nombre.Groups["decimales"].Value : string.Empty);
            quantite = decimal.Parse(invariant, CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    /// <summary>Écrit une quantité pour la modifier : sans zéros inutiles, avec la virgule décimale.</summary>
    public static string Ecrire(decimal? quantite) =>
        quantite is { } valeur
            ? (valeur / 1.000000000000000000000000000000000m).ToString(CultureInfo.GetCultureInfo("fr-CA"))
            : string.Empty;
}
