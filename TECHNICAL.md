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
- Installer: Inno Setup 6
- CI/CD: GitHub Actions on Windows runners

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
- `ShowStatsFps`: saved stats overlay FPS setting.
- `ShowStatsCpu`: saved stats overlay CPU setting.
- `ShowStatsMemory`: saved stats overlay memory setting.
- `ShowStatsGpu`: saved stats overlay GPU utilization setting.
- `ShowStatsGpuMemory`: saved stats overlay GPU memory setting.
- `ShowStatsHorizontal`: saved stats overlay horizontal layout setting.

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

Settings fields:

- `ProfilesPath`: active profile storage root.
- `KeepWebViewsRunningInTray`: last selected multi-view tray mode. The title-bar tray dropdown order is fixed as `Default`, then `Keep running`.

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
- Create, edit, and delete profiles. Edit and delete actions are disabled for currently open profiles.
- Track selected profiles for multi-view.
- Track currently open profile IDs so a profile cannot be opened twice at the same time.
- Track open profile windows so clicking an already-open profile can restore or focus the existing `MultiViewForm`.
- Show a live dark hover popup for open profile cards with CPU, memory, GPU, and GPU memory values sampled from the owning `MultiViewForm`.
- Open newly created profiles in one-tile `MultiViewForm` windows.
- Open selected profiles together in tiled `MultiViewForm` windows.
- Hide to tray from the custom close button and restore from tray.
- Keep the picker topmost when pinned.

Open profile tracking:

- `openProfileIds` prevents duplicate browser sessions for the same profile.
- `openProfileWindows` maps each open profile ID to the owning `MultiViewForm`.
- `TrackOpenWindow()` adds profile IDs and their owning window when a window opens.
- The window `FormClosed` handler removes those IDs and refreshes the picker.
- Profile cards render a state chip: grey `OFF`, green `OPEN`, or orange `TRAY`. If the owning `MultiViewForm` is currently in keep-running tray mode, the card also shows a red `KEEP RUNNING` chip. `TrackOpenWindow(...)` subscribes to `MultiViewForm.TrayStateChanged` so cards refresh when tray mode changes. Open cards disable their edit and delete buttons and call `ActivateOpenProfileWindow(...)` when clicked, which uses `MultiViewForm.ActivateFromProfilePicker()` to restore tray-hidden windows, restore taskbar-minimized windows, and bring visible windows forward.
- Hovering an open profile card shows a borderless dark popup owned by the picker. The popup updates once per second by calling `MultiViewForm.GetProfileUsageAsync(profileId)` on the owning browser window, then hides when the pointer leaves the card, the picker is sent to tray, or the card is activated.

Selection:

- Clicking a profile card toggles its ID in `selectedProfileIds`.
- Clicking an already-open profile card does not toggle selection; it activates the existing browser window instead.
- The "Create multi-view" button appears only when at least one unopened profile is selected.

Profile management dialogs:

- Profile editing uses a custom borderless dark dialog owned by `ProfilePickerForm`.
- Profile deletion uses a matching custom confirmation dialog instead of the default Windows message box.
- `EditProfile(...)` and `DeleteProfile(...)` defensively return without changes if the profile is currently open.

## Multi-View Window

`MultiViewForm` hosts multiple profiles in a tiled grid.

Grid layout:

- Column count is `ceil(sqrt(profileCount))`.
- Row count is `ceil(profileCount / columns)`.
- Each tile is a `TableLayoutPanel` with a compact header and a `WebView2`.

Per-tile state:

- `volumeByWebView`: current volume value for each WebView.
- `mutedByWebView`: current mute state for each WebView.
- `statsByWebView`: current stats overlay options and CPU/memory sampling state for each WebView.
- The profile object supplies the display name and start URL.

Initialization flow:

1. Constructor builds the title bar and grid.
2. Each tile creates a `WebView2`.
3. `WebViewVolumeController.Attach(...)` starts audio enforcement immediately for each WebView control.
4. `Load` maximizes the window before first visible paint to avoid a startup resize flash.
5. `Shown` calls `InitializeWebViewsAsync()`.
6. Each profile gets its own WebView2 environment and user data folder.
7. Each WebView ensures CoreWebView2, creates the early silent audio session, applies saved audio state, and then navigates.

The multi-view window has a single outer title bar with taskbar minimize, tray-mode dropdown, pin, maximize/restore, and close controls. Each tile has its own name, refresh button, screenshot button, profile folder button, stats menu, mute button, volume value, and volume slider.

The tile refresh button performs a full WebView recreation rather than a page reload. `RecreateWebViewAsync(...)` closes open stats menus, creates a new `WebView2`, replaces the old control in `webViews`, carries over the current volume, mute, and stats state, attaches audio enforcement, removes and disposes the old control and stats timer, then calls `InitializeWebViewAsync(...)` for the new control. Initial startup and refresh use the same initialization path so WebView2 environment creation, early audio-session setup, saved audio application, stats overlay setup, and navigation stay consistent.

`WindowIdentity.BuildMultiViewTitle(...)` builds the multi-view form title from the opened profile names. `WindowIdentity.BuildTrayText(...)` reuses that title for the tray icon tooltip and truncates it to the Windows notify-icon text limit.

### Stats Overlay

Each WebView tile has a `STAT` button that opens a custom dark `ContextMenuStrip` rendered by `StatsMenuRenderer`. The menu contains checkboxes for:

- `FPS`
- `CPU`
- `Memory`
- `GPU`
- `GPU VRAM`
- `Horizontal`

The menu uses `AutoClose = false` so selecting several stats does not close and reopen the dropdown. `StatsMenuMessageFilter` closes the menu when the user clicks outside the menu or `STAT` button. The WebView surface is a separate child browser HWND, so WebView page clicks are handled by injecting a small script that posts `__multi_webview_close_stats_menu` through `chrome.webview.postMessage(...)`; the host receives that message and calls `CloseStatsMenus()`. `MultiViewForm.Deactivate` also closes open stats menus when the user switches to another application.

The stats overlay itself is injected into the page with `ExecuteScriptAsync(...)`. It creates a fixed-position element with ID `__multi_webview_stats_overlay` and updates it from two sources:

- In-page `requestAnimationFrame` loop for FPS and `LAT`.
- Host-side timer for CPU, memory, GPU, and GPU memory samples.

`LAT` is render frame time in milliseconds. It is not network ping or server latency.

Stats selections are persisted per profile through `ProfileStore.UpdateProfileStats(...)`. `CreateTile(...)` initializes each tile's `StatsOverlayState` and check marks from the profile's saved stats fields. After the first navigation completes, saved stats are applied by refreshing the injected overlay and starting the stats timer when needed.

CPU, memory, GPU, and GPU memory are sampled approximately from the WebView2 process tree. The root is `CoreWebView2.BrowserProcessId`; the process tree is found by reusing `WebViewVolumeController.GetProcessTreeIds(...)`. CPU is computed from the delta of summed `Process.TotalProcessorTime` over elapsed wall time and normalized by `Environment.ProcessorCount`. Memory is the summed `WorkingSet64` of the same process tree.

GPU utilization and GPU VRAM use Windows performance counters. `MultiViewForm` gets the active WebView2 process IDs from `CoreWebView2Environment.GetProcessInfos()`, then `GpuStatsSampler` sums matching `GPU Engine` utilization instances and `GPU Process Memory` local/dedicated memory instances. GPU utilization counters are cached because Windows percentage counters need a previous sample before they can report useful values. These values are process-level approximations; Chromium can share GPU work across WebView2 processes, and Windows counter availability can vary by GPU driver and OS configuration.

Performance impact is controlled by when sampling runs. A WebView tile starts its stats timer only while at least one overlay option is enabled, and `ProfilePickerForm` starts its profile usage timer only while hovering an open profile card. CPU and memory sampling are simple process snapshots. GPU and GPU memory sampling are heavier because they enumerate Windows performance counters, so they should be treated as diagnostic metrics rather than always-on game telemetry.

The overlay supports vertical and horizontal layout. In horizontal mode, selected values are joined with `|`. The FPS and frame-time values are stored on `window.__multiWebViewStats...` fields so host-side CPU/memory refreshes do not reset them to placeholder values.

Per-tile screenshots use WebView2 `CoreWebView2.CapturePreviewAsync(...)` with PNG output. The screenshot button captures the currently visible WebView viewport and saves it with a sanitized profile-name-and-timestamp filename under:

```text
<AppDataPath>\<profile-id>\screenshots
```

After a successful capture, `ShowTemporaryStatus(...)` displays a short-lived status popup anchored to the WebView tile. Clicking the popup opens the profile's screenshots folder. Capture failures are intentionally swallowed and do not show an error dialog.

Multi-view tray behavior:

- The taskbar minimize button sets `WindowState = FormWindowState.Minimized`.
- The tray button opens a per-window dropdown with `Default` and `Keep running`. `Default` hides the form normally. `Keep running` removes the form from the taskbar and moves it outside the virtual desktop.
- The multi-view tray icon context menu includes a checked `Keep Running` item, which toggles between keep-running offscreen mode and default hidden mode without restoring the window.
- Double-clicking the tray icon or choosing `Restore` from its tray menu shows the same form again without recreating the WebViews.
- Choosing `Close` from the tray menu closes the multi-view window and releases its profiles through the existing `FormClosed` tracking in `ProfilePickerForm`.
- While in the tray, `isMinimizedToTray` is included in the mute state passed to `WebViewVolumeController`, so the existing audio enforcement timer keeps all WebViews muted until restore.

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
- For multi-view windows hidden to tray, the effective mute state is `isMinimizedToTray || profileMuteState`; this is intentionally runtime-only and does not overwrite the saved profile mute setting.
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

`DarkTrayMenuRenderer`

- Custom renderer for profile picker and multi-view tray context menus.
- Draws the tray menu with a dark background, dark hover state, border, and vertically centered white text.

`StatsMenuRenderer`

- Custom renderer for the per-tile `STAT` dropdown.
- Draws a dark menu, dark hover state, custom checkbox boxes, green check marks, and vertically centered menu text.

`WindowIdentity`

- Builds explanatory multi-view window titles from opened profile names.
- Generates runtime window and tray icons so the profile picker and multi-view browser windows are distinguishable in taskbar and tray surfaces.
- The generated runtime icons do not replace the packaged executable, installer, or Start Menu shortcut icon.

## Windowing Notes

The app uses borderless windows with custom title bars.

Title-bar dragging waits until the pointer moves outside `SystemInformation.DragSize`, so a plain click does not restore or move a maximized window. Once dragging starts, it is implemented with Win32:

- `ReleaseCapture()`
- `SendMessage(handle, WM_NCLBUTTONDOWN, HTCAPTION, 0)`

Pinning sets `TopMost`.

Profile picker close behavior:

- The custom close button hides the picker to the system tray.
- The picker stores the previous `WindowState`, hides directly with `Hide()` without setting `WindowState.Minimized`, and restores the previous state from the tray.
- The picker does not toggle `ShowInTaskbar` during tray hide. A hidden form is already removed from the taskbar, and avoiding that taskbar-style transition prevents maximized borderless picker windows from briefly flashing back onscreen.
- The close button hover color is reset before hiding and after restore because hiding the form can skip normal mouse-leave handling.
- `Alt+F4` and tray menu `Exit` close the application.

Multi-view tray behavior:

- The normal minimize button minimizes the multi-view window to the taskbar.
- The separate tray button opens a dropdown with two modes in a fixed order: `Default`, then `Keep running`. `Keep running` sets `ShowInTaskbar = false` and moves the multi-view window outside the virtual desktop instead of calling `Hide()`. Keeping the native host window visible avoids pushing WebView2 into the fully hidden-window path, which can throttle page timers or rendering. `Default` calls `Hide()` after removing the window from normal interaction, which saves rendering resources while keeping page and network activity alive. The last selected mode is persisted as `KeepWebViewsRunningInTray`.
- The multi-view tray icon has a checked `Keep Running` toggle, `Restore`, and `Close` menu items and restores on double-click. Switching from `Default` to `Keep running` shows the hidden form invisibly, moves it offscreen, removes it from the taskbar, and refreshes picker chips through `TrayStateChanged`. Switching back to `Default` first moves the offscreen form back to its saved restore bounds, temporarily returns it to the normal taskbar-visible window state, then hides it through the same normal hidden path used by default tray minimize.
- Tray-mode multi-view windows are force-muted through the same `WebViewVolumeController` state path used by the periodic audio enforcement timer.

Maximize behavior:

- `ProfilePickerForm` and `MultiViewForm` manually store previous bounds and set bounds to `Screen.WorkingArea`, so borderless maximized windows respect the taskbar instead of covering it.
- `MultiViewForm` performs its initial maximize during `Load`, before the form is first shown, so profile opening does not briefly display the normal startup size before expanding.

## Build And Run

From the repository root:

```powershell
dotnet restore .\MultiWebView.slnx
dotnet build .\MultiWebView.slnx
dotnet run --project .\MultiWebView\MultiWebView.csproj
```

In the current managed sandbox, normal `dotnet build` can fail with access denied while writing generated files under `MultiWebView\obj`. Running the same build with normal local filesystem permissions succeeds.

## Packaging And Releases

The app is packaged as a Windows x64 desktop application.

Installer files:

- `installer/MultiWebView.iss`: Inno Setup definition.
- `scripts/build-installer.ps1`: local publish and installer build wrapper.
- `.github/workflows/ci.yml`: normal build validation for pushes and pull requests.
- `.github/workflows/release.yml`: tag-driven GitHub Release workflow.

The installer workflow publishes the app as self-contained for `win-x64`, so users do not need to install the .NET runtime separately. The app still requires Microsoft Edge WebView2 Runtime because browser hosting depends on WebView2.

The Inno Setup installer is intentionally per-user:

- Install path: `%LOCALAPPDATA%\Programs\Multi WebView`
- Start Menu shortcut: current user
- Desktop shortcut: optional, current user
- Admin rights: not required

The uninstaller removes installed binaries and shortcuts only. It does not remove app data under `%LOCALAPPDATA%\MultiWebView`, because that directory contains profile metadata, WebView2 browser data, cookies, sessions, and screenshots.

Local installer build:

```powershell
.\scripts\build-installer.ps1 -Version 0.4.0 -SelfContained
```

The script writes publish and installer outputs under `artifacts\`, which is ignored by Git.

GitHub release flow:

1. Commit and push the source changes.
2. Push a version tag such as `v0.4.0`.
3. `.github/workflows/release.yml` runs on `windows-latest`.
4. The workflow restores, builds, installs Inno Setup with Chocolatey, runs `scripts/build-installer.ps1`, creates a portable zip, and publishes both files to a GitHub Release.

Expected release assets for version `0.4.0`:

```text
MultiWebViewSetup-0.4.0-win-x64.exe
MultiWebView-0.4.0-win-x64-portable.zip
```

## Public Repository Notes

The repository is intended to be safe to publish without local profile data. The app stores real user data outside the repo under `%LOCALAPPDATA%\MultiWebView`.

Public-readiness checks performed during installer/release setup:

- Searched the working tree, excluding build outputs and `.git`, for common secret names such as password, token, secret, API key, private key, bearer, authorization, cookie, and session.
- Checked ignored local files with `git status --short --ignored`.
- Checked whether ignored personal/demo files were tracked with `git ls-files -- MultiWebView/MultiWebView.csproj.user mock profile-picker-render.png`.
- Checked Git history file names for obvious sensitive paths such as `.env`, secret, token, key, cookie, session, settings, `profiles.json`, and `webview2`.

Findings from that pass:

- No obvious tokens, private keys, or credentials were found in tracked source.
- `MultiWebView/MultiWebView.csproj.user`, `mock/`, and `profile-picker-render.png` are ignored local files and were not reported as tracked by the explicit `git ls-files` check.
- The regex scan reports expected false positives for normal documentation and source terms such as browser sessions, cancellation tokens, and Core Audio sessions.

Security and privacy expectations:

- WebView2 profile isolation is profile-level browser data separation, not encryption.
- Real profile folders, screenshots, cookies, and logs should not be committed or attached to public issues.
- Unsigned installer releases may trigger Windows SmartScreen warnings until the project has reputation or uses code signing.
- The release workflow has `contents: write` permission so it can publish GitHub Releases. Keep workflow changes reviewed before merging outside contributions.

## Development Guidance

Prefer keeping audio behavior centralized in `WebViewVolumeController`. `MultiViewForm` should pass profile-specific state into the controller, but it should not duplicate Core Audio enumeration logic.

When changing profile persistence, update both the top-level `profiles.json` flow and the per-profile `profile.json` snapshot flow in `ProfileStore.SaveProfiles()`. Runtime profile setting changes should update the in-memory `Profile` object and the saved copy, following the pattern used by `UpdateProfileAudio(...)` and `UpdateProfileStats(...)`.

When changing WebView creation, preserve the order:

1. Create WebView control.
2. Attach audio enforcement.
3. Ensure CoreWebView2.
4. Ensure early audio session.
5. Apply saved audio state.
6. Navigate.

This order minimizes the window where Windows can create a WebView2 audio session with the wrong default volume.

When changing tile refresh behavior, keep it aligned with `InitializeWebViewAsync(...)` instead of adding a separate initialization path. Refresh should continue to recreate only the selected tile's `WebView2` while preserving the tile's current audio and stats state.
