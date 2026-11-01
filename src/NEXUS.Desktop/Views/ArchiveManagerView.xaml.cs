using System.Windows;
using System.Windows.Controls;
using NEXUS.Desktop.ViewModels;

namespace NEXUS.Desktop.Views;

public partial class ArchiveManagerView : UserControl
{
    public ArchiveManagerView()
    {
        InitializeComponent();
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDropFiles(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (DataContext is ArchiveManagerViewModel vm && files != null && files.Length > 0)
            {
                vm.HandleDroppedFiles(files);
            }
        }
        e.Handled = true;
    }
}

