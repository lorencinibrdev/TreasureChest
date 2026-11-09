using NGettext;
using TreasureChest.Source.Services;

namespace TreasureChest;

public static class I18n
{
    // Private Members:
    private static readonly Catalog Catalog;

    static I18n()
    {
        var localeDir = Path.Combine(SettingsService.DataDirectory, "locale");
        Catalog = new Catalog("TreasureChest", localeDir);
    }

    public static string _(string text)
    {
        return Catalog.GetString(text);
    }

    public static string _(string text, params object[] args)
    {
        return Catalog.GetString(text, args);
    }
}