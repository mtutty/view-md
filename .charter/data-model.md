# Application Data Model

There is no database in this app — all state is local config/cache files
under `~/.config/view-md/`. The "entities" below are JSON-serialized
records, not database tables.

## Entities

### RecentEntry
**Owned by:** mru-list
**Purpose:** One remembered file or folder the user has opened, for the
recent-items menu.
**Key relationships:** Referenced by file-association (creates/updates
entries on open) and directory-browser (creates entries for folder roots).
**Fields (informal):** absolute path, entry type (file | folder), last
opened timestamp.

### AppSettings
**Owned by:** app shell (not a single capability)
**Purpose:** Persisted UI state — sidebar open/closed, sidebar width, theme
(light/dark), font size.
**Key relationships:** Read/written by directory-browser (sidebar state)
and the app shell's theme toggle. Not tied to any specific opened
file/folder.

### OpenDocument (in-memory only, not persisted)
**Owned by:** markdown-rendering
**Purpose:** The currently parsed Markdig AST + rendered Avalonia visual
tree for whatever file is on screen.
**Key relationships:** Consumed by search-in-file (text/highlight matches)
and export-to-pdf (visual tree capture). Replaced wholesale on navigation
or auto-reload — never partially updated.
