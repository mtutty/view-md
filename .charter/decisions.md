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

---

## Windows/macOS builds are self-contained JIT, not NativeAOT — 2026-08-03
**Decision:** `packaging/build-windows.sh` and `packaging/build-macos.sh`
cross-publish `win-x64`/`osx-arm64` from the Linux build host as standard
self-contained (JIT) deployments, not NativeAOT. Only `packaging/build-deb.sh`
(linux-x64, built natively) uses `PublishAot`.
**Context:** Checked against Microsoft Learn's "Cross-compilation - .NET"
page before implementing: NativeAOT does not support cross-OS compilation —
there's no standardized way to obtain a native macOS SDK on Linux, or a
Windows SDK on Linux, so AOT can only target the OS it's built on (limited
cross-*architecture* support exists, e.g. x64->arm64 on the same OS, but
that's not what's needed here). A plain self-contained publish has no such
restriction and was verified to actually work: both `win-x64` and
`osx-arm64` were cross-published from this Linux host and produced valid
PE32+ and Mach-O binaries respectively (checked directly, not assumed).
`src/ViewMd/ViewMd.csproj` scopes `PublishAot` to
`'$(RuntimeIdentifier)' == 'linux-x64'` so publishing the other RIDs from
Linux doesn't attempt (and fail at) AOT compilation.
**Rationale:** Given a single Linux docker build agent, this is the only
way to produce genuinely runnable Windows/macOS builds at all. The
alternative — not shipping Windows/macOS builds — was explicitly what the
user asked to avoid unless truly prohibitive, and this isn't: it's a
one-line RID swap, verified working.
**Alternatives considered:** Windows/macOS-hosted build agents (not
available — only one docker-capable Linux agent label was given); skipping
Windows/macOS entirely (rejected — cross-publish works fine, so skipping
would be leaving something achievable on the table).
**Consequences:** Windows/macOS cold-start is JIT speed, not AOT speed —
noticeably slower first paint than the Linux build, though still normal
JIT startup, not egregious. Binaries are much larger (~74-113MB zipped vs
~13MB for the Linux .deb) since nothing is trimmed. If Windows/macOS build
agents become available later, switching those two scripts to build
natively (with AOT) on their own OS is the natural upgrade — the app code
itself doesn't change, only which host builds which artifact.

---

## macOS build is unsigned; no notarization — 2026-08-03
**Decision:** `packaging/build-macos.sh` produces an unsigned `.app` bundle
with no code signing and no notarization step.
**Context:** Proper macOS distribution requires an Apple Developer Program
membership ($99/year) plus running the built app through Apple's
notarization service. Without both, Gatekeeper blocks the app from opening
on any Mac other than the one that built it (the user can still bypass this
per-app via right-click -> Open, or `xattr -dr com.apple.quarantine`).
**Rationale:** This is a real, recurring monetary cost for a
personal/small-scale project, not an engineering task — it doesn't belong
in this pass. The unsigned build is genuinely usable for the
person who builds it for their own Mac, which covers the stated use case.
**Alternatives considered:** Ad-hoc self-signing (rejected: still triggers
Gatekeeper for anyone but the builder, so it wouldn't actually solve
anything — only real Apple notarization does); skipping macOS entirely
(rejected — see the cross-publish decision above, an unsigned build is
still meaningfully useful).
**Consequences:** If public macOS distribution is ever wanted, that's a
new decision requiring an Apple Developer account and a notarization step
added to `build-macos.sh` — not a natural extension of the current script.

---

## Jenkins base image: mcr.microsoft.com/dotnet/sdk:10.0-noble-aot — 2026-08-03
**Decision:** The Jenkinsfile's single docker agent uses Microsoft's
official `10.0-noble-aot` SDK image (Ubuntu 24.04 "Noble" + `clang`/`llvm`/
`zlib1g-dev` already installed for NativeAOT), plus `git`, `zip`, and
`libfontconfig1` installed as a pipeline step.
**Context:** Checked directly against the image's own Dockerfile in the
`dotnet/dotnet-docker` repo rather than assuming what it contains. The
entire pipeline (restore, build, the headless smoke-test stage, and all
three packaging scripts) was run inside this exact image against this repo
during development — not just written and assumed correct. That run caught
a real gap: the base image lacks `libfontconfig1` (a SkiaSharp/Avalonia.Skia
runtime dependency), which made the headless-render test stage crash with
`DllNotFoundException` until added. `git` and `zip` aren't part of the base
image either — `git` because Jenkins' own SCM checkout needs it inside the
container, `zip` for the Windows/macOS packaging scripts.
**Rationale:** Using the official AOT-focused image avoids hand-maintaining
the NativeAOT prerequisite list; the extra three packages are cheap and
were identified by actually running the pipeline, not guessed.
**Alternatives considered:** Plain `mcr.microsoft.com/dotnet/sdk:10.0` +
manually installing `clang`/`llvm`/`zlib1g-dev` (rejected: duplicates what
the official `-aot` variant already provides, more to maintain for no
benefit).
**Consequences:** The image tag `10.0-noble-aot` floats across .NET 10.0.x
patch releases (not digest-pinned) — acceptable for this project's scale,
but means the exact base image contents can shift over time. If a future
CI failure looks environment-related, checking what changed in that image
tag is the first thing to try.

---

## Image display: standalone vs. inline images render through different paths — 2026-08-06
**Decision:** `MarkdownRenderer` renders `![alt](url)` differently depending
on whether the image is the *only* content of its paragraph or mixed in with
other text. A standalone image (the common case — an image alone on its own
line) is rendered as a real block-level `Image` control, sibling to
headings/paragraphs in the root `StackPanel`. An image genuinely inline with
other text (e.g. `text ![icon](url) more text`) is rendered instead as a
plain clickable `[image: alt]` text link — the same in-flow `Span`
mechanism links already use — that opens the image externally when clicked,
rather than as an embedded picture. Separately, `http(s)://` images are now
actually fetched (superseding the "no network fetch in v1" placeholder from
the markdown-rendering capability doc), synchronously, via a `HttpClient`
with a 5s timeout, on top of already-supported relative paths and newly
added `file://` URL support.
**Context:** Verified empirically (headless smoke-test renders, and
confirmed against the real windowed app, not just headless) that embedding
a real `Image` control inline inside a `TextBlock` via `InlineUIContainer`
is unsafe in Avalonia 12.1.1 for anything but a trivial (e.g. 1x1 solid
color) source: a normal-sized image (tested with a 288x288 PNG) blows up
the line box and silently blanks the entire line — the image *and* any
surrounding text — with no exception thrown. This reproduces for local
files too, so it's unrelated to network/async timing. A separate attempt at
async-fetching remote images and patching the already-rendered `TextBlock`
in place once the download completed (swapping the embedded control's
`Child`, mutating its properties, calling `TextBlock.InvalidateMeasure()`,
and replacing the slot in the owning `InlineCollection` via its indexer)
was also tried and also failed the same way — a `TextBlock`'s `TextLayout`
does not appear to be safely updatable piecemeal after initial layout, for
any of those mechanisms tried.
**Rationale:** The standalone case is both the overwhelmingly common
real-world pattern (matches the user's own example verbatim) and trivially
safe to fix — render it as a normal block-level control the same way
`RenderCodeBlock`/`RenderTable` already do, entirely avoiding
`InlineUIContainer`. The inline case is a much deeper, pre-existing
rendering limitation (not something newly introduced by adding `file://`/
`http(s)://` support — it already applied to a real *local* inline image);
degrading it to a text link keeps documents readable and images reachable
(click to open externally) without chasing what would likely be a
significant restructuring of how mixed-content paragraphs render.
**Alternatives considered:** Capping the inline image's `MaxHeight` to
roughly a line's height (tried first) — fixed the line-box blowup but not
the separate finding that *any* real inline image (even correctly sized)
still blanks its surrounding text; async fetch-then-patch for remote images
(tried, see Context) — added real complexity for a bug that turned out to
be about image size/embedding, not timing, and was abandoned once that
became clear; doing nothing for `http(s)://` (keeping the old placeholder
text) — rejected because displaying the image is the actual ask.
**Consequences:** A future fix for genuinely-inline images (if ever
prioritized) needs to establish *why* `TextBlock` can't safely host a
real embedded `Image`, not just retry the same mutation-after-layout shapes
already ruled out here. `markdown-rendering.md`'s known-limitations section
should be updated to reflect this. `http(s)://` images add a per-document
open-time cost bounded by (number of remote images) × 5s in the worst case
(all unreachable) — acceptable for the common case of zero-to-few remote
images, but a document with many would open slowly; revisit with concurrent
prefetching if that ever becomes a real complaint.

---

## App icon wired from a single 1024px master, hand-assembled per platform — 2026-08-07
**Decision:** The developer supplied one master icon,
`src/ViewMd/Assets/app-icon.png` (originally 1024x1024, downsized to 256px
in place once all derivatives below were generated from the 1024px source).
All platform-specific icon assets were generated from it in a one-off local
pass with Python/Pillow and committed as static files, rather than added to
the build pipeline: `packaging/windows/app-icon.ico` (multi-resolution,
`<ApplicationIcon>`, scoped to the `win-x64` RID in `ViewMd.csproj`),
`packaging/macos/app-icon.icns` (referenced via `CFBundleIconFile` in
`Info.plist`, copied into the `.app` bundle by `build-macos.sh`), and a
freedesktop.org hicolor icon theme tree under
`packaging/deb/usr/share/icons/hicolor/{size}/apps/view-md.png` (referenced
via `Icon=view-md` in the `.desktop` file; `postinst`/`postrm` now also run
`gtk-update-icon-cache`). Separately, `MainWindow.axaml` sets `Window.Icon`
to the embedded 256px copy for the in-app titlebar/taskbar icon on all three
platforms.
**Context:** The app had no icon at all — every packaging target
(`.desktop`, `Info.plist`, the Windows exe) was missing an `Icon=`/icon
reference entirely, and the window itself used Avalonia's default.
**Rationale:** Pillow can write both `.ico` (PNG-compressed frames) and
`.icns` (Apple's PNG-backed chunk types) directly from a single high-res
source with no OS-specific tool (`icotool`, `iconutil`, `png2icns`) needed —
verified by actually generating and file-type-checking both outputs. Doing
this as a one-off script rather than an MSBuild/Jenkins step avoids adding
Pillow as a CI dependency for something that only needs to run again if the
source artwork itself changes.
**Alternatives considered:** Generating icons at publish time via an MSBuild
target calling out to a conversion tool — rejected as unnecessary ongoing
build complexity for an asset that changes rarely, and none of the
lightweight CLI converters (rsvg-convert, ImageMagick, icotool) were even
installed in the dev environment, whereas Pillow was already available and
sufficient.
**Consequences:** If the source artwork is ever replaced, someone needs to
re-run the same Pillow-based generation (not documented as a repo script,
just the process above) rather than it happening automatically from a
checked-in master — regenerating means redoing this by hand or writing a
small script under `packaging/` if it recurs often enough to be worth
automating.
