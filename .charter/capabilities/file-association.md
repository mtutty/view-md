# File Association

## What it does in this app
Lets Ubuntu's file manager (and any other launcher) open `.md` files or
folders directly in view-md via double-click, "Open With", or `xdg-open`.
This is the primary reason the project exists — the app must be fast and
reliable enough to trust as the default handler.

## Configuration decisions
- Implemented via a standard `.desktop` file (`MimeType=text/markdown;` plus
  `text/x-markdown;` for older tooling) and `xdg-mime default` registration,
  wired up by the `.deb` package's postinst script — not snap, not flatpak,
  since both introduce sandboxing/dependency behavior that has previously
  caused install/runtime failures for the user's other Markdown viewer
  attempts.
- CLI contract: `view-md <path>`, where `<path>` may be a file or a
  directory — the app dispatches into single-file mode or
  directory-browser mode based on which it receives.
- Startup latency is treated as a hard requirement here specifically
  because this is invoked on every double-click — NativeAOT + self-contained
  publish exists primarily to serve this capability.

## Integration points
- Every invocation pushes to **mru-list**.
- Directory arguments hand off to **directory-browser**; file arguments hand
  off directly to **markdown-rendering**.

## Post-generation customizations
(empty at init)

## Known limitations and future considerations
Windows/macOS file association (registry / Info.plist) is deferred until
those platforms are actually targeted — see `future-targets` in
project-brief.yaml.
