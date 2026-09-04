namespace Plugin.Maui.LeakAnalyser.Sample;

public sealed class LeakDetail
{
    public LeakDetail(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public string Value { get; }
}

public sealed class LeakReport
{
    public required string ObjectName { get; init; }
    public required IReadOnlyList<LeakDetail> Details { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.Now;

    public string Title => $"Leak · {ObjectName}";
}

public static class LeakLog
{
    static readonly List<LeakReport> ReportsInternal = [];
    static readonly object Gate = new();

    public static event EventHandler? Changed;

    public static int Count
    {
        get
        {
            lock (Gate)
                return ReportsInternal.Count;
        }
    }

    public static IReadOnlyList<LeakReport> Snapshot()
    {
        lock (Gate)
            return ReportsInternal.ToArray();
    }

    public static void Add(CollectionTarget target)
    {
        if (MainThread.IsMainThread)
            AddCore(target);
        else
            MainThread.BeginInvokeOnMainThread(() => AddCore(target));
    }

    static void AddCore(CollectionTarget target)
    {
        var report = new LeakReport
        {
            ObjectName = target.Name,
            Details = Capture(target)
        };

        lock (Gate)
            ReportsInternal.Add(report);

        Changed?.Invoke(null, EventArgs.Empty);
    }

    static IReadOnlyList<LeakDetail> Capture(CollectionTarget target)
    {
        var rows = new List<LeakDetail>
        {
            new("Screen", string.IsNullOrWhiteSpace(target.Screen) ? "Unknown screen" : target.Screen),
            new("Object", target.Name),
            new("Type", target.ObjectType),
            new("Alive", target.IsAlive ? "yes" : "no"),
            new("Method", DescribeMethod(target)),
            new("Detected", DateTimeOffset.Now.ToLocalTime().ToString("T")),
            new("Heap", $"{GC.GetTotalMemory(false) / 1024:N0} KB")
        };

        var live = target.Reference.Target;
        if (live is null)
        {
            rows.Add(new("Live instance", "gone before details could be read"));
            return rows;
        }

        Add(rows, "Runtime type", live.GetType().FullName ?? live.GetType().Name);
        Add(rows, "Hash", live.GetHashCode().ToString("X8"));
        Try(rows, "GC generation", () => GC.GetGeneration(live).ToString());

        switch (live)
        {
            case IElementHandler handler:
                Add(rows, "Kind", "Handler");
                Add(rows, "Handler type", TypeName(handler));
                if (handler.VirtualView is not null)
                    Add(rows, "Virtual view", TypeName(handler.VirtualView));
                if (handler.PlatformView is not null)
                    Add(rows, "Platform view", TypeName(handler.PlatformView));
                break;
            case Element element:
                CaptureElement(rows, element);
                break;
            default:
                Add(rows, "Kind", "Object");
                break;
        }

        return rows;
    }

    static void CaptureElement(List<LeakDetail> rows, Element element)
    {
        Add(rows, "Kind", element is Page ? "Page" : "Element");
        Add(rows, "AutomationId", element.AutomationId);
        Add(rows, "StyleId", element.StyleId);
        Add(rows, "ClassId", element.ClassId);

        if (element is Page page)
            Add(rows, "Title", page.Title);

        if (element is Label label)
            Add(rows, "Text", label.Text);

        Try(rows, "Parent", () => element.Parent is null ? "(none)" : TypeName(element.Parent));
        Try(rows, "BindingContext", () => element.BindingContext is null ? "(none)" : TypeName(element.BindingContext));

        if (element.Handler is null)
        {
            Add(rows, "Handler connected", "no");
        }
        else
        {
            Add(rows, "Handler connected", "yes");
            Add(rows, "Handler type", TypeName(element.Handler));
            if (element.Handler.PlatformView is not null)
                Add(rows, "Platform view", TypeName(element.Handler.PlatformView));
        }

        if (element is VisualElement visual)
        {
            Try(rows, "Loaded", () => visual.IsLoaded ? "yes" : "no");
            Try(rows, "Visible", () => visual.IsVisible ? "yes" : "no");
            Try(rows, "Size", () => $"{visual.Width:0.#} × {visual.Height:0.#}");
        }

        if (element is IVisualTreeElement tree)
        {
            Try(rows, "Visual children", () => tree.GetVisualChildren().Count.ToString());
        }
    }

    static void Add(List<LeakDetail> rows, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            rows.Add(new LeakDetail(label, value));
    }

    static void Try(List<LeakDetail> rows, string label, Func<string?> read)
    {
        try
        {
            Add(rows, label, read());
        }
        catch (Exception)
        {
            // Property getters on a torn-down MAUI object can throw; skip that row.
        }
    }

    static string TypeName(object value)
    {
        var type = value.GetType();
        return type.FullName ?? type.Name;
    }

    static string DescribeMethod(CollectionTarget target)
    {
        var name = target.Name;
        var type = target.ObjectType;
        var isHandler = name.EndsWith(" Handler", StringComparison.Ordinal);

        if (name.Contains("LeakyPage", StringComparison.Ordinal) || type.Contains("LeakyPage", StringComparison.Ordinal))
        {
            return isHandler
                ? "Page handler stayed alive because LeakyPage is still rooted"
                : "LeakyPage..ctor → Application.PropertyChanged += OnAppPropertyChanged";
        }

        if (name is "Label" || type.EndsWith(".Label", StringComparison.Ordinal))
        {
            return isHandler
                ? "Handler stayed alive because the Label is still rooted"
                : "Held by LeakyPage after LeakyPage..ctor → Application.PropertyChanged += OnAppPropertyChanged";
        }

        return "Unknown retain path. LeakAnalyser is a liveness test — use a profiler for the method that still holds this object.";
    }
}
