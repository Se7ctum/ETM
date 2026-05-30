using ETM.Persistence;

namespace ETM.UI.Tabs;

internal sealed class ProfilesTab : UserControl
{
    private readonly AppSettings settings;
    private readonly Profile activeProfile;
    private readonly Action saveRequested;
    private readonly Action<string> activeProfileChanged;
    private readonly ListBox profilesList = new();
    private readonly NumericUpDown autoLoadCount = new();
    private readonly TextBox autoLoadCharacters = new();

    internal ProfilesTab(AppSettings settings, Profile activeProfile, Action saveRequested, Action<string> activeProfileChanged)
    {
        this.settings = settings;
        this.activeProfile = activeProfile;
        this.saveRequested = saveRequested;
        this.activeProfileChanged = activeProfileChanged;
        BuildUi();
        RefreshProfiles();
    }

    private void BuildUi()
    {
        TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        profilesList.Dock = DockStyle.Fill;
        root.Controls.Add(profilesList, 0, 0);

        FlowLayoutPanel buttons = new() { Dock = DockStyle.Bottom, AutoSize = true };
        buttons.Controls.Add(MakeButton("New", (_, _) => AddProfile()));
        buttons.Controls.Add(MakeButton("Rename", (_, _) => RenameSelected()));
        buttons.Controls.Add(MakeButton("Duplicate", (_, _) => DuplicateSelected()));
        buttons.Controls.Add(MakeButton("Delete", (_, _) => DeleteSelected()));
        buttons.Controls.Add(MakeButton("Import (.json)", (_, _) => ImportProfile()));
        buttons.Controls.Add(MakeButton("Export (.json)", (_, _) => ExportSelected()));
        buttons.Controls.Add(MakeButton("Set active", (_, _) => SetActive()));

        Panel left = new() { Dock = DockStyle.Fill };
        left.Controls.Add(profilesList);
        left.Controls.Add(buttons);
        root.Controls.Add(left, 0, 0);

        TableLayoutPanel details = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        autoLoadCount.Minimum = 0;
        autoLoadCount.Maximum = 64;
        autoLoadCount.Value = activeProfile.AutoLoadClientCount ?? 0;
        autoLoadCount.ValueChanged += (_, _) => { activeProfile.AutoLoadClientCount = autoLoadCount.Value == 0 ? null : (int)autoLoadCount.Value; saveRequested(); };
        autoLoadCharacters.Text = string.Join(", ", activeProfile.AutoLoadCharacters);
        autoLoadCharacters.TextChanged += (_, _) => { activeProfile.AutoLoadCharacters = autoLoadCharacters.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); saveRequested(); };
        AddRow(details, "Auto-load count", autoLoadCount);
        AddRow(details, "Auto-load characters", autoLoadCharacters);
        root.Controls.Add(details, 1, 0);
        Controls.Add(root);
    }

    private void RefreshProfiles()
    {
        string? selectedName = profilesList.SelectedItem as string;
        profilesList.Items.Clear();
        foreach (Profile profile in settings.Profiles)
        {
            profilesList.Items.Add(profile.Name);
        }

        if (selectedName is not null)
        {
            int selectedIndex = settings.Profiles.FindIndex(profile =>
                string.Equals(profile.Name, selectedName, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex >= 0)
            {
                profilesList.SelectedIndex = selectedIndex;
            }
        }
    }

    private void AddProfile()
    {
        string name = $"Profile {settings.Profiles.Count + 1}";
        settings.Profiles.Add(new Profile { Name = name });
        RefreshProfiles();
        saveRequested();
    }

    private void RenameSelected()
    {
        if (profilesList.SelectedIndex < 0) return;
        Profile profile = settings.Profiles[profilesList.SelectedIndex];
        string? newName = PromptForText("Rename profile", "Profile name", profile.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        newName = newName.Trim();
        if (!string.Equals(profile.Name, newName, StringComparison.OrdinalIgnoreCase) && FindProfileIndex(newName) >= 0)
        {
            MessageBox.Show(FindForm(), $"A profile named \"{newName}\" already exists.", "Rename Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string oldName = profile.Name;
        profile.Name = newName;
        if (string.Equals(settings.ActiveProfileName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            settings.ActiveProfileName = newName;
            activeProfileChanged(newName);
        }

        RefreshProfiles();
        profilesList.SelectedItem = newName;
        saveRequested();
    }

    private void DuplicateSelected()
    {
        if (profilesList.SelectedIndex < 0) return;
        Profile source = settings.Profiles[profilesList.SelectedIndex];
        settings.Profiles.Add(CloneProfile(source, source.Name + " Copy"));
        RefreshProfiles();
        saveRequested();
    }

    private void DeleteSelected()
    {
        if (profilesList.SelectedIndex < 0 || settings.Profiles.Count <= 1) return;
        string removedName = settings.Profiles[profilesList.SelectedIndex].Name;
        settings.Profiles.RemoveAt(profilesList.SelectedIndex);
        if (string.Equals(settings.ActiveProfileName, removedName, StringComparison.OrdinalIgnoreCase))
        {
            settings.ActiveProfileName = settings.Profiles[0].Name;
            activeProfileChanged(settings.ActiveProfileName);
        }

        RefreshProfiles();
        saveRequested();
    }

    private void SetActive()
    {
        if (profilesList.SelectedItem is string name)
        {
            settings.ActiveProfileName = name;
            activeProfileChanged(name);
            saveRequested();
        }
    }

    private void ImportProfile()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Import profile",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        Profile importedProfile;
        try
        {
            importedProfile = SettingsManager.ImportProfile(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), $"Profile import failed: {ex.Message}", "Import Profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int existingIndex = FindProfileIndex(importedProfile.Name);
        if (existingIndex >= 0)
        {
            DialogResult result = MessageBox.Show(
                FindForm(),
                $"A profile named \"{importedProfile.Name}\" already exists. Replace it?",
                "Import Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            settings.Profiles[existingIndex] = importedProfile;
        }
        else
        {
            settings.Profiles.Add(importedProfile);
        }

        RefreshProfiles();
        profilesList.SelectedItem = importedProfile.Name;

        if (string.Equals(settings.ActiveProfileName, importedProfile.Name, StringComparison.OrdinalIgnoreCase))
        {
            activeProfileChanged(importedProfile.Name);
        }

        saveRequested();
    }

    private void ExportSelected()
    {
        if (profilesList.SelectedIndex < 0)
        {
            return;
        }

        Profile profile = settings.Profiles[profilesList.SelectedIndex];
        using SaveFileDialog dialog = new()
        {
            Title = "Export profile",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = MakeSafeFileName(profile.Name) + ".json"
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SettingsManager.ExportProfile(profile, dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), $"Profile export failed: {ex.Message}", "Export Profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int FindProfileIndex(string profileName)
    {
        return settings.Profiles.FindIndex(profile =>
            string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string MakeSafeFileName(string name)
    {
        string safeName = string.IsNullOrWhiteSpace(name) ? "Profile" : name;
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return safeName;
    }

    private static Profile CloneProfile(Profile source, string name)
    {
        return new Profile
        {
            Name = name,
            AutoLoadClientCount = source.AutoLoadClientCount,
            AutoLoadCharacters = source.AutoLoadCharacters.ToList(),
            Overlays = source.Overlays.Select(overlay => new OverlayState
            {
                CharacterName = overlay.CharacterName,
                CustomLabel = overlay.CustomLabel,
                MonitorIndex = overlay.MonitorIndex,
                X = overlay.X,
                Y = overlay.Y,
                Width = overlay.Width,
                Height = overlay.Height,
                Visible = overlay.Visible,
                Opacity = overlay.Opacity,
                AspectRatioLocked = overlay.AspectRatioLocked,
                ZOrder = overlay.ZOrder,
                DirectHotkey = overlay.DirectHotkey
            }).ToList(),
            HotkeyGroups = source.HotkeyGroups.Select(group => new HotkeyGroup
            {
                Name = group.Name,
                CycleHotkey = group.CycleHotkey,
                CharacterNames = group.CharacterNames.ToList()
            }).ToList(),
            Appearance = new AppearanceDefaults
            {
                BorderColor = source.Appearance.BorderColor,
                ActiveBorderColor = source.Appearance.ActiveBorderColor,
                BorderWidth = source.Appearance.BorderWidth,
                LabelColor = source.Appearance.LabelColor,
                LabelFont = source.Appearance.LabelFont,
                LabelFontSize = source.Appearance.LabelFontSize,
                LabelPosition = source.Appearance.LabelPosition,
                DefaultOpacity = source.Appearance.DefaultOpacity
            },
            ThumbnailsLocked = source.ThumbnailsLocked
        };
    }

    private static Button MakeButton(string text, EventHandler click)
    {
        Button button = new() { Text = text, AutoSize = true };
        button.Click += click;
        return button;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control);
    }

    private static string? PromptForText(string title, string label, string initialValue)
    {
        using Form dialog = new()
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(360, 118)
        };

        Label prompt = new()
        {
            Text = label,
            AutoSize = true,
            Left = 12,
            Top = 12
        };
        TextBox textBox = new()
        {
            Text = initialValue,
            Left = 12,
            Top = 36,
            Width = 336
        };
        Button okButton = new()
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 192,
            Top = 76,
            Width = 75
        };
        Button cancelButton = new()
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 273,
            Top = 76,
            Width = 75
        };

        dialog.Controls.Add(prompt);
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        return dialog.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }
}
