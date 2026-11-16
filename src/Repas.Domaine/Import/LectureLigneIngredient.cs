using System.Text.RegularExpressions;
using Repas.Domaine.Recettes;

namespace Repas.Domaine.Import;

/// <summary>Une ligne d'ingrédient d'une recette importée, découpée mais pas encore rapprochée du référentiel.</summary>
/// <param name="Texte">La ligne telle qu'elle a été lue, pour la montrer à côté du résultat.</param>
/// <param name="Quantite">Absente pour « au goût » ou « une pincée ».</param>
/// <param name="CodeUnite">Code d'une unité du référentiel (« tasse », « c. à soupe », « unité »…) ; absent sans quantité.</param>
/// <param name="Nom">Le nom de l'ingrédient tel qu'écrit (« farine tout usage »), à rapprocher du référentiel.</param>
/// <param name="Precision">Ce qui accompagne le nom (« tamisée », « 1 boîte (540 ml) », « au goût »).</param>
public sealed record LigneLue(
    string Texte,
    decimal? Quantite,
    string? CodeUnite,
    string Nom,
    string? Precision,
    bool EstOptionnel);

/// <summary>
/// Lit une ligne d'ingrédient écrite comme dans un livre ou sur un site : « 1 ½ tasse de farine tout usage, tamisée »
/// donne 1,5 tasse, « farine tout usage » et la précision « tamisée ». Les unités reconnues sont celles du référentiel,
/// en français ou en anglais, au pluriel ou abrégées.
/// </summary>
public static partial class LectureLigneIngredient
{
    /// <summary>Code de l'unité « à la pièce » : « 2 oignons ».</summary>
    public const string CodeUnitePiece = "unité";

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Après une unité ou un mot : ni lettre ni chiffre (« 2 g » oui, « 2 gros » non).
    private const string FinDeMot = @"(?![\p{L}\d])";

    // Une quantité : « 1 ½ », « 1½ », « ½ », « 1 1/2 », « 1/2 », « 2,5 », « 2.5 », « ,5 ».
    private const string MotifQuantite =
        @"\d+\s*[¼½¾⅓⅔⅛⅜⅝⅞]|[¼½¾⅓⅔⅛⅜⅝⅞]|\d+\s+\d+\s*/\s*\d+|\d+\s*/\s*\d+|\d+(?:[.,]\d+)?|[.,]\d+";

    // Au début de la ligne, avec un intervalle au besoin : « 2 à 3 », « 2-3 ».
    [GeneratedRegex(@"^(?<q>" + MotifQuantite + @")(?:\s*(?:à|a|-|–|to|ou|or)\s*(?<q2>" + MotifQuantite + @"))?", Options)]
    private static partial Regex Quantite();

    [GeneratedRegex(@"^(?:une?|one)\s+", Options)]
    private static partial Regex QuantiteEnLettres();

    // Ce qui reste de la ligne après l'unité : « de farine », « d'huile », « of flour ».
    [GeneratedRegex(@"^(?:de\s+la\s+|de\s+l'\s*|du\s+|de\s+|d'\s*|of\s+)", Options)]
    private static partial Regex Liaison();

    [GeneratedRegex(@"^(?<mot>(?:grosse|gros|petite|petit|moyenne|moyen|large|small|medium)s?)" + FinDeMot + @"\s*", Options)]
    private static partial Regex TailleAuDebut();

    [GeneratedRegex(@"\s+(?<mot>(?:grosse|gros|petite|petit|moyenne|moyen)s?)$", Options)]
    private static partial Regex TailleALaFin();

    // Un contenant ou une partie : la quantité compte des pièces, ou la mesure entre parenthèses s'il y en a une.
    [GeneratedRegex(
        @"^(?<mot>(?:boîte|boite|conserve|paquet|sachet|pot|contenant|bouteille|gousse|tranche|botte|branche|tige|can|clove|slice|package)s?)" +
        FinDeMot + @"\s*", Options)]
    private static partial Regex Contenant();

    // Une toute petite quantité qu'on ne compte pas : « une pincée de sel », « un filet d'huile ».
    [GeneratedRegex(@"^(?<mot>(?:pincée|pincee|pinch|trait|filet|soupçon|soupcon|dash)(?:e?s)?)" + FinDeMot + @"\s*", Options)]
    private static partial Regex PetiteQuantite();

    [GeneratedRegex(@"(?:\bou\s+)?\b(?:au\s+goût|au\s+gout|au\s+besoin|to\s+taste|as\s+needed)\b", Options)]
    private static partial Regex AuGout();

    [GeneratedRegex(@"\b(?:facultati(?:f|ve)s?|optionnel(?:le)?s?|optional)\b", Options)]
    private static partial Regex Optionnel();

    [GeneratedRegex(@"\(([^()]*)\)")]
    private static partial Regex Parentheses();

    // Une virgule qui n'est pas celle d'un nombre (« 2,5 »).
    [GeneratedRegex(@"(?<!\d),|,(?!\d)")]
    private static partial Regex Virgule();

    [GeneratedRegex(@"^[-–—•·*▪◦]\s*")]
    private static partial Regex Puce();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espaces();

    [GeneratedRegex(@"^(?:chacune?s?|each)$", Options)]
    private static partial Regex Chacun();

    /// <summary>Les unités du référentiel et leurs façons de les écrire, de la plus longue à la plus courte.</summary>
    private static readonly (Regex Motif, string Code)[] Unites =
    [
        (Unite(@"c(?:uill[eè]res?)?\.?\s*(?:à|a)\.?\s*(?:soupe|table|s\.?)|c\.\s?s\.|cs|tbsp\.?|tbs\.?|tablespoons?"), "c. à soupe"),
        (Unite(@"c(?:uill[eè]res?)?\.?\s*(?:à|a)\.?\s*(?:thé|the|café|cafe|t\.?)|c\.\s?t\.|tsp\.?|teaspoons?"), "c. à thé"),
        (Unite(@"tasses?|cups?"), "tasse"),
        (Unite(@"ml|millilitres?|milliliters?"), "ml"),
        (Unite(@"l|litres?|liters?"), "l"),
        (Unite(@"kg|kilos?|kilogrammes?|kilograms?"), "kg"),
        (Unite(@"g|gr\.?|grammes?|grams?"), "g"),
        (Unite(@"lbs?\.?|livres?|pounds?"), "lb"),
        (Unite(@"oz\.?|onces?|ounces?"), "oz"),
        (Unite(@"douzaines?|dozen"), "douzaine"),
        (Unite(@"unités?|unites?"), CodeUnitePiece),
    ];

    private static Regex Unite(string motif) => new($"^(?:{motif}){FinDeMot}\\s*", Options | RegexOptions.Compiled);

    /// <summary>Lit une ligne ; <c>null</c> si elle est vide.</summary>
    public static LigneLue? Lire(string? ligne)
    {
        var texte = Nettoyer(ligne);
        if (texte.Length == 0)
        {
            return null;
        }

        var reste = Puce().Replace(texte, string.Empty);
        var precisions = new List<string>();

        // « (facultatif) », « au goût » : où qu'ils soient, ils ne font pas partie du nom.
        var estOptionnel = Optionnel().IsMatch(reste);
        reste = Optionnel().Replace(reste, string.Empty);
        var auGout = AuGout().Match(reste);
        reste = AuGout().Replace(reste, string.Empty);

        // Les parenthèses sont mises de côté : une mesure (« 540 ml ») peut servir pour un contenant.
        var parentheses = Parentheses().Matches(reste)
            .Select(m => Nettoyer(m.Groups[1].Value))
            .Where(p => p.Length > 0)
            .ToList();
        reste = Nettoyer(Parentheses().Replace(reste, " "));

        // Après la première virgule : la préparation (« tamisée », « haché finement »).
        string? apresVirgule = null;
        var virgule = Virgule().Match(reste);
        if (virgule.Success)
        {
            apresVirgule = NettoyerMorceau(reste[(virgule.Index + 1)..]);
            reste = reste[..virgule.Index].Trim();
        }

        // La quantité.
        decimal? quantite = null;
        string? codeUnite = null;
        var lue = LireQuantite(ref reste, out var intervalle);
        if (lue is not null)
        {
            quantite = lue;
            if (intervalle is not null)
            {
                precisions.Add(intervalle);
            }
        }

        AjouterTaille(ref reste, precisions);

        // L'unité, un contenant ou une petite quantité.
        if (PetiteQuantite().Match(reste) is { Success: true } petite)
        {
            var mot = petite.Groups["mot"].Value;
            precisions.Add(quantite is null ? mot : $"{AnalyseQuantite.Ecrire(quantite)} {mot}");
            quantite = null;
            reste = RetirerLiaison(reste[petite.Length..]);
        }
        else if (quantite is not null)
        {
            if (TrouverUnite(reste) is { } unite)
            {
                codeUnite = unite.Code;
                reste = RetirerLiaison(reste[unite.Longueur..]);
            }
            else if (Contenant().Match(reste) is { Success: true } contenant)
            {
                reste = RetirerLiaison(reste[contenant.Length..]);
                var mot = contenant.Groups["mot"].Value;
                var mesure = parentheses.Select((p, i) => (Mesure: LireMesure(p), Index: i)).FirstOrDefault(m => m.Mesure is not null);
                var apresMesure = reste;
                if (LireQuantite(ref apresMesure, out var intervalleMesure) is { } quantiteEcrite && intervalleMesure is null
                    && TrouverUnite(apresMesure) is { Code: not CodeUnitePiece } uniteEcrite)
                {
                    // « 1 boîte de 540 ml de maïs » : la mesure suit le contenant (elle passe avant une parenthèse,
                    // souvent la même mesure dans l'autre système : « (19 oz) »).
                    var texteMesure = reste[..(reste.Length - apresMesure.Length + uniteEcrite.Longueur)].Trim();
                    precisions.Add($"{AnalyseQuantite.Ecrire(quantite)} {mot} de {texteMesure}");
                    reste = RetirerLiaison(apresMesure[uniteEcrite.Longueur..]);
                    quantite *= quantiteEcrite;
                    codeUnite = uniteEcrite.Code;
                }
                else if (mesure.Mesure is var (quantiteContenant, codeContenant))
                {
                    // « 2 boîtes (540 ml) » : 1080 ml, et la précision garde la façon de l'acheter.
                    precisions.Add($"{AnalyseQuantite.Ecrire(quantite)} {mot} ({parentheses[mesure.Index]})");
                    parentheses.RemoveAt(mesure.Index);
                    quantite *= quantiteContenant;
                    codeUnite = codeContenant;
                }
                else
                {
                    precisions.Add(mot);
                    codeUnite = CodeUnitePiece;
                }
            }
            else
            {
                codeUnite = CodeUnitePiece;
            }
        }

        AjouterTaille(ref reste, precisions);
        if (TailleALaFin().Match(reste) is { Success: true } tailleFin)
        {
            precisions.Add(tailleFin.Groups["mot"].Value);
            reste = reste[..tailleFin.Index];
        }

        precisions.AddRange(parentheses);
        if (apresVirgule is { Length: > 0 })
        {
            precisions.Add(apresVirgule);
        }

        if (auGout.Success)
        {
            precisions.Add(auGout.Value.ToLowerInvariant());
        }

        return new LigneLue(
            texte,
            quantite,
            codeUnite,
            NettoyerMorceau(reste),
            precisions.Count > 0 ? string.Join(", ", precisions) : null,
            estOptionnel);
    }

    /// <summary>Reconnaît une unité au début du texte.</summary>
    /// <returns>Son code et la longueur du texte qu'elle occupe ; <c>null</c> sinon.</returns>
    public static (string Code, int Longueur)? TrouverUnite(string texte)
    {
        foreach (var (motif, code) in Unites)
        {
            var trouve = motif.Match(texte);
            if (trouve.Success)
            {
                return (code, trouve.Length);
            }
        }

        return null;
    }

    private static decimal? LireQuantite(ref string reste, out string? intervalle)
    {
        intervalle = null;
        var trouve = Quantite().Match(reste);
        if (trouve.Success && AnalyseQuantite.EssayerLire(trouve.Groups["q"].Value, out var premiere) && premiere > 0)
        {
            if (trouve.Groups["q2"].Success && AnalyseQuantite.EssayerLire(trouve.Groups["q2"].Value, out var seconde) && seconde > premiere)
            {
                // « 2 à 3 tasses » : on achète pour le plus grand ; l'intervalle reste visible.
                intervalle = Nettoyer(trouve.Value);
                reste = reste[trouve.Length..].Trim();
                return seconde;
            }

            reste = reste[trouve.Groups["q"].Length..].Trim();
            return premiere;
        }

        var enLettres = QuantiteEnLettres().Match(reste);
        if (enLettres.Success)
        {
            reste = reste[enLettres.Length..];
            return 1m;
        }

        return null;
    }

    /// <summary>« 540 ml », « 28 oz chacune » : une mesure complète, sinon <c>null</c>.</summary>
    private static (decimal Quantite, string Code)? LireMesure(string texte)
    {
        var reste = texte;
        if (LireQuantite(ref reste, out var intervalle) is not { } quantite || intervalle is not null
            || TrouverUnite(reste) is not { } unite || unite.Code == CodeUnitePiece)
        {
            return null;
        }

        var apres = reste[unite.Longueur..].Trim();
        return apres.Length == 0 || Chacun().IsMatch(apres) ? (quantite, unite.Code) : null;
    }

    private static void AjouterTaille(ref string reste, List<string> precisions)
    {
        var taille = TailleAuDebut().Match(reste);
        if (taille.Success)
        {
            precisions.Add(taille.Groups["mot"].Value);
            reste = reste[taille.Length..];
        }
    }

    private static string RetirerLiaison(string texte)
    {
        var debut = texte.TrimStart();
        return debut[Liaison().Match(debut).Length..];
    }

    /// <summary>Espaces normalisées (insécables comprises), apostrophes droites.</summary>
    private static string Nettoyer(string? texte) =>
        Espaces().Replace((texte ?? string.Empty).Replace('’', '\'').Replace(' ', ' ').Replace(' ', ' '), " ").Trim();

    /// <summary>Retire la ponctuation qui traîne autour d'un morceau (« , », « ; », « . »).</summary>
    private static string NettoyerMorceau(string texte) => Nettoyer(texte).Trim(' ', ',', ';', ':', '.', '-');
}
