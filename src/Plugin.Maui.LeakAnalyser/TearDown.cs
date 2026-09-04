namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Attached properties that tear down a visual tree after unload.
/// These are not items in <see cref="VisualElement.Behaviors"/>, so
/// <see cref="TearDownStrategy.Compartmentalize"/> can clear <c>Behaviors</c> without removing these hooks.
/// </summary>
public static class TearDown
{
    /// <summary>
    /// Optional per-element hook. Invoked only for <see cref="TearDownStrategy.Compartmentalize"/>
    /// when the element still has a handler.
    /// </summary>
    public static Action<VisualElement>? OnTearDown { get; set; }

    /// <summary>
    /// When true, subscribe to <see cref="VisualElement.Unloaded"/> and tear down when the view is done.
    /// May only be set on a <see cref="VisualElement"/>.
    /// </summary>
    public static readonly BindableProperty CascadeProperty = BindableProperty.CreateAttached(
        "Cascade",
        typeof(bool),
        typeof(TearDown),
        false,
        propertyChanged: OnCascadeChanged);

    /// <summary>
    /// When true, skip this view and its children.
    /// </summary>
    public static readonly BindableProperty SuppressProperty = BindableProperty.CreateAttached(
        "Suppress",
        typeof(bool),
        typeof(TearDown),
        false);

    /// <summary>
    /// Per-view strategy. <c>null</c> uses <see cref="LeakAnalyserOptions.DefaultTearDownStrategy"/>.
    /// </summary>
    public static readonly BindableProperty StrategyProperty = BindableProperty.CreateAttached(
        "Strategy",
        typeof(TearDownStrategy?),
        typeof(TearDown),
        defaultValue: null);

    /// <summary>Gets <see cref="CascadeProperty"/>.</summary>
    public static bool GetCascade(BindableObject bindable)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        return (bool)bindable.GetValue(CascadeProperty);
    }

    /// <summary>Sets <see cref="CascadeProperty"/>.</summary>
    public static void SetCascade(BindableObject bindable, bool value)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        EnsureVisualElement(bindable, value);
        bindable.SetValue(CascadeProperty, value);
    }

    /// <summary>Gets <see cref="SuppressProperty"/>.</summary>
    public static bool GetSuppress(BindableObject bindable)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        return (bool)bindable.GetValue(SuppressProperty);
    }

    /// <summary>Sets <see cref="SuppressProperty"/>.</summary>
    public static void SetSuppress(BindableObject bindable, bool value)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        bindable.SetValue(SuppressProperty, value);
    }

    /// <summary>Gets <see cref="StrategyProperty"/>.</summary>
    public static TearDownStrategy? GetStrategy(BindableObject bindable)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        return (TearDownStrategy?)bindable.GetValue(StrategyProperty);
    }

    /// <summary>Sets <see cref="StrategyProperty"/>.</summary>
    public static void SetStrategy(BindableObject bindable, TearDownStrategy? value)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        bindable.SetValue(StrategyProperty, value);
    }

    private static void OnCascadeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var enabled = newValue is true;
        EnsureVisualElement(bindable, enabled);

        if (bindable is not VisualElement visual)
            return;

        visual.Unloaded -= OnUnloaded;
        if (enabled)
            visual.Unloaded += OnUnloaded;
    }

    private static void OnUnloaded(object? sender, EventArgs e)
    {
        if (sender is not VisualElement visual)
            return;

        ElementLifecycleTracker.RunWhenDone(
            visual,
            nameof(TearDown),
            static candidate => GetSuppress(candidate),
            static candidate => candidate.TearDown(LeakGraph.ResolveStrategy(candidate)));
    }

    internal static void EnsureVisualElement(BindableObject bindable, bool cascadeEnabled)
    {
        if (cascadeEnabled && bindable is not VisualElement)
        {
            throw new InvalidOperationException("TearDown.Cascade can only be set on a VisualElement.");
        }
    }
}
