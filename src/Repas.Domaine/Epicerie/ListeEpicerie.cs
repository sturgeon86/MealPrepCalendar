using Repas.Domaine.Mesures;

namespace Repas.Domaine.Epicerie;

/// <summary>Statut d'une liste. Les noms correspondent à la colonne <c>ListeEpicerie.Statut</c>.</summary>
public enum StatutListe
{
    Active,
    Terminee,
}

/// <summary>L'en-tête d'une liste d'épicerie.</summary>
/// <param name="DateDebut">Début de la période générée ; <c>null</c> tant que la liste n'a pas été générée.</param>
/// <param name="DateGenerationUtc">Moment de la dernière génération, en UTC.</param>
public sealed record ListeEpicerie(
    int Id,
    string Nom,
    DateOnly? DateDebut,
    DateOnly? DateFin,
    DateTime? DateGenerationUtc,
    StatutListe Statut)
{
    /// <summary>Vrai si la liste a déjà été générée à partir d'une période du calendrier.</summary>
    public bool EstGeneree => DateDebut is not null;
}

/// <summary>Un article de la liste, prêt à afficher.</summary>
/// <param name="Quantite">Somme exacte ; <c>null</c> pour « au goût » ou un article manuel sans quantité.</param>
/// <param name="CodeUnite"><c>null</c> avec la quantité.</param>
/// <param name="OrdreCategorie">Ordre générique de la catégorie dans le trajet en magasin.</param>
/// <param name="Mesures">Les mesures actuelles de l'ingrédient, pour savoir si la ligne attend une fiche complétée.</param>
public sealed record ArticleListe(
    int Id,
    int IngredientId,
    string NomIngredient,
    string Categorie,
    int OrdreCategorie,
    decimal? Quantite,
    int? UniteId,
    string? CodeUnite,
    int NbRecettes,
    bool EstManuel,
    bool EstReporte,
    bool EstCoche,
    MesuresIngredient Mesures);

/// <summary>Une liste et ses articles, dans l'ordre du trajet en magasin (catégorie, puis nom).</summary>
public sealed record ListeAvecArticles(ListeEpicerie Liste, IReadOnlyList<ArticleListe> Articles);

/// <summary>Ce qu'a donné une demande de génération.</summary>
public enum StatutGeneration
{
    /// <summary>La liste active a été créée (ou complétée, si elle attendait sa première génération).</summary>
    Generee,

    /// <summary>Une liste active générée existe déjà : la regénérer est une autre opération.</summary>
    DejaGeneree,

    /// <summary>Aucun repas avec recette dans la période : rien à acheter.</summary>
    AucunRepas,
}

/// <param name="ListeId">La liste active, sauf pour <see cref="StatutGeneration.AucunRepas"/> sans liste existante.</param>
public sealed record ResultatGeneration(StatutGeneration Statut, int? ListeId, int NbRepas, int NbArticles);

/// <summary>Les alertes de la liste : une ligne restée dans une unité que l'ingrédient ne sait pas convertir.</summary>
public static class AlertesListe
{
    /// <summary>
    /// Ce qui manque à la fiche de l'ingrédient pour convertir cette ligne vers son unité de référence ;
    /// <see cref="RaisonEchecConversion.Aucune"/> si la ligne est déjà dans cette unité, sans quantité,
    /// ou convertible (la fiche a été complétée depuis la génération : une regénération la regroupera).
    /// </summary>
    public static RaisonEchecConversion Verifier(ArticleListe article, ConvertisseurUnites convertisseur)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(convertisseur);

        if (article.UniteId is not { } uniteId || uniteId == article.Mesures.UniteReferenceId)
        {
            return RaisonEchecConversion.Aucune;
        }

        return convertisseur.VersReference(1m, uniteId, article.Mesures).Raisons;
    }

    /// <summary>Ce qu'il faut ajouter à la fiche, en français, pour une raison d'échec.</summary>
    public static string Decrire(RaisonEchecConversion raisons)
    {
        var manques = new List<string>();
        if (raisons.HasFlag(RaisonEchecConversion.DensiteManquante))
        {
            manques.Add("la densité");
        }

        if (raisons.HasFlag(RaisonEchecConversion.PoidsMoyenManquant))
        {
            manques.Add("le poids moyen d'une unité");
        }

        if (raisons.HasFlag(RaisonEchecConversion.UniteInconnue))
        {
            manques.Add("une unité connue");
        }

        return manques.Count == 0
            ? string.Empty
            : $"Pour regrouper cette ligne, il manque {string.Join(" et ", manques)} dans la fiche de l'ingrédient.";
    }
}
