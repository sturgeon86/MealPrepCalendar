using Repas.Domaine.Mesures;

namespace Repas.Domaine.Referentiel;

/// <summary>Fiche complète d'un ingrédient (ou d'un article non alimentaire) du référentiel.</summary>
/// <param name="Id">0 pour une fiche pas encore enregistrée.</param>
/// <param name="TypeMesure">Type dans lequel la liste d'épicerie additionne les quantités.</param>
/// <param name="UniteReferenceId">Unité des totaux ; doit être du même type de mesure que l'ingrédient.</param>
/// <param name="Synonymes">Autres noms de l'ingrédient, pour la recherche et l'OCR.</param>
public sealed record FicheIngredient(
    int Id,
    string Nom,
    int CategorieId,
    TypeMesure TypeMesure,
    int UniteReferenceId,
    decimal? DensiteGParMl,
    decimal? PoidsMoyenUniteG,
    bool EstAlimentaire,
    bool Actif,
    IReadOnlyList<string> Synonymes);

/// <summary>Ligne de la liste des ingrédients.</summary>
/// <param name="Synonymes">Synonymes séparés par des virgules, pour l'affichage ; vide s'il n'y en a pas.</param>
/// <param name="FusionneDans">Nom de l'ingrédient qui le remplace, s'il a été fusionné.</param>
public sealed record ResumeIngredient(
    int Id,
    string Nom,
    int CategorieId,
    string Categorie,
    TypeMesure TypeMesure,
    string UniteReference,
    bool EstAlimentaire,
    bool Actif,
    string Synonymes,
    string? FusionneDans = null);
