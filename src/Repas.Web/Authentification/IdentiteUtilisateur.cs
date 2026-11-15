using System.Globalization;
using System.Security.Claims;

namespace Repas.Web.Authentification;

public static class IdentiteUtilisateur
{
    /// <summary>L'<c>Id</c> du compte connecté, ou <c>null</c> si personne n'est connecté.</summary>
    public static int? ObtenirIdUtilisateur(this ClaimsPrincipal principal)
    {
        var valeur = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(valeur, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }
}
