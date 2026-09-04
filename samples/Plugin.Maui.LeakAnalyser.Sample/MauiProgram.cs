using Microsoft.Extensions.Logging;
using Plugin.Maui.LeakAnalyser;

namespace Plugin.Maui.LeakAnalyser.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLeakAnalyser(options =>
            {
                options.DefaultTearDownStrategy = TearDownStrategy.DisconnectHandlers;
                options.OnLeaked = LeakLog.Add;
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
