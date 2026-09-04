using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Default leak monitor: WeakReference plus N-pass forced GC.
/// This is a liveness test, not a roots finder.
/// </summary>
public sealed class GarbageCollectionMonitor : IGarbageCollectionMonitor
{
    /// <summary>
    /// Process-wide default monitor. Tests and <see cref="MauiAppBuilderExtensions.UseLeakAnalyser"/>
    /// may replace this instance.
    /// </summary>
    public static IGarbageCollectionMonitor Instance { get; set; } = new GarbageCollectionMonitor();

    /// <summary>
    /// Forced GC passes. Overridden from <see cref="LeakAnalyserOptions.MaxCollections"/> at startup.
    /// </summary>
    public int MaxCollections { get; set; } = 10;

    /// <summary>
    /// Delay between passes. Overridden from <see cref="LeakAnalyserOptions.MillisecondsBetweenCollections"/>.
    /// </summary>
    public int MillisecondsBetweenCollections { get; set; } = 200;

    /// <summary>
    /// Optional logger. Wired from the host <see cref="IServiceProvider"/> after <c>Build()</c>.
    /// </summary>
    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <inheritdoc />
    public Action<CollectionTarget>? OnLeaked { get; set; }

    /// <inheritdoc />
    public Action<CollectionTarget>? OnCollected { get; set; }

    /// <inheritdoc />
    public async Task MonitorAndForceCollectionAsync(IReadOnlyList<CollectionTarget> targets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            return;

        var remaining = new List<CollectionTarget>(targets);
        var passes = Math.Max(1, MaxCollections);
        var delay = Math.Max(0, MillisecondsBetweenCollections);

        for (var pass = 1; pass <= passes; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            for (var i = remaining.Count - 1; i >= 0; i--)
            {
                var target = remaining[i];
                if (!target.IsAlive)
                {
                    Logger.LogDebug("✅{Name} released", target.Name);
                    OnCollected?.Invoke(target);
                    remaining.RemoveAt(i);
                    continue;
                }

                if (pass == passes)
                {
                    Logger.LogWarning("❗🧟❗{Name} is a zombie", target.Name);
                    OnLeaked?.Invoke(target);
                    remaining.RemoveAt(i);
                }
            }

            if (remaining.Count == 0)
                return;

            if (pass < passes && delay > 0)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
