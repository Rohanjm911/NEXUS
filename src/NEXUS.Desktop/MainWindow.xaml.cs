using System.Windows;
using System.Windows.Input;
using NEXUS.Desktop.ViewModels;

namespace NEXUS.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        KeyDown += OnWindowKeyDown;
        Loaded += async (s, e) => await _viewModel.InitializeAsync();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _viewModel.ToggleCommandPaletteCommand.Execute(null);
            if (_viewModel.IsCommandPaletteOpen)
            {
                CommandQueryBox.Focus();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _viewModel.IsCommandPaletteOpen)
        {
            _viewModel.IsCommandPaletteOpen = false;
            e.Handled = true;
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == CommandPaletteOverlay)
        {
            _viewModel.IsCommandPaletteOpen = false;
        }
    }
}

