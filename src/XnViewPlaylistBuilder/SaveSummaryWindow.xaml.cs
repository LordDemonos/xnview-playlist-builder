using System.Windows;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder;

public partial class SaveSummaryWindow : Window
{
    public SaveSummaryWindow(SaveSummary summary)
    {
        InitializeComponent();
        BindSummary(summary);
    }

    private void BindSummary(SaveSummary summary)
    {
        OutputPathTextBlock.Text = summary.OutputPath;
        EntryCountTextBlock.Text = summary.EntryCount.ToString("N0");
        PathPolicyTextBlock.Text = summary.PathPolicyLabel;

        MissingCountTextBlock.Text = summary.MissingOnDiskCount == 0
            ? "None"
            : $"{summary.MissingOnDiskCount:N0} (stored paths preserved where available)";

        UnplayableCountTextBlock.Text = summary.FileHealthCheckSkipped
            ? "Skipped for large playlists — use Check files to scan before saving"
            : summary.UnplayableFileCount == 0
                ? "None — all files have content and recognizable image headers"
                : $"{summary.UnplayableFileCount:N0} ({summary.EmptyFileCount:N0} empty, {summary.InvalidImageCount:N0} invalid header)";

        if (summary.UnplayableFileCount > 0 && !summary.FileHealthCheckSkipped)
        {
            UnplayableFileWarningTextBlock.Visibility = Visibility.Visible;
            UnplayableFileWarningTextBlock.Text =
                $"{summary.UnplayableFileCount:N0} entries are empty or not valid images. XnView MP skips them silently during playback. Use Check files before saving to review or export the list.";
        }

        if (summary.NonAsciiPathCount > 0)
        {
            UnicodePathWarningTextBlock.Visibility = Visibility.Visible;
            UnicodePathWarningTextBlock.Text = summary.UseXnViewRelativePathsForUnicode
                ? $"{summary.NonAsciiPathCount:N0} non-ASCII entries will be saved as paths relative to the .sld file (Settings fallback)."
                : $"{summary.NonAsciiPathCount:N0} entries still use non-ASCII names. XnView MP may skip them with absolute paths. Use Fix names before saving, or enable the relative-path fallback in Settings.";
        }

        if (summary.IsExperimentalPathPolicy)
        {
            ExperimentalWarningTextBlock.Visibility = Visibility.Visible;
            ExperimentalWarningTextBlock.Text =
                "Relative-to-.sld paths are experimental. Verify the slideshow in XnView MP after moving files.";
        }

        PlaybackSummaryTextBlock.Text = summary.PlaybackSummary;
        OverlaySummaryTextBlock.Text = summary.OverlaySummary;
        EffectsSummaryTextBlock.Text = summary.EffectsSummary;

        if (string.IsNullOrWhiteSpace(summary.WindowSummary))
        {
            WindowSummaryLabelTextBlock.Visibility = Visibility.Collapsed;
            WindowSummaryTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            WindowSummaryTextBlock.Text = summary.WindowSummary;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
