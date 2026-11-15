namespace Repas.Domaine.Mesures;

/// <summary>Ce qui manque pour réussir une conversion. Plusieurs raisons peuvent se combiner.</summary>
[Flags]
public enum RaisonEchecConversion
{
    Aucune = 0,

    /// <summary>L'unité de départ ou d'arrivée n'existe pas dans le référentiel.</summary>
    UniteInconnue = 1,

    /// <summary>Passer du volume au poids (ou l'inverse) demande la densité de l'ingrédient.</summary>
    DensiteManquante = 2,

    /// <summary>Passer de la pièce au poids (ou l'inverse) demande le poids moyen de l'ingrédient.</summary>
    PoidsMoyenManquant = 4,
}

/// <summary>Quantité convertie, ou raison pour laquelle la conversion est impossible.</summary>
public sealed record ResultatConversion
{
    private ResultatConversion(decimal quantite, RaisonEchecConversion raisons)
    {
        Quantite = quantite;
        Raisons = raisons;
    }

    public bool Reussie => Raisons == RaisonEchecConversion.Aucune;

    /// <summary>La quantité dans l'unité demandée. Vaut 0 si la conversion a échoué.</summary>
    public decimal Quantite { get; }

    public RaisonEchecConversion Raisons { get; }

    public static ResultatConversion Reussite(decimal quantite) => new(quantite, RaisonEchecConversion.Aucune);

    public static ResultatConversion Echec(RaisonEchecConversion raisons)
    {
        if (raisons == RaisonEchecConversion.Aucune)
        {
            throw new ArgumentException("Un échec doit avoir au moins une raison.", nameof(raisons));
        }

        return new ResultatConversion(0m, raisons);
    }
}
