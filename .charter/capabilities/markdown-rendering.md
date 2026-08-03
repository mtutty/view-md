# Markdown Rendering

## What it does in this app
Parses a `.md` file with Markdig (CommonMark + common GFM extensions: tables,
task lists, fenced code blocks) and renders it directly into Avalonia's
native visual tree — no webview, no HTML intermediate step. This is the
capability the whole app exists to deliver quickly.

## Configuration decisions
- **Native rendering over webview** (Markdown.Avalonia on top of Markdig,
  not Photino.NET + marked.js): the user has repeatedly hit Ubuntu installs
  where WebKitGTK version mismatches broke Electron/webview-based Markdown
  viewers entirely, or made them slow to cold-start. Native Skia rendering
  removes that dependency class completely at the cost of not supporting
  Mermaid diagrams or LaTeX math out of the box.
- Markdig chosen as the parser because it's the de facto standard CommonMark
  implementation for .NET and actively maintained.
- **Renderer is first-party/custom, not Markdown.Avalonia.** At generation
  time, Markdown.Avalonia's Avalonia-12 support was alpha-only while this
  project targets Avalonia 12.1.1 stable — see decisions.md. The custom
  renderer walks Markdig's AST and emits Avalonia controls directly (tables
  → Grid, code fences → a monospace TextBlock/Border, etc). Lives at
  `src/ViewMd/Rendering/MarkdownRenderer.cs`.

## Integration points
- Feeds **search-in-file** (operates on the rendered document).
- Feeds **export-to-pdf** (the rendered visual tree is what gets captured to
  a PDF canvas).
- Triggered by **directory-browser** selection and by **auto-reload** on
  file change.

## Post-generation customizations
(empty at init)

## Known limitations and future considerations
No Mermaid/LaTeX support in v1 given the native-rendering choice. If that
becomes a hard requirement later, it's a stack-level decision (would mean
reconsidering the webview approach), not a small addition — log it as a new
decision in decisions.md rather than bolting on a partial fix.
