using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Repas.Domaine.Import;
using Repas.Domaine.Recettes;

namespace Repas.Donnees.Web;

/// <summary>Un téléchargement refusé ou raté, avec un message à afficher.</summary>
public sealed class ImportWebException(string message, Exception? cause = null) : Exception(message, cause);

/// <summary>Une page web lue, avec son adresse finale (après les redirections).</summary>
public sealed record PageWeb(string Html, Uri Adresse);

/// <summary>
/// Télécharge une page de recette et sa photo, en se protégeant : seulement http et https, un délai et une taille
/// maximale, et jamais une adresse du réseau local. Le nom est résolu puis chaque adresse IP est vérifiée au moment
/// de la connexion, ce qui couvre aussi les redirections et les noms publics qui pointent vers la maison.
/// </summary>
public sealed class TelechargementWeb : IDisposable
{
    /// <summary>Délai maximal pour un téléchargement complet (en-têtes et contenu).</summary>
    public static readonly TimeSpan Delai = TimeSpan.FromSeconds(20);

    private const int RedirectionsMax = 5;

    private readonly HttpClient _client;
    private readonly bool _verifierAdresses;

    public TelechargementWeb() : this(AdresseImport.EstPublique, verifierAdresses: true)
    {
    }

    /// <param name="ipAutorisee">Les adresses IP auxquelles on accepte de se connecter.</param>
    /// <param name="verifierAdresses">Faux seulement pour les tests, qui servent des pages depuis la machine.</param>
    internal TelechargementWeb(Func<IPAddress, bool> ipAutorisee, bool verifierAdresses)
    {
        _verifierAdresses = verifierAdresses;
        var gestionnaire = new SocketsHttpHandler
        {
            // Un mandataire recevrait la connexion à notre place et ferait la requête sans vérification.
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = async (contexte, annulation) =>
            {
                var adresses = await Dns.GetHostAddressesAsync(contexte.DnsEndPoint.Host, annulation);
                if (adresses.Length == 0 || !adresses.All(ipAutorisee))
                {
                    throw new ImportWebException(AdresseImport.MessageReseauLocal);
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(adresses, contexte.DnsEndPoint.Port, annulation);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };

        _client = new HttpClient(gestionnaire) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PlanificateurRepas/1.0)");
        _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("fr-CA,fr;q=0.9,en;q=0.5");
    }

    /// <summary>Télécharge une page HTML.</summary>
    /// <exception cref="ImportWebException">Adresse refusée, page introuvable, trop lente ou trop grosse.</exception>
    public async Task<PageWeb> LirePageAsync(Uri adresse, CancellationToken annulation = default)
    {
        var (contenu, type, finale) = await TelechargerAsync(adresse, LectureRecetteWeb.TaillePageMax, "text/html", annulation);
        if (type?.MediaType is { } media && !media.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImportWebException("Cette adresse ne mène pas à une page web, mais à un autre genre de fichier.");
        }

        Encoding encodage;
        try
        {
            encodage = string.IsNullOrWhiteSpace(type?.CharSet) ? Encoding.UTF8 : Encoding.GetEncoding(type.CharSet.Trim('"'));
        }
        catch (ArgumentException)
        {
            encodage = Encoding.UTF8;
        }

        return new PageWeb(encodage.GetString(contenu), finale);
    }

    /// <summary>Télécharge la photo d'une recette (JPEG, PNG ou WebP, au plus la taille permise d'une photo).</summary>
    /// <exception cref="ImportWebException">Adresse refusée, photo introuvable, trop lente, trop grosse ou illisible.</exception>
    public async Task<byte[]> LireImageAsync(Uri adresse, CancellationToken annulation = default)
    {
        var (contenu, _, _) = await TelechargerAsync(adresse, PhotoRecette.TailleMaxOctets, "image/*", annulation);
        try
        {
            PhotoRecette.Verifier(contenu);
        }
        catch (PhotoInvalideException ex)
        {
            throw new ImportWebException(ex.Message, ex);
        }

        return contenu;
    }

    private async Task<(byte[] Contenu, MediaTypeHeaderValue? Type, Uri Finale)> TelechargerAsync(
        Uri adresse, int tailleMax, string accepte, CancellationToken annulation)
    {
        using var delai = CancellationTokenSource.CreateLinkedTokenSource(annulation);
        delai.CancelAfter(Delai);
        try
        {
            var courante = adresse;
            for (var redirections = 0; ; redirections++)
            {
                Verifier(courante);
                using var requete = new HttpRequestMessage(HttpMethod.Get, courante);
                requete.Headers.Accept.ParseAdd(accepte);
                using var reponse = await _client.SendAsync(requete, HttpCompletionOption.ResponseHeadersRead, delai.Token);

                // Les redirections sont suivies ici, pour que chaque nouvelle adresse passe par les mêmes vérifications.
                if ((int)reponse.StatusCode is >= 300 and < 400 && reponse.Headers.Location is { } location)
                {
                    if (redirections >= RedirectionsMax)
                    {
                        throw new ImportWebException("La page redirige trop de fois.");
                    }

                    courante = new Uri(courante, location);
                    continue;
                }

                if (!reponse.IsSuccessStatusCode)
                {
                    throw new ImportWebException(reponse.StatusCode switch
                    {
                        HttpStatusCode.NotFound or HttpStatusCode.Gone => "Cette page n'existe pas ou plus (erreur 404).",
                        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                            "Le site refuse que l'app lise cette page. Copiez plutôt le texte de la recette.",
                        _ => $"Le site a répondu par une erreur ({(int)reponse.StatusCode}). Réessayez plus tard.",
                    });
                }

                if (reponse.Content.Headers.ContentLength > tailleMax)
                {
                    throw TropGros(tailleMax);
                }

                await using var flux = await reponse.Content.ReadAsStreamAsync(delai.Token);
                using var memoire = new MemoryStream();
                var tampon = new byte[81920];
                int lus;
                while ((lus = await flux.ReadAsync(tampon, delai.Token)) > 0)
                {
                    if (memoire.Length + lus > tailleMax)
                    {
                        throw TropGros(tailleMax);
                    }

                    memoire.Write(tampon, 0, lus);
                }

                return (memoire.ToArray(), reponse.Content.Headers.ContentType, courante);
            }
        }
        catch (OperationCanceledException) when (!annulation.IsCancellationRequested)
        {
            throw new ImportWebException("Le site a mis trop de temps à répondre. Réessayez plus tard.");
        }
        catch (HttpRequestException ex) when (ex.InnerException is ImportWebException refus)
        {
            throw new ImportWebException(refus.Message, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ImportWebException("Le site n'a pas pu être joint. Vérifiez l'adresse.", ex);
        }
    }

    private void Verifier(Uri adresse)
    {
        if (adresse.Scheme != Uri.UriSchemeHttp && adresse.Scheme != Uri.UriSchemeHttps)
        {
            throw new ImportWebException("Seules les adresses http et https sont acceptées.");
        }

        if (_verifierAdresses && AdresseImport.VerifierUri(adresse) is { } probleme)
        {
            throw new ImportWebException(probleme);
        }
    }

    private static ImportWebException TropGros(int tailleMax) =>
        new($"Le fichier dépasse la taille permise ({tailleMax / (1024 * 1024)} Mo).");

    public void Dispose() => _client.Dispose();
}
