namespace Repas.Domaine.Referentiel;

/// <summary>Ce qu'il faut savoir d'un ingrédient pour décider d'une fusion.</summary>
/// <param name="FusionneDansId">L'ingrédient qui le remplace déjà, s'il a été fusionné.</param>
public sealed record EtatFusion(int Id, string Nom, bool Actif, int? FusionneDansId);

/// <summary>Ingrédient conservé lors de la fusion d'un doublon.</summary>
public sealed record CibleFusion(int Id, string Nom);

/// <summary>
/// Fusionner un doublon dans un autre ingrédient : tout ce qui pointait vers le doublon pointe ensuite
/// vers l'ingrédient conservé, puis le doublon est désactivé.
/// </summary>
public static class RegleFusion
{
    /// <summary>La raison pour laquelle la fusion est impossible, ou <c>null</c> si elle peut se faire.</summary>
    /// <param name="doublon">L'ingrédient à faire disparaître ; <c>null</c> s'il n'existe pas.</param>
    /// <param name="conserve">L'ingrédient à garder ; <c>null</c> s'il n'existe pas.</param>
    public static string? Verifier(EtatFusion? doublon, EtatFusion? conserve)
    {
        if (doublon is null || conserve is null)
        {
            return "L'un des deux ingrédients n'existe plus.";
        }

        if (doublon.Id == conserve.Id)
        {
            return "Un ingrédient ne peut pas être fusionné avec lui-même.";
        }

        if (doublon.FusionneDansId is not null)
        {
            return $"« {doublon.Nom} » a déjà été fusionné dans un autre ingrédient.";
        }

        if (!conserve.Actif || conserve.FusionneDansId is not null)
        {
            return $"L'ingrédient à garder, « {conserve.Nom} », doit être actif.";
        }

        return null;
    }
}

/// <summary>La fusion demandée ne respecte pas les règles ; rien n'a été modifié.</summary>
public sealed class FusionImpossibleException(string message) : Exception(message);
