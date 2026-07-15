using FluxGet.Core.Helpers;
using FluxGet.Core.Models;
using FluxGet.Core.Services;
using FluxGet.UI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;

namespace FluxGet.UI.Views
{
    public sealed partial class DashboardPage : Page
    {
        private readonly MainViewModel _viewModel;
        
        public DashboardPage()
        {
            InitializeComponent();
            _viewModel = App.GetService<MainViewModel>();
            DataContext = _viewModel;
            
            Loaded += async (s, e) => 
            {
                await _viewModel.LoadDownloadsCommand.ExecuteAsync(null);
                RefreshData();
            };
        }
        
        public void RefreshData()
        {
            var downloads = _viewModel.Downloads;
            
            TotalCountText.Text = downloads.Count.ToString();
            TotalCountSubText.Text = $"{downloads.Count} downloads";
            ActiveCountText.Text = downloads.Count(t => t.Status == DownloadStatus.Downloading).ToString();
            CompletedCountText.Text = downloads.Count(t => t.Status == DownloadStatus.Completed).ToString();
            
            var totalSpeed = downloads.Where(t => t.Status == DownloadStatus.Downloading).Sum(t => t.Speed);
            TotalSpeedText.Text = FileHelper.FormatSpeed(totalSpeed);
            
            // Recent downloads (latest 5)
            var recent = downloads.Take(5).ToList();
            RecentDownloadsList.ItemsSource = recent;
            EmptyDownloadsPanel.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RecentDownloadsList.Visibility = recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // Active downloads
            var active = downloads.Where(t => t.Status == DownloadStatus.Downloading).ToList();
            ActiveDownloadsList.ItemsSource = active;
            EmptyActivePanel.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ActiveDownloadsList.Visibility = active.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // Last 5 completed
            var completed = downloads.Where(t => t.Status == DownloadStatus.Completed).Take(5).ToList();
            CompletedDownloadsList.ItemsSource = completed;
            EmptyCompletedPanel.Visibility = completed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CompletedDownloadsList.Visibility = completed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = _viewModel.LoadDownloadsCommand.ExecuteAsync(null);
            RefreshData();
        }
        
        private async void NewDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Add New Download",
                PrimaryButtonText = "Start Download",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Width = 500
            };
            
            var panel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            
            var urlLabel = new TextBlock { Text = "Download URL", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var urlBox = new TextBox
            {
                PlaceholderText = "Paste YouTube or file link",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            
            var ytHint = new TextBlock
            {
                Text = "YouTube links auto-detected (MP4/MP3)",
                FontSize = 11,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 30, 144, 255)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            
            panel.Children.Add(urlLabel);
            panel.Children.Add(urlBox);
            panel.Children.Add(ytHint);
            
            // Save path
            var pathLabel = new TextBlock { Text = "Save Location", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
            var pathPanel = new Grid();
            pathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
            
            var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            var pathBox = new TextBox
            {
                Text = defaultPath,
                IsReadOnly = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            var browseButton = new Button
            {
                Content = "Browse",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            browseButton.Click += async (s, args) =>
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
                folderPicker.FileTypeFilter.Add("*");
                
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    pathBox.Text = folder.Path;
                }
            };
            
            Grid.SetColumn(pathBox, 0);
            Grid.SetColumn(browseButton, 1);
            pathPanel.Children.Add(pathBox);
            pathPanel.Children.Add(browseButton);
            panel.Children.Add(pathLabel);
            panel.Children.Add(pathPanel);
            
            // Category selector
            var catLabel = new TextBlock { Text = "Category", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
            var catCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                ItemsSource = Enum.GetValues(typeof(DownloadCategory)),
                SelectedIndex = 0
            };
            panel.Children.Add(catLabel);
            panel.Children.Add(catCombo);
            
            dialog.Content = panel;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(urlBox.Text))
            {
                var url = urlBox.Text.Trim();
                var savePath = pathBox.Text;
                
                if (YouTubeService.IsYouTubeUrl(url))
                {
                    await ShowYouTubeDownloadDialog(url, savePath);
                }
                else
                {
                    var category = (DownloadCategory)(catCombo.SelectedItem ?? DownloadCategory.General);
                    await _viewModel.AddDownloadWithOptionsAsync(url, savePath, category);
                }
                RefreshData();
            }
        }
        
        private async Task ShowYouTubeDownloadDialog(string url, string savePath)
        {
            var ytDialog = new ContentDialog
            {
                Title = "YouTube Download",
                PrimaryButtonText = "Download",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Width = 550
            };
            
            var ytPage = new YouTubeDownloadDialog(savePath);
            ytDialog.Content = ytPage;
            
            await ytPage.LoadVideoInfoAsync(url);
            
            var dialogResult = await ytDialog.ShowAsync();
            if (dialogResult == ContentDialogResult.Primary && ytPage.SelectedFormat != null)
            {
                var youTubeService = App.GetService<YouTubeService>();
                var downloadService = App.GetService<IDownloadService>();
                
                var format = ytPage.SelectedFormat;
                var outputPath = Path.Combine(ytPage.SavePath, FileHelper.SanitizeFileName(ytPage.VideoInfo?.Title ?? "youtube_video"));
                
                try
                {
                    if (format.Extension == "mp3")
                    {
                        // MP3: download best audio then convert
                        var tempPath = outputPath + ".webm";
                        outputPath += ".mp3";
                        
                        await youTubeService.DownloadVideoAsync(url, tempPath, format.FormatId);
                        await youTubeService.ConvertToMp3Async(tempPath, outputPath);
                    }
                    else
                    {
                        outputPath += ".mp4";
                        await youTubeService.DownloadVideoAsync(url, outputPath, format.FormatId);
                    }
                    
                    // Create task record
                    var task = await downloadService.AddDownloadAsync(url, ytPage.SavePath, DownloadCategory.Video);
                    task.FileName = Path.GetFileName(outputPath);
                    task.FilePath = outputPath;
                    task.Status = DownloadStatus.Completed;
                    task.CompletedAt = DateTime.UtcNow;
                    await downloadService.SaveAsync(task);
                    
                    _viewModel.Downloads.Insert(0, task);
                    _viewModel.UpdateStats();
                    _viewModel.ApplyFilter();
                }
                catch (Exception ex)
                {
                    await DialogHelper.ShowErrorAsync("Download Error", ex.Message);
                }
            }
        }
        
        private async void PauseAllButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.PauseAllCommand.ExecuteAsync(null);
            RefreshData();
        }
        
        private async void ResumeAllButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ResumeAllCommand.ExecuteAsync(null);
            RefreshData();
        }
        
        private async void ClearCompletedButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ClearCompletedCommand.ExecuteAsync(null);
            RefreshData();
        }
        
        // ---- Clipboard Banner ----
        private string _clipboardUrl = string.Empty;
        
        public void ShowClipboardBanner(string url)
        {
            _clipboardUrl = url;
            ClipboardUrlText.Text = url;
            ClipboardBanner.Visibility = Visibility.Visible;
            
            // Auto-close after 10 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000);
                DispatcherQueue.TryEnqueue(() =>
                {
                    ClipboardBanner.Visibility = Visibility.Collapsed;
                });
            });
        }
        
        private async void ClipboardDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_clipboardUrl))
            {
                await _viewModel.AddDownloadWithOptionsAsync(_clipboardUrl, null, Core.Models.DownloadCategory.General);
                ClipboardBanner.Visibility = Visibility.Collapsed;
                RefreshData();
            }
        }
        
        private void ClipboardDismissButton_Click(object sender, RoutedEventArgs e)
        {
            ClipboardBanner.Visibility = Visibility.Collapsed;
        }
    }
}
