namespace Repas.Domaine.Referentiel;

/// <summary>Une autre catégorie porte déjà ce nom.</summary>
public sealed class NomCategorieDejaUtiliseException(string nom, Exception? innerException = null)
    : Exception($"Une catégorie porte déjà le nom « {nom} ».", innerException)
{
    public string Nom { get; } = nom;
}

/// <summary>
/// Le nom ou un synonyme d'un ingrédient est déjà le nom ou un synonyme d'un autre ingrédient :
/// chaque texte doit mener à un seul ingrédient.
/// </summary>
public sealed class TexteIngredientDejaUtiliseException(string texte, string ingredientExistant, Exception? innerException = null)
    : Exception($"« {texte} » est déjà utilisé par l'ingrédient « {ingredientExistant} ».", innerException)
{
    public string Texte { get; } = texte;

    public string IngredientExistant { get; } = ingredientExistant;
}
