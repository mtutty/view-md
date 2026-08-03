# MRU List

## What it does in this app
Tracks the most recently opened files and folders (separately) and surfaces
them in a menu for one-click reopening. Persisted locally so it survives
app restarts.

## Configuration decisions
- Max 15 entries, files and folders tracked as two separate lists in one
  JSON file (`~/.config/view-md/mru.json`) rather than one interleaved list
  — folder-mode is a first-class entry point (see directory-browser), so it
  deserves its own visibility rather than getting buried by individual file
  opens.
- Plain local JSON, no database — this is a single-user desktop app with no
  need for anything heavier.

## Integration points
- **file-association**: every CLI-invoked open (file or directory) pushes
  an entry to the MRU.
- **directory-browser**: selecting a file inside the sidebar tree does
  *not* push a new MRU entry — only the folder root does, to avoid
  flooding the MRU with every file clicked while browsing one folder.

## Post-generation customizations
(empty at init)

## Known limitations and future considerations
No pinning/favorites in v1 — pure recency-based list. Revisit if the list
turns out to churn too fast for folders you return to often.
