-- Liste d'épicerie : une seule liste active à la fois, partagée par la famille.
-- Ses articles viennent des repas planifiés d'une période (avec la trace de chaque repas source),
-- d'un ajout manuel, ou d'un report des articles non cochés de la liste précédente.

CREATE TABLE dbo.ListeEpicerie
(
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ListeEpicerie PRIMARY KEY,
    Nom              NVARCHAR(100) NOT NULL,
    -- Période du calendrier utilisée pour la génération ; vide tant que la liste n'a pas été générée
    -- (ex. : la prochaine liste, créée avec les articles reportés de la précédente).
    DateDebut        DATE NULL,
    DateFin          DATE NULL,
    DateGeneration   DATETIME2(0) NULL,
    Statut           VARCHAR(10) NOT NULL CONSTRAINT DF_ListeEpicerie_Statut DEFAULT 'Active'
        CONSTRAINT CK_ListeEpicerie_Statut CHECK (Statut IN ('Active', 'Terminee')),
    DateTerminaison  DATETIME2(0) NULL,
    -- Une seule liste active : 0 pour la liste active, son propre Id une fois terminée. La contrainte d'unicité
    -- empêche alors une deuxième liste active, sans index filtré (voir LeSchemaNUtiliseAucunIndexFiltre).
    CleActive        INT NOT NULL CONSTRAINT DF_ListeEpicerie_CleActive DEFAULT 0
        CONSTRAINT UQ_ListeEpicerie_UneSeuleActive UNIQUE,
    CreePar          INT NOT NULL CONSTRAINT FK_ListeEpicerie_Utilisateur REFERENCES dbo.Utilisateur (Id),
    DateCreation     DATETIME2(0) NOT NULL CONSTRAINT DF_ListeEpicerie_DateCreation DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_ListeEpicerie_Periode CHECK (
        (DateDebut IS NULL AND DateFin IS NULL)
        OR (DateDebut IS NOT NULL AND DateFin IS NOT NULL AND DateFin >= DateDebut)),
    CONSTRAINT CK_ListeEpicerie_Terminaison CHECK (
        (Statut = 'Active' AND CleActive = 0 AND DateTerminaison IS NULL)
        OR (Statut = 'Terminee' AND CleActive = Id AND DateTerminaison IS NOT NULL))
);

CREATE TABLE dbo.ListeEpicerieArticle
(
    Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ListeEpicerieArticle PRIMARY KEY,
    ListeId            INT NOT NULL
        CONSTRAINT FK_ListeEpicerieArticle_Liste REFERENCES dbo.ListeEpicerie (Id) ON DELETE CASCADE,
    -- Aliments et articles non alimentaires vivent dans le même référentiel : jamais de texte libre.
    IngredientId       INT NOT NULL CONSTRAINT FK_ListeEpicerieArticle_Ingredient REFERENCES dbo.Ingredient (Id),
    -- Quantité et unité vont ensemble ; toutes deux absentes pour « au goût » ou un article manuel sans quantité.
    -- Quand une conversion est impossible, le même ingrédient a une ligne par unité d'origine.
    QuantiteTotale     DECIMAL(18, 4) NULL CONSTRAINT CK_ListeEpicerieArticle_Quantite CHECK (QuantiteTotale > 0),
    UniteId            INT NULL CONSTRAINT FK_ListeEpicerieArticle_Unite REFERENCES dbo.Unite (Id),
    -- Nombre de recettes distinctes d'où vient l'article : « Poulet : 1,2 kg (3 recettes) ».
    NbRecettes         INT NOT NULL CONSTRAINT DF_ListeEpicerieArticle_NbRecettes DEFAULT 0
        CONSTRAINT CK_ListeEpicerieArticle_NbRecettes CHECK (NbRecettes >= 0),
    -- Manuels et reportés ne viennent pas d'un repas : une regénération les conserve tels quels.
    EstManuel          BIT NOT NULL CONSTRAINT DF_ListeEpicerieArticle_EstManuel DEFAULT 0,
    EstReporte         BIT NOT NULL CONSTRAINT DF_ListeEpicerieArticle_EstReporte DEFAULT 0,
    ReporteDeArticleId INT NULL CONSTRAINT FK_ListeEpicerieArticle_ReporteDe REFERENCES dbo.ListeEpicerieArticle (Id),
    EstCoche           BIT NOT NULL CONSTRAINT DF_ListeEpicerieArticle_EstCoche DEFAULT 0,
    CocheePar          INT NULL CONSTRAINT FK_ListeEpicerieArticle_CocheePar REFERENCES dbo.Utilisateur (Id),
    DateCoche          DATETIME2(0) NULL,
    CONSTRAINT CK_ListeEpicerieArticle_QuantiteEtUnite CHECK (
        (QuantiteTotale IS NULL AND UniteId IS NULL) OR (QuantiteTotale IS NOT NULL AND UniteId IS NOT NULL)),
    CONSTRAINT CK_ListeEpicerieArticle_Report CHECK (
        (EstReporte = 0 AND ReporteDeArticleId IS NULL) OR (EstReporte = 1 AND ReporteDeArticleId IS NOT NULL)),
    CONSTRAINT CK_ListeEpicerieArticle_Coche CHECK (
        (EstCoche = 0 AND CocheePar IS NULL AND DateCoche IS NULL)
        OR (EstCoche = 1 AND CocheePar IS NOT NULL AND DateCoche IS NOT NULL))
);

CREATE INDEX IX_ListeEpicerieArticle_ListeId ON dbo.ListeEpicerieArticle (ListeId);

-- Pour la fusion de doublons d'ingrédients.
CREATE INDEX IX_ListeEpicerieArticle_IngredientId ON dbo.ListeEpicerieArticle (IngredientId);

-- D'où vient chaque quantité d'un article : un repas planifié, avec la quantité qu'il demande
-- (dans l'unité de l'article). Sert à compter les recettes et à regénérer la liste.
CREATE TABLE dbo.ListeEpicerieArticleSource
(
    ArticleId       INT NOT NULL
        CONSTRAINT FK_ListeEpicerieArticleSource_Article REFERENCES dbo.ListeEpicerieArticle (Id) ON DELETE CASCADE,
    -- Un repas retiré du calendrier emporte sa trace ; la liste se corrige à la prochaine regénération.
    RepasPlanifieId INT NOT NULL
        CONSTRAINT FK_ListeEpicerieArticleSource_Repas REFERENCES dbo.RepasPlanifie (Id) ON DELETE CASCADE,
    Quantite        DECIMAL(18, 4) NULL CONSTRAINT CK_ListeEpicerieArticleSource_Quantite CHECK (Quantite > 0),
    UniteId         INT NULL CONSTRAINT FK_ListeEpicerieArticleSource_Unite REFERENCES dbo.Unite (Id),
    CONSTRAINT PK_ListeEpicerieArticleSource PRIMARY KEY (ArticleId, RepasPlanifieId),
    CONSTRAINT CK_ListeEpicerieArticleSource_QuantiteEtUnite CHECK (
        (Quantite IS NULL AND UniteId IS NULL) OR (Quantite IS NOT NULL AND UniteId IS NOT NULL))
);

CREATE INDEX IX_ListeEpicerieArticleSource_RepasPlanifieId ON dbo.ListeEpicerieArticleSource (RepasPlanifieId);
