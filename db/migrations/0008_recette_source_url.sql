-- Import depuis un site web (story 8.4) : la recette garde l'adresse de la page d'où elle vient.

ALTER TABLE dbo.Recette ADD SourceUrl NVARCHAR(2000) NULL;
