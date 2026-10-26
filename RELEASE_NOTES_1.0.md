# BioPager 1.0

BioPager 1.0 includes the selectable app logo sizes introduced in 0.4.15, together with live window previews, cached previews, spatial overlap and the existing desktop controls.

Right-click BioPager or its tray icon and open **App logo badge size**. Choose **15%**, **20%**, **30%**, or **50%** of the preview width. The new default is **30%**. Size and badge visibility save across restarts. Select **Show app logo badges on previews** to turn badges on or off.

Badges stay at the top-left and fit inside short previews with a small margin. Live content around the badge continues updating. Cached content, grid fitting, spatial foreground clipping, minimized previews and Other apps use the same size setting. Very small previews omit unreadable badges. Existing capture protection and the memory-only image cache remain in effect.

## Validation

The Windows x64 build uses the .NET Framework compiler with warnings as errors. Tests cover all four size choices, exclusive menu selection, persistence, old settings migration, invalid-value recovery, actual live pixels while resizing between 30% and 50%, cached badge resizing, spatial clipping, decorated portrait fitting and native resource cleanup. See the packaged test report for measured results. Tests use synthetic windows and isolated settings without changing the user's virtual desktops. Named ChatGPT/Claude windows and physical mixed-DPI hardware were not directly tested.
