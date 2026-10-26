# BioPager 0.4.15: selectable app logo size

Right-click BioPager or its tray icon, open **App logo badge size**, and choose **15%**, **20%**, **30%**, or **50%**. The selected percentage controls badge width relative to the preview. The choice saves across restarts and applies immediately to live and cached previews. The new default is **30%**. Existing badge visibility and preview preferences remain intact.

Large badges stay at the top-left. They fit inside short previews with a small content margin, while the remaining live image continues updating. Grid fitting, spatial foreground clipping, minimized previews and Other apps retain their existing behavior. The previous half-height limit is removed so larger choices can produce larger logos.

This feature update is also provided as **BioPager 1.0**. The two builds contain the same badge-size functionality.

## Validation

The Windows x64 build uses the .NET Framework compiler with warnings as errors. Tests check every menu choice, exclusive checkmarks, settings persistence, migration from older settings, invalid-size recovery, live resize and repaint, cached content, spatial clipping, portrait fitting and native resource cleanup. Synthetic windows and isolated settings avoid changes to the user's virtual desktops. See the packaged test report for measured results. Named ChatGPT/Claude windows and physical mixed-DPI hardware were not directly tested.
