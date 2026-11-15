namespace Repas.Domaine.Utilisateurs;

/// <summary>Règles appliquées au choix d'un mot de passe.</summary>
public static class MotDePasse
{
    public const int LongueurMin = 8;

    /// <summary>Borne la quantité de texte à hacher ; largement au-delà de tout mot de passe réel.</summary>
    public const int LongueurMax = 200;
}
