using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;
using LimbusSplitPro.Core.Interfaces;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.Audio;

/// <summary>
/// A custom mixer that holds onto its inputs forever — it never removes them when they
/// return 0 samples. This prevents NAudio's MixingSampleProvider from silently dropping
/// tracks after end-of-stream, which would break seeking and replay.
/// </summary>
public class PersistentMixer : ISampleProvider
{
    private readonly List<ISampleProvider> _sources = new();
    private float[] _tempBuffer = Array.Empty<float>();

    public WaveFormat WaveFormat { get; }

    public PersistentMixer(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;
    }

    public void AddInput(ISampleProvider source)
    {
        _sources.Add(source);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // Zero-fill the output buffer first (mix into silence)
        Array.Clear(buffer, offset, count);

        if (_tempBuffer.Length < count)
            _tempBuffer = new float[count];

        // Mix each source into the buffer
        foreach (var source in _sources)
        {
            int read = source.Read(_tempBuffer, 0, count);
            // Mix whatever was read (ignore 0-return — just means silence for this tick)
            for (int i = 0; i < read; i++)
                buffer[offset + i] += _tempBuffer[i];
        }

        // Always report 'count' samples so WaveOutEvent never stops on its own
        return count;
    }
}

public class MultitrackAudioEngine : IAudioEngine
{
    private IWavePlayer? _waveOut;
    private PersistentMixer? _mixer;
    private MasterMixSampleProvider? _masterProvider;
    private readonly List<AudioFileReader> _trackReaders = new();
    private readonly Dictionary<string, TrackSampleProvider> _trackProviders = new();
    private readonly List<TrackState> _trackStates = new();
    private readonly System.Timers.Timer _positionTimer;
    private bool _reachedEnd = false;

    public LimbusPlaybackState CurrentState { get; private set; } = new LimbusPlaybackState();
    public IReadOnlyList<TrackState> ActiveTracks => _trackStates.AsReadOnly();

    public event EventHandler<LimbusPlaybackState>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<Dictionary<string, (float PeakL, float PeakR)>>? MetersUpdated;

    public MultitrackAudioEngine()
    {
        _positionTimer = new System.Timers.Timer(40); // 25 FPS UI update
        _positionTimer.Elapsed += (s, e) => OnTimerTick();
    }

    public void LoadTracks(IEnumerable<TrackState> tracks)
    {
        Stop();
        ClearTracks();
        _reachedEnd = false;

        _trackStates.AddRange(tracks);
        if (_trackStates.Count == 0) return;

        WaveFormat? masterFormat = null;
        TimeSpan maxDuration = TimeSpan.Zero;

        foreach (var track in _trackStates)
        {
            if (!File.Exists(track.FilePath)) continue;
            try
            {
                var reader = new AudioFileReader(track.FilePath);
                _trackReaders.Add(reader);

                if (reader.TotalTime > maxDuration)
                    maxDuration = reader.TotalTime;

                masterFormat ??= reader.WaveFormat;

                var trackProvider = new TrackSampleProvider(track.TrackId, reader);
                trackProvider.SetVolume(track.Volume);
                trackProvider.SetMute(track.IsMuted);
                _trackProviders[track.TrackId] = trackProvider;
            }
            catch { /* skip unreadable files */ }
        }

        if (masterFormat == null || _trackProviders.Count == 0) return;

        var outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            masterFormat.SampleRate, Math.Max(masterFormat.Channels, 2));

        // Use our PersistentMixer so inputs are never dropped at end-of-stream
        _mixer = new PersistentMixer(outputFormat);

        foreach (var provider in _trackProviders.Values)
        {
            ISampleProvider input = provider;

            if (input.WaveFormat.Channels == 1 && outputFormat.Channels == 2)
                input = new MonoToStereoSampleProvider(input);

            if (input.WaveFormat.SampleRate != outputFormat.SampleRate)
                input = new WdlResamplingSampleProvider(input, outputFormat.SampleRate);

            _mixer.AddInput(input);
        }

        _masterProvider = new MasterMixSampleProvider(_mixer);

        try
        {
            _waveOut = new WaveOutEvent { DesiredLatency = 150, NumberOfBuffers = 3 };
            _waveOut.Init(_masterProvider);
        }
        catch
        {
            _waveOut = null;
        }

        CurrentState.TotalDuration = maxDuration;
        CurrentState.CurrentTime = TimeSpan.Zero;
        CurrentState.Status = PlaybackStatus.Stopped;

        PlaybackStateChanged?.Invoke(this, CurrentState);
    }

    private void OnTimerTick()
    {
        if (CurrentState.Status != PlaybackStatus.Playing || _trackReaders.Count == 0)
            return;

        TimeSpan current = _trackReaders[0].CurrentTime;
        CurrentState.CurrentTime = current;
        PositionChanged?.Invoke(this, current);

        var meterDict = new Dictionary<string, (float PeakL, float PeakR)>();
        foreach (var kvp in _trackProviders)
            meterDict[kvp.Key] = (kvp.Value.PeakLeft, kvp.Value.PeakRight);
        MetersUpdated?.Invoke(this, meterDict);

        // Detect end of song by position (WaveOutEvent no longer self-stops because
        // PersistentMixer always returns 'count' samples)
        if (current >= CurrentState.TotalDuration && CurrentState.TotalDuration > TimeSpan.Zero)
        {
            _reachedEnd = true;
            _positionTimer.Stop();
            if (_waveOut != null)
                try { _waveOut.Pause(); } catch { }

            CurrentState.Status = PlaybackStatus.Stopped;
            CurrentState.CurrentTime = CurrentState.TotalDuration;
            PositionChanged?.Invoke(this, CurrentState.TotalDuration);
            PlaybackStateChanged?.Invoke(this, CurrentState);
        }
    }

    public void Play()
    {
        if (_waveOut == null || _trackReaders.Count == 0) return;

        // If we reached the end, reset all readers to position 0
        if (_reachedEnd)
        {
            foreach (var reader in _trackReaders)
                reader.CurrentTime = TimeSpan.Zero;
            CurrentState.CurrentTime = TimeSpan.Zero;
            _reachedEnd = false;
        }

        // WaveOutEvent was paused (or is stopped) — just call Play() again.
        // Because PersistentMixer always returns samples, WaveOutEvent stays
        // in its pipeline and .Play() resumes correctly from current position.
        try
        {
            _waveOut.Play();
        }
        catch
        {
            // If Play() fails try a full re-init
            try
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = new WaveOutEvent { DesiredLatency = 150, NumberOfBuffers = 3 };
                _waveOut.Init(_masterProvider!);
                _waveOut.Play();
            }
            catch { return; }
        }

        CurrentState.Status = PlaybackStatus.Playing;
        _positionTimer.Start();
        PlaybackStateChanged?.Invoke(this, CurrentState);
    }

    public void Pause()
    {
        if (_waveOut == null) return;
        try { _waveOut.Pause(); } catch { }
        _positionTimer.Stop();
        CurrentState.Status = PlaybackStatus.Paused;
        PlaybackStateChanged?.Invoke(this, CurrentState);
    }

    public void Stop()
    {
        _positionTimer.Stop();
        _reachedEnd = false;
        if (_waveOut != null)
            try { _waveOut.Stop(); } catch { }

        foreach (var reader in _trackReaders)
            reader.CurrentTime = TimeSpan.Zero;

        CurrentState.CurrentTime = TimeSpan.Zero;
        CurrentState.Status = PlaybackStatus.Stopped;
        PositionChanged?.Invoke(this, TimeSpan.Zero);
        PlaybackStateChanged?.Invoke(this, CurrentState);
    }

    public void Seek(TimeSpan position)
    {
        TimeSpan clamped = position < TimeSpan.Zero ? TimeSpan.Zero :
                           position > CurrentState.TotalDuration ? CurrentState.TotalDuration : position;

        _reachedEnd = false; // Clear end-of-song flag so next Play() doesn't reset to 0

        lock (_trackReaders)
        {
            foreach (var reader in _trackReaders)
                reader.CurrentTime = clamped;
        }

        CurrentState.CurrentTime = clamped;
        PositionChanged?.Invoke(this, clamped);
    }

    public void SetTrackVolume(string trackId, float volume)
    {
        if (_trackProviders.TryGetValue(trackId, out var provider))
            provider.SetVolume(volume);
    }

    public void SetTrackMute(string trackId, bool isMuted)
    {
        if (_trackProviders.TryGetValue(trackId, out var provider))
            provider.SetMute(isMuted);
    }

    public void SetTrackSolo(string trackId, bool isSolo)
    {
        var track = _trackStates.FirstOrDefault(t => t.TrackId == trackId);
        if (track != null) track.IsSolo = isSolo;

        bool anySolo = _trackStates.Any(t => t.IsSolo);
        foreach (var t in _trackStates)
        {
            if (_trackProviders.TryGetValue(t.TrackId, out var prov))
                prov.SetSolo(t.IsSolo, anySolo);
        }
    }

    public void SetMasterVolume(float volume)
    {
        CurrentState.MasterVolume = volume;
        if (_masterProvider != null)
            _masterProvider.MasterVolume = volume;
    }

    public async Task ExportMixAsync(string destinationFilePath, Progress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            OfflineMixExporter.ExportMix(_trackStates, destinationFilePath, progress, cancellationToken);
        }, cancellationToken);
    }

    public List<string> GetAvailableDevices() =>
        new List<string> { "Dispositivo Predeterminado de Windows" };

    public void SetAudioDevice(string deviceName) { }

    private void ClearTracks()
    {
        try { _waveOut?.Stop(); } catch { }
        try { _waveOut?.Dispose(); } catch { }
        _waveOut = null;

        foreach (var reader in _trackReaders)
            try { reader.Dispose(); } catch { }

        _trackReaders.Clear();
        _trackProviders.Clear();
        _trackStates.Clear();
    }

    public void Dispose()
    {
        _positionTimer.Dispose();
        ClearTracks();
    }

    private class MasterMixSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public float MasterVolume { get; set; } = 1.0f;
        public WaveFormat WaveFormat => _source.WaveFormat;

        public MasterMixSampleProvider(ISampleProvider source) => _source = source;

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read == 0 || Math.Abs(MasterVolume - 1.0f) < 0.001f) return read;
            for (int i = 0; i < read; i++)
                buffer[offset + i] *= MasterVolume;
            return read;
        }
    }
}
