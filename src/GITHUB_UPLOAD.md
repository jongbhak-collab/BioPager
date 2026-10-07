# Publish under your GitHub account

This package is prepared for publication; it has not been uploaded to GitHub.

Suggested repository name: `BioPager`

Suggested description:
> BioPager: fast Windows virtual desktop pager by Jong Bhak, inspired by Vern by One Guy
> Coding. Drag apps between desktops, customize colors, and optionally attach
> the pager to the taskbar. BioLicense: free for all, including companies and AIs.

## Repository contents

Upload the contents of the `BioPager` folder at the repository root.
Include the C# source files, Build.cmd, Start.cmd, README.md, LICENSE,
THIRD_PARTY_NOTICES.md, and the entire native folder with its MIT license.
The bundled .gitignore excludes generated EXEs, build logs, and release hashes
from a normal git commit. Keep the DLL in native; it is an intentional dependency.

Create the repository while signed into the GitHub account you want to own it.
Your display name, Jong Bhak, need not be the same as your GitHub username.
Do not choose another license template: this repository already contains the
BioLicense text you requested. BioLicense here is a custom project license;
this package makes no claim of OSI approval or an official SPDX identifier.

After the source is uploaded, create a release tagged `v0.4.5` and attach the
complete `BioPager_v0.4.5.zip` so Windows users can download the compiled EXE
or distribute BioPager.exe alone; the native dependency and license notices are embedded.

## Build from source on Windows

Run Build.cmd with the Windows .NET Framework compiler available. No NuGet
packages or Visual Studio project are required. Run Start.cmd after building.

## Suggested release notes

- Credits in Help: Jong Bhak; inspiration from Vern by One Guy Coding.
- BioLicense 1.0 for original code and documentation, free for all including
  commercial and AI use. The native dependency keeps its MIT license.
- Background color picker with saved preference and automatic light/dark text.
- Reset background color to the original dark red.
- Existing direct switching, silent app moves, and experimental taskbar embedding.

Compilation and package checks were performed. Color dialogs, saved color
restoration, and all Windows UI behavior still need testing on a Windows PC.
