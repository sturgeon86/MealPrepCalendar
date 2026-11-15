namespace Repas.Domaine.Mesures;

/// <summary>Unité de mesure (g, kg, tasse, c. à soupe, unité…).</summary>
public sealed record Unite
{
    /// <param name="facteurVersReference">
    /// Combien d'unités de base de son type (g, ml ou unité) vaut 1 de cette unité. Ex. : kg = 1000, tasse = 250.
    /// </param>
    public Unite(int id, string code, TypeMesure typeMesure, decimal facteurVersReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (facteurVersReference <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(facteurVersReference), facteurVersReference, "Le facteur d'une unité doit être positif.");
        }

        Id = id;
        Code = code;
        TypeMesure = typeMesure;
        FacteurVersReference = facteurVersReference;
    }

    public int Id { get; }

    public string Code { get; }

    public TypeMesure TypeMesure { get; }

    public decimal FacteurVersReference { get; }
}
