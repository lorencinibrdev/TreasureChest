using System.Text.RegularExpressions;

namespace TreasureChest.Source.Helpers;

public static partial class StringExtensions
{
    extension(string? source)
    {
        public string EscapeMarkup()
        {
            return string.IsNullOrWhiteSpace(source) 
                ? string.Empty 
                : System.Security.SecurityElement.Escape(source);
        }

        public string CleanHtmlDescription()
        {
            if (string.IsNullOrWhiteSpace(source)) 
                return string.Empty;

            var cleaned = source.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
            cleaned = RegexHtmlBr().Replace(cleaned, Environment.NewLine);
            cleaned = RegexHtmlLi().Replace(cleaned, $"{Environment.NewLine}• ");
            cleaned = RegexHtmlHeadersParagraphs().Replace(cleaned, Environment.NewLine);
            cleaned = RegexHtmlTags().Replace(cleaned, string.Empty);
            cleaned = System.Net.WebUtility.HtmlDecode(cleaned);
            cleaned = RegexTrimLines().Replace(cleaned, Environment.NewLine);
            cleaned = RegexHtmlExcessiveWhiteSpace().Replace(cleaned, $"{Environment.NewLine}{Environment.NewLine}");

            return cleaned.Trim();
        }
    }

    // Some pre-compiled regexes, for performance:
    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)] private static partial Regex RegexHtmlBr();
    [GeneratedRegex("<li[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex RegexHtmlLi();
    [GeneratedRegex("</p>|</h1>|</h2>|</h3>|</h4>|</ul>|</ol>|</div>", RegexOptions.IgnoreCase)] private static partial Regex RegexHtmlHeadersParagraphs();
    [GeneratedRegex("<.*?>")] private static partial Regex RegexHtmlTags();
    [GeneratedRegex(@"[ \t\u00A0]*\n[ \t\u00A0]*")] private static partial Regex RegexTrimLines();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex RegexHtmlExcessiveWhiteSpace();
}