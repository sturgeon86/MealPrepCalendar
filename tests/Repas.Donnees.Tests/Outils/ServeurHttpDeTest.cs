using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Repas.Donnees.Tests.Outils;

/// <summary>Un tout petit serveur HTTP sur la boucle locale, pour tester les téléchargements sans Internet.</summary>
internal sealed class ServeurHttpDeTest : IAsyncDisposable
{
    private readonly TcpListener _ecoute = new(IPAddress.Loopback, 0);
    private readonly Dictionary<string, Reponse> _reponses = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _arret = new();
    private readonly Task _boucle;

    public ServeurHttpDeTest()
    {
        _ecoute.Start();
        _boucle = Task.Run(ServirAsync);
    }

    public int Port => ((IPEndPoint)_ecoute.LocalEndpoint).Port;

    public Uri Adresse(string chemin) => new($"http://127.0.0.1:{Port}{chemin}");

    public void Repondre(string chemin, int code, string type, byte[] corps, string? location = null) =>
        _reponses[chemin] = new Reponse(code, type, corps, location);

    public void Repondre(string chemin, string html) => Repondre(chemin, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));

    private async Task ServirAsync()
    {
        while (!_arret.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _ecoute.AcceptTcpClientAsync(_arret.Token);
            }
            catch (Exception) when (_arret.IsCancellationRequested)
            {
                return;
            }

            _ = Task.Run(() => RepondreAsync(client));
        }
    }

    private async Task RepondreAsync(TcpClient client)
    {
        using (client)
        {
            var flux = client.GetStream();
            var lecteur = new StreamReader(flux, Encoding.ASCII, leaveOpen: true);
            var ligne = await lecteur.ReadLineAsync() ?? string.Empty;
            while (!string.IsNullOrEmpty(await lecteur.ReadLineAsync()))
            {
                // Les en-têtes de la requête ne servent pas.
            }

            var chemin = ligne.Split(' ') is { Length: >= 2 } parties ? parties[1] : "/";
            var reponse = _reponses.GetValueOrDefault(chemin) ?? new Reponse(404, "text/plain", "Introuvable"u8.ToArray(), null);
            var entete = new StringBuilder()
                .Append($"HTTP/1.1 {reponse.Code} X\r\n")
                .Append($"Content-Type: {reponse.Type}\r\n")
                .Append($"Content-Length: {reponse.Corps.Length}\r\n")
                .Append(reponse.Location is null ? string.Empty : $"Location: {reponse.Location}\r\n")
                .Append("Connection: close\r\n\r\n");
            try
            {
                await flux.WriteAsync(Encoding.ASCII.GetBytes(entete.ToString()));
                await flux.WriteAsync(reponse.Corps);
            }
            catch (IOException)
            {
                // Le client a abandonné (fichier trop gros) : rien à faire.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _arret.CancelAsync();
        _ecoute.Stop();
        try
        {
            await _boucle;
        }
        catch (OperationCanceledException)
        {
        }

        _arret.Dispose();
    }

    private sealed record Reponse(int Code, string Type, byte[] Corps, string? Location);
}
