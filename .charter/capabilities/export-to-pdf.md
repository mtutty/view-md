# Export to PDF

## What it does in this app
Exports the currently rendered document to a PDF file, preserving the same
layout the user sees on screen.

## Configuration decisions
- Implemented via **SkiaSharp's `SKDocument` PDF canvas**, not a separate
  PDF library (e.g. QuestPDF, wkhtmltopdf, a headless-browser print). Avalonia
  already depends on SkiaSharp for its own rendering, so this capability adds
  zero new native dependencies — consistent with the project's core
  no-webview, minimal-dependency stance.
- Pagination strategy (single long page vs. paginated to a standard page
  size) is an implementation detail left open for generation time; default
  to paginated Letter/A4 sized on printer-locale, single long page as a
  fallback if pagination proves complex against Avalonia's layout system.

## Integration points
- Depends on **markdown-rendering** having already produced the visual tree
  for the currently open document; captures that tree rather than
  re-parsing the source.

## Post-generation customizations
(empty at init)

## Known limitations and future considerations
No export of arbitrary/unopened files (e.g. batch-export a whole folder) in
v1 — export acts on whatever is currently on screen.

Confirmed via headless smoke testing (`tools/SmokeTest`): the exported PDF
embeds a single rasterized bitmap of the document rather than vector text,
because the capture path is `RenderTargetBitmap` → PNG → `SKDocument` PDF
canvas. This means exported PDFs are not searchable/selectable text and
scale in file size with page complexity/resolution, not text content. If
that becomes a real complaint, the fix is a genuinely different
implementation (a Skia-vector-drawing pass instead of a bitmap capture), not
a tweak to the current one — worth its own decision if pursued.
