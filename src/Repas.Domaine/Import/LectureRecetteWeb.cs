using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Repas.Domaine.Recettes;

namespace Repas.Domaine.Import;

/// <summary>
/// Lit la recette d'une page web à partir de ses données structurées <c>schema.org/Recipe</c> (JSON-LD), que publient
/// la plupart des sites de recettes pour les moteurs de recherche. La recette peut être seule, dans une liste ou dans
/// un <c>@graph</c> ; les étapes peuvent être du texte, des <c>HowToStep</c> ou des <c>HowToSection</c>.
/// </summary>
public static partial class LectureRecetteWeb
{
    /// <summary>Taille maximale d'une page lue, en octets : les pages de recettes font rarement plus de 1 Mo.</summary>
    public const int TaillePageMax = 3 * 1024 * 1024;

    [GeneratedRegex(
        @"<script[^>]*type\s*=\s*[""']?application/ld\+json[""']?[^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlocJsonLd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Balise();

    [GeneratedRegex(@"<\s*br\s*/?\s*>|</\s*p\s*>|</\s*li\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex FinDeLigneHtml();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Espaces();

    // Durée ISO 8601 : « PT1H30M », « PT45M », « P0DT1H », « PT90M », « PT1H30M0S ».
    [GeneratedRegex(
        @"^P(?:(?<j>\d+)D)?(?:T(?:(?<h>\d+(?:\.\d+)?)H)?(?:(?<m>\d+(?:\.\d+)?)M)?(?:(?<s>\d+(?:\.\d+)?)S)?)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DureeIso();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Nombre();

    /// <summary>Lit la recette de la page.</summary>
    /// <param name="html">Le contenu de la page.</param>
    /// <param name="adresse">L'adresse de la page, gardée sur la recette et utilisée pour compléter celle de la photo.</param>
    /// <returns>Le brouillon, ou <c>null</c> si la page n'a pas de données de recette.</returns>
    public static BrouillonRecette? Lire(string? html, Uri adresse)
    {
        ArgumentNullException.ThrowIfNull(adresse);
        foreach (Match bloc in BlocJsonLd().Matches(html ?? string.Empty))
        {
            // Certains sites enveloppent encore le JSON dans un commentaire HTML.
            var json = bloc.Groups["json"].Value.Trim();
            if (json.StartsWith("<!--", StringComparison.Ordinal) && json.EndsWith("-->", StringComparison.Ordinal))
            {
                json = json[4..^3];
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(
                    json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException)
            {
                // Un bloc mal formé n'empêche pas de lire les autres.
                continue;
            }

            using (document)
            {
                if (TrouverRecette(document.RootElement) is { } recette)
                {
                    return Convertir(recette, adresse);
                }
            }
        }

        return null;
    }

    /// <summary>Une durée ISO 8601 (« PT1H30M ») en minutes ; <c>null</c> si absente, nulle ou illisible.</summary>
    public static int? LireDureeIso(string? texte)
    {
        var trouve = DureeIso().Match((texte ?? string.Empty).Trim());
        if (!trouve.Success || trouve.Length <= 1)
        {
            return null;
        }

        decimal Partie(string groupe) => trouve.Groups[groupe].Success
            ? decimal.Parse(trouve.Groups[groupe].Value, CultureInfo.InvariantCulture)
            : 0m;

        var minutes = (Partie("j") * 24 * 60) + (Partie("h") * 60) + Partie("m") + (Partie("s") / 60);
        var arrondi = (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
        return arrondi is > 0 and <= ValidationRecette.TempsMaxMin ? arrondi : null;
    }

    private static JsonElement? TrouverRecette(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var enfant in element.EnumerateArray())
                {
                    if (TrouverRecette(enfant) is { } trouvee)
                    {
                        return trouvee;
                    }
                }

                return null;
            case JsonValueKind.Object:
                if (EstUneRecette(element))
                {
                    return element;
                }

                // « @graph » (sites WordPress) ou une page qui contient la recette (« mainEntity »).
                foreach (var nom in (string[])["@graph", "mainEntity", "mainEntityOfPage"])
                {
                    if (element.TryGetProperty(nom, out var enfant) && TrouverRecette(enfant) is { } trouvee)
                    {
                        return trouvee;
                    }
                }

                return null;
            default:
                return null;
        }
    }

    private static bool EstUneRecette(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => EstTypeRecette(type.GetString()),
            JsonValueKind.Array => type.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && EstTypeRecette(t.GetString())),
            _ => false,
        };
    }

    private static bool EstTypeRecette(string? type) =>
        type is not null && (type.Equals("Recipe", StringComparison.OrdinalIgnoreCase)
                             || type.EndsWith("/Recipe", StringComparison.OrdinalIgnoreCase));

    private static BrouillonRecette Convertir(JsonElement recette, Uri adresse)
    {
        var ingredients = Textes(Propriete(recette, "recipeIngredient") ?? Propriete(recette, "ingredients"))
            .SelectMany(Lignes)
            .ToList();

        var etapes = new List<string>();
        if (Propriete(recette, "recipeInstructions") is { } instructions)
        {
            // Une seule section porte souvent le nom de la recette : son nom n'apporte rien devant les étapes.
            var sections = instructions.ValueKind == JsonValueKind.Array
                ? instructions.EnumerateArray().Count(e => Propriete(e, "itemListElement") is not null)
                : 0;
            AjouterEtapes(instructions, etapes, section: null, nommerSections: sections >= 2);
        }

        return new BrouillonRecette(
            Nettoyer(Texte(Propriete(recette, "name"))),
            LirePortions(Propriete(recette, "recipeYield")),
            LireDureeIso(Texte(Propriete(recette, "prepTime"))),
            LireDureeIso(Texte(Propriete(recette, "cookTime"))),
            ingredients,
            etapes,
            Texte(Propriete(recette, "description")) is { Length: > 0 } description ? Nettoyer(description) : null,
            SourceRecette.Web,
            adresse.AbsoluteUri,
            LireImage(Propriete(recette, "image"), adresse));
    }

    /// <summary>
    /// Ajoute les étapes : du texte (une étape par ligne), une liste, des <c>HowToStep</c> ou des <c>HowToSection</c>.
    /// Le nom d'une section précède sa première étape : « Sauce : Fondre le beurre. »
    /// </summary>
    private static void AjouterEtapes(JsonElement element, List<string> etapes, string? section, bool nommerSections)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                foreach (var ligne in Lignes(element.GetString()))
                {
                    Ajouter(ligne);
                }

                break;
            case JsonValueKind.Array:
                foreach (var enfant in element.EnumerateArray())
                {
                    AjouterEtapes(enfant, etapes, section, nommerSections);
                    section = null;
                }

                break;
            case JsonValueKind.Object:
                if (Propriete(element, "itemListElement") is { } elements)
                {
                    var nom = Nettoyer(Texte(Propriete(element, "name")));
                    AjouterEtapes(elements, etapes, nommerSections && nom.Length > 0 ? nom : section, nommerSections);
                }
                else if ((Texte(Propriete(element, "text")) ?? Texte(Propriete(element, "name"))) is { } texte)
                {
                    foreach (var ligne in Lignes(texte))
                    {
                        Ajouter(ligne);
                    }
                }

                break;
        }

        void Ajouter(string ligne)
        {
            etapes.Add(section is null ? ligne : $"{section.TrimEnd(':', ' ')} : {ligne}");
            section = null;
        }
    }

    /// <summary>« 4 », « 4 portions », [« 6 », « 6 portions »], 4 : le premier nombre trouvé.</summary>
    private static int? LirePortions(JsonElement? element)
    {
        foreach (var texte in Textes(element))
        {
            if (Nombre().Match(texte) is { Success: true } nombre
                && int.TryParse(nombre.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var portions)
                && portions is >= 1 and <= ValidationRecette.PortionsMax)
            {
                return portions;
            }
        }

        return null;
    }

    /// <summary>Une adresse, une liste d'adresses ou un <c>ImageObject</c> ; la première image http(s).</summary>
    private static string? LireImage(JsonElement? element, Uri adresse)
    {
        if (element is not { } image)
        {
            return null;
        }

        IEnumerable<JsonElement> candidates = image.ValueKind == JsonValueKind.Array ? image.EnumerateArray() : [image];
        foreach (var candidate in candidates)
        {
            var texte = candidate.ValueKind == JsonValueKind.Object
                ? Texte(Propriete(candidate, "url")) ?? Texte(Propriete(candidate, "contentUrl"))
                : Texte(candidate);
            if (!string.IsNullOrWhiteSpace(texte)
                && Uri.TryCreate(adresse, WebUtility.HtmlDecode(texte.Trim()), out var url)
                && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
            {
                return url.AbsoluteUri;
            }
        }

        return null;
    }

    private static JsonElement? Propriete(JsonElement element, string nom) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(nom, out var valeur)
        && valeur.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? valeur
            : null;

    private static string? Texte(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.String } e => e.GetString(),
        { ValueKind: JsonValueKind.Number } e => e.GetRawText(),
        _ => null,
    };

    private static IEnumerable<string> Textes(JsonElement? element)
    {
        if (element is { ValueKind: JsonValueKind.Array } liste)
        {
            return liste.EnumerateArray().Select(e => Texte(e)).OfType<string>().ToList();
        }

        return Texte(element) is { } seul ? [seul] : [];
    }

    /// <summary>Du texte, parfois avec du HTML : les entités décodées, les balises retirées, une entrée par ligne.</summary>
    private static IEnumerable<string> Lignes(string? texte) =>
        WebUtility.HtmlDecode(FinDeLigneHtml().Replace(texte ?? string.Empty, "\n"))
            .Split('\n')
            .Select(l => Nettoyer(l))
            .Where(l => l.Length > 0);

    private static string Nettoyer(string? texte) =>
        Espaces().Replace(Balise().Replace(WebUtility.HtmlDecode(texte ?? string.Empty), " "), " ").Trim();
}
