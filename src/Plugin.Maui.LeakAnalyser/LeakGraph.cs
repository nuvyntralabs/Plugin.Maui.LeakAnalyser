using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TearDownHooks = Plugin.Maui.LeakAnalyser.TearDown;

namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Manual leak monitoring, teardown, and parent walks.
/// </summary>
public static class LeakGraph
{
    /// <summary>
    /// Walks <paramref name="element"/> and its parents and returns the first match of <typeparamref name="T"/>.
    /// </summary>
    public static T? GetFirstSelfOrParentOfType<T>(Element? element)
        where T : class
    {
        for (Element? current = element; current is not null; current = current.Parent)
        {
            if (current is T match)
                return match;
        }

        return null;
    }

    /// <summary>
    /// Snapshot the visual tree (and handlers) into weak targets, then force GC.
    /// Do not call this in Release builds.
    /// </summary>
    public static void Monitor(this object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var collected = new List<CollectionTarget>();
        CollectTargets(target, isRoot: true, collected);
        _ = LeakAnalyserConfiguration.Monitor.MonitorAndForceCollectionAsync(collected);
    }

    /// <summary>
    /// Tear down using <see cref="LeakAnalyserOptions.DefaultTearDownStrategy"/>.
    /// </summary>
    public static void TearDown(this IVisualTreeElement element)
        => element.TearDown(ResolveStrategy(element));

    /// <summary>
    /// Tear down using an explicit strategy.
    /// </summary>
    public static void TearDown(this IVisualTreeElement element, TearDownStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(element);

        switch (strategy)
        {
            case TearDownStrategy.DetectOnly:
                return;
            case TearDownStrategy.DisconnectHandlers:
                DisconnectHandlers(element, isRoot: true);
                return;
            case TearDownStrategy.Compartmentalize:
                Compartmentalize(element, isRoot: true);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null);
        }
    }

    internal static TearDownStrategy ResolveStrategy(IVisualTreeElement element)
    {
        if (element is BindableObject bindable)
        {
            var overrideStrategy = TearDownHooks.GetStrategy(bindable);
            if (overrideStrategy.HasValue)
                return overrideStrategy.Value;
        }

        return LeakAnalyserConfiguration.Options.DefaultTearDownStrategy;
    }

    internal static void CollectTargets(object target, bool isRoot, List<CollectionTarget> collected)
    {
        if (target is BindableObject bindable)
        {
            if (!isRoot && LeakMonitor.GetSuppress(bindable))
                return;

            if (!isRoot && LeakMonitor.GetCascade(bindable))
                return;
        }

        if (target is IVisualTreeElement visual)
        {
            var name = target is BindableObject named ? LeakMonitor.GetName(named) : null;
            var screen = target is Element screenElement ? ScreenOf(screenElement) : null;
            collected.Add(new CollectionTarget(visual, name, screen));

            if (target is Element element && element.Handler is not null)
            {
                var handlerName = string.IsNullOrWhiteSpace(name)
                    ? $"{element.GetType().Name} Handler"
                    : $"{name} Handler";
                collected.Add(new CollectionTarget(element.Handler, handlerName, screen));
            }

            foreach (var child in visual.GetVisualChildren())
                CollectTargets(child, isRoot: false, collected);

            return;
        }

        collected.Add(new CollectionTarget(target));
    }

    internal static string? ScreenOf(Element element)
    {
        var page = element as Page ?? GetFirstSelfOrParentOfType<Page>(element);
        if (page is null)
            return null;

        var named = LeakMonitor.GetName(page);
        return string.IsNullOrWhiteSpace(named) ? page.GetType().Name : named;
    }

    internal static void DisconnectHandlers(IVisualTreeElement element, bool isRoot)
    {
        foreach (var child in SnapshotChildren(element))
            DisconnectHandlers(child, isRoot: false);

        if (!ShouldDisconnect(element, isRoot))
            return;

        SafeDisconnect(element);
    }

    internal static void Compartmentalize(IVisualTreeElement element, bool isRoot)
    {
        if (element is BindableObject bindable)
        {
            if (TearDownHooks.GetSuppress(bindable))
                return;

            if (!isRoot && TearDownHooks.GetCascade(bindable))
                return;
        }

        foreach (var child in SnapshotChildren(element))
            Compartmentalize(child, isRoot: false);

        ClearMauiReferences(element);

        if (element is VisualElement visual && visual.Handler is not null)
        {
            Try("OnTearDown", () => TearDownHooks.OnTearDown?.Invoke(visual));
            SafeDisconnect(visual);
        }

        Try("Resources", () =>
        {
            if (element is VisualElement ve)
                ve.Resources = null;
        });
    }

    internal static void ClearMauiReferences(IVisualTreeElement element)
    {
        Try("GestureRecognizers", () =>
        {
            if (element is View view)
                view.GestureRecognizers.Clear();
        });

        Try("FormattedText", () => ClearFormattedText(element));

        Try("ItemsView", () =>
        {
            if (element is ItemsView items)
            {
                items.ItemsSource = null;
                items.ItemTemplate = null;
            }
        });

#pragma warning disable CS0618
        Try("ListView", () =>
        {
            if (element is ListView list)
            {
                list.ItemsSource = null;
                list.ItemTemplate = null;
            }
        });
#pragma warning restore CS0618

        Try("Content", () =>
        {
            switch (element)
            {
                case ContentView contentView:
                    contentView.Content = null;
                    break;
                case Border border:
                    border.Content = null;
                    break;
                case ContentPage contentPage:
                    contentPage.Content = null;
                    break;
                case ScrollView scrollView:
                    scrollView.Content = null;
                    break;
            }
        });

        Try("Behaviors", () =>
        {
            if (element is VisualElement visual)
                visual.Behaviors.Clear();
        });

        Try("ElementGraph", () =>
        {
            if (element is Element el)
            {
                el.BindingContext = null;
                el.ClearLogicalChildren();
                el.Parent = null;
            }
        });
    }

    private static void ClearFormattedText(IVisualTreeElement element)
    {
        if (element is not Label label || label.FormattedText is null)
            return;

        foreach (var span in label.FormattedText.Spans)
            span.GestureRecognizers.Clear();

        label.FormattedText.Spans.Clear();
        label.FormattedText = null;
    }

    private static bool ShouldDisconnect(IVisualTreeElement element, bool isRoot)
    {
        if (element is not BindableObject bindable)
            return element is IView { Handler: not null };

        if (HandlerProperties.GetDisconnectPolicy(bindable) == HandlerDisconnectPolicy.Manual)
            return false;

        if (TearDownHooks.GetSuppress(bindable))
            return false;

        if (!isRoot && TearDownHooks.GetCascade(bindable))
            return false;

        return element is IView { Handler: not null };
    }

    private static void SafeDisconnect(IVisualTreeElement element)
    {
        if (element is not IView view || view.Handler is null)
            return;

        Try("DisconnectHandler", () => view.Handler.DisconnectHandler());
    }

    private static IReadOnlyList<IVisualTreeElement> SnapshotChildren(IVisualTreeElement element)
    {
        var children = element.GetVisualChildren();
        return children as IReadOnlyList<IVisualTreeElement> ?? children.ToList();
    }

    private static void Try(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var logger = (GarbageCollectionMonitor.Instance as GarbageCollectionMonitor)?.Logger
                ?? (ILogger)NullLogger.Instance;
            logger.LogWarning(exception, "LeakAnalyser {Operation} failed; continuing.", operation);
        }
    }
}
