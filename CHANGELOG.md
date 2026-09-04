# Changelog

## 1.0.0

- First stable release (install without `--prerelease`)
- Visual-tree leak detection (WeakReference + forced GC) in Debug
- Handler disconnect and optional compartmentalize teardown
- `UseLeakAnalyser`, `LeakMonitor`, `TearDown`, `LeakGraph`
- Android, iOS, Mac Catalyst, and Windows (`net10.0-windows` packed on Windows)

## 0.1.1-preview

- Include `net10.0-windows` in the published nupkg so NuGet lists Windows as a packed TFM

## 0.1.0-preview

- Initial preview with sample leak-details UI
