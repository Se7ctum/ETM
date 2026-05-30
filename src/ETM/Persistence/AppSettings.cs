namespace ETM.Persistence;

internal sealed class AppSettings
{
    public string ActiveProfileName { get; set; } = "Default";
    public List<Profile> Profiles { get; set; } = new();
    public GlobalSettings Global { get; set; } = new();
}

internal sealed class GlobalSettings
{
    public string ShowHideAllHotkey { get; set; } = string.Empty;
    public bool LaunchOnStartup { get; set; }
    public bool SnapToEdges { get; set; } = true;
    public int SnapThreshold { get; set; } = 12;
    public bool SnapToGrid { get; set; }
    public int GridSize { get; set; } = 20;
}
