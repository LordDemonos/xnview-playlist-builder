using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Tests;

public class SldInfoTokensTests
{
    [Fact]
    public void HelpText_DocumentsPathTokensAndFullPathExample()
    {
        Assert.Contains("{Directory}", SldInfoTokens.HelpText);
        Assert.Contains("{Filename With Ext}", SldInfoTokens.HelpText);
        Assert.Contains("{Directory}{Filename With Ext}", SldInfoTokens.HelpText);
        Assert.Contains("{IPTC:", SldInfoTokens.HelpText);
    }

    [Fact]
    public void TemplateShowsFilename_AcceptsFilenameWithExt()
    {
        Assert.True(SldInfoTokens.TemplateShowsFilename("{Directory}{Filename With Ext}"));
        Assert.False(SldInfoTokens.TemplateShowsFilename("{Folder name}"));
    }

    [Fact]
    public void InsertMenuGroups_UseXnViewTokenNames()
    {
        var tokens = SldInfoTokens.KnownTokens.Select(entry => entry.Token).ToArray();

        Assert.Contains("{Size KB}", tokens);
        Assert.Contains("{Modified Date}", tokens);
        Assert.DoesNotContain("{File size}", tokens);
        Assert.DoesNotContain("{Image size}", tokens);
    }
}
