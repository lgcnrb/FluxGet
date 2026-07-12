using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FluxGet.Core.Security;

/// <summary>
/// Input sanitization and security utilities
/// </summary>
public static class InputSanitizer
{
    /// <summary>
    /// URL'yi dogrular - yalnizca guvenli scheme'lere izin verir
    /// </summary>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Yalnizca http ve https scheme'lerine izin ver
        if (uri.Scheme != "http" && uri.Scheme != "https")
            return false;

        // Host bos olamaz
        if (string.IsNullOrWhiteSpace(uri.Host))
            return false;

        // Localhost veya ozel IP'leri reddet (SSRF korumasi)
        var host = uri.Host.ToLowerInvariant();
        if (host == "localhost" || host == "127.0.0.1" || host == "::1" ||
            host.StartsWith("192.168.") || host.StartsWith("10.") ||
            host.StartsWith("172."))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// YouTube URL'sini dogrular
    /// </summary>
    public static bool IsYouTubeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != "http" && uri.Scheme != "https")
            return false;

        var host = uri.Host.ToLowerInvariant();
        return host == "youtube.com" || host == "www.youtube.com" ||
               host == "m.youtube.com" || host == "youtu.be";
    }

    /// <summary>
    /// Dosya yolu icin guvenli karakterleri temizler
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "download";

        // Tehlikeli karakterleri temizle
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        // Noktalari koru (uzanti icin gerekli)
        // Birden fazla alt cizgiyi tek yap
        fileName = Regex.Replace(fileName, "_+", "_");

        // Basindaki/sonundaki bosluk ve noktalari temizle
        fileName = fileName.Trim('.', ' ', '\t');

        // Bos kalirsa default kullan
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "download";

        // Uzunluk siniri (Windows MAX_PATH)
        if (fileName.Length > 200)
            fileName = fileName.Substring(0, 200);

        return fileName;
    }

    /// <summary>
    /// Dosya yolunu path traversal saldirilarina karsi korur
    /// </summary>
    public static string SanitizeFilePath(string basePath, string userPath)
    {
        if (string.IsNullOrWhiteSpace(userPath))
            return basePath;

        // Path traversal karakterlerini temizle
        userPath = userPath.Replace("..", "");
        userPath = userPath.Replace("~", "");

        // Tam yol olustur ve dogrula
        var fullPath = Path.Combine(basePath, userPath);
        var resolvedPath = Path.GetFullPath(fullPath);

        // Base path icinde olup olmadigini kontrol et
        var resolvedBase = Path.GetFullPath(basePath);
        if (!resolvedPath.StartsWith(resolvedBase, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(basePath, Path.GetFileName(userPath));
        }

        return resolvedPath;
    }

    /// <summary>
    /// yt-dlp/ffmpeg icin guvenli arguman olusturur
    /// </summary>
    public static string EscapeProcessArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";

        // Tirnak isareti ile sarmala
        var escaped = arg.Replace("\"", "\\\"");

        return $"\"{escaped}\"";
    }

    /// <summary>
    /// URL'yi indirme icin dogrular ve temizler
    /// </summary>
    public static (string url, string? filename) ValidateDownloadInput(string url, string? filename)
    {
        if (!IsValidUrl(url))
            throw new ArgumentException("Gecersiz URL: yalnizca http/https URL'lerine izin verilir.");

        // URL'den dosya adini cikar
        if (string.IsNullOrWhiteSpace(filename) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath;
            var lastSlash = path.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < path.Length - 1)
            {
                filename = Uri.UnescapeDataString(path.Substring(lastSlash + 1));
                filename = SanitizeFileName(filename);
            }
        }
        else if (!string.IsNullOrWhiteSpace(filename))
        {
            filename = SanitizeFileName(filename);
        }

        return (url, filename);
    }
}

/// <summary>
/// HTTP istekleri icin guvenlik header'lari
/// </summary>
public static class SecurityHeaders
{
    public const string CorsOrigin = "http://localhost:19874";
    
    public static void AddSecurityHeaders(System.Net.HttpListenerResponse response)
    {
        // CORS - yalnizca kendi origin'imize izin ver
        response.Headers.Add("Access-Control-Allow-Origin", CorsOrigin);
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
        response.Headers.Add("Access-Control-Allow-Credentials", "true");
        
        // Guvenlik header'lari
        response.Headers.Add("X-Content-Type-Options", "nosniff");
        response.Headers.Add("X-Frame-Options", "DENY");
        response.Headers.Add("X-XSS-Protection", "1; mode=block");
    }
}

/// <summary>
/// Basit API token dogrulama
/// </summary>
public static class ApiTokenValidator
{
    private static readonly Dictionary<string, DateTime> _tokens = new();
    private static readonly object _lock = new();
    
    /// <summary>
    /// Yeni bir token olustur
    /// </summary>
    public static string GenerateToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        lock (_lock)
        {
            _tokens[token] = DateTime.UtcNow.AddHours(24);
        }
        return token;
    }
    
    /// <summary>
    /// Token'i dogrula
    /// </summary>
    public static bool ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        
        lock (_lock)
        {
            if (_tokens.TryGetValue(token, out var expiry))
            {
                if (DateTime.UtcNow < expiry)
                    return true;
                
                _tokens.Remove(token);
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Süresi dolmus token'lari temizle
    /// </summary>
    public static void CleanupExpiredTokens()
    {
        lock (_lock)
        {
            var expired = _tokens.Where(t => DateTime.UtcNow >= t.Value).Select(t => t.Key).ToList();
            foreach (var token in expired)
            {
                _tokens.Remove(token);
            }
        }
    }
}

/// <summary>
/// RandomNumberGenerator wrapper
/// </summary>
internal static class RandomNumberGenerator
{
    public static byte[] GetBytes(int count)
    {
        var bytes = new byte[count];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}
