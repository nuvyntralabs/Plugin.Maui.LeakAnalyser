namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Global defaults for <see cref="MauiAppBuilderExtensions.UseLeakAnalyser"/>.
/// </summary>
public sealed class LeakAnalyserOptions
{
    /// <summary>
    /// Strategy used when <see cref="TearDown.StrategyProperty"/> is unset. Default is
    /// <see cref="TearDownStrategy.DisconnectHandlers"/>.
    /// </summary>
    public TearDownStrategy DefaultTearDownStrategy { get; set; } = TearDownStrategy.DisconnectHandlers;

    /// <summary>
    /// Invoked when a watched target is still alive after forced collections.
    /// Detection is intended for Debug only.
    /// </summary>
    public Action<CollectionTarget>? OnLeaked { get; set; }

    /// <summary>
    /// Invoked when a watched target is collected.
    /// </summary>
    public Action<CollectionTarget>? OnCollected { get; set; }

    /// <summary>
    /// Replace the default <see cref="GarbageCollectionMonitor"/>.
    /// </summary>
    public IGarbageCollectionMonitor? CustomMonitor { get; set; }

    /// <summary>
    /// Forced GC passes before a still-alive target is reported as leaked. Default is 10.
    /// </summary>
    public int MaxCollections { get; set; } = 10;

    /// <summary>
    /// Delay between collection passes, in milliseconds. Default is 200.
    /// </summary>
    public int MillisecondsBetweenCollections { get; set; } = 200;
}
