# Capability Integration Map

## file-association → mru-list
**Trigger:** App launched with a file or folder path argument (from a desktop
double-click, "Open With", or CLI invocation).
**Payload:** Resolved absolute path, entry type (file/folder).
**Why:** Every real-world open should be reachable again from the MRU menu,
regardless of how it was opened.

## file-association → markdown-rendering / directory-browser
**Trigger:** App launched with a path argument.
**Payload:** Resolved path.
**Why:** A file argument goes straight to rendering; a folder argument goes
to the sidebar tree first. This dispatch is the core of the "double-click a
.md file" use case the whole app is built around.

## directory-browser → markdown-rendering
**Trigger:** User selects a file node in the sidebar tree.
**Payload:** File path.
**Why:** Browsing and rendering are separate concerns; the tree just tells
the main pane what to display next.

## directory-browser → mru-list
**Trigger:** A folder is opened as a root (not individual file selections
within an already-open folder).
**Payload:** Folder path.
**Why:** Keeps the MRU folder list meaningful — clicking around inside one
folder shouldn't push dozens of MRU entries.

## auto-reload → markdown-rendering
**Trigger:** FileSystemWatcher fires a debounced change event for the
currently open file.
**Payload:** none (triggers a re-read + re-render of the same path).
**Why:** Keeps the preview live when editing the source file elsewhere.

## auto-reload → directory-browser
**Trigger:** FileSystemWatcher fires a create/delete/rename event within the
currently open folder.
**Payload:** Changed path, change type.
**Why:** Keeps the sidebar tree accurate without requiring a manual refresh.

## markdown-rendering → search-in-file
**Trigger:** User invokes find (e.g. Ctrl+F) while a document is rendered.
**Payload:** The rendered visual tree/text content already in memory.
**Why:** Search operates on what's already on screen; no re-parsing.

## preferences → markdown-rendering
**Trigger:** User clicks OK in the Preferences dialog (Edit -> Preferences...).
**Payload:** Updated `AppSettings` (theme, font family, base font size,
line-height multiplier, document margin), persisted, then re-render of the
currently open file with a freshly built `MarkdownRenderOptions`.
**Why:** Changes should be visible immediately, not require a restart or a
manual re-open of the file.

## preferences → app theme
**Trigger:** Same Preferences OK action, or app startup.
**Payload:** `AppSettings.Theme` mapped to `Application.RequestedThemeVariant`
(System -> Default, Light -> Light, Dark -> Dark).
**Why:** Theme is an application-wide concern (Avalonia's FluentTheme
resources), not scoped to the document pane, so it's applied at the
`Application` level rather than per-window.

## markdown-rendering → export-to-pdf
**Trigger:** User invokes "Export to PDF" while a document is rendered.
**Payload:** The rendered visual tree already in memory.
**Why:** PDF output should match what the user sees, captured directly
rather than re-derived from the source Markdown.
