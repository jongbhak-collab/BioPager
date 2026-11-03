# BioPager 0.4.13 — spatial desktop previews

Based on PR #3 / codex/app-previews-0.4.12. No main-branch publication or release workflow is added or run.

Right-click BioPager and select **Preserve desktop positions and window overlap**. The existing **Arrange app previews in a grid (show every app)** option restores the grid. Fresh installations and pre-preview upgrades default to grid; existing 0.4.12 choices persist. Both options save to the existing settings file.

Spatial mode scales the virtual-screen union uniformly, retains negative monitor coordinates and window size/position, clips monitor gaps and off-screen areas, and follows the native top-level Z-order. Front windows cover rear windows. Only exposed areas receive live DWM source crops, cached pixels, icons, labels or mouse hits. A protected or unavailable foreground preview still occludes rear previews. Minimized apps have a separate bottom strip instead of occupying restored desktop geometry. Unknown-desktop apps stay in the Other apps grid.

Double-click a visible app to focus it. Drag an exposed part to another desktop tile to move it; Escape cancels. A fully covered window is deliberately inaccessible in spatial view: select grid to reach every app. Status messages and drag ghosts disable live overlays until dismissed.

## Limits

- Geometry and Z-order update on the existing 750 ms scan; live contents update through DWM independently.
- Rectangular app bounds approximate the desktop. Rounded corners, shadows, transparency, shaped windows, shell surfaces, and excluded tool/dialog windows are not reproduced as a full desktop compositor.
- Other desktops and minimized apps may offer stale or unavailable content. Cached samples are memory-only client-area images, so their frame/letterboxing can differ from a full-window live image. Protected content remains an icon/name; reported protection purges cached pixels.
- Up to 32 live fragments per source per tile are used. More complex overlaps use clipped cached/icon fallback. Windows resource limits may also cause fallback.
- Multi-monitor coordinate and clipping logic is regression-tested. Physical mixed-DPI monitor arrangements, MobaXterm, Gemini and Chrome need user confirmation on the target desktop.

## Validation

Built on Windows x64 using the Windows .NET Framework compiler with warnings as errors. BuildTests.cmd --unit and BioPagerTests.exe --window-tests pass. Tests include randomized exact pixel coverage, actual pager settings round trips, foreground/exposed-rear hit testing, minimized strip access, drag initiation/cancel, native Z-order changes, actual DWM source repaint/crop pixels around a GDI foreground, cached overlap clipping, protection purge, identity rejection, and handle recreation. See the packaged test report and synthetic PNG evidence for the final counts.

Existing desktop-switch and cross-desktop move implementations are unchanged. The live desktop-mutation integration suite was not run on the user's desktop; this run uses synthetic windows and an isolated settings file, without changing the user's virtual desktops.
