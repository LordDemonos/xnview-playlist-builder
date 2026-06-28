using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Services;

public static class Win32DialogService
{
    public static bool TryPickColor(Window owner, RgbaColor current, out RgbaColor selected)
    {
        selected = current;
        using var dialog = new ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B),
            FullOpen = true
        };

        return dialog.ShowDialog(CreateWin32Window(owner)) == DialogResult.OK &&
               ApplyColor(dialog.Color, out selected);
    }

    public static bool TryPickFont(Window owner, string? currentValue, out string fontValue)
    {
        fontValue = currentValue ?? string.Empty;
        using var dialog = new FontDialog
        {
            ShowEffects = true
        };

        if (XnViewFontFormat.TryParse(currentValue, out var currentFont) && currentFont is not null)
        {
            dialog.Font = currentFont;
        }

        if (dialog.ShowDialog(CreateWin32Window(owner)) != DialogResult.OK)
        {
            return false;
        }

        fontValue = XnViewFontFormat.FromDrawingFont(dialog.Font);
        return true;
    }

    public static System.Windows.Media.Color ToMediaColor(RgbaColor color) =>
        System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);

    private static bool ApplyColor(System.Drawing.Color color, out RgbaColor selected)
    {
        selected = new RgbaColor(color.R, color.G, color.B, color.A);
        return true;
    }

    private static IWin32Window CreateWin32Window(Window owner)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(owner);
        return helper.Handle == IntPtr.Zero
            ? new SimpleWin32Window(IntPtr.Zero)
            : new SimpleWin32Window(helper.Handle);
    }

    private sealed class SimpleWin32Window : IWin32Window
    {
        public SimpleWin32Window(IntPtr handle) => Handle = handle;
        public IntPtr Handle { get; }
    }
}
