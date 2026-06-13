namespace ETM.Persistence;

internal sealed class AppSettings
{
    public string ActiveProfileName { get; set; } = "Default";
    public List<Profile> Profiles { get; set; } = new();
    public GlobalSettings Global { get; set; } = new();
    public bool SetupCompleted { get; set; }
}

internal sealed class GlobalSettings
{
    public string ShowHideAllHotkey { get; set; } = string.Empty;
    public bool LaunchOnStartup { get; set; }
    public bool HotkeysRequireEveFocus { get; set; }
    public bool SnapToEdges { get; set; } = true;
    public int SnapThreshold { get; set; } = 12;
    public bool SnapToGrid { get; set; } = true;
    public int GridSize { get; set; } = 10;
}
