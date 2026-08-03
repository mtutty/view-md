# Auto-Reload

## What it does in this app
Watches the currently open file (and, in folder mode, the currently open
folder) for changes and re-renders automatically — useful when editing the
Markdown in another tool while previewing in view-md.

## Configuration decisions
- Built on .NET's `FileSystemWatcher` — no polling, no extra dependency.
  Cheap enough relative to the app's complexity that it was included in v1
  by default rather than deferred.
- Debounced (short delay after last change event) to avoid re-render
  thrashing from editors that write files in multiple small operations.

## Integration points
- Triggers **markdown-rendering** for the currently displayed file.
- In folder mode, file create/delete/rename events also update the
  **directory-browser** tree live.

## Post-generation customizations
(empty at init)

## Known limitations and future considerations
No conflict handling needed — this is read-only, so there's no merge/save
concern, only "re-render on change."
