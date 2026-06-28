# MVP Plan — XnView Playlist Builder

Phased delivery for a solo developer. Each phase ends with **objective verification** before the next phase starts.

## Phase Summary

| Phase | Name | Delivers | Depends on |
|-------|------|----------|------------|
| 0 | Core writer (MVP) | Valid `.sld` from multiple folders | — |
| 1 | Operator UX | Remembered paths, path policies, presets, large-scan progress | Phase 0 verified |
| 2 | Product polish | Wildcards, reorder/sort, file add, search filter, portable `.exe` | Phase 1 |

---

## Phase 0 — Core Writer (MVP)

**Goal:** GUI or CLI produces a valid `.sld` v2 file from ≥2 folders with recursive scan.

### Build checklist

- [ ] Project scaffold: .NET 8 WPF app (CLI flags optional for headless test)
- [ ] Multi-folder picker (or repeated add) + per-root “include subfolders” toggle
- [ ] Folder scanner with configurable image extensions
- [ ] Options panel: all 22 keys from [`TECH_SPEC.md`](TECH_SPEC.md) with defaults from [`tests/fixtures/golden-test.sld`](../tests/fixtures/golden-test.sld)
- [ ] `SldWriterV2` implementing writer contract (header, options, quoted paths)
- [ ] Path policy: **absolute only** in Phase 0
- [ ] Save dialog; status bar: entry count, scan duration
- [ ] De-dupe by normalized full path

### Verification (objective — must all pass)

| # | Step | Expected result |
|---|------|-----------------|
| V0.1 | Add 2+ local folders containing known `.jpg`/`.png` files (with subfolders) | UI lists expected file count (±0 vs manual count on small set) |
| V0.2 | Set Timer=15, Loop=1, RandomOrder=1; save as `test-output.sld` | File exists; line 1 is `# Slide Show Sequence v2` |
| V0.3 | Diff structure against [`tests/fixtures/golden-test.sld`](../tests/fixtures/golden-test.sld): header + 22 option keys present | Same keys in same order; values match UI selections |
| V0.4 | Open `test-output.sld` in XnView MP (File → Open) | Slideshow starts; images advance |
| V0.5 | Run `xnviewmp.exe -slide "test-output.sld"` | Same behavior as V0.4 |
| V0.6 | Move `test-output.sld` to a different directory; run again with absolute paths | Slideshow still finds all images |
| V0.7 | Repeat V0.4–V0.6 against a UNC folder (if available) | UNC paths preserved; slideshow runs |

**Phase 0 exit criterion:** V0.1–V0.6 pass on operator machine. V0.7 optional if no SMB access during dev.

### Known spikes deferred from Phase 0

- Relative path policies
- File encoding round-trip
- Wildcard folder lines

---

## Phase 1 — Operator UX

**Goal:** Fix the top three operator pains beyond raw `.sld` generation: batch workflow polish, remembered picker, path policy control.

### Build checklist

- [ ] Persist `lastBrowseFolder`, `lastSaveFolder` in `%AppData%\XnViewPlaylistBuilder\settings.json`
- [ ] Restore last browse folder on folder picker open
- [ ] Path policy selector: Absolute / Relative-to-`.sld` / Relative-to-anchor
- [ ] Anchor folder picker (when policy = RelativeToAnchor)
- [ ] Options presets: save/load named templates (JSON)
- [ ] Progress UI for scans >1000 files (cancel support)
- [ ] Summary dialog before save: file count, policy, output path

### Verification

| # | Step | Expected result |
|---|------|-----------------|
| V1.1 | Close and reopen app; open folder picker | Starts at last browse folder |
| V1.2 | Save playlist; reopen save dialog | Starts at last save folder |
| V1.3 | Relative-to-`.sld` on small co-located tree | XnView MP plays after moving **both** `.sld` and media together — **(UNVERIFIED until spike passes)** |
| V1.4 | Scan folder with 5k+ files | Progress bar updates; completes without UI freeze |
| V1.5 | Load options preset | All 22 keys restore correctly in UI and output file |

**Phase 1 exit criterion:** V1.1, V1.2, V1.4, V1.5 pass; V1.3 passes or relative modes remain hidden behind “experimental” flag.

---

## Phase 2 — Product Polish & Portable Build

**Goal:** Close remaining operator workflow gaps and ship a single portable `.exe` (no installer, no shell extension).

### Build checklist

- [x] Delete saved options presets
- [x] Reorder entries (Up/Down) and sort A–Z
- [x] Remove all playlist entries under a selected folder
- [x] Add individual image files (not only folder scan)
- [x] Search/filter large entry lists
- [x] Wildcard folder lines (`path\*.*`) per folder source
- [x] CLI `LastBrowseFolder` uses parent-directory helper (same as GUI)
- [ ] `dotnet publish` single-file portable `.exe` documented for operator

### Verification

| # | Step | Expected result |
|---|------|-----------------|
| V2.1 | Add folder with Wildcard (*.*) checked; save without scan | `.sld` contains one `"folder\*.*"` line |
| V2.2 | Filter 11k+ entry list | List narrows live; count shows `N of total` |
| V2.3 | Remove entries under folder | Only entries under selected folder removed |
| V2.4 | Publish portable exe | Runs on clean machine without install |

**Phase 2 exit criterion:** V2.1–V2.3 pass; portable publish command documented.

---

## Deferred / Out of Scope

| Feature | Notes |
|---------|-------|
| Explorer context menu / shell extension | Not planned — GUI + CLI only |
| Installer / auto-update | Portable `.exe` preferred |

---

## Risk Register

| Risk | Mitigation | Phase |
|------|------------|-------|
| Relative path base unknown | Spike before enabling Relative policies | 1 |
| XnView version mismatch | Operator confirms version; test matrix note in progress.md | 0 |
| Shell extension signing friction | Not pursuing Explorer integration | — |
| 22k+ entry write performance | Stream write; progress UI | 1 |
| Non-image files in folders | Extension filter; log skipped | 0 |

---

## Definition of Done (v1)

- [ ] Phase 0 verification complete
- [ ] Phase 1 verification complete (relative modes gated if spike fails)
- [ ] Operator doc: quick-start (add folders → options → save → open in XnView)
- [ ] Sample output `.sld` committed or documented in progress log (redacted paths)
- [ ] Portable `.exe` publish command in README or operator doc

---

## Related Documents

- [`PRODUCT.md`](PRODUCT.md) — scope and non-goals
- [`TECH_SPEC.md`](TECH_SPEC.md) — writer contract
- [`PLANNING_QUESTIONS.md`](PLANNING_QUESTIONS.md) — open decisions
- [`.planning/xnview-playlist-builder/task_plan.md`](../.planning/xnview-playlist-builder/task_plan.md) — live phase tracker
