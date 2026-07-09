# Multi WebView Technical Documentation

This document describes the current architecture and implementation details for future development context.

## Purpose

Multi WebView is a Windows Forms desktop app that opens multiple isolated WebView2 browser profiles. Each profile gets its own persistent WebView2 user data folder, so cookies, Google sign-ins, local storage, and browser data stay separated.

The browser surface is `MultiViewForm`, a tiled browser window with one WebView per profile. `Add profile` opens the new profile in a one-tile multi-view window, and `Create multi-view` opens the selected profiles together.

The app also persists and enforces per-profile audio volume and mute state using Windows Core Audio sessions for the WebView2 process tree.

## Technology Stack

- C# / Windows Forms
- .NET target: `net10.0-windows`
- WebView runtime: Microsoft Edge WebView2
- NuGet package: `Microsoft.Web.WebView2`
- Persistence: JSON files under the configured profile storage folder
- Audio control: Windows Core Audio COM interfaces plus WebView2 `CoreWebView2.IsMuted`

## Entry Point And Single Instance Flow

The application starts in `Program.Main`.

Startup behavior:

1. `Program` creates a named mutex: `MultiWebView.SingleInstance`.
2. If another instance already owns the mutex, the new process signals the existing instance through the named event `MultiWebView.Activate` and exits.
3. The first instance initializes Windows Forms with `ApplicationConfiguration.Initialize()`.
4. It creates and runs `ProfilePickerForm`.
5. `ProfilePickerForm.HandleCreated` starts a thread-pool wait on the activation event.
6. When a later launch signals the event, the existing picker restores itself from tray or minimized state and comes to the foreground.

Important files:

- `Program.cs`: startup, mutex, activation event.
- `ProfilePickerForm.cs`: main picker UI and external activation handling.

## Profile Model

Profiles are represented by `Profile`.

Current fields:

- `Id`: stable GUID-like ID, generated as `N` format.
- `Name`: display name shown in the picker and browser headers.
- `CreatedAt`: UTC creation timestamp.
- `LastUsedAt`: UTC timestamp used for picker sorting.
- `StartUrl`: normalized HTTP/HTTPS URL opened for that profile.
- `VolumePercent`: saved audio volume, clamped to `0..100`.
- `IsMuted`: saved mute state.

The profile ID is used as the persistent folder name and the key for open/selected profile tracking.

## Profile Storage

`ProfileStore` owns profile persistence and storage folder settings.

Default storage:

```text
%LOCALAPPDATA%\MultiWebView\Profiles
```

Settings file:

```text
%LOCALAPPDATA%\MultiWebView\settings.json
```

Main profile index:

```text
<AppDataPath>\profiles.json
```

Per-profile snapshot:

```text
<AppDataPath>\<profile-id>\profile.json
```

WebView2 user data folder:

```text
<AppDataPath>\<profile-id>\webview2
```

`ProfileStore.LoadProfiles()` creates the storage directory if needed, creates an empty profile index if missing, normalizes URLs and volume values, and returns profiles ordered by descending `LastUsedAt`.

`ProfileStore.ChangeProfileFolder()` changes only the future active storage root. It creates a new empty profile index if the chosen folder does not already contain one.

## Main Picker UI

`ProfilePickerForm` is the main app window.

Responsibilities:

- Load saved profiles.
- Create, edit, and delete profiles.
- Track selected profiles for multi-view.
- Track currently open profile IDs so a profile cannot be opened twice at the same time.
- Open newly created profiles in one-tile `MultiViewForm` windows.
- Open selected profiles together in tiled `MultiViewForm` windows.
- Minimize to tray and restore from tray.
- Keep the picker topmost when pinned.

Open profile tracking:

- `openProfileIds` prevents duplicate browser sessions for the same profile.
- `TrackOpenWindow()` adds profile IDs when a window opens.
- The window `FormClosed` handler removes those IDs and refreshes the picker.

Selection:

- Clicking a profile card toggles its ID in `selectedProfileIds`.
- The "Create multi-view" button appears only when at least one unopened profile is selected.

## Multi-View Window

`MultiViewForm` hosts multiple profiles in a tiled grid.

Grid layout:

- Column count is `ceil(sqrt(profileCount))`.
- Row count is `ceil(profileCount / columns)`.
- Each tile is a `TableLayoutPanel` with a compact header and a `WebView2`.

Per-tile state:

- `volumeByWebView`: current volume value for each WebView.
- `mutedByWebView`: current mute state for each WebView.
- The profile object supplies the display name and start URL.

Initialization flow:

1. Constructor builds the title bar and grid.
2. Each tile creates a `WebView2`.
3. `WebViewVolumeController.Attach(...)` starts audio enforcement immediately for each WebView control.
4. `Shown` maximizes the window and calls `InitializeWebViewsAsync()`.
5. Each profile gets its own WebView2 environment and user data folder.
6. Each WebView ensures CoreWebView2, creates the early silent audio session, applies saved audio state, and then navigates.

The multi-view window has a single outer title bar with minimize, pin, maximize/restore, and close controls. Each tile has its own name, refresh button, mute button, volume value, and volume slider.

## WebView2 Environment

`WebViewEnvironmentFactory` creates a `CoreWebView2Environment` with profile-specific user data.

Browser arguments currently configured:

- Disable background timer throttling.
- Disable backgrounding occluded windows.
- Disable renderer backgrounding.
- Disable native occlusion and intensive wake-up throttling features.
- Enable GPU rasterization.
- Enable zero-copy.
- Ignore GPU blocklist.
- Disable autoplay user gesture requirements.

The autoplay flag is important for the early silent Web Audio session. Without it, Chromium can suspend the audio context until a user gesture, which would delay Windows Volume Mixer session creation.

## Audio Design

Audio is handled by `WebViewVolumeController`.

There are three layers:

1. WebView2 mute:
   `CoreWebView2.IsMuted` is set to the profile mute state.

2. Windows Core Audio session control:
   The controller enumerates Windows render audio sessions and finds sessions whose process ID belongs to the WebView2 browser process tree. Matching sessions receive:
   - `ISimpleAudioVolume.SetMasterVolume(...)`
   - `ISimpleAudioVolume.SetMute(...)`
   - `IAudioSessionControl.SetDisplayName(...)`

3. Early session creation:
   After `EnsureCoreWebView2Async` and before navigation, the controller executes a script that creates a zero-gain Web Audio oscillator connected to the destination. This is intentionally inaudible, but it causes WebView2/Chromium to create an audio session early so Windows Volume Mixer can show it before the real page plays sound.

### Audio Lifecycle

`Attach(...)` starts a WinForms timer as soon as the `WebView2` control exists.

Timer behavior:

- Interval: 1000 ms.
- Before `CoreWebView2` exists, apply calls no-op.
- After CoreWebView2 exists, the timer reapplies volume/mute/display-name state every second.
- A simple `isApplying` flag prevents overlapping timer work.
- The timer is stopped and disposed when the WebView control is disposed.

`ConfigureAsync(...)` applies the current state immediately after CoreWebView2 is ready.

`EnsureAudioSessionAsync(...)` registers and executes the silent Web Audio script. The script is also added with `AddScriptToExecuteOnDocumentCreatedAsync` so it can run again on future documents.

### Audio Caveats

Windows 11 Volume Mixer may still display the row as `Microsoft Edge WebView2` even after `SetDisplayName(profile.Name)` is called. The modern mixer appears to prefer the WebView2 runtime app identity for the row label. The session display name call is kept because it is the correct Core Audio API and may affect other audio UIs or older mixer surfaces.

The silent audio session is a workaround. It exists only to force early mixer-session creation and has gain set to `0`, so it should not produce audible sound.

## Custom Controls

`ActionButtonControl`

- Custom-painted rectangular action button.
- Handles hover state, border, and centered ellipsized text.
- Used in picker and edit dialog action buttons.

`VolumeSliderControl`

- Custom-painted slider for `0..100` audio volume.
- Supports click and drag.
- Emits `ValueChanged` when the value changes.
- Used in multi-view tile headers.

`ProfilePickerForm.AvatarControl`

- Small custom-painted initials block for profile cards.
- Uses the first one or two name parts.

## Windowing Notes

The app uses borderless windows with custom title bars.

Dragging is implemented with Win32:

- `ReleaseCapture()`
- `SendMessage(handle, WM_NCLBUTTONDOWN, HTCAPTION, 0)`

Pinning sets `TopMost`.

Maximize behavior differs slightly:

- `ProfilePickerForm` uses `WindowState`.
- `MultiViewForm` manually stores previous bounds and sets bounds to `Screen.WorkingArea`.

## Build And Run

From the repository root:

```powershell
dotnet restore .\MultiWebView.slnx
dotnet build .\MultiWebView.slnx
dotnet run --project .\MultiWebView\MultiWebView.csproj
```

In the current managed sandbox, normal `dotnet build` can fail with access denied while writing generated files under `MultiWebView\obj`. Running the same build with normal local filesystem permissions succeeds.

## Current Git Context

At the time this document was added, the audio-session changes were already committed locally as:

```text
3d8d69e Improve WebView audio session handling
```

The push to `origin/main` failed because GitHub rejected SSH authentication:

```text
git@github.com: Permission denied (publickey).
```

The local `main` branch was ahead of `origin/main` by that commit.

## Development Guidance

Prefer keeping audio behavior centralized in `WebViewVolumeController`. `MultiViewForm` should pass profile-specific state into the controller, but it should not duplicate Core Audio enumeration logic.

When changing profile persistence, update both the top-level `profiles.json` flow and the per-profile `profile.json` snapshot flow in `ProfileStore.SaveProfiles()`.

When changing WebView creation, preserve the order:

1. Create WebView control.
2. Attach audio enforcement.
3. Ensure CoreWebView2.
4. Ensure early audio session.
5. Apply saved audio state.
6. Navigate.

This order minimizes the window where Windows can create a WebView2 audio session with the wrong default volume.
