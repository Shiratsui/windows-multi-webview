# Multi WebView

Multi WebView is a Windows desktop app for opening multiple isolated WebView2 browser profiles. It is useful when you need several signed-in browser sessions at the same time, such as multiple Google accounts, without mixing cookies or local browser data.

## Features

- Create named profiles with their own persistent WebView2 user data folders.
- Open newly created profiles in a one-tile multi-view browser window.
- Select multiple profiles and open them together in a tiled multi-view window.
- Show opened profile names in multi-view window titles and tray tooltips.
- Use distinct runtime window and tray icons for the profile picker and multi-view browser windows.
- Show profile state chips: grey `OFF`, green `OPEN`, orange `TRAY`, plus a red `KEEP RUNNING` chip when the owning multi-view window is in keep-running tray mode.
- Recreate individual WebView tiles from their browser headers when a full WebView refresh is needed.
- Save a PNG screenshot of an individual WebView tile to that profile's `screenshots` folder, with a clickable status popup after capture.
- Open an individual profile folder from its WebView tile header.
- Show an optional per-tile stats overlay for FPS, render latency, CPU, and memory.
- Control and persist volume and mute state per profile.
- Edit or delete saved profiles from the profile picker when they are not currently open.
- Change and open the profile storage folder from the app.
- Close the profile picker to the system tray and restore it from the tray icon.
- Move multi-view browser windows to the system tray with a dedicated tray button.
- Choose `Default` or `Keep running` from each multi-view window's tray dropdown before sending it to the tray.
- Keep windows on top with the pin button.
- Single-instance startup: launching the app again focuses the existing picker.

## Requirements

- Windows
- .NET SDK that supports `net10.0-windows`
- Microsoft Edge WebView2 Runtime

The app depends on the `Microsoft.Web.WebView2` NuGet package. Package restore is handled by the .NET SDK.

## License

This project is released under the MIT License. See `LICENSE`.

## Changelog

See `CHANGELOG.md` for version-by-version change notes.

## Security And Privacy

Multi WebView stores profile metadata, settings, screenshots, and WebView2 browser data locally under `%LOCALAPPDATA%\MultiWebView` by default. Each app profile has its own WebView2 user data folder, so cookies and browser sessions are isolated per profile.

The app does not encrypt local WebView2 profile data. Anyone with access to the Windows user account or profile storage folder may be able to inspect local browser data.

Do not publish real profile folders, cookies, screenshots, or logs that contain private account data. See `SECURITY.md` for reporting and public-sharing guidance.

## Build and Run

From the repository root:

```powershell
dotnet restore .\MultiWebView.slnx
dotnet build .\MultiWebView.slnx
dotnet run --project .\MultiWebView\MultiWebView.csproj
```

## Publish

Create a Windows x64 release build:

```powershell
dotnet publish .\MultiWebView\MultiWebView.csproj -c Release -r win-x64 --self-contained false
```

The published files are written to:

```text
MultiWebView\bin\Release\net10.0-windows\win-x64\publish
```

## Installer

The project includes an Inno Setup installer definition at:

```text
installer\MultiWebView.iss
```

To build the installer locally, install Inno Setup 6, then run:

```powershell
.\scripts\build-installer.ps1 -Version 0.4.0 -SelfContained
```

Outputs are written under:

```text
artifacts\
```

The installer is per-user, does not require administrator privileges, creates Start Menu and optional desktop shortcuts, and includes an automatic uninstaller. Uninstalling removes the installed app files and shortcuts, but leaves profile data and settings under `%LOCALAPPDATA%\MultiWebView`.

## GitHub Releases

The release workflow is defined at:

```text
.github\workflows\release.yml
```

The workflow runs when a version tag such as `v0.4.0` is pushed. It builds a self-contained Windows x64 publish, packages an Inno Setup installer, creates a portable zip, and attaches both files to a GitHub Release.

Before releasing, review the working tree, stage the intended source and documentation changes, then commit and push:

```powershell
git status
git add <paths>
git commit -m "Prepare release"
git push origin main
```

If the repository uses `master` instead of `main`, push `master`:

```powershell
git push origin master
```

Create and push a release tag:

```powershell
git tag v0.4.0
git push origin v0.4.0
```

After pushing the tag:

1. Open the repository on GitHub.
2. Go to `Actions`.
3. Open the `Release` workflow run.
4. Wait for it to complete successfully.
5. Go to `Releases`.
6. Download the generated installer and portable zip.

Expected release assets:

```text
MultiWebViewSetup-0.4.0-win-x64.exe
MultiWebView-0.4.0-win-x64-portable.zip
```

For the next release, use a higher version tag:

```powershell
git tag v0.4.1
git push origin v0.4.1
```

You can also run the `Release` workflow manually from GitHub Actions and provide a version such as `0.4.0`.

## Public Repository Checklist

Before making the repository public:

1. Confirm no secrets are tracked:

```powershell
rg -n --hidden --glob '!bin/**' --glob '!obj/**' --glob '!artifacts/**' --glob '!.git/**' "(?i)(password|token|secret|api[_-]?key|private[_-]?key|client[_-]?secret|authorization|bearer|BEGIN (RSA|OPENSSH|PRIVATE))" .
```

2. Confirm ignored local files are not tracked:

```powershell
git status --short --ignored
git ls-files -- MultiWebView/MultiWebView.csproj.user mock profile-picker-render.png
```

3. Review release workflow permissions before accepting outside contributions. The release workflow uses `contents: write` so it can create GitHub Releases.

4. Expect Windows SmartScreen warnings for unsigned installer builds. Reducing those warnings requires code signing.

## Usage

1. Start the app.
2. Enter a profile name and optional start URL. Invalid or empty URLs fall back to `https://www.google.com/`.
3. Click `Add profile` to create the profile and open it in a one-profile multi-view window.
4. Click a saved profile card to select it.
5. Use `Create multi-view` to open selected profiles in one tiled window.
6. Use the refresh button, screenshot button, profile folder button, `STAT` menu, volume slider, and mute button in each browser header to control that profile's WebView. The refresh button recreates that tile's WebView instead of only reloading the current page.
7. Use the edit and delete buttons on a profile card to manage saved profiles. These buttons are disabled while that profile is open.
8. Use the profile picker's close button to hide it to the system tray. Use the tray menu's `Restore` or the tray icon double-click to bring it back. Use `Alt+F4` or tray menu `Exit` to quit.
9. In a multi-view browser window, use the normal minimize button to minimize to the taskbar. Use the tray dropdown to choose `Default` for normal hidden tray mode or `Keep running` for game-friendly offscreen tray mode. While the window is in the tray, right-click its tray icon and toggle the checked `Keep Running` item without restoring. Double-click its tray icon or use tray menu `Restore` to show it again.

Profile cards show a grey `OFF`, green `OPEN`, or orange `TRAY` chip. If the owning browser window is in `Keep running` tray mode, the card also shows a red `KEEP RUNNING` chip. Open profiles cannot be selected, edited, or deleted again until their browser window is closed. Clicking an open profile card restores or focuses the existing browser window, including windows minimized to the taskbar or sent to the system tray. Deleting a closed profile uses the app's custom confirmation dialog before removing the saved browser data.

## Stats Overlay

Each WebView tile has a `STAT` menu with checkboxes for:

- `FPS`: page render frames per second, measured with `requestAnimationFrame`.
- `CPU`: approximate CPU usage for the WebView2 process tree.
- `Memory`: approximate working set memory for the WebView2 process tree.
- `Horizontal`: display selected stats on one line separated by `|` instead of stacked vertically.

When `FPS` is enabled, the overlay also shows `LAT`, which is render frame time in milliseconds, not network ping or server latency.

Stats selections are saved per profile, so reopening the same profile restores its last selected overlay options. The stats menu stays open while toggling options, closes when `STAT` is clicked again, and closes when the user clicks the page, clicks outside the menu, or switches to another application. GPU usage is not shown because WebView2 and normal Windows APIs do not expose reliable per-WebView GPU utilization.

## Audio Controls

Each browser header includes a refresh button, mute button, and volume slider. The refresh button destroys and recreates that tile's WebView, then initializes it with the same profile data, audio state, and stats settings. The audio setting is saved per profile, so reopening the same profile restores its last volume and mute state.

Volume is applied through the Windows audio session for the WebView2 process tree and is reapplied while the WebView is open. This keeps the saved profile volume and mute state in place even if WebView2 recreates its audio sessions.

When a multi-view browser window is moved to the system tray, the app force-mutes its WebViews at runtime. Restoring the window reapplies each profile's normal saved or current mute state.

When a WebView is initialized, the app creates an inaudible Web Audio session before the first navigation. This gives Windows Volume Mixer a session to display and lets the app apply the saved volume before the page plays real audio.

## Profile Storage

By default, profiles are stored under:

```text
%LOCALAPPDATA%\MultiWebView\Profiles
```

The app also stores its settings at:

```text
%LOCALAPPDATA%\MultiWebView\settings.json
```

The last selected multi-view tray mode is saved in this settings file as `KeepWebViewsRunningInTray`. `Keep running` keeps tray windows alive offscreen instead of hiding them, which helps games and animation-heavy pages avoid hidden-window throttling. `Default` hides the window normally to reduce rendering resource use while keeping pages and network activity alive.

Each profile has a stable ID, display name, start URL, timestamps, saved audio state, saved stats overlay options, and a dedicated `webview2` user data folder. Use `Change folder` in the app to move future profile metadata and WebView2 data to another directory.

Screenshots are saved automatically under:

```text
<AppDataPath>\<profile-id>\screenshots
```

Screenshot files use the profile name and local timestamp:

```text
<profile-name>-yyyyMMdd-HHmmss.png
```

## Project Structure

```text
MultiWebView/
  Program.cs                    App entry point and single-instance activation
  ProfilePickerForm.cs          Main profile picker UI
  MultiViewForm.cs              Tiled multi-profile browser window
  ProfileStore.cs               Profile persistence and storage settings
  WebViewEnvironmentFactory.cs  WebView2 environment options
  WebViewVolumeController.cs    Windows audio session volume control
  WindowIdentity.cs             Runtime window titles and generated window/tray icons
  VolumeSliderControl.cs        Custom-painted volume slider
  Profile.cs                    Profile model
```

See `TECHNICAL.md` for deeper architecture notes, lifecycle details, storage behavior, and audio implementation context.

## Notes

- Browser windows use borderless custom title bars with maximize, close, and pin controls where applicable. Multi-view titles include the opened profile names, while the app executable, installer, and Start Menu shortcut still use the packaged app icon. The profile picker close button hides to tray; multi-view windows have separate taskbar-minimize and tray controls. The multi-view tray dropdown offers `Default`, which hides the window normally, and `Keep running`, which keeps the WebView host window alive offscreen so pages are less likely to be throttled as hidden. The multi-view tray icon menu can switch between those modes without restoring the window. `Alt+F4` exits from the picker. Each WebView tile has its own refresh control, which recreates the WebView control and initializes it again for the same profile.
- WebView2 is created with a profile-specific user data folder so each profile keeps separate cookies, sessions, and local storage.
- Additional WebView2 browser arguments are configured to reduce background throttling for active multi-window use.
- Per-profile audio is controlled through Windows Core Audio sessions. A silent Web Audio graph is used only to create the mixer session early.
