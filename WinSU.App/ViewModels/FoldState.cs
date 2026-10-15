using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinSU.App.ViewModels;

/// <summary>
/// La section ouverte d'un « tableau déroulant » (étape 32, D-128 : Réglages, éditeur de thèmes) : une seule à la fois,
/// toutes fermées au départ. Confirmer sur un titre l'ouvre (l'autre se ferme) ou la referme ; Retour la referme et rend
/// le focus à son titre (<see cref="HeaderFocusRequested"/>, la vue s'en charge). Commun aux écrans qui en ont un : la
/// vue le donne à ses sections (<c>Controls/FoldSection</c>), le ViewModel s'en sert pour Retour.
/// </summary>
public sealed partial class FoldState : ObservableObject, IFocusRequester
{
    /// <summary>Clé de la section ouverte, ou null.</summary>
    [ObservableProperty]
    public partial object? Open { get; set; }

    /// <summary>Retour vient de refermer la section de cette clé : la vue donne le focus à son titre.</summary>
    public event EventHandler<object>? HeaderFocusRequested;

    // La demande de focus de l'écran (Views/FocusRequests) : la clé de la section dont le titre prend le focus
    event EventHandler<object>? IFocusRequester.FocusRequested
    {
        add => HeaderFocusRequested += value;
        remove => HeaderFocusRequested -= value;
    }

    /// <summary>Ouvre une section (depuis un autre écran, par exemple).</summary>
    public void Show(object key) => Open = key;

    /// <summary>Retour : referme la section ouverte et demande le focus pour son titre ; false s'il n'y en avait pas.</summary>
    public bool CloseOnBack()
    {
        if (Open is not { } key)
            return false;

        Open = null;
        HeaderFocusRequested?.Invoke(this, key);
        return true;
    }

    /// <summary>Confirmer ou un clic sur un titre : la section s'ouvre, ou se referme si elle l'était.</summary>
    [RelayCommand]
    private void Toggle(object? key) => Open = key is null || Equals(Open, key) ? null : key;
}
