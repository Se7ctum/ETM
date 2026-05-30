namespace ETM.Persistence;

internal sealed class OverlayState
{
    public string CharacterName { get; set; } = string.Empty;
    public string CustomLabel { get; set; } = string.Empty;
    public int MonitorIndex { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 180;
    public bool Visible { get; set; } = true;
    public float Opacity { get; set; } = 1.0f;
    public bool AspectRatioLocked { get; set; } = true;
    public int ZOrder { get; set; }
    public string? DirectHotkey { get; set; }
}
