using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NEXUS.Desktop.ViewModels;

namespace NEXUS.Desktop.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropZoneBorder.BorderBrush = (Brush)FindResource("Brush.Accent.Primary");
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DropZoneBorder.BorderBrush = (Brush)FindResource("Brush.Border.Subtle");
        e.Handled = true;
    }

    private async void OnDropFiles(object sender, DragEventArgs e)
    {
        DropZoneBorder.BorderBrush = (Brush)FindResource("Brush.Border.Subtle");

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (DataContext is DashboardViewModel vm)
            {
                await vm.HandleDroppedFilesAsync(files);
            }
        }
        e.Handled = true;
    }
}

