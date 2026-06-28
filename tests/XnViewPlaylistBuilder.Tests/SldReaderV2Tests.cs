using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class SldReaderV2Tests
{
    [Fact]
    public void Read_ParsesOptionsAndPathsFromV2File()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var image = Path.Combine(tempDir, "photo.jpg");
        File.WriteAllText(image, "x");

        var sldPath = Path.Combine(tempDir, "sample.sld");
        var writer = new SldWriterV2();
        var options = SldOptionsV2.CreateDefaults();
        options.Timer = 20;
        options.ShowInfo = true;
        options.Info = "{Filename}";
        writer.Write(sldPath, options, [Path.GetFullPath(image)]);

        try
        {
            var reader = new SldReaderV2();
            var playlist = reader.Read(sldPath);

            Assert.Equal(20, playlist.Options.Timer);
            Assert.True(playlist.Options.ShowInfo);
            Assert.Single(playlist.Entries);
            Assert.Equal(Path.GetFullPath(image), playlist.Entries[0].AbsolutePath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Read_PreservesRelativePathsWhenFileMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-rel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sldPath = Path.Combine(tempDir, "relative.sld");
        File.WriteAllText(sldPath, """
            # Slide Show Sequence v2
            UseTimer = 1
            Timer = 15
            Loop = 1
            FullScreen = 1
            WinWidth = 640
            WinHeight = 480
            Stretch = 1
            RandomOrder = 1
            ShowInfo = 0
            Info = {Filename}
            TitleBar = 0
            OnTop = 0
            CursorAutoHide = 0
            BackgroundColor = 0 0 0 255
            TextColor = 255 255 255 255
            UseTextBackColor = 0
            TextPosition = 0
            TextBackColor = 128 128 128 255
            Opacity = 100
            Font = MS Shell Dlg 2,8.25,-1,5,50,0,0,0,0,0
            EffectDuration = 1000
            Effects = 1 2 3 
            "missing/folder/photo.jpg"
            """);

        try
        {
            var playlist = new SldReaderV2().Read(sldPath);

            Assert.Single(playlist.Entries);
            Assert.Equal(@"missing\folder\photo.jpg", playlist.Entries[0].StoredPath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void UnquotePath_HandlesQuotedWindowsPath()
    {
        Assert.Equal(@"D:\media\a.jpg", SldReaderV2.UnquotePath(@"""D:\media\a.jpg"""));
    }
}

public class EntryMergeTests
{
    [Fact]
    public void Merge_DeduplicatesByAbsolutePath()
    {
        var a = Path.Combine(Path.GetTempPath(), "a.jpg");
        var existing = new List<MediaEntry>
        {
            new() { AbsolutePath = a, SourceRootIndex = 0 }
        };
        var scanned = new List<MediaEntry>
        {
            new() { AbsolutePath = a, SourceRootIndex = 1 },
            new() { AbsolutePath = Path.Combine(Path.GetTempPath(), "b.jpg"), SourceRootIndex = 1 }
        };

        var merged = EntryMerge.Merge(existing, scanned);

        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void Merge_WhenAllowDuplicates_KeepsAllEntries()
    {
        var a = Path.Combine(Path.GetTempPath(), "a.jpg");
        var existing = new List<MediaEntry>
        {
            new() { AbsolutePath = a, SourceRootIndex = 0 }
        };
        var scanned = new List<MediaEntry>
        {
            new() { AbsolutePath = a, SourceRootIndex = 1 },
            new() { AbsolutePath = Path.Combine(Path.GetTempPath(), "b.jpg"), SourceRootIndex = 1 }
        };

        var merged = EntryMerge.Merge(existing, scanned, allowDuplicates: true);

        Assert.Equal(3, merged.Count);
    }

    [Fact]
    public void Merge_DeduplicatesImportedAndScannedPathsByAbsolutePath()
    {
        var fullPath = Path.Combine(Path.GetTempPath(), "merged", "a.jpg");
        var existing = new List<MediaEntry>
        {
            new()
            {
                AbsolutePath = fullPath,
                StoredPath = @"relative\a.jpg",
                SourceRootIndex = 0
            }
        };
        var scanned = new List<MediaEntry>
        {
            new() { AbsolutePath = fullPath, SourceRootIndex = 1 }
        };

        var merged = EntryMerge.Merge(existing, scanned);

        Assert.Single(merged);
        Assert.Equal(fullPath, merged[0].AbsolutePath);
    }

    [Fact]
    public void EntryKey_DoesNotRequireFileOnDisk()
    {
        var key = EntryMerge.EntryKey(new MediaEntry
        {
            AbsolutePath = @"\\server\share\u\Astro_bunnies\photo.jpg",
            SourceRootIndex = 0
        });

        Assert.Equal(@"\\server\share\u\Astro_bunnies\photo.jpg", key);
    }

    [Fact]
    public void EntryKey_NormalizesSeparatorsAndTrailingSlashes()
    {
        var key = EntryMerge.EntryKey(new MediaEntry
        {
            AbsolutePath = @"E:/Photos/Vacation/img.jpg/",
            SourceRootIndex = 0
        });

        Assert.Equal(@"E:\Photos\Vacation\img.jpg", key);
    }
}
