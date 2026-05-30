using Microsoft.Win32;

namespace ETM.Core;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ETM";

    internal static void SetLaunchOnStartup(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (key is null)
        {
            throw new InvalidOperationException("Unable to open the Windows startup registry key.");
        }

        if (enabled)
        {
            key.SetValue(ValueName, QuoteExecutablePath(System.Windows.Forms.Application.ExecutablePath), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    internal static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        string? value = key?.GetValue(ValueName) as string;
        return string.Equals(value, QuoteExecutablePath(System.Windows.Forms.Application.ExecutablePath), StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteExecutablePath(string executablePath)
    {
        return $"\"{executablePath}\"";
    }
}

