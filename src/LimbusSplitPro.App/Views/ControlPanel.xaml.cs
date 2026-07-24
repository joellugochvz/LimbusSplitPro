using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LimbusSplitPro.App.ViewModels;

namespace LimbusSplitPro.App.Views;

public partial class ControlPanel : UserControl
{
    public ControlPanel()
    {
        InitializeComponent();
    }

    private void OnSelectInputFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar Canción para Separar — Limbus Split Pro",
            Filter = "Archivos de Audio (*.wav;*.mp3;*.flac;*.m4a)|*.wav;*.mp3;*.flac;*.m4a|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
        {
            vm.LoadInputFile(dialog.FileName);
        }
    }

    private void OnSelectFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar Carpeta de Trabajo — Limbus Split Pro"
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
        {
            vm.OutputFolderPath = dialog.FolderName;
        }
    }

    private void OnDropZoneDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && DataContext is MainViewModel vm)
            {
                vm.LoadInputFile(files[0]);
            }
        }
    }

    private void OnDropZoneDragOver(object sender, DragEventArgs e)
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
}
