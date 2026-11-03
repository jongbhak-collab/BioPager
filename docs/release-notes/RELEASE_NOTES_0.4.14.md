# BioPager 0.4.14: app logo badges

Right-click BioPager or its tray icon and select **Show app logo badges on previews**. The option starts enabled and saves across restarts. Each available app icon appears at the top-left of its live or cached preview on a small white plate with a dark border. White and visually similar windows remain identifiable without replacing their contents.

The square badge measures about 15% of the preview width, with a small readability minimum. It never exceeds half the shorter preview edge. Previews too small for a four-pixel badge retain their content. The badge uses the existing window icon, with the existing executable-icon fallback. Missing icons retain the established icon/name fallback rather than showing a misleading logo.

Grid previews retain their fitted client image and aspect ratio. Spatial previews retain their geometry, source crops and overlap order: a rear app's badge cannot paint through a foreground window. The minimized strip and Other apps use the same option. Turning off actual previews retains the existing icon view.

Live DWM surfaces exclude the small badge plate so they cannot cover the logo. All remaining content keeps updating. Cached previews draw the badge over their existing image. Windows capture protection and the memory-only cache remain in effect. More than 32 live fragments per source falls back to cached content or the existing icon/name.

## Validation

Build with `src/Build.cmd`, then run `src/BuildTests.cmd --unit` and `src/BioPagerTests.exe --window-tests`. The synthetic window suite checks actual live pixels, client fitting, badge visibility, repaint, cached content, overlap clipping, settings migration and persistence, and native resource cleanup. It uses isolated settings without changing the user's virtual desktops. See the packaged test report for measured results. Physical mixed-DPI configurations and the specific ChatGPT/Claude windows still require target-machine use.
