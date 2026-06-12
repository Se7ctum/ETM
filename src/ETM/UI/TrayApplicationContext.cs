using System.IO;
using Microsoft.Win32;
using ETM.Core;
using ETM.Persistence;

namespace ETM.UI;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int DefaultOverlayWidth = 320;
    private const int DefaultOverlayHeight = 180;
    private const int MinimumOverlayWidth = 100;
    private const int MinimumOverlayHeight = 57;
    private const int MaximumOverlayWidth = 480;
    private const int MaximumOverlayHeight = 270;

    private readonly NotifyIcon trayIcon;
    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly System.Windows.Forms.Timer saveDebounceTimer;
    private readonly ForegroundWatcher foregroundWatcher;
    private readonly HotkeyMessageWindow hotkeyWindow;
    private readonly HotkeyManager hotkeyManager;
    private readonly Dictionary<IntPtr, ThumbnailOverlay> overlays = new();
    private readonly Dictionary<string, Rectangle> savedOverlayBounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppSettings settings;
    private Profile activeProfile;
    private ConfigWindow? configWindow;
    private ToolStripMenuItem lockThumbnailsMenuItem = null!;
    private ToolStripMenuItem showHideAllMenuItem = null!;
    private bool thumbnailsLocked;
    private bool overlaysVisible = true;
    private bool isShuttingDown;
    private bool isSwitchingProfile;

    internal TrayApplicationContext()
    {
        settings = SettingsManager.Load();
        activeProfile = GetActiveProfile();
        CaptureSavedOverlayBoundsSnapshot();
        thumbnailsLocked = activeProfile.ThumbnailsLocked;

        trayIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "EVE Thumbnail Manager",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        trayIcon.DoubleClick += (_, _) => OpenConfiguration();

        foregroundWatcher = new ForegroundWatcher();
        foregroundWatcher.ForegroundChanged += (_, hwnd) => UpdateActiveOverlay(hwnd);

        hotkeyWindow = new HotkeyMessageWindow();
        hotkeyManager = new HotkeyManager(hotkeyWindow.Handle);
        hotkeyWindow.HotkeyPressed += (_, message) => hotkeyManager.ProcessHotkeyMessage(message);
        RegisterHotkeys();

        saveDebounceTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000
        };
        saveDebounceTimer.Tick += (_, _) =>
        {
            saveDebounceTimer.Stop();
            SavePositionsSafely();
        };

        refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 3000
        };
        refreshTimer.Tick += (_, _) => RefreshThumbnails();
        refreshTimer.Start();

        RefreshThumbnails();
        SystemEvents.SessionEnding += SystemEventsSessionEnding;
        Application.ApplicationExit += ApplicationApplicationExit;
        QueueSetupWizardIfNeeded();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        ContextMenuStrip menu = new();

        menu.Items.Add("Configure...", null, (_, _) => OpenConfiguration());
        menu.Items.Add("Setup wizard...", null, (_, _) => OpenSetupWizard());
        menu.Items.Add("Reload thumbnails", null, (_, _) => ReloadThumbnails());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(BuildProfilesMenu());
        menu.Items.Add(BuildLayoutMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Save position and size", null, (_, _) => SavePositions());
        menu.Items.Add("Restore positions", null, (_, _) => ReloadThumbnails());
        menu.Items.Add(new ToolStripSeparator());

        lockThumbnailsMenuItem = new ToolStripMenuItem("Lock thumbnails")
        {
            CheckOnClick = true,
            Checked = thumbnailsLocked
        };
        lockThumbnailsMenuItem.CheckedChanged += (_, _) =>
        {
            thumbnailsLocked = lockThumbnailsMenuItem.Checked;
            activeProfile.ThumbnailsLocked = thumbnailsLocked;
            ApplyThumbnailLockState();
            ScheduleSave();
        };
        menu.Items.Add(lockThumbnailsMenuItem);

        showHideAllMenuItem = new ToolStripMenuItem("Hide All");
        showHideAllMenuItem.Click += (_, _) => ToggleAllOverlays();
        menu.Items.Add(showHideAllMenuItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        return menu;
    }

    private ToolStripMenuItem BuildProfilesMenu()
    {
        ToolStripMenuItem profilesMenu = new("Profiles");
        profilesMenu.DropDownOpening += (_, _) =>
        {
            profilesMenu.DropDownItems.Clear();
            foreach (Profile profile in settings.Profiles)
            {
                ToolStripMenuItem item = new(profile.Name)
                {
                    Checked = string.Equals(profile.Name, settings.ActiveProfileName, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (_, _) => SwitchActiveProfile(profile.Name);
                profilesMenu.DropDownItems.Add(item);
            }
        };

        return profilesMenu;
    }

    private ToolStripMenuItem BuildLayoutMenu()
    {
        ToolStripMenuItem layoutMenu = new("Layout tools");
        layoutMenu.DropDownItems.Add("Same size", null, (_, _) => MakeAllThumbnailsSameSize());
        layoutMenu.DropDownItems.Add("Arrange in row", null, (_, _) => ArrangeThumbnails(row: true));
        layoutMenu.DropDownItems.Add("Arrange in column", null, (_, _) => ArrangeThumbnails(row: false));
        layoutMenu.DropDownItems.Add("Align top", null, (_, _) => AlignThumbnails(alignTop: true));
        layoutMenu.DropDownItems.Add("Align left", null, (_, _) => AlignThumbnails(alignTop: false));
        return layoutMenu;
    }

    private static Icon LoadTrayIcon()
    {
        using Stream? embeddedIcon = typeof(TrayApplicationContext).Assembly.GetManifestResourceStream("ETM.Resources.tray_icon.ico");
        if (embeddedIcon is not null)
        {
            try
            {
                using Icon icon = new(embeddedIcon);
                return (Icon)icon.Clone();
            }
            catch (Exception)
            {
                // Fall through to the file-system icon if the embedded icon is invalid.
            }
        }

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "tray_icon.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                return new Icon(iconPath);
            }
            catch (Exception)
            {
                // Fall through to the system icon if the packaged icon is invalid.
            }
        }

        return SystemIcons.Application;
    }

    private Profile GetActiveProfile()
    {
        Profile? profile = settings.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, settings.ActiveProfileName, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            return profile;
        }

        profile = settings.Profiles.FirstOrDefault();
        if (profile is not null)
        {
            settings.ActiveProfileName = profile.Name;
            return profile;
        }

        profile = new Profile { Name = "Default" };
        settings.ActiveProfileName = profile.Name;
        settings.Profiles.Add(profile);
        return profile;
    }

    private void RefreshThumbnails()
    {
        try
        {
            List<EveWindow> detectedWindows = WindowEnumerator.FindEveWindows();
            if (TryAutoLoadProfile(detectedWindows))
            {
                return;
            }

            HashSet<IntPtr> detectedHandles = detectedWindows.Select(window => window.Handle).ToHashSet();

            foreach (IntPtr handle in overlays.Keys.Where(handle => !detectedHandles.Contains(handle)).ToList())
            {
                ThumbnailOverlay overlay = overlays[handle];
                if (NativeMethods.IsWindow(overlay.SourceHandle))
                {
                    continue;
                }

                overlay.OverlayStateChanged -= OverlayStateChanged;
                overlay.Dispose();
                overlays.Remove(handle);
            }

            foreach (EveWindow eveWindow in detectedWindows)
            {
                if (overlays.TryGetValue(eveWindow.Handle, out ThumbnailOverlay? existingOverlay))
                {
                    bool identityChanged = existingOverlay.UpdateEveWindow(eveWindow);
                    OverlayState? state = FindOverlayState(eveWindow.CharacterName);
                    if (identityChanged && state is not null)
                    {
                        existingOverlay.Bounds = RestoreBounds(state);
                    }

                    existingOverlay.ApplySettings(state, activeProfile.Appearance);
                    existingOverlay.SetHotkeyGroups(GetHotkeyGroupMenuItems(eveWindow.CharacterName));
                    existingOverlay.SetLabelHotkey(GetLabelHotkey(eveWindow.CharacterName, state));
                    existingOverlay.Visible = overlaysVisible && (state?.Visible ?? true);
                    continue;
                }

                CreateOverlay(eveWindow);
            }

            UpdateActiveOverlay(NativeMethods.GetForegroundWindow());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ETM: Thumbnail refresh failed: {ex}");
        }
    }

    private void UpdateActiveOverlay(IntPtr foregroundHwnd)
    {
        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            overlay.IsActiveClient = overlay.SourceHandle == foregroundHwnd;
        }
    }

    private void SetActiveOverlayImmediately(ThumbnailOverlay activeOverlay)
    {
        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            overlay.SetActiveImmediate(ReferenceEquals(overlay, activeOverlay));
        }
    }

    private void ReloadThumbnails()
    {
        DisposeOverlays();
        RefreshThumbnails();
    }

    private void QueueSetupWizardIfNeeded()
    {
        if (settings.SetupCompleted)
        {
            return;
        }

        System.Windows.Forms.Timer setupTimer = new() { Interval = 500 };
        setupTimer.Tick += (_, _) =>
        {
            setupTimer.Stop();
            setupTimer.Dispose();
            OpenSetupWizard();
        };
        setupTimer.Start();
    }

    private void OpenSetupWizard()
    {
        using SetupWizard wizard = new(settings, activeProfile, ApplySettingsChanges);
        wizard.ShowDialog(configWindow);
        thumbnailsLocked = activeProfile.ThumbnailsLocked;
        lockThumbnailsMenuItem.Checked = thumbnailsLocked;
        ApplyThumbnailLockState();
        ApplyOverlaySettings();
    }

    private void MakeAllThumbnailsSameSize()
    {
        ThumbnailOverlay? source = overlays.Values
            .Where(overlay => !overlay.IsDisposed && overlay.Visible)
            .OrderBy(overlay => overlay.Top)
            .ThenBy(overlay => overlay.Left)
            .FirstOrDefault();
        if (source is null)
        {
            return;
        }

        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            if (overlay.IsDisposed)
            {
                continue;
            }

            overlay.SetThumbnailSize(source.Size);
            UpdateOverlayState(overlay);
        }

        SavePositions();
    }

    private void ArrangeThumbnails(bool row)
    {
        List<ThumbnailOverlay> visibleOverlays = GetVisibleOverlaysInLayoutOrder();
        if (visibleOverlays.Count == 0)
        {
            return;
        }

        const int gap = 12;
        Point start = visibleOverlays[0].Location;
        int offset = 0;
        foreach (ThumbnailOverlay overlay in visibleOverlays)
        {
            overlay.Location = row ? new Point(start.X + offset, start.Y) : new Point(start.X, start.Y + offset);
            offset += row ? overlay.Width + gap : overlay.Height + gap;
            UpdateOverlayState(overlay);
        }

        SavePositions();
    }

    private void AlignThumbnails(bool alignTop)
    {
        List<ThumbnailOverlay> visibleOverlays = GetVisibleOverlaysInLayoutOrder();
        if (visibleOverlays.Count == 0)
        {
            return;
        }

        int target = alignTop ? visibleOverlays.Min(overlay => overlay.Top) : visibleOverlays.Min(overlay => overlay.Left);
        foreach (ThumbnailOverlay overlay in visibleOverlays)
        {
            overlay.Location = alignTop ? new Point(overlay.Left, target) : new Point(target, overlay.Top);
            UpdateOverlayState(overlay);
        }

        SavePositions();
    }

    private List<ThumbnailOverlay> GetVisibleOverlaysInLayoutOrder()
    {
        return overlays.Values
            .Where(overlay => !overlay.IsDisposed && overlay.Visible)
            .OrderBy(overlay => overlay.Top)
            .ThenBy(overlay => overlay.Left)
            .ToList();
    }

    private bool TryAutoLoadProfile(IReadOnlyCollection<EveWindow> detectedWindows)
    {
        if (isSwitchingProfile || settings.Profiles.Count < 2)
        {
            return false;
        }

        Profile? matchingProfile = FindMatchingRuleProfile(detectedWindows);
        if (matchingProfile is null || ReferenceEquals(matchingProfile, activeProfile))
        {
            return false;
        }

        SwitchActiveProfile(matchingProfile.Name);
        return true;
    }

    private Profile? FindMatchingRuleProfile(IReadOnlyCollection<EveWindow> detectedWindows)
    {
        HashSet<string> detectedCharacters = detectedWindows
            .Select(window => window.CharacterName)
            .Where(characterName => !string.IsNullOrWhiteSpace(characterName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return settings.Profiles.FirstOrDefault(profile =>
        {
            bool hasCharacterRule = profile.AutoLoadCharacters.Count > 0;
            bool hasCountRule = profile.AutoLoadClientCount.HasValue;
            if (!hasCharacterRule && !hasCountRule)
            {
                return false;
            }

            bool charactersMatch = !hasCharacterRule || profile.AutoLoadCharacters.All(characterName =>
                !string.IsNullOrWhiteSpace(characterName)
                && detectedCharacters.Contains(characterName));
            bool countMatches = !hasCountRule || profile.AutoLoadClientCount.GetValueOrDefault() == detectedWindows.Count;
            return charactersMatch && countMatches;
        });
    }

    private void CreateOverlay(EveWindow eveWindow)
    {
        ThumbnailOverlay? overlay = null;
        try
        {
            OverlayState? state = FindOverlayState(eveWindow.CharacterName);
            Rectangle bounds = state is null ? new Rectangle(100, 100, DefaultOverlayWidth, DefaultOverlayHeight) : RestoreBounds(state);
            byte opacity = ToOpacityByte(state?.Opacity ?? activeProfile.Appearance.DefaultOpacity);

            overlay = new ThumbnailOverlay(
                eveWindow,
                settings.Global,
                activeProfile.Appearance,
                GetOtherOverlayBounds,
                GetOtherOverlaySizes,
                bounds,
                opacity,
                state?.CustomLabel ?? string.Empty)
            {
                Visible = overlaysVisible && (state?.Visible ?? true),
                AspectRatioLocked = state?.AspectRatioLocked ?? true
            };
            overlay.ThumbnailsLocked = thumbnailsLocked;
            overlay.SetHotkeyGroups(GetHotkeyGroupMenuItems(eveWindow.CharacterName));
            overlay.SetLabelHotkey(GetLabelHotkey(eveWindow.CharacterName, state));
            overlay.OverlayStateChanged += OverlayStateChanged;
            overlay.SourceFocusRequested += OverlaySourceFocusRequested;
            overlay.ResizeAllRequested += OverlayResizeAllRequested;
            overlay.ResetSizeRequested += OverlayResetSizeRequested;
            overlay.HotkeyGroupAssignmentChanged += OverlayHotkeyGroupAssignmentChanged;
            overlays.Add(eveWindow.Handle, overlay);

            if (overlay.Visible)
            {
                overlay.Show();
                overlay.EnsureAlwaysOnTop();
            }
        }
        catch (Exception ex)
        {
            if (overlay is not null)
            {
                overlay.OverlayStateChanged -= OverlayStateChanged;
                overlay.SourceFocusRequested -= OverlaySourceFocusRequested;
                overlay.ResizeAllRequested -= OverlayResizeAllRequested;
                overlay.ResetSizeRequested -= OverlayResetSizeRequested;
                overlay.HotkeyGroupAssignmentChanged -= OverlayHotkeyGroupAssignmentChanged;
                overlay.Dispose();
            }

            overlays.Remove(eveWindow.Handle);
            System.Diagnostics.Debug.WriteLine($"ETM: Failed to create thumbnail overlay: {ex}");
        }
    }

    private OverlayState? FindOverlayState(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        return activeProfile.Overlays.FirstOrDefault(state =>
            string.Equals(state.CharacterName, characterName, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyCollection<Rectangle> GetOtherOverlayBounds(ThumbnailOverlay overlay)
    {
        return overlays.Values
            .Where(candidate => !ReferenceEquals(candidate, overlay) && !candidate.IsDisposed)
            .Select(candidate => candidate.Bounds)
            .ToList();
    }

    private IReadOnlyCollection<Size> GetOtherOverlaySizes(ThumbnailOverlay overlay)
    {
        return overlays.Values
            .Where(candidate => !ReferenceEquals(candidate, overlay) && !candidate.IsDisposed)
            .Select(candidate => candidate.Size)
            .ToList();
    }

    private static byte ToOpacityByte(float opacity)
    {
        float clamped = Math.Clamp(opacity, 0f, 1f);
        return (byte)Math.Round(clamped * byte.MaxValue);
    }

    private static float FromOpacityByte(byte opacity)
    {
        return opacity / (float)byte.MaxValue;
    }

    private void OverlayStateChanged(object? sender, EventArgs e)
    {
        if (sender is ThumbnailOverlay overlay)
        {
            UpdateOverlayState(overlay);
            ScheduleSave();
        }
    }

    private void OverlaySourceFocusRequested(object? sender, EventArgs e)
    {
        if (sender is ThumbnailOverlay overlay)
        {
            SetActiveOverlayImmediately(overlay);
        }
    }

    private void OverlayResizeAllRequested(object? sender, ThumbnailOverlay.ThumbnailResizeEventArgs e)
    {
        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            if (ReferenceEquals(overlay, sender) || overlay.IsDisposed)
            {
                continue;
            }

            overlay.SetThumbnailSize(e.Size);
            UpdateOverlayState(overlay);
        }

        ScheduleSave();
    }

    private void OverlayResetSizeRequested(object? sender, EventArgs e)
    {
        if (sender is not ThumbnailOverlay overlay)
        {
            return;
        }

        if (savedOverlayBounds.TryGetValue(overlay.CharacterName, out Rectangle savedBounds))
        {
            overlay.SetThumbnailSize(savedBounds.Size);
            return;
        }

        OverlayState? state = FindOverlayState(overlay.CharacterName);
        if (state is not null)
        {
            overlay.SetThumbnailSize(RestoreBounds(state).Size);
        }
    }

    private void ScheduleSave()
    {
        if (isShuttingDown)
        {
            return;
        }

        saveDebounceTimer.Stop();
        saveDebounceTimer.Start();
    }

    private void SavePositions()
    {
        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            UpdateOverlayState(overlay);
        }

        SettingsManager.Save(settings);
        CaptureSavedOverlayBoundsSnapshot();
    }

    private void SavePositionsSafely()
    {
        try
        {
            SavePositions();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ETM: Failed to save thumbnail positions: {ex}");
        }
    }

    private void CaptureSavedOverlayBoundsSnapshot()
    {
        savedOverlayBounds.Clear();
        foreach (OverlayState state in activeProfile.Overlays)
        {
            if (!string.IsNullOrWhiteSpace(state.CharacterName))
            {
                savedOverlayBounds[state.CharacterName] = RestoreBounds(state);
            }
        }
    }

    private void UpdateOverlayState(ThumbnailOverlay overlay)
    {
        if (string.IsNullOrWhiteSpace(overlay.CharacterName))
        {
            return;
        }

        OverlayState? state = FindOverlayState(overlay.CharacterName);
        if (state is null)
        {
            state = new OverlayState { CharacterName = overlay.CharacterName };
            activeProfile.Overlays.Add(state);
        }

        SaveBounds(overlay.Bounds, state);
        state.CustomLabel = overlay.CustomLabel;
        state.Visible = overlay.Visible;
        state.Opacity = FromOpacityByte(overlay.ThumbnailOpacity);
        state.AspectRatioLocked = overlay.AspectRatioLocked;
    }

    private static IReadOnlyList<Screen> GetOrderedScreens()
    {
        return Screen.AllScreens.OrderBy(screen => screen.Bounds.Left).ToList();
    }

    private static void SaveBounds(Rectangle bounds, OverlayState state)
    {
        IReadOnlyList<Screen> screens = GetOrderedScreens();
        Point center = new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        int monitorIndex = 0;
        for (int i = 0; i < screens.Count; i++)
        {
            if (screens[i].Bounds.Contains(center))
            {
                monitorIndex = i;
                break;
            }
        }

        Rectangle monitorBounds = screens.Count == 0 ? Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty : screens[monitorIndex].Bounds;
        state.MonitorIndex = monitorIndex;
        state.X = bounds.Left - monitorBounds.Left;
        state.Y = bounds.Top - monitorBounds.Top;
        state.Width = bounds.Width;
        state.Height = bounds.Height;
    }

    private static Rectangle RestoreBounds(OverlayState state)
    {
        IReadOnlyList<Screen> screens = GetOrderedScreens();
        Screen? primary = Screen.PrimaryScreen;
        Rectangle monitorBounds = primary?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        if (state.MonitorIndex >= 0 && state.MonitorIndex < screens.Count)
        {
            monitorBounds = screens[state.MonitorIndex].Bounds;
        }

        int width = Math.Clamp(state.Width, MinimumOverlayWidth, Math.Min(MaximumOverlayWidth, monitorBounds.Width));
        int height = Math.Clamp(state.Height, MinimumOverlayHeight, Math.Min(MaximumOverlayHeight, monitorBounds.Height));

        if (state.Width > MaximumOverlayWidth || state.Height > MaximumOverlayHeight)
        {
            width = Math.Min(DefaultOverlayWidth, monitorBounds.Width);
            height = Math.Min(DefaultOverlayHeight, monitorBounds.Height);
        }

        int x = Math.Clamp(monitorBounds.Left + state.X, monitorBounds.Left, monitorBounds.Right - width);
        int y = Math.Clamp(monitorBounds.Top + state.Y, monitorBounds.Top, monitorBounds.Bottom - height);
        return new Rectangle(x, y, width, height);
    }

    private void OpenConfiguration()
    {
        if (configWindow is null || configWindow.IsDisposed)
        {
            configWindow = new ConfigWindow(settings, activeProfile, ApplySettingsChanges, SwitchActiveProfile);
        }

        configWindow.Show();
        configWindow.Activate();
    }

    private void SwitchActiveProfile(string profileName)
    {
        Profile? profile = settings.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        if (ReferenceEquals(activeProfile, profile))
        {
            settings.ActiveProfileName = activeProfile.Name;
            return;
        }

        SavePositions();
        try
        {
            isSwitchingProfile = true;
            activeProfile = profile;
            CaptureSavedOverlayBoundsSnapshot();
            settings.ActiveProfileName = profile.Name;
            thumbnailsLocked = activeProfile.ThumbnailsLocked;
            lockThumbnailsMenuItem.Checked = thumbnailsLocked;
            ApplyThumbnailLockState();
            RegisterHotkeys();
            ReloadThumbnails();
            RecreateConfigWindowIfOpen();
            SavePositions();
        }
        finally
        {
            isSwitchingProfile = false;
        }
    }

    private void RecreateConfigWindowIfOpen()
    {
        if (configWindow is null || configWindow.IsDisposed)
        {
            return;
        }

        bool wasVisible = configWindow.Visible;
        configWindow.Close();
        configWindow.Dispose();
        configWindow = null;

        if (wasVisible)
        {
            OpenConfiguration();
        }
    }

    private void ToggleAllOverlays()
    {
        overlaysVisible = !overlaysVisible;
        showHideAllMenuItem.Text = overlaysVisible ? "Hide All" : "Show All";

        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            overlay.Visible = overlaysVisible;
            UpdateOverlayState(overlay);
        }

        ScheduleSave();
    }

    private void ApplySettingsChanges()
    {
        SettingsManager.Save(settings);
        RegisterHotkeys();
        ApplyOverlaySettings();
    }

    private void ApplyOverlaySettings()
    {
        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            OverlayState? state = FindOverlayState(overlay.CharacterName);
            overlay.ApplySettings(state, activeProfile.Appearance);
            overlay.SetHotkeyGroups(GetHotkeyGroupMenuItems(overlay.CharacterName));
            overlay.SetLabelHotkey(GetLabelHotkey(overlay.CharacterName, state));
            overlay.Visible = overlaysVisible && (state?.Visible ?? true);
        }
    }

    private IReadOnlyList<ThumbnailOverlay.HotkeyGroupMenuItem> GetHotkeyGroupMenuItems(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return Array.Empty<ThumbnailOverlay.HotkeyGroupMenuItem>();
        }

        return activeProfile.HotkeyGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.Name))
            .Select(group => new ThumbnailOverlay.HotkeyGroupMenuItem(
                group.Name,
                group.CycleHotkey,
                group.CharacterNames.Any(candidate =>
                    string.Equals(candidate, characterName, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    private string GetLabelHotkey(string characterName, OverlayState? state)
    {
        if (!string.IsNullOrWhiteSpace(state?.DirectHotkey))
        {
            return state.DirectHotkey;
        }

        if (string.IsNullOrWhiteSpace(characterName))
        {
            return string.Empty;
        }

        List<string> groupHotkeys = activeProfile.HotkeyGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.CycleHotkey)
                && group.CharacterNames.Any(candidate =>
                    string.Equals(candidate, characterName, StringComparison.OrdinalIgnoreCase)))
            .Select(group => group.CycleHotkey.Trim())
            .Where(hotkey => !string.IsNullOrWhiteSpace(hotkey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(", ", groupHotkeys);
    }

    private void OverlayHotkeyGroupAssignmentChanged(object? sender, ThumbnailOverlay.HotkeyGroupAssignmentEventArgs e)
    {
        if (sender is not ThumbnailOverlay overlay
            || string.IsNullOrWhiteSpace(overlay.CharacterName)
            || string.IsNullOrWhiteSpace(e.GroupName))
        {
            return;
        }

        HotkeyGroup? group = activeProfile.HotkeyGroups.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, e.GroupName, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            return;
        }

        group.CharacterNames.RemoveAll(candidate =>
            string.IsNullOrWhiteSpace(candidate)
            || string.Equals(candidate, overlay.CharacterName, StringComparison.OrdinalIgnoreCase));
        if (e.Assigned)
        {
            group.CharacterNames.Add(overlay.CharacterName);
        }

        RegisterHotkeys();
        RefreshOverlayHotkeyLabels();
        SettingsManager.Save(settings);
    }

    private void RefreshOverlayHotkeyLabels()
    {
        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            OverlayState? state = FindOverlayState(overlay.CharacterName);
            overlay.SetHotkeyGroups(GetHotkeyGroupMenuItems(overlay.CharacterName));
            overlay.SetLabelHotkey(GetLabelHotkey(overlay.CharacterName, state));
        }
    }

    private void ApplyThumbnailLockState()
    {
        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            overlay.ThumbnailsLocked = thumbnailsLocked;
        }
    }

    private void RegisterHotkeys()
    {
        hotkeyManager.UnregisterAll();
        hotkeyManager.Register(
            string.IsNullOrWhiteSpace(settings.Global.ShowHideAllHotkey) ? "F12" : settings.Global.ShowHideAllHotkey,
            ToggleAllOverlays);

        foreach (OverlayState state in activeProfile.Overlays)
        {
            if (string.IsNullOrWhiteSpace(state.CharacterName) || string.IsNullOrWhiteSpace(state.DirectHotkey))
            {
                continue;
            }

            string characterName = state.CharacterName;
            hotkeyManager.Register(state.DirectHotkey, () => FocusOverlayForCharacter(characterName));
        }

        foreach (HotkeyGroup group in activeProfile.HotkeyGroups)
        {
            if (string.IsNullOrWhiteSpace(group.Name) || string.IsNullOrWhiteSpace(group.CycleHotkey) || group.CharacterNames.Count == 0)
            {
                continue;
            }

            string groupName = group.Name;
            hotkeyManager.Register(group.CycleHotkey, () => CycleHotkeyGroup(groupName));
        }
    }

    private void FocusOverlayForCharacter(string characterName)
    {
        ThumbnailOverlay? overlay = overlays.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.CharacterName, characterName, StringComparison.OrdinalIgnoreCase));
        if (overlay is null)
        {
            return;
        }

        SetActiveOverlayImmediately(overlay);
        overlay.FocusSourceWindow();
    }

    private void CycleHotkeyGroup(string groupName)
    {
        HotkeyGroup? group = activeProfile.HotkeyGroups.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, groupName, StringComparison.OrdinalIgnoreCase));
        if (group is null || group.CharacterNames.Count == 0)
        {
            return;
        }

        List<string> characterNames = group.CharacterNames
            .Where(characterName => !string.IsNullOrWhiteSpace(characterName))
            .ToList();
        if (characterNames.Count == 0)
        {
            return;
        }

        int startIndex = GetCycleStartIndex(characterNames);

        for (int offset = 0; offset < characterNames.Count; offset++)
        {
            int index = (startIndex + offset) % characterNames.Count;
            ThumbnailOverlay? overlay = overlays.Values.FirstOrDefault(candidate =>
                string.Equals(candidate.CharacterName, characterNames[index], StringComparison.OrdinalIgnoreCase));
            if (overlay is null)
            {
                continue;
            }

            SetActiveOverlayImmediately(overlay);
            overlay.FocusSourceWindow();
            return;
        }
    }

    private int GetCycleStartIndex(IReadOnlyList<string> characterNames)
    {
        string? currentCharacter = GetCurrentFocusedCharacterName();
        if (string.IsNullOrWhiteSpace(currentCharacter))
        {
            return 0;
        }

        int currentIndex = -1;
        for (int i = 0; i < characterNames.Count; i++)
        {
            if (string.Equals(characterNames[i], currentCharacter, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        return currentIndex < 0 ? 0 : (currentIndex + 1) % characterNames.Count;
    }

    private string? GetCurrentFocusedCharacterName()
    {
        IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();
        ThumbnailOverlay? foregroundOverlay = overlays.Values.FirstOrDefault(overlay =>
            overlay.SourceHandle == foregroundHwnd);
        if (foregroundOverlay is not null)
        {
            return foregroundOverlay.CharacterName;
        }

        return overlays.Values.FirstOrDefault(overlay => overlay.IsActiveClient)?.CharacterName;
    }

    private void ExitApplication()
    {
        isShuttingDown = true;
        refreshTimer.Stop();
        saveDebounceTimer.Stop();
        SavePositionsSafely();
        hotkeyManager.Dispose();
        hotkeyWindow.DestroyHandle();
        foregroundWatcher.Dispose();
        DisposeOverlays();
        trayIcon.Visible = false;
        ExitThread();
    }

    private void SystemEventsSessionEnding(object sender, SessionEndingEventArgs e)
    {
        isShuttingDown = true;
        saveDebounceTimer.Stop();
        SavePositionsSafely();
    }

    private void ApplicationApplicationExit(object? sender, EventArgs e)
    {
        isShuttingDown = true;
        saveDebounceTimer.Stop();
        SavePositionsSafely();
    }

    private void DisposeOverlays()
    {
        foreach (ThumbnailOverlay overlay in overlays.Values)
        {
            overlay.OverlayStateChanged -= OverlayStateChanged;
            overlay.SourceFocusRequested -= OverlaySourceFocusRequested;
            overlay.ResizeAllRequested -= OverlayResizeAllRequested;
            overlay.ResetSizeRequested -= OverlayResetSizeRequested;
            overlay.HotkeyGroupAssignmentChanged -= OverlayHotkeyGroupAssignmentChanged;
            overlay.Dispose();
        }

        overlays.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            isShuttingDown = true;
            SystemEvents.SessionEnding -= SystemEventsSessionEnding;
            Application.ApplicationExit -= ApplicationApplicationExit;
            refreshTimer.Dispose();
            saveDebounceTimer.Dispose();
            SavePositionsSafely();
            hotkeyManager.Dispose();
            hotkeyWindow.DestroyHandle();
            foregroundWatcher.Dispose();
            DisposeOverlays();
            trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class HotkeyMessageWindow : NativeWindow
    {
        internal event EventHandler<Message>? HotkeyPressed;

        internal HotkeyMessageWindow()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WM_HOTKEY)
            {
                HotkeyPressed?.Invoke(this, message);
                return;
            }

            base.WndProc(ref message);
        }
    }
}
