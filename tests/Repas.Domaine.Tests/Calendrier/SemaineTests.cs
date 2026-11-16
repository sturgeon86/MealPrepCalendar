using Repas.Domaine.Calendrier;

namespace Repas.Domaine.Tests.Calendrier;

public sealed class SemaineTests
{
    [Theory]
    [InlineData("2026-11-15", "2026-11-15")] // dimanche : premier jour lui-même
    [InlineData("2026-11-16", "2026-11-15")] // lundi
    [InlineData("2026-11-21", "2026-11-15")] // samedi : dernier jour
    [InlineData("2026-11-22", "2026-11-22")] // dimanche suivant
    [InlineData("2027-01-01", "2026-12-27")] // à cheval sur deux années
    public void Contenant_RameneAuDimanche(string date, string debutAttendu)
    {
        var semaine = Semaine.Contenant(DateOnly.Parse(date));

        Assert.Equal(DateOnly.Parse(debutAttendu), semaine.Debut);
        Assert.Equal(DayOfWeek.Sunday, semaine.Debut.DayOfWeek);
        Assert.Equal(semaine.Debut.AddDays(6), semaine.Fin);
    }

    [Fact]
    public void Jours_SeptJoursConsecutifsDuDimancheAuSamedi()
    {
        var jours = Semaine.Contenant(new DateOnly(2026, 11, 18)).Jours;

        Assert.Equal(7, jours.Count);
        Assert.Equal(DayOfWeek.Sunday, jours[0].DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, jours[6].DayOfWeek);
        Assert.All(jours.Zip(jours.Skip(1)), paire => Assert.Equal(paire.First.AddDays(1), paire.Second));
    }

    [Fact]
    public void PrecedenteEtSuivante_ReculentEtAvancentDeSeptJours()
    {
        var semaine = Semaine.Contenant(new DateOnly(2026, 11, 18));

        Assert.Equal(new DateOnly(2026, 11, 8), semaine.Precedente().Debut);
        Assert.Equal(new DateOnly(2026, 11, 22), semaine.Suivante().Debut);
        Assert.Equal(semaine, semaine.Suivante().Precedente());
    }

    [Fact]
    public void Contient_LesBornesSontIncluses()
    {
        var semaine = Semaine.Contenant(new DateOnly(2026, 11, 18));

        Assert.True(semaine.Contient(semaine.Debut));
        Assert.True(semaine.Contient(semaine.Fin));
        Assert.False(semaine.Contient(semaine.Debut.AddDays(-1)));
        Assert.False(semaine.Contient(semaine.Fin.AddDays(1)));
    }

    [Theory]
    [InlineData("2026-11-18", "15 au 21 novembre 2026")]
    [InlineData("2026-12-01", "29 novembre au 5 décembre 2026")]
    [InlineData("2027-01-01", "27 décembre 2026 au 2 janvier 2027")]
    [InlineData("2026-03-01", "1er au 7 mars 2026")]
    [InlineData("2026-07-28", "26 juillet au 1er août 2026")]
    public void Libelle_SansRepetitionInutile(string date, string attendu)
    {
        Assert.Equal(attendu, Semaine.Contenant(DateOnly.Parse(date)).Libelle);
    }

    [Theory]
    [InlineData("2026-11-16", "lundi 16 nov.")]
    [InlineData("2026-03-01", "dimanche 1er mars")]
    public void LibelleJour(string date, string attendu)
    {
        Assert.Equal(attendu, Semaine.LibelleJour(DateOnly.Parse(date)));
    }
}
