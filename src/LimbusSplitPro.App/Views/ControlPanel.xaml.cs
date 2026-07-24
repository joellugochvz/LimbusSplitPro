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
            Title = "Seleccionar Canción para Separar - Limbus Split Pro",
            Filter = "Archivos de Audio (*.wav;*.mp3;*.flac;*.m4a)|*.wav;*.mp3;*.flac;*.m4a|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
        {
            vm.LoadInputFile(dialog.FileName);
        }
    }

    private void OnSelectFolderClick(object sender, RoutedEventArgs e)
    {
        // FolderPicker dialog
    }
}
