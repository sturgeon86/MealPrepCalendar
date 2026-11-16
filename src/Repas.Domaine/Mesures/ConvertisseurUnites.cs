namespace Repas.Domaine.Mesures;

/// <summary>
/// Convertit une quantité d'un ingrédient d'une unité à une autre.
/// <list type="bullet">
/// <item>Même type de mesure : par les facteurs des unités (ex. : 1 tasse = 250 ml).</item>
/// <item>Volume ↔ poids : par la densité de l'ingrédient.</item>
/// <item>Pièce ↔ poids : par le poids moyen de l'ingrédient.</item>
/// <item>Volume ↔ pièce : en passant par le poids, donc avec la densité et le poids moyen.</item>
/// </list>
/// Une conversion impossible n'est pas une erreur : le résultat dit ce qui manque.
/// </summary>
public sealed class ConvertisseurUnites
{
    private readonly Dictionary<int, Unite> _unites;

    public ConvertisseurUnites(IEnumerable<Unite> unites)
    {
        ArgumentNullException.ThrowIfNull(unites);
        _unites = unites.ToDictionary(u => u.Id);
    }

    /// <summary>L'unité de ce numéro, ou <c>null</c> si elle n'est pas dans le référentiel.</summary>
    public Unite? Trouver(int uniteId) => _unites.GetValueOrDefault(uniteId);

    /// <summary>L'unité de base d'un type de mesure (facteur 1 : g, ml ou unité), ou <c>null</c> si le référentiel n'en a pas.</summary>
    public Unite? UniteDeBase(TypeMesure type) =>
        _unites.Values.Where(u => u.TypeMesure == type && u.FacteurVersReference == 1m).OrderBy(u => u.Id).FirstOrDefault();

    /// <summary>Convertit vers l'unité de référence de l'ingrédient (celle de la liste d'épicerie).</summary>
    public ResultatConversion VersReference(decimal quantite, int uniteSourceId, MesuresIngredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        return Convertir(quantite, uniteSourceId, ingredient.UniteReferenceId, ingredient);
    }

    public ResultatConversion Convertir(
        decimal quantite, int uniteSourceId, int uniteCibleId, MesuresIngredient ingredient)
    {
        if (!_unites.TryGetValue(uniteSourceId, out var source) || !_unites.TryGetValue(uniteCibleId, out var cible))
        {
            return ResultatConversion.Echec(RaisonEchecConversion.UniteInconnue);
        }

        return Convertir(quantite, source, cible, ingredient);
    }

    /// <summary>Conversion entre deux unités connues ; n'a pas besoin du référentiel d'unités.</summary>
    public static ResultatConversion Convertir(decimal quantite, Unite source, Unite cible, MesuresIngredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cible);
        ArgumentNullException.ThrowIfNull(ingredient);
        if (quantite < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantite), quantite, "Une quantité ne peut pas être négative.");
        }

        // 1. Vers l'unité de base du type de départ (g, ml ou unité).
        var enBase = quantite * source.FacteurVersReference;

        // 2. Changement de type de mesure, s'il y a lieu.
        var changement = ChangerDeType(enBase, source.TypeMesure, cible.TypeMesure, ingredient);
        if (!changement.Reussie)
        {
            return changement;
        }

        // 3. De l'unité de base du type d'arrivée vers l'unité demandée.
        return ResultatConversion.Reussite(changement.Quantite / cible.FacteurVersReference);
    }

    private static ResultatConversion ChangerDeType(
        decimal quantiteEnBase, TypeMesure depart, TypeMesure arrivee, MesuresIngredient ingredient)
    {
        if (depart == arrivee)
        {
            return ResultatConversion.Reussite(quantiteEnBase);
        }

        // Tout changement de type passe par le poids (g), le seul type relié aux deux autres.
        var manque = RaisonEchecConversion.Aucune;
        manque |= FacteurVersGrammes(depart, ingredient);
        manque |= FacteurVersGrammes(arrivee, ingredient);
        if (manque != RaisonEchecConversion.Aucune)
        {
            return ResultatConversion.Echec(manque);
        }

        var grammes = quantiteEnBase * GrammesPar(depart, ingredient);
        return ResultatConversion.Reussite(grammes / GrammesPar(arrivee, ingredient));
    }

    /// <summary>Ce qui manque pour passer de l'unité de base de ce type au gramme.</summary>
    private static RaisonEchecConversion FacteurVersGrammes(TypeMesure type, MesuresIngredient ingredient) => type switch
    {
        TypeMesure.Volume when ingredient.DensiteGParMl is null => RaisonEchecConversion.DensiteManquante,
        TypeMesure.Unite when ingredient.PoidsMoyenUniteG is null => RaisonEchecConversion.PoidsMoyenManquant,
        _ => RaisonEchecConversion.Aucune,
    };

    /// <summary>Grammes dans 1 g, 1 ml ou 1 unité de cet ingrédient (après vérification par <see cref="FacteurVersGrammes"/>).</summary>
    private static decimal GrammesPar(TypeMesure type, MesuresIngredient ingredient) => type switch
    {
        TypeMesure.Poids => 1m,
        TypeMesure.Volume => ingredient.DensiteGParMl!.Value,
        TypeMesure.Unite => ingredient.PoidsMoyenUniteG!.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Type de mesure inconnu."),
    };
}
