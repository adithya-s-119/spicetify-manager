# Code signing policy

Spicetify Manager is a free, open-source Windows GUI for [spicetify](https://spicetify.app).
This page is the project's published code signing policy, as required by the
[SignPath Foundation](https://signpath.org) open-source signing programme.

## Maintainer and roles

- **Author / committer / reviewer / approver:** Adithya S ([@adithya-s-119](https://github.com/adithya-s-119))

The project has a single maintainer. Every commit to `main` is authored by that account.

## Build process

- Source: <https://github.com/adithya-s-119/spicetify-manager>
- Build system: GitHub Actions, workflow [`build`](.github/workflows/build.yml), running on GitHub-hosted `windows-latest` runners.
- Build command: `build.ps1`, which compiles `SpicetifyGui.cs` and `SpicetifyApply.cs` with the C# compiler that ships with the .NET Framework (`csc.exe`) and embeds `SpicetifyGui.ico`.
- No third-party packages, no binary dependencies and no post-build steps: the executables are produced only from the sources in this repository.

## What gets signed

| File | Description |
| --- | --- |
| `SpicetifyGui.exe` | The GUI application |
| `SpicetifyApply.exe` | The console launcher |

The binaries attached to each [release](https://github.com/adithya-s-119/spicetify-manager/releases) are built by the workflow above and, once signing is active, are signed through SignPath.io with a certificate issued to the SignPath Foundation.

## Verification

```powershell
signtool verify /pa /v SpicetifyGui.exe
```

Or right-click the executable → **Properties** → **Digital Signatures**.

## Signing approvals

Signing requests are approved manually by the maintainer in SignPath.io before a release is published.

## Privacy

The application collects no telemetry and sends nothing anywhere on its own. It only contacts the network when the user explicitly clicks a button that requires it:

- **Install Spicetify** runs the official installer from `raw.githubusercontent.com/spicetify/cli`.
- **Update Spicetify** lets the spicetify CLI update itself, as it normally would.

Everything else is local: the application runs `spicetify`, reads its output and displays it.
