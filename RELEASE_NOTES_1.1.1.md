# BioPager 1.1.1

Fixes fresh-account desktop creation and initializes eligible first-launch profiles to seven Windows virtual desktops total.

- Explorer can omit its persisted desktop-ID list on a new account. Native adapters now accept consistent OS enumeration in that case, while rejecting conflicting persisted IDs or counts.
- Creation verifies the original ordered desktop identities plus exactly one new identity. Adapter return conventions are not treated as proof of success.
- New profiles, pending attempts, existing users and completed setup have distinct persistent states. Pending setup survives settings saves. Existing settings and established multi-desktop workspaces are preserved; no existing desktop is removed. A pending attempt can be retried from the context menu.
- Capability selection occurs before creation. A helper crash, timeout or uncertain operation is never automatically replayed through keyboard input. Keyboard fallback acknowledges creation and each switch, restores the original desktop, checks held keys and cleans up partial injected input without replaying the shortcut.
- The embedded modern adapter matches the latest official 2024-12-16 Windows 11 release byte-for-byte (SHA-256 8740c572a1c000e3b87ffeb1e4c397eae9af3bd4a2abdc3bcffacab4493f8ff5). Older isolated adapters remain available. There is no Windows-build whitelist.
- Installer identity and per-user installation path remain unchanged: `%LOCALAPPDATA%\Programs\BioPager`. Running a newer Setup upgrades in place, preserving preferences. Setup and the offline guide explain first launch and retry behavior.
- The 1.0.1 input-helper correction, spatial live previews and app-logo settings remain included.

## Validation and limits

See PR #9 checks and downloadable test evidence. Local native tests run on Windows 11 build 26300.9711. The clean first-launch and existing-profile OS tests run on an isolated Windows Server 2025 GitHub runner (build 26100), not a separately created Windows 11 account. That distinction matters: the exact user account and error environment have not been reproduced directly. Private Windows desktop interfaces can change; failures stop with actionable status rather than a success claim or repeated input.

The installer is offline and needs no application download. BioPager uses the .NET Framework runtime included in supported Windows installations. Packages are unsigned. No main branch update or new release-write CI workflow is included.
