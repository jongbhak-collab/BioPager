# BioPager

Created by Jong Bhak. BioLicense: free for all, including companies and AIs.

## Native taskbar Top / Bottom (0.4.7)

Right-click BioPager or its tray icon, select **Windows taskbar location**, then **Top** or **Bottom**. No ExplorerPatcher or Windhawk installation is required.

This implementation uses Windows' native taskbar location setting and shell notification. **Your Windows installation must have native taskbar positioning available and enabled by Microsoft's rollout.** Check Settings > Personalization > Taskbar > Taskbar behaviors for Taskbar position. Older Windows 11 installations without the feature cannot acquire it through a simple setting change. BioPager does not add Explorer hooks or replace the taskbar.

BioPager verifies the shell-reported location. On failure it restores the prior registry value and requests the prior location; it reports if restoration cannot be confirmed. Concurrent setting changes are preserved. Disable automatic taskbar hiding before using Top. Windows may apply this setting to all displayed taskbars; it is not a per-monitor feature.

**BioPager position within taskbar...** moves the pager within the taskbar, separately from moving the Windows taskbar.

## Build

On Windows x64, run `src/Build.cmd`. The build extracts original assets and native desktop helpers from the tracked 0.4.5 archive automatically. GitHub Actions compiles the executable and runs the existing unit regression checks. The root BioPager.exe remains the previous release; download the new Actions artifact.

## Validation

Interactive Windows tests remain required: Top/Bottom with native positioning enabled, unsupported-feature rollback, Start/Search/flyouts, restart persistence, multi-monitor placement, and floating/docked/integrated BioPager modes. Compilation and existing desktop unit checks do not establish taskbar behavior.

## Implementation references

Microsoft announced native taskbar positioning in https://blogs.windows.com/windows-insider/2026/05/15/improving-windows-quality-making-taskbar-and-start-more-personal/ .

The native setting and message protocol were checked against https://github.com/ramensoftware/windhawk-mods/blob/main/mods/taskbar-on-top.wh.cpp : `Explorer\Advanced\TaskbarLocation` stores the screen edge, and message `0x5CA`, operation 6, asks the shell to apply it. This is a private interface and may change. No Windhawk or ExplorerPatcher source or binaries are incorporated into this feature.
