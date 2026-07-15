using FluxGet.Core.Models;
using FluxGet.Core.Services;
using FluxGet.UI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace FluxGet.UI.Views
{
    public sealed partial class DownloadsPage : Page
    {
        private readonly MainViewModel _viewModel;
        
        public DownloadsPage()
        {
            InitializeComponent();
            _viewModel = App.GetService<MainViewModel>();
            DataContext = _viewModel;
            
            Loaded += (s, e) =>
            {
                _viewModel.SelectedStatus = null;
                UpdateEmptyState();
            };
        }
        
        private void UpdateEmptyState()
        {
            var hasItems = _viewModel.FilteredDownloads.Count > 0;
            EmptyStatePanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            DownloadsListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public void RefreshList()
        {
            _viewModel.ApplyFilter();
            UpdateEmptyState();
        }
        
        // ---- New Download Dialog (Advanced) ----
        private async void NewDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowAddDownloadDialog();
        }
        
        private async Task ShowAddDownloadDialog(string? prefillUrl = null)
        {
            var dialog = new ContentDialog
            {
                Title = "Add New Download",
                PrimaryButtonText = "Start Download",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                MaxWidth = 500,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var panel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            
            var urlLabel = new TextBlock { Text = "Download URL", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var urlBox = new TextBox
            {
                PlaceholderText = "Paste YouTube or file link",
                Text = prefillUrl ?? "",
                Width = 460
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
                Width = 200,
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
                UpdateEmptyState();
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
                MaxWidth = 550,
                HorizontalAlignment = HorizontalAlignment.Center
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
                var outputPath = Path.Combine(ytPage.SavePath, SanitizeFileName(ytPage.VideoInfo?.Title ?? "youtube_video"));
                
                try
                {
                    if (format.Extension == "mp3")
                    {
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
                    var errorDialog = new ContentDialog
                    {
                        Title = "Download Error",
                        Content = ex.Message,
                        CloseButtonText = "OK",
                        XamlRoot = App.MainWindow.Content.XamlRoot,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
        
        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Length > 100 ? name[..100] : name;
        }
        
        // ---- Filtering ----
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                _viewModel.SelectedStatus = tag switch
                {
                    "Active" => DownloadStatus.Downloading,
                    "Completed" => DownloadStatus.Completed,
                    "Paused" => DownloadStatus.Paused,
                    "Error" => DownloadStatus.Error,
                    _ => null
                };
                UpdateEmptyState();
            }
        }
        
        // ---- Item Click ----
        private void DownloadsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is DownloadTask task)
            {
                Frame.Navigate(typeof(DownloadDetailPage), task);
            }
        }
        
        // ---- More Options (Context Menu) ----
        private void MoreOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DownloadTask task)
            {
                var menu = new MenuFlyout();
                
                // Pause / Resume
                if (task.Status == DownloadStatus.Downloading)
                {
                    menu.Items.Add(new MenuFlyoutItem
                    {
                        Text = "Pause",
                        Icon = new FontIcon { Glyph = "\uE769" },
                        Command = _viewModel.PauseDownloadCommand,
                        CommandParameter = task
                    });
                }
                else if (task.Status == DownloadStatus.Paused || task.Status == DownloadStatus.Pending)
                {
                    menu.Items.Add(new MenuFlyoutItem
                    {
                        Text = "Resume",
                        Icon = new FontIcon { Glyph = "\uE768" },
                        Command = _viewModel.ResumeDownloadCommand,
                        CommandParameter = task
                    });
                }
                
                menu.Items.Add(new MenuFlyoutSeparator());
                
                // Retry Download
                if (task.Status == DownloadStatus.Completed || task.Status == DownloadStatus.Error || task.Status == DownloadStatus.Cancelled)
                {
                    menu.Items.Add(new MenuFlyoutItem
                    {
                        Text = "Retry Download",
                        Icon = new FontIcon { Glyph = "\uE72C" },
                        Command = _viewModel.StartDownloadCommand,
                        CommandParameter = task
                    });
                }
                
                // Cancel
                if (task.Status == DownloadStatus.Downloading || task.Status == DownloadStatus.Queued)
                {
                    menu.Items.Add(new MenuFlyoutItem
                    {
                        Text = "Cancel",
                        Icon = new FontIcon { Glyph = "\uE711" },
                        Command = _viewModel.CancelDownloadCommand,
                        CommandParameter = task
                    });
                }
                
                menu.Items.Add(new MenuFlyoutSeparator());
                
                // Open File
                if (task.Status == DownloadStatus.Completed)
                {
                    menu.Items.Add(new MenuFlyoutItem
                    {
                        Text = "Open File",
                        Icon = new FontIcon { Glyph = "\uE8A7" },
                        Command = _viewModel.OpenFileCommand,
                        CommandParameter = task
                    });
                    
                    menu.Items.Add(new MenuFlyoutItem
                    {
                        Text = "Open Folder",
                        Icon = new FontIcon { Glyph = "\uE838" },
                        Command = _viewModel.OpenFolderCommand,
                        CommandParameter = task
                    });
                }
                
                menu.Items.Add(new MenuFlyoutSeparator());
                
                // Copy URL
                var copyUrlItem = new MenuFlyoutItem
                {
                    Text = "Copy URL",
                    Icon = new FontIcon { Glyph = "\uE8C8" }
                };
                copyUrlItem.Click += (s, args) =>
                {
                    var clipboard = new DataPackage();
                    clipboard.SetText(task.Url);
                    Clipboard.SetContent(clipboard);
                };
                menu.Items.Add(copyUrlItem);
                
                menu.Items.Add(new MenuFlyoutSeparator());
                
                // Remove
                menu.Items.Add(new MenuFlyoutItem
                {
                    Text = "Remove",
                    Icon = new FontIcon { Glyph = "\uE74D" },
                    Command = _viewModel.RemoveDownloadCommand,
                    CommandParameter = task
                });
                
                menu.ShowAt(button);
            }
        }
        
        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            _viewModel.SearchText = sender.Text;
            UpdateEmptyState();
        }
        
        private void CategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                _viewModel.SelectedCategory = tag switch
                {
                    "Video" => DownloadCategory.Video,
                    "Music" => DownloadCategory.Music,
                    "Document" => DownloadCategory.Document,
                    "Archive" => DownloadCategory.Archive,
                    "Image" => DownloadCategory.Image,
                    "Software" => DownloadCategory.Software,
                    _ => null
                };
                UpdateEmptyState();
            }
        }
        
        // ---- Button Handlers ----
        private async void PauseResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DownloadTask task)
            {
                if (task.Status == DownloadStatus.Downloading)
                    await _viewModel.PauseDownloadCommand.ExecuteAsync(task);
                else if (task.Status == DownloadStatus.Paused || task.Status == DownloadStatus.Pending)
                    await _viewModel.ResumeDownloadCommand.ExecuteAsync(task);
                
                UpdateEmptyState();
            }
        }
        
        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DownloadTask task)
            {
                await _viewModel.OpenFileCommand.ExecuteAsync(task);
            }
        }
        
        private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DownloadTask task)
            {
                await _viewModel.OpenFolderCommand.ExecuteAsync(task);
            }
        }
        
        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DownloadTask task)
            {
                await _viewModel.CancelDownloadCommand.ExecuteAsync(task);
                UpdateEmptyState();
            }
        }
        
        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DownloadTask task)
            {
                await _viewModel.RemoveDownloadCommand.ExecuteAsync(task);
                UpdateEmptyState();
            }
        }
    }
}
