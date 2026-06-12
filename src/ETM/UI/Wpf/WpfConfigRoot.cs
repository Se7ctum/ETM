using ETM.Core;
using ETM.Persistence;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ComboBox = System.Windows.Controls.ComboBox;
using FontFamily = System.Windows.Media.FontFamily;
using Grid = System.Windows.Controls.Grid;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Label = System.Windows.Controls.Label;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfBrushes = System.Windows.Media.Brushes;
using WinForms = System.Windows.Forms;

namespace ETM.UI.Wpf;

internal sealed class WpfConfigRoot : UserControl
{
    private static readonly Brush Bg = Brush("#0f1218");
    private static readonly Brush Sidebar = Brush("#171c24");
    private static readonly Brush Panel = Brush("#202733");
    private static readonly Brush PanelSoft = Brush("#151a22");
    private static readonly Brush Text = Brush("#eef3f8");
    private static readonly Brush Muted = Brush("#9ba9b7");
    private static readonly Brush Accent = Brush("#38a6ff");
    private static readonly Brush Border = Brush("#303a49");
    private static readonly Brush SavePending = Brush("#c73535");

    private readonly AppSettings settings;
    private readonly Profile profile;
    private readonly Action saveRequested;
    private readonly Action<string> activeProfileChanged;
    private readonly ContentControl content = new();
    private readonly List<Button> navigationButtons = new();
    private Button? currentSaveButton;
    private bool hasUnsavedChanges;
    private Action? flushCurrentPage;

    internal WpfConfigRoot(AppSettings settings, Profile profile, Action saveRequested, Action<string> activeProfileChanged)
    {
        this.settings = settings;
        this.profile = profile;
        this.saveRequested = saveRequested;
        this.activeProfileChanged = activeProfileChanged;

        FontFamily = new FontFamily("Segoe UI");
        Background = Bg;
        Content = BuildShell();
        Navigate(navigationButtons[0], BuildProfilesPage);
    }

    private Grid BuildShell()
    {
        Grid shell = new() { Background = Bg };
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Border sidebar = new()
        {
            Background = Sidebar,
            Padding = new Thickness(18, 22, 18, 22)
        };
        StackPanel sidebarStack = new();
        sidebarStack.Children.Add(new TextBlock
        {
            Text = "ETM",
            Foreground = Text,
            FontSize = 28,
            FontWeight = FontWeights.SemiBold
        });
        sidebarStack.Children.Add(new TextBlock
        {
            Text = "Configuration",
            Foreground = Muted,
            Margin = new Thickness(0, 0, 0, 26)
        });

        sidebarStack.Children.Add(NavButton("Profiles", BuildProfilesPage));
        sidebarStack.Children.Add(NavButton("Thumbnails", BuildThumbnailsPage));
        sidebarStack.Children.Add(NavButton("Hotkeys", BuildHotkeysPage));
        sidebarStack.Children.Add(NavButton("Appearance", BuildAppearancePage));
        sidebarStack.Children.Add(NavButton("System", BuildSystemPage));
        sidebar.Child = sidebarStack;
        shell.Children.Add(sidebar);

        Border contentSurface = new()
        {
            Background = Bg,
            Padding = new Thickness(28)
        };
        contentSurface.Child = content;
        Grid.SetColumn(contentSurface, 1);
        shell.Children.Add(contentSurface);
        return shell;
    }

    private Button NavButton(string text, Func<UIElement> pageFactory)
    {
        Button button = new()
        {
            Content = text,
            Height = 44,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(14, 0, 14, 0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Sidebar,
            Foreground = Muted,
            BorderBrush = WpfBrushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 14
        };
        button.Click += (_, _) => Navigate(button, pageFactory);
        navigationButtons.Add(button);
        return button;
    }

    private void Navigate(Button selectedButton, Func<UIElement> pageFactory)
    {
        foreach (Button button in navigationButtons)
        {
            bool selected = ReferenceEquals(button, selectedButton);
            button.Background = selected ? Panel : Sidebar;
            button.Foreground = selected ? Text : Muted;
        }

        flushCurrentPage = null;
        content.Content = pageFactory();
        RefreshSaveButton();
    }

    private void MarkDirty()
    {
        hasUnsavedChanges = true;
        RefreshSaveButton();
    }

    private void SaveChanges()
    {
        flushCurrentPage?.Invoke();
        saveRequested();
        hasUnsavedChanges = false;
        ShowSaveFeedback();
    }

    private Button CreateSaveButton()
    {
        Button button = ActionButton("Save", SaveChanges);
        button.MinWidth = 108;
        button.Height = 38;
        currentSaveButton = button;
        RefreshSaveButton();
        return button;
    }

    private void RefreshSaveButton()
    {
        if (currentSaveButton is null)
        {
            return;
        }

        currentSaveButton.Background = hasUnsavedChanges ? SavePending : Panel;
        currentSaveButton.Foreground = Text;
    }

    private void ShowSaveFeedback()
    {
        if (currentSaveButton is null)
        {
            return;
        }

        currentSaveButton.Content = "Saved";
        currentSaveButton.Background = Accent;
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(650) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (currentSaveButton is not null)
            {
                currentSaveButton.Content = "Save";
                RefreshSaveButton();
            }
        };
        timer.Start();
    }

    private UIElement BuildProfilesPage()
    {
        Grid grid = Page("Profiles", "Create layouts and rules for when ETM should switch profiles.");
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        ListBox profiles = new()
        {
            Background = PanelSoft,
            Foreground = Text,
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };
        foreach (Profile item in settings.Profiles)
        {
            profiles.Items.Add(item.Name);
        }
        profiles.SelectedItem = settings.ActiveProfileName;
        Grid.SetRow(profiles, 1);
        grid.Children.Add(profiles);

        StackPanel details = new();
        Grid.SetColumn(details, 2);
        Grid.SetRow(details, 1);
        grid.Children.Add(details);

        TextBlock active = SectionTitle($"Active profile: {settings.ActiveProfileName}");
        details.Children.Add(active);
        TextBox characters = TextBox();
        IntegerBox count = new(0, 64);

        void LoadSelected()
        {
            Profile? selected = SelectedProfile(profiles);
            characters.Text = selected is null ? string.Empty : string.Join(Environment.NewLine, selected.AutoLoadCharacters);
            count.Value = selected?.AutoLoadClientCount ?? 0;
        }

        details.Children.Add(Help("Profile activation rules", "If these characters are active, ETM switches to this profile. Add one character per line. Client count is optional."));
        details.Children.Add(Label("If these characters are active"));
        characters.Height = 150;
        characters.AcceptsReturn = true;
        characters.TextWrapping = TextWrapping.NoWrap;
        characters.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        bool loadingProfile = false;
        profiles.SelectionChanged += (_, _) =>
        {
            loadingProfile = true;
            LoadSelected();
            loadingProfile = false;
        };
        loadingProfile = true;
        LoadSelected();
        loadingProfile = false;
        characters.TextChanged += (_, _) =>
        {
            if (!loadingProfile && SelectedProfile(profiles) is { } selected)
            {
                selected.AutoLoadCharacters = SplitLines(characters.Text);
                MarkDirty();
            }
        };
        details.Children.Add(characters);
        details.Children.Add(Label("Also require client count"));
        count.Changed += value =>
        {
            if (!loadingProfile && SelectedProfile(profiles) is { } selected)
            {
                selected.AutoLoadClientCount = value == 0 ? null : value;
                MarkDirty();
            }
        };
        details.Children.Add(count.Control);

        WrapPanel actions = new() { Margin = new Thickness(0, 18, 0, 0) };
        actions.Children.Add(ActionButton("New", () =>
        {
            string name = UniqueProfileName();
            settings.Profiles.Add(new Profile { Name = name });
            profiles.Items.Add(name);
            profiles.SelectedItem = name;
            MarkDirty();
        }));
        actions.Children.Add(ActionButton("Set active", () =>
        {
            if (profiles.SelectedItem is string name)
            {
                settings.ActiveProfileName = name;
                active.Text = $"Active profile: {settings.ActiveProfileName}";
                activeProfileChanged(name);
                MarkDirty();
            }
        }));
        actions.Children.Add(ActionButton("Duplicate", () =>
        {
            if (SelectedProfile(profiles) is not { } selected)
            {
                return;
            }

            Profile clone = CloneProfile(selected, selected.Name + " Copy");
            settings.Profiles.Add(clone);
            profiles.Items.Add(clone.Name);
            profiles.SelectedItem = clone.Name;
            MarkDirty();
        }));
        actions.Children.Add(ActionButton("Delete", () =>
        {
            if (profiles.SelectedIndex < 0 || settings.Profiles.Count <= 1)
            {
                return;
            }

            string removed = settings.Profiles[profiles.SelectedIndex].Name;
            settings.Profiles.RemoveAt(profiles.SelectedIndex);
            profiles.Items.Remove(removed);
            profiles.SelectedIndex = 0;
            if (settings.ActiveProfileName.Equals(removed, StringComparison.OrdinalIgnoreCase))
            {
                settings.ActiveProfileName = settings.Profiles[0].Name;
                activeProfileChanged(settings.ActiveProfileName);
            }
            MarkDirty();
        }));
        actions.Children.Add(ActionButton("Export", () => ExportProfile(profiles)));
        actions.Children.Add(ActionButton("Import", () => ImportProfile(profiles)));
        details.Children.Add(actions);

        return grid;
    }

    private UIElement BuildThumbnailsPage()
    {
        Grid page = Page("Thumbnails", "Per-character thumbnail visibility, opacity, hotkey, and aspect ratio.");
        ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        StackPanel stack = new();
        scroll.Content = stack;
        Grid.SetRow(scroll, 1);
        page.Children.Add(scroll);

        Grid header = ThumbnailGrid();
        header.Margin = new Thickness(0, 0, 0, 6);
        header.Children.Add(HeaderCell("Character", 0));
        header.Children.Add(HeaderCell("Direct hotkey", 1));
        header.Children.Add(HeaderCell("Opacity %", 2));
        header.Children.Add(HeaderCell("Visible", 3));
        header.Children.Add(HeaderCell("Lock ratio", 4));
        stack.Children.Add(header);

        foreach (OverlayState overlay in profile.Overlays.OrderBy(overlay => overlay.CharacterName))
        {
            if (overlay.CharacterName.Equals("EVE Launcher", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Grid row = CardGrid();
            TextBlock characterName = new()
            {
                Text = overlay.CharacterName,
                Foreground = Text,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(12, 0, 8, 0)
            };
            row.Children.Add(characterName);
            TextBox hotkey = TextBox(overlay.DirectHotkey ?? string.Empty);
            hotkey.TextChanged += (_, _) => { overlay.DirectHotkey = hotkey.Text.Trim(); MarkDirty(); };
            AddCell(row, hotkey, 1);
            IntegerBox opacity = new(0, 100, Math.Clamp((int)Math.Round(overlay.Opacity * 100), 0, 100));
            opacity.Changed += value => { overlay.Opacity = value / 100f; MarkDirty(); };
            AddCell(row, opacity.Control, 2);
            CheckBox visible = Check("Visible", overlay.Visible, value => { overlay.Visible = value; MarkDirty(); });
            AddCell(row, visible, 3);
            CheckBox ratio = Check("Ratio", overlay.AspectRatioLocked, value => { overlay.AspectRatioLocked = value; MarkDirty(); });
            AddCell(row, ratio, 4);
            stack.Children.Add(row);
        }

        return page;
    }

    private UIElement BuildHotkeysPage()
    {
        Grid page = Page("Hotkeys", "Capture cycle hotkeys and define which characters each group cycles through.");
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(grid, 1);
        page.Children.Add(grid);

        StackPanel left = new();
        TextBox global = TextBox(settings.Global.ShowHideAllHotkey);
        global.IsReadOnly = true;
        AttachHotkeyCapture(global, hotkey =>
        {
            settings.Global.ShowHideAllHotkey = hotkey;
            MarkDirty();
        });
        left.Children.Add(Label("Show / hide all"));
        left.Children.Add(global);
        ListBox groups = new() { Margin = new Thickness(0, 18, 0, 10), Background = PanelSoft, Foreground = Text, BorderBrush = Border };
        foreach (HotkeyGroup group in profile.HotkeyGroups)
        {
            groups.Items.Add(string.IsNullOrWhiteSpace(group.Name) ? "(unnamed group)" : group.Name);
        }
        left.Children.Add(groups);
        left.Children.Add(ActionButton("New group", () =>
        {
            HotkeyGroup group = new() { Name = UniqueGroupName() };
            profile.HotkeyGroups.Add(group);
            groups.Items.Add(group.Name);
            groups.SelectedIndex = groups.Items.Count - 1;
            MarkDirty();
        }));
        left.Children.Add(ActionButton("Delete group", () =>
        {
            if (groups.SelectedIndex >= 0)
            {
                profile.HotkeyGroups.RemoveAt(groups.SelectedIndex);
                groups.Items.RemoveAt(groups.SelectedIndex);
                MarkDirty();
            }
        }));
        grid.Children.Add(left);

        StackPanel details = new();
        Grid.SetColumn(details, 2);
        grid.Children.Add(details);
        TextBox groupName = TextBox();
        TextBox cycle = TextBox();
        cycle.IsReadOnly = true;
        TextBox characters = TextBox();
        characters.AcceptsReturn = true;
        characters.Height = 190;
        characters.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        bool loadingGroup = false;

        void LoadGroup()
        {
            loadingGroup = true;
            HotkeyGroup? selected = SelectedGroup(groups);
            groupName.Text = selected?.Name ?? string.Empty;
            cycle.Text = selected?.CycleHotkey ?? string.Empty;
            characters.Text = selected is null ? string.Empty : string.Join(Environment.NewLine, selected.CharacterNames);
            loadingGroup = false;
        }

        groups.SelectionChanged += (_, _) => LoadGroup();
        if (groups.Items.Count > 0)
        {
            groups.SelectedIndex = 0;
        }
        LoadGroup();

        groupName.TextChanged += (_, _) =>
        {
            if (!loadingGroup && SelectedGroup(groups) is { } group)
            {
                group.Name = groupName.Text.Trim();
                groups.Items[groups.SelectedIndex] = string.IsNullOrWhiteSpace(group.Name) ? "(unnamed group)" : group.Name;
                MarkDirty();
            }
        };
        AttachHotkeyCapture(cycle, hotkey =>
        {
            if (SelectedGroup(groups) is { } group)
            {
                group.CycleHotkey = hotkey;
                MarkDirty();
            }
        });
        characters.TextChanged += (_, _) =>
        {
            if (!loadingGroup && SelectedGroup(groups) is { } group)
            {
                group.CharacterNames = SplitLines(characters.Text);
                MarkDirty();
            }
        };

        details.Children.Add(Label("Group name"));
        details.Children.Add(groupName);
        details.Children.Add(Label("Cycle hotkey"));
        details.Children.Add(cycle);
        details.Children.Add(Label("Characters"));
        details.Children.Add(characters);
        return page;
    }

    private UIElement BuildAppearancePage()
    {
        AppearanceDefaults appearance = profile.Appearance;
        Grid page = Page("Appearance", "Keep the overlay clean and tune label readability.");
        StackPanel stack = new();
        stack.Children.Add(ColorRow("Border color", appearance.BorderColor, value => appearance.BorderColor = value));
        stack.Children.Add(ColorRow("Active border color", appearance.ActiveBorderColor, value => appearance.ActiveBorderColor = value));
        stack.Children.Add(NumberRow("Border width", appearance.BorderWidth, 0, 16, value => appearance.BorderWidth = value));
        stack.Children.Add(ColorRow("Label color", appearance.LabelColor, value => appearance.LabelColor = value));
        stack.Children.Add(Check("Show hotkey in label", appearance.ShowHotkeyInLabel, value => { appearance.ShowHotkeyInLabel = value; MarkDirty(); }));
        stack.Children.Add(ComboRow("Label font", appearance.LabelFont, ["Segoe UI Semibold", "Segoe UI", "Arial", "Calibri", "Tahoma", "Verdana", "Consolas", "Trebuchet MS", "Microsoft Sans Serif", "Georgia", "Times New Roman"], value => appearance.LabelFont = value));
        stack.Children.Add(NumberRow("Label font size", appearance.LabelFontSize, 6, 32, value => appearance.LabelFontSize = value));
        stack.Children.Add(ComboRow("Label position", appearance.LabelPosition, ["TopLeft", "TopRight", "BottomLeft", "BottomRight"], value => appearance.LabelPosition = value));
        ScrollViewer scroll = Scroll(stack);
        Grid.SetRow(scroll, 1);
        page.Children.Add(scroll);
        return page;
    }

    private UIElement BuildSystemPage()
    {
        GlobalSettings global = settings.Global;
        Grid page = Page("System", "Startup and snapping behavior.");
        StackPanel stack = new();
        bool startupEnabled = global.LaunchOnStartup;
        try
        {
            startupEnabled = StartupManager.IsEnabled();
            global.LaunchOnStartup = startupEnabled;
        }
        catch (Exception)
        {
        }

        stack.Children.Add(Check("Launch on startup", startupEnabled, value =>
        {
            try
            {
                StartupManager.SetLaunchOnStartup(value);
                global.LaunchOnStartup = value;
                MarkDirty();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to update startup setting: {ex.Message}", "ETM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }));
        stack.Children.Add(Check("Snap to edges", global.SnapToEdges, value => { global.SnapToEdges = value; MarkDirty(); }));
        stack.Children.Add(NumberRow("Snap threshold", global.SnapThreshold, 0, 64, value => global.SnapThreshold = value));
        stack.Children.Add(Check("Snap to grid", global.SnapToGrid, value => { global.SnapToGrid = value; MarkDirty(); }));
        stack.Children.Add(NumberRow("Grid size", global.GridSize, 1, 200, value => global.GridSize = value));
        ScrollViewer scroll = Scroll(stack);
        Grid.SetRow(scroll, 1);
        page.Children.Add(scroll);
        return page;
    }

    private Grid Page(string title, string subtitle)
    {
        Grid grid = new();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid header = new() { Margin = new Thickness(0, 0, 0, 22) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        StackPanel titleStack = new();
        titleStack.Children.Add(new TextBlock { Text = title, Foreground = Text, FontSize = 26, FontWeight = FontWeights.SemiBold });
        titleStack.Children.Add(new TextBlock { Text = subtitle, Foreground = Muted, Margin = new Thickness(0, 4, 0, 0) });
        header.Children.Add(titleStack);
        Button save = CreateSaveButton();
        save.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(save, 1);
        header.Children.Add(save);
        Grid.SetColumnSpan(header, 20);
        grid.Children.Add(header);
        return grid;
    }

    private static ScrollViewer Scroll(UIElement child) => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

    private static Brush Brush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    private static TextBlock SectionTitle(string text) => new() { Text = text, Foreground = Text, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) };

    private static TextBlock Label(string text) => new() { Text = text, Foreground = Muted, Margin = new Thickness(0, 14, 0, 6) };

    private static TextBlock Help(string title, string body) => new()
    {
        Text = $"{title}\n{body}",
        Foreground = Muted,
        Margin = new Thickness(0, 8, 0, 14)
    };

    private static TextBox TextBox(string value = "") => new()
    {
        Text = value,
        Background = PanelSoft,
        Foreground = Text,
        BorderBrush = Border,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 7, 10, 7),
        CaretBrush = Text
    };

    private Button ActionButton(string text, Action action)
    {
        Button button = new()
        {
            Content = text,
            MinWidth = 96,
            Height = 36,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(14, 0, 14, 0),
            Background = Panel,
            Foreground = Text,
            BorderBrush = Border
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Grid CardGrid()
    {
        Grid grid = ThumbnailGrid();
        grid.Background = PanelSoft;
        grid.Margin = new Thickness(0, 0, 0, 8);
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
        return grid;
    }

    private static Grid ThumbnailGrid()
    {
        Grid grid = new()
        {
            Margin = new Thickness(0)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        return grid;
    }

    private static TextBlock HeaderCell(string text, int column)
    {
        TextBlock block = new()
        {
            Text = text,
            Foreground = Muted,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(column == 0 ? 12 : 6, 0, 6, 0)
        };
        Grid.SetColumn(block, column);
        return block;
    }

    private static void AddCell(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private CheckBox Check(string text, bool value, Action<bool> setter)
    {
        CheckBox check = new()
        {
            Content = text,
            IsChecked = value,
            Foreground = Text,
            Margin = new Thickness(0, 8, 0, 8)
        };
        check.Checked += (_, _) => setter(true);
        check.Unchecked += (_, _) => setter(false);
        return check;
    }

    private UIElement NumberRow(string label, int value, int min, int max, Action<int> setter)
    {
        IntegerBox box = new(min, max, value);
        box.Changed += next => { setter(next); MarkDirty(); };
        flushCurrentPage += () => setter(box.Value);
        return Row(label, box.Control);
    }

    private UIElement ComboRow(string label, string value, string[] options, Action<string> setter)
    {
        ComboBox combo = new()
        {
            ItemsSource = options,
            SelectedItem = options.Contains(value) ? value : options[0],
            Width = 220,
            Background = PanelSoft,
            Foreground = Text,
            BorderBrush = Border
        };
        combo.Resources[System.Windows.SystemColors.WindowBrushKey] = PanelSoft;
        combo.Resources[System.Windows.SystemColors.ControlBrushKey] = PanelSoft;
        combo.Resources[System.Windows.SystemColors.HighlightBrushKey] = Accent;
        combo.Resources[System.Windows.SystemColors.HighlightTextBrushKey] = Text;
        combo.SelectionChanged += (_, _) =>
        {
            setter(combo.SelectedItem?.ToString() ?? options[0]);
            MarkDirty();
        };
        return Row(label, combo);
    }

    private UIElement ColorRow(string label, string value, Action<string> setter)
    {
        string currentColor = value;
        Button? button = null;
        button = ActionButton(value, () =>
        {
            using WinForms.ColorDialog dialog = new() { FullOpen = true };
            try
            {
                dialog.Color = System.Drawing.ColorTranslator.FromHtml(currentColor);
            }
            catch
            {
                dialog.Color = System.Drawing.Color.White;
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                string color = System.Drawing.ColorTranslator.ToHtml(dialog.Color);
                currentColor = color;
                button!.Content = color;
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                button.Foreground = GetReadableBrush(dialog.Color);
                setter(color);
                MarkDirty();
            }
        });
        try
        {
            System.Drawing.Color parsed = System.Drawing.ColorTranslator.FromHtml(value);
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            button.Foreground = GetReadableBrush(parsed);
        }
        catch
        {
            button.Background = Panel;
            button.Foreground = Text;
        }

        return Row(label, button);
    }

    private static Brush GetReadableBrush(System.Drawing.Color color)
    {
        double brightness = (color.R * 0.299) + (color.G * 0.587) + (color.B * 0.114);
        return brightness > 150 ? WpfBrushes.Black : WpfBrushes.White;
    }

    private static UIElement Row(string label, UIElement control)
    {
        Grid row = new() { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock { Text = label, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private Profile? SelectedProfile(ListBox list)
    {
        return list.SelectedIndex >= 0 && list.SelectedIndex < settings.Profiles.Count ? settings.Profiles[list.SelectedIndex] : null;
    }

    private HotkeyGroup? SelectedGroup(ListBox list)
    {
        return list.SelectedIndex >= 0 && list.SelectedIndex < profile.HotkeyGroups.Count ? profile.HotkeyGroups[list.SelectedIndex] : null;
    }

    private string UniqueProfileName()
    {
        int index = settings.Profiles.Count + 1;
        string name;
        do
        {
            name = $"Profile {index++}";
        }
        while (settings.Profiles.Any(profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        return name;
    }

    private string UniqueGroupName()
    {
        int index = profile.HotkeyGroups.Count + 1;
        string name;
        do
        {
            name = $"Group {index++}";
        }
        while (profile.HotkeyGroups.Any(group => group.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        return name;
    }

    private void ExportProfile(ListBox profiles)
    {
        if (SelectedProfile(profiles) is not { } selected)
        {
            return;
        }

        SaveFileDialog dialog = new() { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", FileName = selected.Name + ".json" };
        if (dialog.ShowDialog() == true)
        {
            SettingsManager.ExportProfile(selected, dialog.FileName);
        }
    }

    private void ImportProfile(ListBox profiles)
    {
        OpenFileDialog dialog = new() { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            Profile imported = SettingsManager.ImportProfile(dialog.FileName);
            settings.Profiles.Add(imported);
            profiles.Items.Add(imported.Name);
            profiles.SelectedItem = imported.Name;
            MarkDirty();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Profile import failed: {ex.Message}", "ETM", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            Appearance = source.Appearance,
            ThumbnailsLocked = source.ThumbnailsLocked
        };
    }

    private static List<string> SplitLines(string text)
    {
        return text.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static void AttachHotkeyCapture(TextBox box, Action<string> apply)
    {
        box.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            if (e.Key is Key.Back or Key.Delete or Key.Escape)
            {
                box.Clear();
                apply(string.Empty);
                return;
            }

            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            {
                return;
            }

            string hotkey = FormatHotkey(Keyboard.Modifiers, e.Key == Key.System ? e.SystemKey : e.Key);
            if (!HotkeyManager.TryParseHotkey(hotkey, out int _, out int _))
            {
                return;
            }

            box.Text = hotkey;
            apply(hotkey);
        };
    }

    private static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        List<string> parts = new();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        parts.Add(GetKeyName(key));
        return string.Join("+", parts);
    }

    private static string GetKeyName(Key key)
    {
        return key switch
        {
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemSemicolon => "Semicolon",
            Key.OemQuotes => "Quote",
            Key.OemQuestion => "Slash",
            Key.OemBackslash => "Backslash",
            Key.OemMinus => "Minus",
            Key.OemPlus => "Equals",
            Key.Oem3 => "Backtick",
            Key.OemOpenBrackets => "LeftBracket",
            Key.OemCloseBrackets => "RightBracket",
            _ => key.ToString()
        };
    }

    private sealed class IntegerBox
    {
        private readonly TextBox box;
        private readonly int min;
        private readonly int max;

        internal IntegerBox(int min, int max, int value = 0)
        {
            this.min = min;
            this.max = max;
            box = TextBox(Math.Clamp(value, min, max).ToString());
            box.Width = 86;
            box.TextChanged += (_, _) =>
            {
                if (int.TryParse(box.Text, out int value) && value >= this.min && value <= this.max)
                {
                    Changed?.Invoke(value);
                }
            };
            box.LostFocus += (_, _) => Normalize();
        }

        internal TextBox Control => box;

        internal int Value
        {
            get => int.TryParse(box.Text, out int value) ? Math.Clamp(value, min, max) : min;
            set => box.Text = Math.Clamp(value, min, max).ToString();
        }

        internal event Action<int>? Changed;

        private void Normalize()
        {
            Value = Value;
            Changed?.Invoke(Value);
        }
    }
}
