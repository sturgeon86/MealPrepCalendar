# CLAUDE.md — Planificateur de repas (3 en 1)

Ce fichier guide Claude Code dans ce dépôt. Lis-le au début de chaque session.

## Le projet en bref
Application web familiale qui réunit un **livre de recettes**, un **calendrier de repas** (dîner et souper) et une **liste d'épicerie** générée à partir des repas planifiés.
La spécification complète est dans `docs/PROJET-REPAS.md`. Le plan de travail est dans `PLAN.md`.

**Chaîne de base :** Ingrédients → Recettes → Calendrier → Liste d'épicerie.
Le référentiel d'ingrédients est la fondation : aucune recette ni aucun article ne contient un ingrédient en texte libre.

## Règles non négociables
1. **Langage : C#.**
2. **Aucun ORM.** Pas d'Entity Framework, pas de Dapper, pas de micro-ORM. Accès aux données en ADO.NET avec `Microsoft.Data.SqlClient` et des requêtes SQL écrites à la main, **toujours paramétrées** (jamais de concaténation de valeurs dans le SQL).
3. **Bibliothèques gratuites seulement** (licence open source ou gratuite pour usage personnel). Demander avant d'ajouter tout package NuGet.
4. **SQL Server 2017 Express.** N'utiliser que des fonctions disponibles en 2017 (pas de `GREATEST`/`LEAST`, `IS DISTINCT FROM`, `DATE_BUCKET`, `GENERATE_SERIES`, etc.). Une mise à niveau viendra plus tard.
5. **Authentification obligatoire** sur toutes les pages, sauf la page de connexion.
6. **Travailler par petites étapes** : une user story à la fois, selon `PLAN.md`. Ne pas commencer la story suivante sans validation.

## Pile technique
- **.NET 10 (LTS)**, ASP.NET Core
- **Blazor Server** pour l'interface : la mise à jour en temps réel (SignalR) permet de voir, sur tous les cellulaires, les articles cochés par les autres en magasin
- Authentification : **cookie ASP.NET Core** avec une table `Utilisateur` maison. Hachage des mots de passe avec `PasswordHasher<T>` (inclus dans ASP.NET Core, sans EF). **Ne pas utiliser ASP.NET Core Identity avec EF.**
- Migrations de base de données : scripts SQL numérotés dans `db/migrations/` (`0001_initial.sql`, `0002_...`), appliqués au démarrage par un petit exécuteur maison qui tient une table `VersionSchema`. Chaque script est idempotent ou ne s'exécute qu'une fois.
- Tests : **xUnit**
- Conteneur : **Docker** (image Linux, build multi-étapes), déployé sur **Unraid**, derrière **Nginx Proxy Manager**, sous le domaine personnel
- Phase 3 : OCR par **Ollama** (conteneur séparé, GPU), appelé en HTTP

## Structure du dépôt (cible)
```
/
├─ CLAUDE.md
├─ PLAN.md
├─ docs/PROJET-REPAS.md          spécification
├─ db/migrations/                 scripts SQL numérotés
├─ db/seed/                       données de base (catégories, unités, ingrédients courants)
├─ src/
│  ├─ Repas.Domaine/              logique pure : conversions d'unités, calcul de la liste (aucune dépendance)
│  ├─ Repas.Donnees/              accès SQL (ADO.NET), un dépôt par agrégat
│  └─ Repas.Web/                  Blazor Server, authentification, pages
├─ tests/
│  ├─ Repas.Domaine.Tests/
│  └─ Repas.Donnees.Tests/        tests d'intégration sur une vraie base SQL Server
├─ Dockerfile
└─ docker-compose.yml             pour le développement local
```

## Conventions
- **Langue :** interface en **français (Québec)**. Noms de tables, colonnes et classes du domaine **en français, sans accents** (ex. : `RecetteIngredient`, `QuantiteTotale`), comme dans la spécification.
- **SQL :** une méthode par requête dans les dépôts ; lecture avec `SqlDataReader` et mappage manuel. Les opérations sur plusieurs tables se font dans une transaction.
- **Logique métier dans `Repas.Domaine`**, jamais dans les pages ni dans le SQL. Les conversions d'unités et le calcul de la liste d'épicerie doivent être couverts par des tests unitaires.
- **Quantités :** type `decimal`. Arrondi seulement à l'affichage.
- **Dates :** le calendrier utilise des dates locales (`DATE` en SQL, `DateOnly` en C#). Les horodatages (création, coche) sont en UTC (`DATETIME2`).
- **Secrets :** chaîne de connexion et clés en variables d'environnement, jamais dans le dépôt. Fournir un `appsettings.Development.json.example`.
- **Commits :** petits, un sujet à la fois, message en français.

## Règles métier à respecter (résumé)
- Plages de repas : **Dîner** et **Souper** seulement.
- Tout est partagé par la famille : recettes, calendrier, liste. Un compte par personne.
- **Une seule liste d'épicerie active** à la fois.
- Calcul de la liste :
  1. Quantité de la recette × (portions planifiées ÷ portions de la recette)
  2. Conversion vers l'unité de référence de l'ingrédient (facteur de l'unité, puis densité ou poids moyen si le type de mesure diffère)
  3. Somme par ingrédient + nombre de recettes distinctes (affiché : *Poulet : 1,2 kg (3 recettes)*)
  4. Conversion impossible → ligne séparée avec l'unité d'origine + alerte pour compléter la fiche
  5. Les articles **manuels** et **reportés** sont conservés lors d'une regénération
- Fin d'une liste : proposer de reporter les articles non cochés sur la prochaine liste.
- Les variantes (ex. : *Lait 2 %* et *Lait 3,25 %*) sont des ingrédients distincts.
- Les articles non alimentaires vivent dans le même référentiel que les ingrédients.

## Commandes
- Build : `dotnet build`
- Tests : `dotnet test` (les tests d'intégration exigent le SQL Server local : `docker compose up -d sql`, et `src/Repas.Web/appsettings.Development.json` ou la variable `REPAS_TESTS_CONNEXION`)
- Lancer en local : `docker compose up` (SQL Server + app), ou `dotnet run --project src/Repas.Web`

## À faire avant de dire qu'une story est terminée
- [ ] Le code compile sans avertissement nouveau
- [ ] Les tests passent (et de nouveaux tests couvrent la logique ajoutée)
- [ ] Les critères d'acceptation de la story dans `PLAN.md` sont vérifiés
- [ ] La story est cochée dans `PLAN.md`
