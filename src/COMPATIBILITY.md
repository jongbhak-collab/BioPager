# BioPager 0.4.11 runtime compatibility

This release is based on the 0.4.9 source from GitHub draft PR #1, commit
c7a74c1c6d35e5c195df618a2aaa3a4259acaeff. It preserves the native taskbar
Top/Bottom option, taskbar support warning, and temporary animation suppression
with preference restoration introduced in 0.4.9.

## No Windows build whitelist

Desktop helper selection does not read CurrentBuildNumber or UBR. The worker
client tries the five embedded interface adapters, newest first, and keeps the
first one that initializes and verifies desktop count, ordered GUIDs and current
index against Explorer state. The adapters are: modern VDA, January 2024 VDA,
November 2023 VDA, February 2023 VDA, and integrated legacy managed COM.

Each candidate runs in its own process. A failing, hanging or crashing candidate
is stopped before another is tried. No desktop mutation is used as a probe.
Every operation also checks its required exports/capability before it is sent.
An adapter that enumerates desktops but lacks an operation does not disable
other available operations. All-failed probes have a five-second cooldown.
The chosen helper is reprobed after worker failure. Native creation/removal
rechecks the layout inside the desktop-change mutex before mutating it.

A timeout/crash during a requested mutation is returned without replaying that
command. Windows may already have applied it. Existing PID/window/GUID checks,
post-operation confirmation and next-desktop removal migration remain in place.
Native payloads still undergo SHA-256 verification, exclusive per-worker leasing,
versioned extraction to LocalAppData, and cleanup. Verification/disk failures
are not hidden by trying a different adapter.

Keyboard fallback remains for switching and new desktops, with GUID acknowledgments
and modifier protection. Arbitrary external-app dragging and desktop removal
require a usable private desktop interface; without one, use Windows Task View.
A stable, public API for every required shell operation does not exist. Removing
the build whitelist improves resilience; it cannot guarantee that all future
Windows updates retain a compatible interface. Windows 11 x64 is the compiled
architecture; ARM64/emulation and older build families need separate live tests.

## Validation on Windows 11 x64 26H2 26300.9550

The complete suite passed: runtime adapter negotiation, per-operation capabilities,
isolated crash/reconnect, no ambiguous mutation replay, verification failure handling,
keyboard fallback, direct worker app movement and independent destination verification,
direct switching, creation, next-desktop migration, last-desktop protection,
taskbar overlay, topmost rivalry, fullscreen ordering, and desktop restoration.

Test app windows are normal taskbar-visible windows, allowing Explorer to register
application views. The original tool-window test fixture was unsuitable for
validating arbitrary app movement. No user app was moved by the test suite.

Sources:
- https://github.com/jongbhak-collab/BioPager/pull/1
- https://github.com/Ciantic/VirtualDesktopAccessor/releases
- https://github.com/MScholtes/VirtualDesktop/blob/6429179996/VirtualDesktop11.cs
- https://blogs.windows.com/windows-insider/2026/08/27/releasing-windows-11-version-26h2-to-the-release-preview-channel/

The package includes all native helpers and assets to rebuild offline with Build.cmd.
