-- Référentiel d'ingrédients : catégories d'épicerie, unités de mesure, ingrédients et leurs synonymes.
--
-- Les noms utilisent la collation French_CI_AS, fixée ici pour ne pas dépendre de celle du serveur :
-- insensible à la casse (« Poulet » = « poulet ») mais sensible aux accents (« pâte » ≠ « pâté »).
--
-- TypeMesure vaut 'Poids', 'Volume' ou 'Unite'. Chaque type a son unité de base : g, ml ou unité.

CREATE TABLE dbo.Categorie
(
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categorie PRIMARY KEY,
    Nom            NVARCHAR(100) COLLATE French_CI_AS NOT NULL CONSTRAINT UQ_Categorie_Nom UNIQUE,
    -- Position dans le trajet en magasin (ordre par défaut de la liste d'épicerie), croissante.
    OrdreGenerique INT NOT NULL
);

CREATE INDEX IX_Categorie_OrdreGenerique ON dbo.Categorie (OrdreGenerique);

CREATE TABLE dbo.Unite
(
    Id                   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Unite PRIMARY KEY,
    -- Abréviation affichée (g, kg, ml, tasse, c. à soupe…).
    Code                 NVARCHAR(20) COLLATE French_CI_AS NOT NULL CONSTRAINT UQ_Unite_Code UNIQUE,
    Nom                  NVARCHAR(50) NOT NULL,
    TypeMesure           VARCHAR(10) NOT NULL
        CONSTRAINT CK_Unite_TypeMesure CHECK (TypeMesure IN ('Poids', 'Volume', 'Unite')),
    -- Combien d'unités de base (g, ml ou unité) vaut 1 de cette unité. Ex. : kg = 1000, tasse = 250.
    FacteurVersReference DECIMAL(18, 6) NOT NULL
        CONSTRAINT CK_Unite_FacteurVersReference CHECK (FacteurVersReference > 0),
    -- Cible de la clé étrangère composée de Ingredient : garantit que l'unité de référence
    -- d'un ingrédient est du même type de mesure que lui.
    CONSTRAINT UQ_Unite_Id_TypeMesure UNIQUE (Id, TypeMesure)
);

CREATE TABLE dbo.Ingredient
(
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ingredient PRIMARY KEY,
    Nom              NVARCHAR(150) COLLATE French_CI_AS NOT NULL CONSTRAINT UQ_Ingredient_Nom UNIQUE,
    CategorieId      INT NOT NULL
        CONSTRAINT FK_Ingredient_Categorie REFERENCES dbo.Categorie (Id),
    TypeMesure       VARCHAR(10) NOT NULL
        CONSTRAINT CK_Ingredient_TypeMesure CHECK (TypeMesure IN ('Poids', 'Volume', 'Unite')),
    -- Unité dans laquelle les quantités sont additionnées sur la liste d'épicerie.
    UniteReferenceId INT NOT NULL,
    -- Pour convertir un volume en poids (ex. : farine ≈ 0,53 g/ml). Facultatif.
    DensiteGParMl    DECIMAL(10, 4) NULL
        CONSTRAINT CK_Ingredient_DensiteGParMl CHECK (DensiteGParMl > 0),
    -- Pour convertir une unité en poids (ex. : 1 oignon ≈ 150 g). Facultatif.
    PoidsMoyenUniteG DECIMAL(10, 2) NULL
        CONSTRAINT CK_Ingredient_PoidsMoyenUniteG CHECK (PoidsMoyenUniteG > 0),
    -- 0 pour les articles non alimentaires (papier de toilette, savon…), qui vivent dans le même référentiel.
    EstAlimentaire   BIT NOT NULL CONSTRAINT DF_Ingredient_EstAlimentaire DEFAULT 1,
    -- Un ingrédient n'est jamais supprimé : il est désactivé (ex. : après une fusion de doublons).
    Actif            BIT NOT NULL CONSTRAINT DF_Ingredient_Actif DEFAULT 1,
    CONSTRAINT FK_Ingredient_UniteReference_MemeType
        FOREIGN KEY (UniteReferenceId, TypeMesure) REFERENCES dbo.Unite (Id, TypeMesure)
);

CREATE INDEX IX_Ingredient_CategorieId ON dbo.Ingredient (CategorieId);

CREATE TABLE dbo.IngredientSynonyme
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IngredientSynonyme PRIMARY KEY,
    IngredientId INT NOT NULL
        CONSTRAINT FK_IngredientSynonyme_Ingredient REFERENCES dbo.Ingredient (Id) ON DELETE CASCADE,
    -- Unique dans tout le référentiel : un synonyme mène à un seul ingrédient (saisie et OCR).
    Synonyme     NVARCHAR(150) COLLATE French_CI_AS NOT NULL CONSTRAINT UQ_IngredientSynonyme_Synonyme UNIQUE
);

CREATE INDEX IX_IngredientSynonyme_IngredientId ON dbo.IngredientSynonyme (IngredientId);
