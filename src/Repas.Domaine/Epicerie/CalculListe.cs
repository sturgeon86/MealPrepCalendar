using Repas.Domaine.Mesures;
using Repas.Domaine.Recettes;

namespace Repas.Domaine.Epicerie;

/// <summary>Un repas planifié avec sa recette : ce qu'il faut acheter pour le préparer.</summary>
/// <param name="RepasPlanifieId">Le repas du calendrier, gardé comme source de chaque quantité.</param>
/// <param name="PortionsRecette">Portions que donnent les quantités de la recette.</param>
/// <param name="PortionsPlanifiees">Portions à préparer pour ce repas.</param>
public sealed record RepasACalculer(
    int RepasPlanifieId,
    int RecetteId,
    int PortionsRecette,
    int PortionsPlanifiees,
    IReadOnlyList<LigneIngredient> Ingredients);

/// <summary>La part d'un repas dans un article : sa quantité, dans l'unité de l'article.</summary>
/// <param name="Quantite"><c>null</c> pour un ingrédient « au goût » dans cette recette.</param>
public sealed record SourceArticle(int RepasPlanifieId, decimal? Quantite);

/// <summary>Une ligne calculée de la liste d'épicerie.</summary>
/// <param name="Quantite">Somme des quantités ; <c>null</c> si l'ingrédient n'est demandé qu'« au goût ».</param>
/// <param name="UniteId">
/// L'unité de référence de l'ingrédient, ou celle des quantités non converties (voir <see cref="CalculListe"/>) ;
/// <c>null</c> avec la quantité.
/// </param>
/// <param name="NbRecettes">Nombre de recettes distinctes d'où vient l'article (une recette planifiée deux fois compte une fois).</param>
/// <param name="Alerte">
/// Ce qui manque à la fiche de l'ingrédient pour regrouper cette ligne avec les autres ;
/// <see cref="RaisonEchecConversion.Aucune"/> pour une ligne convertie normalement.
/// </param>
public sealed record ArticleCalcule(
    int IngredientId,
    decimal? Quantite,
    int? UniteId,
    int NbRecettes,
    RaisonEchecConversion Alerte,
    IReadOnlyList<SourceArticle> Sources)
{
    /// <summary>Vrai si la ligne attend qu'on complète la fiche de l'ingrédient.</summary>
    public bool AUneAlerte => Alerte != RaisonEchecConversion.Aucune;
}

/// <summary>
/// Calcule la liste d'épicerie à partir des repas planifiés :
/// <list type="number">
/// <item>Quantité de la recette × (portions planifiées ÷ portions de la recette).</item>
/// <item>Conversion vers l'unité de référence de l'ingrédient (facteur, puis densité ou poids moyen).</item>
/// <item>Somme par ingrédient, avec le nombre de recettes distinctes.</item>
/// <item>
/// Conversion impossible : ligne séparée, avec la raison, pour compléter la fiche. Les quantités non converties
/// d'un même type de mesure s'additionnent quand même (2 tasses + 3 c. à soupe de farine sans densité) : dans leur
/// unité d'origine s'il n'y en a qu'une, sinon dans l'unité de base du type (ml, g ou unité).
/// </item>
/// <item>« Au goût » rejoint la ligne quantifiée du même ingrédient s'il y en a une ; sinon, une ligne sans quantité.</item>
/// </list>
/// Les ingrédients facultatifs ne sont pas ajoutés. Les quantités restent exactes : l'arrondi se fait à l'affichage.
/// </summary>
public sealed class CalculListe
{
    private readonly ConvertisseurUnites _convertisseur;
    private readonly IReadOnlyDictionary<int, MesuresIngredient> _mesures;

    /// <param name="mesures">Les mesures de chaque ingrédient des recettes, par Id d'ingrédient.</param>
    public CalculListe(ConvertisseurUnites convertisseur, IReadOnlyDictionary<int, MesuresIngredient> mesures)
    {
        _convertisseur = convertisseur ?? throw new ArgumentNullException(nameof(convertisseur));
        _mesures = mesures ?? throw new ArgumentNullException(nameof(mesures));
    }

    /// <summary>Les articles de la liste, par ingrédient ; pour un ingrédient, la ligne convertie d'abord.</summary>
    public IReadOnlyList<ArticleCalcule> Calculer(IEnumerable<RepasACalculer> repas)
    {
        ArgumentNullException.ThrowIfNull(repas);

        var groupes = new SortedDictionary<CleLigne, List<Entree>>();
        var auGout = new List<Entree>();

        // Rang de chaque repas reçu, pour ordonner les sources comme le calendrier.
        var ordreRepas = new Dictionary<int, int>();

        foreach (var unRepas in repas)
        {
            ordreRepas.TryAdd(unRepas.RepasPlanifieId, ordreRepas.Count);
            foreach (var ligne in unRepas.Ingredients.Where(l => !l.EstOptionnel))
            {
                var quantite = AffichageQuantite.MettreALEchelle(ligne.Quantite, unRepas.PortionsRecette, unRepas.PortionsPlanifiees);
                if (quantite is null || ligne.UniteId is null)
                {
                    // Rattaché à la fin, une fois toutes les lignes quantifiées connues.
                    auGout.Add(new Entree(unRepas.RecetteId, unRepas.RepasPlanifieId, null, null, RaisonEchecConversion.Aucune)
                    {
                        IngredientId = ligne.IngredientId,
                    });
                    continue;
                }

                var mesures = Mesures(ligne.IngredientId);
                var conversion = _convertisseur.VersReference(quantite.Value, ligne.UniteId.Value, mesures);
                if (conversion.Reussie)
                {
                    Ajouter(
                        groupes,
                        new CleLigne(ligne.IngredientId, RangLigne.Convertie, 0),
                        new Entree(unRepas.RecetteId, unRepas.RepasPlanifieId, conversion.Quantite, mesures.UniteReferenceId, RaisonEchecConversion.Aucune));
                    continue;
                }

                // Non convertie : regroupée avec les autres quantités du même type (ou de la même unité inconnue).
                var cle = _convertisseur.Trouver(ligne.UniteId.Value) is { } unite
                    ? new CleLigne(ligne.IngredientId, RangLigne.NonConvertie, (int)unite.TypeMesure)
                    : new CleLigne(ligne.IngredientId, RangLigne.UniteInconnue, ligne.UniteId.Value);
                Ajouter(
                    groupes,
                    cle,
                    new Entree(unRepas.RecetteId, unRepas.RepasPlanifieId, quantite.Value, ligne.UniteId.Value, conversion.Raisons));
            }
        }

        RattacherAuGout(groupes, auGout);

        return groupes.Select(g => Construire(g.Key, g.Value, ordreRepas)).ToList();
    }

    /// <summary>
    /// « Au goût » rejoint la première ligne quantifiée de l'ingrédient (la convertie d'abord) : la quantité achetée
    /// couvre aussi la pincée. Sinon, il garde sa ligne sans quantité.
    /// </summary>
    private static void RattacherAuGout(SortedDictionary<CleLigne, List<Entree>> groupes, List<Entree> auGout)
    {
        foreach (var entree in auGout)
        {
            var cible = groupes.Keys
                .Where(c => c.IngredientId == entree.IngredientId && c.Rang != RangLigne.AuGoutSeulement)
                .Select(c => (CleLigne?)c)
                .FirstOrDefault()
                ?? new CleLigne(entree.IngredientId, RangLigne.AuGoutSeulement, 0);
            Ajouter(groupes, cible, entree);
        }
    }

    private static void Ajouter(SortedDictionary<CleLigne, List<Entree>> groupes, CleLigne cle, Entree entree)
    {
        if (!groupes.TryGetValue(cle, out var entrees))
        {
            entrees = [];
            groupes.Add(cle, entrees);
        }

        entrees.Add(entree with { IngredientId = cle.IngredientId });
    }

    private ArticleCalcule Construire(CleLigne cle, List<Entree> entrees, Dictionary<int, int> ordreRepas)
    {
        var uniteFinale = UniteFinale(cle, entrees);
        decimal? EnUniteFinale(Entree e) => e.Quantite is { } q && e.UniteId is { } u ? VersUnite(q, u, uniteFinale!.Value, cle.IngredientId) : null;

        var quantifiees = entrees.Where(e => e.Quantite is not null).ToList();
        decimal? total = quantifiees.Count == 0 ? null : quantifiees.Sum(e => EnUniteFinale(e)!.Value);

        // Une source par repas, dans l'ordre des repas reçus (celui du calendrier) ; une recette peut demander
        // deux fois le même ingrédient (beurre pour la pâte et la garniture) : ses quantités s'additionnent.
        var sources = entrees
            .GroupBy(e => e.RepasPlanifieId)
            .OrderBy(g => ordreRepas[g.Key])
            .Select(g =>
            {
                var quantites = g.Select(EnUniteFinale).Where(q => q is not null).ToList();
                return new SourceArticle(g.Key, quantites.Count == 0 ? null : quantites.Sum());
            })
            .ToList();

        return new ArticleCalcule(
            cle.IngredientId,
            total,
            total is null ? null : uniteFinale,
            entrees.Select(e => e.RecetteId).Distinct().Count(),
            entrees.Aggregate(RaisonEchecConversion.Aucune, (raisons, e) => raisons | e.Alerte),
            sources);
    }

    /// <summary>L'unité de la ligne : la référence, l'unité unique d'origine, ou l'unité de base du type si elles diffèrent.</summary>
    private int? UniteFinale(CleLigne cle, List<Entree> entrees)
    {
        var unites = entrees.Where(e => e.UniteId is not null).Select(e => e.UniteId!.Value).Distinct().ToList();
        if (unites.Count <= 1)
        {
            return unites.Count == 0 ? null : unites[0];
        }

        // Seule une ligne non convertie peut mêler des unités : toutes du même type.
        var type = (TypeMesure)cle.Discriminant;
        return _convertisseur.UniteDeBase(type)?.Id
            ?? throw new InvalidOperationException($"Le référentiel n'a pas d'unité de base pour le type {type}.");
    }

    /// <summary>Conversion dans un même type de mesure : les facteurs suffisent, elle réussit toujours.</summary>
    private decimal VersUnite(decimal quantite, int uniteSourceId, int uniteCibleId, int ingredientId)
    {
        if (uniteSourceId == uniteCibleId)
        {
            return quantite;
        }

        var resultat = _convertisseur.Convertir(quantite, uniteSourceId, uniteCibleId, Mesures(ingredientId));
        return resultat.Reussie
            ? resultat.Quantite
            : throw new InvalidOperationException("Une conversion dans un même type de mesure ne devrait jamais échouer.");
    }

    private MesuresIngredient Mesures(int ingredientId) =>
        _mesures.TryGetValue(ingredientId, out var mesures)
            ? mesures
            : throw new InvalidOperationException($"Les mesures de l'ingrédient {ingredientId} n'ont pas été fournies au calcul.");

    /// <summary>Ordre des lignes d'un même ingrédient.</summary>
    private enum RangLigne
    {
        Convertie,
        NonConvertie,
        UniteInconnue,
        AuGoutSeulement,
    }

    /// <summary>
    /// Identifie une ligne : l'ingrédient, la sorte de ligne, puis le type de mesure (non convertie)
    /// ou l'unité (unité inconnue). L'ordre des clés est celui de la liste.
    /// </summary>
    private readonly record struct CleLigne(int IngredientId, RangLigne Rang, int Discriminant) : IComparable<CleLigne>
    {
        public int CompareTo(CleLigne autre) =>
            (IngredientId, (int)Rang, Discriminant).CompareTo((autre.IngredientId, (int)autre.Rang, autre.Discriminant));
    }

    /// <summary>Une quantité d'un repas, dans l'unité de référence (convertie) ou d'origine (non convertie).</summary>
    private sealed record Entree(int RecetteId, int RepasPlanifieId, decimal? Quantite, int? UniteId, RaisonEchecConversion Alerte)
    {
        public int IngredientId { get; init; }
    }
}
