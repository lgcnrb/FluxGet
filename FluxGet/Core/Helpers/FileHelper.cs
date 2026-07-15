namespace FluxGet.Core.Helpers;

public static class FileHelper
{
    public static string GetDefaultDownloadPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }
    
    public static string GetAppDataPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxGet");
        
        Directory.CreateDirectory(path);
        return path;
    }
    
    public static string GetDatabasePath()
    {
        return Path.Combine(GetAppDataPath(), "downloads.db");
    }
    
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "download";

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        fileName = System.Text.RegularExpressions.Regex.Replace(fileName, "_+", "_");
        fileName = fileName.Trim('.', ' ', '\t');

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "download";

        if (fileName.Length > 100)
            fileName = fileName[..100];

        return fileName;
    }
    
    public static async Task<long> GetFileSizeAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;
        
        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }
    
    public static string FormatBytes(long bytes) => bytes switch
    {
        <= 0 => "Unknown",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
    
    public static string FormatSpeed(double bytesPerSecond) => bytesPerSecond switch
    {
        <= 0 => "0 B/s",
        < 1024 => $"{bytesPerSecond:F0} B/s",
        < 1024 * 1024 => $"{bytesPerSecond / 1024:F2} KB/s",
        _ => $"{bytesPerSecond / (1024 * 1024):F2} MB/s"
    };
}
