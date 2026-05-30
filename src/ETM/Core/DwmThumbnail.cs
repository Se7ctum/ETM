namespace ETM.Core;

internal sealed class DwmThumbnail : IDisposable
{
    private IntPtr thumbnailHandle;
    private IntPtr sourceHandle;
    private IntPtr destinationHandle;
    private bool disposed;

    internal bool IsRegistered => thumbnailHandle != IntPtr.Zero;

    internal void Register(IntPtr sourceHwnd, IntPtr destinationHwnd)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (sourceHwnd == IntPtr.Zero)
        {
            throw new ArgumentException("Source window handle cannot be zero.", nameof(sourceHwnd));
        }

        if (destinationHwnd == IntPtr.Zero)
        {
            throw new ArgumentException("Destination window handle cannot be zero.", nameof(destinationHwnd));
        }

        if (!NativeMethods.IsWindow(sourceHwnd))
        {
            throw new InvalidOperationException("Source window is no longer valid.");
        }

        if (!NativeMethods.IsWindow(destinationHwnd))
        {
            throw new InvalidOperationException("Destination window is no longer valid.");
        }

        Unregister();

        int hr = NativeMethods.DwmRegisterThumbnail(destinationHwnd, sourceHwnd, out thumbnailHandle);
        if (hr < 0 || thumbnailHandle == IntPtr.Zero)
        {
            thumbnailHandle = IntPtr.Zero;
            throw new InvalidOperationException($"DwmRegisterThumbnail failed with HRESULT 0x{hr:X8}.");
        }

        sourceHandle = sourceHwnd;
        destinationHandle = destinationHwnd;
    }

    internal void Update(Rectangle destinationRectangle, byte opacity)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!IsRegistered)
        {
            return;
        }

        if (!NativeMethods.IsWindow(sourceHandle) || !NativeMethods.IsWindow(destinationHandle))
        {
            Unregister();
            return;
        }

        NativeMethods.DWM_THUMBNAIL_PROPERTIES properties = new()
        {
            dwFlags = NativeMethods.DWM_TNP_RECTDESTINATION
                | NativeMethods.DWM_TNP_OPACITY
                | NativeMethods.DWM_TNP_VISIBLE,
            rcDestination = new NativeMethods.RECT
            {
                Left = destinationRectangle.Left,
                Top = destinationRectangle.Top,
                Right = destinationRectangle.Right,
                Bottom = destinationRectangle.Bottom
            },
            opacity = opacity,
            fVisible = 1,
            fSourceClientAreaOnly = 0
        };

        int hr = NativeMethods.DwmUpdateThumbnailProperties(thumbnailHandle, in properties);
        if (hr < 0)
        {
            throw new InvalidOperationException($"DwmUpdateThumbnailProperties failed with HRESULT 0x{hr:X8}.");
        }
    }

    internal void Unregister()
    {
        if (thumbnailHandle != IntPtr.Zero)
        {
            IntPtr handle = thumbnailHandle;
            thumbnailHandle = IntPtr.Zero;
            _ = NativeMethods.DwmUnregisterThumbnail(handle);
        }

        sourceHandle = IntPtr.Zero;
        destinationHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Unregister();
        disposed = true;
    }
}
