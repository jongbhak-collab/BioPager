# BioPager 0.4.12

Created by Jong Bhak. BioLicense: free for all, including companies and AIs.

## Actual app and browser previews (0.4.12)

BioPager now shows the contents of application windows. Chrome and other browser
windows show their active page; switching a tab updates the live preview. Separate
browser windows have separate previews. Background tabs are not separate desktop
windows.

The upgrade enables **Show actual app and browser previews** and **Arrange app
previews in a grid (show every app)**. Existing position, taskbar integration,
background color, and desktop-title preferences are retained. The grid makes
windows individually reachable. Turn off the grid for the previous spatial
arrangement, which uses cached pictures and icons to preserve overlap order.

Visible application windows with hidden owners or explicit application-window
styles are included. Untitled windows get a process-name label. Missing icons are
retried and have window-class/executable fallbacks. If Windows cannot identify an
application's virtual desktop, it remains visible in **Other apps**; click to
focus it. Dragging is enabled once its desktop is known.

Live previews use Windows DWM. A small in-memory cache samples the unobscured
foreground app at most once a second for use when it is minimized or on another
desktop. It does not activate, restore, or move windows to capture them. Sampling
is skipped if another window obscures the app, geometry changes, or Windows
reports capture protection. No sampled image is written to disk or uploaded.
The cache is limited to 64 images of at most 320 by 180 pixels. Unavailable
previews retain a recognizable icon or colored name label, never an unlabeled
grey placeholder. Protected and secure-desktop contents cannot be promised as
previews; a minimized/background window may show its last view rather than new
content.

**Copy window diagnostics** copies the latest inclusion/exclusion report to the
local clipboard only when requested. The report contains window titles, so review
it before sharing it.

The Windows workflow compiles with warnings as errors, runs existing regression
checks plus app-selection tests, and checks real DWM thumbnail pixels using
synthetic windows. The live-pixel check explicitly reports a skip when no usable
interactive compositor is available. Actual MobaXterm, Gemini, and Chrome behavior
still requires verification on the target Windows desktop.

Exit the running BioPager before replacing `BioPager.exe`. The executable remains
portable and includes the native desktop helpers.

## Native taskbar Top / Bottom (0.4.9)

Right-click BioPager or its tray icon, select **Windows taskbar location**, then **Top** or **Bottom**. No ExplorerPatcher or Windhawk installation is required. Before any change, BioPager warns that some Windows 11 releases may not support it and recommends installing and using ExplorerPatcher independently if needed. Choose OK to try or Cancel to leave settings unchanged.

This implementation uses Windows' native taskbar location setting and shell notification. **Your Windows installation must have native taskbar positioning available and enabled by Microsoft's rollout.** Check Settings > Personalization > Taskbar > Taskbar behaviors for Taskbar position. Older Windows 11 installations without the feature cannot acquire it through a simple setting change. BioPager does not add Explorer hooks or replace the taskbar.

BioPager verifies the shell-reported location. On failure it restores the prior registry value and requests the prior location; it reports if restoration cannot be confirmed. Concurrent setting changes are preserved. Disable automatic taskbar hiding before using Top. Windows may apply this setting to all displayed taskbars; it is not a per-monitor feature.

**BioPager position within taskbar...** moves the pager within the taskbar, separately from moving the Windows taskbar.

## Build

On Windows x64, run `Build.cmd` in this source folder. All required assets and native desktop helpers are included. The single compiled BioPager.exe needs no adjacent files. Run BuildTests.cmd for the regression suite.

## Validation

Interactive Windows tests remain required: Top/Bottom with native positioning enabled, unsupported-feature rollback, Start/Search/flyouts, restart persistence, multi-monitor placement, and floating/docked/integrated BioPager modes. Compilation and existing desktop unit checks do not establish taskbar behavior.

## Implementation references

Microsoft announced native taskbar positioning in https://blogs.windows.com/windows-insider/2026/05/15/improving-windows-quality-making-taskbar-and-start-more-personal/ .

The native setting and message protocol were checked against https://github.com/ramensoftware/windhawk-mods/blob/main/mods/taskbar-on-top.wh.cpp : `Explorer\Advanced\TaskbarLocation` stores the screen edge, and message `0x5CA`, operation 6, asks the shell to apply it. This is a private interface and may change. No Windhawk or ExplorerPatcher source or binaries are incorporated into this feature.

## Instant desktop switching (0.4.9)

BioPager temporarily disables Windows client-area animation effects around desktop tile switches, including keyboard fallback, and restores the prior session setting afterward. The saved Windows preference is not changed. Other applications may briefly observe effects being disabled during a switch. If BioPager is forcibly terminated mid-switch, effects may stay disabled for that session; re-enable Animation effects in Windows Accessibility > Visual effects. Visual transition behavior still requires verification on Windows.

## Runtime desktop compatibility (0.4.11)

Automatic isolated helper probing replaces the Windows version whitelist. Available
actions are checked separately, and keyboard fallback remains for switching and
creation. The existing 0.4.9 UI, taskbar options and animation handling are preserved.
Direct app dragging and switching passed live tests on build 26300.9550. See
COMPATIBILITY.md and VALIDATION.txt for behavior, test evidence and limitations.

Exit your running BioPager before replacing its EXE with this version.

