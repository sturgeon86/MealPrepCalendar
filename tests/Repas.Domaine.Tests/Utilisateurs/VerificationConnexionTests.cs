using Repas.Domaine.Utilisateurs;

namespace Repas.Domaine.Tests.Utilisateurs;

public sealed class VerificationConnexionTests
{
    private const string BonMotDePasse = "bon-mot-de-passe";

    private static Utilisateur CreerUtilisateur(bool actif = true, string? hash = null) => new(
        Id: 1,
        Courriel: "marie@exemple.com",
        NomAffiche: "Marie",
        MotDePasseHash: hash ?? FauxHacheur.HashDe(BonMotDePasse),
        Actif: actif,
        DateCreation: new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void BonMotDePasse_CompteActif_Reussit()
    {
        var resultat = VerificationConnexion.Verifier(CreerUtilisateur(), BonMotDePasse, new FauxHacheur());

        Assert.True(resultat.Reussie);
        Assert.Null(resultat.NouveauHash);
    }

    [Fact]
    public void MauvaisMotDePasse_Echoue()
    {
        var resultat = VerificationConnexion.Verifier(CreerUtilisateur(), "mauvais", new FauxHacheur());

        Assert.False(resultat.Reussie);
    }

    [Fact]
    public void CompteInconnu_Echoue_MaisHacheQuandMeme()
    {
        var hacheur = new FauxHacheur();

        var resultat = VerificationConnexion.Verifier(null, BonMotDePasse, hacheur);

        Assert.False(resultat.Reussie);
        Assert.Equal(1, hacheur.NombreDeHachages);
    }

    [Fact]
    public void CompteDesactive_Echoue_MemeAvecLeBonMotDePasse()
    {
        var resultat = VerificationConnexion.Verifier(CreerUtilisateur(actif: false), BonMotDePasse, new FauxHacheur());

        Assert.False(resultat.Reussie);
        Assert.Null(resultat.NouveauHash);
    }

    [Fact]
    public void LesTroisEchecsSontIndiscernables()
    {
        var hacheur = new FauxHacheur();

        var inconnu = VerificationConnexion.Verifier(null, BonMotDePasse, hacheur);
        var desactive = VerificationConnexion.Verifier(CreerUtilisateur(actif: false), BonMotDePasse, hacheur);
        var mauvais = VerificationConnexion.Verifier(CreerUtilisateur(), "mauvais", hacheur);

        Assert.Equal(inconnu, desactive);
        Assert.Equal(inconnu, mauvais);
    }

    [Fact]
    public void HashAncien_ReussitEtProposeUnNouveauHash()
    {
        var utilisateur = CreerUtilisateur(hash: FauxHacheur.AncienHashDe(BonMotDePasse));

        var resultat = VerificationConnexion.Verifier(utilisateur, BonMotDePasse, new FauxHacheur());

        Assert.True(resultat.Reussie);
        Assert.Equal(FauxHacheur.HashDe(BonMotDePasse), resultat.NouveauHash);
    }

    [Fact]
    public void HashAncien_MauvaisMotDePasse_Echoue()
    {
        var utilisateur = CreerUtilisateur(hash: FauxHacheur.AncienHashDe(BonMotDePasse));

        var resultat = VerificationConnexion.Verifier(utilisateur, "mauvais", new FauxHacheur());

        Assert.False(resultat.Reussie);
    }

    /// <summary>Hacheur transparent pour les tests : le « hash » est le mot de passe préfixé.</summary>
    private sealed class FauxHacheur : IHacheurMotDePasse
    {
        public int NombreDeHachages { get; private set; }

        public static string HashDe(string motDePasse) => "v2:" + motDePasse;

        public static string AncienHashDe(string motDePasse) => "v1:" + motDePasse;

        public string Hacher(string motDePasse)
        {
            NombreDeHachages++;
            return HashDe(motDePasse);
        }

        public ResultatVerificationMotDePasse Verifier(string hash, string motDePasse)
        {
            if (hash == HashDe(motDePasse))
            {
                return ResultatVerificationMotDePasse.Reussite;
            }

            return hash == AncienHashDe(motDePasse)
                ? ResultatVerificationMotDePasse.ReussiteARehacher
                : ResultatVerificationMotDePasse.Echec;
        }
    }
}
