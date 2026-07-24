using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LimbusSplitPro.App.ViewModels;

namespace LimbusSplitPro.App;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Spacebar shortcut toggles play/pause ONLY if focus is NOT inside TextBox or ComboBox
        if (e.Key == Key.Space)
        {
            var focusedElem = Keyboard.FocusedElement;
            if (focusedElem is not TextBox && focusedElem is not ComboBox)
            {
                ViewModel.PlayPauseCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                ViewModel.LoadInputFile(files[0]);
            }
        }
    }
}
