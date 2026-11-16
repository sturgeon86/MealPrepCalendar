namespace Repas.Domaine.Epicerie;

/// <summary>Ce qui a changé dans la liste active.</summary>
public enum TypeChangementListe
{
    /// <summary>Un article a été coché ou décoché : les écrans ouverts peuvent l'appliquer directement.</summary>
    Coche,

    /// <summary>Les articles ont changé (génération, regénération, ajout, retrait) : les écrans relisent la liste.</summary>
    Contenu,
}

/// <summary>Un changement de la liste, annoncé aux autres écrans ouverts.</summary>
/// <param name="Origine">L'écran qui a fait le changement ; il l'a déjà appliqué et l'ignore.</param>
/// <param name="Auteur">Le nom de la personne, pour « Paul a pris Lait 2 % ».</param>
public sealed record ChangementListe(
    TypeChangementListe Type,
    Guid Origine,
    int? ArticleId = null,
    bool? EstCoche = null,
    string? Auteur = null,
    string? NomArticle = null)
{
    public static ChangementListe Coche(Guid origine, ArticleListe article, bool estCoche, string? auteur) =>
        new(TypeChangementListe.Coche, origine, article.Id, estCoche, auteur, article.NomIngredient);

    public static ChangementListe Contenu(Guid origine, string? auteur = null) =>
        new(TypeChangementListe.Contenu, origine, Auteur: auteur);
}

/// <summary>
/// Annonce les changements de la liste d'épicerie à tous les écrans ouverts, pour qu'un article coché sur un
/// téléphone apparaisse coché sur les autres. L'app tourne en un seul exemplaire (un conteneur) : la diffusion
/// se fait en mémoire, et chaque écran Blazor Server reçoit l'avis par sa connexion déjà ouverte.
/// </summary>
public sealed class DiffusionListe
{
    private readonly Lock _verrou = new();
    private List<Func<ChangementListe, Task>> _abonnes = [];

    /// <summary>Appelé quand un abonné échoue (écran fermé en plein envoi, par exemple) ; les autres reçoivent quand même l'avis.</summary>
    public Action<Exception>? SurErreur { get; set; }

    /// <summary>Nombre d'écrans abonnés, pour le diagnostic et les tests.</summary>
    public int NombreAbonnes
    {
        get
        {
            lock (_verrou)
            {
                return _abonnes.Count;
            }
        }
    }

    /// <summary>Reçoit chaque changement jusqu'à ce que l'abonnement soit libéré (à la fermeture de l'écran).</summary>
    public IDisposable Abonner(Func<ChangementListe, Task> abonne)
    {
        ArgumentNullException.ThrowIfNull(abonne);
        lock (_verrou)
        {
            // Une nouvelle liste à chaque changement : une diffusion en cours parcourt sa copie sans verrou.
            _abonnes = [.. _abonnes, abonne];
        }

        return new Abonnement(this, abonne);
    }

    /// <summary>
    /// Annonce le changement à tous les abonnés, en parallèle, et attend qu'ils l'aient traité.
    /// Un abonné en erreur n'empêche pas les autres de le recevoir.
    /// </summary>
    public Task PublierAsync(ChangementListe changement)
    {
        ArgumentNullException.ThrowIfNull(changement);

        List<Func<ChangementListe, Task>> abonnes;
        lock (_verrou)
        {
            abonnes = _abonnes;
        }

        return Task.WhenAll(abonnes.Select(async abonne =>
        {
            try
            {
                await abonne(changement);
            }
            catch (Exception ex)
            {
                SurErreur?.Invoke(ex);
            }
        }));
    }

    private void Desabonner(Func<ChangementListe, Task> abonne)
    {
        lock (_verrou)
        {
            _abonnes = _abonnes.Where(a => a != abonne).ToList();
        }
    }

    private sealed class Abonnement(DiffusionListe diffusion, Func<ChangementListe, Task> abonne) : IDisposable
    {
        private int _libere;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _libere, 1) == 0)
            {
                diffusion.Desabonner(abonne);
            }
        }
    }
}
