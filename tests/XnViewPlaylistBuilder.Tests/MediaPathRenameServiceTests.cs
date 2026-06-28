using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class MediaPathRenameServiceTests
{
    [Fact]
    public void BuildPlan_FolderSpacesOnly_DoesNotRenameAsciiSpacedFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnpb-rename-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "My Games");
        var nested = Path.Combine(dir, "Kato Best");

        try
        {
            Directory.CreateDirectory(nested);
            var files = Enumerable.Range(1, 10)
                .Select(index => Path.Combine(nested, $"file{index}.jpg"))
                .ToList();
            foreach (var file in files)
            {
                File.WriteAllText(file, "test");
            }

            var service = new MediaPathRenameService();
            var plan = service.BuildPlan(files.Select(Path.GetFullPath));

            Assert.Equal(0, plan.AffectedEntryCount);
            Assert.Empty(plan.Operations);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ExecutePlan_NestedSameNameFolders_RenamesAllPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnpb-rename-" + Guid.NewGuid().ToString("N"));
        var outerUnicode = Path.Combine(root, "サンプル (sample) - Cream Milk");
        var innerUnicode = Path.Combine(outerUnicode, "サンプル (sample) - Cream Milk");
        var imagesDir = Path.Combine(innerUnicode, "Images");
        var filePath = Path.Combine(imagesDir, "(1).jpg");

        try
        {
            Directory.CreateDirectory(imagesDir);
            File.WriteAllText(filePath, "test");

            var service = new MediaPathRenameService();
            var fullFilePath = Path.GetFullPath(filePath);
            var plan = service.BuildPlan([fullFilePath]);
            Assert.True(plan.Operations.Count >= 2, $"Expected folder renames, got {plan.Operations.Count}.");

            var result = service.ExecutePlan(plan);

            var expectedFile = plan.FilePathMap[fullFilePath];

            Assert.True(File.Exists(expectedFile), $"Expected file missing: {expectedFile}");
            Assert.False(Directory.Exists(outerUnicode));
            Assert.False(File.Exists(filePath));
            Assert.Equal(0, result.SkippedCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ExecutePlan_TargetAlreadyExists_RenamesWithConflictSuffix()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnpb-rename-" + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "on α off");
        var targetDir = Path.Combine(root, "on-off");
        var sourceFile = Path.Combine(sourceDir, "sample.jpg");
        var resolvedDir = Path.Combine(root, "on-off (1)");
        var resolvedFile = Path.Combine(resolvedDir, "sample.jpg");

        try
        {
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            File.WriteAllText(sourceFile, "test");

            var service = new MediaPathRenameService();
            var fullFilePath = Path.GetFullPath(sourceFile);
            var plan = service.BuildPlan([fullFilePath]);
            var result = service.ExecutePlan(plan);

            Assert.Equal(0, result.SkippedCount);
            Assert.Contains(
                result.ResolvedConflicts,
                conflict => conflict.ResolvedTargetPath.Equals(resolvedDir, StringComparison.OrdinalIgnoreCase));
            Assert.True(Directory.Exists(resolvedDir));
            Assert.True(File.Exists(resolvedFile));
            Assert.False(Directory.Exists(sourceDir));
            Assert.True(Directory.Exists(targetDir));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ExecutePlan_TargetAlreadyExists_IncrementsConflictSuffix()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnpb-rename-" + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "café");
        var sourceFile = Path.Combine(sourceDir, "cover.jpg");

        try
        {
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(Path.Combine(root, "cafe"));
            Directory.CreateDirectory(Path.Combine(root, "cafe (1)"));
            File.WriteAllText(sourceFile, "test");

            var service = new MediaPathRenameService();
            var plan = service.BuildPlan([Path.GetFullPath(sourceFile)]);
            var result = service.ExecutePlan(plan);

            Assert.Equal(0, result.SkippedCount);
            Assert.True(Directory.Exists(Path.Combine(root, "cafe (2)")));
            Assert.True(File.Exists(Path.Combine(root, "cafe (2)", "cover.jpg")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ExecutePlan_SourceMissingTargetExists_TreatsAsAlreadyRenamed()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnpb-rename-" + Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(root, "my fíle.jpg");
        var targetPath = Path.Combine(root, "my-file.jpg");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(targetPath, "test");

            var service = new MediaPathRenameService();
            var plan = service.BuildPlan([sourcePath]);
            var result = service.ExecutePlan(plan);

            Assert.Equal(0, result.SkippedCount);
            Assert.Contains(
                result.CompletedOperations,
                operation => operation.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(targetPath));
            Assert.False(File.Exists(sourcePath));
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
