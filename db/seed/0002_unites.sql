-- Unités de mesure courantes.
-- FacteurVersReference : combien d'unités de base vaut 1 de cette unité.
-- Unités de base : g (poids), ml (volume), unité (à la pièce) ; leur facteur vaut 1.
-- Mesures de cuisine canadiennes métriques : tasse = 250 ml, c. à soupe = 15 ml, c. à thé = 5 ml.
-- Une unité déjà présente (même code) n'est pas touchée.

INSERT INTO dbo.Unite (Code, Nom, TypeMesure, FacteurVersReference)
SELECT v.Code, v.Nom, v.TypeMesure, v.FacteurVersReference
FROM (VALUES
    (N'g',          N'gramme',           'Poids',     1),
    (N'kg',         N'kilogramme',       'Poids',  1000),
    (N'lb',         N'livre',            'Poids',   453.59237),
    (N'oz',         N'once',             'Poids',    28.349523),
    (N'ml',         N'millilitre',       'Volume',    1),
    (N'l',          N'litre',            'Volume', 1000),
    (N'tasse',      N'tasse',            'Volume',  250),
    (N'c. à soupe', N'cuillère à soupe', 'Volume',   15),
    (N'c. à thé',   N'cuillère à thé',   'Volume',    5),
    (N'unité',      N'unité',            'Unite',     1),
    (N'douzaine',   N'douzaine',         'Unite',    12)
) AS v (Code, Nom, TypeMesure, FacteurVersReference)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Unite u WHERE u.Code = v.Code);
