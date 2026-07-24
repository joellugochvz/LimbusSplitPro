using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.Core.Interfaces;

public interface ISeparationEngine
{
    event EventHandler<SeparationProgressEventArgs>? ProgressReported;
    
    Task<SeparationJob> ProcessAsync(SeparationJob job, CancellationToken cancellationToken = default);
    Task CancelAsync();
    bool IsGpuAvailable(out string gpuInfo);
}
