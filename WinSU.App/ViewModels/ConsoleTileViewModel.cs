using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.App.Input;
using WinSU.Core.Catalog;

namespace WinSU.App.ViewModels;

/// <summary>Tuile de console (catalogue, grille des consoles, accueil). « Confirmer » appelle <paramref name="activate"/>.</summary>
public sealed partial class ConsoleTileViewModel(ConsoleDefinition console, Action<ConsoleTileViewModel>? activate = null)
    : ObservableObject, IActivatable
{
    public ConsoleDefinition Console { get; } = console;

    public string Name => Console.Name;

    /// <summary>« Nintendo · 2001 », ou ce qui est connu des deux.</summary>
    public string Subtitle { get; } = string.Join(" · ",
        new[] { console.Manufacturer, console.ReleaseYear?.ToString(CultureInfo.InvariantCulture) }
            .Where(part => !string.IsNullOrEmpty(part)));

    [ObservableProperty]
    public partial Bitmap? Image { get; set; }

    /// <summary>Dans le catalogue : la console est déjà dans la bibliothèque.</summary>
    [ObservableProperty]
    public partial bool IsAdded { get; set; }

    public void Activate() => activate?.Invoke(this);

    public void ReleaseImage()
    {
        var image = Image;
        Image = null;
        image?.Dispose();
    }
}

/// <summary>Dernière tuile de la grille des consoles : ouvre le catalogue.</summary>
public sealed class AddConsoleTileViewModel(Action activate) : IActivatable
{
    public void Activate() => activate();
}
