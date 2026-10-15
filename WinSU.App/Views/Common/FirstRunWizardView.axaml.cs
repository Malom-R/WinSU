using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using WinSU.App.Input;
using WinSU.App.ViewModels.Wizard;

namespace WinSU.App.Views.Common;

public partial class FirstRunWizardView : UserControl
{
    private FirstRunWizardViewModel? _viewModel;

    public FirstRunWizardView()
    {
        InitializeComponent();

        // Étape de test : le ViewModel sait si la zone d'essai (la manette) a le focus (D-45).
        PadZone.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsKeyboardFocusWithinProperty && _viewModel is not null)
                _viewModel.IsPadFocused = PadZone.IsKeyboardFocusWithin;
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.TestButtonsRequested -= OnTestButtonsRequested;
        }

        _viewModel = DataContext as FirstRunWizardViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.TestButtonsRequested += OnTestButtonsRequested;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FirstRunWizardViewModel.Step))
            Dispatcher.UIThread.Post(FocusStep, DispatcherPriority.Loaded); // après la mise à jour de la visibilité
    }

    /// <summary>Place le focus sur l'élément principal de chaque étape, pour la manette.</summary>
    private void FocusStep()
    {
        Control? target = _viewModel?.Step switch
        {
            WizardStep.Test => PadZone,
            WizardStep.Theme => ThemeList.ContainerFromIndex(Math.Max(ThemeList.SelectedIndex, 0)),
            WizardStep.Done => FinishButton,
            _ => null,
        };

        if (target is ListBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) is ListBox list)
            list.SelectedItem = list.ItemFromContainer(item);
        target?.Focus(NavigationMethod.Directional);
    }

    /// <summary>Droite dans la zone d'essai : on passe au bouton « Valider », la suite naturelle du test.</summary>
    private void OnTestButtonsRequested(object? sender, EventArgs e) => FocusNavigator.Focus(ValidateTestButton);
}
