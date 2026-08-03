# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project follows [Semantic Versioning](https://semver.org/) once it
reaches 1.0; before that, minor version bumps mark feature additions.

## [0.2.0] - 2026-08-03

### Added
- Theme override (System / Light / Dark) via Edit -> Preferences, on top of
  automatic OS light/dark detection (Avalonia's built-in
  `RequestedThemeVariant=Default` behavior).
- Configurable typography: font family, base font size, line-height
  multiplier (default 1.1x), and document margin, also via Preferences.
  Changes apply immediately and persist.
- App versioning: `version.txt` at the repo root is the single source of
  truth for the version number; the build stamps it together with the
  current git short hash (and a `-dirty` suffix for an uncommitted tree)
  into the assembly automatically. Visible in the app via Help -> About
  view-md.
- Cross-platform packaging: `packaging/build-windows.sh` and
  `build-macos.sh` cross-publish win-x64 and osx-arm64 builds from Linux.
  Both were verified to produce genuine, correct platform binaries.
- Jenkins CI (`Jenkinsfile`): builds, headlessly smoke-tests, and packages
  all three platforms on a single docker-based agent. The pipeline was run
  end to end against the actual base image during development, which
  caught a real missing runtime dependency (`libfontconfig1`) before it
  could break CI.

### Fixed
- `.deb` and macOS `.app` bundle versions were hardcoded and disconnected
  from `version.txt`; both are now generated from it at build time so
  there's exactly one place to bump the version.
- Corrected stack info in `.charter/project-brief.yaml` that had drifted
  from the actual build (.NET 9 -> 10, Avalonia 11 -> 12.1.1,
  Markdown.Avalonia -> the project's own custom renderer).

### Known limitations
- Native AOT cannot cross-compile between operating systems, so only the
  Linux build gets NativeAOT's fast cold start; Windows/macOS builds are
  standard self-contained (JIT) publishes.
- The macOS build is unsigned and unnotarized — Gatekeeper will block it on
  any Mac other than the one that built it unless manually bypassed.
- The Windows file-association PowerShell script is written and checked
  against Microsoft's registry documentation, but not tested on an actual
  Windows machine.

## [0.1.0] - 2026-08-03

### Added
- Initial release. Native Avalonia-based Markdown rendering (custom
  Markdig-to-Avalonia renderer, no webview/Electron dependency), MRU list,
  directory-browser sidebar, CLI file/folder association, auto-reload via
  `FileSystemWatcher`, find-in-document search, and PDF export via
  SkiaSharp's `SKDocument`.
- NativeAOT self-contained Linux packaging (`.deb`, `packaging/build-deb.sh`).
- Hand-authored `.charter/` documentation (project brief, capability docs,
  integration map, decision log) — see `.charter/decisions.md` for why this
  project didn't use Charter's standard web-SaaS reference data.
