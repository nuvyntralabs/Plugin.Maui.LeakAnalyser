namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Best-effort "this view is finished" inference shared by <see cref="LeakMonitor"/> and <see cref="TearDown"/>.
/// Not a navigation framework. Shell tabs, flyouts, popups, and cached pages still need Suppress or a manual call.
/// </summary>
internal static class ElementLifecycleTracker
{
    private static readonly List<TrackedAction> Tracked = [];
    private static readonly object Gate = new();

    internal static TimeSpan FallbackDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    internal static void Reset()
    {
        lock (Gate)
        {
            foreach (var tracked in Tracked)
                tracked.Detach();
            Tracked.Clear();
        }
    }

    internal static void RunWhenDone(
        VisualElement element,
        string actionKey,
        Func<VisualElement, bool> isSuppressed,
        Action<VisualElement> onDone)
    {
        _ = RunWhenDoneAsync(element, actionKey, isSuppressed, onDone);
    }

    internal static async Task RunWhenDoneAsync(
        VisualElement element,
        string actionKey,
        Func<VisualElement, bool> isSuppressed,
        Action<VisualElement> onDone)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(actionKey);
        ArgumentNullException.ThrowIfNull(isSuppressed);
        ArgumentNullException.ThrowIfNull(onDone);

        if (isSuppressed(element) || element is NavigationPage)
            return;

        var hostPage = LeakGraph.GetFirstSelfOrParentOfType<Page>(element);
        if (hostPage is null)
        {
            onDone(element);
            return;
        }

        var navigation = LeakGraph.GetFirstSelfOrParentOfType<NavigationPage>(element);
        if (navigation is not null)
        {
            if (navigation.Navigation.ModalStack.Count > 0)
                return;

            if (!navigation.IsLoaded)
            {
                if (isSuppressed(navigation))
                    return;

                onDone(element);
                return;
            }

            TrackUntilPopped(element, hostPage, navigation, actionKey, onDone);
            return;
        }

        var delay = FallbackDelay;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay).ConfigureAwait(true);

        if (isSuppressed(element))
            return;

        if (IsStillInNavigationOrModalStack(hostPage))
            return;

        if (LeakGraph.GetFirstSelfOrParentOfType<Shell>(element) is not null)
            return;

        if (LeakGraph.GetFirstSelfOrParentOfType<Tab>(element) is not null)
            return;

        onDone(element);
    }

    private static void TrackUntilPopped(
        VisualElement element,
        Page hostPage,
        NavigationPage navigation,
        string actionKey,
        Action<VisualElement> onDone)
    {
        lock (Gate)
        {
            PruneLocked();

            foreach (var existing in Tracked)
            {
                if (existing.Matches(element, actionKey))
                    return;
            }

            var tracked = new TrackedAction(element, hostPage, navigation, actionKey, onDone);
            tracked.Attach();
            Tracked.Add(tracked);
        }
    }

    private static void PruneLocked()
    {
        for (var i = Tracked.Count - 1; i >= 0; i--)
        {
            if (!Tracked[i].IsAlive)
            {
                Tracked[i].Detach();
                Tracked.RemoveAt(i);
            }
        }
    }

    private static bool IsStillInNavigationOrModalStack(Page hostPage)
    {
        if (IsInStacks(hostPage, LeakGraph.GetFirstSelfOrParentOfType<NavigationPage>(hostPage)))
            return true;

        var app = Application.Current;
        if (app is null)
            return false;

        foreach (var window in app.Windows)
        {
            if (window.Page is not null && ContainsPageInNavigation(window.Page, hostPage))
                return true;
        }

        return false;
    }

    private static bool ContainsPageInNavigation(Page root, Page hostPage)
    {
        if (IsInStacks(hostPage, root as NavigationPage))
            return true;

        switch (root)
        {
            case Shell shell:
                if (IsInNavigation(hostPage, shell.Navigation))
                    return true;

                foreach (var item in shell.Items)
                {
                    foreach (var section in item.Items)
                    {
                        foreach (var content in section.Items)
                        {
                            if (content.Content is Page page && ContainsPageInNavigation(page, hostPage))
                                return true;
                        }
                    }
                }

                return false;
            case FlyoutPage flyout:
                return (flyout.Detail is Page detail && ContainsPageInNavigation(detail, hostPage))
                    || (flyout.Flyout is Page flyoutPage && ContainsPageInNavigation(flyoutPage, hostPage));
            case TabbedPage tabs:
                foreach (var child in tabs.Children)
                {
                    if (child is Page page && ContainsPageInNavigation(page, hostPage))
                        return true;
                }

                break;
        }

        return false;
    }

    private static bool IsInStacks(Page hostPage, NavigationPage? navigation)
    {
        return navigation is not null && IsInNavigation(hostPage, navigation.Navigation);
    }

    private static bool IsInNavigation(Page hostPage, INavigation? navigation)
    {
        if (navigation is null)
            return false;

        foreach (var page in navigation.NavigationStack)
        {
            if (ReferenceEquals(page, hostPage))
                return true;
        }

        foreach (var page in navigation.ModalStack)
        {
            if (ReferenceEquals(page, hostPage))
                return true;
        }

        return false;
    }

    private sealed class TrackedAction
    {
        private readonly WeakReference<VisualElement> _element;
        private readonly WeakReference<Page> _hostPage;
        private readonly WeakReference<NavigationPage> _navigation;
        private readonly Action<VisualElement> _onDone;
        private bool _detached;

        public TrackedAction(
            VisualElement element,
            Page hostPage,
            NavigationPage navigation,
            string actionKey,
            Action<VisualElement> onDone)
        {
            _element = new WeakReference<VisualElement>(element);
            _hostPage = new WeakReference<Page>(hostPage);
            _navigation = new WeakReference<NavigationPage>(navigation);
            ActionKey = actionKey;
            _onDone = onDone;
        }

        public string ActionKey { get; }

        public bool IsAlive =>
            _element.TryGetTarget(out _) && _hostPage.TryGetTarget(out _) && _navigation.TryGetTarget(out _);

        public bool Matches(VisualElement element, string actionKey)
            => ActionKey == actionKey && _element.TryGetTarget(out var target) && ReferenceEquals(target, element);

        public void Attach()
        {
            if (_navigation.TryGetTarget(out var navigation))
                navigation.Popped += OnPopped;
        }

        public void Detach()
        {
            if (_detached)
                return;

            _detached = true;
            if (_navigation.TryGetTarget(out var navigation))
                navigation.Popped -= OnPopped;
        }

        private void OnPopped(object? sender, NavigationEventArgs e)
        {
            if (!_hostPage.TryGetTarget(out var host) || !ReferenceEquals(e.Page, host))
                return;

            try
            {
                if (_element.TryGetTarget(out var element))
                    _onDone(element);
            }
            finally
            {
                lock (Gate)
                {
                    Detach();
                    Tracked.Remove(this);
                }
            }
        }
    }
}
