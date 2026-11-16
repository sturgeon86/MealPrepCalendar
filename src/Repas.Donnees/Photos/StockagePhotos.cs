using Repas.Domaine.Recettes;

namespace Repas.Donnees.Photos;

/// <summary>
/// Fichiers des photos de recettes, dans un dossier (un volume Docker en production).
/// La base ne garde que le nom du fichier (<c>Recette.PhotoChemin</c>), jamais un chemin complet.
/// </summary>
public sealed class StockagePhotos
{
    private readonly string _dossier;

    public StockagePhotos(string dossier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dossier);
        _dossier = Path.GetFullPath(dossier);
    }

    /// <summary>Le dossier des photos, en chemin absolu.</summary>
    public string Dossier => _dossier;

    /// <summary>Vérifie la photo puis l'écrit sous un nouveau nom unique.</summary>
    /// <returns>Le nom du fichier, à enregistrer dans <c>Recette.PhotoChemin</c>.</returns>
    /// <exception cref="PhotoInvalideException">Fichier vide, trop gros ou qui n'est pas une image acceptée.</exception>
    public async Task<string> EnregistrerAsync(ReadOnlyMemory<byte> contenu, CancellationToken annulation = default)
    {
        var format = PhotoRecette.Verifier(contenu.Span);
        var nom = PhotoRecette.NouveauNom(format);
        var chemin = Path.Combine(_dossier, nom);

        Directory.CreateDirectory(_dossier);
        try
        {
            // CreateNew : ne remplace jamais un fichier existant.
            await using var fichier = new FileStream(chemin, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await fichier.WriteAsync(contenu, annulation);
        }
        catch
        {
            SupprimerFichier(chemin);
            throw;
        }

        return nom;
    }

    /// <summary>Le chemin complet du fichier, ou <c>null</c> si le nom n'est pas valide ou si le fichier n'existe pas.</summary>
    public string? TrouverChemin(string? nom)
    {
        if (!PhotoRecette.EstNomValide(nom))
        {
            return null;
        }

        var chemin = Path.Combine(_dossier, nom!);
        return File.Exists(chemin) ? chemin : null;
    }

    /// <summary>
    /// Supprime le fichier s'il existe. Sans effet pour <c>null</c> ou un nom invalide. Une photo qu'on n'arrive pas
    /// à supprimer reste simplement sur le disque : l'enregistrement de la recette ne doit pas échouer pour autant.
    /// </summary>
    /// <returns><c>false</c> si le fichier existait mais n'a pas pu être supprimé.</returns>
    public bool Supprimer(string? nom)
    {
        if (!PhotoRecette.EstNomValide(nom))
        {
            return true;
        }

        return SupprimerFichier(Path.Combine(_dossier, nom!));
    }

    private static bool SupprimerFichier(string chemin)
    {
        try
        {
            File.Delete(chemin);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            // Pas de dossier, donc pas de fichier : rien à supprimer.
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
