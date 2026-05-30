namespace ETM.Core;

internal sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr windowHandle;
    private readonly Dictionary<int, HotkeyRegistration> registrationsById = new();
    private readonly Dictionary<string, int> idsByHotkey = new(StringComparer.OrdinalIgnoreCase);
    private int nextId = 1;
    private bool disposed;

    internal HotkeyManager(IntPtr windowHandle)
    {
        this.windowHandle = windowHandle == IntPtr.Zero
            ? throw new ArgumentException("Hotkey window handle cannot be zero.", nameof(windowHandle))
            : windowHandle;
    }

    internal bool Register(string hotkeyString, Action callback)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(callback);

        string normalized = NormalizeHotkey(hotkeyString);
        Unregister(normalized);

        if (!TryParseHotkey(normalized, out int modifiers, out int virtualKey))
        {
            return false;
        }

        int id = nextId++;
        if (!NativeMethods.RegisterHotKey(windowHandle, id, modifiers, virtualKey))
        {
            return false;
        }

        registrationsById[id] = new HotkeyRegistration(normalized, callback);
        idsByHotkey[normalized] = id;
        return true;
    }

    internal void Unregister(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return;
        }

        string normalized = NormalizeHotkey(hotkeyString);
        if (!idsByHotkey.TryGetValue(normalized, out int id))
        {
            return;
        }

        NativeMethods.UnregisterHotKey(windowHandle, id);
        idsByHotkey.Remove(normalized);
        registrationsById.Remove(id);
    }

    internal void UnregisterAll()
    {
        foreach (int id in registrationsById.Keys.ToList())
        {
            NativeMethods.UnregisterHotKey(windowHandle, id);
        }

        registrationsById.Clear();
        idsByHotkey.Clear();
    }

    internal bool ProcessHotkeyMessage(Message message)
    {
        if (message.Msg != NativeMethods.WM_HOTKEY)
        {
            return false;
        }

        int id = message.WParam.ToInt32();
        if (!registrationsById.TryGetValue(id, out HotkeyRegistration? registration))
        {
            return false;
        }

        registration.Callback();
        return true;
    }

    internal static bool TryParseHotkey(string hotkeyString, out int modifiers, out int virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return false;
        }

        string[] parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (string part in parts[..^1])
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "SHIFT":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "ALT":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                default:
                    return false;
            }
        }

        if (!TryParseKey(parts[^1], out Keys key) || key == Keys.None)
        {
            return false;
        }

        modifiers |= NativeMethods.MOD_NOREPEAT;
        virtualKey = (int)key;
        return true;
    }

    private static string NormalizeHotkey(string hotkeyString)
    {
        return string.Join('+', hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool TryParseKey(string keyName, out Keys key)
    {
        if (Enum.TryParse(keyName, ignoreCase: true, out key))
        {
            return true;
        }

        key = keyName.Trim().ToUpperInvariant() switch
        {
            "," or "COMMA" => Keys.Oemcomma,
            "." or "PERIOD" or "DOT" => Keys.OemPeriod,
            ";" or "SEMICOLON" => Keys.OemSemicolon,
            "'" or "QUOTE" => Keys.OemQuotes,
            "/" or "SLASH" => Keys.OemQuestion,
            "\\" or "BACKSLASH" => Keys.OemBackslash,
            "-" or "MINUS" => Keys.OemMinus,
            "=" or "EQUALS" => Keys.Oemplus,
            "`" or "BACKTICK" => Keys.Oemtilde,
            "[" or "LEFTBRACKET" => Keys.OemOpenBrackets,
            "]" or "RIGHTBRACKET" => Keys.OemCloseBrackets,
            _ => Keys.None
        };

        return key != Keys.None;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        UnregisterAll();
        disposed = true;
    }

    private sealed record HotkeyRegistration(string Hotkey, Action Callback);
}
