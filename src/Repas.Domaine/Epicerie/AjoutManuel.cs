using Repas.Domaine.Recettes;

namespace Repas.Domaine.Epicerie;

/// <summary>Ce qu'a donné un ajout manuel.</summary>
/// <param name="Additionne">Vrai si l'article était déjà sur la liste à la main : sa quantité a été augmentée.</param>
public sealed record ResultatAjoutManuel(int ListeId, int ArticleId, bool Additionne);

/// <summary>
/// Un article ajouté à la main à la liste : un ingrédient du référentiel (alimentaire ou non),
/// avec une quantité facultative (« Papier de toilette », « Lait 2 % : 2 l »).
/// </summary>
/// <param name="Quantite">Facultative ; avec l'unité, ou ni l'une ni l'autre.</param>
public sealed record AjoutManuel(int IngredientId, decimal? Quantite, int? UniteId)
{
    /// <summary>Les erreurs à corriger, en français ; vide si l'ajout est valide.</summary>
    public IReadOnlyList<string> Valider()
    {
        var erreurs = new List<string>();
        if (IngredientId <= 0)
        {
            erreurs.Add("Choisissez un article du référentiel.");
        }

        if (Quantite is not null && UniteId is null)
        {
            erreurs.Add("Choisissez l'unité de la quantité, ou laissez la quantité vide.");
        }
        else if (Quantite is null && UniteId is not null)
        {
            erreurs.Add("Indiquez la quantité, ou retirez l'unité.");
        }

        if (Quantite is <= 0 or > ValidationRecette.QuantiteMax)
        {
            erreurs.Add($"La quantité doit être plus grande que 0 et au plus {ValidationRecette.QuantiteMaxTexte}.");
        }

        return erreurs;
    }

    /// <summary>
    /// Ajouter de nouveau un article déjà sur la liste à la main (même ingrédient, même unité) additionne les quantités :
    /// « 1 l de lait » ajouté deux fois donne « 2 l ». Sans quantité, l'article est simplement déjà sur la liste.
    /// </summary>
    public static decimal? Additionner(decimal? existante, decimal? ajout) => (existante, ajout) switch
    {
        (null, null) => null,
        (null, var a) => a,
        (var e, null) => e,
        var (e, a) => e + a,
    };
}

/// <summary>L'ajout manuel est refusé : données invalides ou article désactivé dans le référentiel.</summary>
public sealed class AjoutManuelRefuseException(string message) : Exception(message);
