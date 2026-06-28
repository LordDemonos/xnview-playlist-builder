using System.Windows;

namespace XnViewPlaylistBuilder;

public partial class PresetNameWindow : Window
{
    public PresetNameWindow(string? initialName = null)
    {
        InitializeComponent();
        PresetNameTextBox.Text = initialName ?? string.Empty;
        PresetNameTextBox.SelectAll();
        PresetNameTextBox.Focus();
    }

    public string PresetName => PresetNameTextBox.Text.Trim();

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PresetName))
        {
            MessageBox.Show(this, "Enter a preset name.", "Save preset", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
