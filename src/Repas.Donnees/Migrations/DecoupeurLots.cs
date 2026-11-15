using System.Text.RegularExpressions;

namespace Repas.Donnees.Migrations;

/// <summary>
/// Découpe un script SQL en lots sur les lignes « GO », comme le font sqlcmd et SSMS.
/// SqlCommand ne comprend pas GO, et certaines instructions (CREATE VIEW, CREATE PROCEDURE…)
/// doivent être seules dans leur lot.
/// </summary>
/// <remarks>
/// Limite connue : une ligne contenant seulement « GO » à l'intérieur d'une chaîne ou d'un
/// commentaire sur plusieurs lignes est aussi traitée comme un séparateur.
/// </remarks>
public static partial class DecoupeurLots
{
    [GeneratedRegex(@"^[ \t]*GO[ \t]*;?[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SeparateurGo();

    public static IReadOnlyList<string> Decouper(string script)
    {
        return SeparateurGo()
            .Split(script)
            .Select(lot => lot.Trim())
            .Where(lot => lot.Length > 0)
            .ToList();
    }
}
