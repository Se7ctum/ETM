using ETM.Persistence;
using ETM.UI.Tabs;

namespace ETM.UI;

internal sealed class ConfigWindow : Form
{
    private readonly AppSettings settings;
    private readonly Profile activeProfile;
    private readonly Action saveRequested;
    private readonly Action<string> activeProfileChanged;
    private readonly Panel contentHost = new BufferedPanel();
    private readonly List<Button> navigationButtons = new();

    internal ConfigWindow(AppSettings settings, Profile activeProfile, Action saveRequested, Action<string> activeProfileChanged)
    {
        this.settings = settings;
        this.activeProfile = activeProfile;
        this.saveRequested = saveRequested;
        this.activeProfileChanged = activeProfileChanged;

        Text = "ETM Configuration";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 620);
        Size = new Size(1040, 720);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;

        TableLayoutPanel shell = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0),
            BackColor = UiTheme.Background
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Panel sidebar = BuildSidebar();
        contentHost.Dock = DockStyle.Fill;
        contentHost.Margin = new Padding(0);
        contentHost.Padding = new Padding(24);
        contentHost.BackColor = UiTheme.Background;

        shell.Controls.Add(sidebar, 0, 0);
        shell.Controls.Add(contentHost, 1, 0);

        Controls.Add(shell);
        UiTheme.Apply(this);
        LoadPage(navigationButtons[0], CreateProfilesPage);
    }

    private Panel BuildSidebar()
    {
        Panel sidebar = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(16, 18, 16, 18),
            BackColor = UiTheme.Surface
        };

        Label title = new()
        {
            Text = "ETM",
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = UiTheme.Text
        };
        Label subtitle = new()
        {
            Text = "Configuration",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = UiTheme.MutedText
        };
        FlowLayoutPanel navigation = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 22, 0, 0),
            Margin = new Padding(0),
            BackColor = UiTheme.Surface
        };

        navigation.Controls.Add(CreateNavigationButton("Profiles", CreateProfilesPage));
        navigation.Controls.Add(CreateNavigationButton("Thumbnails", () => new ThumbnailsTab(activeProfile, RequestSave)));
        navigation.Controls.Add(CreateNavigationButton("Hotkeys", () => new HotkeysTab(settings, activeProfile, RequestSave)));
        navigation.Controls.Add(CreateNavigationButton("Appearance", () => new AppearanceTab(activeProfile, RequestSave)));
        navigation.Controls.Add(CreateNavigationButton("System", () => new SystemTab(settings, RequestSave)));

        sidebar.Controls.Add(navigation);
        sidebar.Controls.Add(subtitle);
        sidebar.Controls.Add(title);
        return sidebar;
    }

    private ProfilesTab CreateProfilesPage()
    {
        return new ProfilesTab(settings, activeProfile, RequestSave, activeProfileChanged);
    }

    private Button CreateNavigationButton(string text, Func<Control> pageFactory)
    {
        Button button = new()
        {
            Text = text,
            Width = 188,
            Height = 44,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(14, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.MutedText
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = UiTheme.SurfaceRaised;
        button.FlatAppearance.MouseDownBackColor = UiTheme.SurfaceRaised;
        button.Click += (_, _) => LoadPage(button, pageFactory);
        navigationButtons.Add(button);
        return button;
    }

    private void LoadPage(Button selectedButton, Func<Control> pageFactory)
    {
        SuspendLayout();
        contentHost.SuspendLayout();

        try
        {
            foreach (Button button in navigationButtons)
            {
                bool selected = ReferenceEquals(button, selectedButton);
                button.BackColor = selected ? UiTheme.SurfaceRaised : UiTheme.Surface;
                button.ForeColor = selected ? UiTheme.Text : UiTheme.MutedText;
            }

            Control? oldPage = contentHost.Controls.Count > 0 ? contentHost.Controls[0] : null;
            contentHost.Controls.Clear();
            oldPage?.Dispose();

            Control page = pageFactory();
            page.Dock = DockStyle.Fill;
            page.Margin = new Padding(0);
            contentHost.Controls.Add(page);
            UiTheme.Apply(page);
        }
        finally
        {
            contentHost.ResumeLayout(true);
            ResumeLayout(true);
        }
    }

    private void RequestSave()
    {
        saveRequested();
    }

    private sealed class BufferedPanel : Panel
    {
        internal BufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
