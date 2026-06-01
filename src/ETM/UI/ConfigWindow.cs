using System.Windows.Forms.Integration;
using ETM.Persistence;
using ETM.UI.Wpf;
using WinForms = System.Windows.Forms;

namespace ETM.UI;

internal sealed class ConfigWindow : WinForms.Form
{
    internal ConfigWindow(AppSettings settings, Profile activeProfile, Action saveRequested, Action<string> activeProfileChanged)
    {
        Text = "ETM Configuration";
        StartPosition = WinForms.FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 660);
        Size = new Size(1120, 760);
        BackColor = Color.FromArgb(15, 18, 24);

        ElementHost host = new()
        {
            Dock = WinForms.DockStyle.Fill,
            Child = new WpfConfigRoot(settings, activeProfile, saveRequested, activeProfileChanged)
        };

        Controls.Add(host);
    }
}
