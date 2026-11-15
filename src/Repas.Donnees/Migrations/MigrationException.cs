namespace Repas.Donnees.Migrations;

/// <summary>Erreur qui empêche d'appliquer les migrations ; le démarrage de l'app doit s'arrêter.</summary>
public sealed class MigrationException : Exception
{
    public MigrationException(string message) : base(message)
    {
    }

    public MigrationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
