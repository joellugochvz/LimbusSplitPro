using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.Core.Interfaces;

public interface IAudioEngine : IDisposable
{
    PlaybackState CurrentState { get; }
    IReadOnlyList<TrackState> ActiveTracks { get; }
    
    event EventHandler<PlaybackState>? PlaybackStateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<Dictionary<string, (float PeakL, float PeakR)>>? MetersUpdated;

    void LoadTracks(IEnumerable<TrackState> tracks);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    void SetTrackVolume(string trackId, float volume);
    void SetTrackMute(string trackId, bool isMuted);
    void SetTrackSolo(string trackId, bool isSolo);
    void SetMasterVolume(float volume);
    
    Task ExportMixAsync(string destinationFilePath, Progress<double>? progress = null, CancellationToken cancellationToken = default);
    List<string> GetAvailableDevices();
    void SetAudioDevice(string deviceName);
}
