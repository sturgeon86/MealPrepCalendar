namespace Repas.Domaine.Epicerie;

/// <summary>Un article de la liste qui vient des repas, tel qu'enregistré avant la regénération.</summary>
/// <param name="Quantite">Quantité enregistrée ; <c>null</c> pour « au goût ».</param>
public sealed record ArticleExistant(int Id, int IngredientId, int? UniteId, decimal? Quantite, bool EstCoche);

/// <summary>Un article existant qui reçoit ses nouvelles quantités.</summary>
/// <param name="Decocher">
/// Vrai si l'article était coché et qu'il en faut maintenant davantage : ce qui a été acheté ne suffit plus.
/// </param>
public sealed record MiseAJourArticle(int ArticleId, ArticleCalcule Nouveau, bool Decocher);

/// <summary>Ce qu'a donné une demande de regénération.</summary>
public enum StatutRegeneration
{
    Regeneree,

    /// <summary>Aucune liste active générée : il faut d'abord la générer.</summary>
    AucuneListeGeneree,
}

/// <param name="NbConserves">Articles manuels et reportés, gardés tels quels.</param>
/// <param name="NbDecoches">Articles cochés dont la quantité a augmenté.</param>
public sealed record ResultatRegeneration(
    StatutRegeneration Statut,
    int? ListeId,
    int NbRepas,
    int NbAjoutes,
    int NbMisAJour,
    int NbRetires,
    int NbDecoches,
    int NbConserves);

/// <summary>Ce que la regénération change aux articles qui viennent des repas.</summary>
public sealed record PlanRegeneration(
    IReadOnlyList<MiseAJourArticle> MisesAJour,
    IReadOnlyList<ArticleCalcule> Ajouts,
    IReadOnlyList<int> Suppressions);

/// <summary>
/// Regénération de la liste : les articles qui viennent des repas sont recalculés, sans toucher aux articles
/// manuels ni reportés (ceux-là ne sont jamais passés ici).
/// <list type="bullet">
/// <item>Un article recalculé qui existait (même ingrédient, même unité) garde sa ligne et sa case cochée.</item>
/// <item>Une case cochée est décochée si la quantité a augmenté : il faut racheter la différence.</item>
/// <item>Un article qui n'est plus demandé par aucun repas est retiré ; un nouveau besoin est ajouté.</item>
/// </list>
/// </summary>
public static class Regeneration
{
    public static PlanRegeneration Planifier(IEnumerable<ArticleExistant> existants, IEnumerable<ArticleCalcule> calcules)
    {
        ArgumentNullException.ThrowIfNull(existants);
        ArgumentNullException.ThrowIfNull(calcules);

        // Après une fusion d'ingrédients, une liste peut avoir deux lignes de même clé : la plus ancienne est reprise.
        var disponibles = existants
            .OrderBy(e => e.Id)
            .GroupBy(e => (e.IngredientId, e.UniteId))
            .ToDictionary(g => g.Key, g => new Queue<ArticleExistant>(g));

        var misesAJour = new List<MiseAJourArticle>();
        var ajouts = new List<ArticleCalcule>();
        foreach (var calcule in calcules)
        {
            if (disponibles.TryGetValue((calcule.IngredientId, calcule.UniteId), out var file) && file.TryDequeue(out var existant))
            {
                misesAJour.Add(new MiseAJourArticle(existant.Id, calcule, existant.EstCoche && Augmente(existant.Quantite, calcule.Quantite)));
            }
            else
            {
                ajouts.Add(calcule);
            }
        }

        var suppressions = disponibles.Values.SelectMany(f => f).Select(e => e.Id).Order().ToList();
        return new PlanRegeneration(misesAJour, ajouts, suppressions);
    }

    /// <summary>
    /// Vrai s'il faut plus qu'avant. « Au goût » devenu une quantité compte comme une augmentation ;
    /// une quantité devenue « au goût », non.
    /// </summary>
    private static bool Augmente(decimal? avant, decimal? apres) => (avant, apres) switch
    {
        (_, null) => false,
        (null, _) => true,
        var (a, b) => b > a,
    };
}
