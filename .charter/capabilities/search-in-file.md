# Search in File

## What it does in this app
Browser-style find-in-page over the currently rendered document: type a
query, matches are highlighted in the main pane, next/previous jumps
between them.

## Configuration decisions
- Scoped to the **currently rendered document only** — not a full-text
  index across an entire opened folder. Added explicitly at charter-init
  alongside export-to-pdf; folder-wide search was considered and
  deliberately left out of v1 to keep the app's footprint small (no index
  to build or maintain, no extra startup cost).
- Operates on the already-parsed/rendered content in memory — no
  re-parsing needed per keystroke.

## Integration points
- Depends on **markdown-rendering** having already produced the visual tree
  for the currently open document.

## Post-generation customizations
(empty at init)

## Known limitations and future considerations
Folder-wide search (across all files in directory-browser) is a plausible
v2 feature if the recursive-folder use case grows, but it changes the
performance profile (needs an index or on-demand grep) and should be
scoped as its own decision rather than an incremental add to this
capability.
