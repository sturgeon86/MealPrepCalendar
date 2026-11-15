-- Crée la base Repas et un login dédié à l'application.
-- À exécuter une seule fois en tant que sa (ou un autre sysadmin), sur SQL Server 2017 ou plus récent.
--
-- Avant d'exécuter : remplacer CHANGE_ME ci-dessous par un mot de passe robuste
-- (12 caractères ou plus ; éviter ; et ' parce qu'il ira dans une chaîne de connexion).
-- L'application crée ses tables elle-même au démarrage (migrations de db/migrations).

USE master;
GO

IF DB_ID(N'Repas') IS NULL
    CREATE DATABASE Repas;
GO

IF SUSER_ID(N'repas') IS NULL
    CREATE LOGIN repas WITH PASSWORD = N'CHANGE_ME', CHECK_POLICY = ON, DEFAULT_DATABASE = Repas;
GO

USE Repas;
GO

IF USER_ID(N'repas') IS NULL
    CREATE USER repas FOR LOGIN repas;
GO

-- L'application exécute ses propres migrations : elle est propriétaire de sa base (et de rien d'autre).
ALTER ROLE db_owner ADD MEMBER repas;
GO

SELECT DB_NAME() AS BaseDeDonnees, name AS Utilisateur
FROM sys.database_principals
WHERE name = N'repas';
GO
