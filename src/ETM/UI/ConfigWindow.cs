using ETM.Persistence;
using ETM.UI.Tabs;

namespace ETM.UI;

internal sealed class ConfigWindow : Form
{
    private readonly AppSettings settings;
    private readonly Profile activeProfile;
    private readonly Action saveRequested;
    private readonly Action<string> activeProfileChanged;

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

        TabControl tabs = new()
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };
        UiTheme.StyleTabControl(tabs);

        AddTab(tabs, "Profiles", new ProfilesTab(settings, activeProfile, RequestSave, activeProfileChanged));
        AddTab(tabs, "Thumbnails", new ThumbnailsTab(activeProfile, RequestSave));
        AddTab(tabs, "Hotkeys", new HotkeysTab(settings, activeProfile, RequestSave));
        AddTab(tabs, "Appearance", new AppearanceTab(activeProfile, RequestSave));
        AddTab(tabs, "System", new SystemTab(settings, RequestSave));

        Controls.Add(tabs);
        UiTheme.Apply(this);
    }

    private static void AddTab(TabControl tabs, string title, Control content)
    {
        TabPage page = new(title);
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        tabs.TabPages.Add(page);
    }

    private void RequestSave()
    {
        saveRequested();
    }
}
