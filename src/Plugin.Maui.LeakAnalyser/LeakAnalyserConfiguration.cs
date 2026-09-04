namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Process-wide options and monitor used by attached properties before or after DI startup.
/// </summary>
internal static class LeakAnalyserConfiguration
{
    private static readonly LeakAnalyserOptions Fallback = new();
    private static LeakAnalyserOptions? _options;

    public static LeakAnalyserOptions Options
    {
        get => _options ?? Fallback;
        set => _options = value;
    }

    public static IGarbageCollectionMonitor Monitor { get; set; } = GarbageCollectionMonitor.Instance;

    public static void Reset()
    {
        _options = null;
        var monitor = new GarbageCollectionMonitor();
        GarbageCollectionMonitor.Instance = monitor;
        Monitor = monitor;
        TearDown.OnTearDown = null;
        ElementLifecycleTracker.Reset();
    }
}
