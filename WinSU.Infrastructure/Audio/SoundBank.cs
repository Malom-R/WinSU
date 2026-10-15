using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WinSU.Infrastructure.Audio;

internal static class SoundBank
{
    /// <summary>Format du mixeur : 44,1 kHz, stéréo, en flottants (celui des sons une fois convertis).</summary>
    public static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    /// <summary>
    /// Charge un son (Ogg Vorbis ou WAV) entièrement en mémoire, converti au format du mixeur (échantillons entrelacés) et
    /// multiplié par <paramref name="gain"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">Fichier qui n'est pas un Ogg Vorbis lisible.</exception>
    public static float[] Load(string path, float gain = 1f)
    {
        var (source, reader) = Open(path);
        using (reader)
        {
            var provider = source.WaveFormat.Channels switch
            {
                1 => source.ToStereo(1f, 1f),
                2 => source,
                _ => throw new NotSupportedException($"{Path.GetFileName(path)} : {source.WaveFormat.Channels} canaux non gérés."),
            };

            if (provider.WaveFormat.SampleRate != MixFormat.SampleRate)
                provider = new WdlResamplingSampleProvider(provider, MixFormat.SampleRate);

            var samples = new List<float>();
            var buffer = new float[MixFormat.SampleRate * MixFormat.Channels];
            int read;
            while ((read = provider.Read(buffer)) > 0)
                samples.AddRange(buffer.AsSpan(0, read));

            var result = samples.ToArray();
            if (gain != 1f)
                for (var i = 0; i < result.Length; i++)
                    result[i] *= gain;
            return result;
        }
    }

    /// <summary>Gain linéaire d'un niveau en décibels (−6 dB ≈ 0,5).</summary>
    public static float FromDecibels(float decibels) => MathF.Pow(10f, decibels / 20f);

    /// <summary>Le son et le lecteur à libérer : Ogg Vorbis par NVorbis (lu une fois), sinon WAV.</summary>
    private static (ISampleProvider Source, IDisposable Reader) Open(string path)
    {
        if (!Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            var wave = new WaveFileReader(path);
            return (wave.ToSampleProvider(), wave);
        }

        try
        {
            var vorbis = new VorbisSource(path, loop: false);
            return (vorbis, vorbis);
        }
        catch (ArgumentException ex)
        {
            // NVorbis refuse un fichier qui n'est pas un Ogg Vorbis par une ArgumentException : ramenée à un format illisible.
            throw new InvalidDataException($"{Path.GetFileName(path)} : pas un fichier Ogg Vorbis lisible.", ex);
        }
    }
}

/// <summary>Relit un son déjà en mémoire ; le mixeur le retire de lui-même une fois terminé.</summary>
internal sealed class CachedSoundProvider(float[] samples, float volume) : ISampleProvider
{
    private int _position;

    public WaveFormat WaveFormat => SoundBank.MixFormat;

    public int Read(Span<float> buffer)
    {
        var count = Math.Min(samples.Length - _position, buffer.Length);
        for (var i = 0; i < count; i++)
            buffer[i] = samples[_position + i] * volume;

        _position += count;
        return count;
    }
}
