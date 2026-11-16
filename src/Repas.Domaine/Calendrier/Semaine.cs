using System.Globalization;

namespace Repas.Domaine.Calendrier;

/// <summary>
/// Une semaine du calendrier : sept jours, du dimanche au samedi, comme les calendriers au Québec.
/// </summary>
public readonly record struct Semaine
{
    /// <summary>Premier jour de la semaine affichée. Changer cette valeur suffit à commencer un autre jour.</summary>
    public const DayOfWeek PremierJour = DayOfWeek.Sunday;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-CA");

    private Semaine(DateOnly debut) => Debut = debut;

    /// <summary>Le premier jour de la semaine.</summary>
    public DateOnly Debut { get; }

    /// <summary>Le dernier jour, inclus.</summary>
    public DateOnly Fin => Debut.AddDays(6);

    /// <summary>Les sept jours, dans l'ordre.</summary>
    public IReadOnlyList<DateOnly> Jours => Enumerable.Range(0, 7).Select(Debut.AddDays).ToList();

    /// <summary>La semaine qui contient cette date.</summary>
    public static Semaine Contenant(DateOnly date)
    {
        var ecart = ((int)date.DayOfWeek - (int)PremierJour + 7) % 7;
        return new Semaine(date.AddDays(-ecart));
    }

    public Semaine Precedente() => new(Debut.AddDays(-7));

    public Semaine Suivante() => new(Debut.AddDays(7));

    public bool Contient(DateOnly date) => date >= Debut && date <= Fin;

    /// <summary>
    /// La période en toutes lettres, sans répéter le mois ou l'année inutilement :
    /// « 15 au 21 novembre 2026 », « 29 novembre au 5 décembre 2026 », « 27 décembre 2026 au 2 janvier 2027 ».
    /// </summary>
    public string Libelle
    {
        get
        {
            var fin = Fin.ToString("d MMMM yyyy", Culture);
            var debut = Debut.Year != Fin.Year ? Debut.ToString("d MMMM yyyy", Culture)
                : Debut.Month != Fin.Month ? Debut.ToString("d MMMM", Culture)
                : Debut.Day.ToString(Culture);
            return $"{PremierDuMois(debut, Debut)} au {PremierDuMois(fin, Fin)}";
        }
    }

    /// <summary>Nom du jour et date courte pour l'en-tête d'une colonne : « lundi 16 nov. ».</summary>
    public static string LibelleJour(DateOnly jour) =>
        $"{jour.ToString("dddd", Culture)} {PremierDuMois(jour.ToString("d MMM", Culture), jour)}";

    /// <summary>En français, le premier jour du mois s'écrit « 1er ».</summary>
    private static string PremierDuMois(string texte, DateOnly date) =>
        date.Day == 1 && texte.StartsWith("1 ", StringComparison.Ordinal) ? "1er" + texte[1..]
        : date.Day == 1 && texte == "1" ? "1er"
        : texte;
}
