using ETM.Core;
using ETM.Persistence;

namespace ETM.UI.Tabs;

internal sealed class HotkeysTab : UserControl
{
    private readonly AppSettings settings;
    private readonly Profile profile;
    private readonly Action saveRequested;
    private readonly ListBox groupsList = new();
    private readonly TextBox groupName = new();
    private readonly TextBox cycleHotkey = new();
    private readonly TextBox characterNames = new();
    private TextBox globalHotkey = null!;
    private bool loadingGroup;

    internal HotkeysTab(AppSettings settings, Profile profile, Action saveRequested)
    {
        this.settings = settings;
        this.profile = profile;
        this.saveRequested = saveRequested;
        BuildUi();
        RefreshGroups();
    }

    private void BuildUi()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        GroupBox globalBox = new()
        {
            Text = "Global",
            Dock = DockStyle.Fill
        };
        TableLayoutPanel globalLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(8)
        };
        globalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        globalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        globalHotkey = new TextBox { Text = settings.Global.ShowHideAllHotkey, Dock = DockStyle.Fill, ReadOnly = true };
        globalHotkey.TextChanged += (_, _) =>
        {
            settings.Global.ShowHideAllHotkey = globalHotkey.Text.Trim();
        };
        globalHotkey.Leave += (_, _) => saveRequested();
        AttachHotkeyCapture(globalHotkey, hotkey =>
        {
            settings.Global.ShowHideAllHotkey = hotkey;
            saveRequested();
        });
        AddRow(globalLayout, "Show/Hide All hotkey", globalHotkey, 0);
        globalBox.Controls.Add(globalLayout);
        root.Controls.Add(globalBox, 0, 0);

        TableLayoutPanel groupsRoot = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        groupsRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        groupsRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        groupsList.Dock = DockStyle.Fill;
        groupsList.SelectedIndexChanged += (_, _) => LoadSelectedGroup();

        FlowLayoutPanel groupButtons = new() { Dock = DockStyle.Bottom, AutoSize = true };
        groupButtons.Controls.Add(MakeButton("New group", (_, _) => AddGroup()));
        groupButtons.Controls.Add(MakeButton("Delete", (_, _) => DeleteSelectedGroup()));

        Panel leftPanel = new() { Dock = DockStyle.Fill };
        leftPanel.Controls.Add(groupsList);
        leftPanel.Controls.Add(groupButtons);
        groupsRoot.Controls.Add(leftPanel, 0, 0);

        GroupBox detailsBox = new()
        {
            Text = "Hotkey Group",
            Dock = DockStyle.Top,
            AutoSize = true
        };
        TableLayoutPanel details = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(8)
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        cycleHotkey.ReadOnly = true;
        characterNames.Multiline = true;
        characterNames.AcceptsReturn = true;
        characterNames.ScrollBars = ScrollBars.Vertical;
        characterNames.MinimumSize = new Size(0, 140);

        groupName.TextChanged += (_, _) => UpdateSelectedGroup(save: false);
        characterNames.TextChanged += (_, _) => UpdateSelectedGroup(save: false);
        groupName.Leave += (_, _) => SaveSelectedGroup();
        cycleHotkey.Leave += (_, _) => SaveSelectedGroup();
        characterNames.Leave += (_, _) => SaveSelectedGroup();
        AttachHotkeyCapture(cycleHotkey, hotkey =>
        {
            HotkeyGroup? selected = GetSelectedGroup();
            if (selected is null)
            {
                return;
            }

            selected.CycleHotkey = hotkey;
            SaveSelectedGroup();
        });

        AddRow(details, "Group name", groupName, 0);
        AddRow(details, "Cycle hotkey", cycleHotkey, 1);
        AddRow(details, "Characters", characterNames, 2);

        detailsBox.Controls.Add(details);
        groupsRoot.Controls.Add(detailsBox, 1, 0);
        root.Controls.Add(groupsRoot, 0, 1);
        Controls.Add(root);
    }

    private void RefreshGroups()
    {
        string? selectedName = groupsList.SelectedItem as string;
        groupsList.Items.Clear();
        foreach (HotkeyGroup group in profile.HotkeyGroups)
        {
            groupsList.Items.Add(string.IsNullOrWhiteSpace(group.Name) ? "(unnamed group)" : group.Name);
        }

        if (groupsList.Items.Count == 0)
        {
            LoadSelectedGroup();
            return;
        }

        int selectedIndex = selectedName is null
            ? 0
            : profile.HotkeyGroups.FindIndex(group => string.Equals(group.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        groupsList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }

    private void LoadSelectedGroup()
    {
        loadingGroup = true;
        HotkeyGroup? selected = GetSelectedGroup();
        bool enabled = selected is not null;
        groupName.Enabled = enabled;
        cycleHotkey.Enabled = enabled;
        characterNames.Enabled = enabled;
        groupName.Text = selected?.Name ?? string.Empty;
        cycleHotkey.Text = selected?.CycleHotkey ?? string.Empty;
        characterNames.Text = selected is null ? string.Empty : string.Join(Environment.NewLine, selected.CharacterNames);
        loadingGroup = false;
    }

    private void AddGroup()
    {
        string name = MakeUniqueGroupName();
        profile.HotkeyGroups.Add(new HotkeyGroup { Name = name });
        RefreshGroups();
        groupsList.SelectedItem = name;
        saveRequested();
    }

    private void DeleteSelectedGroup()
    {
        if (groupsList.SelectedIndex < 0)
        {
            return;
        }

        profile.HotkeyGroups.RemoveAt(groupsList.SelectedIndex);
        RefreshGroups();
        saveRequested();
    }

    private void SaveSelectedGroup()
    {
        UpdateSelectedGroup(save: true);
    }

    private void UpdateSelectedGroup(bool save)
    {
        if (loadingGroup)
        {
            return;
        }

        HotkeyGroup? selected = GetSelectedGroup();
        if (selected is null)
        {
            return;
        }

        selected.Name = groupName.Text.Trim();
        selected.CharacterNames = characterNames.Text
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (save)
        {
            int selectedIndex = groupsList.SelectedIndex;
            groupsList.Items[selectedIndex] = string.IsNullOrWhiteSpace(selected.Name) ? "(unnamed group)" : selected.Name;
            saveRequested();
        }
    }

    private HotkeyGroup? GetSelectedGroup()
    {
        return groupsList.SelectedIndex >= 0 && groupsList.SelectedIndex < profile.HotkeyGroups.Count
            ? profile.HotkeyGroups[groupsList.SelectedIndex]
            : null;
    }

    private string MakeUniqueGroupName()
    {
        int index = profile.HotkeyGroups.Count + 1;
        string name;
        do
        {
            name = $"Group {index++}";
        }
        while (profile.HotkeyGroups.Any(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase)));

        return name;
    }

    private static Button MakeButton(string text, EventHandler click)
    {
        Button button = new() { Text = text, AutoSize = true };
        button.Click += click;
        return button;
    }

    private static void AttachHotkeyCapture(TextBox box, Action<string> apply)
    {
        box.Enter += (_, _) => box.SelectAll();
        box.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;

            if (e.KeyCode is Keys.Back or Keys.Delete or Keys.Escape)
            {
                box.Clear();
                apply(string.Empty);
                return;
            }

            Keys keyCode = e.KeyCode;
            if (keyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            {
                return;
            }

            string hotkey = FormatHotkey(e.Modifiers, keyCode);
            if (!HotkeyManager.TryParseHotkey(hotkey, out int _, out int _))
            {
                return;
            }

            box.Text = hotkey;
            box.SelectAll();
            apply(hotkey);
        };
    }

    private static string FormatHotkey(Keys modifiers, Keys keyCode)
    {
        List<string> parts = new();
        if (modifiers.HasFlag(Keys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(Keys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(Keys.Alt))
        {
            parts.Add("Alt");
        }

        parts.Add(GetKeyName(keyCode));
        return string.Join("+", parts);
    }

    private static string GetKeyName(Keys keyCode)
    {
        return keyCode switch
        {
            Keys.Oemcomma => "Comma",
            Keys.OemPeriod => "Period",
            Keys.OemSemicolon => "Semicolon",
            Keys.OemQuotes => "Quote",
            Keys.OemQuestion => "Slash",
            Keys.OemBackslash => "Backslash",
            Keys.OemMinus => "Minus",
            Keys.Oemplus => "Equals",
            Keys.Oemtilde => "Backtick",
            Keys.OemOpenBrackets => "LeftBracket",
            Keys.OemCloseBrackets => "RightBracket",
            _ => keyCode.ToString()
        };
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control, int row)
    {
        panel.RowStyles.Add(new RowStyle(row == 2 ? SizeType.Absolute : SizeType.AutoSize, row == 2 ? 170 : 0));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 1, row);
    }
}
