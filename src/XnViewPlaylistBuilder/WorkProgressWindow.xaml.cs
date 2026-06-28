using System.ComponentModel;
using System.Windows;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder;

public partial class WorkProgressWindow : Window
{
    private CancellationTokenSource? _cancellationSource;

    public WorkProgressWindow()
    {
        InitializeComponent();
    }

    public async Task<T> RunAsync<T>(
        string title,
        Func<IProgress<WorkProgressReport>, CancellationToken, Task<T>> work)
    {
        Title = title;
        TitleTextBlock.Text = title;
        _cancellationSource = new CancellationTokenSource();
        var progress = new Progress<WorkProgressReport>(ApplyReport);

        try
        {
            return await work(progress, _cancellationSource.Token);
        }
        finally
        {
            _cancellationSource.Dispose();
            _cancellationSource = null;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_cancellationSource is { IsCancellationRequested: false })
        {
            _cancellationSource.Cancel();
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        _cancellationSource?.Cancel();

    private void ApplyReport(WorkProgressReport report)
    {
        StatusTextBlock.Text = report.Status;
        DetailTextBlock.Text = report.Detail ?? string.Empty;

        if (report.PercentComplete is double percent)
        {
            WorkProgressBar.IsIndeterminate = false;
            WorkProgressBar.Value = Math.Clamp(percent, 0, 100);
        }
        else
        {
            WorkProgressBar.IsIndeterminate = true;
        }
    }
}
