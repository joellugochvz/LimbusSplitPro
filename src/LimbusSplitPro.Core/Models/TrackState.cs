namespace LimbusSplitPro.Core.Models;

public class TrackState
{
    public string TrackId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public float Volume { get; set; } = 1.0f; // 0.0 to 1.0 (or up to 1.5)
    public bool IsMuted { get; set; }
    public bool IsSolo { get; set; }
    public float PeakLevelLeft { get; set; }
    public float PeakLevelRight { get; set; }
    public string ColorHex { get; set; } = "#0078D4";
    public string IconKey { get; set; } = "AudioTrack";
}

public enum PlaybackStatus
{
    Stopped,
    Playing,
    Paused
}

public class PlaybackState
{
    public PlaybackStatus Status { get; set; } = PlaybackStatus.Stopped;
    public TimeSpan CurrentTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public float MasterVolume { get; set; } = 1.0f;
    public string SelectedAudioDevice { get; set; } = "Default";
}
