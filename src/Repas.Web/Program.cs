using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Repas.Domaine.Epicerie;
using Repas.Domaine.Recettes;
using Repas.Domaine.Utilisateurs;
using Repas.Donnees;
using Repas.Donnees.Migrations;
using Repas.Donnees.Photos;
using Repas.Donnees.Web;
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
builder.Services.AddSingleton<DepotCategories>();
builder.Services.AddSingleton<DepotUnites>();
builder.Services.AddSingleton<DepotIngredients>();
builder.Services.AddSingleton<DepotRecettes>();
builder.Services.AddSingleton<DepotCalendrier>();
builder.Services.AddSingleton<DepotListeEpicerie>();

// Temps réel : les écrans ouverts de la liste reçoivent les changements faits ailleurs (un seul conteneur : en mémoire).
builder.Services.AddSingleton(services =>
{
    var journal = services.GetRequiredService<ILoggerFactory>().CreateLogger<DiffusionListe>();
    return new DiffusionListe
    {
        SurErreur = ex => journal.LogWarning(ex, "Un écran n'a pas pu recevoir un changement de la liste d'épicerie."),
    };
});

// Photos des recettes : volume /data/photos dans l'image Docker, sinon un dossier local ignoré par git.
var dossierPhotos = builder.Configuration["Photos:Chemin"];
if (string.IsNullOrWhiteSpace(dossierPhotos))
{
    dossierPhotos = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "photos");
}
builder.Services.AddSingleton(new StockagePhotos(dossierPhotos));

// Import de recettes depuis un site web : un seul client HTTP, protégé contre les adresses du réseau local.
builder.Services.AddSingleton<TelechargementWeb>();
builder.Services.AddSingleton<IHacheurMotDePasse, HacheurMotDePasse>();
builder.Services.AddSingleton<ServiceConnexion>();
builder.Services.AddSingleton<ServicePremierCompte>();
builder.Services.AddSingleton<SuiviComptesActifs>();
builder.Services.AddSingleton<ServiceComptes>();

// Les cookies ne voyagent qu'en HTTPS. Auth:AllowInsecureHttp=true assouplit cette règle pour les
// essais en HTTP local seulement ; c'est le réglage par défaut en environnement Development.
var autoriserHttp = builder.Configuration.GetValue<bool?>("Auth:AllowInsecureHttp")
    ?? builder.Environment.IsDevelopment();
var politiqueCookies = autoriserHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Repas.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = politiqueCookies;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/connexion";
        options.AccessDeniedPath = "/connexion";
        options.ReturnUrlParameter = "retour";
        // Durée d'une session « rester connecté » ; prolongée tant que l'app est utilisée.
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;

        // À chaque requête : le cookie d'un compte désactivé (ou disparu) est refusé et retiré.
        options.Events.OnValidatePrincipal = async contexte =>
        {
            var suivi = contexte.HttpContext.RequestServices.GetRequiredService<SuiviComptesActifs>();
            var id = contexte.Principal?.ObtenirIdUtilisateur();
            if (id is null || !await suivi.EstActifAsync(id.Value, contexte.HttpContext.RequestAborted))
            {
                contexte.RejectPrincipal();
                await contexte.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Toute page ou tout point d'accès sans indication contraire exige une connexion.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = politiqueCookies);

// Les clés qui chiffrent le cookie de connexion doivent survivre aux redémarrages du conteneur,
// sinon chaque redémarrage déconnecte tout le monde.
var protectionDonnees = builder.Services.AddDataProtection().SetApplicationName("Repas");
var cheminCles = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(cheminCles))
{
    protectionDonnees.PersistKeysToFileSystem(new DirectoryInfo(cheminCles));
}

// Le proxy inverse termine le HTTPS. Son adresse dans le réseau Docker n'est pas connue d'avance,
// donc toute source est acceptée : le port 8080 ne doit être joignable que par le proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Remplace le fournisseur par défaut : revérifie le compte pendant qu'une page interactive reste ouverte.
builder.Services.AddScoped<AuthenticationStateProvider, EtatAuthentificationRevalide>();

var app = builder.Build();

// Prépare la base avant d'accepter des requêtes : création au besoin, migrations du schéma,
// puis données de base (catégories, unités, ingrédients courants), chacune appliquée une seule fois.
// Un échec arrête le démarrage, pour ne jamais servir l'app sur un schéma incomplet.
try
{
    await baseDeDonnees.AssurerExistenceAsync();

    var migrations = await new ExecuteurMigrations(
        chaineConnexion,
        Path.Combine(AppContext.BaseDirectory, "db", "migrations"),
        ExecuteurMigrations.TableSchema).AppliquerAsync();
    JournaliserScripts(app.Logger, "Migrations", migrations);

    var donneesDeBase = await new ExecuteurMigrations(
        chaineConnexion,
        Path.Combine(AppContext.BaseDirectory, "db", "seed"),
        ExecuteurMigrations.TableDonneesDeBase).AppliquerAsync();
    JournaliserScripts(app.Logger, "Données de base", donneesDeBase);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Démarrage interrompu, la base de données n'a pas pu être préparée : {Message}", ex.Message);
    return 1;
}

if (autoriserHttp)
{
    app.Logger.LogWarning(
        "Auth:AllowInsecureHttp est actif : les cookies peuvent voyager en HTTP. À ne jamais utiliser sur un déploiement public.");
}

// En premier : les étapes suivantes doivent voir le schéma (https) et l'adresse transmis par le proxy.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Les fichiers statiques (CSS, scripts) sont nécessaires à la page de connexion elle-même.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Photos des recettes. Comme tout le reste, elles exigent une connexion (politique par défaut).
// Un nom de fichier n'est jamais réutilisé : le navigateur peut garder la photo en cache sans limite.
app.MapGet("/photos/{nom}", (string nom, StockagePhotos stockage, HttpContext contexte) =>
{
    var chemin = stockage.TrouverChemin(nom);
    if (chemin is null)
    {
        return Results.NotFound();
    }

    contexte.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
    contexte.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(chemin, PhotoRecette.TypeContenu(nom));
});

app.MapPost("/deconnexion",async (HttpContext contexte, IAntiforgery antiforgery) =>
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

static void JournaliserScripts(ILogger journal, string serie, IReadOnlyList<string> appliques)
{
    if (appliques.Count == 0)
    {
        journal.LogInformation("{Serie} : à jour, aucun nouveau script.", serie);
    }
    else
    {
        journal.LogInformation(
            "{Serie} : {Nombre} script(s) appliqué(s) : {Scripts}", serie, appliques.Count, string.Join(", ", appliques));
    }
}
