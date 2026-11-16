using System.Net;
using System.Net.Sockets;

namespace Repas.Domaine.Import;

/// <summary>
/// Règles d'une adresse qu'on accepte de télécharger pour un import. Le serveur est dans la maison : il ne doit jamais
/// servir de relais vers le réseau local (routeur, Unraid, autres conteneurs), même si on lui colle une adresse qui
/// y mène directement ou par une redirection.
/// </summary>
public static class AdresseImport
{
    /// <summary>Longueur maximale d'une adresse gardée sur une recette (colonne <c>Recette.SourceUrl</c>).</summary>
    public const int LongueurMax = 2000;

    /// <summary>Vérifie une adresse saisie.</summary>
    /// <param name="adresse">L'adresse lue, si elle est acceptée.</param>
    /// <returns>Le problème, en phrase à afficher ; <c>null</c> si l'adresse est acceptée.</returns>
    public static string? Valider(string? texte, out Uri? adresse)
    {
        adresse = null;
        var nettoye = (texte ?? string.Empty).Trim();
        if (nettoye.Length == 0)
        {
            return "Collez l'adresse de la page de la recette.";
        }

        if (nettoye.Length > LongueurMax)
        {
            return $"L'adresse ne doit pas dépasser {LongueurMax} caractères.";
        }

        // « www.site.com/recette » sans « https:// » : on l'ajoute.
        if (!nettoye.Contains("://", StringComparison.Ordinal))
        {
            nettoye = "https://" + nettoye;
        }

        if (!Uri.TryCreate(nettoye, UriKind.Absolute, out var lue) || lue.Host.Length == 0)
        {
            return "Cette adresse n'est pas valide. Copiez-la depuis la barre d'adresse du navigateur.";
        }

        var probleme = VerifierUri(lue);
        if (probleme is null)
        {
            adresse = lue;
        }

        return probleme;
    }

    /// <summary>
    /// Vérifie une adresse à télécharger (saisie, photo ou redirection) : http ou https sur leur port habituel,
    /// sans identifiants, et jamais une adresse IP du réseau local écrite en toutes lettres.
    /// </summary>
    /// <returns>Le problème ; <c>null</c> si l'adresse est acceptée.</returns>
    public static string? VerifierUri(Uri adresse)
    {
        ArgumentNullException.ThrowIfNull(adresse);
        if (!adresse.IsAbsoluteUri || (adresse.Scheme != Uri.UriSchemeHttp && adresse.Scheme != Uri.UriSchemeHttps))
        {
            return "Seules les adresses http et https sont acceptées.";
        }

        if (!adresse.IsDefaultPort || adresse.UserInfo.Length > 0)
        {
            return "Cette adresse n'est pas acceptée : elle doit mener à un site public, sans port ni identifiants.";
        }

        var hote = adresse.IdnHost.Trim('[', ']').TrimEnd('.');
        if (hote.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || hote.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || hote.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || hote.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || hote.EndsWith(".home.arpa", StringComparison.OrdinalIgnoreCase)
            || hote.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || (!hote.Contains('.', StringComparison.Ordinal) && !IPAddress.TryParse(hote, out _)))
        {
            return MessageReseauLocal;
        }

        if (IPAddress.TryParse(hote, out var ip) && !EstPublique(ip))
        {
            return MessageReseauLocal;
        }

        return null;
    }

    /// <summary>Le message quand une adresse mène au réseau local.</summary>
    public const string MessageReseauLocal = "Les adresses du réseau local ne sont pas acceptées : seulement les sites publics.";

    /// <summary>
    /// Vrai pour une adresse IP joignable sur Internet ; faux pour le réseau local, la machine elle-même et les plages
    /// réservées. Vérifiée au moment de chaque connexion, après la résolution du nom, pour qu'un nom public qui pointe
    /// vers le réseau local (ou une redirection) soit refusé aussi.
    /// </summary>
    public static bool EstPublique(IPAddress ip)
    {
        ArgumentNullException.ThrowIfNull(ip);

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var o = ip.GetAddressBytes();
            return !(o[0] == 0                                         // 0.0.0.0/8 : « cette machine »
                     || o[0] == 10                                     // privé
                     || o[0] == 127                                    // boucle locale
                     || (o[0] == 100 && o[1] >= 64 && o[1] <= 127)     // 100.64.0.0/10 : NAT des fournisseurs
                     || (o[0] == 169 && o[1] == 254)                   // lien local (métadonnées infonuagiques)
                     || (o[0] == 172 && o[1] >= 16 && o[1] <= 31)      // privé
                     || (o[0] == 192 && o[1] == 0 && o[2] is 0 or 2)   // réservé, documentation
                     || (o[0] == 192 && o[1] == 168)                   // privé
                     || (o[0] == 198 && o[1] is 18 or 19)              // tests de performance
                     || (o[0] == 198 && o[1] == 51 && o[2] == 100)     // documentation
                     || (o[0] == 203 && o[1] == 0 && o[2] == 113)      // documentation
                     || o[0] >= 224);                                  // multidiffusion, réservé, diffusion
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var o = ip.GetAddressBytes();

            // 64:ff9b::/96 (NAT64) : l'adresse IPv4 est dans les quatre derniers octets.
            if (o[0] == 0x00 && o[1] == 0x64 && o[2] == 0xff && o[3] == 0x9b && o.Skip(4).Take(8).All(b => b == 0))
            {
                return EstPublique(new IPAddress(o[12..]));
            }

            return !(IPAddress.IPv6Loopback.Equals(ip)
                     || IPAddress.IPv6None.Equals(ip)
                     || ip.IsIPv6LinkLocal
                     || ip.IsIPv6SiteLocal
                     || ip.IsIPv6Multicast
                     || (o[0] & 0xfe) == 0xfc                            // fc00::/7 : adresses locales uniques
                     || o.Take(12).All(b => b == 0)                      // ::/96 : compatibles IPv4 et non spécifiée
                     || (o[0] == 0x20 && o[1] == 0x01 && o[2] == 0x0d && o[3] == 0xb8)); // documentation
        }

        return false;
    }
}
