using System.Windows;
using System.Windows.Controls;
using NEXUS.Desktop.ViewModels;

namespace NEXUS.Desktop.Views;

public partial class ImageUpscalerView : UserControl
{
    public ImageUpscalerView()
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
            if (DataContext is ImageUpscalerViewModel vm && files != null && files.Length > 0)
            {
                vm.HandleDroppedFile(files[0]);
            }
        }
    }
}

