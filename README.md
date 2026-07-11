# Multi WebView

Multi WebView is a Windows desktop app for opening multiple isolated WebView2 browser profiles. It is useful when you need several signed-in browser sessions at the same time, such as multiple Google accounts, without mixing cookies or local browser data.

## Features

- Create named profiles with their own persistent WebView2 user data folders.
- Open newly created profiles in a one-tile multi-view browser window.
- Select multiple profiles and open them together in a tiled multi-view window.
- Refresh individual WebView tiles from their browser headers.
- Save a PNG screenshot of an individual WebView tile to that profile's `screenshots` folder, with a clickable status popup after capture.
- Open an individual profile folder from its WebView tile header.
- Show an optional per-tile stats overlay for FPS, render latency, CPU, and memory.
- Control and persist volume and mute state per profile.
- Edit or delete saved profiles from the profile picker.
- Change and open the profile storage folder from the app.
- Close the profile picker to the system tray and restore it from the tray icon.
- Hide multi-view browser windows to the system tray with a dedicated tray button.
- Keep windows on top with the pin button.
- Single-instance startup: launching the app again focuses the existing picker.

## Requirements

- Windows
- .NET SDK that supports `net10.0-windows`
- Microsoft Edge WebView2 Runtime

The app depends on the `Microsoft.Web.WebView2` NuGet package. Package restore is handled by the .NET SDK.

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
.\scripts\build-installer.ps1 -Version 0.1.0 -SelfContained
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

The workflow runs when a version tag such as `v0.1.0` is pushed. It builds a self-contained Windows x64 publish, packages an Inno Setup installer, creates a portable zip, and attaches both files to a GitHub Release.

Before releasing, commit and push the current changes:

```powershell
git status
git add .gitignore README.md MultiWebView/MultiWebView.csproj installer scripts .github
git commit -m "Add Windows installer and GitHub release workflow"
git push origin main
```

If the repository uses `master` instead of `main`, push `master`:

```powershell
git push origin master
```

Create and push a release tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
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
MultiWebViewSetup-0.1.0-win-x64.exe
MultiWebView-0.1.0-win-x64-portable.zip
```

For the next release, use a higher version tag:

```powershell
git tag v0.1.1
git push origin v0.1.1
```

You can also run the `Release` workflow manually from GitHub Actions and provide a version such as `0.1.0`.

## Usage

1. Start the app.
2. Enter a profile name and optional start URL. Invalid or empty URLs fall back to `https://www.google.com/`.
3. Click `Add profile` to create the profile and open it in a one-profile multi-view window.
4. Click a saved profile card to select it.
5. Use `Create multi-view` to open selected profiles in one tiled window.
6. Use the refresh button, screenshot button, profile folder button, `STAT` menu, volume slider, and mute button in each browser header to control that profile's WebView.
7. Use the edit and delete buttons on a profile card to manage saved profiles.
8. Use the profile picker's close button to hide it to the system tray. Use the tray menu's `Restore` or the tray icon double-click to bring it back. Use `Alt+F4` or tray menu `Exit` to quit.
9. In a multi-view browser window, use the normal minimize button to minimize to the taskbar or the tray button to hide the window to the system tray. Double-click its tray icon or use tray menu `Restore` to show it again.

Profiles that are already open cannot be selected again until their browser window is closed.

## Stats Overlay

Each WebView tile has a `STAT` menu with checkboxes for:

- `FPS`: page render frames per second, measured with `requestAnimationFrame`.
- `CPU`: approximate CPU usage for the WebView2 process tree.
- `Memory`: approximate working set memory for the WebView2 process tree.
- `Horizontal`: display selected stats on one line separated by `|` instead of stacked vertically.

When `FPS` is enabled, the overlay also shows `LAT`, which is render frame time in milliseconds, not network ping or server latency.

Stats selections are saved per profile, so reopening the same profile restores its last selected overlay options. The stats menu stays open while toggling options, closes when `STAT` is clicked again, and closes when the user clicks the page, clicks outside the menu, or switches to another application. GPU usage is not shown because WebView2 and normal Windows APIs do not expose reliable per-WebView GPU utilization.

## Audio Controls

Each browser header includes a refresh button, mute button, and volume slider. The audio setting is saved per profile, so reopening the same profile restores its last volume and mute state.

Volume is applied through the Windows audio session for the WebView2 process tree and is reapplied while the WebView is open. This keeps the saved profile volume and mute state in place even if WebView2 recreates its audio sessions.

When a multi-view browser window is hidden to the system tray, the app force-mutes its WebViews at runtime. Restoring the window reapplies each profile's normal saved or current mute state.

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
  VolumeSliderControl.cs        Custom-painted volume slider
  Profile.cs                    Profile model
```

See `TECHNICAL.md` for deeper architecture notes, lifecycle details, storage behavior, and audio implementation context.

## Notes

- Browser windows use borderless custom title bars with maximize, close, and pin controls where applicable. The profile picker close button hides to tray; multi-view windows have separate taskbar-minimize and tray-hide controls. `Alt+F4` exits from the picker. Each WebView tile has its own refresh control.
- WebView2 is created with a profile-specific user data folder so each profile keeps separate cookies, sessions, and local storage.
- Additional WebView2 browser arguments are configured to reduce background throttling for active multi-window use.
- Per-profile audio is controlled through Windows Core Audio sessions. A silent Web Audio graph is used only to create the mixer session early.
