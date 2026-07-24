using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimbusSplitPro.App.Audio;
using LimbusSplitPro.App.Engine;
using LimbusSplitPro.App.Helpers;
using LimbusSplitPro.Core.Interfaces;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAudioEngine _audioEngine;
    private readonly ISeparationEngine _separationEngine;
    private readonly CancellationTokenSource _cts = new();

    public StemSelectionViewModel StemSelection { get; } = new();
    public ObservableCollection<TrackViewModel> MixerTracks { get; } = new();

    [ObservableProperty]
    private string _selectedInputFilePath = string.Empty;

    [ObservableProperty]
    private AudioFileInfo? _inputFileInfo;

    [ObservableProperty]
    private string _outputFolderPath = string.Empty;

    [ObservableProperty]
    private string _selectedDevice = "Auto";

    [ObservableProperty]
    private ObservableCollection<string> _availableDevices = new() { "Auto (CPU / GPU)", "CPU (Procesamiento Seguro)", "GPU (DirectML / CUDA)" };

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _processingProgress;

    [ObservableProperty]
    private string _currentStageText = "Listo para iniciar la separación.";

    [ObservableProperty]
    private string _statusMessage = "Selecciona una canción y una carpeta de destino.";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private TimeSpan _currentTime;

    [ObservableProperty]
    private TimeSpan _totalDuration;

    [ObservableProperty]
    private double _seekSliderValue;

    [ObservableProperty]
    private string _formattedTimeText = "00:00 / 00:00";

    public MainViewModel()
    {
        _audioEngine = new MultitrackAudioEngine();
        _separationEngine = new PythonProcessController();

        _audioEngine.PlaybackStateChanged += OnAudioPlaybackStateChanged;
        _audioEngine.PositionChanged += OnAudioPositionChanged;
        _audioEngine.MetersUpdated += OnAudioMetersUpdated;
        _separationEngine.ProgressReported += OnSeparationProgressReported;

        OutputFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Limbus Separations");
    }

    public void LoadInputFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        SelectedInputFilePath = filePath;
        var fi = new FileInfo(filePath);

        InputFileInfo = new AudioFileInfo
        {
            FilePath = filePath,
            FileName = fi.Name,
            FormatExtension = fi.Extension.TrimStart('.'),
            Duration = TimeSpan.FromMinutes(3.5), // Example default duration
            SampleRate = 44100,
            Channels = 2,
            FileSizeBytes = fi.Length
        };

        StatusMessage = $"Canción cargada: {fi.Name}";
    }

    [RelayCommand]
    private async Task StartSeparation()
    {
        if (string.IsNullOrEmpty(SelectedInputFilePath) || !File.Exists(SelectedInputFilePath))
        {
            StatusMessage = "Por favor selecciona un archivo de audio válido antes de continuar.";
            return;
        }

        var selectedStems = StemSelection.GetSelectedCategories();
        if (selectedStems.Count == 0)
        {
            StatusMessage = "Selecciona al menos un instrumento o categoría para separar.";
            return;
        }

        if (string.IsNullOrEmpty(OutputFolderPath))
        {
            StatusMessage = "Por favor elige una carpeta de trabajo y exportación.";
            return;
        }

        IsProcessing = true;
        ProcessingProgress = 0.0;
        CurrentStageText = "Iniciando proceso de separación local...";

        var job = new SeparationJob
        {
            InputFilePath = SelectedInputFilePath,
            OutputFolderPath = OutputFolderPath,
            RequestedStems = selectedStems,
            PreferredDevice = SelectedDevice
        };

        try
        {
            var result = await _separationEngine.ProcessAsync(job, _cts.Token);

            if (result.Status == JobStatus.Completed)
            {
                StatusMessage = "¡Separación completada con éxito!";
                LoadGeneratedTracksIntoMixer(result.GeneratedStemFiles);
            }
            else
            {
                StatusMessage = $"Error durante la separación: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falló la separación: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task CancelSeparation()
    {
        await _separationEngine.CancelAsync();
        IsProcessing = false;
        CurrentStageText = "Proceso cancelado por el usuario.";
        StatusMessage = "Separación cancelada.";
    }

    private void LoadGeneratedTracksIntoMixer(Dictionary<string, string> generatedFiles)
    {
        MixerTracks.Clear();

        var trackStates = new List<TrackState>();

        foreach (var kvp in generatedFiles)
        {
            string stemId = kvp.Key;
            string filePath = kvp.Value;
            var category = StemSelection.Categories.FirstOrDefault(c => c.Id == stemId);

            string name = category?.DisplayName ?? stemId;
            string icon = category?.IconKey ?? "AudioTrack";
            string color = category?.DefaultColorHex ?? "#0078D4";

            var trackVm = new TrackViewModel(stemId, name, filePath, icon, color);
            trackVm.VolumeChanged += (s, vol) => _audioEngine.SetTrackVolume(stemId, vol);
            trackVm.MuteToggled += (s, e) => _audioEngine.SetTrackMute(stemId, trackVm.IsMuted);
            trackVm.SoloToggled += (s, e) => _audioEngine.SetTrackSolo(stemId, trackVm.IsSolo);

            MixerTracks.Add(trackVm);

            trackStates.Add(new TrackState
            {
                TrackId = stemId,
                DisplayName = name,
                FilePath = filePath,
                Volume = trackVm.Volume,
                IsMuted = trackVm.IsMuted,
                IsSolo = trackVm.IsSolo,
                ColorHex = color,
                IconKey = icon
            });
        }

        _audioEngine.LoadTracks(trackStates);
        TotalDuration = _audioEngine.CurrentState.TotalDuration;
        UpdateFormattedTime();
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (IsPlaying)
        {
            _audioEngine.Pause();
        }
        else
        {
            _audioEngine.Play();
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _audioEngine.Stop();
    }

    public void OnSeekSliderChanged(double newSeconds)
    {
        _audioEngine.Seek(TimeSpan.FromSeconds(newSeconds));
    }

    [RelayCommand]
    private async Task ExportMix()
    {
        if (MixerTracks.Count == 0) return;

        string exportFile = Path.Combine(OutputFolderPath, "Mezcla_Personalizada.wav");
        try
        {
            var progress = new Progress<double>(p => StatusMessage = $"Exportando mezcla: {p:F1}%");
            await _audioEngine.ExportMixAsync(exportFile, progress);
            StatusMessage = $"Mezcla exportada con éxito en: {exportFile}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al exportar mezcla: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (Directory.Exists(OutputFolderPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = OutputFolderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    private void OnAudioPlaybackStateChanged(object? sender, PlaybackState state)
    {
        IsPlaying = state.Status == PlaybackStatus.Playing;
        CurrentTime = state.CurrentTime;
        TotalDuration = state.TotalDuration;
        UpdateFormattedTime();
    }

    private void OnAudioPositionChanged(object? sender, TimeSpan pos)
    {
        CurrentTime = pos;
        SeekSliderValue = pos.TotalSeconds;
        UpdateFormattedTime();
    }

    private void OnAudioMetersUpdated(object? sender, Dictionary<string, (float PeakL, float PeakR)> meters)
    {
        foreach (var track in MixerTracks)
        {
            if (meters.TryGetValue(track.TrackId, out var val))
            {
                track.PeakLeft = val.PeakL;
                track.PeakRight = val.PeakR;
            }
        }
    }

    private void OnSeparationProgressReported(object? sender, SeparationProgressEventArgs e)
    {
        ProcessingProgress = e.ProgressPercentage;
        CurrentStageText = $"{e.StageDescription} [{e.ActiveModel} - {e.Device}]";
    }

    private void UpdateFormattedTime()
    {
        FormattedTimeText = $"{CurrentTime:mm\\:ss} / {TotalDuration:mm\\:ss}";
    }
}
