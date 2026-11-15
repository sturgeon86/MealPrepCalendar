using System.Globalization;
using Repas.Domaine.Mesures;

namespace Repas.Domaine.Referentiel;

/// <summary>Règles d'une fiche d'ingrédient valide, vérifiées avant l'enregistrement.</summary>
public static class ValidationIngredient
{
    public const int LongueurMaxNom = 150;

    /// <summary>Bornes larges, pour attraper les fautes de frappe (ex. : 500 au lieu de 0,5).</summary>
    public const decimal DensiteMax = 30m;

    public const decimal PoidsMoyenMax = 50_000m;

    /// <summary>
    /// Comparaison des noms : insensible à la casse, sensible aux accents, comme la collation
    /// French_CI_AS de la base (« Poulet » = « poulet », mais « pâte » ≠ « pâté »).
    /// </summary>
    public static readonly StringComparer ComparateurNoms =
        StringComparer.Create(CultureInfo.GetCultureInfo("fr-CA"), CompareOptions.IgnoreCase);

    /// <summary>
    /// Nettoie les synonymes saisis : espaces retirés, lignes vides, doublons et synonymes
    /// identiques au nom de l'ingrédient enlevés. L'ordre de saisie est conservé.
    /// </summary>
    public static IReadOnlyList<string> NormaliserSynonymes(IEnumerable<string?> saisis, string nom)
    {
        ArgumentNullException.ThrowIfNull(saisis);
        var vus = new HashSet<string>(ComparateurNoms) { (nom ?? string.Empty).Trim() };
        var resultat = new List<string>();
        foreach (var saisi in saisis)
        {
            var synonyme = saisi?.Trim();
            if (!string.IsNullOrEmpty(synonyme) && vus.Add(synonyme))
            {
                resultat.Add(synonyme);
            }
        }

        return resultat;
    }

    /// <summary>Retourne les problèmes de la fiche, en phrases à afficher ; vide si elle est valide.</summary>
    /// <param name="unites">Toutes les unités du référentiel.</param>
    public static IReadOnlyList<string> Valider(FicheIngredient fiche, IReadOnlyCollection<Unite> unites)
    {
        ArgumentNullException.ThrowIfNull(fiche);
        ArgumentNullException.ThrowIfNull(unites);
        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(fiche.Nom))
        {
            erreurs.Add("Le nom est requis.");
        }
        else if (fiche.Nom.Trim().Length > LongueurMaxNom)
        {
            erreurs.Add($"Le nom ne doit pas dépasser {LongueurMaxNom} caractères.");
        }

        if (fiche.CategorieId <= 0)
        {
            erreurs.Add("La catégorie est requise.");
        }

        var unite = unites.FirstOrDefault(u => u.Id == fiche.UniteReferenceId);
        if (unite is null)
        {
            erreurs.Add("L'unité de référence est requise.");
        }
        else if (unite.TypeMesure != fiche.TypeMesure)
        {
            erreurs.Add($"L'unité de référence « {unite.Code} » ne correspond pas au type de mesure choisi.");
        }

        if (fiche.DensiteGParMl is { } densite && (densite <= 0 || densite > DensiteMax))
        {
            erreurs.Add($"La densité doit être plus grande que 0 et au plus {DensiteMax} g/ml.");
        }

        if (fiche.PoidsMoyenUniteG is { } poids && (poids <= 0 || poids > PoidsMoyenMax))
        {
            erreurs.Add($"Le poids moyen doit être plus grand que 0 et au plus {PoidsMoyenMax:N0} g.");
        }

        var tropLong = fiche.Synonymes.FirstOrDefault(s => s.Length > LongueurMaxNom);
        if (tropLong is not null)
        {
            erreurs.Add($"Le synonyme « {tropLong[..20]}… » dépasse {LongueurMaxNom} caractères.");
        }

        return erreurs;
    }
}
