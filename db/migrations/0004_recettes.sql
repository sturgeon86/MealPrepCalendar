-- Livre de recettes : recettes, leurs ingrédients et leurs étapes, et les étiquettes pour les classer.
-- Toutes les recettes sont partagées par la famille ; CreePar garde seulement qui l'a saisie.

CREATE TABLE dbo.Recette
(
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Recette PRIMARY KEY,
    Nom                 NVARCHAR(200) COLLATE French_CI_AS NOT NULL,
    -- Nombre de portions que donnent les quantités de la recette.
    Portions            INT NOT NULL CONSTRAINT CK_Recette_Portions CHECK (Portions > 0),
    TempsPreparationMin INT NULL CONSTRAINT CK_Recette_TempsPreparationMin CHECK (TempsPreparationMin >= 0),
    TempsCuissonMin     INT NULL CONSTRAINT CK_Recette_TempsCuissonMin CHECK (TempsCuissonMin >= 0),
    Notes               NVARCHAR(MAX) NULL,
    -- Chemin relatif dans le dossier des photos (story 3.3).
    PhotoChemin         NVARCHAR(260) NULL,
    -- Comment la recette est entrée : saisie manuelle, import web ou photo (OCR).
    Source              VARCHAR(10) NOT NULL CONSTRAINT DF_Recette_Source DEFAULT 'Manuelle'
        CONSTRAINT CK_Recette_Source CHECK (Source IN ('Manuelle', 'Web', 'Ocr')),
    CreePar             INT NOT NULL CONSTRAINT FK_Recette_Utilisateur REFERENCES dbo.Utilisateur (Id),
    DateCreation        DATETIME2(0) NOT NULL CONSTRAINT DF_Recette_DateCreation DEFAULT SYSUTCDATETIME(),
    DateModification    DATETIME2(0) NOT NULL CONSTRAINT DF_Recette_DateModification DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_Recette_Nom ON dbo.Recette (Nom);

CREATE TABLE dbo.RecetteIngredient
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecetteIngredient PRIMARY KEY,
    RecetteId    INT NOT NULL
        CONSTRAINT FK_RecetteIngredient_Recette REFERENCES dbo.Recette (Id) ON DELETE CASCADE,
    IngredientId INT NOT NULL
        CONSTRAINT FK_RecetteIngredient_Ingredient REFERENCES dbo.Ingredient (Id),
    -- Quantité et unité vont ensemble ; toutes deux absentes pour « au goût ».
    -- L'unité peut être d'un autre type que celui de l'ingrédient (ex. : farine en tasses) : la liste convertit.
    Quantite     DECIMAL(18, 4) NULL CONSTRAINT CK_RecetteIngredient_Quantite CHECK (Quantite > 0),
    UniteId      INT NULL CONSTRAINT FK_RecetteIngredient_Unite REFERENCES dbo.Unite (Id),
    -- Précision libre (« haché finement »), sans effet sur la liste d'épicerie.
    Precision    NVARCHAR(200) NULL,
    EstOptionnel BIT NOT NULL CONSTRAINT DF_RecetteIngredient_EstOptionnel DEFAULT 0,
    Ordre        INT NOT NULL,
    CONSTRAINT CK_RecetteIngredient_QuantiteEtUnite
        CHECK ((Quantite IS NULL AND UniteId IS NULL) OR (Quantite IS NOT NULL AND UniteId IS NOT NULL)),
    CONSTRAINT UQ_RecetteIngredient_Ordre UNIQUE (RecetteId, Ordre)
);

-- Pour la fusion de doublons et la recherche de recettes par ingrédient.
CREATE INDEX IX_RecetteIngredient_IngredientId ON dbo.RecetteIngredient (IngredientId);

CREATE TABLE dbo.RecetteEtape
(
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecetteEtape PRIMARY KEY,
    RecetteId INT NOT NULL
        CONSTRAINT FK_RecetteEtape_Recette REFERENCES dbo.Recette (Id) ON DELETE CASCADE,
    Ordre     INT NOT NULL,
    Texte     NVARCHAR(2000) NOT NULL,
    CONSTRAINT UQ_RecetteEtape_Ordre UNIQUE (RecetteId, Ordre)
);

CREATE TABLE dbo.Etiquette
(
    Id  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Etiquette PRIMARY KEY,
    Nom NVARCHAR(50) COLLATE French_CI_AS NOT NULL CONSTRAINT UQ_Etiquette_Nom UNIQUE
);

CREATE TABLE dbo.RecetteEtiquette
(
    RecetteId   INT NOT NULL
        CONSTRAINT FK_RecetteEtiquette_Recette REFERENCES dbo.Recette (Id) ON DELETE CASCADE,
    EtiquetteId INT NOT NULL
        CONSTRAINT FK_RecetteEtiquette_Etiquette REFERENCES dbo.Etiquette (Id) ON DELETE CASCADE,
    CONSTRAINT PK_RecetteEtiquette PRIMARY KEY (RecetteId, EtiquetteId)
);

CREATE INDEX IX_RecetteEtiquette_EtiquetteId ON dbo.RecetteEtiquette (EtiquetteId);
