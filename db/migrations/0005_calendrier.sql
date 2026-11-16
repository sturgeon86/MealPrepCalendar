-- Calendrier de repas : un repas planifié à une date, au dîner ou au souper.
-- Le calendrier est partagé par la famille ; CreePar garde seulement qui a planifié le repas.
-- Une plage peut contenir plusieurs repas (ex. : plat principal et dessert), affichés dans l'ordre d'ajout.

CREATE TABLE dbo.RepasPlanifie
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepasPlanifie PRIMARY KEY,
    -- Date locale de la famille, sans heure.
    [Date]       DATE NOT NULL,
    Plage        VARCHAR(10) NOT NULL CONSTRAINT CK_RepasPlanifie_Plage CHECK (Plage IN ('Diner', 'Souper')),
    -- Une recette du livre, ou un texte libre pour un repas sans recette (restaurant, restes…), jamais les deux.
    -- Pas de suppression en cascade : une recette planifiée ne disparaît pas du calendrier en silence.
    RecetteId    INT NULL CONSTRAINT FK_RepasPlanifie_Recette REFERENCES dbo.Recette (Id),
    TexteLibre   NVARCHAR(200) NULL,
    -- Portions à préparer ; seulement pour une recette (la liste d'épicerie en dépend).
    Portions     INT NULL,
    CreePar      INT NOT NULL CONSTRAINT FK_RepasPlanifie_Utilisateur REFERENCES dbo.Utilisateur (Id),
    DateCreation DATETIME2(0) NOT NULL CONSTRAINT DF_RepasPlanifie_DateCreation DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_RepasPlanifie_RecetteOuTexte CHECK (
        (RecetteId IS NOT NULL AND TexteLibre IS NULL)
        OR (RecetteId IS NULL AND TexteLibre IS NOT NULL AND LEN(LTRIM(RTRIM(TexteLibre))) > 0)),
    -- « Portions IS NOT NULL » est nécessaire : NULL BETWEEN 1 AND 100 vaut UNKNOWN, ce qu'un CHECK accepte.
    CONSTRAINT CK_RepasPlanifie_Portions CHECK (
        (RecetteId IS NOT NULL AND Portions IS NOT NULL AND Portions BETWEEN 1 AND 100)
        OR (RecetteId IS NULL AND Portions IS NULL))
);

-- Affichage d'une semaine et génération de la liste d'épicerie : toujours par période.
CREATE INDEX IX_RepasPlanifie_Date ON dbo.RepasPlanifie ([Date], Plage);

-- Retrouver où une recette est planifiée (avant une éventuelle suppression de recette).
CREATE INDEX IX_RepasPlanifie_RecetteId ON dbo.RepasPlanifie (RecetteId);
