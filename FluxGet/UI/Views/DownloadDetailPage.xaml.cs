using FluxGet.Core.Models;
using FluxGet.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FluxGet.UI.Views;

public sealed partial class DownloadDetailPage : Page
{
    private readonly DownloadDetailViewModel _viewModel;
    
    public DownloadDetailPage()
    {
        InitializeComponent();
        _viewModel = App.GetService<DownloadDetailViewModel>();
        DataContext = _viewModel;
    }
    
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is DownloadTask task)
        {
            _viewModel.LoadTask(task);
            UpdateUI();
        }
    }
    
    private void UpdateUI()
    {
        TitleText.Text = _viewModel.FileName;
        UrlText.Text = _viewModel.Url;
        
        ProgressBar.Value = _viewModel.Progress;
        ProgressText.Text = $"{_viewModel.Progress:F1}%";
        DownloadedText.Text = _viewModel.FormattedDownloadedBytes;
        TotalSizeText.Text = _viewModel.FormattedFileSize;
        SpeedText.Text = _viewModel.FormattedSpeed;
        
        StatusText.Text = _viewModel.Status.ToString();
        CategoryText.Text = _viewModel.Category.ToString();
        
        FileNameText.Text = _viewModel.FileName;
        FilePathText.Text = _viewModel.FilePath;
        CreatedText.Text = _viewModel.CreatedAt.ToString("g");
        CompletedText.Text = _viewModel.CompletedAt?.ToString("g") ?? "--";
        EtaText.Text = _viewModel.EstimatedTimeRemaining;
        
        // Update button visibility
        StartButton.Visibility = _viewModel.Status == DownloadStatus.Pending 
            ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Visibility = _viewModel.Status == DownloadStatus.Downloading 
            ? Visibility.Visible : Visibility.Collapsed;
        ResumeButton.Visibility = _viewModel.Status == DownloadStatus.Paused 
            ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Visibility = _viewModel.Status == DownloadStatus.Downloading 
            ? Visibility.Visible : Visibility.Collapsed;
        OpenFileButton.Visibility = _viewModel.Status == DownloadStatus.Completed 
            ? Visibility.Visible : Visibility.Collapsed;
        OpenFolderButton.Visibility = _viewModel.Status == DownloadStatus.Completed 
            ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.StartCommand.ExecuteAsync(null);
        UpdateUI();
    }
    
    private async void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.PauseCommand.ExecuteAsync(null);
        UpdateUI();
    }
    
    private async void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ResumeCommand.ExecuteAsync(null);
        UpdateUI();
    }
    
    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CancelCommand.ExecuteAsync(null);
        UpdateUI();
    }
    
    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenFileCommand.Execute(null);
    }
    
    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenFolderCommand.Execute(null);
    }
    
    public void RefreshData()
    {
        UpdateUI();
    }
}
