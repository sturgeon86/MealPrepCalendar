using System.Data;
using Microsoft.Data.SqlClient;
using Repas.Domaine.Calendrier;

namespace Repas.Donnees;

/// <summary>Accès à la table <c>RepasPlanifie</c> : le calendrier de repas partagé par la famille.</summary>
public sealed class DepotCalendrier : DepotBase
{
    public DepotCalendrier(BaseDeDonnees baseDeDonnees) : base(baseDeDonnees)
    {
    }

    /// <summary>
    /// Les repas planifiés du <paramref name="debut"/> au <paramref name="fin"/> inclusivement,
    /// par date, dîner avant souper, puis dans l'ordre d'ajout.
    /// </summary>
    public Task<List<RepasPlanifie>> ListerAsync(
        DateOnly debut, DateOnly fin, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            SELECT rp.Id, rp.[Date], rp.Plage, rp.RecetteId, rp.TexteLibre, rp.Portions, r.Nom, r.Portions
            FROM dbo.RepasPlanifie rp
            LEFT JOIN dbo.Recette r ON r.Id = rp.RecetteId
            WHERE rp.[Date] BETWEEN @debut AND @fin
            ORDER BY rp.[Date], CASE rp.Plage WHEN 'Diner' THEN 0 ELSE 1 END, rp.Id;
            """;
        return ListerAsync(
            session,
            sql,
            Lire,
            p => p.Ajouter("@debut", SqlDbType.Date, debut).Ajouter("@fin", SqlDbType.Date, fin),
            annulation);
    }

    /// <summary>Le repas planifié, ou <c>null</c> s'il n'existe pas.</summary>
    public Task<RepasPlanifie?> TrouverAsync(int id, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            SELECT rp.Id, rp.[Date], rp.Plage, rp.RecetteId, rp.TexteLibre, rp.Portions, r.Nom, r.Portions
            FROM dbo.RepasPlanifie rp
            LEFT JOIN dbo.Recette r ON r.Id = rp.RecetteId
            WHERE rp.Id = @id;
            """;
        return PremierOuDefautAsync(session, sql, Lire, p => p.Ajouter("@id", SqlDbType.Int, id), annulation);
    }

    /// <summary>Inscrit un repas au calendrier.</summary>
    /// <param name="creeParId">L'utilisateur qui planifie le repas.</param>
    /// <returns>L'<c>Id</c> du nouveau repas.</returns>
    public Task<int> AjouterAsync(
        RepasPlanifie repas, int creeParId, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            INSERT INTO dbo.RepasPlanifie ([Date], Plage, RecetteId, TexteLibre, Portions, CreePar)
            OUTPUT INSERTED.Id
            VALUES (@date, @plage, @recetteId, @texteLibre, @portions, @creePar);
            """;
        return ScalaireAsync<int>(
            session,
            sql,
            p => AjouterColonnes(p, repas).Ajouter("@creePar", SqlDbType.Int, creeParId),
            annulation);
    }

    /// <summary>
    /// Remplace le repas : autre date ou plage (déplacement), autre recette, autres portions ou autre texte.
    /// </summary>
    /// <returns><c>false</c> si le repas n'existe pas (ou plus).</returns>
    public async Task<bool> ModifierAsync(RepasPlanifie repas, SessionSql? session = null, CancellationToken annulation = default)
    {
        const string sql = """
            UPDATE dbo.RepasPlanifie
            SET [Date] = @date, Plage = @plage, RecetteId = @recetteId, TexteLibre = @texteLibre, Portions = @portions
            WHERE Id = @id;
            """;
        return await ExecuterAsync(
            session,
            sql,
            p => AjouterColonnes(p, repas).Ajouter("@id", SqlDbType.Int, repas.Id),
            annulation) > 0;
    }

    /// <summary>Retire le repas du calendrier.</summary>
    /// <returns><c>false</c> s'il n'existait pas (déjà retiré par quelqu'un d'autre, par exemple).</returns>
    public async Task<bool> SupprimerAsync(int id, SessionSql? session = null, CancellationToken annulation = default)
    {
        return await ExecuterAsync(
            session, "DELETE FROM dbo.RepasPlanifie WHERE Id = @id;", p => p.Ajouter("@id", SqlDbType.Int, id), annulation) > 0;
    }

    private static RepasPlanifie Lire(SqlDataReader l) => new(
        Id: l.GetInt32(0),
        Date: l.GetFieldValue<DateOnly>(1),
        Plage: Enum.Parse<Plage>(l.GetString(2)),
        RecetteId: l.IsDBNull(3) ? null : l.GetInt32(3),
        TexteLibre: l.IsDBNull(4) ? null : l.GetString(4),
        Portions: l.IsDBNull(5) ? null : l.GetInt32(5),
        NomRecette: l.IsDBNull(6) ? null : l.GetString(6),
        PortionsRecette: l.IsDBNull(7) ? null : l.GetInt32(7));

    private static SqlParameterCollection AjouterColonnes(SqlParameterCollection p, RepasPlanifie repas) => p
        .Ajouter("@date", SqlDbType.Date, repas.Date)
        .Ajouter("@plage", SqlDbType.VarChar, 10, repas.Plage.ToString())
        .Ajouter("@recetteId", SqlDbType.Int, repas.RecetteId)
        .Ajouter("@texteLibre", SqlDbType.NVarChar, ValidationRepas.LongueurMaxTexteLibre,
            string.IsNullOrWhiteSpace(repas.TexteLibre) ? null : repas.TexteLibre.Trim())
        .Ajouter("@portions", SqlDbType.Int, repas.Portions);
}
