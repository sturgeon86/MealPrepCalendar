namespace Repas.Domaine.Epicerie;

/// <summary>Ce qu'a donné une demande de terminaison.</summary>
public enum StatutTerminaison
{
    Terminee,

    /// <summary>La liste n'est plus active (terminée depuis un autre appareil, par exemple).</summary>
    ListeNonActive,
}

/// <param name="NouvelleListeId">La prochaine liste, créée pour recevoir les articles reportés ; <c>null</c> sans report.</param>
public sealed record ResultatTerminaison(StatutTerminaison Statut, int ListeTermineeId, int? NouvelleListeId, int NbReportes);

/// <summary>
/// Fin d'une liste : la liste passe au statut terminé, et les articles non achetés choisis (tous ou un par un)
/// sont reportés sur la prochaine liste avec la mention « reporté ». Une regénération ne les touche pas.
/// </summary>
public static class Terminaison
{
    /// <summary>Les articles qui restent à acheter : ce sont eux qu'on propose de reporter.</summary>
    public static IReadOnlyList<ArticleListe> NonAchetes(IEnumerable<ArticleListe> articles)
    {
        ArgumentNullException.ThrowIfNull(articles);
        return articles.Where(a => !a.EstCoche).ToList();
    }

    /// <summary>
    /// Les articles à reporter, parmi ceux choisis : seulement ceux de la liste, pas encore cochés,
    /// dans l'ordre de la liste. Un choix inconnu ou déjà coché (par quelqu'un d'autre entre-temps) est ignoré.
    /// </summary>
    public static IReadOnlyList<ArticleListe> ArticlesAReporter(IEnumerable<ArticleListe> articles, IEnumerable<int> choisis)
    {
        ArgumentNullException.ThrowIfNull(articles);
        ArgumentNullException.ThrowIfNull(choisis);

        var ids = choisis.ToHashSet();
        return NonAchetes(articles).Where(a => ids.Contains(a.Id)).ToList();
    }
}
