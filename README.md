# view-md

A lightweight, fast-starting native Markdown viewer for Linux desktops
(primary target: Ubuntu 24.04), built to be the default file association for
`.md` files.

## Start here

This project uses [Charter](https://github.com/mtutty/charter) as its
documentation framework. **The `.charter/` directory is the source of truth**
for what this app is, why it's built the way it is, and what decisions have
already been made. Read it before making changes:

- `.charter/project-brief.yaml` — app identity, stack, capability config
- `.charter/capabilities/*.md` — one file per capability (what it does here,
  why it's configured this way, how it connects to the rest of the app)
- `.charter/integrations.md` — how capabilities hand off to each other
- `.charter/data-model.md` — the (small, file-based) data model
- `.charter/decisions.md` — append-only architectural decision log

Note: this project's charter was hand-authored rather than generated from
Charter's standard web-SaaS reference data, because that data (Node/Python/
PHP stacks, auth/billing/multi-tenancy capabilities) doesn't apply to a
local, single-user desktop app. See the first entry in `decisions.md` for
why, and what that means for future LLM-assisted work here.

## Building and running

Requires the .NET 10 SDK (`sudo apt install dotnet-sdk-10.0` on Ubuntu
24.04+, straight from Ubuntu's own `noble-updates` repo).

```sh
dotnet build                              # compile everything
dotnet run --project src/ViewMd           # run from source
dotnet run --project src/ViewMd -- ~/notes/README.md   # open a specific file
dotnet run --project src/ViewMd -- ~/notes              # open a folder
```

Keyboard shortcuts: `Ctrl+O` open file, `Ctrl+Shift+O` open folder, `Ctrl+B`
toggle sidebar, `Ctrl+F` find in document, `F3`/`Shift+F3` next/previous
match.

## Headless smoke testing

`tools/SmokeTest` renders a given Markdown file (or folder) through the real
`App`/`MainWindow` using Avalonia's headless Skia backend and saves a PNG —
useful for catching rendering regressions without a display attached:

```sh
dotnet run --project tools/SmokeTest -- path/to/file.md out.png
dotnet run --project tools/SmokeTest -- path/to/file.md out.png out.pdf   # also exercise PDF export
```

## Version

The app version lives in a single file, `version.txt`, at the repo root —
edit it to bump the version. The build stamps it together with the current
git commit's short hash (plus a `-dirty` suffix for an uncommitted working
tree) into the assembly automatically; no separate bump tooling. See it in
the running app via Help -> About view-md.

## Packaging

Three platforms, one Linux build host. Only the Linux build is NativeAOT —
Native AOT cannot cross-compile between operating systems (see
`.charter/decisions.md`), so the Windows/macOS builds are standard
self-contained (JIT) publishes cross-compiled from Linux.

```sh
./packaging/build-deb.sh      # Linux, NativeAOT, produces dist/view-md_<version>_amd64.deb
./packaging/build-windows.sh  # Windows, self-contained JIT, produces dist/view-md_<version>_win-x64.zip
./packaging/build-macos.sh    # macOS, self-contained JIT + .app bundle, unsigned — see decisions.md
```

Linux install + set as default handler:

```sh
sudo dpkg -i dist/view-md_<version>_amd64.deb
xdg-mime default view-md.desktop text/markdown   # printed by dpkg -i too
```

Windows: unzip and run `view-md.exe`; run
`packaging/windows/register-file-association.ps1` from the unzipped folder
to register it as an available `.md` handler (per-user, no admin rights;
**unverified — written and checked against Microsoft's registry docs, but
not tested on an actual Windows machine**).

macOS: unzip and run `view-md.app`. Unsigned — see the comment at the top
of `build-macos.sh` for what that means and why (no Apple Developer account
in scope for this project).

## Status

Charter complete. Core capabilities implemented and verified: rendering,
MRU, directory browser, file association/CLI dispatch, auto-reload, search,
PDF export, theming (OS-follow + override), configurable typography,
versioning, and packaging for Linux/Windows/macOS. See
`.charter/decisions.md` for the dependency and platform issues found and
fixed along the way (an alpha-only Markdown renderer package, a SkiaSharp
version mismatch, and NativeAOT's cross-OS limitation) — worth reading
before touching rendering, PDF export, or packaging.
