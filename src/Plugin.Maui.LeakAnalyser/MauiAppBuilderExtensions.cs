namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// MAUI host registration for leak detection defaults and the GC monitor.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="LeakAnalyserOptions"/> and wires the GC monitor from the host
    /// <see cref="IServiceProvider"/> after <c>Build()</c>.
    /// Required for leak callbacks and global teardown defaults. <see cref="TearDown"/> still
    /// works without it (built-in defaults apply).
    /// </summary>
    /// <example>
    /// <code>
    /// #if DEBUG
    /// builder.Logging.AddDebug();
    /// builder.UseLeakAnalyser(options =>
    /// {
    ///     options.OnLeaked = target => { /* alert / counter */ };
    ///     options.DefaultTearDownStrategy = TearDownStrategy.DetectOnly;
    /// });
    /// #endif
    /// </code>
    /// </example>
    public static MauiAppBuilder UseLeakAnalyser(this MauiAppBuilder builder, Action<LeakAnalyserOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new LeakAnalyserOptions();
        configure?.Invoke(options);
        LeakAnalyserConfiguration.Options = options;
        builder.Services.AddSingleton(options);
        builder.Services.AddTransient<IMauiInitializeService, LeakAnalyserInitializer>();
        return builder;
    }
}
