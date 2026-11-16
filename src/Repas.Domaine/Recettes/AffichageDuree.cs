namespace Repas.Domaine.Recettes;

/// <summary>Durées de recette écrites pour la lecture : « 45 min », « 2 h », « 1 h 05 ».</summary>
public static class AffichageDuree
{
    /// <summary>Espaces insécables : « 1 h 25 » ne se coupe jamais en fin de ligne.</summary>
    public static string Ecrire(int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);

        return minutes switch
        {
            < 60 => $"{minutes} min",
            _ when minutes % 60 == 0 => $"{minutes / 60} h",
            _ => $"{minutes / 60} h {minutes % 60:00}",
        };
    }
}
