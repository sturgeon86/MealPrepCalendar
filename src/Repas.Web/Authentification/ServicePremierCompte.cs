using Repas.Domaine.Utilisateurs;
using Repas.Donnees;

namespace Repas.Web.Authentification;

/// <summary>Création du tout premier compte, possible seulement tant que l'app n'en a aucun.</summary>
public sealed class ServicePremierCompte
{
    private readonly DepotUtilisateurs _depot;
    private readonly IHacheurMotDePasse _hacheur;

    // Un compte n'est jamais supprimé (seulement désactivé) : une fois vrai, c'est définitif,
    // et la page de connexion n'a plus à interroger la base à chaque affichage.
    private volatile bool _unCompteExiste;

    public ServicePremierCompte(DepotUtilisateurs depot, IHacheurMotDePasse hacheur)
    {
        _depot = depot;
        _hacheur = hacheur;
    }

    /// <summary>Vrai tant qu'aucun compte n'existe : l'app doit proposer de créer le premier.</summary>
    public async Task<bool> EstRequisAsync(CancellationToken annulation = default)
    {
        if (_unCompteExiste)
        {
            return false;
        }

        if (await _depot.CompterAsync(annulation: annulation) > 0)
        {
            _unCompteExiste = true;
            return false;
        }

        return true;
    }

    /// <returns>Le compte créé, ou <c>null</c> si un compte existait déjà (rien n'est alors créé).</returns>
    public async Task<Utilisateur?> CreerAsync(
        string nomAffiche, string courriel, string motDePasse, CancellationToken annulation = default)
    {
        var cree = await _depot.AjouterPremierAsync(
            courriel, nomAffiche.Trim(), _hacheur.Hacher(motDePasse), annulation: annulation);

        // Créé à l'instant ou déjà présent : dans les deux cas, un compte existe maintenant.
        _unCompteExiste = true;
        return cree;
    }
}
