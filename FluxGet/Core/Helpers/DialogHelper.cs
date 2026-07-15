using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluxGet.Core.Helpers;

public static class DialogHelper
{
    public static async Task ShowInfoAsync(string title, string message)
    {
        try
        {
            await new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Width = 400
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show info dialog: {ex.Message}");
        }
    }
    
    public static async Task<ContentDialogResult> ShowConfirmAsync(
        string title, 
        string message, 
        string primaryButtonText = "OK", 
        string closeButtonText = "Cancel",
        double width = 400)
    {
        try
        {
            return await new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = closeButtonText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Width = width
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show confirm dialog: {ex.Message}");
            return ContentDialogResult.None;
        }
    }
    
    public static async Task<ContentDialogResult> ShowErrorAsync(string title, string message)
    {
        try
        {
            return await new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Width = 400
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show error dialog: {ex.Message}");
            return ContentDialogResult.None;
        }
    }
}
