using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LimbusSplitPro.App.Helpers;
using LimbusSplitPro.Core.Interfaces;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.Engine;

public class PythonProcessController : ISeparationEngine
{
    private Process? _currentProcess;
    public event EventHandler<SeparationProgressEventArgs>? ProgressReported;

    public bool IsGpuAvailable(out string gpuInfo)
    {
        gpuInfo = "GPU Acceleration (DirectML / CUDA) compatible";
        return true;
    }

    public async Task<SeparationJob> ProcessAsync(SeparationJob job, CancellationToken cancellationToken = default)
    {
        job.Status = JobStatus.Preparing;
        job.StartTime = DateTime.Now;

        string appDir = AppDomain.CurrentDomain.BaseDirectory;

        // Find engine script - try multiple locations
        string engineScriptPath = Path.Combine(appDir, "LimbusEngine", "cli_runner.py");
        if (!File.Exists(engineScriptPath))
        {
            engineScriptPath = Path.Combine(appDir, "LimbusEngine", "src", "cli_runner.py");
        }

        // If engine script not found, fail clearly
        if (!File.Exists(engineScriptPath))
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = $"Motor de separación no encontrado. Buscado en: {Path.Combine(appDir, "LimbusEngine")}";
            return job;
        }

        // Find Python executable - bundled first, then system
        string pythonExecutable = Path.Combine(appDir, "LimbusEngine", "python", "python.exe");
        if (!File.Exists(pythonExecutable))
        {
            // Windows uses "python", Linux/Mac use "python3"
            pythonExecutable = "python";
        }

        string stemsArg = string.Join(",", job.RequestedStems.Select(s => s.Id));

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(engineScriptPath) ?? appDir
        };

        startInfo.ArgumentList.Add(engineScriptPath);
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(job.InputFilePath);
        startInfo.ArgumentList.Add("--output-dir");
        startInfo.ArgumentList.Add(job.OutputFolderPath);
        startInfo.ArgumentList.Add("--stems");
        startInfo.ArgumentList.Add(stemsArg);
        startInfo.ArgumentList.Add("--device");
        startInfo.ArgumentList.Add(job.PreferredDevice);

        // Isolated Python environment
        startInfo.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";

        _currentProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        job.Status = JobStatus.Separating;

        var tcs = new TaskCompletionSource<bool>();

        _currentProcess.OutputDataReceived += (sender, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;

            try
            {
                using var doc = JsonDocument.Parse(args.Data);
                var root = doc.RootElement;
                string type = root.GetProperty("type").GetString() ?? "";

                if (type == "progress")
                {
                    double percentage = root.GetProperty("percentage").GetDouble();
                    string stage = root.GetProperty("stage").GetString() ?? "";
                    string model = root.GetProperty("model").GetString() ?? "";
                    string device = root.GetProperty("device").GetString() ?? "";

                    job.Progress = percentage;
                    job.CurrentStage = stage;

                    ProgressReported?.Invoke(this, new SeparationProgressEventArgs
                    {
                        ProgressPercentage = percentage,
                        StageDescription = stage,
                        ActiveModel = model,
                        Device = device
                    });
                }
                else if (type == "completed")
                {
                    var filesElem = root.GetProperty("generated_files");
                    foreach (var prop in filesElem.EnumerateObject())
                    {
                        job.GeneratedStemFiles[prop.Name] = prop.Value.GetString() ?? "";
                    }
                    job.Status = JobStatus.Completed;
                    job.Progress = 100.0;
                    tcs.TrySetResult(true);
                }
                else if (type == "error")
                {
                    string errorMsg = root.GetProperty("message").GetString() ?? "Unknown Engine Error";
                    job.ErrorMessage = errorMsg;
                    job.Status = JobStatus.Failed;
                    tcs.TrySetException(new Exception(errorMsg));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON parse error from Python output: {ex.Message}");
            }
        };

        _currentProcess.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                try
                {
                    string logDir = WindowsPathHelper.GetLogsDirectory();
                    Directory.CreateDirectory(logDir);
                    string logFile = Path.Combine(logDir, "engine.log");
                    File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {args.Data}{Environment.NewLine}");
                }
                catch
                {
                    // Non-critical logging failure
                }
            }
        };

        // Handle unexpected process exit
        _currentProcess.Exited += (sender, args) =>
        {
            if (job.Status == JobStatus.Separating && job.Progress < 100)
            {
                int exitCode = -1;
                try { exitCode = _currentProcess.ExitCode; } catch { }

                job.Status = JobStatus.Failed;
                job.ErrorMessage = $"El proceso Python terminó inesperadamente (código: {exitCode}). Revisa que Python 3.11+ esté instalado.";
                tcs.TrySetResult(false);
            }
        };

        // Try to start the process
        try
        {
            _currentProcess.Start();
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = $"No se pudo iniciar Python: {ex.Message}. Verifica que Python 3.11+ esté instalado y en el PATH del sistema.";
            return job;
        }

        ProcessJobObject.AttachProcess(_currentProcess);

        _currentProcess.BeginOutputReadLine();
        _currentProcess.BeginErrorReadLine();

        using (cancellationToken.Register(() =>
        {
            CancelInternal();
            job.Status = JobStatus.Cancelled;
            tcs.TrySetCanceled();
        }))
        {
            await tcs.Task;
        }

        job.EndTime = DateTime.Now;
        return job;
    }

    private void CancelInternal()
    {
        if (_currentProcess != null && !_currentProcess.HasExited)
        {
            try
            {
                _currentProcess.Kill(true);
            }
            catch
            {
                // Ignored if already exited
            }
        }
    }

    public Task CancelAsync()
    {
        CancelInternal();
        return Task.CompletedTask;
    }
}
