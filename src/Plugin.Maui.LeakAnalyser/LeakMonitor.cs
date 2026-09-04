namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// Attached properties that watch a visual tree after unload.
/// These are not items in <see cref="VisualElement.Behaviors"/>.
/// </summary>
public static class LeakMonitor
{
    /// <summary>
    /// When true, subscribe to <see cref="VisualElement.Unloaded"/> and monitor when the view is done.
    /// May only be set on a <see cref="VisualElement"/>.
    /// </summary>
    public static readonly BindableProperty CascadeProperty = BindableProperty.CreateAttached(
        "Cascade",
        typeof(bool),
        typeof(LeakMonitor),
        false,
        propertyChanged: OnCascadeChanged);

    /// <summary>
    /// When true, skip this view and its children during a parent monitor walk.
    /// </summary>
    public static readonly BindableProperty SuppressProperty = BindableProperty.CreateAttached(
        "Suppress",
        typeof(bool),
        typeof(LeakMonitor),
        false);

    /// <summary>
    /// Optional display name for logs and callbacks.
    /// </summary>
    public static readonly BindableProperty NameProperty = BindableProperty.CreateAttached(
        "Name",
        typeof(string),
        typeof(LeakMonitor),
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

    /// <summary>Gets <see cref="NameProperty"/>.</summary>
    public static string? GetName(BindableObject bindable)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        return (string?)bindable.GetValue(NameProperty);
    }

    /// <summary>Sets <see cref="NameProperty"/>.</summary>
    public static void SetName(BindableObject bindable, string? value)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        bindable.SetValue(NameProperty, value);
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
            nameof(LeakMonitor),
            static candidate => GetSuppress(candidate),
            static candidate => candidate.Monitor());
    }

    internal static void EnsureVisualElement(BindableObject bindable, bool cascadeEnabled)
    {
        if (cascadeEnabled && bindable is not VisualElement)
        {
            throw new InvalidOperationException("LeakMonitor.Cascade can only be set on a VisualElement.");
        }
    }
}
