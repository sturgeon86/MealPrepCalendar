namespace Repas.Domaine.Utilisateurs;

/// <param name="Reussie">La personne peut être connectée.</param>
/// <param name="NouveauHash">Si non nul, hash à enregistrer à la place de l'ancien.</param>
public sealed record ResultatConnexion(bool Reussie, string? NouveauHash = null)
{
    public static readonly ResultatConnexion Echec = new(false);
}

public static class VerificationConnexion
{
    /// <summary>
    /// Décide si une tentative de connexion est acceptée. Compte inconnu, compte désactivé et
    /// mauvais mot de passe donnent tous le même échec, pour ne rien révéler sur les comptes existants.
    /// </summary>
    /// <param name="utilisateur">Le compte trouvé pour le courriel saisi, ou <c>null</c>.</param>
    public static ResultatConnexion Verifier(
        Utilisateur? utilisateur, string motDePasse, IHacheurMotDePasse hacheur)
    {
        ArgumentNullException.ThrowIfNull(motDePasse);
        ArgumentNullException.ThrowIfNull(hacheur);

        if (utilisateur is null)
        {
            // Même travail de hachage que pour un compte existant : le temps de réponse
            // ne permet pas de deviner si le courriel correspond à un compte.
            hacheur.Hacher(motDePasse);
            return ResultatConnexion.Echec;
        }

        var verification = hacheur.Verifier(utilisateur.MotDePasseHash, motDePasse);
        if (verification == ResultatVerificationMotDePasse.Echec || !utilisateur.Actif)
        {
            return ResultatConnexion.Echec;
        }

        return verification == ResultatVerificationMotDePasse.ReussiteARehacher
            ? new ResultatConnexion(true, hacheur.Hacher(motDePasse))
            : new ResultatConnexion(true);
    }
}
