# Architectural Decisions

This is an append-only log. Never edit existing entries.

---

## Charter framework's SaaS reference data does not apply to this project — 2026-08-03
**Decision:** Hand-author the charter (project-brief, capability docs,
integrations, data-model) following Charter's file structure, without using
`stacks.md` / `libraries.md` / the capability spec fetches from the Charter
framework repo, and without running charter-init's "Generating the Initial
Scaffold" step.
**Context:** charter-init's mandatory reference data (stacks.md,
libraries.md) is entirely web-SaaS-oriented — Node/Python/PHP frameworks,
and capabilities like auth, billing, multi-tenancy, mailer. view-md is a
local, single-user, offline native desktop app with no backend, no
accounts, and none of those concerns.
**Rationale:** Charter's *structure* (a durable, LLM-readable source of
truth describing the app and the reasoning behind its decisions) is still
valuable even when its specific web-SaaS content isn't. Forcing this project
through the SaaS capability model would have produced a nonsensical charter
(e.g. picking Next.js as a "stack" for a desktop app).
**Alternatives considered:** Abandoning Charter entirely for this project;
picking the closest web stack anyway and ignoring the mismatch. Both
rejected — the first throws away a useful documentation structure, the
second produces misleading documentation.
**Consequences:** Future LLM-assisted work on this project (e.g.
charter-iterate) should treat this charter as authoritative for view-md's
domain, but should not expect Charter framework capability specs (e.g.
`capabilities/auth.md`) to be relevant here — there is no equivalent
upstream spec for desktop capabilities like directory-browser or
file-association; the hand-written docs in `.charter/capabilities/` are the
only source for those.

---

## Native Avalonia rendering over a webview — 2026-08-03
**Decision:** Render Markdown natively via Avalonia's Skia-based visual
tree (Markdig for parsing, Markdown.Avalonia or an equivalent custom
renderer for display), rather than embedding a webview (e.g. Photino.NET +
marked.js).
**Context:** The user has repeatedly had other Linux Markdown viewers
either fail to install via apt/snap, fail to run outright, or run too
slowly. Failure-to-run and slow-startup symptoms are both consistent with
Electron/webview-based tools hitting WebKitGTK version mismatches or paying
Chromium/webview startup cost.
**Rationale:** Removing the OS webview dependency removes an entire class
of "won't run on this machine" failure and gets the fastest possible cold
start, which is the app's primary success metric (it's meant to be the
default handler for double-clicking `.md` files).
**Alternatives considered:** Photino.NET + WebKitGTK (rejected: reintroduces
the exact dependency class causing prior failures); Electron (rejected:
heavy, slow cold start, explicitly what the user is trying to get away
from).
**Consequences:** No Mermaid diagrams or LaTeX math rendering without
significant future work; markdown rendering fidelity depends on the
maturity of the .NET Markdown-to-Avalonia rendering path rather than a
browser engine.

---

## NativeAOT + self-contained .deb over snap/flatpak — 2026-08-03
**Decision:** Publish as a NativeAOT, self-contained `linux-x64` binary,
packaged as a plain `.deb` with a postinst script handling `.desktop`/mime
registration — not distributed via snap or flatpak.
**Context:** Same root complaint as above — prior tools failed to install
cleanly via apt or snap. Snap's confinement model and flatpak's sandboxing
have both been known to interfere with file-association and filesystem
access assumptions for simple desktop utilities.
**Rationale:** A self-contained native binary in a plain `.deb` has the
fewest moving parts: no runtime to install separately (NativeAOT), no
sandboxing to reason about, no store/repo dependency.
**Alternatives considered:** Snap (rejected: sandboxing risk, prior bad
experience); Flatpak (same); traditional `.deb` depending on a
shared/framework-dependent .NET install (rejected: adds an install-order
dependency — "install .NET runtime first" — that a self-contained binary
avoids).
**Consequences:** Larger binary size (self-contained includes the trimmed
runtime) in exchange for zero external runtime dependencies. Cross-platform
builds (win-x64, osx-arm64) later reuse the same `dotnet publish` pipeline
with a different RID.

---

## Custom Markdig-to-Avalonia renderer instead of Markdown.Avalonia — 2026-08-03
**Decision:** Write a small first-party renderer that walks the Markdig AST
and emits Avalonia controls directly, rather than taking a dependency on the
third-party `Markdown.Avalonia` package.
**Context:** `project-brief.yaml` named Markdown.Avalonia as the renderer,
with an explicit note to verify it's still maintained at generation time and
fall back to a custom renderer if stale. At generation time (checked via
NuGet), Markdown.Avalonia's port to Avalonia 12 (the version the scaffolded
project targets, 12.1.1 stable) exists only as alpha prereleases
(`12.0.0-a1` through `a3`, last published months before this check). The
stable `11.0.3` line only targets Avalonia 11.
**Rationale:** Depending on an alpha-quality package for the app's single
most important capability (rendering) contradicts the project's core
reliability goal. Markdig itself (the parser) is unaffected — it's
stack-agnostic and stable — so only the rendering layer needed to change.
**Alternatives considered:** Pin Avalonia down to the 11.x line to match
Markdown.Avalonia's stable release (rejected: gives up a newer stable UI
framework version to work around a rendering library's lag, and still
leaves the project dependent on a third-party package with a demonstrated
history of lagging Avalonia's major versions); take the alpha dependency
anyway (rejected: alpha packages are exactly the kind of fragile dependency
this project is trying to avoid).
**Consequences:** More code to own and maintain directly (headings, lists,
tables, code fences, inline formatting, images, task lists), but no
third-party rendering dependency to break on a future Avalonia upgrade.
Revisit if Markdown.Avalonia's Avalonia-12 port reaches a stable release and
the custom renderer's maintenance cost outweighs the switch.

---

## SkiaSharp version pinned to match Avalonia.Skia exactly — 2026-08-03
**Decision:** `src/ViewMd/ViewMd.csproj` pins `PackageReference Include="SkiaSharp"
Version="3.119.4"` explicitly, rather than letting it float to latest.
**Context:** export-to-pdf needs a direct `SkiaSharp` package reference (for
`SKDocument`/`SKBitmap`, not exposed by Avalonia's own API surface). Adding
it without a version constraint resolved to the latest release (`4.151.0`),
whose managed assembly expects a native `libSkiaSharp` in the 151.0-152.0
ABI range. `Avalonia.Skia` 12.1.1 pins `SkiaSharp 3.119.4` internally and
brings the matching native asset for that version (ABI 119.0). With both
package versions in the dependency graph, the mismatched native asset from
Avalonia.Skia's `3.119.4` was the one that actually got loaded at runtime
against the `4.151.0` managed assembly, and headless smoke testing (see
below) caught the resulting crash immediately on startup:
`TypeInitializationException` — "native libSkiaSharp (119.0) is
incompatible... supported [151.0, 152.0)".
**Rationale:** The app must depend on exactly the same `SkiaSharp` version
Avalonia.Skia uses internally — there's no independent "latest" to float to
for a package that's this tightly coupled to Avalonia's own rendering
internals.
**Alternatives considered:** None — this isn't a preference, it's a
hard compatibility requirement discovered by running the app.
**Consequences:** Upgrading the `Avalonia`/`Avalonia.Skia` package versions
in the future must be paired with checking (and likely bumping) this
`SkiaSharp` pin to whatever version that Avalonia release depends on —
check `Avalonia.Skia`'s `.nuspec` dependency list, don't just take NuGet's
default resolution.

---

## OS theme detection uses Avalonia's built-in PlatformSettings, not custom code — 2026-08-03
**Decision:** Implement "follow the OS light/dark setting" by leaving
`Application.RequestedThemeVariant` at `ThemeVariant.Default` (the scaffold
default), and implement the Light/Dark override by setting it to
`ThemeVariant.Light`/`ThemeVariant.Dark` explicitly. No platform-specific
detection code (no GSettings/registry/NSApplication calls).
**Context:** The request was to observe the OS-level theme and let the user
override it. Checked against Avalonia's own theme-variant docs before
implementing: `RequestedThemeVariant = Default` (or unset) already means
"inherit from the system," and Avalonia's `PlatformSettings` abstraction
handles the actual OS-specific detection — including live updates if the
OS setting changes while the app is running — on all three target
platforms.
**Rationale:** Writing custom per-OS theme-detection code would duplicate
functionality Avalonia already provides, for no benefit, and would be a
second thing to keep working across Linux/Windows/macOS instead of relying
on the UI framework's own cross-platform abstraction.
**Alternatives considered:** None seriously — this was a "verify the
framework already does this" check, not a design choice with real
alternatives.
**Consequences:** view-md has no explicit OS-theme-changed event handler;
if Avalonia's automatic re-styling on OS change ever proves insufficient
(e.g. some other part of the UI doesn't visually update), the fix is to
subscribe to `Application.Current.PlatformSettings.ColorValuesChanged` or
`TopLevel.ActualThemeVariantChanged`, not to add OS-specific detection.

---

## Preferences typography: one global scale, not per-element styling — 2026-08-03
**Decision:** `AppSettings`/`MarkdownRenderOptions` expose exactly four
appearance knobs — font family, base font size, line-height multiplier,
document margin — applied uniformly across the rendered document, rather
than a richer per-block-type style system (e.g. independently configurable
heading fonts, per-side margins, code-block font size separate from body).
**Context:** The request was general ("some font face/size/line
spacing/margin/padding values"), not a specific enumerated set of controls.
**Rationale:** Four scalar settings cover the realistic "make text more
readable to me" use case with a small, easy-to-understand Preferences
dialog. Line-height is deliberately a *multiplier* of font size (not a
stored pixel value) so it continues to make sense as font size changes.
**Alternatives considered:** A full CSS-like box model (independent
margin/padding per side, per-element font overrides) — rejected as
speculative complexity with no concrete use case driving it yet.
**Consequences:** If a future request needs finer control (e.g. "headings
should use a different font than body text"), that's a deliberate
expansion of `MarkdownRenderOptions`, not something the current shape
accidentally already supports.
