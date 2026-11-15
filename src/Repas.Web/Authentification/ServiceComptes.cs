using Repas.Domaine.Utilisateurs;
using Repas.Donnees;

namespace Repas.Web.Authentification;

/// <summary>Gestion des comptes par une personne connectée.</summary>
public sealed class ServiceComptes
{
    private readonly DepotUtilisateurs _depot;
    private readonly IHacheurMotDePasse _hacheur;
    private readonly SuiviComptesActifs _suivi;

    public ServiceComptes(DepotUtilisateurs depot, IHacheurMotDePasse hacheur, SuiviComptesActifs suivi)
    {
        _depot = depot;
        _hacheur = hacheur;
        _suivi = suivi;
    }

    public Task<List<Utilisateur>> ListerAsync(CancellationToken annulation = default)
    {
        return _depot.ListerAsync(annulation: annulation);
    }

    public Task<Utilisateur?> TrouverAsync(int id, CancellationToken annulation = default)
    {
        return _depot.TrouverParIdAsync(id, annulation: annulation);
    }

    /// <exception cref="CourrielDejaUtiliseException">Un compte existe déjà avec ce courriel.</exception>
    public Task<Utilisateur> CreerAsync(
        string nomAffiche, string courriel, string motDePasse, CancellationToken annulation = default)
    {
        return _depot.AjouterAsync(courriel, nomAffiche.Trim(), _hacheur.Hacher(motDePasse), annulation: annulation);
    }

    /// <returns><c>false</c> si le mot de passe actuel est incorrect ; rien n'est alors changé.</returns>
    public async Task<bool> ChangerMotDePasseAsync(
        int idUtilisateur, string motDePasseActuel, string nouveauMotDePasse, CancellationToken annulation = default)
    {
        var utilisateur = await _depot.TrouverParIdAsync(idUtilisateur, annulation: annulation);
        if (utilisateur is null || !VerificationConnexion.Verifier(utilisateur, motDePasseActuel, _hacheur).Reussie)
        {
            return false;
        }

        return await _depot.ModifierMotDePasseHashAsync(
            idUtilisateur, _hacheur.Hacher(nouveauMotDePasse), annulation: annulation);
    }

    /// <returns><c>false</c> si le changement est refusé (son propre compte) ou si le compte n'existe pas.</returns>
    public async Task<bool> ModifierActifAsync(
        int idDemandeur, int idCible, bool actif, CancellationToken annulation = default)
    {
        if (!GestionComptes.PeutModifierActif(idDemandeur, idCible))
        {
            return false;
        }

        var modifie = await _depot.ModifierActifAsync(idCible, actif, annulation: annulation);
        if (modifie)
        {
            // Les sessions ouvertes du compte sont refusées dès la prochaine vérification.
            _suivi.Noter(idCible, actif);
        }

        return modifie;
    }
}
