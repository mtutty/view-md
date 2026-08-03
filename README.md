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

## Packaging (.deb)

```sh
./packaging/build-deb.sh
sudo dpkg -i dist/view-md_0.1.0_amd64.deb
```

To make it the default handler for `.md` files after installing:

```sh
xdg-mime default view-md.desktop text/markdown
```

(`sudo dpkg -i` prints this same instruction after install.)

## Status

Charter complete. Core capabilities implemented and verified: rendering,
MRU, directory browser, file association/CLI dispatch, auto-reload, search,
PDF export, NativeAOT publish, and `.deb` packaging. See
`.charter/decisions.md` for two dependency issues found and fixed during
implementation (an alpha-only Markdown renderer package, and a SkiaSharp
version mismatch) — both are exactly the class of "won't install/won't run"
problem this project set out to avoid, so they're worth reading before
touching the rendering or PDF-export code.
