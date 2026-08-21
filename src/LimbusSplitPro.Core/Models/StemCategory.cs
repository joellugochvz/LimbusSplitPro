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
    public string FullDescription { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static List<StemCategory> CreateDefaultCategories()
    {
        return new List<StemCategory>
        {
            // Vocals group — Amarillo / Yellow
            new StemCategory { Id = "vocals",         DisplayName = "Voces (General)",          Description = "Pista vocal completa",         FullDescription = "Extrae la pista vocal completa de la canción.", Group = StemGroup.Vocals, IsSelected = true,  IconKey = "Microphone",    DefaultColorHex = "#F59E0B" },
            new StemCategory { Id = "lead_vocal",     DisplayName = "Voz Principal",            Description = "Voz central / Lead",           FullDescription = "Canal Mid (centro) de la pista vocal donde se ubica la voz cantante principal.", Group = StemGroup.Vocals, IsSelected = false, IsSubStem = true, ParentCategoryKey = "vocals", IconKey = "Account",       DefaultColorHex = "#FBBF24" },
            new StemCategory { Id = "backing_vocals", DisplayName = "Coros y Segundas Voces",   Description = "Armonías y dobles",            FullDescription = "Canal Side (lados) de la pista vocal con armonías, coros y voces paneadas.", Group = StemGroup.Vocals, IsSelected = false, IsSubStem = true, ParentCategoryKey = "vocals", IconKey = "AccountGroup",  DefaultColorHex = "#FCD34D" },
            new StemCategory { Id = "vocal_fx",       DisplayName = "Efectos Vocales / Reverb", Description = "Aire, sibilancia y reverb",    FullDescription = "Filtro en altas frecuencias (2–8 kHz) que aísla el aire vocal, sibilancia y cola de reverb.", Group = StemGroup.Vocals, IsSelected = false, IsSubStem = true, ParentCategoryKey = "vocals", IconKey = "Waves",         DefaultColorHex = "#EAB308" },

            // Drums group — Verde / Green
            new StemCategory { Id = "drums",   DisplayName = "Batería Completa", Description = "Percusión completa",          FullDescription = "Set de percusión completo extraído de la mezcla.", Group = StemGroup.Drums, IsSelected = true,  IconKey = "Drum",   DefaultColorHex = "#22C55E" },
            new StemCategory { Id = "kick",    DisplayName = "Bombo y Toms",     Description = "Bombo y percusión grave",     FullDescription = "Frecuencias bajas del set: golpe de bombo, toms y resonancia grave.", Group = StemGroup.Drums, IsSelected = false, IsSubStem = true, ParentCategoryKey = "drums", IconKey = "Circle", DefaultColorHex = "#16A34A" },
            new StemCategory { Id = "snare",   DisplayName = "Caja",             Description = "Caja y ataque metálico",      FullDescription = "Frecuencias medias-altas de la percusión: snare drum y golpeteos.", Group = StemGroup.Drums, IsSelected = false, IsSubStem = true, ParentCategoryKey = "drums", IconKey = "Square", DefaultColorHex = "#4ADE80" },
            new StemCategory { Id = "cymbals", DisplayName = "Platos y HI-hats", Description = "Crash, ride y hi-hats",        FullDescription = "Altas frecuencias de la batería: platos, ride, crash y hi-hats.", Group = StemGroup.Drums, IsSelected = false, IsSubStem = true, ParentCategoryKey = "drums", IconKey = "Star",   DefaultColorHex = "#86EFAC" },

            // Bass group — Rojo Vino / Wine Red
            new StemCategory { Id = "bass", DisplayName = "Bajo", Description = "Bajo eléctrico o synth", FullDescription = "Línea de bajo eléctrico, sintético o contrabajo.", Group = StemGroup.Bass, IsSelected = true, IconKey = "GuitarBass", DefaultColorHex = "#9B1C2E" },

            // Guitar group — Morado / Purple
            new StemCategory { Id = "guitar", DisplayName = "Guitarra", Description = "Guitarras rítmicas y solo", FullDescription = "Guitarras acústicas y eléctricas (rítmicas y solistas).", Group = StemGroup.Guitar, IsSelected = false, IsAvailable = true, IconKey = "GuitarAcoustic", DefaultColorHex = "#8B5CF6" },

            // Piano group — Rosa / Pink
            new StemCategory { Id = "piano", DisplayName = "Piano y Teclados", Description = "Pianos y sintetizadores", FullDescription = "Pianos acústicos, teclados digitales y sintetizadores.", Group = StemGroup.Piano, IsSelected = false, IsAvailable = true, IconKey = "Piano", DefaultColorHex = "#EC4899" },

            // Other group (Residual) — Azul / Blue
            new StemCategory { Id = "other", DisplayName = "Other (Residual)", Description = "Acompañamiento restante", FullDescription = "Cualquier instrumento o acompañamiento no seleccionado previamente.", Group = StemGroup.Other, IsSelected = true, IconKey = "MusicNote", DefaultColorHex = "#3B82F6" }
        };
    }
}
