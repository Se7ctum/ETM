# ETM Handoff

Last updated: 2026-06-28

## Current State

ETM is usable but still in active polish. Recent work focused on thumbnail responsiveness, cleaner labels, modern config UI, reliable position saves, hotkey groups, profile rules, and hotkeys that only register while EVE is active.

The repository should build with .NET 8 on Windows.

## Quick Start For A New Agent

1. Open the repo root.
2. Read `AGENTS.md`, this file, and `docs/PROJECT_SUMMARY.md`.
3. Run git status.
4. Build before editing if possible.
5. Keep edits small and testable.

Commands from repo root:

```powershell
git status --short
dotnet build .\src\ETM.sln
```

Publish command:

```powershell
dotnet publish .\src\ETM\ETM.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -o .\publish
```

If working on the original local machine/repo, paths may be:

```powershell
C:\ETM\tools\PortableGit\cmd\git.exe -C C:\ETM status --short
dotnet build C:\ETM\src\ETM.sln
dotnet publish C:\ETM\src\ETM\ETM.csproj -c Release -p:RestoreSources=https://api.nuget.org/v3/index.json -o C:\ETM\publish
```

## GitHub Push From Original Machine

The remote has been `git@github.com:Se7ctum/ETM.git`.

If the deploy key exists at `C:\ETM\.github-deploy\etm_deploy_key`, push with:

```powershell
$env:GIT_SSH_COMMAND='ssh -i C:/ETM/.github-deploy/etm_deploy_key -o StrictHostKeyChecking=accept-new'
C:\ETM\tools\PortableGit\cmd\git.exe -C C:\ETM push origin main
```

Do not commit secrets. The deploy key path is local machine state and should not be added to Git.

## Runtime Verification Checklist

When changes touch overlays, hotkeys, settings, or config UI, verify manually with real EVE clients when possible:

- Start ETM from `publish\ETM.exe`.
- Confirm only EVE clients appear, not EVE Launcher.
- Confirm logged-out `exefile.exe` clients still show thumbnails.
- Click thumbnails rapidly and confirm client switching feels immediate.
- Confirm active border updates immediately and does not flicker when thumbnails overlap.
- Move thumbnails with right mouse drag.
- Resize one thumbnail; all thumbnails should resize unless Shift is held.
- Use Save position and size, restart ETM, and confirm positions/sizes persist.
- Reboot/session-end reliability depends on settings saving under `%APPDATA%\ETM\settings.json`.
- Test hotkey group cycle order.
- With Hotkeys only while EVE is active enabled, confirm keys are not captured in other apps.
- Open config and confirm Save button becomes dirty immediately on edits, then gives feedback when clicked.

## Recent Behavior Decisions

- `HotkeysRequireEveFocus` defaults to `true`.
- When this setting is enabled, ETM must unregister global hotkeys outside EVE, not just ignore the callback.
- Assigning a thumbnail to a hotkey group from the thumbnail context menu is single-selection behavior. Assigning one group removes the character from other groups.
- EVE Launcher should not be picked up or displayed in the Thumbnails tab.
- Thumbnail labels currently use a separate layered text overlay. Keep label changes crisp and verify visually.
- The top label band was brought back because floating text alone looked blurry/cheap to the user.
- The active border should wrap the entire thumbnail card including the label band, without an internal separator line.

## Known Issues / Follow-Up Ideas

These are not instructions to implement immediately, but useful context:

- There are legacy WinForms tab files under `src/ETM/UI/Tabs/*`; the current config UI is `WpfConfigRoot`. Check references before deleting anything.
- `AppearanceDefaults.LabelBackgroundEnabled` exists in the model but current label rendering may not expose or honor it the way older iterations did. Confirm before changing.
- `OverlayState.ZOrder` exists but active topmost behavior is mostly managed at runtime.
- `ThumbnailOverlay.TextOverlay.GetLocation` currently exists but top-band rendering uses owner top-left/full-width positioning. If label position is revisited, inspect this carefully.
- No automated tests are currently present. Most validation is build plus manual runtime checks.

## Build Warning

`WFAC010` may appear:

```text
Remove high DPI settings from App.manifest and configure via Application.SetHighDpiMode API or ApplicationHighDpiMode project property
```

This warning is known and not currently blocking.

## User Preferences Captured From Iteration

- Performance matters more than decorative UI.
- The app must feel crisp and instant, especially thumbnail click switching and active border state.
- The config UI should be modern, dark, tactile, and not classic WinForms-looking.
- Avoid clutter in thumbnails. Keep labels readable but clean.
- Avoid surprising global key capture outside EVE.
- Prefer practical behavior over extra configuration unless the user asks for more options.
