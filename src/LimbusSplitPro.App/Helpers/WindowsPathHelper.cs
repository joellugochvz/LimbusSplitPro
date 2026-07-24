using System.IO;

namespace LimbusSplitPro.App.Helpers;

public static class WindowsPathHelper
{
    private const string AppFolderName = "Limbus Split Pro";

    public static string GetAppDataDirectory()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string path = Path.Combine(localAppData, AppFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetModelsDirectory()
    {
        string path = Path.Combine(GetAppDataDirectory(), "Models");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetLogsDirectory()
    {
        string path = Path.Combine(GetAppDataDirectory(), "Logs");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetTempDirectory()
    {
        string temp = Path.GetTempPath();
        string path = Path.Combine(temp, AppFolderName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static bool IsPathInProgramFiles(string path)
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrEmpty(programFilesX86) && fullPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase));
    }
}
