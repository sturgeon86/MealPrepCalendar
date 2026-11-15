namespace Repas.Domaine.Utilisateurs;

/// <summary>Hachage des mots de passe. L'implémentation réelle est fournie par l'application web.</summary>
public interface IHacheurMotDePasse
{
    string Hacher(string motDePasse);

    ResultatVerificationMotDePasse Verifier(string hash, string motDePasse);
}

public enum ResultatVerificationMotDePasse
{
    Echec,
    Reussite,

    /// <summary>Le mot de passe est bon, mais le hash utilise d'anciens réglages et doit être recalculé.</summary>
    ReussiteARehacher,
}
