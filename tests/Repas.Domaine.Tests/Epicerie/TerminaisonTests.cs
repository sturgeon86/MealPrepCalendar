using Repas.Domaine.Epicerie;
using Repas.Domaine.Mesures;

namespace Repas.Domaine.Tests.Epicerie;

public sealed class TerminaisonTests
{
    private static ArticleListe Article(int id, bool coche) =>
        new(id, 100 + id, $"Article {id}", "Divers", 10, null, null, null, 1, false, false, coche, new MesuresIngredient(1));

    private static readonly ArticleListe[] Liste = [Article(1, coche: true), Article(2, coche: false), Article(3, coche: false), Article(4, coche: true)];

    [Fact]
    public void NonAchetes_SontLesArticlesNonCoches()
    {
        Assert.Equal([2, 3], Terminaison.NonAchetes(Liste).Select(a => a.Id));
    }

    [Fact]
    public void ArticlesAReporter_TousLesNonAchetes()
    {
        Assert.Equal([2, 3], Terminaison.ArticlesAReporter(Liste, [2, 3]).Select(a => a.Id));
    }

    [Fact]
    public void ArticlesAReporter_UnParUn()
    {
        Assert.Equal([3], Terminaison.ArticlesAReporter(Liste, [3]).Select(a => a.Id));
    }

    [Fact]
    public void ArticlesAReporter_IgnoreLesArticlesCochesEtInconnus()
    {
        // 1 a été coché entre-temps sur un autre téléphone ; 99 n'est pas sur la liste.
        Assert.Equal([2], Terminaison.ArticlesAReporter(Liste, [1, 2, 99]).Select(a => a.Id));
    }

    [Fact]
    public void ArticlesAReporter_AucunChoix_RienAReporter()
    {
        Assert.Empty(Terminaison.ArticlesAReporter(Liste, []));
    }

    [Fact]
    public void ArticlesAReporter_GardeLOrdreDeLaListe()
    {
        Assert.Equal([2, 3], Terminaison.ArticlesAReporter(Liste, [3, 2]).Select(a => a.Id));
    }
}
