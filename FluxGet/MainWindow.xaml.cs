using FluxGet.Core.Services;
using FluxGet.UI.ViewModels;
using FluxGet.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace FluxGet
{
    public sealed partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _clipboardTimer;
        private string _lastClipboardText = string.Empty;
        
        public MainWindow()
        {
            InitializeComponent();
            
            _viewModel = App.GetService<MainViewModel>();
            
            if (Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = _viewModel;
                rootElement.KeyDown += MainWindow_KeyDown;
            }
            
            SetupTitleBar();
            
            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(DashboardPage));
            
            // Refresh every 500ms
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
            
            // Clipboard monitoring - check every 2 seconds
            _clipboardTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _clipboardTimer.Tick += ClipboardTimer_Tick;
            _clipboardTimer.Start();
        }
        
        private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.F5)
            {
                if (ContentFrame.Content is DashboardPage dashboard)
                {
                    dashboard.RefreshData();
                }
            }
        }
        
        private void ClipboardTimer_Tick(object? sender, object e)
        {
            _ = CheckClipboardForUrl();
        }
        
        private async Task CheckClipboardForUrl()
        {
            try
            {
                var clipboard = Clipboard.GetContent();
                if (clipboard.Contains(StandardDataFormats.Text))
                {
                    var text = await clipboard.GetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text) && text != _lastClipboardText && IsValidUrl(text))
                    {
                        _lastClipboardText = text;
                        ShowClipboardNotification(text.Trim());
                    }
                }
            }
            catch { }
        }
        
        private void ShowClipboardNotification(string url)
        {
            if (ContentFrame.Content is DashboardPage dashboard)
            {
                dashboard.ShowClipboardBanner(url);
            }
        }
        
        private static bool IsValidUrl(string text)
        {
            return Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) 
                && (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "ftp");
        }
        
        private void RefreshTimer_Tick(object? sender, object e)
        {
            if (ContentFrame.Content is DashboardPage dashboard)
            {
                dashboard.RefreshData();
            }
            else if (ContentFrame.Content is DownloadsPage downloads)
            {
                downloads.RefreshList();
            }
            else if (ContentFrame.Content is QueuePage queue)
            {
                queue.RefreshQueue();
            }
            else if (ContentFrame.Content is DownloadDetailPage detail)
            {
                detail.RefreshData();
            }
        }
        
        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.DragUIOverride.Caption = "Drop URL to start download";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        
        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                var text = await e.DataView.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text) && IsValidUrl(text.Trim()))
                {
                    try
                    {
                        var downloadService = App.GetService<IDownloadService>();
                        var task = await downloadService.AddDownloadAsync(text.Trim(), null, Core.Models.DownloadCategory.General);
                        
                        var mainVm = App.GetService<MainViewModel>();
                        mainVm.Downloads.Insert(0, task);
                        mainVm.UpdateStats();
                        mainVm.ApplyFilter();
                        
                        var queueService = App.GetService<IQueueService>();
                        await queueService.EnqueueAsync(task);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Drag-drop download error: {ex.Message}");
                    }
                }
            }
        }
        
        private void SetupTitleBar()
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.BackgroundColor = Microsoft.UI.Colors.Black;
            titleBar.ForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Black;
            titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonHoverBackgroundColor = Microsoft.UI.Colors.DarkGray;
            titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonPressedBackgroundColor = Microsoft.UI.Colors.Gray;
            titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
            
            ExtendsContentIntoTitleBar = true;
        }
        
        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();
                
                Type? pageType = tag switch
                {
                    "Dashboard" => typeof(DashboardPage),
                    "Downloads" => typeof(DownloadsPage),
                    "Queue" => typeof(QueuePage),
                    "YouTube" => typeof(YouTubePage),
                    "Tools" => typeof(ToolsPage),
                    "Settings" => typeof(SettingsPage),
                    "About" => typeof(AboutPage),
                    _ => typeof(DashboardPage)
                };
                
                if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
                {
                    ContentFrame.Navigate(pageType);
                }
            }
        }
        
        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(DashboardPage))
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
            else if (e.SourcePageType == typeof(DownloadsPage))
            {
                NavView.SelectedItem = NavView.MenuItems[1];
            }
            else if (e.SourcePageType == typeof(QueuePage))
            {
                NavView.SelectedItem = NavView.MenuItems[2];
            }
            else if (e.SourcePageType == typeof(YouTubePage))
            {
                NavView.SelectedItem = NavView.MenuItems[3];
            }
            else if (e.SourcePageType == typeof(ToolsPage))
            {
                NavView.SelectedItem = NavView.FooterMenuItems[0];
            }
            else if (e.SourcePageType == typeof(AboutPage))
            {
                NavView.SelectedItem = NavView.FooterMenuItems[2];
            }
            else if (e.SourcePageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = NavView.FooterMenuItems[1];
            }
        }
        
        public void NavigateToSettings()
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }
    }
}
