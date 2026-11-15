using Repas.Donnees;
using Repas.Donnees.Migrations;
using Repas.Web.Components;

var builder = WebApplication.CreateBuilder(args);

var chaineConnexion = builder.Configuration.GetConnectionString("Repas");
if (string.IsNullOrWhiteSpace(chaineConnexion))
{
    throw new InvalidOperationException(
        "Chaîne de connexion « Repas » absente. Définir la variable d'environnement " +
        "ConnectionStrings__Repas, ou copier appsettings.Development.json.example vers " +
        "appsettings.Development.json et la compléter.");
}

var baseDeDonnees = new BaseDeDonnees(chaineConnexion);
builder.Services.AddSingleton(baseDeDonnees);
builder.Services.AddSingleton<DepotUtilisateurs>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Prépare la base avant d'accepter des requêtes : création au besoin, puis migrations.
// Un échec arrête le démarrage, pour ne jamais servir l'app sur un schéma incomplet.
try
{
    await baseDeDonnees.AssurerExistenceAsync();

    var dossierMigrations = Path.Combine(AppContext.BaseDirectory, "db", "migrations");
    var appliquees = await new ExecuteurMigrations(chaineConnexion, dossierMigrations).AppliquerAsync();

    if (appliquees.Count == 0)
    {
        app.Logger.LogInformation("Base de données à jour : aucune migration à appliquer.");
    }
    else
    {
        app.Logger.LogInformation(
            "{Nombre} migration(s) appliquée(s) : {Scripts}", appliquees.Count, string.Join(", ", appliquees));
    }
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Démarrage interrompu, la base de données n'a pas pu être préparée : {Message}", ex.Message);
    return 1;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
return 0;
