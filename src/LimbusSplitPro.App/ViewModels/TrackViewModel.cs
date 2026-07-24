using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LimbusSplitPro.App.ViewModels;

public partial class TrackViewModel : ObservableObject
{
    public string TrackId { get; }
    public string DisplayName { get; }
    public string FilePath { get; }
    public string IconKey { get; }
    public string ColorHex { get; }

    [ObservableProperty]
    private float _volume = 1.0f;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isSolo;

    [ObservableProperty]
    private float _peakLeft;

    [ObservableProperty]
    private float _peakRight;

    public event EventHandler? MuteToggled;
    public event EventHandler? SoloToggled;
    public event EventHandler<float>? VolumeChanged;

    public TrackViewModel(string trackId, string displayName, string filePath, string iconKey, string colorHex)
    {
        TrackId = trackId;
        DisplayName = displayName;
        FilePath = filePath;
        IconKey = iconKey;
        ColorHex = colorHex;
    }

    partial void OnVolumeChanged(float value)
    {
        VolumeChanged?.Invoke(this, value);
    }

    partial void OnIsMutedChanged(bool value)
    {
        MuteToggled?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsSoloChanged(bool value)
    {
        SoloToggled?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private void ToggleSolo()
    {
        IsSolo = !IsSolo;
    }
}
