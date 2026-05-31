using ETM.Persistence;
using ETM.UI.Tabs;

namespace ETM.UI;

internal sealed class ConfigWindow : Form
{
    private readonly AppSettings settings;
    private readonly Profile activeProfile;
    private readonly Action saveRequested;
    private readonly Action<string> activeProfileChanged;
    private readonly Panel contentHost = new();
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
            Padding = new Padding(0)
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Panel sidebar = BuildSidebar();
        contentHost.Dock = DockStyle.Fill;
        contentHost.BackColor = UiTheme.Background;
        contentHost.Padding = new Padding(24);

        shell.Controls.Add(sidebar, 0, 0);
        shell.Controls.Add(contentHost, 1, 0);

        Controls.Add(shell);
        UiTheme.Apply(this);
        ShowPage(navigationButtons[0], new ProfilesTab(settings, activeProfile, RequestSave, activeProfileChanged));
    }

    private Panel BuildSidebar()
    {
        Panel sidebar = new()
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Padding = new Padding(14, 18, 14, 18)
        };

        Label title = new()
        {
            Text = "ETM",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = UiTheme.Text
        };
        Label subtitle = new()
        {
            Text = "Configuration",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = UiTheme.MutedText
        };

        FlowLayoutPanel nav = new()
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 22, 0, 0)
        };

        nav.Controls.Add(CreateNavButton("Profiles", () => new ProfilesTab(settings, activeProfile, RequestSave, activeProfileChanged)));
        nav.Controls.Add(CreateNavButton("Thumbnails", () => new ThumbnailsTab(activeProfile, RequestSave)));
        nav.Controls.Add(CreateNavButton("Hotkeys", () => new HotkeysTab(settings, activeProfile, RequestSave)));
        nav.Controls.Add(CreateNavButton("Appearance", () => new AppearanceTab(activeProfile, RequestSave)));
        nav.Controls.Add(CreateNavButton("System", () => new SystemTab(settings, RequestSave)));

        sidebar.Controls.Add(nav);
        sidebar.Controls.Add(subtitle);
        sidebar.Controls.Add(title);
        return sidebar;
    }

    private Button CreateNavButton(string text, Func<Control> pageFactory)
    {
        Button button = new()
        {
            Text = text,
            Width = 188,
            Height = 44,
            Margin = new Padding(0, 0, 0, 8),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.Text
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = UiTheme.SurfaceRaised;
        button.FlatAppearance.MouseDownBackColor = UiTheme.SurfaceRaised;
        button.Click += (_, _) => ShowPage(button, pageFactory());
        navigationButtons.Add(button);
        return button;
    }

    private void ShowPage(Button selectedButton, Control content)
    {
        foreach (Button button in navigationButtons)
        {
            button.BackColor = ReferenceEquals(button, selectedButton) ? UiTheme.SurfaceRaised : UiTheme.Surface;
            button.ForeColor = ReferenceEquals(button, selectedButton) ? Color.White : UiTheme.MutedText;
        }

        foreach (Control control in contentHost.Controls)
        {
            control.Dispose();
        }

        contentHost.Controls.Clear();
        content.Dock = DockStyle.Fill;
        contentHost.Controls.Add(content);
        UiTheme.Apply(contentHost);
    }

    private void RequestSave()
    {
        saveRequested();
    }
}
