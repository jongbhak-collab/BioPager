# BioPager 0.4.12

## Changes

- Display actual app contents and the active page of each browser window.
- Enable a grid of previews on upgrade so windows do not hide behind one another.
- Recognize owned application windows and untitled apps, including cases previously omitted by the strict owner filter.
- Retain apps with unresolved desktop membership in an Other apps group.
- Retry missing icons and use sampled app views, executable icons, or colored name labels instead of empty grey boxes.
- Keep a bounded memory-only cache of unobscured foreground windows for inactive or minimized previews.
- Add local window diagnostics for identifying remaining compatibility cases.
- Preserve desktop switching, app dragging, taskbar positioning, and the 0.4.11 foreground-focus fix.

## Validation

The Windows build workflow compiles the Windows x64 executable with warnings as errors and runs regression and native window/preview checks. Synthetic DWM tests compare actual pixels before and after the source window changes. If no interactive compositor is available, the pixel test explicitly reports a skip.

MobaXterm, Gemini, and Chrome still need confirmation on the user's Windows installation. Protected content cannot be captured; minimized and inactive apps may display a retained view. Background browser tabs are not separate windows.

## Install

Exit BioPager, replace the old BioPager.exe with this version, and start it again. The single executable contains its native helpers. The two preview options are enabled automatically when upgrading older settings.
