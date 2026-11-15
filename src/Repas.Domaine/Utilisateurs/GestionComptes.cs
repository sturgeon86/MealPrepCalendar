namespace Repas.Domaine.Utilisateurs;

/// <summary>Règles de gestion des comptes de la famille.</summary>
public static class GestionComptes
{
    /// <summary>
    /// Une personne peut activer ou désactiver n'importe quel compte sauf le sien : ainsi,
    /// il reste toujours au moins un compte actif (le sien) et personne ne s'enferme dehors.
    /// </summary>
    public static bool PeutModifierActif(int idDemandeur, int idCible) => idDemandeur != idCible;
}
