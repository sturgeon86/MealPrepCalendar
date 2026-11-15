using Microsoft.Data.SqlClient;

namespace Repas.Donnees;

/// <summary>
/// Une connexion ouverte, avec ou sans transaction, sur laquelle on exécute des requêtes paramétrées.
/// Obtenue par <see cref="BaseDeDonnees.OuvrirSessionAsync"/> ou
/// <see cref="BaseDeDonnees.DansTransactionAsync(Func{SessionSql, Task}, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// Les valeurs passent toujours par <c>parametres</c> (voir <see cref="ParametresSql"/>),
/// jamais par concaténation dans le texte SQL.
/// </remarks>
public sealed class SessionSql : IAsyncDisposable
{
    private readonly SqlConnection _connexion;
    private readonly SqlTransaction? _transaction;

    internal SessionSql(SqlConnection connexion, SqlTransaction? transaction)
    {
        _connexion = connexion;
        _transaction = transaction;
    }

    /// <summary>Exécute un INSERT, UPDATE, DELETE ou du DDL.</summary>
    /// <returns>Le nombre de lignes touchées.</returns>
    public async Task<int> ExecuterAsync(
        string sql,
        Action<SqlParameterCollection>? parametres = null,
        CancellationToken annulation = default)
    {
        await using var commande = CreerCommande(sql, parametres);
        return await commande.ExecuteNonQueryAsync(annulation);
    }

    /// <summary>Retourne la première colonne de la première ligne, ou <c>default</c> si elle est absente ou NULL.</summary>
    public async Task<T?> ScalaireAsync<T>(
        string sql,
        Action<SqlParameterCollection>? parametres = null,
        CancellationToken annulation = default)
    {
        await using var commande = CreerCommande(sql, parametres);
        var valeur = await commande.ExecuteScalarAsync(annulation);
        if (valeur is null or DBNull)
        {
            return default;
        }

        // Ex. : SCOPE_IDENTITY() arrive en decimal et se convertit vers int.
        var typeCible = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(valeur, typeCible);
    }

    /// <summary>Retourne toutes les lignes, chacune mappée à la main par <paramref name="lire"/>.</summary>
    public async Task<List<T>> ListerAsync<T>(
        string sql,
        Func<SqlDataReader, T> lire,
        Action<SqlParameterCollection>? parametres = null,
        CancellationToken annulation = default)
    {
        await using var commande = CreerCommande(sql, parametres);
        await using var lecteur = await commande.ExecuteReaderAsync(annulation);

        var resultats = new List<T>();
        while (await lecteur.ReadAsync(annulation))
        {
            resultats.Add(lire(lecteur));
        }

        return resultats;
    }

    /// <summary>Retourne la première ligne mappée par <paramref name="lire"/>, ou <c>default</c> s'il n'y en a aucune.</summary>
    public async Task<T?> PremierOuDefautAsync<T>(
        string sql,
        Func<SqlDataReader, T> lire,
        Action<SqlParameterCollection>? parametres = null,
        CancellationToken annulation = default)
    {
        await using var commande = CreerCommande(sql, parametres);
        await using var lecteur = await commande.ExecuteReaderAsync(annulation);
        return await lecteur.ReadAsync(annulation) ? lire(lecteur) : default;
    }

    internal Task ValiderAsync(CancellationToken annulation) =>
        _transaction?.CommitAsync(annulation) ?? Task.CompletedTask;

    internal Task AnnulerAsync() =>
        _transaction?.RollbackAsync(CancellationToken.None) ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }

        await _connexion.DisposeAsync();
    }

    private SqlCommand CreerCommande(string sql, Action<SqlParameterCollection>? parametres)
    {
        var commande = new SqlCommand(sql, _connexion, _transaction);
        parametres?.Invoke(commande.Parameters);
        return commande;
    }
}
