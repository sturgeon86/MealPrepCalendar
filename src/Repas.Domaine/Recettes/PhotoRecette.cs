using System.Text.RegularExpressions;

namespace Repas.Domaine.Recettes;

/// <summary>Formats d'image acceptés pour la photo d'une recette.</summary>
public enum FormatPhoto
{
    Jpeg,
    Png,
    Webp,
}

/// <summary>La photo est refusée : format non reconnu, fichier vide ou trop gros.</summary>
public sealed class PhotoInvalideException(string message) : Exception(message);

/// <summary>
/// Règles de la photo d'une recette : formats acceptés (reconnus par leur contenu, pas par l'extension),
/// taille maximale et noms de fichiers. Le nom est généré, jamais fourni par l'utilisateur.
/// </summary>
public static partial class PhotoRecette
{
    /// <summary>Taille maximale d'une photo reçue (le navigateur la réduit avant l'envoi).</summary>
    public const int TailleMaxOctets = 5 * 1024 * 1024;

    /// <summary>Plus grand côté, en pixels, de la photo réduite par le navigateur avant l'envoi.</summary>
    public const int DimensionMax = 1600;

    /// <summary>Reconnaît le format d'après les premiers octets du fichier ; <c>null</c> si ce n'est pas une image acceptée.</summary>
    public static FormatPhoto? DetecterFormat(ReadOnlySpan<byte> debut)
    {
        if (debut.Length >= 3 && debut[0] == 0xFF && debut[1] == 0xD8 && debut[2] == 0xFF)
        {
            return FormatPhoto.Jpeg;
        }

        ReadOnlySpan<byte> signaturePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (debut.StartsWith(signaturePng))
        {
            return FormatPhoto.Png;
        }

        // RIFF....WEBP
        if (debut.Length >= 12 && debut[..4].SequenceEqual("RIFF"u8) && debut[8..12].SequenceEqual("WEBP"u8))
        {
            return FormatPhoto.Webp;
        }

        return null;
    }

    /// <summary>Vérifie le contenu reçu et en déduit le format.</summary>
    /// <exception cref="PhotoInvalideException">Fichier vide, trop gros ou qui n'est pas une image acceptée.</exception>
    public static FormatPhoto Verifier(ReadOnlySpan<byte> contenu)
    {
        if (contenu.IsEmpty)
        {
            throw new PhotoInvalideException("Le fichier de la photo est vide.");
        }

        if (contenu.Length > TailleMaxOctets)
        {
            throw new PhotoInvalideException($"La photo dépasse {TailleMaxOctets / (1024 * 1024)} Mo.");
        }

        return DetecterFormat(contenu)
            ?? throw new PhotoInvalideException("Ce fichier n'est pas une image reconnue (JPEG, PNG ou WebP).");
    }

    /// <summary>Un nouveau nom de fichier unique, par exemple <c>3f2a…9c.jpg</c>.</summary>
    public static string NouveauNom(FormatPhoto format) => $"{Guid.NewGuid():N}{Extension(format)}";

    /// <summary>
    /// Vrai si le nom a la forme de ceux générés par <see cref="NouveauNom"/>. Protège le dossier des photos :
    /// aucun séparateur, aucun « .. », seulement des chiffres hexadécimaux et une extension connue.
    /// </summary>
    public static bool EstNomValide(string? nom) => nom is not null && NomValide().IsMatch(nom);

    /// <summary>Le type MIME à renvoyer au navigateur pour ce nom de fichier.</summary>
    public static string TypeContenu(string nom) => Path.GetExtension(nom) switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

    private static string Extension(FormatPhoto format) => format switch
    {
        FormatPhoto.Png => ".png",
        FormatPhoto.Webp => ".webp",
        _ => ".jpg",
    };

    [GeneratedRegex(@"^[0-9a-f]{32}\.(jpg|png|webp)\z",RegexOptions.CultureInvariant)]
    private static partial Regex NomValide();
}
