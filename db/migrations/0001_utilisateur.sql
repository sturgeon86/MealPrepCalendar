-- Comptes des membres de la famille (un compte par personne).
-- Le courriel est enregistré en minuscules par l'application ; l'unicité ne dépend donc pas
-- de la collation du serveur.
CREATE TABLE dbo.Utilisateur
(
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Utilisateur PRIMARY KEY,
    Courriel       NVARCHAR(254)     NOT NULL CONSTRAINT UQ_Utilisateur_Courriel UNIQUE,
    NomAffiche     NVARCHAR(100)     NOT NULL,
    MotDePasseHash NVARCHAR(500)     NOT NULL,
    Actif          BIT               NOT NULL CONSTRAINT DF_Utilisateur_Actif DEFAULT 1,
    DateCreation   DATETIME2(0)      NOT NULL CONSTRAINT DF_Utilisateur_DateCreation DEFAULT SYSUTCDATETIME()
);
