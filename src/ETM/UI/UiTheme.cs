namespace ETM.UI;

internal static class UiTheme
{
    internal static readonly Color Background = Color.FromArgb(18, 22, 28);
    internal static readonly Color Surface = Color.FromArgb(27, 33, 42);
    internal static readonly Color SurfaceRaised = Color.FromArgb(36, 44, 56);
    internal static readonly Color Border = Color.FromArgb(62, 74, 91);
    internal static readonly Color Text = Color.FromArgb(236, 241, 247);
    internal static readonly Color MutedText = Color.FromArgb(158, 171, 186);
    internal static readonly Color Accent = Color.FromArgb(54, 162, 235);

    internal static void Apply(Control root)
    {
        root.BackColor = Background;
        root.ForeColor = Text;
        ApplyRecursive(root);
    }

    internal static void StyleTabControl(TabControl tabs)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.ItemSize = new Size(132, 38);
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.Padding = new Point(16, 6);
        tabs.DrawItem += (_, e) =>
        {
            TabPage page = tabs.TabPages[e.Index];
            bool selected = e.Index == tabs.SelectedIndex;
            using Brush background = new SolidBrush(selected ? SurfaceRaised : Surface);
            using Brush foreground = new SolidBrush(selected ? Text : MutedText);
            e.Graphics.FillRectangle(background, e.Bounds);
            TextRenderer.DrawText(
                e.Graphics,
                page.Text,
                tabs.Font,
                e.Bounds,
                selected ? Text : MutedText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (selected)
            {
                using Pen accent = new(Accent, 3);
                e.Graphics.DrawLine(accent, e.Bounds.Left + 8, e.Bounds.Bottom - 2, e.Bounds.Right - 8, e.Bounds.Bottom - 2);
            }
        };
    }

    private static void ApplyRecursive(Control control)
    {
        switch (control)
        {
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = SurfaceRaised;
                button.FlatAppearance.MouseDownBackColor = Accent;
                button.BackColor = SurfaceRaised;
                button.ForeColor = Text;
                button.Height = Math.Max(button.Height, 34);
                break;
            case TextBox textBox:
                textBox.BackColor = Color.FromArgb(12, 15, 20);
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.None;
                textBox.Margin = new Padding(0, 4, 0, 4);
                break;
            case ListBox listBox:
                listBox.BackColor = Color.FromArgb(12, 15, 20);
                listBox.ForeColor = Text;
                listBox.BorderStyle = BorderStyle.None;
                break;
            case ComboBox comboBox:
                comboBox.BackColor = Color.FromArgb(12, 15, 20);
                comboBox.ForeColor = Text;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = Color.FromArgb(12, 15, 20);
                numeric.ForeColor = Text;
                break;
            case TabPage tabPage:
                tabPage.BackColor = Background;
                tabPage.ForeColor = Text;
                break;
            case TableLayoutPanel or FlowLayoutPanel or Panel:
                if (control.BackColor == SystemColors.Control)
                {
                    control.BackColor = Background;
                }

                control.ForeColor = Text;
                break;
            case Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = Text;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyRecursive(child);
        }
    }
}
