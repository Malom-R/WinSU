using System.Globalization;
using System.Resources;
using Avalonia.Markup.Xaml;

namespace WinSU.App.Localization;

/// <summary>
/// Accès aux textes de WinSU : <c>Localization/Strings(.fr).resx</c> (clés <c>win_*</c>, anglais et français ; étape 34 : plus
/// aucun texte d'iiSU). Une clé absente s'affiche entre crochets. La langue est fixée au démarrage : changer de langue
/// demande un redémarrage.
/// </summary>
public static class Tr
{
    private static readonly ResourceManager Own = new("WinSU.App.Localization.Strings", typeof(Tr).Assembly);

    public static string Get(string key) =>
        Own.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}

/// <summary>Extension XAML : <c>Text="{loc:Tr win_tab_settings}"</c>.</summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Tr.Get(Key);
}
