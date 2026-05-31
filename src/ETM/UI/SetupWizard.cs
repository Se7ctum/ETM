using ETM.Persistence;

namespace ETM.UI;

internal sealed class SetupWizard : Form
{
    private readonly AppSettings settings;
    private readonly Profile profile;
    private readonly Action saveRequested;
    private readonly NumericUpDown width = new();
    private readonly NumericUpDown height = new();
    private readonly CheckBox lockThumbnails = new();
    private readonly CheckBox showHotkeys = new();
    private readonly CheckBox snapToGrid = new();

    internal SetupWizard(AppSettings settings, Profile profile, Action saveRequested)
    {
        this.settings = settings;
        this.profile = profile;
        this.saveRequested = saveRequested;

        Text = "ETM Setup";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(520, 390);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        BuildUi();
        UiTheme.Apply(this);
    }

    private void BuildUi()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            RowCount = 8,
            ColumnCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Label title = new()
        {
            Text = "Quick setup",
            AutoSize = true,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point)
        };
        root.Controls.Add(title, 0, 0);
        root.SetColumnSpan(title, 2);

        Label intro = new()
        {
            Text = "Set the defaults ETM should use for thumbnail layouts. You can change everything later.",
            AutoSize = false,
            Height = 48,
            ForeColor = UiTheme.MutedText,
            Dock = DockStyle.Fill
        };
        root.Controls.Add(intro, 0, 1);
        root.SetColumnSpan(intro, 2);

        width.Minimum = 100;
        width.Maximum = 640;
        width.Value = 320;
        height.Minimum = 57;
        height.Maximum = 360;
        height.Value = 180;
        lockThumbnails.Text = "Start with thumbnails locked";
        lockThumbnails.Checked = profile.ThumbnailsLocked;
        showHotkeys.Text = "Show hotkeys in labels";
        showHotkeys.Checked = profile.Appearance.ShowHotkeyInLabel;
        snapToGrid.Text = "Snap thumbnails to grid";
        snapToGrid.Checked = settings.Global.SnapToGrid;

        int row = 2;
        AddRow(root, row++, "Default thumbnail width", width);
        AddRow(root, row++, "Default thumbnail height", height);
        AddWide(root, row++, lockThumbnails);
        AddWide(root, row++, showHotkeys);
        AddWide(root, row++, snapToGrid);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        Button finish = new()
        {
            Text = "Finish",
            DialogResult = DialogResult.OK,
            MinimumSize = new Size(120, 38)
        };
        Button skip = new()
        {
            Text = "Skip",
            DialogResult = DialogResult.Cancel,
            MinimumSize = new Size(120, 38)
        };
        buttons.Controls.Add(finish);
        buttons.Controls.Add(skip);
        root.Controls.Add(buttons, 0, 7);
        root.SetColumnSpan(buttons, 2);

        AcceptButton = finish;
        CancelButton = skip;
        Controls.Add(root);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            profile.ThumbnailsLocked = lockThumbnails.Checked;
            profile.Appearance.ShowHotkeyInLabel = showHotkeys.Checked;
            settings.Global.SnapToGrid = snapToGrid.Checked;
            foreach (OverlayState overlay in profile.Overlays)
            {
                overlay.Width = (int)width.Value;
                overlay.Height = (int)height.Value;
            }
        }

        settings.SetupCompleted = true;
        saveRequested();
        base.OnFormClosed(e);
    }

    private static void AddRow(TableLayoutPanel root, int row, string label, Control control)
    {
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Dock = DockStyle.Fill;
        root.Controls.Add(control, 1, row);
    }

    private static void AddWide(TableLayoutPanel root, int row, Control control)
    {
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        control.Dock = DockStyle.Fill;
        root.Controls.Add(control, 0, row);
        root.SetColumnSpan(control, 2);
    }
}
