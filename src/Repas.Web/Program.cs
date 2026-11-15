using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Repas.Domaine.Utilisateurs;
using Repas.Donnees;
using Repas.Donnees.Migrations;
using Repas.Web.Authentification;
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
builder.Services.AddSingleton<IHacheurMotDePasse, HacheurMotDePasse>();
builder.Services.AddSingleton<ServiceConnexion>();
builder.Services.AddSingleton<ServicePremierCompte>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Repas.Auth";
        options.LoginPath = "/connexion";
        options.AccessDeniedPath = "/connexion";
        options.ReturnUrlParameter = "retour";
        // Durée d'une session « rester connecté » ; prolongée tant que l'app est utilisée.
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

// Toute page ou tout point d'accès sans indication contraire exige une connexion.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddCascadingAuthenticationState();

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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Les fichiers statiques (CSS, scripts) sont nécessaires à la page de connexion elle-même.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/deconnexion", async (HttpContext contexte, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(contexte);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    await contexte.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/connexion");
});

app.Run();
return 0;
