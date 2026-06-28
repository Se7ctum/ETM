# ETM Project Summary

## Purpose

ETM is EVE Thumbnail Manager, a Windows desktop utility for players running multiple EVE Online clients. It shows small always-on-top live thumbnails of EVE clients, lets the user click a thumbnail to focus that client, supports cycling between characters with hotkeys, and saves layouts/profiles.

## Technology

- Language/runtime: C# on .NET 8.
- App type: Windows desktop `WinExe`.
- UI stack: WinForms for tray app, overlay windows, and native window behavior; WPF hosted inside WinForms for the modern config UI.
- Native APIs: Win32, DWM thumbnails, global hotkeys, foreground window events, startup registry integration.
- Persistence: JSON under `%APPDATA%\ETM\settings.json`.

## Main Architecture

### `TrayApplicationContext`

`src/ETM/UI/TrayApplicationContext.cs` is the app hub. It owns:

- tray icon and tray menu;
- refresh timer that discovers EVE windows every 3 seconds;
- foreground watcher used to mark the active thumbnail;
- hotkey message window and `HotkeyManager`;
- active profile and settings save/debounce flow;
- overlay creation, disposal, visibility, size, and position saving;
- profile switching and profile auto-load rules.

### `ThumbnailOverlay`

`src/ETM/UI/ThumbnailOverlay.cs` is each live thumbnail window. It is a borderless topmost WinForms form with `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW`. It registers a DWM thumbnail from the EVE client into the overlay, draws the border, handles input, and maintains a separate layered `TextOverlay` for the top label bar/text.

Important behaviors:

- Right mouse drag moves an unlocked thumbnail.
- Left drag from the resize grip resizes.
- Resizing normally resizes all thumbnails; holding Shift resizes only that thumbnail.
- Movement can snap to grid/edges depending on global settings.
- Resize can snap to nearby peer thumbnail sizes.
- Left click focuses the source EVE client.
- Context menu includes hide, rename, hotkey group assignment, reset size, and opacity.

### `WindowEnumerator`

`src/ETM/Core/WindowEnumerator.cs` finds EVE windows. It recognizes EVE clients by:

- window class `triuiScreen`, or
- title prefix `EVE - `, or
- process name `exefile`.

Character names are extracted from window titles like `EVE - Character Name`. Logged-out clients can have empty character names but should still be represented if the process is `exefile`.

### `SettingsManager`

`src/ETM/Persistence/SettingsManager.cs` loads and saves app settings. It normalizes settings after load, creates a default profile if needed, clamps overlay opacity, removes old `EVE Launcher` overlay entries, and migrates old label font defaults.

## Settings Model

Top-level `AppSettings` contains:

- `ActiveProfileName`
- `Profiles`
- `Global`
- `SetupCompleted`

`GlobalSettings` contains:

- `ShowHideAllHotkey`
- `LaunchOnStartup`
- `HotkeysRequireEveFocus` default `true`
- `SnapToEdges`
- `SnapThreshold`
- `SnapToGrid`
- `GridSize`

Each `Profile` contains:

- profile name;
- optional auto-load client count;
- auto-load character rules;
- per-character `OverlayState` entries;
- `HotkeyGroup` entries;
- `AppearanceDefaults`;
- thumbnail lock state.

Each `OverlayState` stores character name, custom label, monitor-relative position, size, visibility, opacity, aspect ratio lock, z-order placeholder, and optional direct hotkey.

## Hotkeys

Hotkeys are global Windows hotkeys registered by `HotkeyManager` through `RegisterHotKey`. Valid hotkey strings look like `Tab`, `Shift+F`, `Ctrl+Comma`, etc. The WPF config UI captures hotkeys by clicking the field and pressing the desired key combination.

Current important behavior: if `HotkeysRequireEveFocus` is enabled, ETM unregisters hotkeys when no EVE client thumbnail source is foreground. This avoids ETM locking keys while the user is outside EVE. The callback still has a guard as a second safety layer.

Cycle hotkeys use the currently focused EVE character to determine the next character in the configured order. If the current character is not in the group, cycling starts at the first configured character.

## Config UI

`WpfConfigRoot` builds the config UI in code. There is no XAML. Pages:

- Profiles: activation rules, create/duplicate/delete/import/export.
- Thumbnails: per-character direct hotkey, opacity, visibility, aspect ratio.
- Hotkeys: show/hide hotkey plus cycle groups and character lists.
- Appearance: border colors, border width, label color/font/size/position, show hotkey in label.
- System: launch on startup, hotkeys only while EVE is active, snap settings.

The Save button is page-level and turns red when dirty. Page builders call `MarkDirty()` on change. Some controls use `flushCurrentPage` so values are pushed before save.

## Known Sensitive Areas

- Overlay performance and perceived latency are critical. Avoid unnecessary redraws, z-order calls, or timer work.
- Topmost/z-order behavior can cause flicker when thumbnails overlap. Active thumbnail should be topmost without thumbnails fighting each other.
- DWM thumbnails and layered label windows must stay visually aligned during move/resize.
- Hotkey registration should match user expectations exactly. Registered global hotkeys intercept keys system-wide.
- Settings save reliability matters on restart/reboot. Save positions on explicit save, debounce, session ending, application exit, and dispose.

## Current Public Status

The app is an early public build under the MIT license. Published executables are unsigned, so SmartScreen or Smart App Control may warn or block until the app gains reputation or is signed.
