# Credits and third-party notices

## BioPager

Created by **Jong Bhak**. Original project material is distributed under
**BioLicense 1.0**; see [LICENSE](LICENSE).

## Inspiration: Vern by One Guy Coding

BioPager was inspired by the miniature desktop-pager interface of
**Vern**, created by **One Guy Coding** (also styled OneGuyCoding).

This is an independent implementation. No Vern source code, executable,
icons, or other assets are included. This credit acknowledges inspiration;
it does not imply affiliation, endorsement, or ownership of Vern.

## VirtualDesktopAccessor

Author: **Jari Otto Oskari Pennanen**.

The bundled `native/VirtualDesktopAccessor.dll` is from release
`2024-12-16-windows11` of:
https://github.com/Ciantic/VirtualDesktopAccessor

It retains its **MIT license**, reproduced without modification in
[native/LICENSE.txt](native/LICENSE.txt). Preserve that notice when distributing
the dependency. BioLicense does not replace or remove its license terms.

SHA-256 of the bundled DLL:
`8740c572a1c000e3b87ffeb1e4c397eae9af3bd4a2abdc3bcffacab4493f8ff5`

## Runtime app icons

The pager displays icons obtained from applications already running on the
user's computer. Those application icons are not distributed in this package.

## Older compatibility helpers

The embedded VirtualDesktopAccessor releases 2023-02-22-windows11,
2023-11-10-windows11 and 2024-01-25-windows11 are also by Jari Pennanen
and retain the MIT license in native/LICENSE.txt. Their download URLs and
hashes are recorded in COMPATIBILITY.md and SHA256SUMS.json.

## Integrated Windows 11 21H2 backend

Legacy21.cs is adapted from Markus Scholtes' VirtualDesktop11.cs, commit
6429179996 in https://github.com/MScholtes/VirtualDesktop (version 1.9).
The command-line tool and assembly attributes were removed, and the namespace
was changed to BioPager.Legacy21. The COM wrappers remain under MIT;
see native/Legacy21-LICENSE.txt, also embedded in the executable.
