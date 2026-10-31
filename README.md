# Spicetify Manager

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A small Windows GUI for [spicetify](https://spicetify.app) — install spicetify, back up and apply your setup, rebuild the backup, and update the CLI without remembering commands.

![Spicetify Manager](docs/screenshot.png)

## Features

- **Install Spicetify** — if spicetify isn't found, the GUI offers to run the official installer (`install.ps1`) for you, then picks it up automatically.
- **Backup & Apply** — runs `spicetify backup apply` on a fresh install, or `spicetify apply` when a backup already exists (the backup step is skipped automatically).
- **Rebuild Backup** — forces a fresh backup with `spicetify restore backup apply`; asks for confirmation first.
- **Update Spicetify** — runs `spicetify update`.
- Live, colour-coded command output with ANSI codes stripped; **Stop**, **Copy** and **Clear**.
- Detects the installed version, the spicetify location, and whether a backup exists.
- Single self-contained executable: no setup program, no config files, no runtime files next to it.

## Requirements

- Windows 10 or 11 (the .NET Framework 4.x runtime is included with Windows).
- [spicetify](https://spicetify.app) installed — or let the GUI install it (needs an internet connection). It is found via `PATH`, `%LOCALAPPDATA%\spicetify` or `%APPDATA%\spicetify`.
- Spotify installed, as required by spicetify.

## Download and run

1. Download `SpicetifyGui.exe` from the [latest release](https://github.com/adithya-s-119/spicetify-manager/releases/latest) (it is also committed in this repository).
2. Double-click it.

The executable is standalone — you can move it anywhere and delete every other file. You can also pass spicetify arguments to run them on launch:

```powershell
.\SpicetifyGui.exe update
.\SpicetifyGui.exe --version
```

## Installing spicetify

If spicetify isn't installed, the version label in the top-right corner is replaced by an **Install Spicetify** button. It runs the official installer:

```powershell
iwr -useb https://raw.githubusercontent.com/spicetify/cli/main/install.ps1 | iex
```

That installer is interactive (it asks about the Marketplace and can repair an existing install), so it opens in its own console window — answer its prompts there. When it closes, the GUI re-checks for spicetify and enables the action buttons automatically. It installs to `%LOCALAPPDATA%\spicetify` and adds that folder to your user `PATH`.

## Why "Rebuild Backup" instead of "Backup"?

`spicetify backup` creates exactly one backup and refuses to overwrite it. Once a backup exists, theme changes only need `spicetify apply`. To start over you must restore Spotify's original files first:

```
spicetify restore backup apply
```

That restores the stock files, captures a fresh backup, and re-applies your setup. The GUI asks for confirmation before running it.

## Console launcher

`SpicetifyApply.exe` is a minimal console alternative. It runs `spicetify backup apply` by default and passes arguments through:

```powershell
.\SpicetifyApply.exe              # spicetify backup apply
.\SpicetifyApply.exe apply        # spicetify apply
.\SpicetifyApply.exe --version --no-pause
```

`--no-pause` skips the final "press any key" prompt, which is useful in scripts.

## Build from source

Both programs are plain C# compiled with the C# compiler that ships with the .NET Framework. From the repository root:

```powershell
.\build.ps1
```

Or manually:

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:SpicetifyGui.ico `
  /out:SpicetifyGui.exe `
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
  SpicetifyGui.cs

& $csc /nologo /target:exe /platform:anycpu /optimize+ /win32icon:SpicetifyGui.ico `
  /out:SpicetifyApply.exe SpicetifyApply.cs
```

## Repository layout

| File | Purpose |
| --- | --- |
| `SpicetifyGui.exe` | The GUI application (prebuilt). |
| `SpicetifyGui.cs` | WinForms source for the GUI. |
| `SpicetifyGui.ico` | Application icon. |
| `SpicetifyApply.exe` | Console launcher (prebuilt). |
| `SpicetifyApply.cs` | Console launcher source. |
| `build.ps1` | Rebuilds both executables. |
| `docs/screenshot.png` | Screenshot used in this README. |
| `LICENSE` | MIT license. |

## License

[MIT](LICENSE) © 2026 Adithya S

## Disclaimer

Unofficial tool. Not affiliated with spicetify or Spotify.
