using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using LimbusSplitPro.App.ViewModels;
using LimbusSplitPro.Core.Models;

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
            Filter = "Archivos de Audio (*.wav;*.mp3;*.flac;*.m4a;*.aif;*.aiff)|*.wav;*.mp3;*.flac;*.m4a;*.aif;*.aiff|Todos los archivos (*.*)|*.*"
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

    /// <summary>Click on a stem card toggles its IsSelected state</summary>
    private void OnStemCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is StemCategory stem && stem.IsAvailable)
        {
            stem.IsSelected = !stem.IsSelected;
        }
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StemSelection.SelectAllCommand.Execute(null);
        }
    }

    private void OnSelectNoneClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StemSelection.SelectNoneCommand.Execute(null);
        }
    }
}
