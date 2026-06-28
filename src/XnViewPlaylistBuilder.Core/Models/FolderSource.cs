using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XnViewPlaylistBuilder.Core.Models;

public sealed class FolderSource : INotifyPropertyChanged
{
    private bool _includeSubfolders = true;
    private bool _useWildcardLine;
    private bool _showCollapsedSubfolders;

    public required string AbsolutePath { get; init; }

    public IReadOnlyList<string> CollapsedSubfolderPaths { get; init; } = [];

    public bool HasCollapsedSubfolders => CollapsedSubfolderPaths.Count > 0;

    public string CollapsedSubfoldersLabel =>
        ShowCollapsedSubfolders
            ? "Hide collapsed subfolders"
            : $"Show {CollapsedSubfolderPaths.Count:N0} collapsed subfolder(s)";

    public bool ShowCollapsedSubfolders
    {
        get => _showCollapsedSubfolders;
        set
        {
            if (_showCollapsedSubfolders == value)
            {
                return;
            }

            _showCollapsedSubfolders = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CollapsedSubfoldersLabel));
        }
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set
        {
            if (_includeSubfolders == value)
            {
                return;
            }

            _includeSubfolders = value;
            OnPropertyChanged();
        }
    }

    public bool UseWildcardLine
    {
        get => _useWildcardLine;
        set
        {
            if (_useWildcardLine == value)
            {
                return;
            }

            _useWildcardLine = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
