# BioPager 1.1.0: Windows installer

Adds a single-file offline Setup.exe installer, a short in-setup usage guide, and a permanent offline Quick Start guide. Installs to the standard per-user Programs folder, adds Start menu shortcuts, and offers optional desktop and sign-in-startup shortcuts.

New installer versions replace the managed installed version in place, close the existing pager gracefully, retain settings and previous task choices, and keep one Windows uninstall entry. Downgrades are blocked. Uninstall preserves preferences and unrelated files. Loose portable copies and manually created startup shortcuts elsewhere are not deleted.

The app remains portable when copied separately. The Windows input-helper fix from 1.0.1, spatial overlap, live previews, logo badges and badge-size settings are retained without rendering changes. The executable version is 1.1.0.0; the existing in-app Help heading still identifies the 1.0 product series.

Normal setup is interactive and includes usage information. Unattended setup is available using `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`; add `/LAUNCH` to start the pager afterward. This does not add automatic downloading of future releases.

The Windows CI workflow builds and tests the application and installer and publishes downloadable build artifacts. It has read-only repository permissions and does not update main or create releases. This distribution is unsigned.
