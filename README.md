# BioPager 1.1.1

Created by Jong Bhak. BioLicense: free for all, including companies and AIs.

Fixes fresh-account desktop creation and initializes eligible new profiles to **seven Windows virtual desktops total**. Existing BioPager settings, completed setup and established multi-desktop workspaces keep their layout. Failed first setup remains pending and can safely resume after checking Task View. No existing desktop is removed by onboarding.

## Download and install

[PR #9](https://github.com/jongbhak-collab/BioPager/pull/9) contains the review branch and links to validated downloads. In its successful **Build BioPager** run, download **BioPager-1.1.1-setup**, extract the ZIP and run **BioPager-Setup-1.1.1.exe**. **BioPager-1.1.1-portable**, matching source and test evidence are also available. GitHub Actions artifact downloads may require sign-in and expire according to repository retention.

Setup installs offline into `%LOCALAPPDATA%\Programs\BioPager`, includes usage instructions and an offline Quick Start guide, and upgrades the managed installation in place while preserving preferences. There is no need to uninstall first. The historical root `BioPager.exe` is not this build.

Right-click the pager for **New desktop** or **Retry initial seven-desktop setup**. Check Win+Tab after an unconfirmed operation before retrying. BioPager verifies OS desktop identities and count before reporting success, and does not repeat uncertain keyboard or native mutations automatically.

The 1.0.1 input-helper correction, spatial live previews, grid mode, app-logo sizes, dragging, focus handling and saved settings remain included. See [1.1.1 release notes](RELEASE_NOTES_1.1.1.md) for compatibility, profile behavior and test limits, and [installer details](installer/README.md) for setup and upgrade behavior.

## What is filtered

The window catalog recognizes the input helper by window class and known Windows SystemApps executable location. A restricted Korean/English caption fallback is used only when the image path is unavailable and the window also has the reported unresolved-desktop, shell-cloaked, non-app popup signature. This changes BioPager's list only: Windows input services continue running untouched.

Settings and normal applications are not excluded simply because they use CoreWindow or belong to another desktop. The **Other apps** group remains for genuinely unresolved application windows, and disappears when none remain. Diagnostics now explain the exclusion and include executable metadata when available.

See [1.0.1 release notes](RELEASE_NOTES_1.0.1.md) for exact behavior and validation scope, [1.0 release notes](RELEASE_NOTES_1.0.md) for badge sizes, and [the existing feature/build documentation](src/README.md) for earlier features and platform limitations.

## Build and tests

Run `src/Build.cmd` on Windows x64. Run `src/BuildTests.cmd --unit` for targeted input-filter checks plus regression tests, and `src/BioPagerTests.exe --window-tests` for synthetic native-window and thumbnail/overlap/badge pixel tests. The source archive includes the native payloads and assets needed for an offline build. The CI workflow has read-only repository permission and does not auto-merge or publish a release.

The user's original diagnostic log and screenshots are not included in this repository or the uploaded test fixtures.
