using FluxGet.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace FluxGet.UI.Views;

public sealed partial class YouTubeDownloadDialog : Page
{
    private readonly YouTubeService _youTubeService;
    private YouTubeInfo? _videoInfo;
    private YouTubeFormat? _selectedFormat;
    private string _savePath;
    
    public YouTubeFormat? SelectedFormat => _selectedFormat;
    public string SavePath => _savePath;
    public YouTubeInfo? VideoInfo => _videoInfo;
    
    public YouTubeDownloadDialog(string? initialPath = null)
    {
        InitializeComponent();
        _youTubeService = App.GetService<YouTubeService>();
        _savePath = initialPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        PathText.Text = _savePath;
    }
    
    public async Task LoadVideoInfoAsync(string url)
    {
        try
        {
            LoadingPanel.Visibility = Visibility.Visible;
            VideoInfoPanel.Visibility = Visibility.Collapsed;
            
            _videoInfo = await _youTubeService.GetVideoInfoAsync(url);
            
            if (!_videoInfo.IsAvailable)
            {
                ErrorText.Text = "Video info could not be retrieved. Please check the URL.";
                ErrorText.Visibility = Visibility.Visible;
                LoadingPanel.Visibility = Visibility.Collapsed;
                return;
            }
            
            TitleText.Text = _videoInfo.Title;
            UploaderText.Text = _videoInfo.Uploader;
            DurationText.Text = _videoInfo.Duration;
            
            // Populate format list
            FormatRadioButtons.Items.Clear();
            
            var mp4Formats = _videoInfo.Formats
                .Where(f => f.Extension == "mp4" && f.HasVideo && f.HasAudio && f.FileSize > 0)
                .GroupBy(f => f.Resolution)
                .Select(g => g.OrderByDescending(f => f.FileSize).First())
                .OrderByDescending(f => f.Resolution)
                .ToList();
            
            foreach (var fmt in mp4Formats)
            {
                var rb = new RadioButton
                {
                    Content = fmt.DisplayName,
                    Tag = fmt,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                FormatRadioButtons.Items.Add(rb);
            }
            
            // MP3 option
            var bestAudio = _videoInfo.Formats
                .Where(f => f.HasAudio && !f.HasVideo && f.FileSize > 0)
                .OrderByDescending(f => f.FileSize)
                .FirstOrDefault();
            
            if (bestAudio != null)
            {
                var mp3Format = new YouTubeFormat
                {
                    FormatId = bestAudio.FormatId,
                    Extension = "mp3",
                    Resolution = "Audio Only",
                    FileSize = bestAudio.FileSize,
                    VCodec = "none",
                    ACodec = bestAudio.ACodec
                };
                
                var rb = new RadioButton
                {
                    Content = $"MP3 - Audio Only ({FormatBytes(mp3Format.FileSize)})",
                    Tag = mp3Format,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                FormatRadioButtons.Items.Add(rb);
            }
            
            if (FormatRadioButtons.Items.Count > 0)
            {
                ((RadioButton)FormatRadioButtons.Items[0]).IsChecked = true;
            }
            
            LoadingPanel.Visibility = Visibility.Collapsed;
            VideoInfoPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }
    
    private void FormatRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormatRadioButtons.SelectedItem is RadioButton rb && rb.Tag is YouTubeFormat fmt)
        {
            _selectedFormat = fmt;
        }
    }
    
    private async void ChangePathButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
        folderPicker.FileTypeFilter.Add("*");
        
        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            _savePath = folder.Path;
            PathText.Text = _savePath;
        }
    }
    
    private static string FormatBytes(long bytes) => bytes switch
    {
        <= 0 => "Unknown",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
