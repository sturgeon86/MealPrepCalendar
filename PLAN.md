# PLAN.md — Planificateur de repas

Plan de développement par itérations (Agile). Une story à la fois, validée avant de passer à la suivante.
Référence : `docs/PROJET-REPAS.md` (spécification) et `CLAUDE.md` (règles).

**Ordre de travail :** les sections se font de haut en bas. Le déploiement sur Unraid (stories 7.1 à 7.3) est devancé juste après l'itération 1, pour voir l'avancement sur le serveur dès que la connexion existe. Les numéros de stories n'ont pas été changés. Une fois l'app déployée, le conteneur Unraid est mis à jour après chaque story validée.

---

# Phase 1 — MVP

## Itération 0 — Fondations
- [x] **0.1 Solution .NET** : créer la solution et les projets (`Repas.Domaine`, `Repas.Donnees`, `Repas.Web`, projets de tests) selon la structure de `CLAUDE.md`.
  - *Critères :* `dotnet build` et `dotnet test` passent ; une page Blazor « Bonjour » s'affiche.
- [x] **0.2 Docker local** : `Dockerfile` multi-étapes et `docker-compose.yml` (app + SQL Server 2017).
  - *Critères :* `docker compose up` démarre l'app, qui se connecte à la base.
- [x] **0.3 Exécuteur de migrations** : table `VersionSchema`, lecture des scripts de `db/migrations/` en ordre, exécution au démarrage dans une transaction par script.
  - *Critères :* un script déjà appliqué n'est jamais réexécuté ; un échec arrête le démarrage avec un message clair ; tests d'intégration.
- [x] **0.4 Couche d'accès aux données** : classe de base pour ouvrir les connexions, exécuter des requêtes paramétrées et gérer les transactions.
  - *Critères :* aucun ORM ; un test d'intégration lit et écrit une ligne.

## Itération 1 — Comptes et connexion
- [x] **1.1 Table `Utilisateur`** (migration) et dépôt.
- [x] **1.2 Connexion / déconnexion** par cookie, mots de passe hachés avec `PasswordHasher<T>`.
  - *Critères :* toutes les pages exigent une connexion ; mauvais mot de passe → message générique ; session persistante (« rester connecté »).
- [x] **1.3 Premier compte** : au premier démarrage (aucun utilisateur), une page permet de créer le compte administrateur.
- [x] **1.4 Gestion des comptes** : un utilisateur connecté peut créer un compte pour un membre de la famille, et chacun peut changer son mot de passe.
  - *Critères :* courriel unique ; un compte peut être désactivé.

## Itération 7 (devancée) — Mise en production
À faire dès que l'itération 1 est terminée : l'app ne doit pas être exposée sous le domaine avant que la connexion existe.
- [x] **7.1 Image Docker de production** : variables d'environnement, volume pour les photos, volume pour les clés de protection des données ASP.NET (sinon les sessions sont perdues à chaque redémarrage).
  - *Décidé :* même fonctionnement que WebBudgetCalendar. Chaque push sur `main` exécute les tests puis publie `ghcr.io/sturgeon86/mealprepcalendar` (étiquettes `latest` et `sha-<commit>`) par `.github/workflows/docker.yml`.
  - *Volumes :* `/data/keys` (clés de session) et `/data/photos` (utilisé à partir de la story 3.3).
- [x] **7.2 Déploiement Unraid** : modèle de conteneur, connexion au SQL Server existant, création de la base dédiée.
  - *Critères :* la mise à jour du conteneur vers une nouvelle version de l'image est documentée et se fait en une étape.

L'app tourne sur Unraid en réseau local seulement (`http://192.168.1.10:8096`, avec `Auth__AllowInsecureHttp=true`). L'exposition sous le domaine est reportée : les stories ci-dessous se font quand on décide d'ouvrir l'app sur Internet.
- [ ] **1.5 Limite des tentatives de connexion** : limiteur de débit d'ASP.NET Core sur la page de connexion (ex. : 5 essais par minute par adresse IP), avec un message clair quand la limite est atteinte.
  - *Critères :* fait avant la story 7.3 ; l'adresse du client vient de l'en-tête `X-Forwarded-For` transmis par le proxy.
- [ ] **7.3 Nginx Proxy Manager** : sous-domaine, certificat HTTPS, prise en charge des WebSockets (nécessaire pour Blazor Server).
  - *Critères :* `Auth__AllowInsecureHttp` retiré du conteneur Unraid ; délais de 1 h sur le proxy pour éviter les reconnexions.

## Itération 2 — Référentiel d'ingrédients
- [x] **2.1 Tables** `Categorie`, `Unite`, `Ingredient`, `IngredientSynonyme` (migration).
- [x] **2.2 Données de base** (`db/seed/`) : catégories avec ordre générique (Fruits et légumes, Boulangerie, Viandes, Poissons, Produits laitiers, Épicerie sèche, Conserves, Surgelés, Épices et condiments, Boissons, Entretien ménager, Hygiène…), unités courantes avec facteurs (g, kg, ml, l, tasse, c. à soupe, c. à thé, lb, oz, unité…), une centaine d'ingrédients courants.
  - *Critères :* les données de base s'insèrent une seule fois.
- [x] **2.3 Logique de conversion** dans `Repas.Domaine` : même type de mesure (facteur), volume ↔ poids (densité), unité ↔ poids (poids moyen), conversion impossible signalée.
  - *Critères :* tests unitaires couvrant chaque cas, y compris les cas limites (densité absente, unité inconnue).
- [x] **2.4 Écrans** : liste des ingrédients (recherche par nom et synonyme, filtre par catégorie), création et modification d'une fiche, gestion des catégories (ordre générique).
- [x] **2.5 Fusion de doublons** : remplacer un ingrédient par un autre partout (recettes, listes), puis désactiver l'ancien.
  - *Critères :* fait dans une transaction ; aucune donnée perdue.

## Itération 3 — Livre de recettes
- [x] **3.1 Tables** `Recette`, `RecetteIngredient`, `RecetteEtape`, `Etiquette`, `RecetteEtiquette`.
- [x] **3.2 Saisie d'une recette** : nom, portions, temps, notes, ingrédients (recherche dans le référentiel avec synonymes, quantité, unité, précision, optionnel), étapes ordonnées, étiquettes.
  - *Critères :* un ingrédient absent peut être créé sans quitter la recette ; ordre des ingrédients et des étapes modifiable.
- [x] **3.3 Photo de la recette** : téléversement et affichage (stockage sur un volume Docker).
- [x] **3.4 Consultation** : fiche lisible sur cellulaire, avec un sélecteur de portions qui recalcule les quantités.
- [x] **3.5 Liste et recherche** : par nom, étiquette, ingrédient, temps total.

## Itération 4 — Calendrier de repas
- [x] **4.1 Table** `RepasPlanifie`.
  - *Décidé :* une recette (avec portions) ou un texte libre, jamais les deux ; plusieurs repas possibles dans une même plage ; une recette planifiée ne peut pas être supprimée.
- [x] **4.2 Vue semaine** : 7 jours × Dîner / Souper, navigation semaine précédente / suivante.
- [x] **4.3 Planifier un repas** : choisir une recette (recherche) et un nombre de portions, ou saisir un repas libre (restaurant, restes…).
  - *Critères :* RecetteId ou TexteLibre obligatoire ; déplacement et suppression possibles.
- [x] **4.4 Affichage cellulaire** : la vue semaine reste utilisable sur petit écran (jours empilés).

## Itération 5 — Liste d'épicerie
- [x] **5.1 Tables** `ListeEpicerie`, `ListeEpicerieArticle`, `ListeEpicerieArticleSource` (contrainte : une seule liste active).
  - *Décidé :* une liste peut exister sans période (la prochaine liste, qui reçoit les articles reportés avant d'être générée) ; un repas retiré du calendrier emporte sa trace dans les sources ; la fusion d'ingrédients reporte aussi les articles des listes.
- [x] **5.2 Calcul de la liste** dans `Repas.Domaine` : portions, conversions, sommes, nombre de recettes, lignes séparées si conversion impossible.
  - *Critères :* tests unitaires sur des scénarios réalistes (ex. : 3 recettes de poulet, lait en ml et en tasses, farine sans densité).
- [x] **5.3 Générer la liste** à partir d'une période du calendrier.
- [x] **5.4 Regénérer** : recalcule les articles venant des repas, conserve les articles manuels, reportés et les cases cochées.
  - *Décidé :* une case cochée est décochée si la quantité a augmenté (ce qui a été acheté ne suffit plus) ; la période peut changer à la regénération.
- [x] **5.5 Ajout manuel** d'un article du référentiel (alimentaire ou non), avec quantité optionnelle.
- [x] **5.6 Affichage par catégorie**, selon l'ordre générique, avec unités lisibles (1500 ml → 1,5 l) et alertes de conversion.

## Itération 6 — Mode magasin
- [x] **6.1 Écran magasin** pour cellulaire : grandes cases à cocher, articles cochés grisés ou déplacés en bas, catégories repliables.
- [x] **6.2 Temps réel** : un article coché sur un téléphone apparaît coché sur les autres (SignalR / Blazor Server).
- [x] **6.3 Terminer la liste** : affiche les articles non cochés, permet de les reporter (tous ou un par un) sur la prochaine liste.
  - *Critères :* les articles reportés portent la mention *(reporté)* et ne disparaissent pas à la regénération.

---

# Phase 2 — Supermarchés et rabais
- [ ] Tables `Supermarche`, `SupermarcheCategorieOrdre`, `Rabais`
- [ ] Gestion des supermarchés et de l'ordre des catégories pour chacun
- [ ] Mode magasin : choix de la vue **par catégorie** (ordre générique) ou **par supermarché** (ordre du magasin)
- [ ] Saisie manuelle des rabais (supermarché, prix, dates) et affichage dans la liste
- [ ] Copier une semaine du calendrier

# Phase 3 — Import de recettes
Les trois façons d'importer (texte collé, site web, photo) produisent le même **brouillon de recette**, validé dans le même écran
avant l'enregistrement. On construit donc d'abord ce qui est commun (lecture d'une ligne d'ingrédient, rapprochement avec le
référentiel, écran de validation), puis les sources, de la plus simple à la plus exigeante. Aucune bibliothèque à ajouter :
`HttpClient` et `System.Text.Json` suffisent (Ollama et les données `schema.org` parlent JSON).

## Itération 8 — Import de recettes
- [x] **8.1 Lire une ligne d'ingrédient** (domaine) : « 1 ½ tasse de farine tout usage, tamisée » → quantité, unité, nom, précision.
  - *Critères :* tests sur des lignes réelles : fractions, unités au pluriel et abrégées (« tasses », « c. à s. », « c. à thé », « ml », « lb »), unités anglaises courantes (« cup », « tbsp »), « au goût », « 2 oignons », « 1 boîte (540 ml) de pois chiches ».
- [x] **8.2 Rapprocher un nom du référentiel** (domaine) : par nom et synonyme, sans égard à la casse, aux accents ni au pluriel (« oignons jaunes » → *Oignon jaune*) ; sinon des suggestions proches, ou « à créer ».
  - *Critères :* tests de rapprochement exact, approché et sans correspondance ; aucun choix silencieux quand deux ingrédients conviennent.
- [x] **8.3 Écran de validation d'un brouillon** : nom, portions, temps, étapes et ingrédients pré-remplis ; pour chaque ingrédient, le rapprochement proposé à confirmer, à changer ou à créer (création rapide) ; puis enregistrement comme une recette normale. Première source : **coller le texte** d'une recette (une ligne par ingrédient).
  - *Critères :* rien n'est enregistré avant la validation ; une ligne non rapprochée bloque l'enregistrement avec un message clair ; la recette garde sa source.
- [ ] **8.4 Import depuis un site web** : on colle l'adresse ; l'app lit les données `schema.org/Recipe` de la page (JSON-LD), y compris dans un `@graph` ; temps ISO 8601 (`PT1H30M`), portions (« 4 portions »), étapes en sections ; photo téléchargée dans le dossier des photos ; adresse d'origine gardée sur la recette (migration : `Recette.SourceUrl`).
  - *Critères :* tests sur des pages enregistrées de quelques sites québécois ; message clair si la page n'a pas de données de recette ; seulement `http`/`https`, délai et taille maximale ; jamais d'adresse du réseau local (le serveur ne doit pas servir de relais vers le réseau de la maison).
- [ ] **8.5 Ollama sur Unraid** : conteneur avec le GPU (plugin *Nvidia Driver*), modèle `qwen3-vl:8b` (plan B : `qwen2.5vl:7b`), documenté dans le README ; adresse dans `Ollama__Url` ; page de diagnostic dans l'app (Ollama joignable, modèle présent).
  - *Critères :* l'app fonctionne normalement sans Ollama (l'import par photo est simplement indisponible, avec un message).
- [ ] **8.6 Import par photo** : une ou plusieurs photos (recette sur deux pages) → Ollama, avec un schéma JSON imposé à la réponse (nom, portions, temps, ingrédients en texte, étapes) → brouillon → écran de validation. Les lignes d'ingrédients passent par 8.1 et 8.2, comme les autres sources.
  - *Critères :* indicateur d'attente (l'analyse peut prendre une minute), délai maximal, réponse invalide signalée sans planter ; la photo peut devenir celle de la recette.

# Phase 4 — Inventaire
- [ ] Table `Inventaire` et écran de gestion du garde-manger
- [ ] Déduire le stock de la liste d'épicerie générée

---

# Avant les vraies données — Sauvegarde
Déplacée à la fin à la demande (2026-11-16) : à faire avant de saisir de vraies recettes dans l'app.
- [ ] **7.4 Sauvegarde** : script de sauvegarde de la base et du dossier des photos.
  - *Critères :* en place avant de saisir de vraies données dans l'app.
