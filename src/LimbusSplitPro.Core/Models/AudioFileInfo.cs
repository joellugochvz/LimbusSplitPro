namespace LimbusSplitPro.Core.Models;

public class AudioFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FormatExtension { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitsPerSample { get; set; }
    public long FileSizeBytes { get; set; }

    public string FormattedDuration => Duration.ToString(@"mm\:ss\.ff");
    public string FormattedSize => $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
    public string FormattedFormat => $"{FormatExtension.ToUpperInvariant()} • {SampleRate / 1000.0:F1} kHz • {(Channels == 2 ? "Estéreo" : "Mono")}";
}
