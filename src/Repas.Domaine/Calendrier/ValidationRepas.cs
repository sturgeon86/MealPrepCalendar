using Repas.Domaine.Recettes;

namespace Repas.Domaine.Calendrier;

/// <summary>Règles d'un repas planifié, vérifiées avant l'enregistrement (la base les impose aussi).</summary>
public static class ValidationRepas
{
    public const int LongueurMaxTexteLibre = 200;

    /// <summary>Les erreurs à corriger, en français ; vide si le repas est valide.</summary>
    public static IReadOnlyList<string> Valider(RepasPlanifie repas)
    {
        var erreurs = new List<string>();

        if (!Enum.IsDefined(repas.Plage))
        {
            erreurs.Add("Choisissez le dîner ou le souper.");
        }

        var aTexte = !string.IsNullOrWhiteSpace(repas.TexteLibre);
        if (repas.RecetteId is null && !aTexte)
        {
            erreurs.Add("Choisissez une recette ou décrivez le repas (ex. : Restaurant, Restes).");
        }
        else if (repas.RecetteId is not null && repas.TexteLibre is not null)
        {
            erreurs.Add("Un repas est une recette ou un texte libre, pas les deux.");
        }

        if (repas.RecetteId is not null && repas.Portions is not (>= 1 and <= ValidationRecette.PortionsMax))
        {
            erreurs.Add($"Le nombre de portions doit être entre 1 et {ValidationRecette.PortionsMax}.");
        }
        else if (repas.RecetteId is null && repas.Portions is not null)
        {
            erreurs.Add("Les portions ne s'appliquent qu'à une recette.");
        }

        if (aTexte && repas.TexteLibre!.Trim().Length > LongueurMaxTexteLibre)
        {
            erreurs.Add($"La description ne doit pas dépasser {LongueurMaxTexteLibre} caractères.");
        }

        return erreurs;
    }
}
