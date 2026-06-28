# XnView Playlist Builder

Windows desktop tool for building [XnView MP](https://www.xnview.com/en/xnviewmp/) `.sld` slideshow playlists from multiple folders — without hand-editing thousands of lines or adding folders one at a time in XnView's built-in creator.

## Download (v1.0.1)

**[Download XnViewPlaylistBuilder.exe (v1.0.1)](https://github.com/LordDemonos/xnview-playlist-builder/releases/tag/v1.0.1)**

Single portable executable. No installer. No .NET runtime required on the target PC.

Copy `XnViewPlaylistBuilder.exe` anywhere and run it. Settings and logs are stored under `%AppData%\XnViewPlaylistBuilder\`.

> First launch may take a few seconds while the app extracts bundled libraries to a temp folder — normal for .NET single-file apps.

## Requirements

- Windows 10 or 11
- [XnView MP](https://www.xnview.com/en/xnviewmp/) for slideshow playback (optional for building playlists; required for **Play in XnView**)

## Quick start (GUI)

1. Run `XnViewPlaylistBuilder.exe`.
2. Click **Add folders…** (or **Add files…**) and select your image sources.
3. Toggle **Include subfolders** per folder as needed.
4. Click **Scan folders** to expand folders into individual image entries.
5. Adjust slideshow options on the right (timer, effects, overlay text, colors, etc.).
6. Click **Save .sld** or **Save As…**.
7. Open the playlist in XnView MP, or click **Play in XnView** (configure the XnView path in **Settings…** first).

## Features

### Playlist workflow

- **New** — start a blank playlist
- **Open .sld…** — load an existing XnView slideshow file for viewing or editing
- **Save .sld** / **Save As…** — write the current playlist
- **Add folders…** — batch-add multiple folder roots in one session
- **Add files…** — add individual image files
- **Remove** — remove selected folder sources from the scan list
- **Scan folders** — expand folder roots into per-file playlist entries
- **Remove entries** — remove playlist entries under selected folders
- **Sort A–Z** — sort entries or folder list alphabetically
- **Up** / **Down** — manually reorder selected entries
- **Filter** — live search/filter on entry paths
- **Rescan suggestions** — after removing entries, get suggested folder roots to rescan
- Per-folder **Include subfolders** toggle and **Check all** to toggle all at once
- **Collapsed subfolders** indicator when nested folders are merged in the folder list

### Slideshow options (XnView `.sld` v2 — all 22 keys)

- **Use timer** and interval (seconds)
- **Loop**, **Full screen**, window width/height, **Stretch**, **Random order**
- **Show info** overlay with customizable template (`{Folder name}`, `{Filename}`, etc.) and **Insert** token helper
- **Title bar**, **On top**, **Cursor auto-hide**
- **Background**, **text**, and **text background** colors (RGBA editor)
- **Text position** (3×3 grid: top-left through bottom-right)
- **Opacity**
- **Font** picker (Windows font dialog)
- **Effect duration** and all **56 transition effects** (individual toggles + use all)
- **Options presets** — save, load, and delete named option sets

### Path handling

- **Save path policy** (Settings): absolute local paths, absolute UNC (`\\server\share\…`), or relative-to-`.sld` (experimental)
- Optional XnView-style relative paths for non-ASCII entries (fallback; off by default)
- Configurable **image extensions** for folder scanning

### Maintenance tools

- **Check files** — health report for missing or empty media files
- **Fix names** — preview and rename non-ASCII or mojibake folder/file names to ASCII-safe paths
- **Undo renames** — revert renames using the rename log
- **Play in XnView** — launch `xnviewmp.exe -slide "playlist.sld"`
- **Workflow indicators** — status for scan freshness, file health, and rescan needs
- **Open log folder** — inspect application logs

### CLI (headless)

Run with `--` arguments (or invoke the `.exe` directly from a shell):

```powershell
XnViewPlaylistBuilder.exe --add "D:\photos\folder1" "D:\photos\folder2" --out "D:\playlists\show.sld" --recursive
```

| Option | Description |
|--------|-------------|
| `--add` | One or more folder paths |
| `--out` | Output `.sld` file path |
| `--recursive` | Include subfolders (default) |
| `--no-recursive` | Scan only the top level of each folder |
| `--help` | Show usage |

The CLI uses default options and path policy from your saved settings.

## Settings and logs

| Item | Location |
|------|----------|
| Settings (last folders, XnView path, defaults) | `%AppData%\XnViewPlaylistBuilder\settings.json` |
| Daily log files | `%AppData%\XnViewPlaylistBuilder\logs\app-YYYYMMDD.log` |

The GUI status bar shows the current log file path.

## Verify in XnView MP

1. Build a playlist with the GUI or CLI.
2. In XnView MP: **File → Open** → select the `.sld` file.
3. Or from a shell:

```powershell
& "C:\Program Files\XnViewMP\xnviewmp.exe" -slide "D:\playlists\show.sld"
```

## For developers

### Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build and test

```powershell
cd path\to\xnview-playlist-builder
dotnet build
dotnet test
```

### Run from source (GUI)

```powershell
dotnet run --project src\XnViewPlaylistBuilder\XnViewPlaylistBuilder.csproj
```

### Publish portable executable

```powershell
dotnet publish src\XnViewPlaylistBuilder\XnViewPlaylistBuilder.csproj -c Release
```

Output: `src\XnViewPlaylistBuilder\bin\Release\net8.0-windows\win-x64\publish\XnViewPlaylistBuilder.exe`

Release publish settings (single-file, self-contained, win-x64) are configured in the `.csproj` for `Release` builds.

## Project layout

| Path | Purpose |
|------|---------|
| `src/XnViewPlaylistBuilder.Core/` | Scanner, `.sld` reader/writer, settings, logging, path tools |
| `src/XnViewPlaylistBuilder/` | WPF GUI, CLI entry, themes |
| `tests/XnViewPlaylistBuilder.Tests/` | Unit tests |
| `tests/fixtures/` | Sanitized `.sld` test fixtures |
| `docs/` | Product and technical planning notes |

## License

This project is provided as-is for personal use. See repository releases for downloadable builds.
