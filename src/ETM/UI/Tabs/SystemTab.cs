using ETM.Persistence;
using ETM.Core;

namespace ETM.UI.Tabs;

internal sealed class SystemTab : UserControl
{
    internal SystemTab(AppSettings settings, Action saveRequested)
    {
        GlobalSettings global = settings.Global;
        TableLayoutPanel root = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        bool startupEnabled = global.LaunchOnStartup;
        try
        {
            startupEnabled = StartupManager.IsEnabled();
            global.LaunchOnStartup = startupEnabled;
        }
        catch (Exception)
        {
            // Keep the persisted value if the registry cannot be read.
        }

        CheckBox startup = new() { Checked = startupEnabled };
        bool suppressStartupChange = false;
        startup.CheckedChanged += (_, _) =>
        {
            if (suppressStartupChange)
            {
                return;
            }

            try
            {
                StartupManager.SetLaunchOnStartup(startup.Checked);
                global.LaunchOnStartup = startup.Checked;
                saveRequested();
            }
            catch (Exception ex)
            {
                suppressStartupChange = true;
                startup.Checked = global.LaunchOnStartup;
                suppressStartupChange = false;
                MessageBox.Show(FindForm(), $"Unable to update startup setting: {ex.Message}", "ETM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        CheckBox snapEdges = new() { Checked = global.SnapToEdges };
        snapEdges.CheckedChanged += (_, _) => { global.SnapToEdges = snapEdges.Checked; saveRequested(); };
        NumericUpDown snapThreshold = new() { Minimum = 0, Maximum = 64 };
        snapThreshold.Value = Math.Clamp(global.SnapThreshold, (int)snapThreshold.Minimum, (int)snapThreshold.Maximum);
        snapThreshold.ValueChanged += (_, _) => { global.SnapThreshold = (int)snapThreshold.Value; saveRequested(); };
        CheckBox snapGrid = new() { Checked = global.SnapToGrid };
        snapGrid.CheckedChanged += (_, _) => { global.SnapToGrid = snapGrid.Checked; saveRequested(); };
        NumericUpDown gridSize = new() { Minimum = 1, Maximum = 200 };
        gridSize.Value = Math.Clamp(global.GridSize, (int)gridSize.Minimum, (int)gridSize.Maximum);
        gridSize.ValueChanged += (_, _) => { global.GridSize = (int)gridSize.Value; saveRequested(); };
        AddRow(root, "Launch on startup", startup);
        AddRow(root, "Snap to edges", snapEdges);
        AddRow(root, "Snap threshold", snapThreshold);
        AddRow(root, "Snap to grid", snapGrid);
        AddRow(root, "Grid size", gridSize);
        Controls.Add(root);
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left });
        panel.Controls.Add(control);
    }
}
