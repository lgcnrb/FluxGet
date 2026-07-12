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
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        
        return string.IsNullOrWhiteSpace(sanitized) ? "download" : sanitized;
    }
    
    public static async Task<long> GetFileSizeAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;
        
        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }
    
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
}
