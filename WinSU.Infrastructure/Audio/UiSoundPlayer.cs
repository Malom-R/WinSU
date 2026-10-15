using System.Diagnostics;
using WinSU.Core.Audio;
using WinSU.Core.Settings;
using WinSU.Core.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WinSU.Infrastructure.Audio;

/// <summary>
/// Sons d'interface (Kenney, CC0 : Assets/Sounds/CREDITS.txt) : préchargés, mixés sur une sortie WASAPI partagée (latence
/// ~50 ms) qui suit le périphérique par défaut. Démarré par l'hôte ; sans périphérique audio, il se désactive simplement.
/// </summary>
public sealed class UiSoundPlayer(IAssetLocator assets, ISettingsStore settings, ILogger<UiSoundPlayer> logger)
    : IAudioService, IHostedService, IDisposable
{
    private const int LatencyMs = 50;

    /// <summary>Un même son relancé plus vite que cela (répétition d'une direction) n'est pas rejoué.</summary>
    private static readonly TimeSpan MinimumRepeatInterval = TimeSpan.FromMilliseconds(45);

    /// <summary>
    /// Fichier de chaque son et son niveau en décibels : Kenney livre ses sons à −1 dB de crête, ramenés ici au niveau des
    /// anciens sons (le déplacement, joué sans cesse, plus bas que le reste).
    /// </summary>
    internal static readonly IReadOnlyDictionary<UiSound, UiSoundFile> Files = new Dictionary<UiSound, UiSoundFile>
    {
        [UiSound.Navigate] = new("navigate.ogg", -15f),
        [UiSound.Open] = new("open.ogg", -9f),
        [UiSound.Close] = new("close.ogg", -9f),
        [UiSound.ContextMenu] = new("context_menu.ogg", -8f),
        [UiSound.EnterConsole] = new("enter_console.ogg", -8f),
        [UiSound.ExitConsole] = new("exit_console.ogg", -8f),
        [UiSound.LaunchGame] = new("launch.ogg", -9f),
    };

    private readonly Dictionary<UiSound, float[]> _samples = [];
    private readonly Dictionary<UiSound, long> _lastPlayed = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private volatile MixingSampleProvider? _mixer;
    private WasapiPlayer? _output;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Hors du démarrage : ouvrir le périphérique audio peut prendre un moment.
        _ = Task.Run(InitializeAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _mixer = null;
        _output?.Stop();
        _output?.Dispose();
        _output = null;
    }

    public void Play(UiSound sound)
    {
        var audio = settings.Current.Audio;
        var mixer = _mixer;
        if (!audio.UiSounds || mixer is null || !_samples.TryGetValue(sound, out var samples))
            return;

        lock (_lastPlayed)
        {
            var now = _clock.ElapsedMilliseconds;
            if (_lastPlayed.TryGetValue(sound, out var last) && now - last < MinimumRepeatInterval.TotalMilliseconds)
                return;
            _lastPlayed[sound] = now;
        }

        mixer.AddMixerInput(new CachedSoundProvider(samples, (float)Math.Clamp(audio.UiVolume, 0, 1)));
    }

    private async Task InitializeAsync()
    {
        var loaded = new Dictionary<UiSoundFile, float[]>(); // un fichier partagé par deux sons n'est lu qu'une fois
        foreach (var (sound, file) in Files)
        {
            var path = assets.Resolve("Sounds/" + file.Name);
            try
            {
                if (!loaded.TryGetValue(file, out var samples))
                    loaded[file] = samples = SoundBank.Load(path, SoundBank.FromDecibels(file.Decibels));
                _samples[sound] = samples;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or NotSupportedException or InvalidDataException)
            {
                logger.LogWarning(ex, "Son d'interface indisponible : {Path}", path);
            }
        }

        try
        {
            var mixer = new MixingSampleProvider(SoundBank.MixFormat) { ReadFully = true };
            var output = await new WasapiPlayerBuilder()
                .WithSharedMode()
                .WithDefaultDeviceStreamRouting() // suit le périphérique par défaut s'il change (casque branché…)
                .WithEventSync()
                .WithLatency(LatencyMs) // pas de mode IAudioClient3 : incompatible avec le suivi du périphérique par défaut
                .BuildAsync();          // BuildAsync obligatoire avec ce suivi
            output.Init(mixer.ToWaveProvider());
            output.Play();
            _output = output;
            _mixer = mixer;
            logger.LogInformation(
                "Sons d'interface prêts ({Count} sons, sortie « {Device} », latence {Latency} ms)",
                _samples.Count, output.DeviceFriendlyName ?? "périphérique par défaut", output.LatencyMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Aucune sortie audio : les sons d'interface sont désactivés");
        }
    }
}

/// <summary>Fichier d'un son d'interface (dans Assets/Sounds) et son niveau de lecture en décibels.</summary>
internal sealed record UiSoundFile(string Name, float Decibels);
