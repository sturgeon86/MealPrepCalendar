namespace Repas.Domaine.Referentiel;

/// <summary>Catégorie d'épicerie (Fruits et légumes, Viandes…).</summary>
/// <param name="OrdreGenerique">Position dans le trajet en magasin, croissante.</param>
public sealed record Categorie(int Id, string Nom, int OrdreGenerique)
{
    public const int LongueurMaxNom = 100;
}

/// <summary>Sens d'un déplacement dans l'ordre des catégories.</summary>
public enum SensDeplacement
{
    /// <summary>Plus tôt dans le trajet en magasin.</summary>
    Haut,

    /// <summary>Plus tard dans le trajet en magasin.</summary>
    Bas,
}
