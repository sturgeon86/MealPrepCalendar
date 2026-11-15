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

    /// <summary>Ouvre une connexion sans transaction. L'appelant la libère avec <c>await using</c>.</summary>
    public async Task<SessionSql> OuvrirSessionAsync(CancellationToken annulation = default)
    {
        var connexion = new SqlConnection(_chaineConnexion);
        try
        {
            await connexion.OpenAsync(annulation);
            return new SessionSql(connexion, null);
        }
        catch
        {
            await connexion.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Exécute <paramref name="travail"/> dans une transaction : validée s'il se termine normalement,
    /// annulée au complet s'il lance une exception (qui est ensuite relancée).
    /// </summary>
    public async Task<T> DansTransactionAsync<T>(
        Func<SessionSql, Task<T>> travail, CancellationToken annulation = default)
    {
        await using var connexion = new SqlConnection(_chaineConnexion);
        await connexion.OpenAsync(annulation);
        var transaction = (SqlTransaction)await connexion.BeginTransactionAsync(annulation);

        // La session libère la transaction ; la connexion est libérée ici (double libération sans effet).
        await using var session = new SessionSql(connexion, transaction);
        try
        {
            var resultat = await travail(session);
            await session.ValiderAsync(annulation);
            return resultat;
        }
        catch
        {
            try
            {
                await session.AnnulerAsync();
            }
            catch
            {
                // La transaction est peut-être déjà annulée par le serveur : l'erreur d'origine prime.
            }

            throw;
        }
    }

    /// <inheritdoc cref="DansTransactionAsync{T}"/>
    public Task DansTransactionAsync(Func<SessionSql, Task> travail, CancellationToken annulation = default)
    {
        return DansTransactionAsync<object?>(
            async session =>
            {
                await travail(session);
                return null;
            },
            annulation);
    }

    /// <summary>Ouvre une connexion à la base et retourne la version du serveur (ex. : 14.0.3465.1).</summary>
    public async Task<string> ObtenirVersionServeurAsync(CancellationToken annulation = default)
    {
        await using var session = await OuvrirSessionAsync(annulation);
        var version = await session.ScalaireAsync<string>(
            "SELECT CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(50));", annulation: annulation);
        return version ?? "inconnue";
    }
}
