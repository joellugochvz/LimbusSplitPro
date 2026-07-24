using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LimbusSplitPro.Core.Models;

public enum StemGroup
{
    Vocals,
    Drums,
    Bass,
    Guitar,
    Piano,
    Other,
    Noise,
    Custom
}

public class StemCategory : INotifyPropertyChanged
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public StemGroup Group { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public bool IsAvailable { get; set; } = true;
    public string UnavailableReason { get; set; } = string.Empty;
    public string IconKey { get; set; } = "AudioTrack";
    public string DefaultColorHex { get; set; } = "#0078D4";
    public bool IsSubStem { get; set; }
    public string? ParentCategoryKey { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static List<StemCategory> CreateDefaultCategories()
    {
        return new List<StemCategory>
        {
            // Vocals group
            new StemCategory { Id = "vocals", DisplayName = "Voces (General)", Description = "Pista vocal completa", Group = StemGroup.Vocals, IsSelected = true, IconKey = "Microphone", DefaultColorHex = "#E74C3C" },
            new StemCategory { Id = "lead_vocal", DisplayName = "Voz Principal", Description = "Voz solista desglosada", Group = StemGroup.Vocals, IsSelected = false, IsSubStem = true, ParentCategoryKey = "vocals", IconKey = "Account", DefaultColorHex = "#E67E22" },
            new StemCategory { Id = "backing_vocals", DisplayName = "Coros y Segundas Voces", Description = "Voces secundarias y armonías", Group = StemGroup.Vocals, IsSelected = false, IsSubStem = true, ParentCategoryKey = "vocals", IconKey = "AccountGroup", DefaultColorHex = "#F39C12" },
            new StemCategory { Id = "vocal_fx", DisplayName = "Efectos Vocales / Reverb", Description = "Ambiente y reverberación vocal", Group = StemGroup.Vocals, IsSelected = false, IsSubStem = true, ParentCategoryKey = "vocals", IconKey = "Waves", DefaultColorHex = "#D35400" },
            new StemCategory { Id = "noise", DisplayName = "Ruido y Artefactos", Description = "Soplido, siseos y ruido de fondo", Group = StemGroup.Noise, IsSelected = false, IconKey = "VolumeOff", DefaultColorHex = "#7F8C8D" },

            // Drums group
            new StemCategory { Id = "drums", DisplayName = "Batería Completa", Description = "Set de percusión completo", Group = StemGroup.Drums, IsSelected = true, IconKey = "Drum", DefaultColorHex = "#2ECC71" },
            new StemCategory { Id = "kick", DisplayName = "Bombo", Description = "Kick drum de frecuencia baja", Group = StemGroup.Drums, IsSelected = false, IsSubStem = true, ParentCategoryKey = "drums", IconKey = "Circle", DefaultColorHex = "#27AE60" },
            new StemCategory { Id = "snare", DisplayName = "Caja", Description = "Snare drum y golpeteos metálicos", Group = StemGroup.Drums, IsSelected = false, IsSubStem = true, ParentCategoryKey = "drums", IconKey = "Square", DefaultColorHex = "#1ABC9C" },
            new StemCategory { Id = "toms", DisplayName = "Toms", Description = "Tom-toms y rototoms", Group = StemGroup.Drums, IsSelected = false, IsSubStem = true, ParentCategoryKey = "drums", IconKey = "Layers", DefaultColorHex = "#16A085" },
            new StemCategory { Id = "cymbals", DisplayName = "Platos y Hi-Hats", Description = "Címbalos, crash, ride y hi-hats", Group = StemGroup.Drums, IsSelected = false, IsSubStem = true, ParentCategoryKey = "drums", IconKey = "Star", DefaultColorHex = "#3498DB" },

            // Bass group
            new StemCategory { Id = "bass", DisplayName = "Bajo", Description = "Bajo eléctrico, sintético o contrabajo", Group = StemGroup.Bass, IsSelected = true, IconKey = "GuitarBass", DefaultColorHex = "#9B59B6" },

            // Guitar group
            new StemCategory { Id = "guitar_acoustic", DisplayName = "Guitarra Acústica", Description = "Guitarras de cuerdas de nailon/acero", Group = StemGroup.Guitar, IsSelected = false, IsAvailable = true, IconKey = "GuitarAcoustic", DefaultColorHex = "#8E44AD" },
            new StemCategory { Id = "guitar_electric", DisplayName = "Guitarra Eléctrica", Description = "Guitarras rítmicas o solistas amplificadas", Group = StemGroup.Guitar, IsSelected = false, IsAvailable = true, IconKey = "GuitarElectric", DefaultColorHex = "#2980B9" },

            // Piano group
            new StemCategory { Id = "piano", DisplayName = "Piano y Teclados", Description = "Pianos acústicos, digitales y sintetizadores", Group = StemGroup.Piano, IsSelected = false, IsAvailable = true, IconKey = "Piano", DefaultColorHex = "#34495E" },

            // Other group (Residual)
            new StemCategory { Id = "other", DisplayName = "Other (Residual)", Description = "Cualquier instrumento o acompañamiento no seleccionado previamente", Group = StemGroup.Other, IsSelected = true, IconKey = "MusicNote", DefaultColorHex = "#95A5A6" }
        };
    }
}
