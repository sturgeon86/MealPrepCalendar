using Microsoft.Data.SqlClient;
using Repas.Domaine.Mesures;

namespace Repas.Donnees;

/// <summary>Accès à la table <c>Unite</c>.</summary>
public sealed class DepotUnites : DepotBase
{
    public DepotUnites(BaseDeDonnees baseDeDonnees) : base(baseDeDonnees)
    {
    }

    /// <summary>Toutes les unités, par type de mesure puis de la plus petite à la plus grande.</summary>
    public Task<List<Unite>> ListerAsync(SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            SELECT Id, Code, TypeMesure, FacteurVersReference
            FROM dbo.Unite
            ORDER BY CASE TypeMesure WHEN 'Poids' THEN 1 WHEN 'Volume' THEN 2 ELSE 3 END, FacteurVersReference, Code;
            """;
        return ListerAsync(session, sql, Lire, annulation: annulation);
    }

    private static Unite Lire(SqlDataReader lecteur) => new(
        lecteur.GetInt32(0),
        lecteur.GetString(1),
        Enum.Parse<TypeMesure>(lecteur.GetString(2)),
        LireDecimal(lecteur, 3)!.Value);
}
