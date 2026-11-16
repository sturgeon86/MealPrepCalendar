using Repas.Domaine.Epicerie;

namespace Repas.Domaine.Tests.Epicerie;

public sealed class DiffusionListeTests
{
    private static readonly Guid EcranA = Guid.NewGuid();

    [Fact]
    public async Task TousLesAbonnesRecoiventLeChangement()
    {
        var diffusion = new DiffusionListe();
        var recus = new List<string>();
        using var premier = diffusion.Abonner(c => { lock (recus) { recus.Add($"1:{c.ArticleId}"); } return Task.CompletedTask; });
        using var second = diffusion.Abonner(c => { lock (recus) { recus.Add($"2:{c.ArticleId}"); } return Task.CompletedTask; });

        await diffusion.PublierAsync(new ChangementListe(TypeChangementListe.Coche, EcranA, ArticleId: 42, EstCoche: true));

        Assert.Equal(["1:42", "2:42"], recus.Order());
    }

    [Fact]
    public async Task UnAbonnementLibere_NeRecoitPlusRien()
    {
        var diffusion = new DiffusionListe();
        var recus = 0;
        var abonnement = diffusion.Abonner(_ => { recus++; return Task.CompletedTask; });

        abonnement.Dispose();
        abonnement.Dispose(); // sans effet la deuxième fois
        await diffusion.PublierAsync(ChangementListe.Contenu(EcranA));

        Assert.Equal(0, recus);
        Assert.Equal(0, diffusion.NombreAbonnes);
    }

    [Fact]
    public async Task UnAbonneEnErreur_NEmpechePasLesAutres_EtLErreurEstSignalee()
    {
        var erreurs = new List<Exception>();
        var diffusion = new DiffusionListe { SurErreur = erreurs.Add };
        var recu = false;
        using var enPanne = diffusion.Abonner(_ => throw new ObjectDisposedException("écran fermé"));
        using var correct = diffusion.Abonner(_ => { recu = true; return Task.CompletedTask; });

        await diffusion.PublierAsync(ChangementListe.Contenu(EcranA));

        Assert.True(recu);
        Assert.IsType<ObjectDisposedException>(Assert.Single(erreurs));
    }

    [Fact]
    public async Task SansAbonne_PublierNeFaitRien()
    {
        await new DiffusionListe().PublierAsync(ChangementListe.Contenu(EcranA));
    }

    [Fact]
    public async Task SeDesabonnerPendantUneDiffusion_NeLaPerturbePas()
    {
        var diffusion = new DiffusionListe();
        var recus = 0;
        IDisposable? abonnement = null;
        abonnement = diffusion.Abonner(_ => { abonnement!.Dispose(); recus++; return Task.CompletedTask; });
        using var autre = diffusion.Abonner(_ => { recus++; return Task.CompletedTask; });

        await diffusion.PublierAsync(ChangementListe.Contenu(EcranA));

        Assert.Equal(2, recus);
        Assert.Equal(1, diffusion.NombreAbonnes);
    }

    [Fact]
    public void Coche_PorteLArticleEtLAuteur()
    {
        var article = new ArticleListe(7, 102, "Lait 2 %", "Produits laitiers", 50, 2m, 6, "l", 1, false, false, false, new(5));

        var changement = ChangementListe.Coche(EcranA, article, estCoche: true, auteur: "Paul");

        Assert.Equal(
            new ChangementListe(TypeChangementListe.Coche, EcranA, 7, true, "Paul", "Lait 2 %"),
            changement);
    }
}
