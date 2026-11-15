using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Repas.Donnees.Tests.Outils;

/// <summary>
/// Base SQL Server jetable pour un test d'intégration : créée avec un nom unique, supprimée à la fin.
/// </summary>
/// <remarks>
/// Le serveur vient de la variable d'environnement <c>REPAS_TESTS_CONNEXION</c> ou, à défaut,
/// de la chaîne « Repas » de <c>src/Repas.Web/appsettings.Development.json</c>
/// (le SQL Server de <c>docker compose up sql</c>). Le nom de base de cette chaîne est ignoré.
/// </remarks>
public sealed class BaseDeTest : IAsyncDisposable
{
    private const string VariableEnvironnement = "REPAS_TESTS_CONNEXION";

    private readonly string _nomBase;

    private BaseDeTest(string chaineConnexion, string nomBase)
    {
        ChaineConnexion = chaineConnexion;
        _nomBase = nomBase;
    }

    public string ChaineConnexion { get; }

    public static async Task<BaseDeTest> CreerAsync()
    {
        var nomBase = "Repas_Tests_" + Guid.NewGuid().ToString("N");
        var constructeur = new SqlConnectionStringBuilder(TrouverChaineServeur()) { InitialCatalog = nomBase };

        await new BaseDeDonnees(constructeur.ConnectionString).AssurerExistenceAsync();
        return new BaseDeTest(constructeur.ConnectionString, nomBase);
    }

    public async Task ExecuterAsync(string sql)
    {
        await using var connexion = new SqlConnection(ChaineConnexion);
        await connexion.OpenAsync();
        await using var commande = new SqlCommand(sql, connexion);
        await commande.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalaireAsync<T>(string sql)
    {
        await using var connexion = new SqlConnection(ChaineConnexion);
        await connexion.OpenAsync();
        await using var commande = new SqlCommand(sql, connexion);
        return (T)(await commande.ExecuteScalarAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        // Les connexions gardées en réserve empêcheraient la suppression de la base.
        SqlConnection.ClearAllPools();

        var versMaster = new SqlConnectionStringBuilder(ChaineConnexion) { InitialCatalog = "master" };
        await using var connexion = new SqlConnection(versMaster.ConnectionString);
        await connexion.OpenAsync();

        const string sql = """
            IF DB_ID(@nomBase) IS NOT NULL
            BEGIN
                DECLARE @sql NVARCHAR(600) =
                    N'ALTER DATABASE ' + QUOTENAME(@nomBase) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; ' +
                    N'DROP DATABASE ' + QUOTENAME(@nomBase) + N';';
                EXEC (@sql);
            END
            """;
        await using var commande = new SqlCommand(sql, connexion);
        commande.Parameters.Add("@nomBase", SqlDbType.NVarChar, 128).Value = _nomBase;
        await commande.ExecuteNonQueryAsync();
    }

    private static string TrouverChaineServeur()
    {
        var chaine = Environment.GetEnvironmentVariable(VariableEnvironnement);
        if (!string.IsNullOrWhiteSpace(chaine))
        {
            return chaine;
        }

        var configLocale = TrouverConfigLocale();
        if (configLocale is not null)
        {
            using var json = JsonDocument.Parse(
                File.ReadAllText(configLocale),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (json.RootElement.TryGetProperty("ConnectionStrings", out var chaines)
                && chaines.TryGetProperty("Repas", out var repas)
                && !string.IsNullOrWhiteSpace(repas.GetString()))
            {
                return repas.GetString()!;
            }
        }

        throw new InvalidOperationException(
            "Aucun SQL Server configuré pour les tests d'intégration. Démarrer `docker compose up -d sql`, puis " +
            $"définir {VariableEnvironnement} ou créer src/Repas.Web/appsettings.Development.json " +
            "à partir du fichier .example.");
    }

    private static string? TrouverConfigLocale()
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier is not null; dossier = dossier.Parent)
        {
            if (File.Exists(Path.Combine(dossier.FullName, "Repas.slnx")))
            {
                var chemin = Path.Combine(dossier.FullName, "src", "Repas.Web", "appsettings.Development.json");
                return File.Exists(chemin) ? chemin : null;
            }
        }

        return null;
    }
}
