# Multi WebView

Multi WebView is a Windows desktop app for opening multiple isolated WebView2 browser profiles. It is useful when you need several signed-in browser sessions at the same time, such as multiple Google accounts, without mixing cookies or local browser data.

## Features

- Create named profiles with their own persistent WebView2 user data folders.
- Open newly created profiles in a one-tile multi-view browser window.
- Select multiple profiles and open them together in a tiled multi-view window.
- Refresh individual WebView tiles from their browser headers.
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

## Usage

1. Start the app.
2. Enter a profile name and optional start URL. Invalid or empty URLs fall back to `https://www.google.com/`.
3. Click `Add profile` to create the profile and open it in a one-profile multi-view window.
4. Click a saved profile card to select it.
5. Use `Create multi-view` to open selected profiles in one tiled window.
6. Use the refresh button, volume slider, and mute button in each browser header to control that profile's WebView.
7. Use the edit and delete buttons on a profile card to manage saved profiles.
8. Use the profile picker's close button to hide it to the system tray. Use the tray menu's `Restore` or the tray icon double-click to bring it back. Use `Alt+F4` or tray menu `Exit` to quit.
9. In a multi-view browser window, use the normal minimize button to minimize to the taskbar or the tray button to hide the window to the system tray. Double-click its tray icon or use tray menu `Restore` to show it again.

Profiles that are already open cannot be selected again until their browser window is closed.

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

Each profile has a stable ID, display name, start URL, timestamps, saved audio state, and a dedicated `webview2` user data folder. Use `Change folder` in the app to move future profile metadata and WebView2 data to another directory.

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
