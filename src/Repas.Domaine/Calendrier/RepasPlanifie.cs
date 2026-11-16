namespace Repas.Domaine.Calendrier;

/// <summary>Moment de la journée où un repas est planifié. Les noms correspondent à la colonne <c>RepasPlanifie.Plage</c>.</summary>
public enum Plage
{
    Diner,
    Souper,
}

/// <summary>Un repas inscrit au calendrier : une recette avec un nombre de portions, ou un texte libre.</summary>
/// <param name="Id">0 pour un repas pas encore enregistré.</param>
/// <param name="Date">Date locale de la famille.</param>
/// <param name="RecetteId">La recette à préparer ; <c>null</c> pour un repas libre.</param>
/// <param name="TexteLibre">Un repas sans recette (« Restaurant », « Restes ») ; <c>null</c> pour une recette.</param>
/// <param name="Portions">Portions à préparer ; seulement pour une recette.</param>
/// <param name="NomRecette">Pour l'affichage seulement ; ignoré à l'enregistrement.</param>
/// <param name="PortionsRecette">Portions que donne la recette elle-même ; pour l'affichage seulement.</param>
public sealed record RepasPlanifie(
    int Id,
    DateOnly Date,
    Plage Plage,
    int? RecetteId,
    string? TexteLibre,
    int? Portions,
    string? NomRecette = null,
    int? PortionsRecette = null)
{
    /// <summary>Un repas tiré d'une recette du livre.</summary>
    public static RepasPlanifie PourRecette(DateOnly date, Plage plage, int recetteId, int portions) =>
        new(0, date, plage, recetteId, null, portions);

    /// <summary>Un repas sans recette.</summary>
    public static RepasPlanifie Libre(DateOnly date, Plage plage, string texte) =>
        new(0, date, plage, null, texte, null);

    /// <summary>Ce qui s'affiche dans le calendrier.</summary>
    public string Titre => RecetteId is null ? TexteLibre ?? string.Empty : NomRecette ?? string.Empty;
}

/// <summary>Libellés des plages pour l'interface.</summary>
public static class LibellesPlage
{
    public static string Libelle(this Plage plage) => plage switch
    {
        Plage.Diner => "Dîner",
        _ => "Souper",
    };
}
