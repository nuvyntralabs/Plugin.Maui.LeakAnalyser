using System.ComponentModel;

namespace Plugin.Maui.LeakAnalyser.Sample;

public partial class LeakyPage : ContentPage
{
    public LeakyPage()
    {
        InitializeComponent();
        if (Application.Current is not null)
            Application.Current.PropertyChanged += OnAppPropertyChanged;
    }

    void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Intentionally retained by Application.Current.
        _ = StatusLabel;
    }
}
