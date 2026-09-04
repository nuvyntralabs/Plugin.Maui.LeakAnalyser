namespace Plugin.Maui.LeakAnalyser.Tests;

public sealed class LifecycleTests : IDisposable
{
    public LifecycleTests()
    {
        LeakAnalyserConfiguration.Reset();
        ElementLifecycleTracker.FallbackDelay = TimeSpan.Zero;
    }

    public void Dispose()
    {
        ElementLifecycleTracker.FallbackDelay = TimeSpan.FromMilliseconds(100);
        LeakAnalyserConfiguration.Reset();
    }

    [Fact]
    public async Task No_host_page_runs_immediately()
    {
        var ran = false;
        var view = new ContentView();

        await ElementLifecycleTracker.RunWhenDoneAsync(
            view,
            "test",
            _ => false,
            _ => ran = true);

        Assert.True(ran);
    }

    [Fact]
    public async Task Suppress_does_not_run()
    {
        var ran = false;
        var view = new ContentView();

        await ElementLifecycleTracker.RunWhenDoneAsync(
            view,
            "test",
            _ => true,
            _ => ran = true);

        Assert.False(ran);
    }

    [Fact]
    public async Task NavigationPage_container_is_ignored()
    {
        var ran = false;
        var nav = new NavigationPage(new ContentPage());

        await ElementLifecycleTracker.RunWhenDoneAsync(
            nav,
            "test",
            _ => false,
            _ => ran = true);

        Assert.False(ran);
    }

    [Fact]
    public async Task Unloaded_navigation_page_runs_unless_suppressed()
    {
        var ran = false;
        var page = new ContentPage();
        var nav = new NavigationPage(page);
        // NavigationPage starts unloaded in unit tests (no window).

        await ElementLifecycleTracker.RunWhenDoneAsync(
            page,
            "test",
            _ => false,
            _ => ran = true);

        Assert.True(ran);
        Assert.Same(nav, LeakGraph.GetFirstSelfOrParentOfType<NavigationPage>(page));
    }

    [Fact]
    public async Task Unloaded_navigation_page_honors_suppress_on_the_nav()
    {
        var ran = false;
        var page = new ContentPage();
        var nav = new NavigationPage(page);
        LeakMonitor.SetSuppress(nav, true);

        await ElementLifecycleTracker.RunWhenDoneAsync(
            page,
            "test",
            candidate => LeakMonitor.GetSuppress(candidate),
            _ => ran = true);

        Assert.False(ran);
    }

    [Fact]
    public async Task Modal_stack_skips_teardown()
    {
        var ran = false;
        var root = new ContentPage();
        var nav = new NavigationPage(root);
        await nav.Navigation.PushModalAsync(new ContentPage());

        await ElementLifecycleTracker.RunWhenDoneAsync(
            root,
            "test",
            _ => false,
            _ => ran = true);

        Assert.False(ran);
    }

    [Fact]
    public void GetFirstSelfOrParentOfType_walks_parents()
    {
        var label = new Label();
        var page = new ContentPage { Content = label };

        Assert.Same(page, LeakGraph.GetFirstSelfOrParentOfType<ContentPage>(label));
        Assert.Same(page, LeakGraph.GetFirstSelfOrParentOfType<Page>(label));
        Assert.Null(LeakGraph.GetFirstSelfOrParentOfType<NavigationPage>(label));
    }
}
