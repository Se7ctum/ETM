namespace ETM.Persistence;

internal sealed class Profile
{
    public string Name { get; set; } = "Default";
    public int? AutoLoadClientCount { get; set; }
    public List<string> AutoLoadCharacters { get; set; } = new();
    public List<OverlayState> Overlays { get; set; } = new();
    public List<HotkeyGroup> HotkeyGroups { get; set; } = new();
    public AppearanceDefaults Appearance { get; set; } = new();
    public bool ThumbnailsLocked { get; set; }
}

internal sealed class AppearanceDefaults
{
    public string BorderColor { get; set; } = "#444444";
    public string ActiveBorderColor { get; set; } = "#00BFFF";
    public int BorderWidth { get; set; } = 2;
    public string LabelColor { get; set; } = "#FFFFFF";
    public bool LabelBackgroundEnabled { get; set; } = true;
    public bool ShowHotkeyInLabel { get; set; }
    public string LabelFont { get; set; } = "Segoe UI";
    public int LabelFontSize { get; set; } = 9;
    public string LabelPosition { get; set; } = "TopLeft";
    public float DefaultOpacity { get; set; } = 1.0f;
}
