using Microsoft.Extensions.Logging;

namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Applies options to the process monitor using the real host service provider.
/// Avoids building a temporary <see cref="IServiceProvider"/> during <c>UseLeakAnalyser</c>.
/// </summary>
internal sealed class LeakAnalyserInitializer : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        var options = services.GetService<LeakAnalyserOptions>() ?? LeakAnalyserConfiguration.Options;
        LeakAnalyserConfiguration.Options = options;

        var monitor = options.CustomMonitor ?? GarbageCollectionMonitor.Instance;
        if (monitor is GarbageCollectionMonitor garbage)
        {
            garbage.MaxCollections = options.MaxCollections;
            garbage.MillisecondsBetweenCollections = options.MillisecondsBetweenCollections;
            garbage.OnLeaked = options.OnLeaked;
            garbage.OnCollected = options.OnCollected;
            var logger = services.GetService<ILogger<GarbageCollectionMonitor>>();
            if (logger is not null)
                garbage.Logger = logger;
        }
        else
        {
            monitor.OnLeaked = options.OnLeaked;
            monitor.OnCollected = options.OnCollected;
        }

        GarbageCollectionMonitor.Instance = monitor;
        LeakAnalyserConfiguration.Monitor = monitor;
    }
}
