using ETM.Persistence;

namespace ETM.UI.Tabs;

internal sealed class ThumbnailsTab : UserControl
{
    internal ThumbnailsTab(Profile profile, Action saveRequested)
    {
        profile.Overlays.RemoveAll(overlay => string.Equals(overlay.CharacterName, "EVE Launcher", StringComparison.OrdinalIgnoreCase));

        AutoScroll = true;

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 6,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddHeader(root, "Character", 0);
        AddHeader(root, "Direct hotkey", 1);
        AddHeader(root, "Opacity %", 2);
        AddHeader(root, "Visible", 3);
        AddHeader(root, "Lock ratio", 4);

        int row = 1;
        foreach (OverlayState overlay in profile.Overlays.OrderBy(overlay => overlay.CharacterName))
        {
            AddOverlayRow(root, overlay, row++, saveRequested);
        }

        Controls.Add(root);
    }

    private static void AddOverlayRow(TableLayoutPanel root, OverlayState overlay, int row, Action saveRequested)
    {
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        Label character = new()
        {
            Text = overlay.CharacterName,
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(character, 0, row);

        TextBox hotkey = new()
        {
            Text = overlay.DirectHotkey ?? string.Empty,
            Dock = DockStyle.Fill
        };
        hotkey.Leave += (_, _) =>
        {
            overlay.DirectHotkey = hotkey.Text.Trim();
            saveRequested();
        };
        root.Controls.Add(hotkey, 1, row);

        NumericUpDown opacity = new()
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp((int)Math.Round(overlay.Opacity * 100), 0, 100),
            Dock = DockStyle.Fill
        };
        opacity.ValueChanged += (_, _) =>
        {
            overlay.Opacity = (float)opacity.Value / 100f;
            saveRequested();
        };
        root.Controls.Add(opacity, 2, row);

        CheckBox visible = new()
        {
            Checked = overlay.Visible,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        visible.CheckedChanged += (_, _) =>
        {
            overlay.Visible = visible.Checked;
            saveRequested();
        };
        root.Controls.Add(visible, 3, row);

        CheckBox aspectRatio = new()
        {
            Checked = overlay.AspectRatioLocked,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        aspectRatio.CheckedChanged += (_, _) =>
        {
            overlay.AspectRatioLocked = aspectRatio.Checked;
            saveRequested();
        };
        root.Controls.Add(aspectRatio, 4, row);
    }

    private static void AddHeader(TableLayoutPanel root, string text, int column)
    {
        Label label = new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(label, column, 0);
    }
}
