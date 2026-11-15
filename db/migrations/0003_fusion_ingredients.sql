-- Fusion de doublons : un ingrédient fusionné est désactivé et garde la trace de celui qui le remplace.
-- Ainsi, rien n'est perdu : son nom reste connu et mène à l'ingrédient conservé.

ALTER TABLE dbo.Ingredient
    ADD FusionneDansId INT NULL
        CONSTRAINT FK_Ingredient_FusionneDans REFERENCES dbo.Ingredient (Id);
GO

ALTER TABLE dbo.Ingredient
    ADD CONSTRAINT CK_Ingredient_FusionneDans_PasLuiMeme CHECK (FusionneDansId <> Id),
        -- Un ingrédient fusionné n'est plus proposé ; le réactiver annule le lien de fusion.
        CONSTRAINT CK_Ingredient_FusionneDans_Inactif CHECK (FusionneDansId IS NULL OR Actif = 0);

-- Index ordinaire, pas filtré : un index filtré obligerait toute modification de la table à se faire
-- avec QUOTED_IDENTIFIER ON, ce que sqlcmd ne fait pas par défaut (scripts manuels, maintenance).
CREATE INDEX IX_Ingredient_FusionneDansId ON dbo.Ingredient (FusionneDansId);
