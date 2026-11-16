namespace Repas.Domaine.Recettes;

/// <summary>
/// Filtres de la liste des recettes. Un critère vide ou <c>null</c> ne filtre rien ; les critères se cumulent.
/// Les recherches de texte ignorent la casse et les accents, et cherchent le texte n'importe où dans le nom.
/// </summary>
/// <param name="Nom">Partie du nom de la recette.</param>
/// <param name="Ingredient">Partie du nom ou d'un synonyme d'un des ingrédients (« poulet » trouve aussi « Bouillon de poulet »).</param>
/// <param name="Etiquette">Nom exact d'une étiquette (sans égard à la casse).</param>
/// <param name="TempsTotalMaxMin">
/// Préparation et cuisson au plus ; une recette dont aucun temps n'est connu est alors exclue.
/// </param>
public sealed record CriteresRechercheRecettes(
    string? Nom = null,
    string? Ingredient = null,
    string? Etiquette = null,
    int? TempsTotalMaxMin = null)
{
    /// <summary>Aucun filtre : toutes les recettes.</summary>
    public static CriteresRechercheRecettes Aucun { get; } = new();

    /// <summary>Vrai si aucun critère n'est actif.</summary>
    public bool EstVide =>
        string.IsNullOrWhiteSpace(Nom)
        && string.IsNullOrWhiteSpace(Ingredient)
        && string.IsNullOrWhiteSpace(Etiquette)
        && TempsTotalMaxMin is null;
}
