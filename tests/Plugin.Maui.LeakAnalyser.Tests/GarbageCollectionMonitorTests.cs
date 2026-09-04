using System.Runtime.CompilerServices;

namespace Plugin.Maui.LeakAnalyser.Tests;

public sealed class GarbageCollectionMonitorTests : IDisposable
{
    public GarbageCollectionMonitorTests() => LeakAnalyserConfiguration.Reset();

    public void Dispose() => LeakAnalyserConfiguration.Reset();

    [Fact]
    public async Task Reports_collected_when_nothing_roots_the_target()
    {
        var collected = new List<string>();
        var leaked = new List<string>();
        var monitor = new GarbageCollectionMonitor
        {
            MaxCollections = 2,
            MillisecondsBetweenCollections = 0,
            OnCollected = t => collected.Add(t.Name),
            OnLeaked = t => leaked.Add(t.Name)
        };

        var targets = new List<CollectionTarget> { CreateUnrooted("ephemeral") };
        await monitor.MonitorAndForceCollectionAsync(targets);

        Assert.Contains("ephemeral", collected);
        Assert.Empty(leaked);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static CollectionTarget CreateUnrooted(string name) => new(new object(), name);

    [Fact]
    public async Task Reports_leaked_when_a_strong_root_remains()
    {
        var leaked = new List<string>();
        var monitor = new GarbageCollectionMonitor
        {
            MaxCollections = 2,
            MillisecondsBetweenCollections = 0,
            OnLeaked = t => leaked.Add(t.Name)
        };

        var rooted = new object();
        await monitor.MonitorAndForceCollectionAsync([new CollectionTarget(rooted, "rooted")]);

        Assert.Contains("rooted", leaked);
        GC.KeepAlive(rooted);
    }

    [Fact]
    public void CollectionTarget_defaults_name_to_type()
    {
        var target = new CollectionTarget(new Label());
        Assert.Equal(nameof(Label), target.Name);
        Assert.Equal(typeof(Label).FullName, target.ObjectType);
        Assert.Null(target.Screen);
        Assert.True(target.IsAlive);
    }

    [Fact]
    public void Monitor_walk_skips_suppressed_and_nested_cascade_islands()
    {
        var nested = new Label();
        LeakMonitor.SetCascade(nested, true);
        LeakMonitor.SetName(nested, "nested");

        var suppressed = new Label();
        LeakMonitor.SetSuppress(suppressed, true);
        LeakMonitor.SetName(suppressed, "suppressed");

        var watched = new Label();
        LeakMonitor.SetName(watched, "watched");

        var page = new ContentPage
        {
            Content = new VerticalStackLayout { Children = { nested, suppressed, watched } }
        };
        LeakMonitor.SetName(page, "page");

        var targets = new List<CollectionTarget>();
        LeakGraph.CollectTargets(page, isRoot: true, targets);
        var names = targets.Select(t => t.Name).ToArray();

        Assert.Contains("page", names);
        Assert.Contains("watched", names);
        Assert.DoesNotContain("nested", names);
        Assert.DoesNotContain("suppressed", names);
        Assert.Equal("page", targets.Single(t => t.Name == "watched").Screen);
        Assert.Equal(typeof(Label).FullName, targets.Single(t => t.Name == "watched").ObjectType);
    }

    [Fact]
    public void Monitor_includes_handler_as_a_second_target()
    {
        var label = new Label { Handler = new StubHandler() };
        LeakMonitor.SetName(label, "photo");

        var targets = new List<CollectionTarget>();
        LeakGraph.CollectTargets(label, isRoot: true, targets);

        Assert.Equal(2, targets.Count);
        Assert.Equal("photo", targets[0].Name);
        Assert.Equal("photo Handler", targets[1].Name);
    }
}
