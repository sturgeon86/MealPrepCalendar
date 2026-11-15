-- Catégories d'épicerie de base, dans l'ordre d'un trajet typique en magasin.
-- Les ordres vont de 10 en 10 pour pouvoir insérer une catégorie entre deux autres.
-- Une catégorie déjà présente (même nom) n'est pas touchée.

INSERT INTO dbo.Categorie (Nom, OrdreGenerique)
SELECT v.Nom, v.OrdreGenerique
FROM (VALUES
    (N'Fruits et légumes',     10),
    (N'Boulangerie',           20),
    (N'Viandes',               30),
    (N'Poissons',              40),
    (N'Produits laitiers',     50),
    (N'Épicerie sèche',        60),
    (N'Conserves',             70),
    (N'Surgelés',              80),
    (N'Épices et condiments',  90),
    (N'Boissons',             100),
    (N'Entretien ménager',    110),
    (N'Hygiène',              120)
) AS v (Nom, OrdreGenerique)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categorie c WHERE c.Nom = v.Nom);
