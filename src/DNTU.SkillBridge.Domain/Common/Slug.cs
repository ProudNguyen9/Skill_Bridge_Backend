using System.Globalization;
using System.Text;

namespace DNTU.SkillBridge.Domain.Common;

public static class SlugBuilder
{
    /// <summary>
    /// Builds a stable, URL-safe slug from any display name, including Vietnamese
    /// names with diacritics (e.g. "Kỹ thuật phần mềm" -> "ky-thuat-phan-mem").
    /// </summary>
    public static string Create(string value)
    {
        var source = value.Trim().Replace('đ', 'd').Replace('Đ', 'D');
        var builder = new StringBuilder(source.Length);
        var lastWasSeparator = true;

        foreach (var character in source.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Dots and apostrophes stay inside a token (ASP.NET Core -> aspnet-core).
            if (character is '.' or '\'' or '’')
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
