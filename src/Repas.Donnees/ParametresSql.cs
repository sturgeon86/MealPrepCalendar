using System.Data;
using Microsoft.Data.SqlClient;

namespace Repas.Donnees;

/// <summary>Raccourcis pour déclarer les paramètres d'une requête, avec leur type SQL explicite.</summary>
public static class ParametresSql
{
    /// <summary>Ajoute un paramètre ; une valeur <c>null</c> est envoyée comme NULL.</summary>
    public static SqlParameterCollection Ajouter(
        this SqlParameterCollection parametres, string nom, SqlDbType type, object? valeur)
    {
        parametres.Add(nom, type).Value = valeur ?? DBNull.Value;
        return parametres;
    }

    /// <summary>Ajoute un paramètre de longueur fixée (NVARCHAR, VARCHAR…) ; <c>-1</c> pour MAX.</summary>
    public static SqlParameterCollection Ajouter(
        this SqlParameterCollection parametres, string nom, SqlDbType type, int taille, object? valeur)
    {
        parametres.Add(nom, type, taille).Value = valeur ?? DBNull.Value;
        return parametres;
    }

    /// <summary>Ajoute un paramètre DECIMAL avec sa précision et son échelle (ex. : 18, 4 pour une quantité).</summary>
    public static SqlParameterCollection AjouterDecimal(
        this SqlParameterCollection parametres, string nom, byte precision, byte echelle, decimal? valeur)
    {
        var parametre = parametres.Add(nom, SqlDbType.Decimal);
        parametre.Precision = precision;
        parametre.Scale = echelle;
        parametre.Value = valeur ?? (object)DBNull.Value;
        return parametres;
    }
}
