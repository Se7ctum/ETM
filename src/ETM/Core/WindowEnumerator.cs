using System.Diagnostics;

namespace ETM.Core;

internal sealed record EveWindow(IntPtr Handle, string Title, string CharacterName);

internal static class WindowEnumerator
{
    private const string EveWindowClassName = "triuiScreen";
    private const string EveCharacterTitlePrefix = "EVE - ";
    private const string EveClientProcessName = "exefile";

    internal static List<EveWindow> FindEveWindows()
    {
        List<EveWindow> windows = new();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
            {
                return true;
            }

            string className = GetClassName(hwnd);
            string title = GetWindowTitle(hwnd);
            string processName = GetProcessName(hwnd);
            bool isEveWindow = string.Equals(className, EveWindowClassName, StringComparison.Ordinal)
                || title.StartsWith(EveCharacterTitlePrefix, StringComparison.Ordinal)
                || string.Equals(processName, EveClientProcessName, StringComparison.OrdinalIgnoreCase);

            if (!isEveWindow)
            {
                return true;
            }

            string characterName = ExtractCharacterName(title);
            if (string.IsNullOrWhiteSpace(characterName)
                && string.Equals(processName, EveClientProcessName, StringComparison.OrdinalIgnoreCase))
            {
                characterName = string.IsNullOrWhiteSpace(title) ? $"EVE Client {windows.Count + 1}" : title;
            }

            windows.Add(new EveWindow(hwnd, title, characterName));

            return true;
        }, IntPtr.Zero);

        Debug.WriteLine($"ETM: Found {windows.Count} EVE window(s).");

        return windows;
    }

    internal static string ExtractCharacterName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        string trimmedTitle = title.Trim();
        if (!trimmedTitle.StartsWith(EveCharacterTitlePrefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        string characterName = trimmedTitle[EveCharacterTitlePrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(characterName) ? string.Empty : characterName;
    }

    internal static string DescribeDetectedWindows()
    {
        List<EveWindow> windows = FindEveWindows();
        if (windows.Count == 0)
        {
            return "No EVE windows detected.";
        }

        return string.Join(
            Environment.NewLine,
            windows.Select(window =>
                $"0x{window.Handle.ToInt64():X}: \"{window.Title}\" Character=\"{window.CharacterName}\""));
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        char[] buffer = new char[length + 1];
        int copied = NativeMethods.GetWindowText(hwnd, buffer, buffer.Length);
        return copied <= 0 ? string.Empty : new string(buffer, 0, copied);
    }

    private static string GetClassName(IntPtr hwnd)
    {
        char[] buffer = new char[256];
        int copied = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
        return copied <= 0 ? string.Empty : new string(buffer, 0, copied);
    }

    private static string GetProcessName(IntPtr hwnd)
    {
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == 0)
        {
            return string.Empty;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
