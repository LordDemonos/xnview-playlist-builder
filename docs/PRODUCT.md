# XnView Playlist Builder — Product Brief

Working name: **XnView Playlist Builder** (rename if a clearer name emerges during build).

## Problem

XnView MP includes a slideshow creator, but the operator workflow is painful for large, multi-folder libraries:

1. **Single-folder picking** — folders must be added one at a time; there is no batch “add many folders (and subfolders) in one pass.”
2. **No remembered picker location** — the folder dialog does not persist the last directory between sessions.
3. **Fragile path semantics** — saved `.sld` files often use paths that break when the playlist file is moved. Local-style playlists can store partial path fragments (folder name + filename, no drive root). UNC/SMB-hosted playlists work when every entry is a fully qualified `\\server\share\...` path.
4. **Limited control surface** — the operator wants the same slideshow options XnView exposes, but with predictable path handling and a faster build workflow.

Ideal operator flow: add many folders (or files) in the GUI → configure options → save a valid `.sld`. Optional CLI for headless batch builds. A portable Windows `.exe` is the delivery target (no installer).

## Persona

**Solo Windows operator** (Windows 10/11):

- Curates large image libraries on local drives and SMB shares.
- Already uses XnView MP for viewing and slideshow playback (`xnviewmp.exe -slide "file.sld"`).
- Comfortable with Explorer; does not want to hand-edit 22k-line playlist files.
- Prefers explicit acceptance criteria, verification steps, minimal scope creep, and operator-facing docs.

Assumption: [`Prompto/context/operator-profile.md`](../Prompto/context/operator-profile.md) is not present in this repo; the above cues come from the planning brief.

## Goals (v1)

| Goal | Success signal |
|------|----------------|
| Batch folder input | Add ≥2 folder roots (with optional recursion) in one session |
| Remember paths | Last browse folder and last save folder persist in `%AppData%` |
| Valid `.sld` output | File opens in XnView MP and slideshow runs with configured options |
| Path policy choice | Operator selects absolute, relative-to-`.sld`, or relative-to-anchor before save |
| Options parity | All 22 option keys observed in sample files are configurable |

## v1 Scope (in)

- **Multi-folder add** with per-root “include subfolders” toggle.
- **Recursive folder scan** → expand to per-file quoted paths at save time (matches both repo samples).
- **Slideshow options panel** for `.sld` v2 keys (see [`TECH_SPEC.md`](TECH_SPEC.md) schema table).
- **Path policies:** absolute local, absolute UNC, relative-to-user-chosen anchor, relative-to-`.sld` file location (relative modes gated on verification spikes).
- **Settings persistence:** `%AppData%\XnViewPlaylistBuilder\settings.json` — last folders, default options template, path policy.
- **Optional preview launch:** if XnView MP path is configured, run `-slide "path.sld"`.
- **Portable delivery:** single-file `.exe` via `dotnet publish` (no installer).

## Explicit Non-Goals (v1)

| Non-goal | Rationale |
|----------|-----------|
| macOS / Linux | Windows-first operator environment |
| Cloud sync | Out of scope; operator manages files locally/SMB |
| Image editing or transcoding | XnView remains the viewer |
| Replace XnView MP | Tool only writes playlists |
| Classic XnView `.sld` (directory-only, no file list) | MP GUI expands folders; compatibility with Classic format is poor ([forum t=38502](https://newsgroup.xnview.com/viewtopic.php?t=38502)) |
| Bulk re-path editor for existing 22k-entry files | Separate tool; not MVP |
| Fix Windows `.sld` file association | XnView/Windows registration issue ([forum t=44067](https://forum.xnview.com/viewtopic.php?t=44067)) |
| Explorer / shell context menu | GUI + CLI workflow is sufficient |
| Installer / auto-update | Portable `.exe` preferred for solo operator |

## Compatibility Target

Generated `.sld` files must open and run in **XnView MP** on the operator’s installed version. Exact version is an open question — see [`PLANNING_QUESTIONS.md`](PLANNING_QUESTIONS.md).

## Reference Samples

| File | Role | Path style |
|------|------|------------|
| [`tests/fixtures/golden-test.sld`](../tests/fixtures/golden-test.sld) | Local-style sample | Partial fragments: `"FolderName\Subfolder\file.jpg"` |
| [`tests/fixtures/official-example.sld`](../tests/fixtures/official-example.sld) | Relative-path sample | Relative: `"examples\Album\file.jpg"` |

Both use header `# Slide Show Sequence v2` and identical option key set (values differ slightly for `ShowInfo`, `Info`, `Font`).

## Related Documents

- [`PLANNING_QUESTIONS.md`](PLANNING_QUESTIONS.md) — open decisions and recommended defaults
- [`TECH_SPEC.md`](TECH_SPEC.md) — architecture and `.sld` writer contract
- [`MVP_PLAN.md`](MVP_PLAN.md) — phased delivery and verification
