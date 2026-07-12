using FluxGet.Core.Models;

namespace FluxGet.Core.Helpers;

public static class UrlHelper
{
    public static string ExtractFileName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "download";
            }
            
            return Uri.UnescapeDataString(fileName);
        }
        catch
        {
            return "download";
        }
    }
    
    public static string GetDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    public static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) 
            && (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "ftp");
    }
    
    public static string GenerateUrlHash(string url)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(url);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
    
    public static DownloadCategory DetectCategory(string url, string? contentType = null)
    {
        var extension = Path.GetExtension(ExtractFileName(url)).ToLowerInvariant();
        
        if (!string.IsNullOrEmpty(contentType))
        {
            if (contentType.StartsWith("video/"))
                return DownloadCategory.Video;
            if (contentType.StartsWith("audio/"))
                return DownloadCategory.Music;
            if (contentType.StartsWith("image/"))
                return DownloadCategory.Image;
            if (contentType.Contains("pdf") || contentType.Contains("document"))
                return DownloadCategory.Document;
        }
        
        return extension switch
        {
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => DownloadCategory.Video,
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" => DownloadCategory.Music,
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" => DownloadCategory.Document,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => DownloadCategory.Archive,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" or ".webp" => DownloadCategory.Image,
            ".exe" or ".msi" or ".dmg" or ".pkg" or ".deb" or ".rpm" => DownloadCategory.Software,
            _ => DownloadCategory.General
        };
    }
}
