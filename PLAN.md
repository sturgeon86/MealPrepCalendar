# PLAN.md — Planificateur de repas

Plan de développement par itérations (Agile). Une story à la fois, validée avant de passer à la suivante.
Référence : `docs/PROJET-REPAS.md` (spécification) et `CLAUDE.md` (règles).

---

# Phase 1 — MVP

## Itération 0 — Fondations
- [x] **0.1 Solution .NET** : créer la solution et les projets (`Repas.Domaine`, `Repas.Donnees`, `Repas.Web`, projets de tests) selon la structure de `CLAUDE.md`.
  - *Critères :* `dotnet build` et `dotnet test` passent ; une page Blazor « Bonjour » s'affiche.
- [x] **0.2 Docker local** : `Dockerfile` multi-étapes et `docker-compose.yml` (app + SQL Server 2017).
  - *Critères :* `docker compose up` démarre l'app, qui se connecte à la base.
- [x] **0.3 Exécuteur de migrations** : table `VersionSchema`, lecture des scripts de `db/migrations/` en ordre, exécution au démarrage dans une transaction par script.
  - *Critères :* un script déjà appliqué n'est jamais réexécuté ; un échec arrête le démarrage avec un message clair ; tests d'intégration.
- [ ] **0.4 Couche d'accès aux données** : classe de base pour ouvrir les connexions, exécuter des requêtes paramétrées et gérer les transactions.
  - *Critères :* aucun ORM ; un test d'intégration lit et écrit une ligne.

## Itération 1 — Comptes et connexion
- [ ] **1.1 Table `Utilisateur`** (migration) et dépôt.
- [ ] **1.2 Connexion / déconnexion** par cookie, mots de passe hachés avec `PasswordHasher<T>`.
  - *Critères :* toutes les pages exigent une connexion ; mauvais mot de passe → message générique ; session persistante (« rester connecté »).
- [ ] **1.3 Premier compte** : au premier démarrage (aucun utilisateur), une page permet de créer le compte administrateur.
- [ ] **1.4 Gestion des comptes** : un utilisateur connecté peut créer un compte pour un membre de la famille, et chacun peut changer son mot de passe.
  - *Critères :* courriel unique ; un compte peut être désactivé.

## Itération 2 — Référentiel d'ingrédients
- [ ] **2.1 Tables** `Categorie`, `Unite`, `Ingredient`, `IngredientSynonyme` (migration).
- [ ] **2.2 Données de base** (`db/seed/`) : catégories avec ordre générique (Fruits et légumes, Boulangerie, Viandes, Poissons, Produits laitiers, Épicerie sèche, Conserves, Surgelés, Épices et condiments, Boissons, Entretien ménager, Hygiène…), unités courantes avec facteurs (g, kg, ml, l, tasse, c. à soupe, c. à thé, lb, oz, unité…), une centaine d'ingrédients courants.
  - *Critères :* les données de base s'insèrent une seule fois.
- [ ] **2.3 Logique de conversion** dans `Repas.Domaine` : même type de mesure (facteur), volume ↔ poids (densité), unité ↔ poids (poids moyen), conversion impossible signalée.
  - *Critères :* tests unitaires couvrant chaque cas, y compris les cas limites (densité absente, unité inconnue).
- [ ] **2.4 Écrans** : liste des ingrédients (recherche par nom et synonyme, filtre par catégorie), création et modification d'une fiche, gestion des catégories (ordre générique).
- [ ] **2.5 Fusion de doublons** : remplacer un ingrédient par un autre partout (recettes, listes), puis désactiver l'ancien.
  - *Critères :* fait dans une transaction ; aucune donnée perdue.

## Itération 3 — Livre de recettes
- [ ] **3.1 Tables** `Recette`, `RecetteIngredient`, `RecetteEtape`, `Etiquette`, `RecetteEtiquette`.
- [ ] **3.2 Saisie d'une recette** : nom, portions, temps, notes, ingrédients (recherche dans le référentiel avec synonymes, quantité, unité, précision, optionnel), étapes ordonnées, étiquettes.
  - *Critères :* un ingrédient absent peut être créé sans quitter la recette ; ordre des ingrédients et des étapes modifiable.
- [ ] **3.3 Photo de la recette** : téléversement et affichage (stockage sur un volume Docker).
- [ ] **3.4 Consultation** : fiche lisible sur cellulaire, avec un sélecteur de portions qui recalcule les quantités.
- [ ] **3.5 Liste et recherche** : par nom, étiquette, ingrédient, temps total.

## Itération 4 — Calendrier de repas
- [ ] **4.1 Table** `RepasPlanifie`.
- [ ] **4.2 Vue semaine** : 7 jours × Dîner / Souper, navigation semaine précédente / suivante.
- [ ] **4.3 Planifier un repas** : choisir une recette (recherche) et un nombre de portions, ou saisir un repas libre (restaurant, restes…).
  - *Critères :* RecetteId ou TexteLibre obligatoire ; déplacement et suppression possibles.
- [ ] **4.4 Affichage cellulaire** : la vue semaine reste utilisable sur petit écran (jours empilés).

## Itération 5 — Liste d'épicerie
- [ ] **5.1 Tables** `ListeEpicerie`, `ListeEpicerieArticle`, `ListeEpicerieArticleSource` (contrainte : une seule liste active).
- [ ] **5.2 Calcul de la liste** dans `Repas.Domaine` : portions, conversions, sommes, nombre de recettes, lignes séparées si conversion impossible.
  - *Critères :* tests unitaires sur des scénarios réalistes (ex. : 3 recettes de poulet, lait en ml et en tasses, farine sans densité).
- [ ] **5.3 Générer la liste** à partir d'une période du calendrier.
- [ ] **5.4 Regénérer** : recalcule les articles venant des repas, conserve les articles manuels, reportés et les cases cochées.
- [ ] **5.5 Ajout manuel** d'un article du référentiel (alimentaire ou non), avec quantité optionnelle.
- [ ] **5.6 Affichage par catégorie**, selon l'ordre générique, avec unités lisibles (1500 ml → 1,5 l) et alertes de conversion.

## Itération 6 — Mode magasin
- [ ] **6.1 Écran magasin** pour cellulaire : grandes cases à cocher, articles cochés grisés ou déplacés en bas, catégories repliables.
- [ ] **6.2 Temps réel** : un article coché sur un téléphone apparaît coché sur les autres (SignalR / Blazor Server).
- [ ] **6.3 Terminer la liste** : affiche les articles non cochés, permet de les reporter (tous ou un par un) sur la prochaine liste.
  - *Critères :* les articles reportés portent la mention *(reporté)* et ne disparaissent pas à la regénération.

## Itération 7 — Mise en production
- [ ] **7.1 Image Docker de production** : variables d'environnement, volume pour les photos, volume pour les clés de protection des données ASP.NET (sinon les sessions sont perdues à chaque redémarrage).
- [ ] **7.2 Déploiement Unraid** : modèle de conteneur, connexion au SQL Server existant, création de la base dédiée.
- [ ] **7.3 Nginx Proxy Manager** : sous-domaine, certificat HTTPS, prise en charge des WebSockets (nécessaire pour Blazor Server).
- [ ] **7.4 Sauvegarde** : script de sauvegarde de la base et du dossier des photos.

---

# Phase 2 — Supermarchés et rabais
- [ ] Tables `Supermarche`, `SupermarcheCategorieOrdre`, `Rabais`
- [ ] Gestion des supermarchés et de l'ordre des catégories pour chacun
- [ ] Mode magasin : choix de la vue **par catégorie** (ordre générique) ou **par supermarché** (ordre du magasin)
- [ ] Saisie manuelle des rabais (supermarché, prix, dates) et affichage dans la liste
- [ ] Copier une semaine du calendrier

# Phase 3 — Import de recettes
- [ ] Conteneur Ollama sur Unraid avec le GPU (plugin Nvidia Driver), modèle `qwen3-vl:8b`
- [ ] Photo → OCR → recette en JSON → écran de validation (ingrédients rapprochés du référentiel par nom et synonymes)
- [ ] Import depuis un site web (lecture des données structurées `schema.org/Recipe` quand le site en fournit)

# Phase 4 — Inventaire
- [ ] Table `Inventaire` et écran de gestion du garde-manger
- [ ] Déduire le stock de la liste d'épicerie générée
