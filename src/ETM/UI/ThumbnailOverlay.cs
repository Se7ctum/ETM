using System.Runtime.InteropServices;
using ETM.Core;
using ETM.Persistence;

namespace ETM.UI;

internal sealed class ThumbnailOverlay : Form
{
    private const int ResizeGripSize = 20;
    private const double AspectRatio = 16d / 9d;
    private const int MaximumWidth = 640;
    private const int MaximumHeight = 360;
    private const int LabelBandHeight = 26;

    private EveWindow eveWindow;
    private readonly DwmThumbnail thumbnail = new();
    private readonly GlobalSettings snapSettings;
    private readonly Func<ThumbnailOverlay, IReadOnlyCollection<Rectangle>> otherOverlayBoundsProvider;
    private AppearanceDefaults appearance;
    private ContextMenuStrip? overlayMenu;
    private bool isMenuOpen;
    private string customLabel = string.Empty;
    private string directHotkey = string.Empty;
    private byte opacity;
    private bool aspectRatioLocked = true;
    private bool thumbnailRegistered;
    private DragMode dragMode = DragMode.None;
    private Point dragStartCursor;
    private Rectangle dragStartBounds;
    private bool hasDragged;
    private bool isActiveClient;

    internal IntPtr SourceHandle => eveWindow.Handle;

    internal event EventHandler? OverlayStateChanged;
    internal event EventHandler? SourceFocusRequested;
    internal event EventHandler<ThumbnailResizeEventArgs>? ResizeAllRequested;

    internal bool ThumbnailsLocked { get; set; }

    internal bool IsActiveClient
    {
        get => isActiveClient;
        set
        {
            if (isActiveClient == value)
            {
                return;
            }

            isActiveClient = value;
            Invalidate();
        }
    }

    internal void MarkActiveNow()
    {
        IsActiveClient = true;
        Update();
    }

    internal void SetActiveImmediate(bool active)
    {
        IsActiveClient = active;
        Update();
    }

    internal byte ThumbnailOpacity
    {
        get => opacity;
        set
        {
            if (opacity == value)
            {
                return;
            }

            opacity = value;
            UpdateThumbnail();
            OnOverlayStateChanged();
        }
    }

    internal bool AspectRatioLocked
    {
        get => aspectRatioLocked;
        set
        {
            if (aspectRatioLocked == value)
            {
                return;
            }

            aspectRatioLocked = value;
            OnOverlayStateChanged();
        }
    }

    internal string CharacterName => eveWindow.CharacterName;

    internal string CustomLabel
    {
        get => customLabel;
        set
        {
            string normalized = value.Trim();
            if (string.Equals(customLabel, normalized, StringComparison.Ordinal))
            {
                return;
            }

            customLabel = normalized;
            Invalidate();
            OnOverlayStateChanged();
        }
    }

    private string DisplayLabel
    {
        get
        {
            string label = string.IsNullOrWhiteSpace(customLabel)
                ? (string.IsNullOrWhiteSpace(eveWindow.CharacterName) ? eveWindow.Title : eveWindow.CharacterName)
                : customLabel;

            if (appearance.ShowHotkeyInLabel && !string.IsNullOrWhiteSpace(directHotkey))
            {
                label = $"{label} ({FormatHotkeyForLabel(directHotkey)})";
            }

            return label;
        }
    }

    private enum DragMode
    {
        None,
        Move,
        Resize
    }

    internal sealed class ThumbnailResizeEventArgs(Size size) : EventArgs
    {
        internal Size Size { get; } = size;
    }

    internal ThumbnailOverlay(
        EveWindow eveWindow,
        GlobalSettings snapSettings,
        AppearanceDefaults appearance,
        Func<ThumbnailOverlay, IReadOnlyCollection<Rectangle>> otherOverlayBoundsProvider,
        Rectangle? initialBounds = null,
        byte opacity = byte.MaxValue,
        string customLabel = "")
    {
        this.eveWindow = eveWindow;
        this.snapSettings = snapSettings;
        this.appearance = appearance;
        this.otherOverlayBoundsProvider = otherOverlayBoundsProvider;
        this.opacity = opacity;
        this.customLabel = customLabel.Trim();

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        MinimumSize = new Size(100, 57);
        StartPosition = FormStartPosition.Manual;
        Bounds = initialBounds ?? new Rectangle(100, 100, 320, 180);
        BackColor = Color.Black;
        overlayMenu = BuildContextMenu();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams createParams = base.CreateParams;
            createParams.ExStyle |= NativeMethods.WS_EX_NOACTIVATE
                | NativeMethods.WS_EX_TOOLWINDOW;
            return createParams;
        }
    }

    protected override bool ShowWithoutActivation => true;

    internal void EnsureAlwaysOnTop()
    {
        if (IsDisposed || isMenuOpen)
        {
            return;
        }

        _ = NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    internal void ApplySettings(OverlayState? state, AppearanceDefaults appearanceDefaults)
    {
        appearance = appearanceDefaults ?? new AppearanceDefaults();

        if (state is not null)
        {
            customLabel = (state.CustomLabel ?? string.Empty).Trim();
            directHotkey = (state.DirectHotkey ?? string.Empty).Trim();
            aspectRatioLocked = state.AspectRatioLocked;
            opacity = (byte)Math.Round(Math.Clamp(state.Opacity, 0f, 1f) * byte.MaxValue);
            UpdateThumbnail();
        }

        Invalidate();
    }

    internal void UpdateEveWindow(EveWindow updatedWindow)
    {
        if (eveWindow.Handle != updatedWindow.Handle)
        {
            return;
        }

        if (string.Equals(eveWindow.Title, updatedWindow.Title, StringComparison.Ordinal)
            && string.Equals(eveWindow.CharacterName, updatedWindow.CharacterName, StringComparison.Ordinal))
        {
            return;
        }

        eveWindow = updatedWindow;
        customLabel = string.Empty;
        Invalidate();
        OnOverlayStateChanged();
    }

    internal void SetThumbnailSize(Size size)
    {
        Size = new Size(
            Math.Clamp(size.Width, MinimumSize.Width, MaximumWidth),
            Math.Clamp(size.Height, MinimumSize.Height, MaximumHeight));
        ApplyAspectRatioLock();
        OnOverlayStateChanged();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RegisterThumbnail();
        UpdateThumbnail();
        EnsureAlwaysOnTop();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateThumbnail();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        int borderWidth = Math.Max(0, appearance.BorderWidth);
        if (borderWidth > 0)
        {
            Color borderColor = ParseColor(isActiveClient ? appearance.ActiveBorderColor : appearance.BorderColor, isActiveClient ? Color.DeepSkyBlue : Color.DimGray);
            using Pen borderPen = new(borderColor, borderWidth);
            Rectangle border = ClientRectangle;
            border.Width -= 1;
            border.Height -= 1;
            e.Graphics.DrawRectangle(borderPen, border);
        }

        string label = DisplayLabel;
        if (!string.IsNullOrWhiteSpace(label))
        {
            string fontName = string.IsNullOrWhiteSpace(appearance.LabelFont) ? "Segoe UI" : appearance.LabelFont;
            float fontSize = Math.Clamp(appearance.LabelFontSize, 6, 32);
            using Font font = new(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Point);
            SizeF textSize = e.Graphics.MeasureString(label, font);
            PointF labelPoint = GetLabelPoint(textSize);
            RectangleF background = new(labelPoint.X - 4, labelPoint.Y - 2, textSize.Width + 8, textSize.Height + 4);
            if (appearance.LabelBackgroundEnabled)
            {
                using Brush backgroundBrush = new SolidBrush(Color.FromArgb(160, Color.Black));
                e.Graphics.FillRectangle(backgroundBrush, background);
            }

            using Brush labelBrush = new SolidBrush(ParseColor(appearance.LabelColor, Color.White));
            e.Graphics.DrawString(label, font, labelBrush, labelPoint);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Right && !ThumbnailsLocked)
        {
            dragMode = DragMode.Move;
            dragStartCursor = Cursor.Position;
            dragStartBounds = Bounds;
            hasDragged = false;
            Capture = true;
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (!ThumbnailsLocked)
        {
            if (IsResizeGrip(e.Location))
            {
                dragMode = DragMode.Resize;
                dragStartCursor = Cursor.Position;
                dragStartBounds = Bounds;
                hasDragged = false;
                Capture = true;
                return;
            }
        }

        SourceFocusRequested?.Invoke(this, EventArgs.Empty);
        MarkActiveNow();
        Update();
        FocusSourceWindow();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (dragMode == DragMode.None)
        {
            Cursor = ThumbnailsLocked
                ? Cursors.Default
                : IsResizeGrip(e.Location) ? Cursors.SizeNWSE : Cursors.SizeAll;
            return;
        }

        Point cursorDelta = new(Cursor.Position.X - dragStartCursor.X, Cursor.Position.Y - dragStartCursor.Y);
        if (!hasDragged && Math.Abs(cursorDelta.X) < SystemInformation.DragSize.Width / 2
            && Math.Abs(cursorDelta.Y) < SystemInformation.DragSize.Height / 2)
        {
            return;
        }

        hasDragged = true;

        Bounds = dragMode == DragMode.Resize
            ? ResizeFromDrag(cursorDelta)
            : MoveFromDrag(cursorDelta, snap: snapSettings.SnapToGrid, includeEdges: false);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && dragMode == DragMode.None)
        {
            overlayMenu?.Show(this, e.Location);
            return;
        }

        base.OnMouseUp(e);

        if (e.Button is not (MouseButtons.Left or MouseButtons.Right))
        {
            return;
        }

        DragMode completedMode = dragMode;
        bool completedDrag = completedMode != DragMode.None && hasDragged;
        dragMode = DragMode.None;
        Capture = false;

        if (completedDrag)
        {
            if (completedMode == DragMode.Move)
            {
                Bounds = MoveFromDrag(new Point(Cursor.Position.X - dragStartCursor.X, Cursor.Position.Y - dragStartCursor.Y), snap: true, includeEdges: true);
            }

            ApplyAspectRatioLock();
            OnOverlayStateChanged();
            if (completedMode == DragMode.Resize && (ModifierKeys & Keys.Shift) != Keys.Shift)
            {
                ResizeAllRequested?.Invoke(this, new ThumbnailResizeEventArgs(Size));
            }

            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            overlayMenu?.Show(this, e.Location);
            return;
        }

        FocusSourceWindow();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (dragMode == DragMode.None)
        {
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            FocusSourceWindow();
            return;
        }

        base.OnMouseDoubleClick(e);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        ContextMenuStrip menu = new();
        menu.Opened += (_, _) => isMenuOpen = true;
        menu.Closed += (_, _) => isMenuOpen = false;
        menu.Items.Add("Hide this thumbnail", null, (_, _) => HideThumbnail());
        menu.Items.Add("Rename...", null, (_, _) => RenameThumbnail());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Bring to front", null, (_, _) => EnsureAlwaysOnTop());
        menu.Items.Add("Send to back", null, (_, _) => SendBehindOtherThumbnails());
        menu.Items.Add("Reset size", null, (_, _) => ResetSize());

        ToolStripMenuItem opacityMenu = new("Opacity");
        opacityMenu.DropDownItems.Add("25%", null, (_, _) => SetOpacityPercent(25));
        opacityMenu.DropDownItems.Add("50%", null, (_, _) => SetOpacityPercent(50));
        opacityMenu.DropDownItems.Add("75%", null, (_, _) => SetOpacityPercent(75));
        opacityMenu.DropDownItems.Add("100%", null, (_, _) => SetOpacityPercent(100));
        menu.Items.Add(opacityMenu);

        menu.Items.Add(new ToolStripSeparator());
        return menu;
    }

    private static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private PointF GetLabelPoint(SizeF textSize)
    {
        const float margin = 8f;
        string position = appearance.LabelPosition.Trim();

        float x = position.Contains("Right", StringComparison.OrdinalIgnoreCase)
            ? ClientSize.Width - textSize.Width - margin
            : margin;
        float y = position.Contains("Bottom", StringComparison.OrdinalIgnoreCase)
            ? ClientSize.Height - textSize.Height - margin
            : margin - 2;

        return new PointF(Math.Max(margin, x), Math.Max(4f, y));
    }

    private void SendBehindOtherThumbnails()
    {
        _ = NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HWND_BOTTOM,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private void ResetSize()
    {
        Size = new Size(320, 180);
        OnOverlayStateChanged();
    }

    private void HideThumbnail()
    {
        Visible = false;
        OnOverlayStateChanged();
    }

    private void RenameThumbnail()
    {
        using Form dialog = new()
        {
            Text = "Rename thumbnail",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(320, 92)
        };

        TextBox textBox = new()
        {
            Left = 12,
            Top = 12,
            Width = 296,
            Text = CustomLabel
        };
        Button okButton = new()
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 152,
            Top = 52,
            Width = 75
        };
        Button cancelButton = new()
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 233,
            Top = 52,
            Width = 75
        };

        dialog.Controls.Add(textBox);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            CustomLabel = textBox.Text;
        }
    }

    private void SetOpacityPercent(int percent)
    {
        ThumbnailOpacity = (byte)Math.Round(byte.MaxValue * (percent / 100d));
    }

    private bool IsResizeGrip(Point clientPoint)
    {
        return clientPoint.X >= ClientSize.Width - ResizeGripSize
            && clientPoint.Y >= ClientSize.Height - ResizeGripSize;
    }

    private Rectangle MoveFromDrag(Point cursorDelta, bool snap, bool includeEdges)
    {
        Rectangle nextBounds = dragStartBounds;
        nextBounds.Offset(cursorDelta);
        return snap ? SnapMovingBounds(nextBounds, includeEdges) : nextBounds;
    }

    private Rectangle ResizeFromDrag(Point cursorDelta)
    {
        int width = Math.Clamp(dragStartBounds.Width + cursorDelta.X, MinimumSize.Width, MaximumWidth);
        int height = Math.Clamp(dragStartBounds.Height + cursorDelta.Y, MinimumSize.Height, MaximumHeight);

        if (aspectRatioLocked)
        {
            if (Math.Abs(cursorDelta.Y) > Math.Abs(cursorDelta.X))
            {
                width = Math.Clamp((int)Math.Round(height * AspectRatio), MinimumSize.Width, MaximumWidth);
            }
            else
            {
                height = Math.Clamp((int)Math.Round(width / AspectRatio), MinimumSize.Height, MaximumHeight);
            }
        }

        return new Rectangle(dragStartBounds.Location, new Size(width, height));
    }

    private Rectangle SnapMovingBounds(Rectangle movingBounds, bool includeEdges)
    {
        int threshold = Math.Max(0, snapSettings.SnapThreshold);
        if (threshold == 0 && !snapSettings.SnapToGrid)
        {
            return movingBounds;
        }

        NativeMethods.RECT rect = new()
        {
            Left = movingBounds.Left,
            Top = movingBounds.Top,
            Right = movingBounds.Right,
            Bottom = movingBounds.Bottom
        };

        Screen screen = Screen.FromRectangle(movingBounds);
        Rectangle bounds = screen.Bounds;
        int width = rect.Width;
        int height = rect.Height;

        if (snapSettings.SnapToGrid && snapSettings.GridSize > 0)
        {
            int gridSize = Math.Max(1, snapSettings.GridSize);
            rect.Left = (int)Math.Round(rect.Left / (double)gridSize) * gridSize;
            rect.Top = (int)Math.Round(rect.Top / (double)gridSize) * gridSize;
            rect.Right = rect.Left + width;
            rect.Bottom = rect.Top + height;
        }

        if (includeEdges && snapSettings.SnapToEdges)
        {
            SnapToBounds(ref rect, bounds, threshold);

            foreach (Rectangle peerBounds in otherOverlayBoundsProvider(this))
            {
                SnapToBounds(ref rect, peerBounds, threshold);
            }
        }

        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static void SnapToBounds(ref NativeMethods.RECT rect, Rectangle bounds, int threshold)
    {
        if (threshold <= 0)
        {
            return;
        }

        int width = rect.Width;
        int height = rect.Height;

        if (Math.Abs(rect.Left - bounds.Left) <= threshold)
        {
            rect.Left = bounds.Left;
            rect.Right = rect.Left + width;
        }
        else if (Math.Abs(rect.Right - bounds.Right) <= threshold)
        {
            rect.Right = bounds.Right;
            rect.Left = rect.Right - width;
        }
        else if (Math.Abs(rect.Left - bounds.Right) <= threshold)
        {
            rect.Left = bounds.Right;
            rect.Right = rect.Left + width;
        }
        else if (Math.Abs(rect.Right - bounds.Left) <= threshold)
        {
            rect.Right = bounds.Left;
            rect.Left = rect.Right - width;
        }

        if (Math.Abs(rect.Top - bounds.Top) <= threshold)
        {
            rect.Top = bounds.Top;
            rect.Bottom = rect.Top + height;
        }
        else if (Math.Abs(rect.Bottom - bounds.Bottom) <= threshold)
        {
            rect.Bottom = bounds.Bottom;
            rect.Top = rect.Bottom - height;
        }
        else if (Math.Abs(rect.Top - bounds.Bottom) <= threshold)
        {
            rect.Top = bounds.Bottom;
            rect.Bottom = rect.Top + height;
        }
        else if (Math.Abs(rect.Bottom - bounds.Top) <= threshold)
        {
            rect.Bottom = bounds.Top;
            rect.Top = rect.Bottom - height;
        }
    }

    private void ApplyAspectRatioLock()
    {
        if (!aspectRatioLocked)
        {
            return;
        }

        int lockedHeight = Math.Max(MinimumSize.Height, (int)Math.Round(Width / AspectRatio));
        if (Height != lockedHeight)
        {
            Height = lockedHeight;
        }
    }

    internal void FocusSourceWindow()
    {
        if (!NativeMethods.IsWindow(eveWindow.Handle))
        {
            return;
        }

        if (NativeMethods.IsIconic(eveWindow.Handle))
        {
            NativeMethods.ShowWindow(eveWindow.Handle, NativeMethods.SW_RESTORE);
        }

        if (!NativeMethods.SetForegroundWindow(eveWindow.Handle))
        {
            NativeMethods.BringWindowToTop(eveWindow.Handle);
            NativeMethods.SetForegroundWindow(eveWindow.Handle);
        }
    }

    private void OnOverlayStateChanged()
    {
        OverlayStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RegisterThumbnail()
    {
        if (thumbnailRegistered || !IsHandleCreated)
        {
            return;
        }

        thumbnail.Register(eveWindow.Handle, Handle);
        thumbnailRegistered = true;
    }

    private void UpdateThumbnail()
    {
        if (!thumbnailRegistered)
        {
            return;
        }

        int borderWidth = Math.Max(0, appearance.BorderWidth);
        int labelBandHeight = string.IsNullOrWhiteSpace(DisplayLabel) ? 0 : LabelBandHeight;
        Rectangle thumbnailBounds = new(
            borderWidth,
            labelBandHeight,
            Math.Max(1, ClientSize.Width - borderWidth * 2),
            Math.Max(1, ClientSize.Height - labelBandHeight - borderWidth));
        thumbnail.Update(thumbnailBounds, opacity);
    }

    private static string FormatHotkeyForLabel(string hotkey)
    {
        return hotkey
            .Replace("Ctrl+", "Ctrl-", StringComparison.OrdinalIgnoreCase)
            .Replace("Control+", "Ctrl-", StringComparison.OrdinalIgnoreCase)
            .Replace("Shift+", "Shift-", StringComparison.OrdinalIgnoreCase)
            .Replace("Alt+", "Alt-", StringComparison.OrdinalIgnoreCase)
            .Replace("Win+", "Win-", StringComparison.OrdinalIgnoreCase);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            thumbnail.Dispose();
        }

        base.Dispose(disposing);
    }
}






