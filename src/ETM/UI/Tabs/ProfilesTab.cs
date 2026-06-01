using System.IO;
using ETM.Persistence;
using ETM.UI;

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
    private readonly Label activeProfileLabel = new();
    private bool isLoadingProfileDetails;

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
        TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(18) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        profilesList.Dock = DockStyle.Fill;
        profilesList.SelectedIndexChanged += (_, _) => LoadSelectedProfileDetails();
        root.Controls.Add(profilesList, 0, 0);

        FlowLayoutPanel buttons = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 12, 0, 0) };
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

        TableLayoutPanel details = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(18, 0, 0, 0) };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        activeProfileLabel.Text = $"Active profile: {settings.ActiveProfileName}";
        activeProfileLabel.AutoSize = true;
        activeProfileLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
        details.Controls.Add(activeProfileLabel, 0, 0);
        details.SetColumnSpan(activeProfileLabel, 2);

        autoLoadCount.Minimum = 0;
        autoLoadCount.Maximum = 64;
        autoLoadCount.ValueChanged += (_, _) =>
        {
            if (isLoadingProfileDetails || SelectedProfile is not { } selectedProfile)
            {
                return;
            }

            selectedProfile.AutoLoadClientCount = autoLoadCount.Value == 0 ? null : (int)autoLoadCount.Value;
            saveRequested();
        };
        autoLoadCharacters.Multiline = true;
        autoLoadCharacters.AcceptsReturn = true;
        autoLoadCharacters.ScrollBars = ScrollBars.Vertical;
        autoLoadCharacters.Height = 150;
        autoLoadCharacters.TextChanged += (_, _) =>
        {
            if (isLoadingProfileDetails || SelectedProfile is not { } selectedProfile)
            {
                return;
            }

            selectedProfile.AutoLoadCharacters = SplitCharacterRules(autoLoadCharacters.Text);
            saveRequested();
        };
        AddSectionText(details, "Profile activation rules", "ETM can switch to this profile automatically when your running EVE clients match these rules. Leave fields empty to switch manually.");
        AddRow(details, "If these characters are active", autoLoadCharacters);
        AddRow(details, "Also require client count", autoLoadCount);
        root.Controls.Add(details, 1, 0);
        Controls.Add(root);
    }

    private Profile? SelectedProfile => profilesList.SelectedIndex >= 0 && profilesList.SelectedIndex < settings.Profiles.Count
        ? settings.Profiles[profilesList.SelectedIndex]
        : null;

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

        if (profilesList.SelectedIndex < 0 && profilesList.Items.Count > 0)
        {
            int activeIndex = settings.Profiles.FindIndex(profile =>
                string.Equals(profile.Name, settings.ActiveProfileName, StringComparison.OrdinalIgnoreCase));
            profilesList.SelectedIndex = activeIndex >= 0 ? activeIndex : 0;
        }

        LoadSelectedProfileDetails();
    }

    private void LoadSelectedProfileDetails()
    {
        isLoadingProfileDetails = true;
        try
        {
            Profile? selectedProfile = SelectedProfile;
            autoLoadCount.Value = selectedProfile?.AutoLoadClientCount ?? 0;
            autoLoadCharacters.Text = selectedProfile is null ? string.Empty : string.Join(Environment.NewLine, selectedProfile.AutoLoadCharacters);
            activeProfileLabel.Text = $"Active profile: {settings.ActiveProfileName}";
        }
        finally
        {
            isLoadingProfileDetails = false;
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
            activeProfileLabel.Text = $"Active profile: {settings.ActiveProfileName}";
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
                DefaultOpacity = source.Appearance.DefaultOpacity,
                ShowHotkeyInLabel = source.Appearance.ShowHotkeyInLabel
            },
            ThumbnailsLocked = source.ThumbnailsLocked
        };
    }

    private static Button MakeButton(string text, EventHandler click)
    {
        Button button = new() { Text = text, AutoSize = true, MinimumSize = new Size(92, 36), Margin = new Padding(0, 0, 8, 8) };
        button.Click += click;
        return button;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 10, 0, 0) });
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control);
    }

    private static void AddSectionText(TableLayoutPanel panel, string title, string body)
    {
        Label titleLabel = new()
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(0, 24, 0, 4)
        };
        Label bodyLabel = new()
        {
            Text = body,
            AutoSize = false,
            Height = 48,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.MutedText
        };

        panel.Controls.Add(titleLabel);
        panel.SetColumnSpan(titleLabel, 2);
        panel.Controls.Add(bodyLabel);
        panel.SetColumnSpan(bodyLabel, 2);
    }

    private static List<string> SplitCharacterRules(string value)
    {
        return value
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(character => !string.IsNullOrWhiteSpace(character))
            .ToList();
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
