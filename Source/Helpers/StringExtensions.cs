namespace TreasureChest.Source.Helpers;

public static class StringExtensions
{
    public static string EscapeMarkup(this string? source)
    {
        return string.IsNullOrWhiteSpace(source) 
            ? string.Empty 
            : System.Security.SecurityElement.Escape(source);
    }
}