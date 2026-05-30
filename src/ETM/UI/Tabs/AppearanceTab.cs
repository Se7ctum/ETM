using ETM.Persistence;

namespace ETM.UI.Tabs;

internal sealed class AppearanceTab : UserControl
{
    private static readonly string[] PopularFonts =
    [
        "Segoe UI",
        "Arial",
        "Calibri",
        "Tahoma",
        "Verdana",
        "Consolas",
        "Trebuchet MS",
        "Microsoft Sans Serif",
        "Georgia",
        "Times New Roman"
    ];

    private static readonly string[] LabelPositions =
    [
        "TopLeft",
        "TopRight",
        "BottomLeft",
        "BottomRight"
    ];

    internal AppearanceTab(Profile profile, Action saveRequested)
    {
        AppearanceDefaults appearance = profile.Appearance;
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddColor(root, "Border color", appearance.BorderColor, value => appearance.BorderColor = value, saveRequested);
        AddColor(root, "Active border color", appearance.ActiveBorderColor, value => appearance.ActiveBorderColor = value, saveRequested);
        AddNumber(root, "Border width", appearance.BorderWidth, 0, 16, value => appearance.BorderWidth = value, saveRequested);
        AddColor(root, "Label color", appearance.LabelColor, value => appearance.LabelColor = value, saveRequested);
        AddCheck(root, "Show hotkey in label", appearance.ShowHotkeyInLabel, value => appearance.ShowHotkeyInLabel = value, saveRequested);
        AddCombo(root, "Label font", appearance.LabelFont, PopularFonts, value => appearance.LabelFont = value, saveRequested);
        AddNumber(root, "Label font size", appearance.LabelFontSize, 6, 32, value => appearance.LabelFontSize = value, saveRequested);
        AddCombo(root, "Label position", appearance.LabelPosition, LabelPositions, value => appearance.LabelPosition = value, saveRequested);

        Controls.Add(root);
    }

    private static void AddColor(TableLayoutPanel panel, string label, string value, Action<string> setter, Action save)
    {
        Button button = new()
        {
            Text = value,
            BackColor = ParseColor(value, Color.White),
            Dock = DockStyle.Left,
            Width = 140
        };
        button.Click += (_, _) =>
        {
            using ColorDialog dialog = new()
            {
                Color = button.BackColor,
                FullOpen = true
            };

            if (dialog.ShowDialog(button) != DialogResult.OK)
            {
                return;
            }

            string color = ColorTranslator.ToHtml(dialog.Color);
            button.Text = color;
            button.BackColor = dialog.Color;
            setter(color);
            save();
        };
        AddRow(panel, label, button);
    }

    private static void AddCombo(TableLayoutPanel panel, string label, string value, string[] values, Action<string> setter, Action save)
    {
        ComboBox combo = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Left,
            Width = 180
        };
        combo.Items.AddRange(values);
        combo.SelectedItem = values.Contains(value) ? value : values[0];
        combo.SelectedIndexChanged += (_, _) =>
        {
            setter(combo.SelectedItem?.ToString() ?? values[0]);
            save();
        };
        AddRow(panel, label, combo);
    }

    private static void AddCheck(TableLayoutPanel panel, string label, bool value, Action<bool> setter, Action save)
    {
        CheckBox checkBox = new()
        {
            Checked = value,
            Dock = DockStyle.Left,
            AutoSize = true
        };
        checkBox.CheckedChanged += (_, _) =>
        {
            setter(checkBox.Checked);
            save();
        };
        AddRow(panel, label, checkBox);
    }

    private static void AddNumber(TableLayoutPanel panel, string label, int value, int minimum, int maximum, Action<int> setter, Action save)
    {
        NumericUpDown box = new()
        {
            Minimum = minimum,
            Maximum = maximum,
            Dock = DockStyle.Left,
            Width = 80
        };
        box.Value = Math.Clamp(value, (int)box.Minimum, (int)box.Maximum);
        box.ValueChanged += (_, _) =>
        {
            setter((int)box.Value);
            save();
        };
        AddRow(panel, label, box);
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        int row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private static Color ParseColor(string value, Color fallback)
    {
        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
