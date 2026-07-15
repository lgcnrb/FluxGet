using FluxGet.Core.Helpers;
using FluxGet.Core.Services;
using FluxGet.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FluxGet.UI.Views;

public sealed partial class YouTubePage : Page
{
    public YouTubeViewModel VM { get; }
    
    public YouTubePage()
    {
        VM = App.GetService<YouTubeViewModel>();
        InitializeComponent();
        DataContext = VM;
    }
    
    private void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            VM.FetchInfoCommand.Execute(null);
        }
    }
    
    private void Mp4Card_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        VM.IsMp4Mode = true;
        UpdateFormatCardStyles();
    }
    
    private void Mp3Card_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        VM.IsMp4Mode = false;
        UpdateFormatCardStyles();
    }
    
    private void UpdateFormatCardStyles()
    {
        var activeBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
        var defaultBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        
        Mp4Card.BorderBrush = VM.IsMp4Mode ? activeBrush : defaultBrush;
        Mp4Card.BorderThickness = VM.IsMp4Mode ? new Thickness(2) : new Thickness(1);
        
        Mp3Card.BorderBrush = !VM.IsMp4Mode ? activeBrush : defaultBrush;
        Mp3Card.BorderThickness = !VM.IsMp4Mode ? new Thickness(2) : new Thickness(1);
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
            VM.SavePath = folder.Path;
        }
    }
    
    private void GoToSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        (App.MainWindow as MainWindow)?.NavigateToSettings();
    }
}
