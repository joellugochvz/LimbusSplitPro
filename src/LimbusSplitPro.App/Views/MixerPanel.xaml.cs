using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LimbusSplitPro.App.ViewModels;

namespace LimbusSplitPro.App.Views;

public partial class MixerPanel : UserControl
{
    public MixerPanel()
    {
        InitializeComponent();
    }

    private void OnVolumeSliderMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TrackViewModel trackVm)
        {
            trackVm.Volume = 1.0f;
        }
        else if (sender is Slider slider)
        {
            slider.Value = 1.0;
        }
    }
}
