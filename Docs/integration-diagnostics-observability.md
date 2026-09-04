# Integrate LeakAnalyser with Diagnostics and Observability

`Plugin.Maui.LeakAnalyser` is standalone. It does not reference Diagnostics or Observability. Wire leak callbacks yourself when the host already uses those plugins.

Leak detection stays in Debug. Do not enable `LeakMonitor` or forced GC in Release just to feed telemetry.

## Diagnostics — breadcrumbs

[Plugin.Maui.Diagnostics](https://www.nuget.org/packages/Plugin.Maui.Diagnostics) records a pre-crash timeline. Treat a leaked view as a breadcrumb, not a crash.

```csharp
using Plugin.Maui.Diagnostics;
using Plugin.Maui.LeakAnalyser;

#if DEBUG
builder.Logging.AddDebug();
builder.UseMauiDiagnostics();
builder.UseLeakAnalyser(options =>
{
    options.OnLeaked = target =>
    {
        MauiDiagnostics.TrackEvent($"Leak:{target.Name}");
        MauiDiagnostics.TrackException(
            new InvalidOperationException($"{target.Name} survived forced GC."));
    };

    options.OnCollected = target =>
        MauiDiagnostics.TrackEvent($"Collected:{target.Name}");
});
#endif
```

Register Diagnostics first if you want its logger and timeline ready before the first leak callback.

Do **not** call `TrackException` for every collected target — that is success. Use `OnLeaked` only.

## Observability — export path

[Plugin.Maui.Observability](https://www.nuget.org/packages/Plugin.Maui.Observability) is an umbrella over AppHealth, Diagnostics, NetworkMonitor, and other siblings. It does not import leak events automatically.

If the host already calls `UseMauiObservability`, keep LeakAnalyser registered beside it and forward `OnLeaked` into Diagnostics as above. Observability will pick up those breadcrumbs on the next report export if Diagnostics is part of that pipeline.

```csharp
builder
    .UseMauiApp<App>()
    .UseMauiObservability(); // host already chose the umbrella

#if DEBUG
builder.UseLeakAnalyser(options =>
{
    options.OnLeaked = target =>
        MauiDiagnostics.TrackEvent($"Leak:{target.Name}");
});
#endif
```

Do not add Observability only to see leaks. Install LeakAnalyser (and optionally Diagnostics) instead.

## What not to wire

| Signal | Why not |
| --- | --- |
| Forced GC counts as health | `GC.Collect` is a Debug probe, not a production metric |
| Every `OnCollected` as an error | Collection is the healthy outcome |
| Release `LeakMonitor.Cascade` | Expensive and can hide real leaks behind GC noise |
| Package reference from LeakAnalyser → Diagnostics | Keeps this plugin usable without the telemetry suite |

## Related

- LeakAnalyser README — detection and teardown
- [Plugin.Maui.Diagnostics](https://github.com/nuvyntralabs/Plugin.Maui.Diagnostics) — crash / ANR / breadcrumbs
- [Plugin.Maui.Performance](https://github.com/nuvyntralabs/Plugin.Maui.Performance) — timings, not liveness
- [Plugin.Maui.Observability](https://github.com/nuvyntralabs/Plugin.Maui.Observability) — unified export, not leak detection
