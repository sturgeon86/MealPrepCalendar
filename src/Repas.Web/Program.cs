using Repas.Donnees;
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

builder.Services.AddSingleton(new BaseDeDonnees(chaineConnexion));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Crée la base au premier démarrage. Un échec est journalisé sans arrêter l'app pour l'instant ;
// l'exécuteur de migrations (story 0.3) rendra cet échec bloquant.
try
{
    await app.Services.GetRequiredService<BaseDeDonnees>().AssurerExistenceAsync();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Impossible de joindre le serveur SQL ou de créer la base de données.");
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
