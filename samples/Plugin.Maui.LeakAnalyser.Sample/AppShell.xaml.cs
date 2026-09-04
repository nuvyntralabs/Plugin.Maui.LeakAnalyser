namespace Plugin.Maui.LeakAnalyser.Sample;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(PhotoPage), typeof(PhotoPage));
        Routing.RegisterRoute(nameof(LeakyPage), typeof(LeakyPage));
    }
}
