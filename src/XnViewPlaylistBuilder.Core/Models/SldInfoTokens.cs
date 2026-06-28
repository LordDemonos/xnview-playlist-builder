namespace XnViewPlaylistBuilder.Core.Models;

public sealed record InfoTokenGroup(string Label, IReadOnlyList<(string Token, string Description)> Tokens);

public static class SldInfoTokens
{
    public const string Filename = "{Filename}";
    public const string FilenameWithExt = "{Filename With Ext}";
    public const string FolderName = "{Folder name}";
    public const string Directory = "{Directory}";
    public const string DefaultTemplate = "{Folder name} - {Filename}";
    public const string FullPathTemplate = "{Directory}{Filename With Ext}";

    public static IReadOnlyList<InfoTokenGroup> InsertMenuGroups { get; } =
    [
        new("Path / name", [
            (Filename, "File name (extension behavior varies by XnView version)."),
            (FilenameWithExt, "File name with extension."),
            (FolderName, "Leaf folder name only (not full path)."),
            (Directory, "Full directory path."),
            (FullPathTemplate, "Full file path (combine directory + file name).")
        ]),
        new("Size / dimensions", [
            ("{Width}", "Image width in pixels."),
            ("{Height}", "Image height in pixels."),
            ("{Size}", "File size."),
            ("{Size KB}", "File size in kilobytes.")
        ]),
        new("Dates", [
            ("{Modified Date}", "File modified date."),
            ("{Creation Date}", "File creation date."),
            ("{Accessed Date}", "File last accessed date."),
            ("{Current date}", "Current date/time.")
        ]),
        new("EXIF (examples)", [
            ("{EXIF:Make}", "Camera make."),
            ("{EXIF:Model}", "Camera model."),
            ("{EXIF:Date taken}", "Date the photo was taken."),
            ("{EXIF:ISO Value}", "ISO sensitivity."),
            ("{EXIF:Focal Length}", "Lens focal length.")
        ]),
        new("Other", [
            ("{Zoom}", "Current zoom level."),
            ("{Comment}", "File comment."),
            ("{Annotation}", "Annotation text."),
            ("{Rating}", "Star rating."),
            ("{Categories}", "Catalog categories."),
            ("{Color label}", "Color label.")
        ])
    ];

    public static IReadOnlyList<(string Token, string Description)> KnownTokens =>
        InsertMenuGroups.SelectMany(group => group.Tokens).ToArray();

    public static string HelpText =>
        """
        XnView MP Info template tokens. Combine with spaces or punctuation.

        Path / name
        {Filename} — File name (extension behavior varies by version)
        {Filename With Ext} — File name with extension
        {Folder name} — Leaf folder name only (not full path)
        {Directory} — Full directory path

        Full path examples:
        {Directory}{Filename With Ext}
        {Directory}\{Filename With Ext}

        Size / dimensions
        {Width}, {Height}, {Size}, {Size KB}

        Dates
        {Modified Date}, {Creation Date}, {Accessed Date}
        {Current date}, {Current Date [Y-m-d_H-M-S]}, etc.

        EXIF (examples)
        {EXIF:Make}, {EXIF:Model}, {EXIF:Date taken}
        {EXIF:ISO Value}, {EXIF:Focal Length}, …

        Other
        {Zoom}, {Comment}, {Annotation}, {Rating}
        {Categories}, {Color label}

        Metadata
        Also supports {IPTC:…}, {XMP:…}, and {Exiftool:group:tag}
        (same syntax as the viewer Info overlay).

        Default example: {Folder name} - {Filename}
        """;

    public static bool TemplateShowsFilename(string? template) =>
        !string.IsNullOrWhiteSpace(template) &&
        (template.Contains(Filename, StringComparison.OrdinalIgnoreCase) ||
         template.Contains(FilenameWithExt, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return DefaultTemplate;
        }

        return template.Trim();
    }
}
