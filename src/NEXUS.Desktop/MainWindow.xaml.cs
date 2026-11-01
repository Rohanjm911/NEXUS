using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        else if (e.Key == Key.F11)
        {
            ToggleMaximize();
            e.Handled = true;
        }
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
        {
            ToggleMaximize();
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RootBorder.Margin = new Thickness(7);
            RootBorder.CornerRadius = new CornerRadius(0);
            if (MaximizeIcon != null)
            {
                MaximizeIcon.Data = (System.Windows.Media.Geometry)FindResource("Icon.Restore");
                MaximizeBtn.ToolTip = "Restore Down";
            }
        }
        else
        {
            RootBorder.Margin = new Thickness(0);
            RootBorder.CornerRadius = new CornerRadius(8);
            if (MaximizeIcon != null)
            {
                MaximizeIcon.Data = (System.Windows.Media.Geometry)FindResource("Icon.Maximize");
                MaximizeBtn.ToolTip = "Maximize";
            }
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
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

    private void OnCommandSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is CommandPaletteItem selectedItem)
        {
            selectedItem.Command?.Execute(null);
            listBox.SelectedItem = null;
            _viewModel.IsCommandPaletteOpen = false;
        }
    }

    private void OnCommandItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is CommandPaletteItem item)
        {
            item.Command?.Execute(null);
            _viewModel.IsCommandPaletteOpen = false;
            e.Handled = true;
        }
    }

    private void OnCommandQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var firstCommand = _viewModel.FilteredCommands.FirstOrDefault();
            if (firstCommand != null)
            {
                firstCommand.Command?.Execute(null);
                _viewModel.IsCommandPaletteOpen = false;
                e.Handled = true;
            }
        }
    }
}

