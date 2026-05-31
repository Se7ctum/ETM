using System.Text.Json;

namespace ETM.Persistence;

internal static class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    internal static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ETM");

    internal static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    internal static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return CreateDefaultSettings();
        }

        try
        {
            string json = File.ReadAllText(SettingsPath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Normalize(settings ?? CreateDefaultSettings());
        }
        catch (JsonException)
        {
            return CreateDefaultSettings();
        }
        catch (Exception)
        {
            return CreateDefaultSettings();
        }
    }

    internal static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        AppSettings normalized = Normalize(settings);
        string json = JsonSerializer.Serialize(normalized, JsonOptions);
        string tempPath = SettingsPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsPath, overwrite: true);
    }

    internal static Profile ImportProfile(string path)
    {
        string json = File.ReadAllText(path);
        Profile? profile = JsonSerializer.Deserialize<Profile>(json, JsonOptions);
        return NormalizeProfile(profile ?? throw new InvalidOperationException("Profile file did not contain a valid profile."));
    }

    internal static void ExportProfile(Profile profile, string path)
    {
        string json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(path, json);
    }

    internal static AppSettings CreateDefaultSettings()
    {
        AppSettings settings = new();
        settings.Profiles.Add(new Profile { Name = settings.ActiveProfileName });
        return settings;
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.Global ??= new GlobalSettings();
        settings.Profiles ??= new List<Profile>();

        if (string.IsNullOrWhiteSpace(settings.ActiveProfileName))
        {
            settings.ActiveProfileName = "Default";
        }

        if (settings.Profiles.Count == 0)
        {
            settings.Profiles.Add(new Profile { Name = settings.ActiveProfileName });
        }

        if (!settings.Profiles.Any(profile => string.Equals(profile.Name, settings.ActiveProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            settings.ActiveProfileName = settings.Profiles[0].Name;
        }

        foreach (Profile profile in settings.Profiles)
        {
            NormalizeProfile(profile);
        }

        return settings;
    }

    private static Profile NormalizeProfile(Profile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = "Default";
        }

        profile.AutoLoadCharacters ??= new List<string>();
        profile.Overlays ??= new List<OverlayState>();
        profile.Overlays.RemoveAll(overlay =>
            string.Equals(overlay.CharacterName, "EVE Launcher", StringComparison.OrdinalIgnoreCase)
            || string.Equals(overlay.CustomLabel, "EVE Launcher", StringComparison.OrdinalIgnoreCase));
        foreach (OverlayState overlay in profile.Overlays)
        {
            overlay.CharacterName ??= string.Empty;
            overlay.CustomLabel ??= string.Empty;
            overlay.DirectHotkey ??= string.Empty;
            overlay.Opacity = Math.Clamp(overlay.Opacity, 0f, 1f);
        }

        profile.HotkeyGroups ??= new List<HotkeyGroup>();
        foreach (HotkeyGroup group in profile.HotkeyGroups)
        {
            group.Name ??= string.Empty;
            group.CycleHotkey ??= string.Empty;
            group.CharacterNames ??= new List<string>();
            group.CharacterNames.RemoveAll(string.IsNullOrWhiteSpace);
        }

        profile.Appearance ??= new AppearanceDefaults();
        if (string.Equals(profile.Appearance.LabelFont, "Segoe UI", StringComparison.OrdinalIgnoreCase)
            && profile.Appearance.LabelFontSize == 9)
        {
            profile.Appearance.LabelFont = "Segoe UI Semibold";
            profile.Appearance.LabelFontSize = 10;
        }

        return profile;
    }
}


