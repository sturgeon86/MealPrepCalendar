using System.Collections.Concurrent;
using Repas.Donnees;

namespace Repas.Web.Authentification;

/// <summary>
/// Indique si un compte est encore actif, pour refuser les sessions d'un compte désactivé.
/// La réponse est gardée en mémoire quelques secondes : la vérification a lieu à chaque requête
/// authentifiée, et une lecture en base à chaque fois serait inutile.
/// </summary>
public sealed class SuiviComptesActifs
{
    private static readonly TimeSpan DureeDeValidite = TimeSpan.FromSeconds(30);

    private readonly DepotUtilisateurs _depot;
    private readonly ConcurrentDictionary<int, (bool Actif, long VerifieA)> _etats = new();

    public SuiviComptesActifs(DepotUtilisateurs depot)
    {
        _depot = depot;
    }

    public async Task<bool> EstActifAsync(int idUtilisateur, CancellationToken annulation = default)
    {
        if (_etats.TryGetValue(idUtilisateur, out var connu)
            && Environment.TickCount64 - connu.VerifieA < DureeDeValidite.TotalMilliseconds)
        {
            return connu.Actif;
        }

        var utilisateur = await _depot.TrouverParIdAsync(idUtilisateur, annulation: annulation);
        var actif = utilisateur?.Actif == true;
        Noter(idUtilisateur, actif);
        return actif;
    }

    /// <summary>Enregistre tout de suite un changement fait dans l'app, sans attendre l'expiration.</summary>
    public void Noter(int idUtilisateur, bool actif)
    {
        _etats[idUtilisateur] = (actif, Environment.TickCount64);
    }
}
