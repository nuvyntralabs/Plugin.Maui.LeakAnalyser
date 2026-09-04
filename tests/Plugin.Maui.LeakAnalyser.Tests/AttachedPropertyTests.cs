namespace Plugin.Maui.LeakAnalyser.Tests;

public sealed class AttachedPropertyTests : IDisposable
{
    public AttachedPropertyTests() => LeakAnalyserConfiguration.Reset();

    public void Dispose() => LeakAnalyserConfiguration.Reset();

    [Fact]
    public void LeakMonitor_round_trips_cascade_suppress_and_name()
    {
        var page = new ContentPage();

        Assert.False(LeakMonitor.GetCascade(page));
        Assert.False(LeakMonitor.GetSuppress(page));
        Assert.Null(LeakMonitor.GetName(page));

        LeakMonitor.SetCascade(page, true);
        LeakMonitor.SetSuppress(page, true);
        LeakMonitor.SetName(page, "Home");

        Assert.True(LeakMonitor.GetCascade(page));
        Assert.True(LeakMonitor.GetSuppress(page));
        Assert.Equal("Home", LeakMonitor.GetName(page));
    }

    [Fact]
    public void TearDown_round_trips_cascade_suppress_and_strategy()
    {
        var page = new ContentPage();

        Assert.False(TearDown.GetCascade(page));
        Assert.False(TearDown.GetSuppress(page));
        Assert.Null(TearDown.GetStrategy(page));

        TearDown.SetCascade(page, true);
        TearDown.SetSuppress(page, true);
        TearDown.SetStrategy(page, TearDownStrategy.Compartmentalize);

        Assert.True(TearDown.GetCascade(page));
        Assert.True(TearDown.GetSuppress(page));
        Assert.Equal(TearDownStrategy.Compartmentalize, TearDown.GetStrategy(page));
    }

    [Fact]
    public void LeakMonitor_cascade_rejects_non_visual_element()
    {
        var bindable = new NonVisualBindable();
        Assert.Throws<InvalidOperationException>(() => LeakMonitor.SetCascade(bindable, true));
    }

    [Fact]
    public void TearDown_cascade_rejects_non_visual_element()
    {
        var bindable = new NonVisualBindable();
        Assert.Throws<InvalidOperationException>(() => TearDown.SetCascade(bindable, true));
    }

    [Fact]
    public void Suppress_is_allowed_on_non_visual_bindable()
    {
        var bindable = new NonVisualBindable();
        LeakMonitor.SetSuppress(bindable, true);
        TearDown.SetSuppress(bindable, true);
        Assert.True(LeakMonitor.GetSuppress(bindable));
        Assert.True(TearDown.GetSuppress(bindable));
    }

    private sealed class NonVisualBindable : BindableObject;
}
