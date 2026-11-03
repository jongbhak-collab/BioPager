# BioPager 1.0.1: Windows input-helper filtering fix

This patch builds on 1.0 and keeps live previews, actual desktop overlap, app-logo badges and the saved 15%, 20%, 30% or 50% badge-size preference.

## Fixed

Windows Input Experience could appear as a blue extra window in the **Other apps** group. The window catalog now recognizes the Windows input host by its CoreWindow class and known executable location under the actual Windows SystemApps directory. It excludes that helper only from BioPager's catalog, without stopping the process or changing Windows input settings.

When the executable path is unavailable, a narrowly constrained fallback recognizes the reported Korean or English input-helper caption together with CoreWindow, unresolved desktop membership, shell cloaking and non-app popup styling. A known unrelated process/path defeats the fallback. The diagnostic report distinguishes this fallback from executable recognition.

This is not a blanket exclusion of CoreWindow, shell-cloaked windows, or unknown desktop IDs. Settings and normal apps remain eligible. **Other apps** remains available for genuine app windows whose desktop is unknown, and automatically disappears when no such windows remain.

## Diagnostics and privacy

Copy window diagnostics now includes the running assembly version, process names, image paths when available, and a count of filtered input helpers. Reports remain local and are copied only when requested. Review them before sharing: paths and titles can contain personal information. The uploaded tests use sanitized fixtures, not the user's raw diagnostic log.

## Install

Exit BioPager; extract the new portable ZIP; replace the old BioPager.exe and start the new one. Existing settings are retained. Windows file Properties > Details reports **1.0.1.0**. The inherited Help heading remains the 1.0 product-series heading. Use the new versioned build artifact, not the historical root BioPager.exe.

## Validation scope

The build runs targeted input-helper policy tests, a sanitized replay of the reported inclusion pattern, the existing regression suite, and synthetic Windows thumbnail/overlap/badge pixel tests. Results must be read from the completed matching workflow. A passing synthetic test does not establish behavior on the user's actual Windows input host; a fresh local diagnostic report can confirm the excluded-helper reason after installation.
