namespace Repas.Domaine.Utilisateurs;

/// <summary>Compte d'un membre de la famille.</summary>
/// <param name="Courriel">Identifiant de connexion, toujours sous sa forme normalisée (voir <see cref="Courriel"/>).</param>
/// <param name="MotDePasseHash">Mot de passe haché ; le mot de passe en clair n'est jamais conservé.</param>
/// <param name="Actif">Un compte désactivé ne peut plus se connecter.</param>
/// <param name="DateCreation">Horodatage UTC.</param>
public sealed record Utilisateur(
    int Id,
    string Courriel,
    string NomAffiche,
    string MotDePasseHash,
    bool Actif,
    DateTime DateCreation);
