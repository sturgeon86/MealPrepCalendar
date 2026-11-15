namespace Repas.Domaine.Mesures;

/// <summary>Ce qu'il faut savoir d'un ingrédient pour convertir ses quantités.</summary>
public sealed record MesuresIngredient
{
    /// <param name="uniteReferenceId">Unité dans laquelle la liste d'épicerie additionne ses quantités.</param>
    /// <param name="densiteGParMl">Pour passer du volume au poids (ex. : farine = 0,5 g/ml). Facultatif.</param>
    /// <param name="poidsMoyenUniteG">Pour passer de la pièce au poids (ex. : 1 oignon ≈ 150 g). Facultatif.</param>
    public MesuresIngredient(int uniteReferenceId, decimal? densiteGParMl = null, decimal? poidsMoyenUniteG = null)
    {
        if (densiteGParMl <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(densiteGParMl), densiteGParMl, "La densité doit être positive.");
        }

        if (poidsMoyenUniteG <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(poidsMoyenUniteG), poidsMoyenUniteG, "Le poids moyen doit être positif.");
        }

        UniteReferenceId = uniteReferenceId;
        DensiteGParMl = densiteGParMl;
        PoidsMoyenUniteG = poidsMoyenUniteG;
    }

    public int UniteReferenceId { get; }

    public decimal? DensiteGParMl { get; }

    public decimal? PoidsMoyenUniteG { get; }
}
