using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FluxGet.Core.Security;

/// <summary>
/// Input sanitization and security utilities
/// </summary>
public static class InputSanitizer
{
    /// <summary>
    /// Validates URL - only allows safe schemes
    /// </summary>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Only allow http and https schemes
        if (uri.Scheme != "http" && uri.Scheme != "https")
            return false;

        // Host cannot be empty
        if (string.IsNullOrWhiteSpace(uri.Host))
            return false;

        // Reject localhost or private IPs (SSRF protection)
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
    /// Validates YouTube URL
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
    /// Sanitizes filename for safe file system usage
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "download";

        // Remove dangerous characters
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        // Preserve dots (needed for extension)
        // Replace multiple underscores with single one
        fileName = Regex.Replace(fileName, "_+", "_");

        // Trim leading/trailing dots and spaces
        fileName = fileName.Trim('.', ' ', '\t');

        // Use default if empty
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "download";

        // Length limit (Windows MAX_PATH)
        if (fileName.Length > 200)
            fileName = fileName.Substring(0, 200);

        return fileName;
    }

    /// <summary>
    /// Protects file path against path traversal attacks
    /// </summary>
    public static string SanitizeFilePath(string basePath, string userPath)
    {
        if (string.IsNullOrWhiteSpace(userPath))
            return basePath;

        // Remove path traversal characters
        userPath = userPath.Replace("..", "");
        userPath = userPath.Replace("~", "");

        // Create full path and validate
        var fullPath = Path.Combine(basePath, userPath);
        var resolvedPath = Path.GetFullPath(fullPath);

        // Check if within base path
        var resolvedBase = Path.GetFullPath(basePath);
        if (!resolvedPath.StartsWith(resolvedBase, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(basePath, Path.GetFileName(userPath));
        }

        return resolvedPath;
    }

    /// <summary>
    /// Creates safe arguments for yt-dlp/ffmpeg
    /// </summary>
    public static string EscapeProcessArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";

        // Wrap in quotes
        var escaped = arg.Replace("\"", "\\\"");

        return $"\"{escaped}\"";
    }

    /// <summary>
    /// Validates and cleans URL for download
    /// </summary>
    public static (string url, string? filename) ValidateDownloadInput(string url, string? filename)
    {
        if (!IsValidUrl(url))
            throw new ArgumentException("Invalid URL: only http/https URLs are allowed.");

        // Extract filename from URL
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
/// Security headers for HTTP requests
/// </summary>
public static class SecurityHeaders
{
    public const string CorsOrigin = "http://localhost:19874";
    
    public static void AddSecurityHeaders(System.Net.HttpListenerResponse response)
    {
        // CORS - only allow our own origin
        response.Headers.Add("Access-Control-Allow-Origin", CorsOrigin);
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
        response.Headers.Add("Access-Control-Allow-Credentials", "true");
        
        // Security headers
        response.Headers.Add("X-Content-Type-Options", "nosniff");
        response.Headers.Add("X-Frame-Options", "DENY");
        response.Headers.Add("X-XSS-Protection", "1; mode=block");
    }
}

/// <summary>
/// Simple API token validation
/// </summary>
public static class ApiTokenValidator
{
    private static readonly Dictionary<string, DateTime> _tokens = new();
    private static readonly object _lock = new();
    
    /// <summary>
    /// Generate a new token
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
    /// Validate token
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
    /// Clean up expired tokens
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
