using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder;

public partial class MediaHealthReportWindow : Window
{
    private readonly int _totalChecked;
    private readonly ObservableCollection<MediaHealthRow> _rows = [];
    private readonly List<RemovedPathRecord> _removedPaths = [];

    public int DeletedEmptyFileCount { get; private set; }

    public IReadOnlyList<string> PathsRemovedFromPlaylist =>
        _removedPaths.Select(record => record.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<RemovedPathRecord> RemovedPathRecords => _removedPaths;

    public bool HasRemainingIssues => _rows.Count > 0;

    public MediaHealthReportWindow(MediaFileHealthReport report)
    {
        InitializeComponent();
        _totalChecked = report.TotalChecked;
        foreach (var finding in report.Findings)
        {
            _rows.Add(new MediaHealthRow(finding));
        }

        RefreshSummary();
        FindingsDataGrid.ItemsSource = _rows;
    }

    private void RefreshSummary()
    {
        var emptyCount = _rows.Count(row => row.Issue == MediaFileHealthIssue.Empty);
        var invalidCount = _rows.Count(row => row.Issue == MediaFileHealthIssue.InvalidImageHeader);
        var missingCount = _rows.Count(row => row.Issue == MediaFileHealthIssue.Missing);
        var issueCount = _rows.Count;
        var healthyCount = _totalChecked - issueCount;

        SummaryTextBlock.Text =
            $"{healthyCount:N0} of {_totalChecked:N0} files look readable. " +
            "Empty files are excluded when you scan; invalid files may still be in the playlist until you remove them. " +
            "The table below lists every issue. XnView MP skips these silently during playback.";

        MissingCountTextBlock.Text = missingCount.ToString("N0");
        EmptyCountTextBlock.Text = emptyCount.ToString("N0");
        InvalidCountTextBlock.Text = invalidCount.ToString("N0");

        RemoveMissingFromPlaylistButton.Visibility = missingCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        RemoveInvalidFromPlaylistButton.Visibility = invalidCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        DeleteEmptyFilesButton.Visibility = emptyCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RemoveMissingFromPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        var missingPaths = _rows
            .Where(row => row.Issue == MediaFileHealthIssue.Missing)
            .Select(row => row.Path)
            .ToList();

        if (missingPaths.Count == 0)
        {
            return;
        }

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Remove missing from playlist",
            Message = $"Remove {missingPaths.Count:N0} missing file(s) from the playlist?",
            Detail = MediaFileHealthChecker.FormatPathExamples(missingPaths),
            ConfirmText = "Remove from playlist",
            CancelText = "Cancel"
        });

        if (!confirm.Confirmed)
        {
            return;
        }

        RemoveRowsFromPlaylist(missingPaths, MediaFileHealthIssue.Missing);
        RefreshSummary();
        DialogResult = true;
    }

    private void RemoveInvalidFromPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        var invalidPaths = _rows
            .Where(row => row.Issue == MediaFileHealthIssue.InvalidImageHeader)
            .Select(row => row.Path)
            .ToList();

        if (invalidPaths.Count == 0)
        {
            return;
        }

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Remove invalid from playlist",
            Message = $"Remove {invalidPaths.Count:N0} invalid image(s) from the playlist?",
            Detail = "These files exist on disk but do not have a recognizable image header. " +
                     "XnView MP will skip them during playback." +
                     Environment.NewLine +
                     Environment.NewLine +
                     MediaFileHealthChecker.FormatPathExamples(invalidPaths),
            ConfirmText = "Remove from playlist",
            CancelText = "Cancel"
        });

        if (!confirm.Confirmed)
        {
            return;
        }

        RemoveRowsFromPlaylist(invalidPaths, MediaFileHealthIssue.InvalidImageHeader);
        RefreshSummary();
        DialogResult = true;
    }

    private async void DeleteEmptyFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var emptyPaths = _rows
            .Where(row => row.Issue == MediaFileHealthIssue.Empty)
            .Select(row => row.Path)
            .ToList();

        if (emptyPaths.Count == 0)
        {
            return;
        }

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Delete empty files",
            Message = $"Permanently delete {emptyPaths.Count:N0} empty file(s) from disk and remove them from the playlist?",
            Detail = MediaFileHealthChecker.FormatPathExamples(emptyPaths),
            ConfirmText = "Delete and remove",
            CancelText = "Cancel"
        });

        if (!confirm.Confirmed)
        {
            return;
        }

        DeleteEmptyFilesButton.IsEnabled = false;
        RemoveMissingFromPlaylistButton.IsEnabled = false;
        RemoveInvalidFromPlaylistButton.IsEnabled = false;
        var progressWindow = new WorkProgressWindow { Owner = Owner ?? this };
        progressWindow.Show();

        EmptyFileDeleteResult result;
        try
        {
            result = await progressWindow.RunAsync(
                "Deleting empty files…",
                (progress, token) => Task.Run(
                    () => MediaFileHealthChecker.DeleteEmptyFiles(emptyPaths, progress, token),
                    token));
        }
        catch (OperationCanceledException)
        {
            DeleteEmptyFilesButton.IsEnabled = true;
            RemoveMissingFromPlaylistButton.IsEnabled = true;
            RemoveInvalidFromPlaylistButton.IsEnabled = true;
            return;
        }
        finally
        {
            progressWindow.Close();
        }

        DeletedEmptyFileCount += result.DeletedCount;

        var deletedPaths = emptyPaths
            .Except(result.FailedPaths, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RemoveRowsFromPlaylist(deletedPaths, MediaFileHealthIssue.Empty);
        RefreshSummary();
        DeleteEmptyFilesButton.IsEnabled = true;
        RemoveMissingFromPlaylistButton.IsEnabled = true;
        RemoveInvalidFromPlaylistButton.IsEnabled = true;

        if (result.FailedPaths.Count > 0)
        {
            MessageBox.Show(
                this,
                $"Deleted {result.DeletedCount:N0} empty file(s) from disk and queued them for playlist removal. " +
                $"{result.FailedPaths.Count:N0} could not be deleted and remain in the report.",
                "Delete empty files",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        DialogResult = true;
    }

    private void RemoveRowsFromPlaylist(
        IEnumerable<string> paths,
        MediaFileHealthIssue issue)
    {
        var pathSet = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (pathSet.Count == 0)
        {
            return;
        }

        foreach (var path in pathSet)
        {
            _removedPaths.Add(new RemovedPathRecord(path, issue));
        }

        for (var index = _rows.Count - 1; index >= 0; index--)
        {
            if (_rows[index].Issue == issue && pathSet.Contains(_rows[index].Path))
            {
                _rows.RemoveAt(index);
            }
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export file health report",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "playlist-file-health.txt",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildExportText(), Encoding.UTF8);
        MessageBox.Show(this, $"Report saved to{Environment.NewLine}{dialog.FileName}", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private string BuildExportText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("XnView Playlist Builder — file health report");
        builder.AppendLine(SummaryTextBlock.Text);
        builder.AppendLine($"Missing: {MissingCountTextBlock.Text}");
        builder.AppendLine($"Empty: {EmptyCountTextBlock.Text}");
        builder.AppendLine($"Invalid: {InvalidCountTextBlock.Text}");
        builder.AppendLine();

        foreach (var row in _rows.OrderBy(row => row.Issue).ThenBy(row => row.Path, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{row.Detail}\t{row.SizeLabel}\t{row.Path}");
        }

        return builder.ToString();
    }

    private static string DescribeIssue(MediaFileHealthIssue issue) =>
        issue switch
        {
            MediaFileHealthIssue.Missing => "Missing",
            MediaFileHealthIssue.Empty => "Empty (0 bytes)",
            MediaFileHealthIssue.InvalidImageHeader => "Invalid image",
            _ => issue.ToString()
        };

    private sealed class MediaHealthRow
    {
        public MediaHealthRow(MediaFileHealthFinding finding)
        {
            Path = finding.Path;
            Issue = finding.Issue;
            Detail = DescribeIssue(finding.Issue);
            SizeLabel = finding.SizeBytes switch
            {
                null => "—",
                0 => "0 B",
                var bytes when bytes < 1024 => $"{bytes} B",
                var bytes => $"{bytes / 1024.0:F1} KB"
            };
        }

        public string Path { get; }
        public MediaFileHealthIssue Issue { get; }
        public string Detail { get; }
        public string SizeLabel { get; }
    }

    public sealed record RemovedPathRecord(string Path, MediaFileHealthIssue Issue);
}
