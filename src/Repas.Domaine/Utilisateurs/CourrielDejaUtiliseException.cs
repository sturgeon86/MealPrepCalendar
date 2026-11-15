namespace Repas.Domaine.Utilisateurs;

/// <summary>Un compte existe déjà avec ce courriel.</summary>
public sealed class CourrielDejaUtiliseException : Exception
{
    public CourrielDejaUtiliseException(string courriel, Exception? innerException = null)
        : base($"Un compte existe déjà avec le courriel « {courriel} ».", innerException)
    {
        Courriel = courriel;
    }

    public string Courriel { get; }
}
