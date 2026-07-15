using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluxGet.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;

namespace FluxGet.UI.ViewModels;

public partial class ToolsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ILogger<ToolsViewModel> _logger;

    private string _ytdlpStatusText = "Not installed";
    private string _ytdlpPathText = "Select the file using the button below";
    private string _ytdlpVersionText = string.Empty;
    private bool _ytdlpVersionVisible;
    private Brush _ytdlpStatusBrush = new SolidColorBrush(Microsoft.UI.Colors.Orange);

    private string _ffmpegStatusText = "Not installed";
    private string _ffmpegPathText = "Select the file using the button below (required for MP3)";
    private string _ffmpegVersionText = string.Empty;
    private bool _ffmpegVersionVisible;
    private Brush _ffmpegStatusBrush = new SolidColorBrush(Microsoft.UI.Colors.Orange);

    public string YtDlpStatusText { get => _ytdlpStatusText; set => SetProperty(ref _ytdlpStatusText, value); }
    public string YtDlpPathText { get => _ytdlpPathText; set => SetProperty(ref _ytdlpPathText, value); }
    public string YtDlpVersionText { get => _ytdlpVersionText; set => SetProperty(ref _ytdlpVersionText, value); }
    public bool YtDlpVersionVisible { get => _ytdlpVersionVisible; set => SetProperty(ref _ytdlpVersionVisible, value); }
    public Brush YtDlpStatusBrush { get => _ytdlpStatusBrush; set => SetProperty(ref _ytdlpStatusBrush, value); }

    public string FfmpegStatusText { get => _ffmpegStatusText; set => SetProperty(ref _ffmpegStatusText, value); }
    public string FfmpegPathText { get => _ffmpegPathText; set => SetProperty(ref _ffmpegPathText, value); }
    public string FfmpegVersionText { get => _ffmpegVersionText; set => SetProperty(ref _ffmpegVersionText, value); }
    public bool FfmpegVersionVisible { get => _ffmpegVersionVisible; set => SetProperty(ref _ffmpegVersionVisible, value); }
    public Brush FfmpegStatusBrush { get => _ffmpegStatusBrush; set => SetProperty(ref _ffmpegStatusBrush, value); }

    public ToolsViewModel(SettingsService settingsService, ILogger<ToolsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task RefreshToolsStatusAsync()
    {
        var ytdlpPath = _settingsService.YtDlpPath;
        if (IsToolValid(ytdlpPath))
        {
            YtDlpStatusText = "Installed";
            YtDlpStatusBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            YtDlpPathText = ytdlpPath;

            var version = GetToolVersion(ytdlpPath);
            YtDlpVersionText = version != null ? $"Version: {version}" : string.Empty;
            YtDlpVersionVisible = version != null;
        }
        else
        {
            YtDlpStatusText = "Not installed";
            YtDlpStatusBrush = new SolidColorBrush(Microsoft.UI.Colors.Orange);
            YtDlpPathText = "Select the file using the button below";
            YtDlpVersionVisible = false;
        }

        var ffmpegPath = _settingsService.FfmpegPath;
        if (IsToolValid(ffmpegPath))
        {
            FfmpegStatusText = "Installed";
            FfmpegStatusBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            FfmpegPathText = ffmpegPath;

            var version = GetToolVersion(ffmpegPath);
            FfmpegVersionText = version != null ? $"Version: {version}" : string.Empty;
            FfmpegVersionVisible = version != null;
        }
        else
        {
            FfmpegStatusText = "Not installed";
            FfmpegStatusBrush = new SolidColorBrush(Microsoft.UI.Colors.Orange);
            FfmpegPathText = "Select the file using the button below (required for MP3)";
            FfmpegVersionVisible = false;
        }

        await Task.CompletedTask;
    }

    public void SetYtDlpPath(string path)
    {
        _settingsService.YtDlpPath = path;
        _settingsService.Save();
    }

    public void SetFfmpegPath(string path)
    {
        _settingsService.FfmpegPath = path;
        _settingsService.Save();
    }

    public bool IsToolValid(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;
        try
        {
            var fi = new FileInfo(exePath);
            return fi.Length > 100_000;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate tool: {Path}", exePath);
            return false;
        }
    }

    public string? GetToolVersion(string? exePath)
    {
        if (exePath == null || !File.Exists(exePath)) return null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tool version: {Path}", exePath);
            return null;
        }
    }

    public async Task<string?> CheckYtDlpUpdateAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -Command \"$resp = Invoke-RestMethod 'https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest'; $resp.tag_name\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)!;
        var latestVersion = (await process.StandardOutput.ReadToEndAsync()).Trim();
        await process.WaitForExitAsync();
        return latestVersion;
    }

    public async Task<string?> CheckFfmpegUpdateAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -Command \"$resp = Invoke-RestMethod 'https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest'; $resp.tag_name\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)!;
        var latestVersion = (await process.StandardOutput.ReadToEndAsync()).Trim();
        await process.WaitForExitAsync();
        return latestVersion;
    }

    public void OpenToolsFolder()
    {
        var toolsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxGet", "tools");
        Directory.CreateDirectory(toolsDir);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = toolsDir,
            UseShellExecute = true
        });
    }
}
