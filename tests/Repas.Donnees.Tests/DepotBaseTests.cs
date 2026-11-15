using System.Data;
using Microsoft.Data.SqlClient;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests;

/// <summary>
/// Tests d'intégration de la couche d'accès aux données, à travers un dépôt d'essai
/// écrit comme le seront les vrais dépôts.
/// </summary>
public sealed class DepotBaseTests : IAsyncLifetime
{
    private BaseDeTest _baseDeTest = null!;
    private BaseDeDonnees _base = null!;
    private DepotEssai _depot = null!;

    public async Task InitializeAsync()
    {
        _baseDeTest = await BaseDeTest.CreerAsync();
        await _baseDeTest.ExecuterAsync(
            """
            CREATE TABLE dbo.Essai
            (
                Id       INT IDENTITY  NOT NULL CONSTRAINT PK_Essai PRIMARY KEY,
                Nom      NVARCHAR(100) NOT NULL CONSTRAINT UQ_Essai_Nom UNIQUE,
                Note     NVARCHAR(200) NULL,
                Quantite DECIMAL(18,4) NOT NULL
            );
            """);
        _base = new BaseDeDonnees(_baseDeTest.ChaineConnexion);
        _depot = new DepotEssai(_base);
    }

    public async Task DisposeAsync() => await _baseDeTest.DisposeAsync();

    [Fact]
    public async Task EcritPuisRelitUneLigne()
    {
        var id = await _depot.AjouterAsync(new Essai(0, "Farine", "tout usage", 1.2345m));

        var relu = await _depot.TrouverAsync(id);

        Assert.Equal(new Essai(id, "Farine", "tout usage", 1.2345m), relu);
    }

    [Fact]
    public async Task UneValeurNullEstEcriteEtRelueCommeNull()
    {
        var id = await _depot.AjouterAsync(new Essai(0, "Sel", null, 1m));

        var relu = await _depot.TrouverAsync(id);

        Assert.NotNull(relu);
        Assert.Null(relu.Note);
    }

    [Fact]
    public async Task LigneAbsente_RetourneNull()
    {
        Assert.Null(await _depot.TrouverAsync(999));
    }

    [Fact]
    public async Task ListeLesLignesMappees()
    {
        await _depot.AjouterAsync(new Essai(0, "Sucre", null, 2m));
        await _depot.AjouterAsync(new Essai(0, "Beurre", null, 1m));

        var lignes = await _depot.ListerAsync();

        Assert.Equal(["Beurre", "Sucre"], lignes.Select(l => l.Nom));
    }

    [Fact]
    public async Task LesValeursPassentParDesParametres_PasParLeTexteSql()
    {
        // Un nom qui briserait une requête construite par concaténation.
        const string nom = "Lait'; DROP TABLE dbo.Essai; --";

        var id = await _depot.AjouterAsync(new Essai(0, nom, null, 1m));

        Assert.Equal(nom, (await _depot.TrouverAsync(id))!.Nom);
    }

    [Fact]
    public async Task ModifierRetourneLeNombreDeLignesTouchees()
    {
        var id = await _depot.AjouterAsync(new Essai(0, "Riz", null, 1m));

        Assert.Equal(1, await _depot.ChangerQuantiteAsync(id, 3.5m));
        Assert.Equal(0, await _depot.ChangerQuantiteAsync(999, 3.5m));
        Assert.Equal(3.5m, (await _depot.TrouverAsync(id))!.Quantite);
    }

    [Fact]
    public async Task TransactionReussie_ToutEstValide()
    {
        var total = await _base.DansTransactionAsync(async session =>
        {
            await _depot.AjouterAsync(new Essai(0, "Oeufs", null, 12m), session);
            await _depot.AjouterAsync(new Essai(0, "Lait", null, 2m), session);
            return await _depot.CompterAsync(session);
        });

        Assert.Equal(2, total);
        Assert.Equal(2, await _depot.CompterAsync());
    }

    [Fact]
    public async Task TransactionEnEchec_ToutEstAnnuleEtLErreurEstRelancee()
    {
        await _depot.AjouterAsync(new Essai(0, "Poivre", null, 1m));

        // Le deuxième ajout viole la contrainte d'unicité sur Nom.
        await Assert.ThrowsAsync<SqlException>(() => _base.DansTransactionAsync(async session =>
        {
            await _depot.AjouterAsync(new Essai(0, "Cannelle", null, 1m), session);
            await _depot.AjouterAsync(new Essai(0, "Poivre", null, 1m), session);
        }));

        Assert.Equal(["Poivre"], (await _depot.ListerAsync()).Select(l => l.Nom));
    }

    [Fact]
    public async Task ExceptionDuCodeAppelant_AnnuleAussiLaTransaction()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _base.DansTransactionAsync(async session =>
        {
            await _depot.AjouterAsync(new Essai(0, "Thym", null, 1m), session);
            throw new InvalidOperationException("Règle métier non respectée.");
        }));

        Assert.Equal(0, await _depot.CompterAsync());
    }

    private sealed record Essai(int Id, string Nom, string? Note, decimal Quantite);

    private sealed class DepotEssai(BaseDeDonnees baseDeDonnees) : DepotBase(baseDeDonnees)
    {
        public async Task<int> AjouterAsync(Essai essai, SessionSql? session = null)
        {
            const string sql = """
                INSERT INTO dbo.Essai (Nom, Note, Quantite) VALUES (@nom, @note, @quantite);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;
            return await ScalaireAsync<int>(session, sql, p => p
                .Ajouter("@nom", SqlDbType.NVarChar, 100, essai.Nom)
                .Ajouter("@note", SqlDbType.NVarChar, 200, essai.Note)
                .AjouterDecimal("@quantite", 18, 4, essai.Quantite));
        }

        public Task<Essai?> TrouverAsync(int id, SessionSql? session = null)
        {
            const string sql = "SELECT Id, Nom, Note, Quantite FROM dbo.Essai WHERE Id = @id;";
            return PremierOuDefautAsync(session, sql, Lire, p => p.Ajouter("@id", SqlDbType.Int, id));
        }

        public Task<List<Essai>> ListerAsync(SessionSql? session = null)
        {
            const string sql = "SELECT Id, Nom, Note, Quantite FROM dbo.Essai ORDER BY Nom;";
            return ListerAsync(session, sql, Lire);
        }

        public Task<int> ChangerQuantiteAsync(int id, decimal quantite, SessionSql? session = null)
        {
            const string sql = "UPDATE dbo.Essai SET Quantite = @quantite WHERE Id = @id;";
            return ExecuterAsync(session, sql, p => p
                .AjouterDecimal("@quantite", 18, 4, quantite)
                .Ajouter("@id", SqlDbType.Int, id));
        }

        public Task<int> CompterAsync(SessionSql? session = null)
        {
            return ScalaireAsync<int>(session, "SELECT COUNT(*) FROM dbo.Essai;");
        }

        private static Essai Lire(SqlDataReader lecteur) => new(
            lecteur.GetInt32(0),
            lecteur.GetString(1),
            lecteur.IsDBNull(2) ? null : lecteur.GetString(2),
            lecteur.GetDecimal(3));
    }
}
