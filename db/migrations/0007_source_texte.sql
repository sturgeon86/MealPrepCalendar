-- Import de recettes (story 8.3) : une recette peut venir d'un texte collé.
-- La recette garde sa source : 'Texte' s'ajoute aux sources permises.

ALTER TABLE dbo.Recette DROP CONSTRAINT CK_Recette_Source;

ALTER TABLE dbo.Recette ADD CONSTRAINT CK_Recette_Source
    CHECK (Source IN ('Manuelle', 'Web', 'Ocr', 'Texte'));
