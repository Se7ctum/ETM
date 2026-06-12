using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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
    private readonly Func<ThumbnailOverlay, IReadOnlyCollection<Size>> otherOverlaySizesProvider;
    private AppearanceDefaults appearance;
    private ContextMenuStrip? overlayMenu;
    private TextOverlay? textOverlay;
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
    private IReadOnlyList<HotkeyGroupMenuItem> hotkeyGroupMenuItems = Array.Empty<HotkeyGroupMenuItem>();

    internal IntPtr SourceHandle => eveWindow.Handle;

    internal event EventHandler? OverlayStateChanged;
    internal event EventHandler? SourceFocusRequested;
    internal event EventHandler? ResetSizeRequested;
    internal event EventHandler<ThumbnailResizeEventArgs>? ResizeAllRequested;
    internal event EventHandler<HotkeyGroupAssignmentEventArgs>? HotkeyGroupAssignmentChanged;

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
            UpdateTextOverlay();
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
            UpdateTextOverlay();
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

            string hotkeyLabel = GetHotkeyLabel();
            if (appearance.ShowHotkeyInLabel && !string.IsNullOrWhiteSpace(hotkeyLabel))
            {
                label = $"{label} ({hotkeyLabel})";
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

    internal sealed class HotkeyGroupMenuItem(string name, string hotkey, bool assigned)
    {
        internal string Name { get; } = name;
        internal string Hotkey { get; } = hotkey;
        internal bool Assigned { get; } = assigned;
    }

    internal sealed class HotkeyGroupAssignmentEventArgs(string groupName, bool assigned) : EventArgs
    {
        internal string GroupName { get; } = groupName;
        internal bool Assigned { get; } = assigned;
    }

    internal ThumbnailOverlay(
        EveWindow eveWindow,
        GlobalSettings snapSettings,
        AppearanceDefaults appearance,
        Func<ThumbnailOverlay, IReadOnlyCollection<Rectangle>> otherOverlayBoundsProvider,
        Func<ThumbnailOverlay, IReadOnlyCollection<Size>> otherOverlaySizesProvider,
        Rectangle? initialBounds = null,
        byte opacity = byte.MaxValue,
        string customLabel = "")
    {
        this.eveWindow = eveWindow;
        this.snapSettings = snapSettings;
        this.appearance = appearance;
        this.otherOverlayBoundsProvider = otherOverlayBoundsProvider;
        this.otherOverlaySizesProvider = otherOverlaySizesProvider;
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

    internal void SetHotkeyGroups(IReadOnlyList<HotkeyGroupMenuItem> groups)
    {
        hotkeyGroupMenuItems = groups;
    }

    internal void ApplySettings(OverlayState? state, AppearanceDefaults appearanceDefaults)
    {
        appearance = appearanceDefaults ?? new AppearanceDefaults();

        if (state is not null)
        {
            customLabel = (state.CustomLabel ?? string.Empty).Trim();
            aspectRatioLocked = state.AspectRatioLocked;
            opacity = (byte)Math.Round(Math.Clamp(state.Opacity, 0f, 1f) * byte.MaxValue);
        }

        UpdateThumbnail();
        UpdateTextOverlay();
        Invalidate();
    }

    internal void SetLabelHotkey(string hotkey)
    {
        directHotkey = hotkey.Trim();
        UpdateTextOverlay();
        Invalidate();
    }

    internal bool UpdateEveWindow(EveWindow updatedWindow)
    {
        if (eveWindow.Handle != updatedWindow.Handle)
        {
            return false;
        }

        if (string.Equals(eveWindow.Title, updatedWindow.Title, StringComparison.Ordinal)
            && string.Equals(eveWindow.CharacterName, updatedWindow.CharacterName, StringComparison.Ordinal))
        {
            return false;
        }

        bool characterChanged = !string.Equals(eveWindow.CharacterName, updatedWindow.CharacterName, StringComparison.Ordinal);
        eveWindow = updatedWindow;
        if (characterChanged)
        {
            customLabel = string.Empty;
        }

        UpdateTextOverlay();
        Invalidate();
        return characterChanged;
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
        UpdateTextOverlay();
        EnsureAlwaysOnTop();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateThumbnail();
        UpdateTextOverlay();
        Invalidate();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        UpdateTextOverlay();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateTextOverlay();
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

        UpdateTextOverlay();
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
        ToolStripMenuItem hotkeyGroupsMenu = new("Hotkey groups");
        hotkeyGroupsMenu.DropDownOpening += (_, _) => PopulateHotkeyGroupsMenu(hotkeyGroupsMenu);
        menu.Items.Add(hotkeyGroupsMenu);

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

    private void PopulateHotkeyGroupsMenu(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        if (string.IsNullOrWhiteSpace(CharacterName))
        {
            ToolStripMenuItem item = new("Log in character first") { Enabled = false };
            menu.DropDownItems.Add(item);
            return;
        }

        if (hotkeyGroupMenuItems.Count == 0)
        {
            ToolStripMenuItem item = new("No hotkey groups") { Enabled = false };
            menu.DropDownItems.Add(item);
            return;
        }

        foreach (HotkeyGroupMenuItem group in hotkeyGroupMenuItems)
        {
            string suffix = string.IsNullOrWhiteSpace(group.Hotkey) ? string.Empty : $" ({FormatHotkeyForLabel(group.Hotkey)})";
            ToolStripMenuItem item = new($"{group.Name}{suffix}")
            {
                CheckOnClick = true,
                Checked = group.Assigned
            };
            item.Click += (_, _) => HotkeyGroupAssignmentChanged?.Invoke(this, new HotkeyGroupAssignmentEventArgs(group.Name, item.Checked));
            menu.DropDownItems.Add(item);
        }
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

    private string GetHotkeyLabel()
    {
        if (!string.IsNullOrWhiteSpace(directHotkey))
        {
            return FormatHotkeyForLabel(directHotkey);
        }

        return string.Empty;
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
        ResetSizeRequested?.Invoke(this, EventArgs.Empty);
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

        return new Rectangle(dragStartBounds.Location, SnapResizeSize(new Size(width, height)));
    }

    private Size SnapResizeSize(Size size)
    {
        int threshold = Math.Max(0, snapSettings.SnapThreshold);
        if (threshold == 0)
        {
            return size;
        }

        foreach (Size peerSize in otherOverlaySizesProvider(this))
        {
            if (Math.Abs(size.Width - peerSize.Width) <= threshold
                || Math.Abs(size.Height - peerSize.Height) <= threshold)
            {
                return peerSize;
            }
        }

        return size;
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

        Rectangle thumbnailBounds = GetThumbnailArea();
        thumbnail.Update(thumbnailBounds, opacity);
        Invalidate();
    }

    private Rectangle GetThumbnailArea()
    {
        int borderWidth = Math.Max(0, appearance.BorderWidth);
        return new Rectangle(
            borderWidth,
            borderWidth,
            Math.Max(1, ClientSize.Width - borderWidth * 2),
            Math.Max(1, ClientSize.Height - borderWidth * 2));
    }

    private void UpdateTextOverlay()
    {
        string label = DisplayLabel;
        if (IsDisposed || !Visible || string.IsNullOrWhiteSpace(label))
        {
            textOverlay?.Hide();
            return;
        }

        textOverlay ??= new TextOverlay(this);
        textOverlay.UpdateText(label, appearance);
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
            textOverlay?.Dispose();
            thumbnail.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class TextOverlay : Form
    {
        private const int PaddingX = 8;
        private const int PaddingY = 8;
        private readonly ThumbnailOverlay owner;

        internal TextOverlay(ThumbnailOverlay owner)
        {
            this.owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = false;
            StartPosition = FormStartPosition.Manual;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= NativeMethods.WS_EX_LAYERED
                    | NativeMethods.WS_EX_NOACTIVATE
                    | NativeMethods.WS_EX_TOOLWINDOW
                    | NativeMethods.WS_EX_TRANSPARENT;
                return createParams;
            }
        }

        protected override bool ShowWithoutActivation => true;

        internal void UpdateText(string text, AppearanceDefaults appearance)
        {
            using Font font = CreateFont(appearance);
            Size measured = MeasureText(text, font);
            int barHeight = Math.Max(LabelBandHeight, measured.Height + PaddingY * 2);
            Size overlaySize = new(Math.Max(1, owner.Width), barHeight);
            Point location = new(owner.Left, owner.Top);

            using Bitmap bitmap = RenderTextBitmap(text, font, appearance, overlaySize);

            if (!Visible)
            {
                Show(owner);
            }

            UpdateLayeredBitmap(bitmap, location);
            EnsureAboveOwner();
        }

        private static Bitmap RenderTextBitmap(string text, Font font, AppearanceDefaults appearance, Size overlaySize)
        {
            Color labelColor = ParseColor(appearance.LabelColor, Color.White);
            Bitmap bitmap = new(overlaySize.Width, overlaySize.Height, PixelFormat.Format32bppPArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.FromArgb(225, 0, 0, 0));
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Rectangle textBounds = new(PaddingX, 0, Math.Max(1, overlaySize.Width - PaddingX * 2), overlaySize.Height);
            TextRenderer.DrawText(
                graphics,
                text,
                font,
                textBounds,
                labelColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            return bitmap;
        }

        private static Size MeasureText(string text, Font font)
        {
            using Bitmap bitmap = new(1, 1, PixelFormat.Format32bppPArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            using StringFormat format = CreateTextFormat();
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            SizeF measured = graphics.MeasureString(text, font, int.MaxValue, format);
            return new Size(
                Math.Max(1, (int)Math.Ceiling(measured.Width)),
                Math.Max(1, (int)Math.Ceiling(measured.Height)));
        }

        internal void EnsureAboveOwner()
        {
            if (!IsHandleCreated || owner.IsDisposed || !owner.Visible)
            {
                return;
            }

            _ = NativeMethods.SetWindowPos(
                Handle,
                owner.Handle,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        private void UpdateLayeredBitmap(Bitmap bitmap, Point location)
        {
            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            IntPtr bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmap = NativeMethods.SelectObject(memoryDc, bitmapHandle);

            try
            {
                NativeMethods.POINT destination = new() { X = location.X, Y = location.Y };
                NativeMethods.PSIZE size = new() { X = bitmap.Width, Y = bitmap.Height };
                NativeMethods.POINT source = new() { X = 0, Y = 0 };
                NativeMethods.BLENDFUNCTION blend = new()
                {
                    BlendOp = NativeMethods.AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = NativeMethods.AC_SRC_ALPHA
                };

                NativeMethods.UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    in destination,
                    in size,
                    memoryDc,
                    in source,
                    0,
                    in blend,
                    NativeMethods.ULW_ALPHA);
            }
            finally
            {
                NativeMethods.SelectObject(memoryDc, oldBitmap);
                NativeMethods.DeleteObject(bitmapHandle);
                NativeMethods.DeleteDC(memoryDc);
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private Point GetLocation(Size overlaySize, AppearanceDefaults appearance)
        {
            int borderWidth = Math.Max(0, appearance.BorderWidth);
            int inset = Math.Max(4, borderWidth + 4);
            string position = appearance.LabelPosition.Trim();
            bool right = position.Contains("Right", StringComparison.OrdinalIgnoreCase);
            bool bottom = position.Contains("Bottom", StringComparison.OrdinalIgnoreCase);

            int x = right ? owner.Right - overlaySize.Width - inset : owner.Left + inset;
            int y = bottom ? owner.Bottom - overlaySize.Height - inset : owner.Top + inset;
            return new Point(x, y);
        }

        private static Font CreateFont(AppearanceDefaults appearance)
        {
            string fontName = string.IsNullOrWhiteSpace(appearance.LabelFont) ? "Segoe UI Semibold" : appearance.LabelFont;
            float fontSize = Math.Clamp(appearance.LabelFontSize, 6, 32);
            return new Font(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Point);
        }

        private static StringFormat CreateTextFormat()
        {
            return new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces,
                Trimming = StringTrimming.None
            };
        }
    }
}






