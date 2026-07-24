using System.Windows;
using System.Windows.Controls;
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
}
