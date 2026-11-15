-- Ingrédients et articles courants, déjà classés par catégorie.
--
-- TypeMesure est l'unité dans laquelle la liste d'épicerie additionne les quantités :
--   'Poids'  -> en g,     'Volume' -> en ml,     'Unite' -> à la pièce.
-- DensiteGParMl permet de convertir un volume en poids (ex. : 1 tasse de farine = 125 g, donc 0,5 g/ml).
-- PoidsMoyenUniteG permet de convertir une pièce en poids (ex. : 1 oignon ≈ 150 g).
-- Ces valeurs sont des moyennes de cuisine, modifiables dans la fiche de l'ingrédient.
--
-- Un ingrédient déjà présent (même nom) n'est pas touché. Une catégorie mal écrite arrête le script.

SET NOCOUNT ON;

DECLARE @Ingredients TABLE
(
    Nom              NVARCHAR(150) COLLATE French_CI_AS NOT NULL PRIMARY KEY,
    Categorie        NVARCHAR(100) COLLATE French_CI_AS NOT NULL,
    TypeMesure       VARCHAR(10)    NOT NULL,
    DensiteGParMl    DECIMAL(10, 4) NULL,
    PoidsMoyenUniteG DECIMAL(10, 2) NULL,
    EstAlimentaire   BIT            NOT NULL
);

INSERT INTO @Ingredients (Nom, Categorie, TypeMesure, DensiteGParMl, PoidsMoyenUniteG, EstAlimentaire)
VALUES
    -- Fruits et légumes
    (N'Oignon jaune',                 N'Fruits et légumes', 'Unite',  NULL, 150, 1),
    (N'Oignon rouge',                 N'Fruits et légumes', 'Unite',  NULL, 150, 1),
    (N'Oignon vert',                  N'Fruits et légumes', 'Unite',  NULL,  15, 1),
    -- Pour l'ail, l'unité est la gousse.
    (N'Ail',                          N'Fruits et légumes', 'Unite',  NULL,   5, 1),
    (N'Carotte',                      N'Fruits et légumes', 'Unite',  NULL,  70, 1),
    (N'Pomme de terre',               N'Fruits et légumes', 'Unite',  NULL, 200, 1),
    (N'Patate douce',                 N'Fruits et légumes', 'Unite',  NULL, 300, 1),
    (N'Céleri',                       N'Fruits et légumes', 'Unite',  NULL,  40, 1),
    (N'Tomate',                       N'Fruits et légumes', 'Unite',  NULL, 125, 1),
    (N'Tomate cerise',                N'Fruits et légumes', 'Poids',  0.60,  15, 1),
    (N'Poivron rouge',                N'Fruits et légumes', 'Unite',  NULL, 160, 1),
    (N'Poivron vert',                 N'Fruits et légumes', 'Unite',  NULL, 160, 1),
    (N'Concombre',                    N'Fruits et légumes', 'Unite',  NULL, 300, 1),
    (N'Courgette',                    N'Fruits et légumes', 'Unite',  NULL, 200, 1),
    (N'Brocoli',                      N'Fruits et légumes', 'Unite',  NULL, 350, 1),
    (N'Chou-fleur',                   N'Fruits et légumes', 'Unite',  NULL, 600, 1),
    (N'Laitue romaine',               N'Fruits et légumes', 'Unite',  NULL, 600, 1),
    (N'Épinards',                     N'Fruits et légumes', 'Poids',  0.12, NULL, 1),
    (N'Champignons blancs',           N'Fruits et légumes', 'Poids',  0.28,  18, 1),
    (N'Avocat',                       N'Fruits et légumes', 'Unite',  NULL, 170, 1),
    (N'Citron',                       N'Fruits et légumes', 'Unite',  NULL, 100, 1),
    (N'Lime',                         N'Fruits et légumes', 'Unite',  NULL,  65, 1),
    (N'Pomme',                        N'Fruits et légumes', 'Unite',  NULL, 180, 1),
    (N'Banane',                       N'Fruits et légumes', 'Unite',  NULL, 120, 1),
    (N'Orange',                       N'Fruits et légumes', 'Unite',  NULL, 150, 1),
    (N'Fraises',                      N'Fruits et légumes', 'Poids',  0.60,  12, 1),
    (N'Bleuets',                      N'Fruits et légumes', 'Poids',  0.60, NULL, 1),
    (N'Raisins',                      N'Fruits et légumes', 'Poids',  0.60, NULL, 1),
    (N'Gingembre frais',              N'Fruits et légumes', 'Poids',  NULL, NULL, 1),
    -- Pour les fines herbes fraîches, l'unité est la botte.
    (N'Coriandre fraîche',            N'Fruits et légumes', 'Unite',  NULL, NULL, 1),
    (N'Persil frais',                 N'Fruits et légumes', 'Unite',  NULL, NULL, 1),
    (N'Basilic frais',                N'Fruits et légumes', 'Unite',  NULL, NULL, 1),

    -- Boulangerie
    (N'Pain tranché',                 N'Boulangerie',       'Unite',  NULL, NULL, 1),
    (N'Baguette',                     N'Boulangerie',       'Unite',  NULL, NULL, 1),
    (N'Tortillas de blé',             N'Boulangerie',       'Unite',  NULL,  40, 1),
    (N'Pains à hamburger',            N'Boulangerie',       'Unite',  NULL, NULL, 1),
    (N'Pain pita',                    N'Boulangerie',       'Unite',  NULL, NULL, 1),

    -- Viandes
    (N'Poitrine de poulet',           N'Viandes',           'Poids',  NULL, 225, 1),
    (N'Cuisse de poulet désossée',    N'Viandes',           'Poids',  NULL, 100, 1),
    (N'Poulet entier',                N'Viandes',           'Poids',  NULL, 1800, 1),
    (N'Bœuf haché',                   N'Viandes',           'Poids',  NULL, NULL, 1),
    (N'Bœuf à ragoût',                N'Viandes',           'Poids',  NULL, NULL, 1),
    (N'Steak de surlonge',            N'Viandes',           'Poids',  NULL, 250, 1),
    (N'Porc haché',                   N'Viandes',           'Poids',  NULL, NULL, 1),
    (N'Filet de porc',                N'Viandes',           'Poids',  NULL, 450, 1),
    (N'Côtelette de porc',            N'Viandes',           'Poids',  NULL, 200, 1),
    (N'Dinde hachée',                 N'Viandes',           'Poids',  NULL, NULL, 1),
    (N'Bacon',                        N'Viandes',           'Poids',  NULL,  20, 1),
    (N'Jambon tranché',               N'Viandes',           'Poids',  NULL, NULL, 1),
    (N'Saucisse italienne',           N'Viandes',           'Poids',  NULL, 100, 1),

    -- Poissons
    (N'Filet de saumon',              N'Poissons',          'Poids',  NULL, 150, 1),
    (N'Filet de tilapia',             N'Poissons',          'Poids',  NULL, 120, 1),
    (N'Filet de morue',               N'Poissons',          'Poids',  NULL, 150, 1),
    (N'Crevettes crues décortiquées', N'Poissons',          'Poids',  NULL, NULL, 1),

    -- Produits laitiers (et œufs)
    (N'Lait 2 %',                     N'Produits laitiers', 'Volume', 1.03, NULL, 1),
    (N'Crème 35 %',                   N'Produits laitiers', 'Volume', 1.00, NULL, 1),
    (N'Crème 15 % à cuisson',         N'Produits laitiers', 'Volume', 1.01, NULL, 1),
    (N'Crème sure',                   N'Produits laitiers', 'Volume', 1.00, NULL, 1),
    (N'Beurre',                       N'Produits laitiers', 'Poids',  0.91, NULL, 1),
    (N'Œufs',                         N'Produits laitiers', 'Unite',  NULL,  50, 1),
    (N'Fromage cheddar',              N'Produits laitiers', 'Poids',  0.45, NULL, 1),
    (N'Fromage mozzarella',           N'Produits laitiers', 'Poids',  0.45, NULL, 1),
    (N'Fromage parmesan',             N'Produits laitiers', 'Poids',  0.40, NULL, 1),
    (N'Fromage à la crème',           N'Produits laitiers', 'Poids',  1.00, NULL, 1),
    (N'Yogourt nature',               N'Produits laitiers', 'Poids',  1.03, NULL, 1),

    -- Épicerie sèche
    (N'Farine tout usage',            N'Épicerie sèche',    'Poids',  0.50, NULL, 1),
    (N'Sucre blanc',                  N'Épicerie sèche',    'Poids',  0.80, NULL, 1),
    (N'Cassonade',                    N'Épicerie sèche',    'Poids',  0.88, NULL, 1),
    (N'Sucre à glacer',               N'Épicerie sèche',    'Poids',  0.50, NULL, 1),
    (N'Riz blanc à grain long',       N'Épicerie sèche',    'Poids',  0.75, NULL, 1),
    (N'Riz basmati',                  N'Épicerie sèche',    'Poids',  0.75, NULL, 1),
    (N'Pâtes penne',                  N'Épicerie sèche',    'Poids',  NULL, NULL, 1),
    (N'Spaghetti',                    N'Épicerie sèche',    'Poids',  NULL, NULL, 1),
    (N'Flocons d''avoine',            N'Épicerie sèche',    'Poids',  0.36, NULL, 1),
    (N'Chapelure',                    N'Épicerie sèche',    'Poids',  0.45, NULL, 1),
    (N'Poudre à pâte',                N'Épicerie sèche',    'Volume', NULL, NULL, 1),
    (N'Bicarbonate de soude',         N'Épicerie sèche',    'Volume', NULL, NULL, 1),
    (N'Pépites de chocolat',          N'Épicerie sèche',    'Poids',  0.68, NULL, 1),
    (N'Cacao en poudre',              N'Épicerie sèche',    'Poids',  0.36, NULL, 1),
    (N'Beurre d''arachide',           N'Épicerie sèche',    'Poids',  1.07, NULL, 1),
    (N'Noix de Grenoble',             N'Épicerie sèche',    'Poids',  0.48, NULL, 1),
    (N'Amandes',                      N'Épicerie sèche',    'Poids',  NULL, NULL, 1),
    (N'Huile d''olive',               N'Épicerie sèche',    'Volume', 0.92, NULL, 1),
    (N'Huile de canola',              N'Épicerie sèche',    'Volume', 0.92, NULL, 1),
    (N'Sirop d''érable',              N'Épicerie sèche',    'Volume', 1.32, NULL, 1),
    (N'Miel',                         N'Épicerie sèche',    'Poids',  1.42, NULL, 1),

    -- Conserves
    (N'Tomates en dés',               N'Conserves',         'Volume', NULL, NULL, 1),
    (N'Sauce tomate',                 N'Conserves',         'Volume', NULL, NULL, 1),
    (N'Pâte de tomates',              N'Conserves',         'Volume', NULL, NULL, 1),
    (N'Pois chiches',                 N'Conserves',         'Volume', NULL, NULL, 1),
    (N'Haricots noirs',               N'Conserves',         'Volume', NULL, NULL, 1),
    (N'Maïs en grains',               N'Conserves',         'Volume', NULL, NULL, 1),
    (N'Lait de coco',                 N'Conserves',         'Volume', NULL, NULL, 1),
    (N'Bouillon de poulet',           N'Conserves',         'Volume', NULL, NULL, 1),
    (N'Bouillon de bœuf',             N'Conserves',         'Volume', NULL, NULL, 1),
    -- Poids moyen : une boîte de 170 g.
    (N'Thon en conserve',             N'Conserves',         'Poids',  NULL, 170, 1),

    -- Surgelés
    (N'Petits pois surgelés',         N'Surgelés',          'Poids',  0.58, NULL, 1),
    (N'Légumes mélangés surgelés',    N'Surgelés',          'Poids',  0.55, NULL, 1),
    (N'Petits fruits surgelés',       N'Surgelés',          'Poids',  0.60, NULL, 1),
    (N'Frites surgelées',             N'Surgelés',          'Poids',  NULL, NULL, 1),

    -- Épices et condiments
    (N'Sel',                          N'Épices et condiments', 'Volume', 1.20, NULL, 1),
    (N'Poivre noir moulu',            N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Cumin moulu',                  N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Paprika',                      N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Poudre de chili',              N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Origan séché',                 N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Thym séché',                   N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Cannelle moulue',              N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Poudre d''ail',                N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Feuilles de laurier',          N'Épices et condiments', 'Unite',  NULL, NULL, 1),
    (N'Extrait de vanille',           N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Moutarde de Dijon',            N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Ketchup',                      N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Mayonnaise',                   N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Sauce soya',                   N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Sauce sriracha',               N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Vinaigre balsamique',          N'Épices et condiments', 'Volume', NULL, NULL, 1),
    (N'Vinaigre blanc',               N'Épices et condiments', 'Volume', NULL, NULL, 1),

    -- Boissons
    (N'Jus d''orange',                N'Boissons',          'Volume', NULL, NULL, 1),
    (N'Café moulu',                   N'Boissons',          'Poids',  NULL, NULL, 1),
    (N'Thé',                          N'Boissons',          'Unite',  NULL, NULL, 1),

    -- Entretien ménager (articles non alimentaires)
    (N'Papier essuie-tout',           N'Entretien ménager', 'Unite',  NULL, NULL, 0),
    (N'Savon à vaisselle',            N'Entretien ménager', 'Unite',  NULL, NULL, 0),
    (N'Tablettes pour lave-vaisselle', N'Entretien ménager', 'Unite', NULL, NULL, 0),
    (N'Détergent à lessive',          N'Entretien ménager', 'Unite',  NULL, NULL, 0),
    (N'Sacs à ordures',               N'Entretien ménager', 'Unite',  NULL, NULL, 0),
    (N'Pellicule plastique',          N'Entretien ménager', 'Unite',  NULL, NULL, 0),
    (N'Papier d''aluminium',          N'Entretien ménager', 'Unite',  NULL, NULL, 0),

    -- Hygiène (articles non alimentaires)
    (N'Papier de toilette',           N'Hygiène',           'Unite',  NULL, NULL, 0),
    (N'Dentifrice',                   N'Hygiène',           'Unite',  NULL, NULL, 0),
    (N'Shampoing',                    N'Hygiène',           'Unite',  NULL, NULL, 0),
    (N'Savon pour les mains',         N'Hygiène',           'Unite',  NULL, NULL, 0),
    (N'Déodorant',                    N'Hygiène',           'Unite',  NULL, NULL, 0);

-- Une catégorie absente indique une faute de frappe : mieux vaut échouer que perdre des ingrédients.
DECLARE @CategorieAbsente NVARCHAR(100) =
(
    SELECT TOP (1) i.Categorie
    FROM @Ingredients i
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Categorie c WHERE c.Nom = i.Categorie)
);
IF @CategorieAbsente IS NOT NULL
BEGIN
    DECLARE @Message NVARCHAR(300) = N'Catégorie introuvable dans les données de base : ' + @CategorieAbsente;
    THROW 50000, @Message, 1;
END

-- L'unité de référence est l'unité de base du type de mesure : g, ml ou unité.
INSERT INTO dbo.Ingredient (Nom, CategorieId, TypeMesure, UniteReferenceId, DensiteGParMl, PoidsMoyenUniteG, EstAlimentaire)
SELECT i.Nom, c.Id, i.TypeMesure, u.Id, i.DensiteGParMl, i.PoidsMoyenUniteG, i.EstAlimentaire
FROM @Ingredients i
JOIN dbo.Categorie c ON c.Nom = i.Categorie
JOIN dbo.Unite u ON u.TypeMesure = i.TypeMesure
                AND u.Code = CASE i.TypeMesure WHEN 'Poids' THEN N'g' WHEN 'Volume' THEN N'ml' ELSE N'unité' END
WHERE NOT EXISTS (SELECT 1 FROM dbo.Ingredient x WHERE x.Nom = i.Nom);
