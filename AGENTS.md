# AGENTS.md

This file is the first stop for coding agents working on ETM.

## Project

ETM means EVE Thumbnail Manager. It is a Windows-only .NET 8 desktop utility for EVE Online multi-client management. It creates always-on-top live DWM thumbnails for running EVE clients, lets the user click thumbnails to switch clients, supports hotkey cycling, and stores per-character layout/profile settings.

## Repository Layout

- `src/ETM.sln` - solution file.
- `src/ETM/ETM.csproj` - WinExe project targeting `net8.0-windows`, with WinForms and WPF enabled.
- `src/ETM/Program.cs` - app entrypoint.
- `src/ETM/UI/TrayApplicationContext.cs` - main application coordinator: tray icon, refresh timer, overlay lifecycle, profile switching, saving, hotkeys.
- `src/ETM/UI/ThumbnailOverlay.cs` - borderless topmost thumbnail window, DWM thumbnail host, dragging/resizing, label overlay, context menu.
- `src/ETM/UI/Wpf/WpfConfigRoot.cs` - current modern configuration UI.
- `src/ETM/UI/ConfigWindow.cs` - WinForms host for WPF config.
- `src/ETM/UI/SetupWizard.cs` - first-run wizard.
- `src/ETM/Core/*` - native interop, DWM thumbnail wrapper, EVE window enumeration, global hotkey manager, foreground watcher, startup helper.
- `src/ETM/Persistence/*` - JSON settings models and read/write/import/export logic.
- `src/ETM/Resources/tray_icon.ico` - app/tray icon.
- `docs/PROJECT_SUMMARY.md` - architecture and behavior summary.
- `docs/CODING_HANDOFF.md` - exact setup/build/publish/GitHub handoff notes.

## Build And Publish

From repo root:

```powershell
dotnet build .\src\ETM.sln
```

Publish the runnable build:

```powershell
dotnet publish .\src\ETM\ETM.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -o .\publish
```

Known build warning: `WFAC010` about high DPI manifest configuration. It is currently non-blocking.

## Runtime Data

Settings are stored at:

```text
%APPDATA%\ETM\settings.json
```

Do not assume settings live beside the exe. `SettingsManager.Save` writes atomically through `settings.json.tmp` and then replaces `settings.json`.

## GitHub / Local Machine Notes

The user's working repo has historically lived at `C:\ETM`. A portable Git executable and deploy key may exist there:

```powershell
C:\ETM\tools\PortableGit\cmd\git.exe -C C:\ETM status --short
$env:GIT_SSH_COMMAND='ssh -i C:/ETM/.github-deploy/etm_deploy_key -o StrictHostKeyChecking=accept-new'
C:\ETM\tools\PortableGit\cmd\git.exe -C C:\ETM push origin main
```

If working outside `C:\ETM`, adapt paths instead of hard-coding them.

## Coding Guidelines For Agents

- Keep changes focused. This app is very interaction-sensitive; small UI timing changes can create visible jank.
- Prefer existing WinForms/WPF/native interop patterns over introducing a new UI stack.
- Be careful with always-on-top windows and z-order. Overlay flicker is a known sensitive area.
- Do not reintroduce EVE Launcher thumbnails. EVE clients are `exefile.exe`; launcher windows should be ignored.
- Treat hotkeys as global Windows registrations. If a feature says hotkeys should only work while EVE is active, unregister them when EVE is inactive, not only ignore callbacks.
- Preserve per-character settings when window titles change, and handle logged-out clients whose character name may be empty.
- Verify with `dotnet build` at minimum. Publish when producing a runnable EXE for the user.

## Current UX Expectations

The user cares most about speed and crispness:

- Clicking a thumbnail must switch to the EVE client immediately.
- Active border updates must feel instant.
- Dragging/resizing should be smooth, predictable, and snap gently.
- Config UI should feel modern and tactile, not classic WinForms/Windows 98.
- Labels should be readable and clean. The current label implementation uses a separate layered text overlay above the thumbnail.
