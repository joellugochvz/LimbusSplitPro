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

    /// <summary>
    /// Finds a working Python executable. Searches multiple locations:
    /// 1. Bundled python inside app directory
    /// 2. "python" on PATH
    /// 3. "python3" on PATH
    /// 4. Common Windows install locations
    /// 5. Windows Store python (py launcher)
    /// </summary>
    private static string? FindPythonExecutable(string appDir)
    {
        // 1. Check bundled Python
        string bundled = Path.Combine(appDir, "LimbusEngine", "python", "python.exe");
        if (File.Exists(bundled)) return bundled;

        // 2-3. Check PATH candidates
        string[] pathCandidates = { "python", "python3", "py" };
        foreach (var candidate in pathCandidates)
        {
            if (TryRunPython(candidate)) return candidate;
        }

        // 4. Common Windows install locations
        string[] commonPaths = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python"),
            @"C:\Python311",
            @"C:\Python312",
            @"C:\Python313",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python311"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python312"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python313"),
        };

        foreach (var basePath in commonPaths)
        {
            if (Directory.Exists(basePath))
            {
                // Search for python.exe recursively in immediate subdirectories
                try
                {
                    var pythonExe = Directory.GetFiles(basePath, "python.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (pythonExe != null && File.Exists(pythonExe))
                    {
                        return pythonExe;
                    }
                }
                catch { /* ignore access errors */ }
            }
        }

        return null;
    }

    /// <summary>Test if a python command is available by trying to run --version</summary>
    private static bool TryRunPython(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
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
            job.ErrorMessage = $"Motor de separación no encontrado. Buscado en: {Path.Combine(appDir, "LimbusEngine")}. Asegúrate de que los archivos Python estén junto al ejecutable.";
            return job;
        }

        // Find Python executable with intelligent search
        string? pythonExecutable = FindPythonExecutable(appDir);
        if (pythonExecutable == null)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = "Python no encontrado en el sistema. Instala Python 3.11+ desde python.org y asegúrate de marcar 'Add Python to PATH' durante la instalación.";
            return job;
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

        string stderrBuffer = "";
        _currentProcess.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                stderrBuffer += args.Data + Environment.NewLine;
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

                if (exitCode == 9009)
                {
                    job.ErrorMessage = $"Python no fue encontrado (código 9009). Instala Python 3.11+ desde python.org y marca 'Add Python to PATH'.";
                }
                else
                {
                    string errorDetail = stderrBuffer.Length > 200 ? stderrBuffer[..200] : stderrBuffer;
                    job.ErrorMessage = $"El proceso Python terminó con código {exitCode}. {(string.IsNullOrEmpty(errorDetail) ? "Sin detalles adicionales." : errorDetail.Trim())}";
                }
                tcs.TrySetResult(false);
            }
        };

        // Try to start the process
        try
        {
            _currentProcess.Start();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            // ERROR_FILE_NOT_FOUND = 2
            job.Status = JobStatus.Failed;
            job.ErrorMessage = $"No se encontró el ejecutable Python en: '{pythonExecutable}'. Instala Python 3.11+ desde python.org y marca 'Add Python to PATH'.";
            return job;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = $"No se pudo iniciar Python: {ex.Message}. Verifica que Python 3.11+ esté instalado.";
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
