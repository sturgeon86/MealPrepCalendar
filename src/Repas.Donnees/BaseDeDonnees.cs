using Microsoft.Data.SqlClient;

namespace Repas.Donnees;

/// <summary>
/// Point d'entrée vers la base SQL Server : existence de la base et état de la connexion.
/// </summary>
public sealed class BaseDeDonnees
{
    private readonly string _chaineConnexion;
    private readonly string _nomBase;

    public BaseDeDonnees(string chaineConnexion)
    {
        var constructeur = new SqlConnectionStringBuilder(chaineConnexion);
        if (string.IsNullOrWhiteSpace(constructeur.InitialCatalog))
        {
            throw new ArgumentException(
                "La chaîne de connexion doit préciser la base de données (Database=...).",
                nameof(chaineConnexion));
        }

        _chaineConnexion = chaineConnexion;
        _nomBase = constructeur.InitialCatalog;
    }

    /// <summary>Crée la base de données si elle n'existe pas encore sur le serveur.</summary>
    public async Task AssurerExistenceAsync(CancellationToken annulation = default)
    {
        var versMaster = new SqlConnectionStringBuilder(_chaineConnexion) { InitialCatalog = "master" };

        await using var connexion = new SqlConnection(versMaster.ConnectionString);
        await connexion.OpenAsync(annulation);

        // CREATE DATABASE n'accepte pas de paramètre : le nom passe par QUOTENAME.
        const string sql = """
            IF DB_ID(@nomBase) IS NULL
            BEGIN
                DECLARE @sql NVARCHAR(300) = N'CREATE DATABASE ' + QUOTENAME(@nomBase);
                EXEC (@sql);
            END
            """;

        await using var commande = new SqlCommand(sql, connexion);
        commande.Parameters.Add("@nomBase", System.Data.SqlDbType.NVarChar, 128).Value = _nomBase;
        await commande.ExecuteNonQueryAsync(annulation);
    }

    /// <summary>Ouvre une connexion à la base et retourne la version du serveur (ex. : 14.0.3465.1).</summary>
    public async Task<string> ObtenirVersionServeurAsync(CancellationToken annulation = default)
    {
        await using var connexion = new SqlConnection(_chaineConnexion);
        await connexion.OpenAsync(annulation);

        await using var commande = new SqlCommand(
            "SELECT CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(50));", connexion);
        var version = await commande.ExecuteScalarAsync(annulation);
        return version as string ?? "inconnue";
    }
}
