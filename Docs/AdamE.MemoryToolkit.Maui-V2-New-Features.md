# AdamE.MemoryToolkit.Maui V2 — New Features and Improvements

**Compared to:** V1.0.0 (April 2024, .NET 8 era)  
**Shipped as:** V2.0.0 (13 April 2026, .NET 10 / MAUI 10)  
**Source:** [v2.0.0 release](https://github.com/AdamEssenmacher/MemoryToolkit.Maui/releases/tag/v2.0.0), [PR #35](https://github.com/AdamEssenmacher/MemoryToolkit.Maui/pull/35), README, and library code  
**Companion:** [AdamE.MemoryToolkit.Maui-Complete-Analysis.md](./AdamE.MemoryToolkit.Maui-Complete-Analysis.md)

V2 is not a small bump. It retargets the library, changes the default teardown philosophy, and adds a strategy API. Detection still exists; aggressive graph-smashing is now **opt-in**.

---

## What changed in one sentence

V2 defaults to **low-destruction handler disconnect** on modern MAUI, and keeps V1-style containment behind an explicit `Compartmentalize` switch.

---

## New features

These did not exist as first-class APIs in V1.

### 1. `UseMemoryToolkit(options)` entry point

New builder hook. Configure everything in one options object.

```csharp
builder.UseMemoryToolkit(options =>
{
    options.DefaultTearDownStrategy = TearDownStrategy.DisconnectHandlers;
    options.OnLeaked = target => { };
    options.OnCollected = target => { };
    options.CustomMonitor = /* optional */;
});
```

`UseLeakDetection(...)` still works as an **obsolete compatibility wrapper**. New apps should not use it.

### 2. `MemoryToolkitOptions`

New options type:

| Property | Purpose |
|---|---|
| `DefaultTearDownStrategy` | Global teardown mode when XAML does not set `Strategy` |
| `OnLeaked` | Callback when a WeakRef is still alive after forced GCs |
| `OnCollected` | Callback when a target is collected |
| `CustomMonitor` | Swap `IGarbageCollectionMonitor` |

### 3. `TearDownStrategy` enum

New public strategy model:

| Strategy | New? | Meaning |
|---|---|---|
| `DetectOnly` | Yes | Watch only. Do not tear down. |
| `DisconnectHandlers` | Yes (and V2 **default**) | Disconnect handlers. Leave managed graph alone. |
| `Compartmentalize` | Yes as a named opt-in | V1-style graph clear, then disconnect. |

### 4. Per-view `TearDownBehavior.Strategy`

New attached property. Override the global default on one page or subtree:

```xml
mtk:TearDownBehavior.Cascade="True"
mtk:TearDownBehavior.Strategy="Compartmentalize"
```

`null` (unset) falls back to `MemoryToolkitOptions.DefaultTearDownStrategy`.

### 5. Shared `ElementLifecycleTracker`

New internal lifecycle engine used by both leak monitor and teardown.

New / rewritten tracking for:

- Shell-hosted pages
- Flyout / Tab hosts
- `NavigationPage.Popped`
- Modal stack (do nothing while modal pages are up)
- Fallback 100 ms delay for non-navigation unloads
- Skip `NavigationPage` containers themselves

V1 tore down too eagerly (previous page on push, flyout “zombies”). This tracker is the V2 fix.

### 6. Safe handler-disconnect walk

New disconnect path that:

- Walks the visual tree itself (does not blindly call MAUI `DisconnectHandlers()` and hope)
- Honors `HandlerDisconnectPolicy.Manual`
- Honors `TearDownBehavior.Suppress`
- Stops at a child that has its own `Cascade` (teardown island)
- try/catches each `DisconnectHandler()` so one bad handler does not crash the app

### 7. Best-effort `Compartmentalize` clears

Each managed-reference clear is isolated. A throwing setter is logged; teardown continues. V1 could abort the whole page on one bad control.

### 8. Broader `Compartmentalize` surface (new clears)

These clears were missing or incomplete in V1 and are now part of opt-in containment:

| Clear | Issue |
|---|---|
| `View.GestureRecognizers` | #12 |
| `Label.FormattedText` + span gestures | #13 |
| `VisualElement.Behaviors` | #14 |
| `ItemsView.ItemsSource` / `ItemTemplate` (CollectionView, CarouselView) | #15 |
| Legacy `ListView` source / template still handled | #15 / #20 |

Already-known V1-style clears that remain, now only in `Compartmentalize`:

- `BindingContext`
- `Parent`
- `ClearLogicalChildren()`
- `Content` on ContentPage / ContentView / Border / ScrollView
- `Resources = null`
- `OnTearDown` hook (still Compartmentalize-only in V2)

Toolkit hooks stay intact because they are **attached properties**, not items in `Behaviors`.

### 9. XML documentation in the NuGet package

V2 generates and packs XML docs for the public entry point, options, and strategies.

### 10. Unit test suite

New pure tests for:

- Options defaults
- Attached properties
- Lifecycle (Shell, flyout, modal, pop, suppress)
- All three strategies
- Exception swallowing
- `HandlerDisconnectPolicy.Manual`
- Child Suppress / Cascade boundaries
- GC monitor collected vs leaked

### 11. Mac Catalyst e2e app + smoke scripts

New, opt-in, not CI:

- Real `Unloaded` fires `LeakMonitorBehavior`
- Real `Unloaded` + `Compartmentalize` clears BC / behaviors / gestures
- Navigation pop collects views, handlers, platform views, and binding context

Scripts: `tests/smoke/run-maccatalyst-e2e.sh`, `run-maccatalyst-smoke.sh`.

### 12. Modernized Shell sample

- Targets .NET 10 / MAUI 10
- `ListView` replaced with `CollectionView`
- Leak alerts dispatched on the UI thread (`MainThread` + `DisplayAlertAsync`)
- Compatibility / template cruft removed
- Default sample teardown strategy is `DisconnectHandlers`

---

## Improvements (existing features made safer or smarter)

### Platform / product

| Improvement | Detail |
|---|---|
| .NET 10 / MAUI 10 retarget | `net10.0`, `net10.0-ios`, `net10.0-maccatalyst`; `Microsoft.Maui.Controls >= 10.0.0` |
| Less destructive default | Matches MAUI 9/10 auto handler disconnect. Small leaks no longer assume they pin the whole page. |
| README rewritten for V2 | “Done with” rules, strategies, Suppress, ControlTemplates, sample caveats |
| V1 API kept compiling | Obsolete `UseLeakDetection` maps onto `UseMemoryToolkit` |

### Reliability (issue-driven)

| Improvement | Was | Now |
|---|---|---|
| Handler disconnect crash (#10, #23, #24, #25) | Uncaught `InvalidOperationException` / `ObjectDisposedException` | Logged, siblings continue |
| iOS teardown crash (#17) | One failing clear could take down the page | Best-effort per reference |
| Previous page torn down on navigate (#11) | Unload ≈ done | Still-in-stack / still-in-Shell → skip |
| Flyout false zombies (#31) | Unloaded flyout pages reported as leaks | Shell host check |
| Modal NavigationPage (#33) | iOS modal unload looked like “done” | Skip while `ModalStack.Count > 0` |
| CollectionView leak leftover (#15) | Only old ListView path | `ItemsView` + ListView |
| Gestures / spans / behaviors leftover (#12–14) | Not cleared | Cleared in `Compartmentalize` |

### Lifecycle inference

V2 is more conservative about “done”:

1. Host page popped from `NavigationPage` → done
2. No host `Page` (template swap / detached) → done
3. Host `NavigationPage` unloaded and not suppressed → done
4. Else wait 100 ms; skip if still in nav/modal stack, still in Shell, or under a Tab

`NavigationPage` itself is never auto-torn down. Call `TearDown()` if you destroy the container.

### Teardown policy

| V1 | V2 |
|---|---|
| Teardown ≈ dispose-heavy containment | Default = disconnect handlers only |
| One implicit behavior | Three named strategies |
| Easy to wreck cached / tabbed UI | Destructive path is opt-in |

`OnTearDown` is no longer implied on the default path. It runs only for `Compartmentalize`.

---

## What V2 did **not** add

Requested or discussed, still absent:

| Item | Notes |
|---|---|
| ToolbarItems / MenuBarItems clear | PR #16 not merged |
| `IDisposable` / `IAsyncDisposable` | Issue #22; V2 **dropped** dispose-heavy teardown on purpose |
| Border `StrokeShape` | Issue #18 |
| BindableLayout / empty / header / group templates | Not in `ClearMauiReferences` |
| First-class Shell tab / flyout / popup lifecycle | Guarded, not fully modeled |
| MAUI Hybrid / Blazor circuit tracking | Visual tree only |
| Root-cause / retain-path diagnosis | Still WeakRef + forced GC only |
| Android / Windows-specific TFMs | Shared `net10.0` build |
| CI for Mac Catalyst e2e | Local smoke only |
| Leak-lab / MAUI issue catalog | Explicitly left out of PR #35 |

---

## Quick upgrade notes (V1 → V2)

1. Retarget the app to **.NET 10 / MAUI 10**.
2. Replace `UseLeakDetection` with `UseMemoryToolkit`.
3. Expect default teardown to **stop clearing** `BindingContext` / `Content` / `Parent` unless you set `Compartmentalize`.
4. Old discussion names (`GCMonitorBehavior`, `AutoDisconnectBehavior`) are gone. Use `LeakMonitorBehavior` and `TearDownBehavior`.
5. Keep detection inside `#if DEBUG`. Teardown can stay in Release.
6. If you relied on V1 “whack the whole graph,” set:

```xml
mtk:TearDownBehavior.Strategy="Compartmentalize"
```

or:

```csharp
options.DefaultTearDownStrategy = TearDownStrategy.Compartmentalize;
```

---

## Checklist — new in V2

- [x] .NET 10 / MAUI 10 package
- [x] `UseMemoryToolkit` + `MemoryToolkitOptions`
- [x] `TearDownStrategy` (`DetectOnly`, `DisconnectHandlers`, `Compartmentalize`)
- [x] Per-view `TearDownBehavior.Strategy`
- [x] Default = low-destruction handler disconnect
- [x] Opt-in managed-graph containment
- [x] CollectionView / `ItemsView` teardown
- [x] Gesture, span, and MAUI Behavior clears
- [x] Safe disconnect (try/catch, Manual policy, Cascade islands)
- [x] Best-effort reference clearing
- [x] Shell / flyout / modal / stack lifecycle guards
- [x] XML docs in the nupkg
- [x] Unit tests
- [x] Mac Catalyst e2e + smoke scripts
- [x] Modernized CollectionView sample + UI-thread leak alerts
- [x] Obsolete V1 `UseLeakDetection` wrapper
