namespace LimbusSplitPro.Core.Models;

public enum JobStatus
{
    Idle,
    Preparing,
    Separating,
    ExtractingSubStems,
    CalculatingResidual,
    Completed,
    Failed,
    Cancelled
}

public class SeparationProgressEventArgs : EventArgs
{
    public double ProgressPercentage { get; set; }
    public string StageDescription { get; set; } = string.Empty;
    public string ActiveModel { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
}

public class SeparationJob
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    public string InputFilePath { get; set; } = string.Empty;
    public string OutputFolderPath { get; set; } = string.Empty;
    public List<StemCategory> RequestedStems { get; set; } = new();
    public string PreferredDevice { get; set; } = "Auto"; // Auto, CPU, GPU
    public JobStatus Status { get; set; } = JobStatus.Idle;
    public double Progress { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, string> GeneratedStemFiles { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
