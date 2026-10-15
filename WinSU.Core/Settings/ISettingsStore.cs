namespace WinSU.Core.Settings;

public interface ISettingsStore
{
    /// <summary>Réglages actuels. Valeurs par défaut tant que <see cref="LoadAsync"/> n'a pas été appelé.</summary>
    AppSettings Current { get; }

    /// <summary>Déclenché après chaque modification enregistrée sur le disque.</summary>
    event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>
    /// Charge <c>settings.json</c>. Crée le fichier s'il n'existe pas ; s'il est illisible,
    /// le met de côté et repart des valeurs par défaut.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applique <paramref name="update"/>, enregistre le résultat, puis déclenche <see cref="Changed"/>.
    /// Ne fait rien si le résultat est identique aux réglages actuels.
    /// </summary>
    Task UpdateAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default);
}

public sealed class SettingsChangedEventArgs(AppSettings previous, AppSettings current) : EventArgs
{
    public AppSettings Previous { get; } = previous;
    public AppSettings Current { get; } = current;
}
