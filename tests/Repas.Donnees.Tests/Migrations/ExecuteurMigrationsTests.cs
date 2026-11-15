using Repas.Donnees.Migrations;
using Repas.Donnees.Tests.Outils;

namespace Repas.Donnees.Tests.Migrations;

/// <summary>Tests d'intégration : chaque test reçoit une base vide et un dossier de scripts vide.</summary>
public sealed class ExecuteurMigrationsTests : IAsyncLifetime
{
    private BaseDeTest _base = null!;
    private string _dossier = null!;

    public async Task InitializeAsync()
    {
        _base = await BaseDeTest.CreerAsync();
        _dossier = Directory.CreateTempSubdirectory("repas-migrations-").FullName;
    }

    public async Task DisposeAsync()
    {
        await _base.DisposeAsync();
        Directory.Delete(_dossier, recursive: true);
    }

    private void EcrireScript(string nomFichier, string sql) =>
        File.WriteAllText(Path.Combine(_dossier, nomFichier), sql);

    private ExecuteurMigrations CreerExecuteur() => new(_base.ChaineConnexion, _dossier);

    private Task<int> CompterAsync(string table) =>
        _base.ScalaireAsync<int>($"SELECT COUNT(*) FROM {table};");

    [Fact]
    public async Task DossierVide_CreeLaTableVersionSchemaSansRienAppliquer()
    {
        var appliques = await CreerExecuteur().AppliquerAsync();

        Assert.Empty(appliques);
        Assert.Equal(0, await CompterAsync("dbo.VersionSchema"));
    }

    [Fact]
    public async Task AppliqueLesScriptsEnOrdreDeNumeroEtLesConsigne()
    {
        // Le 0002 dépend du 0001 : un mauvais ordre ferait échouer l'insertion.
        EcrireScript("0002_ajouter_ligne.sql", "INSERT INTO dbo.Essai (Valeur) VALUES (42);");
        EcrireScript("0001_creer_table.sql", "CREATE TABLE dbo.Essai (Valeur INT NOT NULL);");

        var appliques = await CreerExecuteur().AppliquerAsync();

        Assert.Equal(["0001_creer_table.sql", "0002_ajouter_ligne.sql"], appliques);
        Assert.Equal(42, await _base.ScalaireAsync<int>("SELECT Valeur FROM dbo.Essai;"));
        Assert.Equal(2, await CompterAsync("dbo.VersionSchema"));
        Assert.Equal(
            "0002_ajouter_ligne.sql",
            await _base.ScalaireAsync<string>("SELECT Nom FROM dbo.VersionSchema WHERE Numero = 2;"));
    }

    [Fact]
    public async Task UnScriptDejaAppliqueNEstJamaisReexecute()
    {
        EcrireScript("0001_creer_table.sql", "CREATE TABLE dbo.Essai (Valeur INT NOT NULL);");
        EcrireScript("0002_ajouter_ligne.sql", "INSERT INTO dbo.Essai (Valeur) VALUES (1);");
        await CreerExecuteur().AppliquerAsync();

        var appliquesAuDeuxiemeAppel = await CreerExecuteur().AppliquerAsync();

        Assert.Empty(appliquesAuDeuxiemeAppel);
        Assert.Equal(1, await CompterAsync("dbo.Essai"));
        Assert.Equal(2, await CompterAsync("dbo.VersionSchema"));
    }

    [Fact]
    public async Task UnNouveauScriptEstAppliqueSeul()
    {
        EcrireScript("0001_creer_table.sql", "CREATE TABLE dbo.Essai (Valeur INT NOT NULL);");
        await CreerExecuteur().AppliquerAsync();
        EcrireScript("0002_ajouter_ligne.sql", "INSERT INTO dbo.Essai (Valeur) VALUES (1);");

        var appliques = await CreerExecuteur().AppliquerAsync();

        Assert.Equal(["0002_ajouter_ligne.sql"], appliques);
        Assert.Equal(1, await CompterAsync("dbo.Essai"));
    }

    [Fact]
    public async Task UnScriptEnEchecEstAnnuleAuCompletEtArreteLaSuite()
    {
        EcrireScript("0001_creer_table.sql", "CREATE TABLE dbo.Essai (Valeur INT NOT NULL);");
        EcrireScript(
            "0002_en_echec.sql",
            """
            INSERT INTO dbo.Essai (Valeur) VALUES (1);
            GO
            INSERT INTO dbo.TableInexistante (Valeur) VALUES (2);
            """);
        EcrireScript("0003_jamais_atteint.sql", "CREATE TABLE dbo.Suite (Valeur INT NOT NULL);");

        var erreur = await Assert.ThrowsAsync<MigrationException>(() => CreerExecuteur().AppliquerAsync());

        Assert.Contains("0002_en_echec.sql", erreur.Message);
        Assert.Contains("TableInexistante", erreur.Message);
        // Le 0001 reste en place, la première instruction du 0002 est annulée, le 0003 n'est pas tenté.
        Assert.Equal(0, await CompterAsync("dbo.Essai"));
        Assert.Equal(1, await CompterAsync("dbo.VersionSchema"));
        Assert.Equal(0, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name = N'Suite';"));
    }

    [Fact]
    public async Task ApresCorrectionDuScriptEnEchec_LaSuiteSApplique()
    {
        EcrireScript("0001_en_echec.sql", "INSERT INTO dbo.TableInexistante (Valeur) VALUES (1);");
        await Assert.ThrowsAsync<MigrationException>(() => CreerExecuteur().AppliquerAsync());
        EcrireScript("0001_en_echec.sql", "CREATE TABLE dbo.Essai (Valeur INT NOT NULL);");

        var appliques = await CreerExecuteur().AppliquerAsync();

        Assert.Equal(["0001_en_echec.sql"], appliques);
    }

    [Fact]
    public async Task LesLotsSeparesParGoSontExecutesUnParUn()
    {
        // CREATE VIEW doit être la première instruction de son lot : sans découpage, ce script échoue.
        EcrireScript(
            "0001_table_et_vue.sql",
            """
            CREATE TABLE dbo.Essai (Valeur INT NOT NULL);
            INSERT INTO dbo.Essai (Valeur) VALUES (7);
            GO
            CREATE VIEW dbo.VueEssai AS SELECT Valeur FROM dbo.Essai;
            go
            """);

        await CreerExecuteur().AppliquerAsync();

        Assert.Equal(7, await _base.ScalaireAsync<int>("SELECT Valeur FROM dbo.VueEssai;"));
    }

    [Fact]
    public async Task NomDeFichierInvalide_EstRefuseAvantTouteExecution()
    {
        EcrireScript("0001_creer_table.sql", "CREATE TABLE dbo.Essai (Valeur INT NOT NULL);");
        EcrireScript("ajout_sans_numero.sql", "SELECT 1;");

        var erreur = await Assert.ThrowsAsync<MigrationException>(() => CreerExecuteur().AppliquerAsync());

        Assert.Contains("ajout_sans_numero.sql", erreur.Message);
        Assert.Equal(0, await _base.ScalaireAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name = N'Essai';"));
    }

    [Fact]
    public async Task NumeroEnDouble_EstRefuse()
    {
        EcrireScript("0001_premier.sql", "SELECT 1;");
        EcrireScript("0001_deuxieme.sql", "SELECT 1;");

        var erreur = await Assert.ThrowsAsync<MigrationException>(() => CreerExecuteur().AppliquerAsync());

        Assert.Contains("0001_premier.sql", erreur.Message);
        Assert.Contains("0001_deuxieme.sql", erreur.Message);
    }

    [Fact]
    public async Task DossierIntrouvable_EstSignale()
    {
        var executeur = new ExecuteurMigrations(_base.ChaineConnexion, Path.Combine(_dossier, "absent"));

        var erreur = await Assert.ThrowsAsync<MigrationException>(() => executeur.AppliquerAsync());

        Assert.Contains("introuvable", erreur.Message);
    }

    [Fact]
    public async Task DeuxTablesDeSuivi_SuiventLeursScriptsSeparement()
    {
        var autreDossier = Directory.CreateTempSubdirectory("repas-donnees-").FullName;
        try
        {
            EcrireScript("0001_creer_table.sql", "CREATE TABLE dbo.Essai (Valeur INT NOT NULL);");
            File.WriteAllText(Path.Combine(autreDossier, "0001_ajouter_ligne.sql"), "INSERT INTO dbo.Essai (Valeur) VALUES (1);");

            await CreerExecuteur().AppliquerAsync();
            var donnees = await new ExecuteurMigrations(_base.ChaineConnexion, autreDossier, "VersionEssai").AppliquerAsync();

            // Le même numéro 0001 est appliqué dans chaque série, chacune dans sa table.
            Assert.Equal(["0001_ajouter_ligne.sql"], donnees);
            Assert.Equal(1, await CompterAsync("dbo.Essai"));
            Assert.Equal(1, await CompterAsync("dbo.VersionSchema"));
            Assert.Equal(1, await CompterAsync("dbo.VersionEssai"));
        }
        finally
        {
            Directory.Delete(autreDossier, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Version;DROP TABLE x")]
    [InlineData("dbo.Version")]
    [InlineData("1Version")]
    public void NomDeTableDeSuiviInvalide_EstRefuse(string table)
    {
        Assert.Throws<ArgumentException>(() => new ExecuteurMigrations("Server=x;Database=y", _dossier, table));
    }

    [Fact]
    public async Task LesFichiersNonSqlSontIgnores()
    {
        EcrireScript(".gitkeep", "");
        EcrireScript("LISEZMOI.md", "Notes");

        var appliques = await CreerExecuteur().AppliquerAsync();

        Assert.Empty(appliques);
    }
}
