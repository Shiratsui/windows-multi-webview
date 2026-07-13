# Changelog

Change notes for tagged Multi WebView releases. Entries are based on the Git tag history.

## Unreleased

- Added optional `GPU` and `GPU VRAM` entries to each WebView tile's `STAT` menu.
- Persisted GPU stats overlay selections per profile.
- Added a dark live usage popup when hovering open profile cards in the profile picker.

## v0.4.0

- Changed the per-tile refresh button to recreate the selected WebView instead of reloading the current page.
- Preserved current per-tile volume, mute, and stats overlay state across WebView recreation.
- Shared WebView initialization between initial tile creation and refresh-triggered recreation.
- Updated release documentation for the `v0.4.0` release.

## v0.3.3

- Added a custom delete confirmation dialog for profile deletion.
- Disabled profile edit and delete actions while that profile is open in a WebView window.
- Added defensive guards so open profiles cannot be edited or deleted through the profile management code path.
- Added version-by-version changelog documentation.

## v0.3.2

- Fixed `Default` tray mode so moving a multi-view window to the tray does not cut off the page process or network activity.

## v0.3.1

- Fixed switching a multi-view window from `Keep running` tray mode back to `Default` tray mode.

## v0.3.0

- Added `Keep running` tray mode for multi-view browser windows.
- Added tray-menu support for toggling `Keep Running` without restoring the window.
- Added profile picker state feedback for keep-running tray windows.
- Refined layout and status badge behavior for tray-mode profile cards.

## v0.2.1

- Updated runtime icons, window titles, and tray context labels so the profile picker and browser windows are easier to distinguish.

## v0.2.0

- Added more profile picker controls and profile-card states.
- Added `OPEN` state handling in the profile picker.
- Clicking an already-open profile now restores or focuses the existing browser window instead of opening a duplicate profile.
- Fixed profile picker minimize-to-tray behavior when the picker was maximized.
- Updated technical documentation for profile picker tray behavior.

## v0.1.1

- Added MIT license information.
- Added security and public-release documentation updates.

## v0.1.0

- Initial tagged release.
- Added isolated WebView2 profiles with persistent per-profile browser data.
- Added tiled multi-profile browser windows.
- Added profile storage, default URL handling, and single-instance startup.
- Added profile picker and multi-view tray behavior.
- Added per-profile audio persistence and WebView2 audio session handling.
- Added browser refresh controls.
- Added per-profile screenshots and profile-folder access.
- Added optional stats overlay for FPS, render latency, CPU, and memory.
- Added GitHub Actions build and release workflow support.
- Added README and technical documentation.
