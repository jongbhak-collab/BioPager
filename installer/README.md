# BioPager Windows installer

Build `src/Build.cmd`, then run `powershell -File installer/BuildInstaller.ps1` on Windows x64 with Inno Setup 6.3 or later installed. The output is `dist/BioPager-Setup-1.1.1.exe`, a single offline setup executable.

## Behavior

- Installs per user to `%LOCALAPPDATA%\Programs\BioPager`, without requesting administrator privileges.
- Shows a usage page during setup and installs an offline Quick Start guide with a Start menu shortcut.
- Offers optional desktop and sign-in-startup shortcuts. Both are off on first installation. Inno Setup retains chosen tasks during upgrades.
- Uses stable `AppId=JongBhak.BioPager`. New setup files update the same installation and uninstall registration rather than installing parallel versions.
- Gracefully closes matching current-user, current-session BioPager processes. Metadata and Windows owner checks prevent an unrelated same-name process from being closed. These compatibility checks are not code-signature verification. No process is force-terminated; if closing fails, installation stops before replacement.
- Refuses downgrades and refuses changing an already-registered install folder during upgrade.
- Keeps `%LOCALAPPDATA%\BioPager` preferences on upgrade and uninstall. Only package-managed files are removed by uninstall. No recursive delete of the application folder is used.
- Existing loose portable files, downloaded ZIPs and manually created startup shortcuts remain untouched. Remove obsolete manually created startup entries separately.
- Does not add an online/background updater. Run each new installer to upgrade.

Normal setup ends with a checked option to start BioPager. For unattended setup:

```cmd
BioPager-Setup-1.1.1.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

Add `/LAUNCH` to start afterward. `/TASKS="startup,desktopicon"` opts into both extra tasks; `/TASKS=""` selects neither. A silent install without `/TASKS` keeps prior choices.

## Validation

`TestInstaller.ps1` requires a clean isolated Windows profile and refuses to run over an existing installation or settings directory. It performs actual setup, repair, upgrade, downgrade-rejection and uninstall operations, using a synthetic older application for controlled shutdown tests. It also launches and upgrades the real production BioPager. The fixture executable is never distributed.

Use only an isolated CI runner for these tests. Test cleanup deletes only directories created by the tests and terminates only test-launched processes.

No code-signing certificate is configured. Windows may display an unknown-publisher or SmartScreen warning. Do not disable security features; verify the source, filename and SHA-256 before deciding whether to run an unsigned build.
