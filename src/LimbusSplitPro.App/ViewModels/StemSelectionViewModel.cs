using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.ViewModels;

public partial class StemSelectionViewModel : ObservableObject
{
    public ObservableCollection<StemCategory> Categories { get; } = new();

    public StemSelectionViewModel()
    {
        var defaults = StemCategory.CreateDefaultCategories();
        foreach (var c in defaults)
        {
            Categories.Add(c);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var c in Categories)
        {
            if (c.IsAvailable)
            {
                c.IsSelected = true;
            }
        }
        OnPropertyChanged(nameof(Categories));
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var c in Categories)
        {
            c.IsSelected = false;
        }
        OnPropertyChanged(nameof(Categories));
    }

    public List<StemCategory> GetSelectedCategories()
    {
        return Categories.Where(c => c.IsSelected && c.IsAvailable).ToList();
    }
}
