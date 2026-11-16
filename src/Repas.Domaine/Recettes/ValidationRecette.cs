using Repas.Domaine.Referentiel;

namespace Repas.Domaine.Recettes;

/// <summary>Règles d'une recette valide, vérifiées avant l'enregistrement.</summary>
public static class ValidationRecette
{
    public const int LongueurMaxNom = 200;
    public const int LongueurMaxPrecision = 200;
    public const int LongueurMaxEtape = 2000;
    public const int LongueurMaxEtiquette = 50;
    public const int PortionsMax = 100;

    /// <summary>Une semaine : borne large, pour attraper les fautes de frappe.</summary>
    public const int TempsMaxMin = 7 * 24 * 60;

    public const decimal QuantiteMax = 100_000m;

    /// <summary>La quantité maximale écrite en français (« 100 000 »), quelle que soit la culture du serveur.</summary>
    public static readonly string QuantiteMaxTexte = QuantiteMax.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("fr-CA"));

    /// <summary>Retire les étapes vides et les espaces autour de chacune, en gardant l'ordre.</summary>
    public static IReadOnlyList<string> NormaliserEtapes(IEnumerable<string?> etapes)
    {
        ArgumentNullException.ThrowIfNull(etapes);
        return etapes.Select(e => e?.Trim() ?? string.Empty).Where(e => e.Length > 0).ToList();
    }

    /// <summary>Retire les espaces, les étiquettes vides et les doublons (sans égard à la casse), en gardant l'ordre.</summary>
    public static IReadOnlyList<string> NormaliserEtiquettes(IEnumerable<string?> etiquettes)
    {
        ArgumentNullException.ThrowIfNull(etiquettes);
        var vues = new HashSet<string>(ValidationIngredient.ComparateurNoms);
        var resultat = new List<string>();
        foreach (var etiquette in etiquettes)
        {
            var nettoyee = etiquette?.Trim();
            if (!string.IsNullOrEmpty(nettoyee) && vues.Add(nettoyee))
            {
                resultat.Add(nettoyee);
            }
        }

        return resultat;
    }

    /// <summary>Retourne les problèmes de la recette, en phrases à afficher ; vide si elle est valide.</summary>
    public static IReadOnlyList<string> Valider(FicheRecette recette)
    {
        ArgumentNullException.ThrowIfNull(recette);
        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(recette.Nom))
        {
            erreurs.Add("Le nom de la recette est requis.");
        }
        else if (recette.Nom.Trim().Length > LongueurMaxNom)
        {
            erreurs.Add($"Le nom ne doit pas dépasser {LongueurMaxNom} caractères.");
        }

        if (recette.Portions is < 1 or > PortionsMax)
        {
            erreurs.Add($"Le nombre de portions doit être entre 1 et {PortionsMax}.");
        }

        if (recette.TempsPreparationMin is < 0 or > TempsMaxMin || recette.TempsCuissonMin is < 0 or > TempsMaxMin)
        {
            erreurs.Add($"Les temps doivent être entre 0 et {TempsMaxMin} minutes.");
        }

        for (var i = 0; i < recette.Ingredients.Count; i++)
        {
            var ligne = recette.Ingredients[i];
            var nom = string.IsNullOrEmpty(ligne.NomIngredient) ? $"la ligne {i + 1}" : $"« {ligne.NomIngredient} »";

            if (ligne.IngredientId <= 0)
            {
                // Une ligne importée pas encore rapprochée porte le texte lu : on le montre pour la retrouver.
                erreurs.Add(string.IsNullOrEmpty(ligne.NomIngredient)
                    ? $"Choisissez l'ingrédient de la ligne {i + 1}."
                    : $"Choisissez l'ingrédient de la ligne {i + 1} ({nom}) ou créez-le.");
            }

            if (ligne.Quantite is not null && ligne.UniteId is null)
            {
                erreurs.Add($"Choisissez l'unité de {nom}.");
            }
            else if (ligne.Quantite is null && ligne.UniteId is not null)
            {
                erreurs.Add($"Indiquez la quantité de {nom}, ou retirez l'unité pour « au goût ».");
            }

            if (ligne.Quantite is <= 0 or > QuantiteMax)
            {
                erreurs.Add($"La quantité de {nom} doit être plus grande que 0 et au plus {QuantiteMaxTexte}.");
            }

            if (ligne.Precision?.Length > LongueurMaxPrecision)
            {
                erreurs.Add($"La précision de {nom} ne doit pas dépasser {LongueurMaxPrecision} caractères.");
            }
        }

        var etapeTropLongue = recette.Etapes.Select((texte, i) => (texte, i)).FirstOrDefault(e => e.texte.Length > LongueurMaxEtape);
        if (etapeTropLongue.texte is not null)
        {
            erreurs.Add($"L'étape {etapeTropLongue.i + 1} ne doit pas dépasser {LongueurMaxEtape} caractères.");
        }

        var etiquetteTropLongue = recette.Etiquettes.FirstOrDefault(e => e.Length > LongueurMaxEtiquette);
        if (etiquetteTropLongue is not null)
        {
            erreurs.Add($"Une étiquette ne doit pas dépasser {LongueurMaxEtiquette} caractères.");
        }

        if (recette.SourceUrl is { } source
            && (source.Length > Import.AdresseImport.LongueurMax
                || !Uri.TryCreate(source, UriKind.Absolute, out var adresse)
                || (adresse.Scheme != Uri.UriSchemeHttp && adresse.Scheme != Uri.UriSchemeHttps)))
        {
            erreurs.Add("L'adresse d'origine de la recette n'est pas valide.");
        }

        // Le nom est toujours généré par l'app ; un autre nom indique une erreur de programmation.
        if (recette.PhotoChemin is not null && !PhotoRecette.EstNomValide(recette.PhotoChemin))
        {
            erreurs.Add("Le fichier de la photo n'est pas valide.");
        }

        return erreurs;
    }
}
