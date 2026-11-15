namespace Repas.Domaine.Utilisateurs;

public static class Courriel
{
    /// <summary>Longueur maximale d'une adresse courriel (colonne <c>Utilisateur.Courriel</c>).</summary>
    public const int LongueurMax = 254;

    /// <summary>
    /// Forme sous laquelle un courriel est enregistré et comparé : sans espaces autour, en minuscules.
    /// Ainsi « Marie@Exemple.com » et « marie@exemple.com » désignent le même compte.
    /// </summary>
    public static string Normaliser(string courriel)
    {
        ArgumentNullException.ThrowIfNull(courriel);
        return courriel.Trim().ToLowerInvariant();
    }
}
