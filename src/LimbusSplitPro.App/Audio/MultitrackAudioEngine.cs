using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using LimbusSplitPro.Core.Interfaces;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.Audio;

public class MultitrackAudioEngine : IAudioEngine
{
    private IWavePlayer? _waveOut;
    private MixingSampleProvider? _mixer;
    private MasterMixSampleProvider? _masterProvider;
    private readonly List<AudioFileReader> _trackReaders = new();
    private readonly Dictionary<string, TrackSampleProvider> _trackProviders = new();
    private readonly List<TrackState> _trackStates = new();
    private readonly System.Timers.Timer _positionTimer;

    // Tracks whether playback stopped because the song reached its natural end
    // (vs. user manually stopping). Used by Play() to decide whether to rewind to 0.
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

        // MixingSampleProvider normally removes inputs that return 0 samples.
        // TrackSampleProvider now returns silence (not 0) at EOF, so inputs
        // are NEVER removed. This makes seek and replay work without rebuilding
        // the audio graph.
        _mixer = new MixingSampleProvider(outputFormat) { ReadFully = true };

        foreach (var provider in _trackProviders.Values)
        {
            ISampleProvider input = provider;

            if (input.WaveFormat.Channels == 1 && outputFormat.Channels == 2)
                input = new MonoToStereoSampleProvider(input);

            if (input.WaveFormat.SampleRate != outputFormat.SampleRate)
                input = new WdlResamplingSampleProvider(input, outputFormat.SampleRate);

            _mixer.AddMixerInput(input);
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

        // Detect natural end-of-song by position.
        // We pause (not stop) WaveOutEvent so it stays initialized for replay.
        if (current >= CurrentState.TotalDuration && CurrentState.TotalDuration > TimeSpan.Zero)
        {
            _reachedEnd = true;
            _positionTimer.Stop();
            try { _waveOut?.Pause(); } catch { }

            CurrentState.Status = PlaybackStatus.Stopped;
            // Keep CurrentTime at TotalDuration so the slider shows 100%
            CurrentState.CurrentTime = CurrentState.TotalDuration;
            PositionChanged?.Invoke(this, CurrentState.TotalDuration);
            PlaybackStateChanged?.Invoke(this, CurrentState);
        }
    }

    public void Play()
    {
        if (_waveOut == null || _trackReaders.Count == 0) return;

        // If the song had played to its natural end, rewind to 0 before playing again.
        if (_reachedEnd)
        {
            foreach (var reader in _trackReaders)
                reader.CurrentTime = TimeSpan.Zero;
            CurrentState.CurrentTime = TimeSpan.Zero;
            _reachedEnd = false;
        }

        // WaveOutEvent was either paused or never started — Play() resumes from
        // the current reader position (which was set by Seek() if the user dragged
        // the slider, or 0 if we just rewound above).
        try
        {
            _waveOut.Play();
        }
        catch
        {
            // Last-resort: fully re-initialize the audio device
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

        // Clear the end-of-song flag so Play() after a seek plays from the new position
        // instead of rewinding to 0.
        _reachedEnd = false;

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
