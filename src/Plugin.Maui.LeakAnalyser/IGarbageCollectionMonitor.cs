namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Forces garbage collection and reports whether watched targets survived.
/// </summary>
public interface IGarbageCollectionMonitor
{
    /// <summary>
    /// Called when a target is still alive after the last collection pass.
    /// </summary>
    Action<CollectionTarget>? OnLeaked { get; set; }

    /// <summary>
    /// Called when a target is collected before the last pass.
    /// </summary>
    Action<CollectionTarget>? OnCollected { get; set; }

    /// <summary>
    /// Watch <paramref name="targets"/> across forced GC passes.
    /// </summary>
    Task MonitorAndForceCollectionAsync(IReadOnlyList<CollectionTarget> targets, CancellationToken cancellationToken = default);
}
