# Preferences (theme + appearance)

## What it does in this app
Lets the user override the OS-detected light/dark theme, and configure
document typography: font family, base font size, line-height multiplier,
and document margin. Exposed via Edit -> Preferences..., a small modal
dialog (`PreferencesWindow`).

## Configuration decisions
- **Theme detection is not custom code.** Avalonia's `RequestedThemeVariant
  = ThemeVariant.Default` already follows the OS light/dark setting on
  Linux/Windows/macOS via its own `PlatformSettings` abstraction, and
  updates live if the OS setting changes while the app is running. "System"
  in the Preferences dialog just means "leave it at Default" — see
  decisions.md.
- Theme override is one of three values: System (default) / Light / Dark,
  stored as `AppSettings.Theme` (already present in the original scaffold,
  reused as-is).
- Typography settings (`FontFamily`, `BaseFontSize`, `LineHeightMultiplier`,
  `DocumentMargin`) live on `AppSettings` and are mapped into a separate
  `MarkdownRenderOptions` record before being passed to `MarkdownRenderer`,
  so the renderer doesn't need to know about settings persistence.
- **Line-height is a multiplier of font size** (`LineHeight = BaseFontSize *
  LineHeightMultiplier`), not an absolute pixel value — Avalonia's
  `TextBlock.LineHeight` is an absolute value, so the multiplier is applied
  at render time, not stored as a resolved pixel number. Default multiplier
  is 1.1.
- The multiplier/size/margin only apply to flowing body text (paragraphs,
  list item content, blockquote content, table cell content — all of which
  route through `RenderParagraph`). Headings get the configured font family
  and a size proportional to `BaseFontSize`, but not the line-height
  multiplier — they're normally one line, and forcing extra leading on
  large heading text reads as a bug, not a feature.
- Code blocks/inline code always stay monospace regardless of the chosen
  body font, but do scale with `BaseFontSize` for consistency.
- Font family is a free-text/editable ComboBox (with a few common presets:
  Sans Serif, Serif, Monospace), not a system font picker — enumerating and
  validating installed fonts cross-platform is real added complexity this
  app doesn't need for v1. An invalid name just falls back to the platform
  default via Avalonia's own `FontFamily` resolution.
- Document margin is a single uniform value (all four sides), not a full
  CSS-style box model with independent margin/padding per side — the
  simplest thing that satisfies "let the user adjust spacing" without
  building a spacing UI nobody asked for the granularity of.

## Integration points
- Changing preferences re-renders the currently open document immediately
  (calls the same `OpenFile` path used for auto-reload) and re-applies the
  theme variant — no restart required.
- Persisted through the same `SettingsService`/`~/.config/view-md/settings.json`
  used for window size and sidebar state (see `.charter/data-model.md`,
  `AppSettings`).

## Post-generation customizations
(empty at init)

## Known limitations and future considerations
No per-element (e.g. per-heading-level) typography overrides — one base
size and one line-height multiplier apply globally. If that turns out to
be insufficient, it's a bigger UI (a typography scale editor) worth its own
design pass, not an incremental add here.
