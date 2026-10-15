using WinSU.App.ViewModels;
using WinSU.Core.Audio;
using Microsoft.Extensions.DependencyInjection;

namespace WinSU.App.Navigation;

public sealed class NavigationService(IServiceProvider services, IAudioService audio) : INavigationService
{
    private readonly Stack<ViewModelBase> _history = new();

    public ViewModelBase? Current { get; private set; }

    public bool CanGoBack => _history.Count > 0;

    public event EventHandler? CurrentChanged;

    public void NavigateTo<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase
    {
        var next = services.GetRequiredService<TViewModel>();
        configure?.Invoke(next);
        if (Current is not null)
        {
            Current.OnNavigatedFrom();
            _history.Push(Current);
        }

        Play(next.EnterSound);
        Show(next);
    }

    public void ResetTo<TViewModel>() where TViewModel : ViewModelBase
    {
        Current?.OnNavigatedFrom();
        _history.Clear();
        Show(services.GetRequiredService<TViewModel>());
    }

    public bool GoBack()
    {
        if (_history.Count == 0)
            return false;

        var leaving = Current;
        leaving?.OnNavigatedFrom();
        Play(leaving?.ExitSound);
        Show(_history.Pop());
        return true;
    }

    private void Play(UiSound? sound)
    {
        if (sound is { } value)
            audio.Play(value);
    }

    private void Show(ViewModelBase viewModel)
    {
        Current = viewModel;
        viewModel.OnNavigatedTo();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }
}
