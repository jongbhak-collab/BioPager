# BioPager 1.0.1

Created by Jong Bhak. BioLicense: free for all, including companies and AIs.

This patch fixes the Windows Input Experience helper appearing as a blue window in **Other apps**, while preserving the 1.0 features: live app/browser previews, actual desktop positions and overlap, grid mode, top-left app logos with selectable 15%, 20%, 30% or 50% sizes, app dragging, foreground focus and taskbar integration.

## Download and install

Use the **BioPager-1.0.1.0-windows-x64** artifact from the completed **Build BioPager** workflow for branch `fix/input-helper-1.0.1`. It contains the portable `BioPager.exe` and build identity/hash information. The workflow also uploads matching source and test evidence. The historical root `BioPager.exe` has not been replaced and is not this patched build.

Exit the running BioPager, extract the new package, replace the executable and start it again. Existing layout, badge and taskbar preferences are retained. File Properties > Details shows version **1.0.1.0**; the inherited Help heading is the 1.0 product-series heading.

## What is filtered

The window catalog recognizes the input helper by window class and known Windows SystemApps executable location. A restricted Korean/English caption fallback is used only when the image path is unavailable and the window also has the reported unresolved-desktop, shell-cloaked, non-app popup signature. This changes BioPager's list only: Windows input services continue running untouched.

Settings and normal applications are not excluded simply because they use CoreWindow or belong to another desktop. The **Other apps** group remains for genuinely unresolved application windows, and disappears when none remain. Diagnostics now explain the exclusion and include executable metadata when available.

See [1.0.1 release notes](RELEASE_NOTES_1.0.1.md) for exact behavior and validation scope, [1.0 release notes](RELEASE_NOTES_1.0.md) for badge sizes, and [the existing feature/build documentation](src/README.md) for earlier features and platform limitations.

## Build and tests

Run `src/Build.cmd` on Windows x64. Run `src/BuildTests.cmd --unit` for targeted input-filter checks plus regression tests, and `src/BioPagerTests.exe --window-tests` for synthetic native-window and thumbnail/overlap/badge pixel tests. The source archive includes the native payloads and assets needed for an offline build. The CI workflow has read-only repository permission and does not auto-merge or publish a release.

The user's original diagnostic log and screenshots are not included in this repository or the uploaded test fixtures.
