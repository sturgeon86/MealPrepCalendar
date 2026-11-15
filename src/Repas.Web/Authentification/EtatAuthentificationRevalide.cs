using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Repas.Web.Authentification;

/// <summary>
/// Dans une page interactive, la connexion temps réel reste ouverte sans repasser par le cookie.
/// Ce fournisseur revérifie donc périodiquement que le compte est toujours actif ; sinon,
/// la personne redevient anonyme et la page la renvoie à la connexion.
/// </summary>
public sealed class EtatAuthentificationRevalide : RevalidatingServerAuthenticationStateProvider
{
    private readonly SuiviComptesActifs _suivi;

    public EtatAuthentificationRevalide(ILoggerFactory fabriqueJournaux, SuiviComptesActifs suivi)
        : base(fabriqueJournaux)
    {
        _suivi = suivi;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var id = authenticationState.User.ObtenirIdUtilisateur();
        return id is not null && await _suivi.EstActifAsync(id.Value, cancellationToken);
    }
}
