namespace Plugin.Maui.LeakAnalyser.Sample;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
        LeakLog.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateStats);
        UpdateStats();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateStats();
    }

    async void OnOpenClean(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(PhotoPage));

    async void OnOpenLeaky(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(LeakyPage));

    void UpdateStats()
    {
        HeapLabel.Text = $"Managed heap: {GC.GetTotalMemory(false) / 1024:N0} KB";
        var reports = LeakLog.Snapshot();
        LeakLabel.Text = $"Leaks reported: {reports.Count}";
        EmptyLeaksLabel.IsVisible = reports.Count == 0;

        LeakList.Children.Clear();
        foreach (var report in reports)
            LeakList.Children.Add(CreateLeakCard(report));
    }

    static View CreateLeakCard(LeakReport report)
    {
        var card = new VerticalStackLayout { Spacing = 4 };
        card.Add(new Label
        {
            Text = report.Title,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828")
        });
        foreach (var detail in report.Details)
            card.Add(Row(detail.Label, detail.Value));

        return new Border
        {
            Stroke = Color.FromArgb("#C62828"),
            StrokeThickness = 1,
            Padding = new Thickness(12),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Content = card
        };
    }

    static Label Row(string label, string value) => new()
    {
        Text = $"{label}: {value}",
        LineBreakMode = LineBreakMode.WordWrap
    };
}
