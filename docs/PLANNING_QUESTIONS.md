# Planning Questions — XnView Playlist Builder

Open decisions for operator confirmation before or during implementation. Each item includes a **recommended default** based on sample analysis and XnView forum evidence.

---

## Open Questions

### Q1 — Architecture: shell extension vs GUI vs hybrid

| Option | Pros | Cons |
|--------|------|------|
| Shell extension only | Best Explorer UX | Poor options UI; COM/signing complexity; two code paths if writer lives in extension |
| GUI only | Fastest MVP; easy to test | No right-click flow; extra steps for operator |
| **Hybrid, GUI-first (recommended)** | One `.sld` writer; extension is thin launcher in Phase 2 | Two deliverables over time; signing still required for extension |

**Recommended default:** **Hybrid, GUI-first.** Ship Phase 0 as standalone GUI (or GUI + CLI). Phase 2 adds Explorer verb via signed sparse package + `IExplorerCommand`, spawning `XnViewPlaylistBuilder.exe --add "D:\folder1" "D:\folder2"`.

---

### Q2 — Path policy default

| Policy | When to use | Risk |
|--------|-------------|------|
| **Absolute — local or UNC (recommended default)** | Operator wants playlists that survive `.sld` file moves; SMB libraries | Long paths; not portable across machines for local drive letters |
| Relative to `.sld` file location | Co-locate `.sld` with media tree | Base resolution behavior needs spike |
| Relative to user-chosen anchor folder | Portable tree on removable drive | Operator must pick anchor correctly |
| Partial fragments (like local-style samples) | Match legacy XnView output | Breaks when `.sld` moves; base path **(UNVERIFIED — needs spike)** |

**Recommended default:** **Absolute** (drive letter for local, preserve UNC for network). Offer relative modes in Phase 1 after round-trip verification.

---

### Q3 — Expand folders to files at save vs store folder roots

| Option | Evidence | Recommendation |
|--------|----------|----------------|
| **Expand to per-file paths at save (recommended)** | Both repo samples list individual files; MP GUI expands folders on add ([t=38502](https://newsgroup.xnview.com/viewtopic.php?t=38502)) | Default for MVP |
| Wildcard folder lines (`"D:\media\2024\*.*"`) | Forum example ([t=44268](https://forum.xnview.com/viewtopic.php?t=44268)); not in operator samples | Phase 3 spike only |
| Classic directory-only entries | MP compatibility poor ([t=38502](https://newsgroup.xnview.com/viewtopic.php?t=38502)) | Out of scope |

**Recommended default:** **Expand folders to files at save time.** Re-save from tool when folder contents change unless wildcard spike succeeds.

---

### Q4 — Tech stack

| Stack | Fit | Tradeoffs |
|-------|-----|-----------|
| **C# / .NET 8 + WPF (recommended)** | Native folder dialogs, `Path` APIs, sparse-package samples exist | Larger runtime; WPF is mature |
| Rust + Tauri 2 | Small binary, modern UI | More effort for shell extension + Windows APIs |
| Python + PySide6 | Fastest prototype | Weaker shell-extension story; packaging friction |

**Recommended default:** **C# / .NET 8 + WPF** for GUI core. Shell extension can be C++ COM DLL or C# sparse package per Microsoft samples.

**Alternatives:** Rust + Tauri if binary size matters more than dev speed; Python + PySide6 for a throwaway Phase 0 spike only.

---

### Q5 — XnView MP version target

**Status:** Open — operator must confirm installed version.

**Recommended default:** Test against operator’s installed MP build; document version in `progress.md` after first successful verification. Minimum referenced in forum fixes: relative paths in `.sld` fixed in MP 0.91 ([Mantis #1409](https://www.xnview.com/mantisbt/view.php?id=1409)).

---

### Q6 — Image file filter

**Recommended default:** Configurable set of common raster extensions: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.bmp`, `.tif`, `.tiff`.

**Alternative:** Mirror XnView MP’s internal slideshow filter — **(UNVERIFIED — needs spike)** (forum notes non-image files can cause black screens, [t=37181](https://newsgroup.xnview.com/viewtopic.php?t=37181)).

---

### Q7 — Subfolder recursion

**Recommended default:** **On** by default for each added root; per-root toggle in UI.

---

### Q8 — File ordering in output `.sld`

**Recommended default:** Roots in add order; within each root, depth-first scan with case-insensitive alphabetical sort by filename. Random playback deferred to XnView via `RandomOrder = 1`.

---

### Q9 — File encoding

**Recommended default:** **(UNVERIFIED — needs spike)** — round-trip test: save from tool, open in XnView, re-save from XnView, compare encoding. Candidates: UTF-8 with BOM vs system ANSI (CP1252).

---

### Q10 — Code signing for shell extension

**Recommended default:** Self-signed certificate for local dev; optional Authenticode for wider distribution. **Do not ship Explorer integration unsigned** — Windows 11 sparse packages require signing for reliable registration.

**Alternative:** Skip shell extension until MVP `.sld` output is verified; GUI-only is acceptable for Phase 0–1.

---

### Q11 — Primary path scenario (operator preference)

**Status:** Open — affects default path policy in UI.

| Scenario | Suggested default policy |
|----------|-------------------------|
| Mostly local, movable libraries | Relative-to-anchor or relative-to-`.sld` (after spike) |
| Mostly UNC / SMB | Absolute UNC |
| Mixed | Absolute default; operator overrides per project |

---

### Q12 — Refresh when folder contents change

**Status:** Open — is live folder watching a v1 requirement?

**Recommended default:** **No** — operator re-runs tool and saves updated `.sld`. Wildcard folder entries remain Phase 3 spike.

---

## Answered (from research)

| Question | Answer | Source |
|----------|--------|--------|
| `.sld` format version | `# Slide Show Sequence v2` + `key = value` options + quoted paths | [`tests/fixtures/golden-test.sld`](../tests/fixtures/golden-test.sld), [`tests/fixtures/official-example.sld`](../tests/fixtures/official-example.sld) |
| CLI launch | `xnviewmp.exe -slide "path\to\file.sld"` (Classic: `xnview.exe -slide`) | [forum t=2870](https://forum.xnview.com/viewtopic.php?t=2870); MP command-line tab in Help → About |
| Include full options block? | **Yes** — missing settings may not fall back to `xnview.ini` | [forum t=2870](https://forum.xnview.com/viewtopic.php?t=2870) |
| Relative paths in MP | Supported; bug fixed in 0.91 | [Mantis #1409](https://www.xnview.com/mantisbt/view.php?id=1409) |
| Network share + “relative” in XnView GUI | May still write absolute UNC paths | [forum t=30576](https://newsgroup.xnview.com/viewtopic.php?t=30576) |
| MP GUI folder add behavior | Expands directory inventory into file list | [forum t=38502](https://newsgroup.xnview.com/viewtopic.php?t=38502) |
| Wildcard path lines | User-edited example `"D:\...\2018\*.*"` reported | [forum t=44268](https://forum.xnview.com/viewtopic.php?t=44268) — **not verified against operator MP version** |

---

## Resolution Log

| Date | Question | Decision | Decided by |
|------|----------|----------|------------|
| *(pending)* | Q5 — MP version | — | Operator |
| *(pending)* | Q11 — primary path scenario | — | Operator |
| *(pending)* | Q12 — live refresh vs re-save | — | Operator |
