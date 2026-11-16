using System.Globalization;
using System.Text.RegularExpressions;
using Repas.Domaine.Recettes;

namespace Repas.Domaine.Import;

/// <summary>
/// Lit une recette collée en texte (d'un courriel, d'un document ou d'une page) : le titre sur la première ligne,
/// puis les ingrédients (une ligne chacun) et les étapes. Les intertitres « Ingrédients » et « Préparation » guident
/// la lecture ; sans eux, une ligne qui commence par une quantité est un ingrédient. Les lignes « Portions : 4 »,
/// « Préparation : 15 min » et « Cuisson : 1 h 30 » remplissent les champs correspondants.
/// </summary>
public static partial class LectureTexteRecette
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private enum Section
    {
        Debut,
        Ingredients,
        Etapes,
    }

    [GeneratedRegex(@"^(?:liste\s+des\s+)?ingr[eé]dients?\s*:?$", Options)]
    private static partial Regex IntertitreIngredients();

    [GeneratedRegex(
        @"^(?:pr[eé]paration|[eé]tapes?|instructions?|m[eé]thode|mode\s+de\s+pr[eé]paration|directions?|method|r[eé]alisation)\s*:?$",
        Options)]
    private static partial Regex IntertitreEtapes();

    // « Portions : 4 », « Donne 6 portions », « Pour 4 personnes », « 8 portions », « Servings: 4 ».
    [GeneratedRegex(
        @"^(?:(?:portions?|rendement|servings?|serves|yield|donne|pour)\s*:?\s*(?<n>\d+)(?:\s*(?:portions?|personnes?|servings?))?|(?<n>\d+)\s*(?:portions?|personnes?|servings?))\.?$",
        Options)]
    private static partial Regex Portions();

    [GeneratedRegex(@"^(?:temps\s+de\s+)?(?:pr[eé]paration|prep(?:\s+time)?)\s*:\s*(?<duree>.+)$", Options)]
    private static partial Regex TempsPreparation();

    [GeneratedRegex(@"^(?:temps\s+de\s+)?(?:cuisson|cook(?:\s+time)?)\s*:\s*(?<duree>.+)$", Options)]
    private static partial Regex TempsCuisson();

    // Une ligne qu'on ne garde pas : « Temps total : 1 h », « Étape 2 », un intertitre de sous-section « Pour la sauce : ».
    [GeneratedRegex(@"^(?:temps\s+total|total(?:\s+time)?)\s*:", Options)]
    private static partial Regex TempsTotal();

    [GeneratedRegex(@"^(?:[eé]tape|step)\s*\d+\s*:?$", Options)]
    private static partial Regex EtapeSeule();

    // La numérotation d'une étape : « 1. », « 2) », « Étape 3 : ».
    [GeneratedRegex(@"^(?:(?:[eé]tape|step)\s*\d+\s*[:.)-]?|\d+\s*[.)]|\d+\s+-)\s*", Options)]
    private static partial Regex NumeroEtape();

    [GeneratedRegex(@"^[-–—•·*▪◦]?\s*(?:\d|[¼½¾⅓⅔⅛⅜⅝⅞])")]
    private static partial Regex CommenceParUneQuantite();

    // « 1 h 30 », « 1h30 », « 1 heure 15 min », « 45 min », « 90 minutes », « 2 h ».
    [GeneratedRegex(
        @"^(?:(?<h>\d+)\s*(?:h|hr?s?|heures?|hours?)\.?)?\s*(?:(?<m>\d+)\s*(?:min(?:ute)?s?\.?)?)?$", Options)]
    private static partial Regex Duree();

    /// <summary>Lit le texte collé ; les lignes vides et les espaces superflues sont ignorées.</summary>
    public static BrouillonRecette Lire(string? texte)
    {
        var lignes = (texte ?? string.Empty)
            .Replace(' ', ' ')
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        string? nom = null;
        int? portions = null;
        int? preparation = null;
        int? cuisson = null;
        var ingredients = new List<string>();
        var etapes = new List<string>();
        var notes = new List<string>();
        var aDesIntertitres = lignes.Any(l => IntertitreIngredients().IsMatch(l) || IntertitreEtapes().IsMatch(l));
        var section = Section.Debut;

        foreach (var ligne in lignes)
        {
            if (IntertitreIngredients().IsMatch(ligne))
            {
                section = Section.Ingredients;
                continue;
            }

            if (IntertitreEtapes().IsMatch(ligne))
            {
                section = Section.Etapes;
                continue;
            }

            // Les renseignements de la recette peuvent être n'importe où avant les étapes.
            if (section != Section.Etapes || nom is null)
            {
                if (Portions().Match(ligne) is { Success: true } p && portions is null)
                {
                    portions = int.Parse(p.Groups["n"].Value, CultureInfo.InvariantCulture);
                    continue;
                }

                if (TempsPreparation().Match(ligne) is { Success: true } tp && LireDuree(tp.Groups["duree"].Value) is { } minutesPrep)
                {
                    preparation = minutesPrep;
                    continue;
                }

                if (TempsCuisson().Match(ligne) is { Success: true } tc && LireDuree(tc.Groups["duree"].Value) is { } minutesCuisson)
                {
                    cuisson = minutesCuisson;
                    continue;
                }

                if (TempsTotal().IsMatch(ligne))
                {
                    continue;
                }
            }

            if (nom is null && section == Section.Debut)
            {
                nom = ligne;
                continue;
            }

            switch (section)
            {
                case Section.Ingredients:
                    // « Pour la sauce : » : un sous-titre, pas un ingrédient.
                    if (!ligne.EndsWith(':'))
                    {
                        ingredients.Add(ligne);
                    }

                    break;
                case Section.Etapes:
                    AjouterEtape(ligne, etapes);
                    break;
                default:
                    if (aDesIntertitres)
                    {
                        notes.Add(ligne);
                    }
                    else if (etapes.Count == 0 && CommenceParUneQuantite().IsMatch(ligne))
                    {
                        // Sans intertitres : les lignes qui commencent par une quantité, avant la première étape.
                        ingredients.Add(ligne);
                    }
                    else if (ingredients.Count == 0)
                    {
                        notes.Add(ligne);
                    }
                    else
                    {
                        AjouterEtape(ligne, etapes);
                    }

                    break;
            }
        }

        return new BrouillonRecette(
            nom ?? string.Empty,
            portions,
            preparation,
            cuisson,
            ingredients,
            etapes,
            notes.Count > 0 ? string.Join(Environment.NewLine, notes) : null,
            SourceRecette.Texte);
    }

    /// <summary>Lit une durée (« 1 h 30 », « 45 min », « 1h30 », « 2 heures ») en minutes ; <c>null</c> si illisible.</summary>
    public static int? LireDuree(string? texte)
    {
        var nettoye = (texte ?? string.Empty).Trim().TrimEnd('.');
        var trouve = Duree().Match(nettoye);
        if (nettoye.Length == 0 || !trouve.Success || (!trouve.Groups["h"].Success && !trouve.Groups["m"].Success))
        {
            return null;
        }

        var heures = trouve.Groups["h"].Success ? int.Parse(trouve.Groups["h"].Value, CultureInfo.InvariantCulture) : 0;
        var minutes = trouve.Groups["m"].Success ? int.Parse(trouve.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        var total = (heures * 60) + minutes;
        return total <= ValidationRecette.TempsMaxMin ? total : null;
    }

    private static void AjouterEtape(string ligne, List<string> etapes)
    {
        if (EtapeSeule().IsMatch(ligne))
        {
            return;
        }

        var sansNumero = NumeroEtape().Replace(ligne, string.Empty, 1).Trim();
        if (sansNumero.Length > 0)
        {
            etapes.Add(sansNumero);
        }
    }
}
