using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class OptionsPresetStoreTests
{
    [Fact]
    public void SaveAndLoad_PreservesAllOptionKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-preset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = new OptionsPresetStore(presetsDirectory: root);
            var options = new SldOptionsV2
            {
                UseTimer = false,
                Timer = 7,
                Loop = false,
                FullScreen = false,
                WinWidth = 800,
                WinHeight = 600,
                Stretch = false,
                RandomOrder = false,
                ShowInfo = true,
                Info = "{Filename} test",
                TitleBar = true,
                OnTop = true,
                CursorAutoHide = true,
                BackgroundColor = new RgbaColor(1, 2, 3, 255),
                TextColor = new RgbaColor(4, 5, 6, 255),
                UseTextBackColor = true,
                TextPosition = 3,
                TextBackColor = new RgbaColor(7, 8, 9, 255),
                Opacity = 80,
                Font = "Arial,10",
                EffectDuration = 500,
                Effects = [1, 3, 5]
            };

            store.Save("Test Preset", options);
            var loaded = store.Load("Test Preset");

            Assert.Equal(options.UseTimer, loaded.UseTimer);
            Assert.Equal(options.Timer, loaded.Timer);
            Assert.Equal(options.Loop, loaded.Loop);
            Assert.Equal(options.FullScreen, loaded.FullScreen);
            Assert.Equal(options.WinWidth, loaded.WinWidth);
            Assert.Equal(options.WinHeight, loaded.WinHeight);
            Assert.Equal(options.Stretch, loaded.Stretch);
            Assert.Equal(options.RandomOrder, loaded.RandomOrder);
            Assert.Equal(options.ShowInfo, loaded.ShowInfo);
            Assert.Equal(options.Info, loaded.Info);
            Assert.Equal(options.TitleBar, loaded.TitleBar);
            Assert.Equal(options.OnTop, loaded.OnTop);
            Assert.Equal(options.CursorAutoHide, loaded.CursorAutoHide);
            Assert.Equal(options.BackgroundColor, loaded.BackgroundColor);
            Assert.Equal(options.TextColor, loaded.TextColor);
            Assert.Equal(options.UseTextBackColor, loaded.UseTextBackColor);
            Assert.Equal(options.TextPosition, loaded.TextPosition);
            Assert.Equal(options.TextBackColor, loaded.TextBackColor);
            Assert.Equal(options.Opacity, loaded.Opacity);
            Assert.Equal(options.Font, loaded.Font);
            Assert.Equal(options.EffectDuration, loaded.EffectDuration);
            Assert.Equal(options.Effects, loaded.Effects);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
