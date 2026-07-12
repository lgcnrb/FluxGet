using System.Diagnostics;
using System.Text.Json;
using FluxGet.Core.Security;
using Microsoft.Extensions.Logging;

namespace FluxGet.Core.Services;

public class YouTubeFormat
{
    public string FormatId { get; set; } = "";
    public string Extension { get; set; } = "";
    public string Resolution { get; set; } = "";
    public string VCodec { get; set; } = "";
    public string ACodec { get; set; } = "";
    public long FileSize { get; set; }
    public string Note { get; set; } = "";
    public bool HasVideo => !string.IsNullOrEmpty(VCodec) && VCodec != "none";
    public bool HasAudio => !string.IsNullOrEmpty(ACodec) && ACodec != "none";
    
    public string DisplayName => Extension.ToUpper() switch
    {
        "MP4" => $"MP4 - {Resolution} ({FormatBytes(FileSize)})",
        "WEBM" => $"WebM - {Resolution} ({FormatBytes(FileSize)})",
        "MP3" => $"MP3 - Sadece Ses ({FormatBytes(FileSize)})",
        _ => $"{Extension.ToUpper()} - {Resolution} ({FormatBytes(FileSize)})"
    };
    
    private static string FormatBytes(long bytes) => bytes switch
    {
        <= 0 => "Bilinmiyor",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public class YouTubeInfo
{
    public string Title { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Uploader { get; set; } = "";
    public List<YouTubeFormat> Formats { get; set; } = new();
    public bool IsAvailable { get; set; }
}

public class YouTubeService
{
    private readonly ILogger<YouTubeService> _logger;
    private readonly SettingsService _settings;
    private static readonly string ToolsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FluxGet", "tools");
    
    public YouTubeService(ILogger<YouTubeService> logger, SettingsService settings)
    {
        _logger = logger;
        _settings = settings;
    }
    
    public static bool IsYouTubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var lower = url.ToLowerInvariant();
        return lower.Contains("youtube.com/watch") 
            || lower.Contains("youtube.com/shorts/")
            || lower.Contains("youtu.be/")
            || lower.Contains("youtube.com/embed/");
    }
    
    public async Task<string> EnsureYtDlpAsync(IProgress<double>? progress = null)
    {
        // SettingsService'den kayitli yolu kontrol et
        var savedPath = _settings.YtDlpPath;
        if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            return savedPath;
        
        // Fallback: tools klasorunde ara
        var fallbackPath = Path.Combine(ToolsDir, "yt-dlp.exe");
        if (File.Exists(fallbackPath) && new FileInfo(fallbackPath).Length > 100_000)
            return fallbackPath;
        
        throw new Exception("yt-dlp yuklu degil. Ayarlar > Araclar bolumunden yukleyin.");
    }
    
    public async Task<string> EnsureFfmpegAsync(IProgress<double>? progress = null)
    {
        // SettingsService'den kayitli yolu kontrol et
        var savedPath = _settings.FfmpegPath;
        if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            return savedPath;
        
        // Fallback: tools klasorunde ara
        var fallbackPath = Path.Combine(ToolsDir, "ffmpeg.exe");
        if (File.Exists(fallbackPath) && new FileInfo(fallbackPath).Length > 100_000)
            return fallbackPath;
        
        var binPath = Path.Combine(ToolsDir, "ffmpeg_bin", "ffmpeg.exe");
        if (File.Exists(binPath) && new FileInfo(binPath).Length > 100_000)
            return binPath;
        
        throw new Exception("ffmpeg yuklu degil. Ayarlar > Araclar bolumunden yukleyin.");
    }
    
    public async Task<YouTubeInfo> GetVideoInfoAsync(string url)
    {
        var ytdlpPath = await EnsureYtDlpAsync();
        
        _logger.LogInformation("yt-dlp video bilgisi aliniyor: {Url}", url);
        
        var psi = new ProcessStartInfo
        {
            FileName = ytdlpPath,
            Arguments = $"--dump-json --no-playlist {InputSanitizer.EscapeProcessArgument(url)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("yt-dlp islemi baslatilamadi.");
        
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        var output = await outputTask;
        var error = await errorTask;
        
        if (process.ExitCode != 0)
        {
            _logger.LogWarning("yt-dlp hata (kod {Code}): {Error}", process.ExitCode, error);
            throw new Exception($"yt-dlp hatasi: {(string.IsNullOrEmpty(error) ? "Bilinmeyen hata" : error.Trim())}");
        }
        
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new Exception("yt-dlp bos sonuc dondurdu.");
        }
        
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        
        var info = new YouTubeInfo
        {
            Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
            ThumbnailUrl = root.TryGetProperty("thumbnail", out var th) ? th.GetString() ?? "" : "",
            Uploader = root.TryGetProperty("uploader", out var u) ? u.GetString() ?? "" : "",
            IsAvailable = true
        };
        
        if (root.TryGetProperty("duration", out var dur) && dur.TryGetInt64(out var durSec))
        {
            var ts = TimeSpan.FromSeconds(durSec);
            info.Duration = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}sa {ts.Minutes}dk"
                : ts.TotalMinutes >= 1
                    ? $"{(int)ts.TotalMinutes}dk {ts.Seconds}sn"
                    : $"{(int)ts.TotalSeconds}sn";
        }
        
        if (root.TryGetProperty("formats", out var formats))
        {
            foreach (var fmt in formats.EnumerateArray())
            {
                try
                {
                    var formatId = fmt.TryGetProperty("format_id", out var fid) ? fid.GetString() ?? "" : "";
                    var ext = fmt.TryGetProperty("ext", out var ex) ? ex.GetString() ?? "" : "";
                    var res = fmt.TryGetProperty("resolution", out var reso) ? reso.GetString() ?? "" : "";
                    var vcodec = fmt.TryGetProperty("vcodec", out var vc) ? vc.GetString() ?? "" : "";
                    var acodec = fmt.TryGetProperty("acodec", out var ac) ? ac.GetString() ?? "" : "";
                    var note = fmt.TryGetProperty("format_note", out var fn) ? fn.GetString() ?? "" : "";
                    
                    // filesize: once dogrudan, sonra filesize_approx, yoksa 0
                    long filesize = 0;
                    if (fmt.TryGetProperty("filesize", out var fs) && fs.ValueKind == JsonValueKind.Number)
                        fs.TryGetInt64(out filesize);
                    else if (fmt.TryGetProperty("filesize_approx", out var fsa) && fsa.ValueKind == JsonValueKind.Number)
                        fsa.TryGetInt64(out filesize);
                    
                    if (string.IsNullOrEmpty(ext)) continue;
                    
                    info.Formats.Add(new YouTubeFormat
                    {
                        FormatId = formatId,
                        Extension = ext,
                        Resolution = res,
                        VCodec = vcodec,
                        ACodec = acodec,
                        FileSize = filesize,
                        Note = note
                    });
                }
                catch
                {
                    // Hatali format kaydini atla
                }
            }
        }
        
        return info;
    }
    
    public async Task<string> GetDownloadUrlAsync(string url, string formatId)
    {
        var ytdlpPath = await EnsureYtDlpAsync();
        
        var psi = new ProcessStartInfo
        {
            FileName = ytdlpPath,
            Arguments = $"-g -f {InputSanitizer.EscapeProcessArgument(formatId)} --no-playlist {InputSanitizer.EscapeProcessArgument(url)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("yt-dlp islemi baslatilamadi.");
            
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Indirme URL'si alinamadi: {error}");
        }
        
        return output.Trim();
    }
    
    public async Task DownloadVideoAsync(string url, string outputPath, string formatId, 
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var ytdlpPath = await EnsureYtDlpAsync();
        
        var args = $"-f {InputSanitizer.EscapeProcessArgument(formatId)} --no-playlist -o {InputSanitizer.EscapeProcessArgument(outputPath)} {InputSanitizer.EscapeProcessArgument(url)}";
        
        var psi = new ProcessStartInfo
        {
            FileName = ytdlpPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("yt-dlp islemi baslatilamadi.");
        
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null && progress != null)
            {
                // Parse progress from yt-dlp output: [download]  45.2% of 123.45MiB
                if (e.Data.Contains("% of"))
                {
                    var start = e.Data.LastIndexOf('[');
                    var end = e.Data.IndexOf('%');
                    if (start >= 0 && end > start)
                    {
                        var pctStr = e.Data.Substring(start + 1, end - start - 1).Trim();
                        if (double.TryParse(pctStr, System.Globalization.NumberStyles.Float, 
                            System.Globalization.CultureInfo.InvariantCulture, out var pct))
                        {
                            progress.Report(pct);
                        }
                    }
                }
            }
        };
        
        process.BeginOutputReadLine();
        
        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Indirme basarisiz: {error}");
        }
    }
    
    public async Task DownloadVideoByHeightAsync(string url, string outputPath, int height,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var ytdlpPath = await EnsureYtDlpAsync();
        
        // ffmpeg var mi kontrol et
        string? ffmpegPath = null;
        try
        {
            ffmpegPath = await EnsureFfmpegAsync();
        }
        catch { }
        var hasFfmpeg = ffmpegPath != null && File.Exists(ffmpegPath);
        var ffmpegDir = hasFfmpeg ? Path.GetDirectoryName(ffmpegPath) : null;
        
        string formatSelector;
        string mergeArg = "";
        if (hasFfmpeg && ffmpegDir != null)
        {
            // ffmpeg varsa: video+ses birlestirme yapabilir
            formatSelector = $"bestvideo[height<={height}][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<={height}]+bestaudio/bestvideo[height<={height}]/best[height<={height}]";
            mergeArg = $"--merge-output-format mp4 --ffmpeg-location {InputSanitizer.EscapeProcessArgument(ffmpegDir)}";
        }
        else
        {
            // ffmpeg yoksa: SADECE birlesik (video+ses tek dosya) formatlari
            formatSelector = $"best[height<={height}][ext=mp4][acodec!=none]/best[height<={height}][acodec!=none]/best[height<={height}]";
        }
        
        var args = $"-f {InputSanitizer.EscapeProcessArgument(formatSelector)} {mergeArg} --no-playlist -o {InputSanitizer.EscapeProcessArgument(outputPath)} {InputSanitizer.EscapeProcessArgument(url)}";
        
        var psi = new ProcessStartInfo
        {
            FileName = ytdlpPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("yt-dlp islemi baslatilamadi.");
        
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null && progress != null)
            {
                if (e.Data.Contains("% of") || e.Data.Contains("% of"))
                {
                    var idx = e.Data.LastIndexOf('[');
                    var end = e.Data.IndexOf('%');
                    if (idx >= 0 && end > idx)
                    {
                        var pctStr = e.Data.Substring(idx + 1, end - idx - 1).Trim();
                        if (double.TryParse(pctStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var pct))
                        {
                            progress.Report(pct);
                        }
                    }
                }
            }
        };
        
        process.BeginOutputReadLine();
        
        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Video indirme basarisiz: {error}");
        }
    }
    
    public async Task DownloadAudioAsync(string url, string outputPath,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var ytdlpPath = await EnsureYtDlpAsync();
        
        var args = $"-f \"bestaudio/best\" --no-playlist -o {InputSanitizer.EscapeProcessArgument(outputPath)} {InputSanitizer.EscapeProcessArgument(url)}";
        
        var psi = new ProcessStartInfo
        {
            FileName = ytdlpPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("yt-dlp islemi baslatilamadi.");
        
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null && progress != null)
            {
                if (e.Data.Contains("% of"))
                {
                    var idx = e.Data.LastIndexOf('[');
                    var end = e.Data.IndexOf('%');
                    if (idx >= 0 && end > idx)
                    {
                        var pctStr = e.Data.Substring(idx + 1, end - idx - 1).Trim();
                        if (double.TryParse(pctStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var pct))
                        {
                            progress.Report(pct);
                        }
                    }
                }
            }
        };
        
        process.BeginOutputReadLine();
        
        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Ses indirme basarisiz: {error}");
        }
    }
    
    public async Task ConvertToMp3Async(string inputPath, string outputPath, 
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var ffmpegPath = await EnsureFfmpegAsync();
        
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-i {InputSanitizer.EscapeProcessArgument(inputPath)} -vn -ab 192k -ar 44100 -y {InputSanitizer.EscapeProcessArgument(outputPath)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("ffmpeg islemi baslatilamadi.");
            
        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Donusturme basarisiz: {error}");
        }
        
        // Delete original file
        try { if (File.Exists(inputPath) && inputPath != outputPath) File.Delete(inputPath); } catch { }
    }
}
