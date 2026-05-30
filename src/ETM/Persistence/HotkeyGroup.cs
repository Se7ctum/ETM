namespace ETM.Persistence;

internal sealed class HotkeyGroup
{
    public string Name { get; set; } = string.Empty;
    public string CycleHotkey { get; set; } = string.Empty;
    public List<string> CharacterNames { get; set; } = new();
}
