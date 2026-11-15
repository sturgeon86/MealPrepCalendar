# Planificateur de repas

Application web familiale qui réunit un livre de recettes, un calendrier de repas
(dîner et souper) et une liste d'épicerie générée à partir des repas planifiés.
Elle est faite pour une famille, roule dans Docker et garde ses données dans SQL Server.

Pile : .NET 10, Blazor (Interactive Server), SQL Server 2017 Express (ADO.NET, sans ORM,
migrations SQL maison). La spécification est dans [docs/PROJET-REPAS.md](docs/PROJET-REPAS.md),
le plan de travail dans [PLAN.md](PLAN.md) et les règles du projet dans [CLAUDE.md](CLAUDE.md).

## Développement local

Exige Docker Desktop et le SDK .NET 10.

1. Copier `.env.example` vers `.env` et choisir un mot de passe pour `sa`.
2. Démarrer SQL Server et l'app :

   ```bash
   docker compose up -d --build
   ```

3. Ouvrir http://localhost:5180. Au premier démarrage, l'app propose de créer le premier compte.

Pour lancer l'app hors Docker (le conteneur SQL doit rouler) : copier
`src/Repas.Web/appsettings.Development.json.example` vers `appsettings.Development.json`,
y mettre le mot de passe de `.env`, puis :

```bash
dotnet run --project src/Repas.Web
```

## Tests

```bash
dotnet test
```

Les tests de `Repas.Domaine.Tests` n'ont besoin de rien. Ceux de `Repas.Donnees.Tests`
sont des tests d'intégration sur un vrai SQL Server : chaque test crée puis supprime sa
propre base temporaire. Le serveur vient de la variable `REPAS_TESTS_CONNEXION` ou, à
défaut, de `src/Repas.Web/appsettings.Development.json`.

## Configuration

Variables d'environnement du conteneur :

| Variable | Obligatoire | Rôle |
|---|---|---|
| `ConnectionStrings__Repas` | oui | Chaîne de connexion SQL Server (voir plus bas) |
| `TZ` | non | Fuseau horaire, ex. `America/Toronto` |
| `Auth__AllowInsecureHttp` | non (`false`) | `true` permet les cookies en HTTP simple. Essais locaux seulement |
| `DataProtection__KeysPath` | non (`/data/keys`, fixé dans l'image) | Dossier des clés qui chiffrent le cookie de connexion |
| `Photos__Chemin` | non (`/data/photos`, fixé dans l'image) | Dossier des photos de recettes (à venir) |

### Volumes

- `/data/keys` : clés de protection des données ASP.NET Core. À conserver, sinon chaque
  redémarrage du conteneur déconnecte tout le monde. Elles sont stockées sans chiffrement
  (l'app l'indique dans son journal) : garder ce dossier privé.
- `/data/photos` : photos des recettes.

Le conteneur roule sous l'utilisateur `app` (UID 1654) : un dossier monté de l'hôte doit
lui appartenir.

## Déploiement sur Unraid

### 1. Base de données

L'app utilise le SQL Server existant (2017 ou plus récent), avec un login dédié plutôt
que `sa`. Elle doit pouvoir créer ses tables : elle est donc propriétaire de sa base.

1. Ouvrir [deploy/create-database.sql](deploy/create-database.sql) et remplacer `CHANGE_ME`
   par un mot de passe robuste (sans `;` ni `'`).
2. L'exécuter une fois en tant que `sa`, par exemple avec SSMS, Azure Data Studio, ou depuis
   le terminal Unraid :

   ```bash
   docker exec -i <conteneur-sql> /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P '<mot de passe sa>' < create-database.sql
   ```

Les migrations (`db/migrations`) puis les données de base (`db/seed` : catégories, unités,
ingrédients courants) s'appliquent ensuite toutes seules au démarrage de l'app, chaque script
une seule fois. Un démarrage sans nouveau script ne change rien, et les données de base
modifiées dans l'app ne sont jamais écrasées. Si un script échoue, le conteneur s'arrête et
son journal donne la raison.

### 2. Conteneur

Chaque push sur `main` exécute les tests et, s'ils passent, publie
`ghcr.io/sturgeon86/mealprepcalendar:latest` (et une étiquette `sha-<commit>` pour revenir
en arrière). L'image est privée : Unraid doit être connecté à `ghcr.io` avec un jeton
en lecture seule.

**Connexion à GHCR (une seule fois).** Si c'est déjà fait pour WebBudgetCalendar, il n'y a
rien à refaire : le même jeton `read:packages` donne accès à tous tes paquets. Sinon :

1. Sur GitHub : **Settings → Developer settings → Personal access tokens →
   Tokens (classic) → Generate new token**, avec seulement la portée `read:packages`.
2. Dans le terminal Unraid :

   ```bash
   docker login ghcr.io -u sturgeon86
   ```

   Coller le jeton comme mot de passe. Docker le garde dans `/root/.docker`, qu'Unraid
   recrée au démarrage ; pour le rendre permanent :

   ```bash
   mkdir -p /boot/config/docker-auth && cp /root/.docker/config.json /boot/config/docker-auth/
   ```

   puis ajouter cette ligne à `/boot/config/go` (avant la ligne `emhttp`) :

   ```bash
   mkdir -p /root/.docker && cp /boot/config/docker-auth/config.json /root/.docker/
   ```

**Préparer les dossiers** dans le terminal Unraid :

```bash
mkdir -p /mnt/user/appdata/mealprepcalendar/keys /mnt/user/appdata/mealprepcalendar/photos
chown -R 1654:1654 /mnt/user/appdata/mealprepcalendar
```

**Créer le conteneur** (onglet **Docker → Add Container**) :

| Champ | Valeur |
|---|---|
| Name | `mealprepcalendar` |
| Repository | `ghcr.io/sturgeon86/mealprepcalendar:latest` |
| Network Type | le même réseau que le SQL Server et Nginx Proxy Manager si possible |
| Port | conteneur `8080` → un port libre de l'hôte, utilisé seulement par le proxy |
| Path | `/data/keys` → `/mnt/user/appdata/mealprepcalendar/keys` |
| Path | `/data/photos` → `/mnt/user/appdata/mealprepcalendar/photos` |
| Variable | `ConnectionStrings__Repas` → voir ci-dessous |
| Variable | `TZ` → `America/Toronto` |

Chaîne de connexion, avec le mot de passe choisi à l'étape 1 :

```
Server=<serveur>,1433;Database=Repas;User Id=repas;Password=<mot de passe>;TrustServerCertificate=True
```

`<serveur>` est le nom du conteneur SQL si les deux conteneurs partagent un réseau Docker
personnalisé, sinon l'adresse IP du serveur Unraid.

Au premier démarrage, le journal du conteneur doit afficher
`Migrations : … script(s) appliqué(s)` puis `Données de base : … script(s) appliqué(s)`. L'app attend ensuite le proxy HTTPS (story 7.3) :
en HTTP simple, elle refuse d'émettre son cookie de connexion.

### 3. Mise à jour

Après un push sur `main` et la fin du workflow GitHub, l'onglet Docker affiche
**update ready** sur le conteneur (ou utiliser **Check for Updates**) : un clic tire la
nouvelle image et recrée le conteneur. Les données restent dans SQL Server et les dossiers
`keys` et `photos`. Le plugin CA Auto Update peut le faire selon un horaire.

Pour revenir en arrière : dans la page **Edit** du conteneur, remplacer `latest` par une
étiquette `sha-<commit>` (liste sur la page du paquet dans GitHub), puis **Apply**.
Attention : une migration déjà appliquée par une version plus récente n'est pas défaite.
