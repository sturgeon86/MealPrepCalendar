using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Repas.Donnees.Migrations;

/// <summary>
/// Applique, en ordre de numéro, les scripts SQL d'un dossier (<c>0001_nom.sql</c>, <c>0002_…</c>).
/// Chaque script s'exécute une seule fois, dans sa propre transaction, et est consigné
/// dans une table de suivi (<c>VersionSchema</c> par défaut).
/// </summary>
public sealed partial class ExecuteurMigrations
{
    /// <summary>Table de suivi des migrations du schéma (<c>db/migrations</c>).</summary>
    public const string TableSchema = "VersionSchema";

    /// <summary>Table de suivi des données de base (<c>db/seed</c>).</summary>
    public const string TableDonneesDeBase = "VersionDonneesDeBase";

    private readonly string _chaineConnexion;
    private readonly string _dossierScripts;
    private readonly string _tableSuivi;

    /// <param name="tableSuivi">
    /// Table qui consigne les scripts appliqués. Un nom différent par dossier permet de suivre
    /// séparément deux séries de scripts numérotés (schéma et données de base).
    /// </param>
    public ExecuteurMigrations(string chaineConnexion, string dossierScripts, string tableSuivi = TableSchema)
    {
        // Le nom de table entre dans le texte SQL (il ne peut pas être paramétré) : lettres et chiffres seulement.
        if (!NomTableValide().IsMatch(tableSuivi))
        {
            throw new ArgumentException($"Nom de table de suivi invalide : « {tableSuivi} ».", nameof(tableSuivi));
        }

        _chaineConnexion = chaineConnexion;
        _dossierScripts = dossierScripts;
        _tableSuivi = tableSuivi;
    }

    [GeneratedRegex(@"^(\d{4})_(.+)\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex FormatNomFichier();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]{0,99}$")]
    private static partial Regex NomTableValide();

    private sealed record ScriptMigration(int Numero, string NomFichier, string Chemin);

    /// <summary>Applique les scripts qui ne l'ont pas encore été.</summary>
    /// <returns>Les noms des fichiers appliqués par cet appel, en ordre.</returns>
    /// <exception cref="MigrationException">Dossier ou nom de fichier invalide, ou script en échec.</exception>
    public async Task<IReadOnlyList<string>> AppliquerAsync(CancellationToken annulation = default)
    {
        var scripts = ListerScripts();

        await using var connexion = new SqlConnection(_chaineConnexion);
        await connexion.OpenAsync(annulation);

        // Toute erreur SQL annule la transaction en cours au complet.
        await ExecuterAsync(connexion, null, "SET XACT_ABORT ON;", annulation);
        await CreerTableSuiviAsync(connexion, annulation);
        var dejaAppliques = await LireNumerosAppliquesAsync(connexion, annulation);

        var appliques = new List<string>();
        foreach (var script in scripts)
        {
            if (dejaAppliques.Contains(script.Numero))
            {
                continue;
            }

            await AppliquerScriptAsync(connexion, script, annulation);
            appliques.Add(script.NomFichier);
        }

        return appliques;
    }

    private List<ScriptMigration> ListerScripts()
    {
        if (!Directory.Exists(_dossierScripts))
        {
            throw new MigrationException($"Dossier de migrations introuvable : {_dossierScripts}");
        }

        var scripts = new List<ScriptMigration>();
        foreach (var chemin in Directory.EnumerateFiles(_dossierScripts, "*.sql"))
        {
            var nomFichier = Path.GetFileName(chemin);
            var correspondance = FormatNomFichier().Match(nomFichier);
            if (!correspondance.Success)
            {
                throw new MigrationException(
                    $"Nom de script de migration invalide : « {nomFichier} ». " +
                    "Format attendu : quatre chiffres, un soulignement, un nom (ex. : 0001_initial.sql).");
            }

            scripts.Add(new ScriptMigration(int.Parse(correspondance.Groups[1].Value), nomFichier, chemin));
        }

        var doublon = scripts.GroupBy(s => s.Numero).FirstOrDefault(g => g.Count() > 1);
        if (doublon is not null)
        {
            throw new MigrationException(
                $"Deux scripts de migration portent le numéro {doublon.Key:D4} : " +
                string.Join(", ", doublon.Select(s => s.NomFichier).Order()) + ".");
        }

        return scripts.OrderBy(s => s.Numero).ToList();
    }

    private Task CreerTableSuiviAsync(SqlConnection connexion, CancellationToken annulation)
    {
        var sql = $"""
            IF OBJECT_ID(N'dbo.{_tableSuivi}', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.{_tableSuivi}
                (
                    Numero          INT           NOT NULL CONSTRAINT PK_{_tableSuivi} PRIMARY KEY,
                    Nom             NVARCHAR(200) NOT NULL,
                    DateApplication DATETIME2(0)  NOT NULL
                        CONSTRAINT DF_{_tableSuivi}_DateApplication DEFAULT SYSUTCDATETIME()
                );
            END
            """;
        return ExecuterAsync(connexion, null, sql, annulation);
    }

    private async Task<HashSet<int>> LireNumerosAppliquesAsync(SqlConnection connexion, CancellationToken annulation)
    {
        var numeros = new HashSet<int>();
        await using var commande = new SqlCommand($"SELECT Numero FROM dbo.{_tableSuivi};", connexion);
        await using var lecteur = await commande.ExecuteReaderAsync(annulation);
        while (await lecteur.ReadAsync(annulation))
        {
            numeros.Add(lecteur.GetInt32(0));
        }

        return numeros;
    }

    private async Task AppliquerScriptAsync(
        SqlConnection connexion, ScriptMigration script, CancellationToken annulation)
    {
        var contenu = await File.ReadAllTextAsync(script.Chemin, annulation);
        var lots = DecoupeurLots.Decouper(contenu);

        await using var transaction = (SqlTransaction)await connexion.BeginTransactionAsync(annulation);
        try
        {
            foreach (var lot in lots)
            {
                await ExecuterAsync(connexion, transaction, lot, annulation);
            }

            await using var consigne = new SqlCommand(
                $"INSERT INTO dbo.{_tableSuivi} (Numero, Nom) VALUES (@numero, @nom);", connexion, transaction);
            consigne.Parameters.Add("@numero", SqlDbType.Int).Value = script.Numero;
            consigne.Parameters.Add("@nom", SqlDbType.NVarChar, 200).Value = script.NomFichier;
            await consigne.ExecuteNonQueryAsync(annulation);

            await transaction.CommitAsync(annulation);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // XACT_ABORT a peut-être déjà annulé la transaction : rien de plus à défaire.
            }

            throw new MigrationException(
                $"Le script « {script.NomFichier} » de {Path.GetFileName(_dossierScripts)} a échoué " +
                $"et a été annulé au complet : {ex.Message}", ex);
        }
    }

    private static async Task ExecuterAsync(
        SqlConnection connexion, SqlTransaction? transaction, string sql, CancellationToken annulation)
    {
        await using var commande = new SqlCommand(sql, connexion, transaction);
        await commande.ExecuteNonQueryAsync(annulation);
    }
}
