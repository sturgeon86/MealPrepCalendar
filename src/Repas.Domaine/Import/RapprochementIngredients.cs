using System.Globalization;
using System.Text;

namespace Repas.Domaine.Import;

/// <summary>Un ingrédient actif du référentiel, avec ses autres noms.</summary>
public sealed record IngredientConnu(int Id, string Nom, IReadOnlyList<string> Synonymes);

/// <summary>Ce que le rapprochement a trouvé pour un nom.</summary>
public enum StatutRapprochement
{
    /// <summary>Un seul ingrédient porte ce nom (ou ce synonyme) : il est proposé.</summary>
    Exact,

    /// <summary>Pas de nom identique, mais un ingrédient ressemble plus que les autres : il est proposé, à confirmer.</summary>
    Approche,

    /// <summary>Plusieurs ingrédients conviennent autant : aucun n'est choisi, la personne tranche.</summary>
    Ambigu,

    /// <summary>Rien ne ressemble : l'ingrédient est à créer.</summary>
    ACreer,
}

/// <summary>Un ingrédient suggéré pour un nom lu.</summary>
/// <param name="Synonyme">Le synonyme qui a mené à l'ingrédient ; <c>null</c> si c'est son nom.</param>
/// <param name="Score">De 0 à 1 ; 1 pour un nom identique.</param>
public sealed record SuggestionIngredient(int IngredientId, string Nom, string? Synonyme, decimal Score);

/// <summary>Résultat du rapprochement d'un nom avec le référentiel.</summary>
/// <param name="Suggestions">Les meilleurs d'abord ; vide pour <see cref="StatutRapprochement.ACreer"/>.</param>
public sealed record ResultatRapprochement(StatutRapprochement Statut, IReadOnlyList<SuggestionIngredient> Suggestions)
{
    /// <summary>L'ingrédient à proposer ; <c>null</c> s'il faut choisir (ambigu) ou créer.</summary>
    public SuggestionIngredient? Propose =>
        Statut is StatutRapprochement.Exact or StatutRapprochement.Approche ? Suggestions[0] : null;
}

/// <summary>
/// Rapproche un nom d'ingrédient lu dans une recette (« oignons jaunes ») du référentiel (<i>Oignon jaune</i>) :
/// par nom et par synonyme, sans égard à la casse, aux accents, au pluriel ni aux petits mots (« de », « à »…).
/// Quand rien n'est identique, des ingrédients qui partagent des mots sont suggérés. Deux ingrédients qui conviennent
/// autant ne sont jamais départagés en silence.
/// </summary>
public sealed class RapprochementIngredients
{
    /// <summary>Nombre maximal de suggestions retournées.</summary>
    public const int NombreSuggestions = 5;

    /// <summary>Ressemblance minimale (proportion de mots en commun) pour suggérer un ingrédient.</summary>
    public const decimal ScoreMinimal = 0.5m;

    private static readonly HashSet<string> PetitsMots =
    [
        "de", "d", "du", "des", "la", "le", "les", "l", "a", "au", "aux", "en", "et", "pour", "avec", "of", "the", "and",
    ];

    private readonly List<Libelle> _libelles = [];

    /// <param name="ingredients">Les ingrédients actifs du référentiel.</param>
    public RapprochementIngredients(IEnumerable<IngredientConnu> ingredients)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        foreach (var ingredient in ingredients)
        {
            _libelles.Add(new Libelle(ingredient, null, Mots(ingredient.Nom)));
            foreach (var synonyme in ingredient.Synonymes)
            {
                _libelles.Add(new Libelle(ingredient, synonyme, Mots(synonyme)));
            }
        }
    }

    /// <summary>Rapproche un nom du référentiel.</summary>
    public ResultatRapprochement Rapprocher(string? nom)
    {
        var mots = Mots(nom);
        if (mots.Count == 0)
        {
            return new ResultatRapprochement(StatutRapprochement.ACreer, []);
        }

        // Le meilleur libellé de chaque ingrédient : son nom à score égal, plutôt qu'un synonyme.
        var suggestions = _libelles
            .Select(l => (Libelle: l, Score: Ressemblance(mots, l.Mots)))
            .Where(s => s.Score >= ScoreMinimal)
            .GroupBy(s => s.Libelle.Ingredient.Id)
            .Select(g => g.OrderByDescending(s => s.Score).ThenBy(s => s.Libelle.Synonyme is null ? 0 : 1).First())
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Libelle.Ingredient.Nom, StringComparer.Create(CultureInfo.GetCultureInfo("fr-CA"), true))
            .Select(s => new SuggestionIngredient(
                s.Libelle.Ingredient.Id, s.Libelle.Ingredient.Nom, s.Libelle.Synonyme, Math.Round(s.Score, 4)))
            .ToList();

        if (suggestions.Count == 0)
        {
            return new ResultatRapprochement(StatutRapprochement.ACreer, []);
        }

        var exacts = suggestions.Count(s => s.Score == 1m);
        if (exacts == 1)
        {
            return new ResultatRapprochement(StatutRapprochement.Exact, suggestions.Take(1).ToList());
        }

        var retenues = suggestions.Take(NombreSuggestions).ToList();
        var departage = exacts == 0 && (suggestions.Count == 1 || suggestions[0].Score > suggestions[1].Score);
        return new ResultatRapprochement(departage ? StatutRapprochement.Approche : StatutRapprochement.Ambigu, retenues);
    }

    /// <summary>
    /// Les mots qui comptent d'un nom, ramenés à une forme commune : minuscules, sans accents (« œ » devient « oe »),
    /// au singulier, sans ponctuation ni petits mots. « Pâtes de tomates » et « pate tomate » donnent les mêmes mots.
    /// </summary>
    public static IReadOnlyList<string> Mots(string? nom)
    {
        if (string.IsNullOrWhiteSpace(nom))
        {
            return [];
        }

        var texte = nom.ToLowerInvariant().Replace("œ", "oe").Replace("æ", "ae");
        var sansAccents = new StringBuilder(texte.Length);
        foreach (var caractere in texte.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                sansAccents.Append(caractere);
            }
        }

        // « 3,25 % » et « 3.25% » s'écrivent pareil ; le reste de la ponctuation sépare les mots.
        var mots = new List<string>();
        var mot = new StringBuilder();
        var normalise = sansAccents.ToString().Normalize(NormalizationForm.FormC);
        for (var i = 0; i < normalise.Length; i++)
        {
            var c = normalise[i];
            var entreChiffres = (c is ',' or '.') && i > 0 && i < normalise.Length - 1
                                && char.IsDigit(normalise[i - 1]) && char.IsDigit(normalise[i + 1]);
            if (char.IsLetterOrDigit(c) || entreChiffres)
            {
                mot.Append(c == ',' ? '.' : c);
            }
            else if (c == '%')
            {
                // « 2 % » : le pourcentage colle au nombre qui le précède.
                if (mot.Length == 0 && mots.Count > 0 && char.IsDigit(mots[^1][^1]))
                {
                    mots[^1] += "%";
                }
                else
                {
                    mot.Append('%');
                }
            }
            else
            {
                Terminer(mot, mots);
            }
        }

        Terminer(mot, mots);
        return mots.Where(m => !PetitsMots.Contains(m)).Select(AuSingulier).ToList();
    }

    private static void Terminer(StringBuilder mot, List<string> mots)
    {
        if (mot.Length > 0)
        {
            mots.Add(mot.ToString());
            mot.Clear();
        }
    }

    /// <summary>
    /// Un singulier approximatif : le « s » ou le « x » final tombe. Appliqué des deux côtés, il suffit à rapprocher
    /// « oignons » d'« oignon » ou « choux » de « chou », même s'il donne parfois un mot qui n'existe pas (« poi »).
    /// </summary>
    private static string AuSingulier(string mot) =>
        mot.Length > 3 && (mot[^1] is 's' or 'x') && !char.IsDigit(mot[^2]) ? mot[..^1] : mot;

    /// <summary>
    /// Proportion de mots en commun (coefficient de Dice) : 1 quand les deux noms ont les mêmes mots, dans n'importe
    /// quel ordre. Une petite faute de frappe dans un mot assez long compte comme le même mot (« brocolli »).
    /// </summary>
    private static decimal Ressemblance(IReadOnlyList<string> lus, IReadOnlyList<string> connus)
    {
        if (connus.Count == 0)
        {
            return 0m;
        }

        var restants = connus.ToList();
        var communs = 0;
        var identiques = true;
        foreach (var mot in lus)
        {
            var position = restants.IndexOf(mot);
            if (position < 0)
            {
                position = restants.FindIndex(c => PresqueEgal(mot, c));
                identiques = false;
            }

            if (position >= 0)
            {
                restants.RemoveAt(position);
                communs++;
            }
        }

        var score = 2m * communs / (lus.Count + connus.Count);

        // Une faute de frappe rapproche, mais n'est jamais un nom identique.
        return score == 1m && !identiques ? 0.95m : score;
    }

    private static bool PresqueEgal(string a, string b)
    {
        var plusCourt = Math.Min(a.Length, b.Length);
        if (plusCourt < 5 || Math.Abs(a.Length - b.Length) > 2 || a.Any(char.IsDigit) || b.Any(char.IsDigit))
        {
            return false;
        }

        return Distance(a, b) <= (plusCourt >= 8 ? 2 : 1);
    }

    /// <summary>Distance de Levenshtein : nombre de lettres à ajouter, retirer ou changer.</summary>
    private static int Distance(string a, string b)
    {
        var precedente = new int[b.Length + 1];
        var courante = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            precedente[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            courante[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cout = a[i - 1] == b[j - 1] ? 0 : 1;
                courante[j] = Math.Min(Math.Min(courante[j - 1] + 1, precedente[j] + 1), precedente[j - 1] + cout);
            }

            (precedente, courante) = (courante, precedente);
        }

        return precedente[b.Length];
    }

    private sealed record Libelle(IngredientConnu Ingredient, string? Synonyme, IReadOnlyList<string> Mots);
}
