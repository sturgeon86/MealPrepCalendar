# Projet : Planificateur de repas (3 en 1)

> Version de travail : version 1.0, 2026-11-15
> Statut : concept validé, prêt pour le plan de développement

## 1. Vision

Un **calendrier de repas familial** pour simplifier la planification des repas. L'application réunit trois outils liés :

1. **Livre de recettes** : on y conserve nos recettes.
2. **Calendrier de repas** : on y planifie les repas de la semaine à partir des recettes.
3. **Liste d'épicerie** : elle se génère à partir des repas planifiés.

**Principe de base :**
`Ingrédients` → utilisés dans les → `Recettes` → planifiées dans le → `Calendrier` → qui génère la → `Liste d'épicerie`

Le **référentiel d'ingrédients** est la fondation : c'est lui qui permet de regrouper les quantités, de classer la liste par catégorie et de savoir où un produit est en rabais.

## 2. Modules

### 2.0 Référentiel d'ingrédients (la base de tout)
Une liste unique et partagée de tous les ingrédients et produits connus. Chaque recette et chaque article de la liste d'épicerie pointe vers un ingrédient de ce référentiel, jamais vers du texte libre.

**Fiche d'un ingrédient :**
- **Nom** (ex. : *Poitrine de poulet*)
- **Synonymes** (ex. : *poulet désossé*, *suprême de poulet*), pour retrouver l'ingrédient à la saisie et lors de l'OCR
- **Catégorie d'épicerie** (ex. : Viandes), qui sert au classement de la liste
- **Type de mesure** : poids, volume ou unité (à la pièce)
- **Unité de référence** pour les totaux (ex. : g, ml, unité)
- **Densité** (optionnelle) : pour convertir le volume en poids (ex. : 1 tasse de farine = 125 g)
- **Poids moyen à l'unité** (optionnel) : ex. : 1 oignon ≈ 150 g
- **Type** : alimentaire ou non alimentaire (papier de toilette, savon…). Les deux types vivent dans le **même référentiel**, car ce sont tous des articles à acheter pendant les courses (décidé)
- **Rabais** (optionnels) : supermarché, prix, date de fin

**Ingrédient dans une recette :**
- L'ingrédient (lien vers le référentiel)
- La quantité et l'unité utilisées dans la recette (ex. : *2 tasses*)
- Une précision libre (ex. : *haché finement*, *à température pièce*), sans effet sur la liste d'épicerie
- Optionnel ou non (ex. : *coriandre au goût*)

**Gestion :**
- Ajouter un ingrédient au vol pendant la saisie d'une recette (s'il n'existe pas encore)
- Fusionner deux ingrédients en double (ex. : *Lait 2 %* et *lait 2%*)
- Les **variantes sont des ingrédients distincts** (ex. : *Lait 2 %* et *Lait 3,25 %* sont deux fiches) : plus simple à gérer (décidé)
- Le référentiel démarre avec une liste de base d'ingrédients courants, déjà classés par catégorie

### 2.1 Livre de recettes
- Créer, modifier et supprimer une recette
- Champs : nom, portions, temps de préparation et de cuisson, ingrédients (quantité + unité), étapes, catégorie ou étiquettes, photo, notes
- Recherche et filtres (par catégorie, ingrédient, temps)
- Toutes les recettes sont **partagées** par la famille
- Trois façons d'ajouter une recette :
  1. **Saisie manuelle** (version 1)
  2. **Import depuis un site web** (souhait, phase ultérieure)
  3. **Photo + OCR avancé** : on photographie une recette (livre, fiche, écran), le texte est extrait et structuré (nom, ingrédients, étapes), puis l'utilisateur valide avant d'enregistrer (souhait, phase ultérieure)

### 2.2 Calendrier de repas
- Vue semaine (vue principale), et peut-être une vue mois
- Plages par jour : **dîner** et **souper** seulement
- Un seul calendrier **partagé** par la famille
- Associer une recette à une plage, en ajustant le nombre de portions
- Possibilité d'un repas « libre » sans recette (restaurant, restes, etc.)
- *À définir :* copier une semaine ? semaines types ?

### 2.3 Liste d'épicerie
- **Une seule liste active** à la fois, **partagée** par la famille (le choix du supermarché se fait à l'affichage, en mode magasin)
- **Report des articles non achetés** : quand on termine une liste, les articles non cochés peuvent être reportés sur la prochaine liste (tous, ou un par un)
- Générée à partir d'une période du calendrier (ex. : la semaine prochaine)
- **Regroupement des quantités** : les ingrédients identiques sont additionnés, après conversion vers une unité commune quand il le faut (liquides, produits secs, viandes, légumes, épices…)
  - Ex. : 250 ml + 1 tasse de lait = **500 ml de lait**
  - Le nombre de recettes concernées est indiqué : *Poulet : 1,2 kg (3 recettes)*
- Ajout manuel d'articles hors recettes (papier de toilette, savon, etc.)
- **Classement par catégorie** pour un trajet fluide en magasin (ex. : Fruits et légumes → Viandes → Produits laitiers → …)
  - Chaque ingrédient appartient à une catégorie (définie dans le référentiel)
  - **Ordre générique** des catégories, utilisé par défaut
  - **Ordre propre à chaque supermarché** (optionnel), selon la disposition des allées
- **Supermarché assigné (optionnel)** à un article : pour savoir où il est en rabais
  - Rabais en **saisie manuelle** pour le moment (supermarché, prix, date de fin)
  - Plus tard : brancher un service de circulaires (Reebee, Glouton ou autre) s'il offre une API
- **Mode magasin** : une interface simple sur le cellulaire pour cocher ce qu'on a pris
  - **Vue par catégorie** : toute la liste, classée selon l'ordre générique
  - **Vue par supermarché** : on choisit un supermarché, les catégories suivent l'ordre de ce magasin (et on voit les articles en rabais chez lui)

### 2.4 Inventaire du garde-manger (phase ultérieure)
- Savoir ce qu'on a déjà à la maison
- Retirer de la liste d'épicerie ce qui est déjà en stock

## 3. Utilisateurs
- **Un compte par personne** (connexion individuelle)
- Tout est **partagé** pour le moment : recettes, calendrier et liste d'épicerie
- Interface web **responsive**, utilisable sur le cellulaire en magasin

## 4. Technique (même pile que l'app budget-calendrier)
- Application web
- Langage : **C#**
- Bibliothèques **gratuites seulement**, **aucun ORM**
- Base de données : **SQL Server 2017 Express** (conteneur Docker existant sur Unraid), mise à niveau prévue plus tard
- Hébergement : **Docker sur Unraid**, sous un nom de domaine personnel, avec authentification
- Développement par étapes (Agile) avec **Claude Code**, dans un dépôt git

## 5. Phases (validées)

| Phase | Contenu |
|---|---|
| **1 – MVP** | Comptes, référentiel d'ingrédients (avec catégories et liste de base), recettes en saisie manuelle, calendrier dîner/souper, liste d'épicerie générée (regroupement + conversions + catégories), mode magasin avec cases à cocher |
| **2** | Supermarchés, ordre des catégories par supermarché, vue par supermarché, rabais en saisie manuelle, copie de semaine |
| **3** | Import de recettes web, photo + OCR |
| **4** | Inventaire du garde-manger |

## 6. OCR des recettes (photo)
- Le modèle d'IA roule **en local**, dans un **conteneur Docker séparé** sur Unraid : **Ollama**
- Le conteneur utilise le **GPU du serveur** (RTX 3060 Ti, 8 Go de VRAM) grâce au plugin *Nvidia Driver* d'Unraid
- Modèle visé : **qwen3-vl:8b** (gratuit, licence Apache 2.0, entre dans 8 Go de VRAM). Plan B : *MiniCPM-V* ou *qwen2.5vl:7b*
- L'application C# envoie la photo à l'API HTTP d'Ollama (réseau local) et reçoit la recette en JSON (nom, portions, ingrédients, étapes)
- L'utilisateur valide et corrige avant d'enregistrer
- Ça respecte la règle « gratuit seulement » : aucun service payant

## 7. Points d'attention
- **Conversions d'unités** : passer du volume au poids (ex. : tasse de farine → grammes) demande une densité par ingrédient. On peut commencer par convertir à l'intérieur d'un même type (volume ↔ volume, poids ↔ poids).
- **GPU partagé** : Plex (transcodage) et Immich (reconnaissance) peuvent aussi utiliser le GPU. Ollama décharge le modèle après une période d'inactivité, donc la VRAM est libérée entre deux imports.

## 8. Modèle de données (validé, à ajuster en cours de route)

SQL Server, accès par requêtes SQL écrites à la main (aucun ORM). Toutes les tables ont une clé `Id INT IDENTITY`, sauf mention contraire.

### Utilisateurs
- **Utilisateur** : Id, Courriel (unique), NomAffiche, MotDePasseHash, Actif, DateCreation

### Référentiel
- **Categorie** : Id, Nom, OrdreGenerique
- **Unite** : Id, Code (*g, kg, ml, l, tasse, c. à soupe, c. à thé, unité…*), Nom, TypeMesure (*Poids / Volume / Unité*), FacteurVersReference (ex. : tasse → 250 ml, kg → 1000 g)
- **Ingredient** : Id, Nom (unique), CategorieId, TypeMesure, UniteReferenceId, DensiteGParMl (nullable), PoidsMoyenUniteG (nullable), EstAlimentaire, Actif
- **IngredientSynonyme** : Id, IngredientId, Synonyme

### Recettes
- **Recette** : Id, Nom, Portions, TempsPreparationMin, TempsCuissonMin, Notes, PhotoChemin, Source (*manuelle / web / OCR*), CreePar (UtilisateurId), DateCreation, DateModification
- **RecetteIngredient** : Id, RecetteId, IngredientId, Quantite, UniteId, Precision (texte libre), EstOptionnel, Ordre
- **RecetteEtape** : Id, RecetteId, Ordre, Texte
- **Etiquette** : Id, Nom
- **RecetteEtiquette** : RecetteId, EtiquetteId (clé composée)

### Calendrier
- **RepasPlanifie** : Id, Date, Plage (*Dîner / Souper*), RecetteId (nullable), TexteLibre (nullable, pour un repas sans recette), Portions, CreePar
  - Contrainte : RecetteId **ou** TexteLibre doit être rempli

### Liste d'épicerie
- **ListeEpicerie** : Id, Nom, DateDebut, DateFin, DateGeneration, Statut (*Active / Terminée*)
- **ListeEpicerieArticle** : Id, ListeId, IngredientId, QuantiteTotale, UniteId, NbRecettes, EstManuel, EstReporte, ReporteDeArticleId (nullable), EstCoche, CocheePar, DateCoche
  - Contrainte : une seule liste au statut *Active* à la fois
- **ListeEpicerieArticleSource** : ArticleId, RepasPlanifieId, Quantite, UniteId
  - Garde la trace des repas d'où vient chaque quantité : sert à afficher *(3 recettes)* et à regénérer la liste sans perdre les articles manuels ni les cases cochées

### Supermarchés et rabais (phase 2)
- **Supermarche** : Id, Nom, Adresse, Actif
- **SupermarcheCategorieOrdre** : SupermarcheId, CategorieId, Ordre (clé composée)
- **Rabais** : Id, IngredientId, SupermarcheId, Prix, Description, DateDebut, DateFin, SaisiPar

### Plus tard
- **Inventaire** (phase 4) : IngredientId, Quantite, UniteId, DateMiseAJour

### Règles de calcul de la liste
1. Pour chaque repas de la période : quantité de la recette × (portions planifiées ÷ portions de la recette)
2. Conversion de chaque quantité vers l'unité de référence de l'ingrédient (via `FacteurVersReference`, puis la densité ou le poids moyen si le type de mesure diffère)
3. Somme par ingrédient, avec le nombre de recettes distinctes
4. Si une conversion est impossible (ex. : densité manquante), l'article reste sur une ligne séparée avec son unité d'origine, et une alerte invite à compléter la fiche de l'ingrédient
5. Affichage dans une unité lisible (ex. : 1500 ml → 1,5 l)
6. Les articles **reportés** et **manuels** ne viennent pas d'un repas : une regénération de la liste les conserve tels quels. Ils restent sur leur propre ligne, avec la mention *(reporté)* le cas échéant

### Fin d'une liste
1. L'utilisateur termine la liste (statut *Terminée*)
2. L'app affiche les articles non cochés et propose de les reporter
3. Les articles choisis sont copiés dans la prochaine liste (`EstReporte = 1`, `ReporteDeArticleId` = l'article d'origine)

## 9. Prochaines étapes
- [x] Définir le concept de base
- [x] Valider les phases
- [x] Répondre aux questions ouvertes
- [x] Esquisser le modèle de données (Ingrédient, Catégorie, Unité, Recette, RecetteIngrédient, RepasPlanifié, ListeÉpicerie, Supermarché, OrdreCatégorieSupermarché, Rabais…)
- [x] Produire le CLAUDE.md et le PLAN.md pour Claude Code
- [x] Créer le dépôt git (PROJET-REPAS.md dans `docs/`) et lancer l’itération 0
