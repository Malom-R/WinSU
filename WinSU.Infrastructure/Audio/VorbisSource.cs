using NAudio.Wave;
using NVorbis;

namespace WinSU.Infrastructure.Audio;

/// <summary>
/// Fichier Ogg Vorbis (NVorbis), dans son format d'origine : lu en boucle ou une seule fois (sons d'interface,
/// étape 34).
/// </summary>
internal sealed class VorbisSource : ISampleProvider, IDisposable
{
    private readonly VorbisReader _reader;
    private readonly bool _loop;

    public VorbisSource(string path, bool loop)
    {
        _reader = new VorbisReader(path);
        _loop = loop;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_reader.SampleRate, _reader.Channels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(Span<float> buffer)
    {
        var total = 0;
        var emptyReads = 0;
        while (total < buffer.Length)
        {
            var read = _reader.ReadSamples(buffer[total..]);
            if (read > 0)
            {
                total += read;
                emptyReads = 0;
                continue;
            }

            if (!_loop || ++emptyReads > 1)
                break; // fin d'un son lu une fois, ou fichier vide : ne pas boucler indéfiniment
            _reader.SamplePosition = 0; // fin du fichier : on reprend au début
        }

        return total;
    }

    public void Dispose() => _reader.Dispose();
}
