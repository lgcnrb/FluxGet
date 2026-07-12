using FluxGet.Core.Models;
using FluxGet.Core.Services;
using FluxGet.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluxGet.UI.Views;

    public sealed partial class QueuePage : Page
    {
        private readonly QueueViewModel _viewModel;
        private readonly IQueueService _queueService;
        
        public QueuePage()
        {
            InitializeComponent();
            _viewModel = App.GetService<QueueViewModel>();
            _queueService = App.GetService<IQueueService>();
            DataContext = _viewModel;
            
            Loaded += (s, e) =>
            {
                MaxConcurrentBox.Value = _queueService.MaxConcurrent;
                _viewModel.LoadQueueCommand.Execute(null);
                UpdateStatus();
                UpdateEmptyState();
            };
        }
        
        public void RefreshQueue()
        {
            _viewModel.LoadQueueCommand.Execute(null);
            UpdateStatus();
            UpdateEmptyState();
        }
    
    private void UpdateStatus()
    {
        var active = _queueService.ActiveCount;
        var queued = _queueService.PendingCount;
        QueueStatusText.Text = $"{queued} queued, {active} active";
    }
    
    private void UpdateEmptyState()
    {
        var hasItems = _viewModel.QueueItems.Count > 0;
        EmptyStatePanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        QueueListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }
    
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _queueService.MaxConcurrent = (int)MaxConcurrentBox.Value;
        UpdateStatus();
    }
    
    private async void PauseQueueButton_Click(object sender, RoutedEventArgs e)
    {
        await _queueService.PauseQueueAsync();
        UpdateStatus();
    }
    
    private async void ResumeQueueButton_Click(object sender, RoutedEventArgs e)
    {
        await _queueService.ResumeQueueAsync();
        UpdateStatus();
    }
    
    private async void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DownloadTask task)
        {
            await _queueService.MoveUpAsync(task);
            _viewModel.LoadQueueCommand.Execute(null);
            UpdateStatus();
        }
    }
    
    private async void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DownloadTask task)
        {
            await _queueService.MoveDownAsync(task);
            _viewModel.LoadQueueCommand.Execute(null);
            UpdateStatus();
        }
    }
    
    private async void PriorityUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DownloadTask task)
        {
            await _queueService.SetPriorityAsync(task, task.Priority + 1);
            _viewModel.LoadQueueCommand.Execute(null);
            UpdateStatus();
        }
    }
    
    private async void PriorityDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DownloadTask task)
        {
            await _queueService.SetPriorityAsync(task, task.Priority - 1);
            _viewModel.LoadQueueCommand.Execute(null);
            UpdateStatus();
        }
    }
    
    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DownloadTask task)
        {
            await _queueService.RemoveFromQueueAsync(task);
            _viewModel.LoadQueueCommand.Execute(null);
            UpdateEmptyState();
            UpdateStatus();
        }
    }
    
    private async void ClearQueueButton_Click(object sender, RoutedEventArgs e)
    {
        await _queueService.ClearQueueAsync();
        _viewModel.LoadQueueCommand.Execute(null);
        UpdateEmptyState();
        UpdateStatus();
    }
    
    private async void StartAllButton_Click(object sender, RoutedEventArgs e)
    {
        await _queueService.ResumeQueueAsync();
        _viewModel.LoadQueueCommand.Execute(null);
        UpdateStatus();
    }
}
