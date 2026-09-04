namespace Plugin.Maui.LeakAnalyser.Tests;

public sealed class OptionsTests : IDisposable
{
    public OptionsTests() => LeakAnalyserConfiguration.Reset();

    public void Dispose() => LeakAnalyserConfiguration.Reset();

    [Fact]
    public void Defaults_match_low_destruction_v2_surface()
    {
        var options = new LeakAnalyserOptions();

        Assert.Equal(TearDownStrategy.DisconnectHandlers, options.DefaultTearDownStrategy);
        Assert.Null(options.OnLeaked);
        Assert.Null(options.OnCollected);
        Assert.Null(options.CustomMonitor);
        Assert.Equal(10, options.MaxCollections);
        Assert.Equal(200, options.MillisecondsBetweenCollections);
    }

    [Fact]
    public void TearDownStrategy_has_three_values()
    {
        Assert.Equal(new[]
        {
            TearDownStrategy.DetectOnly,
            TearDownStrategy.DisconnectHandlers,
            TearDownStrategy.Compartmentalize
        }, Enum.GetValues<TearDownStrategy>());
    }

    [Fact]
    public void UseLeakAnalyser_stores_options_without_building_a_service_provider()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseLeakAnalyser(options =>
        {
            options.DefaultTearDownStrategy = TearDownStrategy.DetectOnly;
            options.MaxCollections = 3;
        });

        Assert.Equal(TearDownStrategy.DetectOnly, LeakAnalyserConfiguration.Options.DefaultTearDownStrategy);
        Assert.Equal(3, LeakAnalyserConfiguration.Options.MaxCollections);
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IMauiInitializeService));
    }
}
