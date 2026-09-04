# AdamE.MemoryToolkit.Maui — Complete Feature and Code Analysis

**Analyzed:** 2026-09-04  
**Primary sources:** GitHub `main` (commit tree `58f343a`), README, NuGet 2.0.0, release notes, issues, discussions, unit tests, e2e tests, sample app  
**Wiki:** none (API 404)  
**Extra docs on `main`:** none. PR #35 notes that an audit track (`docs/`, `tools/maui-leak-catalog/`, `tests/MemoryToolkit.Maui.LeakLab/`) was intentionally kept out of the V2 merge.

This file is a reference inventory. It lists what the package claims, what the code actually does, and what it does not do — so you can implement your own leak tooling without guessing.

---

## 1. Package identity

| Item | Value |
|---|---|
| NuGet | [AdamE.MemoryToolkit.Maui](https://www.nuget.org/packages/AdamE.MemoryToolkit.Maui) |
| Latest | **2.0.0** (2026-04-13) — ~13.4k downloads |
| Previous | **1.0.0** (2024-04-17) — ~233k downloads |
| GitHub | [AdamEssenmacher/MemoryToolkit.Maui](https://github.com/AdamEssenmacher/MemoryToolkit.Maui) |
| Stars / forks / watchers | 352 / 17 / 18 |
| License | MIT |
| Author | Adam Essenmacher ([sponsors](https://github.com/sponsors/AdamEssenmacher)) |
| Language | C# |
| Repo created | 2024-01-20 |
| Last push (as of analysis) | 2026-07-03 |
| Assembly / XAML xmlns | `MemoryToolkit.Maui` / `clr-namespace:MemoryToolkit.Maui;assembly=MemoryToolkit.Maui` |
| Typical prefix | `mtk:` |

### Target frameworks (V2)

Library (`MemoryToolkit.Maui.csproj`):

- `net10.0`
- `net10.0-ios`
- `net10.0-maccatalyst`

NuGet reports those as `net10.0`, `net10.0-ios26.0`, `net10.0-maccatalyst26.0`.

Dependency: `Microsoft.Maui.Controls >= 10.0.0`.

There is **no Android or Windows TFM** on the library. Android / Windows apps consume the shared `net10.0` build. Sample app additionally targets Android, iOS, Mac Catalyst, and Windows.

XML docs are generated and packed (`GenerateDocumentationFile=true`). CS1591 (missing XML comments) is suppressed.

---

## 2. What the README says it does (3 primary features)

V2 is described as a **.NET 10 rebaseline**. MAUI 9/10 added automatic handler disconnection and `DisconnectHandlers()`, so V2 defaults to a **less destructive** path.

1. **Detect leaks** in MAUI views as they happen.
2. **Request cleanup** for lifecycle-sensitive leaks by using MAUI’s handler disconnection path when a view appears “done”.
3. **Optionally compartmentalize** leaks by clearing strong managed references (`BindingContext`, MAUI behaviors, `Content`, `ItemsSource`, item templates, gesture recognizers, formatted text spans, `Parent`, logical children, resources).

That is the whole product. There is no profiler, no heap dump, no native-memory meter, no Visual Studio debugger integration, and no automatic “fix the leak at the source” rewriter.

---

## 3. Complete feature list (document + code)

### 3.1 Runtime leak detection

| Feature | How you use it | Code |
|---|---|---|
| Visual-tree leak monitor | `mtk:LeakMonitorBehavior.Cascade="True"` | `LeakMonitorBehavior` |
| Suppress a subtree | `mtk:LeakMonitorBehavior.Suppress="True"` | same |
| Optional display name on a target | `mtk:LeakMonitorBehavior.Name="..."` | same (in code; barely mentioned in README) |
| Manual monitor | `someView.Monitor()` | `Utilities.Monitor` |
| Forced GC loop | up to 10 collections, 200 ms apart | `GarbageCollectionMonitor` |
| Leak callback | `options.OnLeaked` | `MemoryToolkitOptions` |
| Collected callback | `options.OnCollected` | same |
| Custom monitor swap | `options.CustomMonitor` | `IGarbageCollectionMonitor` |
| Debug logging | `ILogger<GarbageCollectionMonitor>` from DI | `AppBuilder.UseMemoryToolkit` |
| Zombie / released log lines | `❗🧟❗{Name} is a zombie` / `✅{Name} released` | `GarbageCollectionMonitor` |

Detection is **WeakReference + `GC.Collect()` + `GC.WaitForPendingFinalizers()`**. If the reference is still alive after `MaxCollections`, it is a leak.

**Do not use leak detection in Release.** The README and discussions are explicit: forced GC is expensive and is only there to make collection deterministic.

### 3.2 Automatic teardown when a view is “done”

| Feature | How you use it | Code |
|---|---|---|
| Auto teardown on unload | `mtk:TearDownBehavior.Cascade="True"` | `TearDownBehavior` |
| Per-view strategy override | `mtk:TearDownBehavior.Strategy="..."` | attached `TearDownStrategy?` |
| Global default strategy | `options.DefaultTearDownStrategy` | default = `DisconnectHandlers` |
| Suppress a subtree | `mtk:TearDownBehavior.Suppress="True"` | same |
| Manual teardown | `vte.TearDown()` or `vte.TearDown(strategy)` | `Utilities.TearDown` |
| Per-element hook | `TearDownBehavior.OnTearDown` | **Compartmentalize only** |
| Honor MAUI `HandlerDisconnectPolicy.Manual` | automatic | `DisconnectHandlers` path only |

`UseMemoryToolkit` is **required for leak detection and global V2 defaults**. `TearDownBehavior` works without it (falls back to the static default options object).

### 3.3 Three teardown strategies

| Strategy | Effect |
|---|---|
| `DetectOnly` | No teardown. Graph stays intact. |
| `DisconnectHandlers` (V2 default) | Walk visual children, call `IElementHandler.DisconnectHandler()` per view. **Does not** null `BindingContext`, `Content`, `Parent`, etc. |
| `Compartmentalize` | Walk children, then clear managed references (best-effort), invoke `OnTearDown`, then disconnect the element’s handler. |

### 3.4 Lifecycle inference (“done with”)

Automatic behaviors do **not** tear down on every `Unloaded`. They wait until `ElementLifecycleTracker` believes the view is finished.

Treated as done when:

1. Host `Page` was **popped** from an active `NavigationPage`.
2. View unloaded and is **not hosted in a `Page`** (ControlTemplate swap, detached view).
3. Hosted in a `NavigationPage` that is itself unloaded and **not suppressed**.
4. Not in a `NavigationPage`, still unloaded after a **100 ms** delay, **not** in `NavigationStack` / `ModalStack`, and **not** still hosted by `Shell`.

Skipped:

- The `NavigationPage` container itself (call `TearDown()` if you really destroy it).
- Views under a `NavigationPage` that still has **modal pages**.
- Views whose host page is still in Shell / flyout / tab content.
- Suppressed views.
- Intentionally cached pages (you must `Suppress`).

This is **not** full Shell / flyout / tab / modal tracking. README says so. Code confirms it.

### 3.5 Developer-facing utilities

| API | Purpose |
|---|---|
| `Utilities.GetFirstSelfOrParentOfType<T>(Element)` | Walk `Parent` chain. Used in README for temporarily suppressing a `NavigationPage` around `Browser.OpenAsync`. |
| `Utilities.Monitor(object)` | Snapshot visual tree + handlers into `CollectionTarget`s, then force-GC. |
| `Utilities.TearDown(IVisualTreeElement)` | Use configured default strategy. |
| `Utilities.TearDown(IVisualTreeElement, TearDownStrategy)` | Explicit strategy. |

### 3.6 App builder / options

```csharp
builder.UseMemoryToolkit(options =>
{
    options.DefaultTearDownStrategy = TearDownStrategy.DisconnectHandlers;
    options.OnLeaked = target => { /* ... */ };
    options.OnCollected = target => { /* ... */ };
    options.CustomMonitor = /* IGarbageCollectionMonitor */;
});
```

V1 compatibility (obsolete):

```csharp
builder.UseLeakDetection(onLeaked, onCollected, customMonitor);
```

Must be called **after** logging is configured if you want toolkit logs to use the app logger.

Implementation detail: `UseMemoryToolkit` builds a temporary `IServiceProvider` just to resolve `ILogger<GarbageCollectionMonitor>`. That is a known smell (service provider built before `builder.Build()`).

### 3.7 Sample app (`samples/ShellSample`)

- Shell app, intended for **iOS / Mac Catalyst**.
- Page with `CollectionView` of 100 picsum photos.
- Each item VM holds a **300 KB** byte array (artificial heap weight).
- Push / pop navigation.
- Live managed heap size (`GC.GetTotalMemory(false)`) and leak count.
- `LeakyLabel` roots itself via `Application.Current.PropertyChanged += ...` so the toolkit has a **predictable leak**.
- `LeakMonitorBehavior.Cascade` is on; `TearDownBehavior.Cascade` is off by default.
- Leak alerts use `MainThread` + `DisplayAlertAsync`.
- README is clear: current MAUI should **not** turn one leaked label into a full-page leak. The demo is a signal, not a V1-era horror show.

### 3.8 Tests

**Unit** (`tests/MemoryToolkit.Maui.Tests`, xUnit):

- Options default + enum surface
- Attached property round-trip (`Cascade` / `Suppress` / `Name` / `Strategy`)
- Cascade rejects non-`VisualElement`
- Lifecycle: no host page, suppress, Shell stack, flyout, modal, pop
- Teardown: DetectOnly / DisconnectHandlers / Compartmentalize graphs
- Handler disconnect exceptions (including `ObjectDisposedException`) do not abort siblings
- `OnTearDown` only in Compartmentalize
- Managed-reference clear failures are swallowed
- `HandlerDisconnectPolicy.Manual` honored
- Child `Suppress` and child `Cascade` boundaries honored
- GC monitor collected vs leaked

**E2E** (`tests/MemoryToolkit.Maui.E2ETests`, Mac Catalyst / iOS app, not CI):

1. `LeakMonitorBehavior` fires from a real `Unloaded`.
2. `TearDownBehavior` + `Compartmentalize` clears BC / behaviors / gestures from a real `Unloaded`.
3. Navigation pop + Compartmentalize collects views, handlers, platform views, and binding context.

**Smoke** (`tests/smoke/`):

- `run-maccatalyst-smoke.sh` — launch sample, wait, quit.
- `run-maccatalyst-e2e.sh` — launch e2e app and assert the three scenarios.
- Not wired to CI. Local GUI macOS only.

---

## 4. Repository layout

```
MemoryToolkit.Maui/
├── README.md
├── LICENSE                          # MIT
├── .github/FUNDING.yml
├── src/
│   ├── MemoryToolkit.Maui.sln
│   └── MemoryToolkit.Maui/
│       ├── AppBuilder.cs
│       ├── MemoryToolkitOptions.cs  # + internal MemoryToolkitConfiguration
│       ├── TearDownStrategy.cs
│       ├── LeakMonitorBehavior.cs
│       ├── TearDownBehavior.cs
│       ├── ElementLifecycleTracker.cs
│       ├── GarbageCollectionMonitor.cs
│       ├── CollectionTarget.cs
│       ├── Utilities.cs             # Monitor + TearDown + parent walk
│       ├── MemoryToolkit.Maui.csproj
│       └── Properties/AssemblyInfo.cs
├── samples/ShellSample/             # demo app
└── tests/
    ├── MemoryToolkit.Maui.Tests/
    ├── MemoryToolkit.Maui.E2ETests/
    └── smoke/
```

Library surface is **10 C# files**. Internals are visible to the unit test project only.

---

## 5. Public API surface

### Types

| Type | Kind | Notes |
|---|---|---|
| `AppBuilder` | static class | `UseMemoryToolkit`, obsolete `UseLeakDetection` |
| `MemoryToolkitOptions` | sealed class | 4 properties |
| `TearDownStrategy` | enum | `DetectOnly`, `DisconnectHandlers`, `Compartmentalize` |
| `LeakMonitorBehavior` | class of attached properties | not a MAUI `Behavior<T>` |
| `TearDownBehavior` | static class of attached properties | not a MAUI `Behavior<T>` |
| `Utilities` | static class | `Monitor`, `TearDown`, `GetFirstSelfOrParentOfType` |
| `CollectionTarget` | class | `Name` + `WeakReference` |
| `IGarbageCollectionMonitor` | interface | leak/collected callbacks + force-GC |
| `GarbageCollectionMonitor` | class | default singleton |

Internal (not for apps):

- `MemoryToolkitConfiguration`
- `ElementLifecycleTracker`
- `LifecycleAction`
- `TrackedElement`

### Attached properties

**LeakMonitorBehavior**

| Property | Type | Default | Effect |
|---|---|---|---|
| `Cascade` | `bool` | `false` | Subscribe `Unloaded` → monitor when done |
| `Suppress` | `bool` | `false` | Skip this view and children |
| `Name` | `string` | `null` | Override log / callback name |

**TearDownBehavior**

| Property | Type | Default | Effect |
|---|---|---|---|
| `Cascade` | `bool` | `false` | Subscribe `Unloaded` → tear down when done |
| `Suppress` | `bool` | `false` | Skip this view and children |
| `Strategy` | `TearDownStrategy?` | `null` | Override; else `DefaultTearDownStrategy` |

`Cascade` may only be set on a `VisualElement`. Setting it on a plain `BindableObject` throws `InvalidOperationException`.

`LeakMonitorBehavior` / `TearDownBehavior` are **attached properties**, not items in `VisualElement.Behaviors`. That is why `Compartmentalize` can `Behaviors.Clear()` without removing the toolkit hooks.

XAML order: put `TearDownBehavior` **after** `LeakMonitorBehavior` so teardown does not run before the monitor snapshots the tree.

### `GarbageCollectionMonitor` knobs (not in options)

| Property | Default | Meaning |
|---|---|---|
| `Instance` | new monitor | Global singleton; replaceable |
| `MaxCollections` | `10` | Forced GC passes |
| `MillisecondsBetweenCollections` | `200` | Delay between passes |
| `Logger` | `NullLogger` | Set by `UseMemoryToolkit` |
| `OnLeaked` / `OnCollected` | `null` | Wired from options |

These two ints are **not** exposed on `MemoryToolkitOptions`. To change them you assign a custom `GarbageCollectionMonitor` (or implement `IGarbageCollectionMonitor`).

---

## 6. Code-level architecture

```
XAML Cascade=True
        │
        ▼
 Unloaded event
        │
        ▼
 ElementLifecycleTracker.RunWhenDoneAsync
        │
        ├── suppressed?            → stop
        ├── is NavigationPage?     → stop
        ├── no host Page?          → run now   (template swap / detached)
        ├── has NavigationPage?
        │     ├── modal stack > 0  → stop
        │     ├── nav not loaded   → run (unless nav suppressed)
        │     └── else track until NavigationPage.Popped
        └── else wait 100 ms
              ├── still in nav/modal stack → stop
              ├── still hosted by Shell    → stop
              ├── has Tab parent           → stop
              └── else run
                    │
                    ├── LeakMonitorBehavior → Utilities.Monitor()
                    └── TearDownBehavior    → Utilities.TearDown(strategy)
```

Both behaviors share the same tracker. Each action has a string key (`"LeakMonitorBehavior"` / `"TearDownBehavior"`) so the same view can be tracked twice without colliding.

Tracked state uses **weak references** (`WeakReference<VisualElement>`, `WeakReference<Page>`, `WeakReference<NavigationPage>`) plus a static list. Dead entries are cleaned on the next track.

---

## 7. Component-by-component analysis

### 7.1 `LeakMonitorBehavior`

On `Cascade=true`, subscribe `Unloaded`. When lifecycle says done, call `visualElement.Monitor()`.

`Monitor()`:

1. Recurse `IVisualTreeElement.GetVisualChildren()`.
2. Skip a child if `Suppress` or (not root and already `Cascade`) — avoids double-monitoring nested Cascade islands.
3. Add a `CollectionTarget` for the VTE (name from `LeakMonitorBehavior.Name` if set).
4. If it is a `VisualElement`/`Element` with a handler, add a **second** `CollectionTarget` for the handler.
5. Non-VTE objects are still wrapped as a target.
6. Hand the list to `GarbageCollectionMonitor.Instance.MonitorAndForceCollectionAsync`.

**Documented gap (confirmed in code):** monitoring walks the tree **at Unload**. A child removed earlier is never seen. Put another `Cascade` on that child if you care.

Handlers are monitored separately from views. A leaked handler with a collected view still reports.

### 7.2 `GarbageCollectionMonitor`

```text
for pass in 1..MaxCollections:
    GC.Collect()
    GC.WaitForPendingFinalizers()
    for each target:
        if dead → OnCollected, remove
        if last pass and alive → OnLeaked, remove
        else keep for next pass
    delay MillisecondsBetweenCollections
```

`CollectionTarget` is `new WeakReference(reference)` (short weak, not tracking resurrection). `Name` defaults to `reference.GetType().Name`.

This is a **liveness test**, not a roots finder. It cannot tell you *why* something is alive (event, static, captured lambda, native cycle). You still need a memory profiler for diagnosis.

### 7.3 `TearDownBehavior` + `Utilities.TearDown`

#### `DetectOnly`

Return immediately.

#### `DisconnectHandlers`

Does **not** call MAUI’s `view.DisconnectHandlers()` extension, despite README wording. It reimplements a safer tree walk:

1. Flatten `IView` children.
2. Skip if:
   - `HandlerProperties.GetDisconnectPolicy == HandlerDisconnectPolicy.Manual`
   - `TearDownBehavior.Suppress`
   - not root and child has its own `Cascade` (boundary)
3. For each remaining view, `handler.DisconnectHandler()` inside try/catch.
4. On exception: log warning, continue siblings.

This addresses issues #10, #23, #25 (disconnect / `ObjectDisposedException` crashes).

No managed graph is touched. Tests assert `Content`, `BindingContext`, `ItemsSource`, behaviors, gestures, and formatted text all remain.

#### `Compartmentalize`

Depth-first children, then for each node `ClearMauiReferences`, then `OnTearDown` (if handler exists), then `DisconnectHandler()` on **that** element only.

`OnTearDown` is **not** invoked for `DetectOnly` or `DisconnectHandlers`. README example: turn off `SKLottieView.IsAnimationEnabled` before disconnect.

Each clear is isolated:

```csharp
try { clearReference(); }
catch { log warning; continue; }
```

A throwing setter does not abort the rest (issue #17 class). Tests prove a `ContentView` that throws on Content-null still disconnects its handler and keeps Content.

### 7.4 Exact references `Compartmentalize` clears

From `Utilities.ClearMauiReferences` / `ClearFormattedTextReferences`:

| Target type | What is cleared |
|---|---|
| `VisualElement` | `Behaviors.Clear()`, `Resources = null` (after handler work) |
| `Element` | `BindingContext = null`, `Parent = null`, `ClearLogicalChildren()` |
| `View` | `GestureRecognizers.Clear()` |
| `Label` | each span’s gestures, `Spans.Clear()`, `FormattedText = null` |
| `ItemsView` (CollectionView, CarouselView, …) | `ItemsSource = null`, `ItemTemplate = null` |
| `ListView` (obsolete API, still handled) | `ItemsSource = null`, `ItemTemplate = null` |
| `ContentView` | `Content = null` |
| `Border` | `Content = null` |
| `ContentPage` | `Content = null` |
| `ScrollView` | `Content = null` |

Then handler disconnect + optional `OnTearDown`.

That list is the **entire** containment surface. Everything else is out of scope unless you hook `OnTearDown`.

### 7.5 What `Compartmentalize` does **not** clear

Confirmed by reading `Utilities.cs` and by closed-but-unmerged / unanswered issues:

- `ToolbarItems` / `MenuBarItems` (PR #16 proposed this, **not merged**)
- `IDisposable` / `IAsyncDisposable` (issue #22 — closed in V2 without implementing dispose; V2 moved away from dispose-heavy teardown)
- `BindableLayout.ItemsSource` / layout item templates
- `ItemsView` header/footer/empty view/group templates
- `Triggers`, `Effects`, `Style`, `ControlTemplate`
- Button / toolbar `Command` objects except indirectly via `BindingContext = null`
- Event subscriptions (cannot)
- `WebView`, `MediaElement`, maps, Skia, Syncfusion, CommunityToolkit popups
- `Shadow`, `StrokeShape` (issue #18)
- Native / platform views beyond handler disconnect
- Shell flyout / tab item graphs as first-class objects

If a remaining root still holds the view (sample’s `Application.PropertyChanged`), compartmentalize shrinks the retained graph but **does not collect the leaked control**.

### 7.6 `ElementLifecycleTracker` details

| Rule | Code behavior |
|---|---|
| NavigationPage itself | Always ignored |
| No `Page` ancestor | Immediate `OnDone` |
| `NavigationPage` + `ModalStack.Count > 0` | Ignore (fixes #33 / iOS modal unload) |
| `NavigationPage` not loaded | Treat hosted views as done unless nav is suppressed (Browser.OpenAsync pattern) |
| `NavigationPage` loaded | Subscribe `Popped` once per nav page; fire when `e.Page` matches host |
| No NavigationPage | `Task.Delay(100)` then re-check stacks + Shell parent |
| Still in `NavigationStack` or `ModalStack` | Do nothing (fixes #11 — previous page torn down on push) |
| Still has `Shell` ancestor | Do nothing (fixes #31 flyout false zombies) |
| Has `Tab` parent after those checks | Do nothing (extra conservative) |

False-positive history V1 had: flyout apps reporting every previous page as a zombie; Shell push tearing down the page you just left. V2 added the Shell / stack guards.

Remaining honesty from README: this is best-effort inference, not a navigation framework.

---

## 8. “Done with” vs MAUI 9/10 reality

README problem statement (still accurate):

1. **Leak propagation.** V1/.NET 8 could retain a whole page from one child. Current MAUI uses weaker parents and auto-disconnects handlers more often, so a leaked child is less likely to pin the page. A leaked control can still pin its own `BindingContext`, `Content`, `ItemsSource`, templates, resources, events, commands, platform objects. `Compartmentalize` is opt-in containment, not a default.
2. **Lifecycle.** Some controls (especially Apple, cycles) still need an explicit “this view is finished” signal: cached pages, ControlTemplate swaps, unload outside navigation.

V2 default (`DisconnectHandlers`) is **low-destruction**: ask MAUI to drop handlers when the toolkit thinks the view is done. `Compartmentalize` is **invasive** and can break UI if it fires too early (tabs, cached pages, Syncfusion tab views — discussion #8).

---

## 9. How to use it (from docs + sample)

### Debug: detect only

```csharp
#if DEBUG
builder.Logging.AddDebug();
builder.UseMemoryToolkit(options =>
{
    options.OnLeaked = t => { /* alert / counter */ };
    options.DefaultTearDownStrategy = TearDownStrategy.DetectOnly; // or omit and don't set Cascade teardown
});
#endif
```

```xml
<ContentPage xmlns:mtk="clr-namespace:MemoryToolkit.Maui;assembly=MemoryToolkit.Maui"
             mtk:LeakMonitorBehavior.Cascade="True">
```

### Debug: detect + disconnect handlers

```xml
mtk:LeakMonitorBehavior.Cascade="True"
mtk:TearDownBehavior.Cascade="True"
mtk:TearDownBehavior.Strategy="DisconnectHandlers"
```

### Production: teardown without detection (community consensus, discussion #4)

README: prevention / compartmentalization intended to be production-safe; detection is not.

Typical pattern:

- Release: `TearDownBehavior.Cascade="True"`, **no** `UseMemoryToolkit`, **no** `LeakMonitorBehavior`.
- Debug: both, plus `#if DEBUG` builder hook.

V1 discussion used old names (`AutoDisconnectBehavior` / `GCMonitorBehavior`). Those names no longer exist.

### Temporary unload (Browser, etc.)

```csharp
var nav = Utilities.GetFirstSelfOrParentOfType<NavigationPage>(this);
LeakMonitorBehavior.SetSuppress(nav, true);
TearDownBehavior.SetSuppress(nav, true);
// later, on Loaded:
LeakMonitorBehavior.SetSuppress(nav, false);
TearDownBehavior.SetSuppress(nav, false);
```

### ControlTemplates

Sharpnado.TaskLoaderView-style template swaps: put Cascade on **each template root**, not only the host control.

### C# instead of XAML

Issue #9 asked for this. Attached property setters exist:

```csharp
LeakMonitorBehavior.SetCascade(page, true);
TearDownBehavior.SetCascade(page, true);
TearDownBehavior.SetStrategy(page, TearDownStrategy.DisconnectHandlers);
```

Or skip inference and call `page.Monitor()` / `page.TearDown(strategy)` yourself.

---

## 10. V1 vs V2 (from README, PR #35, release notes)

| | V1 (1.0.0, .NET 8 era) | V2 (2.0.0, .NET 10) |
|---|---|---|
| Default teardown | Aggressive / dispose-heavy containment | `DisconnectHandlers` |
| Entry point | `UseLeakDetection` | `UseMemoryToolkit` (`UseLeakDetection` obsolete wrapper) |
| Strategies | Implicit “always compartmentalize-ish” | `DetectOnly` / `DisconnectHandlers` / `Compartmentalize` |
| Handler disconnect | Could crash the app (#10, #23, #24) | try/catch, continue |
| CollectionView | ItemsSource not cleared (#15) | `ItemsView` + legacy `ListView` |
| Gestures / spans / behaviors | Missing (#12, #13, #14) | Cleared in Compartmentalize |
| Shell / flyout false leaks | Yes (#11, #31) | Guarded |
| Modal NavigationPage | iOS empty ModalStack / false teardown (#33) | Skip while modal active |
| Sample | ListView + compatibility cruft | CollectionView, UI-thread alerts |
| Tests | Thin | Unit + Mac Catalyst e2e |
| Docs in package | README | README + XML docs |

V2 **does not** bring back V1 dispose-on-teardown. That is a deliberate product change, not an omission.

---

## 11. Issues and discussions (feature-relevant)

Open issues on `main` at analysis time: **0**.

Closed issues that shaped the feature set:

| # | Title | Outcome in V2 code |
|---|---|---|
| 10 | Try/Catch in disconnect | Yes |
| 11 | Previous page torn down on navigate | Shell/stack guards |
| 12 | Clear gesture recognizers | Compartmentalize |
| 13 | Clear label formatted text | Compartmentalize |
| 14 | Clear behaviors | Compartmentalize (`Behaviors` only, not toolkit attached props) |
| 15 | CollectionView ItemsSource | `ItemsView` |
| 16 | Toolbar items teardown | **PR not merged** — still missing |
| 17 | iOS teardown crashes | Best-effort clears |
| 18 | Border StrokeShape | Not implemented |
| 22 | `IAsyncDisposable` | Not implemented (V2 dropped dispose path) |
| 23 / 25 | `ObjectDisposedException` | Caught |
| 31 | Flyout false zombies | Shell host check |
| 33 | Modal stack / iOS | Skip when modal active |
| 35 | .NET 10 modernization | Merged — this is V2 |

Discussions (Q&A, still useful):

| # | Topic | Takeaway |
|---|---|---|
| 2 | MAUI Hybrid | Toolkit watches **MAUI visual tree / handlers**, not Blazor circuit objects. Hybrid pages can use it for the MAUI chrome; Blazor leaks are a different problem. |
| 4 | Release vs Debug | Detection off in Release; teardown can stay on. |
| 8 | Syncfusion TabView NRE | Third-party tabs + teardown is unsafe; Suppress or don’t Cascade those hosts. |
| 19 | CommunityToolkit.Maui Popups | Popup lifetime ≠ Page pop; inference often wrong; use `Monitor()`/`TearDown()`/`Suppress`. |
| 20 | ListView cleanup | V2 still special-cases obsolete `ListView`. |
| 21 | Debug/Release from code | Use `#if DEBUG` + attached setters. |
| 27 / 28 | Fix code vs just TearDown | Toolkit is a **mitigation**. Real fix is still unsubscribe / don’t capture `this` / don’t leak handlers. |
| 32 | `MauiContext` should have been set | Disconnect/teardown racing platform lifecycle; V2 is more defensive but not immune. |

Used-by signal (NuGet): UraniumUI, Plugin.Maui.Calendar, Cyber.FrameworkNet8.

---

## 12. Limitations (honest list)

1. **No root-cause diagnosis.** Alive vs dead only.
2. **Forced GC** makes detection Debug-only.
3. **Unload-time tree walk** misses dynamically removed children.
4. **Lifecycle inference is incomplete** for Shell tabs, flyouts, popups, cached pages, Hybrid, third-party hosts.
5. **`Compartmentalize` is destructive.** Early fire = blank / broken UI.
6. **Does not dispose** view models or `IAsyncDisposable` resources.
7. **Does not clear** toolbar items, bindable layouts, empty/header templates, events, commands as objects.
8. **Disconnect path is a reimplementation**, not a call to MAUI `DisconnectHandlers()`. Behavior is close, plus Suppress / Cascade / Manual policy.
9. **No Android/Windows-specific code.** Those platforms get the shared managed implementation.
10. **No CI** for the e2e/smoke Mac Catalyst runners.
11. **Singleton statics** (`GarbageCollectionMonitor.Instance`, `TearDownBehavior.OnTearDown`, `MemoryToolkitConfiguration.Options`) — not multi-window / test-isolation friendly without care.
12. **`UseMemoryToolkit` builds an intermediate service provider** to grab a logger.
13. **Not a substitute for Instruments / dotMemory / VS diagnostic tools** for native or complex managed graphs.

---

## 13. Feature checklist (for your own implementation)

If you reimplement this “your own way,” this is the actual feature set to accept, drop, or extend:

### Detection

- [x] Attach `Cascade` on a page; walk visual tree on unload
- [x] Track view **and** handler as separate weak targets
- [x] Optional per-view name
- [x] Suppress subtree
- [x] Nested Cascade = monitoring island (don’t double-count)
- [x] Force GC N times with delay
- [x] OnLeaked / OnCollected / ILogger
- [x] Pluggable monitor
- [ ] Find GC roots / retain cycles (not present)
- [ ] Native heap / image caches (not present)
- [ ] Detect children removed before unload (not present)

### Teardown

- [x] Three strategies
- [x] Default = handler disconnect only
- [x] Opt-in graph break (`Compartmentalize`)
- [x] Best-effort try/catch per clear and per disconnect
- [x] Honor `HandlerDisconnectPolicy.Manual`
- [x] Honor Suppress + child Cascade boundaries
- [x] `OnTearDown` hook (Compartmentalize only)
- [x] Clear: BC, Parent, logical children, Behaviors, Resources, gestures, label spans, ItemsView/ListView source+template, Content on ContentView/Border/ContentPage/ScrollView
- [ ] ToolbarItems (requested, not shipped)
- [ ] IDisposable / IAsyncDisposable (intentionally out in V2)
- [ ] BindableLayout / empty/header/group templates
- [ ] Popup / Hybrid / third-party control adapters

### Lifecycle

- [x] NavigationPage.Popped
- [x] Detached / no host page
- [x] Unloaded NavigationPage (with Suppress escape hatch)
- [x] 100 ms fallback + Shell / stack / modal / Tab guards
- [ ] First-class Shell tab/flyout/modal state machine
- [ ] Page cache awareness

### Product extras in the repo

- [x] Shell sample with intentional leak + heap readout
- [x] Unit tests for strategies and lifecycle
- [x] Opt-in Mac Catalyst e2e
- [ ] Wiki / extra design docs on `main`

---

## 14. Source file map

| File | Role |
|---|---|
| `AppBuilder.cs` | DI entry, logger hook, V1 wrapper |
| `MemoryToolkitOptions.cs` | Options + internal static config |
| `TearDownStrategy.cs` | Enum + XML docs |
| `LeakMonitorBehavior.cs` | Cascade / Suppress / Name + Unloaded |
| `TearDownBehavior.cs` | Cascade / Suppress / Strategy / OnTearDown + Unloaded |
| `ElementLifecycleTracker.cs` | “Done with” inference |
| `GarbageCollectionMonitor.cs` | WeakRef + forced GC |
| `CollectionTarget.cs` | Named weak reference |
| `Utilities.cs` | Parent walk, Monitor, TearDown, clears, safe disconnect |
| `AssemblyInfo.cs` | `InternalsVisibleTo` tests |

---

## 15. Linked documents reviewed

| Document | URL | What it added |
|---|---|---|
| README | https://github.com/AdamEssenmacher/MemoryToolkit.Maui/blob/main/README.md | Product story, API usage, “done with”, sample walkthrough |
| NuGet page | https://www.nuget.org/packages/AdamE.MemoryToolkit.Maui | TFMs, versions, dependents, same README |
| V2 release | https://github.com/AdamEssenmacher/MemoryToolkit.Maui/releases/tag/v2.0.0 | .NET 10 scope, strategy list, issue list, validation |
| V1 release | https://github.com/AdamEssenmacher/MemoryToolkit.Maui/releases/tag/v1.0.0 | “Initial release” only |
| PR #35 | https://github.com/AdamEssenmacher/MemoryToolkit.Maui/pull/35 | Full V2 design notes; audit docs excluded |
| Issues 3–35 | GitHub issues API | Gaps, crashes, Shell/modal, what V2 fixed vs skipped |
| Discussions 1–32 | GitHub discussions API | Hybrid, Release settings, popups, third-party tabs |
| Smoke README | `tests/smoke/README.md` | Local Mac Catalyst only, not CI |
| Third-party article | https://mobiletechlead.com/article/find-fix-memory-leaks-dotnet-maui-detection-prevention-tooling | Repeats V1-ish teardown story; treat as secondary. Prefer repo README + this code pass. |

No wiki. No extra markdown under `docs/` on current `main`.

---

## 16. Adjacent tools (not substitutes)

This package is a **MAUI visual-tree leak detector + teardown helper**. Nothing in the usual MAUI plugin catalogs replaces that exact job.

If you are also building diagnostics around the same app:

- Visual Studio / Rider memory profiler, `dotnet-gcdump`, `dotnet-dump`, Instruments (iOS) — for **why** something is rooted.
- MAUI’s own `DisconnectHandlers()` and `HandlerDisconnectPolicy` — V2 wraps / reimplements this.
- [Plugin.Maui.Performance](https://www.nuget.org/packages/Plugin.Maui.Performance) (Niladri Padhy / Nuvyntra Labs) — startup / page / API **timing**, not leak detection. Docs: https://nuvyntralabs.github.io/packages/plugin-maui-performance/
- [Plugin.Maui.Diagnostics](https://www.nuget.org/packages/Plugin.Maui.Diagnostics) — crash / ANR / breadcrumbs, not GC liveness. Docs: https://nuvyntralabs.github.io/packages/plugin-maui-diagnostics/

Use those beside a leak toolkit, not instead of one.

---

## 17. Bottom line

MemoryToolkit.Maui V2 is a small, focused library:

1. **Watch** views/handlers after unload and shout if they survive forced GCs.
2. **Ask MAUI to disconnect handlers** when a conservative lifecycle guess says the view is finished.
3. **Optionally smash the managed graph** so a remaining leak holds less.

That is all of it. The code matches the README except:

- disconnect is a **custom safe walk**, not a direct call to MAUI `DisconnectHandlers()`;
- `LeakMonitorBehavior.Name` exists in code more than in docs;
- several requested clears (toolbar, dispose, StrokeShape) never shipped;
- Shell/popup/Hybrid coverage is explicitly incomplete.

If you build your own, the useful ideas to steal are the **three strategies**, **attached Cascade/Suppress islands**, **WeakReference + N-pass GC**, **best-effort teardown**, and **navigation-aware “done” inference** — plus the V2 lesson that default teardown should stay **low-destruction** on modern MAUI.
