using Microsoft.AspNetCore.Identity;
using Repas.Domaine.Utilisateurs;

namespace Repas.Web.Authentification;

/// <summary>Hachage par <see cref="PasswordHasher{TUser}"/> d'ASP.NET Core (PBKDF2, sel aléatoire par mot de passe).</summary>
public sealed class HacheurMotDePasse : IHacheurMotDePasse
{
    // PasswordHasher n'utilise pas l'utilisateur reçu en paramètre : on lui passe null.
    private readonly PasswordHasher<Utilisateur> _hacheur = new();

    public string Hacher(string motDePasse) => _hacheur.HashPassword(null!, motDePasse);

    public ResultatVerificationMotDePasse Verifier(string hash, string motDePasse)
    {
        PasswordVerificationResult resultat;
        try
        {
            resultat = _hacheur.VerifyHashedPassword(null!, hash, motDePasse);
        }
        catch (FormatException)
        {
            // Hash illisible dans la base : traité comme un mauvais mot de passe.
            return ResultatVerificationMotDePasse.Echec;
        }

        return resultat switch
        {
            PasswordVerificationResult.Success => ResultatVerificationMotDePasse.Reussite,
            PasswordVerificationResult.SuccessRehashNeeded => ResultatVerificationMotDePasse.ReussiteARehacher,
            _ => ResultatVerificationMotDePasse.Echec,
        };
    }
}
