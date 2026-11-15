using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Repas.Domaine.Utilisateurs;
using Repas.Donnees;

namespace Repas.Web.Authentification;

/// <summary>Vérifie une tentative de connexion et construit l'identité placée dans le cookie.</summary>
public sealed class ServiceConnexion
{
    private readonly DepotUtilisateurs _depot;
    private readonly IHacheurMotDePasse _hacheur;

    public ServiceConnexion(DepotUtilisateurs depot, IHacheurMotDePasse hacheur)
    {
        _depot = depot;
        _hacheur = hacheur;
    }

    /// <returns>Le compte si la connexion est acceptée ; <c>null</c> pour tout échec, sans distinction.</returns>
    public async Task<Utilisateur?> VerifierAsync(
        string courriel, string motDePasse, CancellationToken annulation = default)
    {
        var utilisateur = await _depot.TrouverParCourrielAsync(courriel, annulation: annulation);

        var resultat = VerificationConnexion.Verifier(utilisateur, motDePasse, _hacheur);
        if (!resultat.Reussie || utilisateur is null)
        {
            return null;
        }

        if (resultat.NouveauHash is not null)
        {
            await _depot.ModifierMotDePasseHashAsync(utilisateur.Id, resultat.NouveauHash, annulation: annulation);
        }

        return utilisateur;
    }

    public static ClaimsPrincipal CreerPrincipal(Utilisateur utilisateur)
    {
        var identite = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, utilisateur.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, utilisateur.NomAffiche),
                new Claim(ClaimTypes.Email, utilisateur.Courriel),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identite);
    }
}
