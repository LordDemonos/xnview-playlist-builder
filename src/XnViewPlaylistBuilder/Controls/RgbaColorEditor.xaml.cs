using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Services;

namespace XnViewPlaylistBuilder.Controls;

public partial class RgbaColorEditor : UserControl
{
    private RgbaColor _color = RgbaColor.Black;

    public RgbaColorEditor()
    {
        InitializeComponent();
        ApplyColor(_color);
    }

    public string Label
    {
        get => LabelTextBlock.Text;
        set => LabelTextBlock.Text = value;
    }

    public RgbaColor Color
    {
        get => _color;
        set => ApplyColor(value);
    }

    public void SetColor(RgbaColor color) => ApplyColor(color);

    private void ApplyColor(RgbaColor color)
    {
        _color = color;
        ColorButton.Background = new SolidColorBrush(Win32DialogService.ToMediaColor(color));
        SummaryTextBlock.Text = color.ToSldValue();
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        if (Win32DialogService.TryPickColor(owner, _color, out var selected))
        {
            ApplyColor(selected);
        }
    }
}
