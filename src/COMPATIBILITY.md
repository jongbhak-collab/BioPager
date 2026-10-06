# BioPager 0.4.5 Windows compatibility

This release broadens compatibility; it is not certification for all Windows
builds. Only Windows 11 x64 26200.9550 was available for live testing.

| Windows build | Backend chosen | Live validation on this release |
| --- | --- | --- |
| 22000 (21H2) | Integrated managed COM wrapper from VirtualDesktop v1.9 | Selection only; no 21H2 VM available |
| 22621 before revision 2215 (early 22H2) | VDA 2023-02-22-windows11 | Selection/payload checks only |
| 22621/22631 revision 2215 through 3084 | VDA 2023-11-10-windows11 | Selection/payload checks only |
| 22621/22631 revision 3085 and later | VDA 2024-01-25-windows11 | Selection/payload checks only |
| 26100 revision 2605 and later (24H2), 26200 (25H2) | Existing VDA 2024-12-16-windows11 | Full tests on 26200.9550 |
| Early 24H2, unmatched Insider/future builds | Windows shortcuts | Real shortcuts tested on 26200.9550; other builds unverified |

BioPager keeps its UI, settings, icon, default topmost behavior, taskbar overlay
and six-desktop first-use setup. It needs no files adjacent to the EXE. x64 is
the compiled architecture; ARM64/emulation was not available for testing.

For an unmatched build, switching uses Win+Ctrl+Left/Right and creation uses
Win+Ctrl+D. Each step verifies the desktop GUID and layout from Explorer before
sending the next shortcut. Creation restores the original desktop. Held modifier
keys, changed layouts, blocked input and unconfirmed results stop the operation.
Closing the pager cancels pending shortcut operations. Shortcut fallback may
animate and briefly visit intervening desktops. Dragging other programs and
removal with a guaranteed next-desktop destination require a matching native API;
on unmatched builds, use Windows Task View (Win+Tab) for those actions.

Creation/setup probes the helper before mutating desktops. It never retries a
native mutation via keyboard after an ambiguous error or timeout. Desktop removal
keeps the existing next-desktop migration and final-desktop protection. Native
interfaces are used only by an isolated worker; Explorer restart/native failure
restarts the worker on a subsequent request. Initialization checks native desktop
count, order, GUIDs and current index against Explorer's registry state.

All native resources are SHA-256 checked and extracted to a unique per-worker
LocalAppData directory with an exclusive lease, version and content hash. The
21H2 managed wrapper is compiled into the EXE and does not need extraction.

Sources:
- https://github.com/Ciantic/VirtualDesktopAccessor/releases
- https://github.com/MScholtes/VirtualDesktop/blob/6429179996/VirtualDesktop11.cs
- https://github.com/Ciantic/VirtualDesktopAccessor/releases/download/2023-02-22-windows11/VirtualDesktopAccessor.dll
- https://github.com/Ciantic/VirtualDesktopAccessor/releases/download/2023-11-10-windows11/VirtualDesktopAccessor.dll
- https://github.com/Ciantic/VirtualDesktopAccessor/releases/download/2024-01-25-windows11/VirtualDesktopAccessor.dll

All four DLL hashes are fixed in NativePayload.cs; the package's SHA256SUMS.json
also records their hashes. The source package includes all dependencies needed
to rebuild using Build.cmd, without a network download at build or runtime.
