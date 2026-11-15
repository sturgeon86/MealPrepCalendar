namespace Repas.Domaine.Mesures;

/// <summary>
/// Nature d'une mesure. Chaque type a son unité de base, de facteur 1 : g, ml ou unité.
/// Les noms correspondent aux valeurs de la colonne <c>TypeMesure</c> en base.
/// </summary>
public enum TypeMesure
{
    Poids,
    Volume,
    Unite,
}
