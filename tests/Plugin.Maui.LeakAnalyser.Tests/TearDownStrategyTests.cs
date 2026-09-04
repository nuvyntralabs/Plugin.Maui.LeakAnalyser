namespace Plugin.Maui.LeakAnalyser.Tests;

public sealed class TearDownStrategyTests : IDisposable
{
    public TearDownStrategyTests() => LeakAnalyserConfiguration.Reset();

    public void Dispose() => LeakAnalyserConfiguration.Reset();

    [Fact]
    public void DetectOnly_leaves_the_graph_intact()
    {
        var label = new Label { BindingContext = "vm" };
        var page = new ContentPage { Content = label, BindingContext = "page" };

        page.TearDown(TearDownStrategy.DetectOnly);

        Assert.Same(label, page.Content);
        Assert.Equal("vm", label.BindingContext);
        Assert.Equal("page", page.BindingContext);
    }

    [Fact]
    public void DisconnectHandlers_does_not_clear_managed_references()
    {
        var tap = new TapGestureRecognizer();
        var label = new Label { BindingContext = "vm" };
        label.GestureRecognizers.Add(tap);
        var page = new ContentPage { Content = label };

        page.TearDown(TearDownStrategy.DisconnectHandlers);

        Assert.Same(label, page.Content);
        Assert.Equal("vm", label.BindingContext);
        Assert.Single(label.GestureRecognizers);
    }

    [Fact]
    public void Compartmentalize_clears_known_managed_references()
    {
        var tap = new TapGestureRecognizer();
        var label = new Label
        {
            BindingContext = "vm",
            FormattedText = new FormattedString { Spans = { new Span { Text = "hi" } } }
        };
        label.GestureRecognizers.Add(tap);
        label.Behaviors.Add(new EmptyBehavior());

        var page = new ContentPage { Content = label, BindingContext = "page" };

        page.TearDown(TearDownStrategy.Compartmentalize);

        Assert.Null(page.Content);
        Assert.Null(page.BindingContext);
        Assert.Null(label.BindingContext);
        Assert.Empty(label.GestureRecognizers);
        Assert.Empty(label.Behaviors);
        Assert.Null(label.FormattedText);
        Assert.Null(label.Parent);
    }

    [Fact]
    public void Compartmentalize_clears_items_view_source_and_template()
    {
        var list = new CollectionView
        {
            ItemsSource = new[] { 1, 2, 3 },
            ItemTemplate = new DataTemplate(() => new Label())
        };

        list.TearDown(TearDownStrategy.Compartmentalize);

        Assert.Null(list.ItemsSource);
        Assert.Null(list.ItemTemplate);
    }

    [Fact]
    public void OnTearDown_runs_only_for_compartmentalize_when_handler_exists()
    {
        var called = 0;
        TearDown.OnTearDown = _ => called++;

        var page = new ContentPage { Handler = new StubHandler() };
        page.TearDown(TearDownStrategy.DetectOnly);
        page.TearDown(TearDownStrategy.DisconnectHandlers);
        Assert.Equal(0, called);

        page.Handler = new StubHandler();
        page.TearDown(TearDownStrategy.Compartmentalize);
        Assert.Equal(1, called);
    }

    [Fact]
    public void Disconnect_exceptions_do_not_abort_siblings()
    {
        var good = new Label { Handler = new StubHandler() };
        var bad = new Label { Handler = new ThrowingHandler() };
        var page = new ContentPage
        {
            Content = new VerticalStackLayout { Children = { bad, good } }
        };

        page.TearDown(TearDownStrategy.DisconnectHandlers);

        Assert.True(((StubHandler)good.Handler!).Disconnected);
    }

    [Fact]
    public void Manual_disconnect_policy_is_honored()
    {
        var label = new Label { Handler = new StubHandler() };
        HandlerProperties.SetDisconnectPolicy(label, HandlerDisconnectPolicy.Manual);

        label.TearDown(TearDownStrategy.DisconnectHandlers);

        Assert.False(((StubHandler)label.Handler!).Disconnected);
    }

    [Fact]
    public void Child_suppress_skips_that_subtree()
    {
        var suppressed = new Label { BindingContext = "keep" };
        TearDown.SetSuppress(suppressed, true);
        var page = new ContentPage { Content = suppressed };

        page.TearDown(TearDownStrategy.Compartmentalize);

        Assert.Equal("keep", suppressed.BindingContext);
    }

    [Fact]
    public void Child_cascade_is_a_teardown_island()
    {
        var child = new ContentView { BindingContext = "island", Content = new Label { BindingContext = "inner" } };
        TearDown.SetCascade(child, true);
        var page = new ContentPage { Content = child };

        page.TearDown(TearDownStrategy.Compartmentalize);

        Assert.Equal("island", child.BindingContext);
        Assert.NotNull(child.Content);
    }

    [Fact]
    public void Unset_strategy_uses_options_default()
    {
        LeakAnalyserConfiguration.Options = new LeakAnalyserOptions
        {
            DefaultTearDownStrategy = TearDownStrategy.DetectOnly
        };

        var page = new ContentPage { Content = new Label() };
        page.TearDown();

        Assert.NotNull(page.Content);
    }

    [Fact]
    public void Per_view_strategy_overrides_default()
    {
        LeakAnalyserConfiguration.Options = new LeakAnalyserOptions
        {
            DefaultTearDownStrategy = TearDownStrategy.DetectOnly
        };

        var page = new ContentPage { Content = new Label() };
        TearDown.SetStrategy(page, TearDownStrategy.Compartmentalize);
        page.TearDown();

        Assert.Null(page.Content);
    }

    [Fact]
    public void Content_clear_failure_does_not_abort_handler_disconnect()
    {
        var page = new ThrowingContentPage { Handler = new StubHandler() };

        page.TearDown(TearDownStrategy.Compartmentalize);

        Assert.True(((StubHandler)page.Handler!).Disconnected);
    }

    private sealed class EmptyBehavior : Behavior<View>;

    private sealed class ThrowingContentPage : ContentPage
    {
        protected override void OnPropertyChanged(string? propertyName = null)
        {
            if (propertyName == ContentProperty.PropertyName && Content is null)
                throw new InvalidOperationException("content rejected");

            base.OnPropertyChanged(propertyName);
        }
    }
}
