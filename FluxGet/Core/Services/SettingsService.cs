using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FluxGet.Core.Services;

public class SettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FluxGet",
        "settings.json");
    
    public int MaxConcurrentDownloads { get; set; } = 3;
    public int GlobalSpeedLimit { get; set; } = 0; // KB/s, 0 = unlimited
    public string DefaultDownloadPath { get; set; } = string.Empty;
    public bool IsAutoStartDownloads { get; set; } = true;
    public bool IsShowNotifications { get; set; } = true;
    public bool IsBrowserExtensionEnabled { get; set; } = true;
    public string YtDlpPath { get; set; } = string.Empty;
    public string FfmpegPath { get; set; } = string.Empty;
    
    public SettingsService()
    {
        _logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsService>();
        DefaultDownloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }
    
    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        DefaultDownloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }
    
    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                _logger.LogInformation("No settings file found, using defaults");
                return;
            }
            
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<SettingsService>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (settings != null)
            {
                MaxConcurrentDownloads = settings.MaxConcurrentDownloads;
                GlobalSpeedLimit = settings.GlobalSpeedLimit;
                DefaultDownloadPath = settings.DefaultDownloadPath;
                IsAutoStartDownloads = settings.IsAutoStartDownloads;
                IsShowNotifications = settings.IsShowNotifications;
                IsBrowserExtensionEnabled = settings.IsBrowserExtensionEnabled;
                YtDlpPath = settings.YtDlpPath;
                FfmpegPath = settings.FfmpegPath;
                
                _logger.LogInformation("Settings loaded from {Path}", SettingsPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
        }
    }
    
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null)
                Directory.CreateDirectory(dir);
            
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(SettingsPath, json);
            
            _logger.LogInformation("Settings saved to {Path}", SettingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }
}
