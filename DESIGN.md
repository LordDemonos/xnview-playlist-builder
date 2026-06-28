---
name: XnView Playlist Builder
description: Dark pro utility UI for a Windows desktop playlist builder
version: alpha
colors:
  background: "#0D0F12"
  surface: "#161A21"
  surface-raised: "#1E2430"
  surface-overlay: "#252C38"
  border: "#2E3644"
  border-subtle: "#232830"
  primary: "#4C8BF5"
  primary-hover: "#6BA0FF"
  primary-pressed: "#3A73D9"
  on-primary: "#FFFFFF"
  accent: "#34D399"
  on-accent: "#0D0F12"
  text-primary: "#E8EAED"
  text-secondary: "#9AA3AF"
  text-muted: "#6B7280"
  danger: "#F87171"
  focus-ring: "#4C8BF5"
typography:
  display:
    fontFamily: Segoe UI
    fontSize: 22px
    fontWeight: 600
  title:
    fontFamily: Segoe UI
    fontSize: 13px
    fontWeight: 600
  body:
    fontFamily: Segoe UI
    fontSize: 13px
    fontWeight: 400
  body-sm:
    fontFamily: Segoe UI
    fontSize: 12px
    fontWeight: 400
  label:
    fontFamily: Segoe UI
    fontSize: 12px
    fontWeight: 500
  mono:
    fontFamily: Cascadia Mono
    fontSize: 11px
    fontWeight: 400
rounded:
  sm: 4px
  md: 8px
  lg: 12px
spacing:
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
components:
  window:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text-primary}"
  panel:
    backgroundColor: "{colors.surface}"
    borderColor: "{colors.border-subtle}"
    rounded: "{rounded.lg}"
    padding: 16px
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.on-primary}"
    rounded: "{rounded.md}"
    padding: 10px 16px
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
    textColor: "{colors.on-primary}"
  button-secondary:
    backgroundColor: "{colors.surface-raised}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.md}"
    padding: 10px 16px
  button-ghost:
    backgroundColor: transparent
    textColor: "{colors.text-secondary}"
    rounded: "{rounded.md}"
    padding: 8px 12px
  input:
    backgroundColor: "{colors.surface-overlay}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.sm}"
    padding: 8px 10px
  status-bar:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-secondary}"
    typography: "{typography.body-sm}"
---

## Overview

Dark pro utility aesthetic: low-glare surfaces, crisp typography, and a single cool accent for primary actions. The app is a focused tool — clarity and scanability beat decoration. Layout emphasizes the workflow **Add folders → Scan → Save .sld**.

## Colors

- **Background (#0D0F12):** Main window canvas; deepest layer.
- **Surface (#161A21):** Panels, list areas, status bar.
- **Surface raised (#1E2430):** Inputs, secondary buttons, hover states.
- **Primary (#4C8BF5):** Save and other commit actions only.
- **Accent (#34D399):** Scan / progress success hints (sparingly).
- **Text primary (#E8EAED):** Labels, paths, headings.
- **Text secondary (#9AA3AF):** Hints, footer metadata, section subtitles.

## Typography

Segoe UI for all UI chrome. Cascadia Mono for log paths and long folder paths in lists. Section titles use **title** tokens; page headline uses **display**.

## Layout

- Outer padding: `{spacing.lg}` (24px).
- Panel gap: `{spacing.md}` (16px).
- Control row gap: `{spacing.sm}` (8px).
- Two-column main area: folder sources (~2fr) | options (~3fr).
- Sticky footer status strip with log path truncated.

## Elevation & Depth

Depth is communicated with surface steps and 1px borders, not heavy shadows. Optional subtle border `{colors.border-subtle}` on panels.

## Shapes

- Panels and cards: `{rounded.lg}` (12px).
- Buttons and inputs: `{rounded.md}` / `{rounded.sm}`.

## Components

| WPF resource | DESIGN.md component | Usage |
|--------------|---------------------|--------|
| `Button.Primary` | button-primary | Save .sld |
| `Button.Accent` | accent + on-accent | Scan folders |
| `Button.Secondary` | button-secondary | Add folders, Remove |
| `Button.Ghost` | button-ghost | Open log folder |
| `Panel.Card` | panel | Folder list, options sections |
| `TextBox` style | input | All text fields |
| `CheckBox` style | — | Slideshow toggles |

## Do's and Don'ts

**Do**

- Keep all functional control names (`x:Name`) unchanged for code-behind.
- Use primary blue only for the main save action.
- Truncate long paths with ellipsis; show full path in tooltip if needed later.
- Maintain high contrast for body text on surfaces (WCAG AA).

**Don't**

- Introduce gradients, glassmorphism, or bright saturated backgrounds.
- Change scanner, writer, or CLI behavior when restyling UI.
- Use more than one accent color in the same toolbar row.

## Agent notes

When editing UI, map tokens to [`src/XnViewPlaylistBuilder/Themes/AppTheme.xaml`](src/XnViewPlaylistBuilder/Themes/AppTheme.xaml). Validate with:

```powershell
npx "@google/design.md" lint DESIGN.md
```
