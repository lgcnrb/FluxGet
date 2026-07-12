using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluxGet.Core.Services;
using Microsoft.Extensions.Logging;

namespace FluxGet.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IQueueService _queueService;
    private readonly ISpeedLimiter _speedLimiter;
    private readonly IBrowserExtensionService _browserExtensionService;
    private readonly ILogger<SettingsViewModel> _logger;
    private static SettingsService? _settingsService;
    
    [ObservableProperty]
    private int _maxConcurrentDownloads = 3;
    
    [ObservableProperty]
    private int _globalSpeedLimit;
    
    [ObservableProperty]
    private bool _isBrowserExtensionEnabled = true;
    
    [ObservableProperty]
    private bool _isAutoStartDownloads = true;
    
    [ObservableProperty]
    private bool _isShowNotifications = true;
    
    [ObservableProperty]
    private string _defaultDownloadPath = string.Empty;
    
    [ObservableProperty]
    private bool _isBrowserExtensionRunning;
    
    [ObservableProperty]
    private string _extensionStatus = "Durum: Pasif";
    
    public SettingsViewModel(
        IQueueService queueService,
        ISpeedLimiter speedLimiter,
        IBrowserExtensionService browserExtensionService,
        ILogger<SettingsViewModel> logger)
    {
        _queueService = queueService;
        _speedLimiter = speedLimiter;
        _browserExtensionService = browserExtensionService;
        _logger = logger;
        
        DefaultDownloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        
        IsBrowserExtensionRunning = browserExtensionService.IsRunning;
    }
    
    public static void Initialize(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    
    public static void LoadSettings()
    {
        if (_settingsService == null) return;
        
        _settingsService.Load();
        
        // Singleton oldugu icin dogrudan atama yapabiliriz
        // Ancak bu sırada ViewModel olusturulmamis olabilir
    }
    
    public void ApplyLoadedSettings()
    {
        if (_settingsService == null) return;
        
        _settingsService.Load();
        
        MaxConcurrentDownloads = _settingsService.MaxConcurrentDownloads;
        GlobalSpeedLimit = _settingsService.GlobalSpeedLimit;
        DefaultDownloadPath = _settingsService.DefaultDownloadPath;
        IsAutoStartDownloads = _settingsService.IsAutoStartDownloads;
        IsShowNotifications = _settingsService.IsShowNotifications;
        IsBrowserExtensionEnabled = _settingsService.IsBrowserExtensionEnabled;
        
        _queueService.MaxConcurrent = MaxConcurrentDownloads;
        _speedLimiter.BytesPerSecond = GlobalSpeedLimit * 1024;
        _speedLimiter.IsEnabled = GlobalSpeedLimit > 0;
    }
    
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            _queueService.MaxConcurrent = MaxConcurrentDownloads;
            _speedLimiter.BytesPerSecond = GlobalSpeedLimit * 1024;
            _speedLimiter.IsEnabled = GlobalSpeedLimit > 0;
            
            if (_settingsService != null)
            {
                _settingsService.MaxConcurrentDownloads = MaxConcurrentDownloads;
                _settingsService.GlobalSpeedLimit = GlobalSpeedLimit;
                _settingsService.DefaultDownloadPath = DefaultDownloadPath;
                _settingsService.IsAutoStartDownloads = IsAutoStartDownloads;
                _settingsService.IsShowNotifications = IsShowNotifications;
                _settingsService.IsBrowserExtensionEnabled = IsBrowserExtensionEnabled;
                _settingsService.Save();
            }
            
            _logger.LogInformation("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }
    
    [RelayCommand]
    private async Task ToggleBrowserExtensionAsync()
    {
        try
        {
            if (IsBrowserExtensionRunning)
            {
                await _browserExtensionService.StopAsync();
                ExtensionStatus = "Durum: Pasif";
            }
            else
            {
                await _browserExtensionService.StartAsync();
                ExtensionStatus = "Durum: Aktif - Baglanti bekleniyor";
            }
            
            IsBrowserExtensionRunning = _browserExtensionService.IsRunning;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle browser extension");
            ExtensionStatus = "Durum: Hata - " + ex.Message;
        }
    }
    
    [RelayCommand]
    private void ResetDefaults()
    {
        MaxConcurrentDownloads = 3;
        GlobalSpeedLimit = 0;
        IsBrowserExtensionEnabled = true;
        IsAutoStartDownloads = true;
        IsShowNotifications = true;
        DefaultDownloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }
}
