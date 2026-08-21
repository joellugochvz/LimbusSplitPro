using NAudio.Wave;

namespace LimbusSplitPro.App.Audio;

public class TrackSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    public string TrackId { get; }
    public WaveFormat WaveFormat => _source.WaveFormat;

    private float _targetVolume = 1.0f;
    private float _currentVolume = 1.0f;
    private bool _isMuted;
    private bool _isSolo;
    private bool _anySoloActive;

    public float PeakLeft { get; private set; }
    public float PeakRight { get; private set; }

    public TrackSampleProvider(string trackId, ISampleProvider source)
    {
        TrackId = trackId;
        _source = source;
    }

    public void SetVolume(float volume)
    {
        _targetVolume = Math.Max(0.0f, Math.Min(2.0f, volume));
    }

    public void SetMute(bool isMuted)
    {
        _isMuted = isMuted;
    }

    public void SetSolo(bool isSolo, bool anySoloActive)
    {
        _isSolo = isSolo;
        _anySoloActive = anySoloActive;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);

        // If source reached EOF, fill remainder with silence and still report 'count'.
        // This prevents PersistentMixer from having a gap and keeps the pipeline alive
        // so seeking and replay work without rebuilding the audio graph.
        if (samplesRead < count)
        {
            Array.Clear(buffer, offset + samplesRead, count - samplesRead);
            samplesRead = count;
        }

        if (samplesRead == 0) return 0;

        // Determine effective mute state based on solo/mute matrix
        bool shouldBeAudible = !_isMuted;
        if (_anySoloActive && !_isSolo)
        {
            shouldBeAudible = false;
        }

        float effectiveTargetVol = shouldBeAudible ? _targetVolume : 0.0f;

        float maxL = 0.0f;
        float maxR = 0.0f;

        int channels = WaveFormat.Channels;
        for (int n = 0; n < samplesRead; n += channels)
        {
            // Smooth gain ramping (exponential interpolation to prevent clicks)
            _currentVolume = _currentVolume + 0.05f * (effectiveTargetVol - _currentVolume);

            for (int ch = 0; ch < channels; ch++)
            {
                int index = offset + n + ch;
                buffer[index] *= _currentVolume;

                float absVal = Math.Abs(buffer[index]);
                if (ch == 0 && absVal > maxL) maxL = absVal;
                if (ch == 1 && absVal > maxR) maxR = absVal;
                if (channels == 1 && absVal > maxR) maxR = absVal;
            }
        }

        PeakLeft = maxL;
        PeakRight = maxR;

        return samplesRead;
    }
}
