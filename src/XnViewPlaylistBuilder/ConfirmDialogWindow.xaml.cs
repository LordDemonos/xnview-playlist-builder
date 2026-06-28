using System.Windows;

namespace XnViewPlaylistBuilder;

public sealed class ConfirmDialogRequest
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? Detail { get; init; }
    public string? CheckBoxLabel { get; init; }
    public bool CheckBoxDefault { get; init; }
    public string ConfirmText { get; init; } = "Yes";
    public string CancelText { get; init; } = "No";
    public bool IsInformationOnly { get; init; }
}

public sealed class ConfirmDialogResult
{
    public bool Confirmed { get; init; }
    public bool IsOptionChecked { get; init; }
}

public partial class ConfirmDialogWindow : Window
{
    private ConfirmDialogWindow(ConfirmDialogRequest request)
    {
        InitializeComponent();

        Title = request.Title;
        TitleTextBlock.Text = request.Title;
        MessageTextBlock.Text = request.Message;

        if (!string.IsNullOrWhiteSpace(request.Detail))
        {
            DetailTextBlock.Text = request.Detail;
            DetailTextBlock.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(request.CheckBoxLabel))
        {
            OptionCheckBox.Content = request.CheckBoxLabel;
            OptionCheckBox.IsChecked = request.CheckBoxDefault;
            OptionCheckBox.Visibility = Visibility.Visible;
        }

        if (request.IsInformationOnly)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            ConfirmButton.Content = "OK";
            return;
        }

        ConfirmButton.Content = request.ConfirmText;
        CancelButton.Content = request.CancelText;
    }

    public static ConfirmDialogResult Show(Window owner, ConfirmDialogRequest request)
    {
        var dialog = new ConfirmDialogWindow(request)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true
            ? new ConfirmDialogResult { Confirmed = true, IsOptionChecked = dialog.OptionCheckBox.IsChecked == true }
            : new ConfirmDialogResult();
    }

    public static void ShowInfo(Window owner, string title, string message, string? detail = null)
    {
        Show(owner, new ConfirmDialogRequest
        {
            Title = title,
            Message = message,
            Detail = detail,
            IsInformationOnly = true
        });
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
