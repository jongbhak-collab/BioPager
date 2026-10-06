# BioPager

Created by Jong Bhak. BioLicense: free for all, including companies and AIs.

## Taskbar Top / Bottom (0.4.6)

Right-click BioPager or its tray icon, select **Windows taskbar location (primary monitor)**, then **Top** or **Bottom**.

This feature requires a separately installed, active, compatible [ExplorerPatcher](https://github.com/valinet/ExplorerPatcher/releases) taskbar. On modern Windows 11, enable **Windows 10 (ExplorerPatcher)** in ExplorerPatcher Properties first. The stock Windows 11 taskbar on 22H2 and later does not support top positioning through this integration. BioPager does not install ExplorerPatcher, change its taskbar style, or restart Explorer.

The operation affects the primary taskbar only. It follows ExplorerPatcher's current live taskbar-position protocol and verifies the resulting position. Incompatible or unresponsive taskbars produce an explanatory message. ExplorerPatcher remains responsible for persistence and compatibility with Windows updates.

**BioPager position within taskbar...** moves the pager within the taskbar; it is distinct from moving the Windows taskbar itself.

## Build

The editable source is in `src/`, extracted from the repository's latest 0.4.5 source archive. On Windows x64, run `src/Build.cmd`. The build extracts original assets and native helpers from the tracked 0.4.5 archive automatically. GitHub Actions builds the executable and runs existing regression checks; download its `BioPager-0.4.6-windows-x64` artifact. The root `BioPager.exe` is the previous release until a validated Windows build replaces it.

## Validation required on Windows

Verify Top and Bottom with ExplorerPatcher active, persistence after Explorer restart, and BioPager floating/docked/integrated modes. Also check stock Windows 11, absent ExplorerPatcher, secondary monitors, and Explorer restarting: these must fail gracefully without changing the stock taskbar or other monitors. Runtime taskbar positioning cannot be tested on Linux.

## ExplorerPatcher reference

Integration was checked against ExplorerPatcher `ep_gui/GUI.c`, `GUI_Internal_RegSetValueExW` and `GUI_Internal_RegQueryValueExW`. Its primary position setting uses `Shell_TrayWnd`, message `WM_USER + 0x1CA`, operation 5 (query) and 6 (set), with edge 1 (top) or 3 (bottom). BioPager uses a bounded message timeout and checks the shell-reported edge. This private protocol may change in later ExplorerPatcher releases.

ExplorerPatcher is independently distributed under its own GPL license. No ExplorerPatcher source or binaries are incorporated into BioPager.
