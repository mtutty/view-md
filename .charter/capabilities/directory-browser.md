# Directory Browser

## What it does in this app
When a folder is opened (via CLI arg, "Open Folder", or MRU), shows a
collapsible sidebar tree of every `.md` file in that folder and its
subfolders. Clicking a file renders it in the main pane without leaving the
folder context — this is the "browse a whole docs folder/wiki" use case.

## Configuration decisions
- **Recursive tree, not a flat single-directory list**: chosen because the
  motivating use case is docs folders/wikis with nested structure, where a
  flat list would hide most of the content.
- Sidebar is collapsible; its open/closed state and pixel width are
  remembered per session (stored alongside app settings, not per-folder).
- Opening a single file (not a folder) still allows revealing it in the
  sidebar, scoped to its parent directory, via a "reveal in folder" action —
  so single-file and folder mode aren't fully disconnected.

## Integration points
- Selecting a node feeds **markdown-rendering** for the main pane.
- Opening a folder root (not individual file clicks within it) pushes to
  **mru-list**.
- **auto-reload** watches the currently open folder (not just the currently
  open file) so new/deleted `.md` files update the tree live.

## Post-generation customizations
(empty at init)

## Known limitations and future considerations
No search/filter box on the tree in v1 — for very large doc trees this may
need a filter input later; not blocking for the initial use case.
