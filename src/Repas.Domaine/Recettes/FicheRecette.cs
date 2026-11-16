namespace Repas.Domaine.Recettes;

/// <summary>Comment une recette est entrée dans le livre. Les noms correspondent à la colonne <c>Recette.Source</c>.</summary>
public enum SourceRecette
{
    Manuelle,
    Web,
    Ocr,

    /// <summary>Importée d'un texte collé (story 8.3).</summary>
    Texte,
}

/// <summary>Un ingrédient d'une recette, avec sa quantité.</summary>
/// <param name="Quantite">Absente avec l'unité pour « au goût ».</param>
/// <param name="Precision">Précision libre (« haché finement »), sans effet sur la liste d'épicerie.</param>
/// <param name="NomIngredient">Pour l'affichage seulement ; ignoré à l'enregistrement.</param>
public sealed record LigneIngredient(
    int IngredientId,
    decimal? Quantite,
    int? UniteId,
    string? Precision,
    bool EstOptionnel,
    string NomIngredient = "");

/// <summary>Une recette complète. L'ordre des listes est celui de la recette.</summary>
/// <param name="Id">0 pour une recette pas encore enregistrée.</param>
/// <param name="Portions">Nombre de portions que donnent les quantités.</param>
/// <param name="Etiquettes">Noms des étiquettes (« Végétarien », « Souper rapide »…).</param>
/// <param name="PhotoChemin">Nom du fichier dans le dossier des photos (voir <see cref="PhotoRecette"/>) ; <c>null</c> sans photo.</param>
public sealed record FicheRecette(
    int Id,
    string Nom,
    int Portions,
    int? TempsPreparationMin,
    int? TempsCuissonMin,
    string? Notes,
    SourceRecette Source,
    IReadOnlyList<LigneIngredient> Ingredients,
    IReadOnlyList<string> Etapes,
    IReadOnlyList<string> Etiquettes,
    string? PhotoChemin = null);

/// <summary>Ligne de la liste des recettes.</summary>
/// <param name="TempsTotalMin">Préparation et cuisson ; <c>null</c> si aucun des deux n'est connu.</param>
/// <param name="Etiquettes">Étiquettes séparées par des virgules ; vide s'il n'y en a pas.</param>
/// <param name="PhotoChemin">Nom du fichier de la photo ; <c>null</c> sans photo.</param>
public sealed record ResumeRecette(int Id, string Nom, int Portions, int? TempsTotalMin, string Etiquettes, string? PhotoChemin = null);

/// <summary>Résultat d'une modification de recette.</summary>
/// <param name="AnciennePhotoChemin">La photo enregistrée juste avant la modification, pour supprimer son fichier si elle a changé.</param>
public sealed record ModificationRecette(string? AnciennePhotoChemin);
