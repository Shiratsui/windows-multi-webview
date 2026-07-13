# Changelog

Change notes for Multi WebView releases. Tagged sections describe released versions; the next release section is prepared before tagging.

## Unreleased

- Added a per-tile pop-out button that moves a profile from a multi-view window into its own browser window.
- Reflowed source multi-view windows and updated profile picker ownership tracking after pop-out.
- Added drag-to-combine for browser windows, with target highlighting and profile ownership transfer into the target window.
- Added a slide-style drop preview while dragging a browser window over a target window so the combined tile position can be chosen before release.
- Extended drag-to-combine to merge multi-profile source windows as a contiguous block, such as dragging a two-profile window into another two-profile window.
- Updated the profile picker usage popup header to show state on the right with a divider above the metrics.
- Reduced profile picker blinking by reusing profile cards and updating state changes in place.

## v0.6.0

- Added a persisted per-profile `GPU` / `DEF` WebView mode toggle on profile cards.
- Added high-GPU and default WebView2 environment modes per profile.
- Kept locked profile-card action buttons visually readable while making edit, delete, and `GPU` / `DEF` actions inert for open profiles.
- Added a separate `FEATURES.md` major-features guide with screenshots.
- Updated release documentation for the `v0.6.0` release.

## v0.5.0

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
