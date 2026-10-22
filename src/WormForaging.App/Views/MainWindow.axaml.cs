using Avalonia.Controls;

namespace WormForaging.App.Views;

/// <summary>
/// The application's main window. Its runtime <c>DataContext</c> is a DI-resolved
/// <see cref="ViewModels.MainWindowViewModel"/>, assigned during application startup.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Initializes the window and loads its XAML.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
