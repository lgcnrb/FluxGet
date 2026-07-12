using System.Diagnostics;
using System.Reflection;
using FluxGet.Core.Services;
using Microsoft.UI.Xaml.Controls;

namespace FluxGet.UI.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Versiyon {version?.Major}.{version?.Minor}.{version?.Build ?? 0}";
        
        PlatformText.Text = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        RuntimeText.Text = $".NET {Environment.Version.Major}.{Environment.Version.Minor}.{Environment.Version.Build}";
        WinUIText.Text = "Windows App SDK 1.6";
    }
}
