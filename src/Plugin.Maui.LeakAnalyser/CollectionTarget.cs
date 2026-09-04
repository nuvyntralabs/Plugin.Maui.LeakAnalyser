namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// A named weak reference used by leak detection.
/// </summary>
public sealed class CollectionTarget
{
    /// <summary>
    /// Creates a short weak reference to <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The object to watch.</param>
    /// <param name="name">Optional display name. Defaults to the runtime type name.</param>
    /// <param name="screen">Host page name captured at snapshot time, before the visual tree is torn down.</param>
    public CollectionTarget(object reference, string? name = null, string? screen = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var type = reference.GetType();
        Reference = new WeakReference(reference);
        Name = string.IsNullOrWhiteSpace(name) ? type.Name : name;
        ObjectType = type.FullName ?? type.Name;
        Screen = screen;
    }

    /// <summary>
    /// Name used in logs and callbacks.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Runtime type of the watched object, captured when the target was created.
    /// </summary>
    public string ObjectType { get; }

    /// <summary>
    /// Host page display name, if the target was part of a visual tree.
    /// </summary>
    public string? Screen { get; }

    /// <summary>
    /// Short weak reference (does not track resurrection).
    /// </summary>
    public WeakReference Reference { get; }

    /// <summary>
    /// True when the target has not been collected.
    /// </summary>
    public bool IsAlive => Reference.IsAlive;
}
