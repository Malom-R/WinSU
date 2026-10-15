using Avalonia.Data.Converters;
using Avalonia.Media;
using WinSU.Core.Appearance;

namespace WinSU.App.Themes;

/// <summary>Couleurs des thèmes (M14) dans les vues : pastilles, aperçus, carré du sélecteur.</summary>
public static class ThemeColorConverters
{
    public static readonly IValueConverter ToBrush =
        new FuncValueConverter<ThemeColor, IBrush>(color => new SolidColorBrush(ThemeResources.ToColor(color)));
}
