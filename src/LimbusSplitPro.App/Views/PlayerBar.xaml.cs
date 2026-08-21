using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LimbusSplitPro.App.ViewModels;

namespace LimbusSplitPro.App.Views;

public partial class PlayerBar : UserControl
{
    public PlayerBar()
    {
        InitializeComponent();
    }

    private void OnSeekValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DataContext is MainViewModel vm && Math.Abs(e.NewValue - e.OldValue) > 0.5)
        {
            vm.OnSeekSliderChanged(e.NewValue);
        }
    }

    private void OnLoadProjectClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Cargar Proyecto — Selecciona la carpeta con los stems WAV"
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
        {
            vm.LoadStemsFromFolder(dialog.FolderName);
        }
    }
}
