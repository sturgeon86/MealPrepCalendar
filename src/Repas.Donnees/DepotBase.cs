using Microsoft.Data.SqlClient;

namespace Repas.Donnees;

/// <summary>
/// Classe de base des dépôts (un par agrégat). Chaque méthode d'un dépôt correspond à une requête SQL
/// écrite à la main et accepte une <see cref="SessionSql"/> optionnelle :
/// <list type="bullet">
/// <item>sans session, la requête ouvre sa propre connexion et la referme ;</item>
/// <item>avec la session reçue de <see cref="BaseDeDonnees.DansTransactionAsync(Func{SessionSql, Task}, CancellationToken)"/>,
/// elle participe à la transaction, ce qui permet de combiner plusieurs dépôts.</item>
/// </list>
/// </summary>
public abstract class DepotBase
{
    private readonly BaseDeDonnees _baseDeDonnees;

    protected DepotBase(BaseDeDonnees baseDeDonnees)
    {
        _baseDeDonnees = baseDeDonnees;
    }

    /// <inheritdoc cref="SessionSql.ExecuterAsync"/>
    protected Task<int> ExecuterAsync(
        SessionSql? session,
        string sql,
        Action<SqlParameterCollection>? parametres = null,
        CancellationToken annulation = default)
    {
        return AvecSessionAsync(session, s => s.ExecuterAsync(sql, parametres, annulation), annulation);
    }

    /// <inheritdoc cref="SessionSql.ScalaireAsync{T}"/>
    protected Task<T?> ScalaireAsync<T>(
        SessionSql? session,
        string sql,
        Action<SqlParameterCollection>? parametres = null,
        CancellationToken annulation = default)
    {
        return AvecSessionAsync(session, s => s.ScalaireAsync<T>(sql, parametres, annulation), annulation);
    }

    /// <inheritdoc cref="SessionSql.ListerAsync{T}"/>
    protected Task<List<T>> ListerAsync<T>(
        SessionSql? session,
        string sql,
        Func<SqlDataReader, T> lire,
        Action<SqlParameterCollection>? parametres = null,
        CancellationToken annulation = default)
    {
        return AvecSessionAsync(session, s => s.ListerAsync(sql, lire, parametres, annulation), annulation);
    }

    /// <inheritdoc cref="SessionSql.PremierOuDefautAsync{T}"/>
    protected Task<T?> PremierOuDefautAsync<T>(
        SessionSql? session,
        string sql,
        Func<SqlDataReader, T> lire,
        Action<SqlParameterCollection>? parametres = null,
        CancellationToken annulation = default)
    {
        return AvecSessionAsync(session, s => s.PremierOuDefautAsync(sql, lire, parametres, annulation), annulation);
    }

    private async Task<T> AvecSessionAsync<T>(
        SessionSql? session, Func<SessionSql, Task<T>> requete, CancellationToken annulation)
    {
        if (session is not null)
        {
            return await requete(session);
        }

        await using var temporaire = await _baseDeDonnees.OuvrirSessionAsync(annulation);
        return await requete(temporaire);
    }
}
