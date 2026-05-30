namespace ETM.Core;

internal sealed class ForegroundWatcher : IDisposable
{
    private readonly Control marshalControl;
    private readonly NativeMethods.WinEventProc eventProc;
    private IntPtr hookHandle;
    private bool disposed;

    internal event EventHandler<IntPtr>? ForegroundChanged;

    internal ForegroundWatcher()
    {
        marshalControl = new Control();
        marshalControl.CreateControl();

        eventProc = HandleWinEvent;
        hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            eventProc,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
    }

    internal void Unhook()
    {
        if (hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWinEvent(hookHandle);
        hookHandle = IntPtr.Zero;
    }

    private void HandleWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (disposed || eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND || hwnd == IntPtr.Zero)
        {
            return;
        }

        if (marshalControl.IsHandleCreated && marshalControl.InvokeRequired)
        {
            try
            {
                marshalControl.BeginInvoke(() => OnForegroundChanged(hwnd));
            }
            catch (InvalidOperationException)
            {
                // The app is shutting down and the marshal handle is already gone.
            }

            return;
        }

        OnForegroundChanged(hwnd);
    }

    private void OnForegroundChanged(IntPtr hwnd)
    {
        if (!disposed)
        {
            ForegroundChanged?.Invoke(this, hwnd);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Unhook();
        marshalControl.Dispose();
    }
}
