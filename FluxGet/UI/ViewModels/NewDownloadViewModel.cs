using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluxGet.Core.Models;
using FluxGet.Core.Services;
using Microsoft.Extensions.Logging;

namespace FluxGet.UI.ViewModels;

public partial class NewDownloadViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;
    private readonly ILogger<NewDownloadViewModel> _logger;
    
    [ObservableProperty]
    private string _url = string.Empty;
    
    [ObservableProperty]
    private string _savePath = string.Empty;
    
    [ObservableProperty]
    private DownloadCategory _selectedCategory = DownloadCategory.General;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    public NewDownloadViewModel(
        IDownloadService downloadService,
        ILogger<NewDownloadViewModel> logger)
    {
        _downloadService = downloadService;
        _logger = logger;
        SavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }
    
    [RelayCommand]
    private async Task CheckUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            ErrorMessage = "Lutfen bir URL girin";
            return;
        }
        
        if (!Core.Helpers.UrlHelper.IsValidUrl(Url))
        {
            ErrorMessage = "Gecersiz URL formati";
            return;
        }
        
        IsLoading = true;
        ErrorMessage = string.Empty;
        
        try
        {
            var category = Core.Helpers.UrlHelper.DetectCategory(Url);
            SelectedCategory = category;
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error checking URL: {ex.Message}";
            _logger.LogError(ex, "Failed to check URL");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    [RelayCommand]
    private async Task StartDownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            ErrorMessage = "Lutfen bir URL girin";
            return;
        }
        
        if (!Core.Helpers.UrlHelper.IsValidUrl(Url))
        {
            ErrorMessage = "Gecersiz URL formati";
            return;
        }
        
        IsLoading = true;
        ErrorMessage = string.Empty;
        
        try
        {
            var task = await _downloadService.AddDownloadAsync(Url, SavePath, SelectedCategory);
            _logger.LogInformation("Indirme baslatildi: {FileName}", task.FileName);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Indirme baslatilamadi: {ex.Message}";
            _logger.LogError(ex, "Indirme baslatilamadi");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
