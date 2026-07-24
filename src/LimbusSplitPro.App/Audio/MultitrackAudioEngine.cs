using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using LimbusSplitPro.Core.Interfaces;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.Audio;

public class MultitrackAudioEngine : IAudioEngine
{
    private WasapiOut? _wasapiOut;
    private MixingSampleProvider? _mixer;
    private MasterMixSampleProvider? _masterProvider;
    private readonly List<AudioFileReader> _trackReaders = new();
    private readonly Dictionary<string, TrackSampleProvider> _trackProviders = new();
    private readonly List<TrackState> _trackStates = new();
    private readonly System.Timers.Timer _positionTimer;

    public PlaybackState CurrentState { get; private set; } = new PlaybackState();
    public IReadOnlyList<TrackState> ActiveTracks => _trackStates.AsReadOnly();

    public event EventHandler<PlaybackState>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<Dictionary<string, (float PeakL, float PeakR)>>? MetersUpdated;

    public MultitrackAudioEngine()
    {
        _positionTimer = new System.Timers.Timer(40); // 25 FPS UI position & meter update
        _positionTimer.Elapsed += (s, e) => OnTimerTick();
    }

    public void LoadTracks(IEnumerable<TrackState> tracks)
    {
        Stop();
        ClearTracks();

        _trackStates.AddRange(tracks);

        if (_trackStates.Count == 0) return;

        WaveFormat? masterFormat = null;
        TimeSpan maxDuration = TimeSpan.Zero;

        foreach (var track in _trackStates)
        {
            if (!File.Exists(track.FilePath)) continue;

            var reader = new AudioFileReader(track.FilePath);
            _trackReaders.Add(reader);

            if (reader.TotalTime > maxDuration)
            {
                maxDuration = reader.TotalTime;
            }

            if (masterFormat == null)
            {
                masterFormat = reader.WaveFormat;
            }

            var trackProvider = new TrackSampleProvider(track.TrackId, reader);
            trackProvider.SetVolume(track.Volume);
            trackProvider.SetMute(track.IsMuted);
            _trackProviders[track.TrackId] = trackProvider;
        }

        if (masterFormat == null || _trackProviders.Count == 0) return;

        _mixer = new MixingSampleProvider(masterFormat);
        _mixer.ReadFully = true;

        foreach (var provider in _trackProviders.Values)
        {
            _mixer.AddMixerInput(provider);
        }

        _masterProvider = new MasterMixSampleProvider(_mixer);
        _wasapiOut = new WasapiOut(AudioClientShareMode.Shared, 50);
        _wasapiOut.Init(_masterProvider);

        CurrentState.TotalDuration = maxDuration;
        CurrentState.CurrentTime = TimeSpan.Zero;
        CurrentState.Status = PlaybackStatus.Stopped;

        PlaybackStateChanged?.Invoke(this, CurrentState);
    }

    private void OnTimerTick()
    {
        if (CurrentState.Status == PlaybackStatus.Playing && _trackReaders.Count > 0)
        {
            TimeSpan current = _trackReaders[0].CurrentTime;
            CurrentState.CurrentTime = current;
            PositionChanged?.Invoke(this, current);

            var meterDict = new Dictionary<string, (float PeakL, float PeakR)>();
            foreach (var kvp in _trackProviders)
            {
                meterDict[kvp.Key] = (kvp.Value.PeakLeft, kvp.Value.PeakRight);
            }
            MetersUpdated?.Invoke(this, meterDict);

            if (current >= CurrentState.TotalDuration)
            {
                Stop();
            }
        }
    }

    public void Play()
    {
        if (_wasapiOut != null && _trackReaders.Count > 0)
        {
            _wasapiOut.Play();
            CurrentState.Status = PlaybackStatus.Playing;
            _positionTimer.Start();
            PlaybackStateChanged?.Invoke(this, CurrentState);
        }
    }

    public void Pause()
    {
        if (_wasapiOut != null)
        {
            _wasapiOut.Pause();
            _positionTimer.Stop();
            CurrentState.Status = PlaybackStatus.Paused;
            PlaybackStateChanged?.Invoke(this, CurrentState);
        }
    }

    public void Stop()
    {
        _positionTimer.Stop();
        if (_wasapiOut != null)
        {
            _wasapiOut.Stop();
        }

        foreach (var reader in _trackReaders)
        {
            reader.CurrentTime = TimeSpan.Zero;
        }

        CurrentState.CurrentTime = TimeSpan.Zero;
        CurrentState.Status = PlaybackStatus.Stopped;
        PositionChanged?.Invoke(this, TimeSpan.Zero);
        PlaybackStateChanged?.Invoke(this, CurrentState);
    }

    public void Seek(TimeSpan position)
    {
        TimeSpan clamped = position;
        if (clamped < TimeSpan.Zero) clamped = TimeSpan.Zero;
        if (clamped > CurrentState.TotalDuration) clamped = CurrentState.TotalDuration;

        lock (_trackReaders)
        {
            foreach (var reader in _trackReaders)
            {
                reader.CurrentTime = clamped;
            }
        }

        CurrentState.CurrentTime = clamped;
        PositionChanged?.Invoke(this, clamped);
    }

    public void SetTrackVolume(string trackId, float volume)
    {
        if (_trackProviders.TryGetValue(trackId, out var provider))
        {
            provider.SetVolume(volume);
        }
    }

    public void SetTrackMute(string trackId, bool isMuted)
    {
        if (_trackProviders.TryGetValue(trackId, out var provider))
        {
            provider.SetMute(isMuted);
        }
    }

    public void SetTrackSolo(string trackId, bool isSolo)
    {
        var track = _trackStates.FirstOrDefault(t => t.TrackId == trackId);
        if (track != null) track.IsSolo = isSolo;

        bool anySolo = _trackStates.Any(t => t.IsSolo);

        foreach (var t in _trackStates)
        {
            if (_trackProviders.TryGetValue(t.TrackId, out var prov))
            {
                prov.SetSolo(t.IsSolo, anySolo);
            }
        }
    }

    public void SetMasterVolume(float volume)
    {
        CurrentState.MasterVolume = volume;
        if (_masterProvider != null)
        {
            _masterProvider.MasterVolume = volume;
        }
    }

    public async Task ExportMixAsync(string destinationFilePath, Progress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            OfflineMixExporter.ExportMix(_trackStates, destinationFilePath, progress, cancellationToken);
        }, cancellationToken);
    }

    public List<string> GetAvailableDevices()
    {
        return new List<string> { "Dispositivo Predeterminado de Windows (WASAPI Compartido)" };
    }

    public void SetAudioDevice(string deviceName)
    {
        // Re-initialize WASAPI device if changed
    }

    private void ClearTracks()
    {
        foreach (var reader in _trackReaders)
        {
            reader.Dispose();
        }
        _trackReaders.Clear();
        _trackProviders.Clear();
        _trackStates.Clear();
    }

    public void Dispose()
    {
        _positionTimer.Dispose();
        _wasapiOut?.Dispose();
        ClearTracks();
    }

    private class MasterMixSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public float MasterVolume { get; set; } = 1.0f;
        public WaveFormat WaveFormat => _source.WaveFormat;

        public MasterMixSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read == 0 || Math.Abs(MasterVolume - 1.0f) < 0.001f) return read;

            for (int i = 0; i < read; i++)
            {
                buffer[offset + i] *= MasterVolume;
            }
            return read;
        }
    }
}
