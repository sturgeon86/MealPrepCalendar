using Repas.Domaine.Recettes;

namespace Repas.Domaine.Import;

/// <summary>
/// Une recette lue d'une source (texte collé, site web, photo), pas encore validée. Les ingrédients sont encore du texte :
/// ils passent par <see cref="LectureLigneIngredient"/> et <see cref="RapprochementIngredients"/> dans l'écran de validation.
/// </summary>
/// <param name="Portions">Absentes si la source ne les donne pas.</param>
/// <param name="Notes">Le texte qui n'est ni un ingrédient ni une étape (une introduction, par exemple).</param>
/// <param name="SourceUrl">L'adresse de la page d'où vient la recette (import web).</param>
/// <param name="PhotoUrl">L'adresse de la photo de la recette, à télécharger (import web).</param>
public sealed record BrouillonRecette(
    string Nom,
    int? Portions,
    int? TempsPreparationMin,
    int? TempsCuissonMin,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<string> Etapes,
    string? Notes,
    SourceRecette Source,
    string? SourceUrl = null,
    string? PhotoUrl = null);

/// <summary>Une ligne d'ingrédient du brouillon, lue et rapprochée du référentiel.</summary>
public sealed record IngredientImporte(LigneLue Lue, ResultatRapprochement Rapprochement);

/// <summary>Prépare les ingrédients d'un brouillon pour l'écran de validation.</summary>
public static class PreparationImport
{
    /// <summary>Lit chaque ligne et la rapproche du référentiel ; les lignes vides sont ignorées.</summary>
    public static IReadOnlyList<IngredientImporte> Preparer(IEnumerable<string> lignes, RapprochementIngredients rapprochement)
    {
        ArgumentNullException.ThrowIfNull(lignes);
        ArgumentNullException.ThrowIfNull(rapprochement);

        return lignes
            .Select(LectureLigneIngredient.Lire)
            .OfType<LigneLue>()
            .Where(l => l.Nom.Length > 0)
            .Select(l => new IngredientImporte(l, rapprochement.Rapprocher(l.Nom)))
            .ToList();
    }
}
