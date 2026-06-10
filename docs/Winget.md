# Windows Package Manager Preparation

This repository includes a draft Windows Package Manager manifest under `packaging/winget/`.

The manifest is not submitted automatically. Before submitting a new version to `microsoft/winget-pkgs`, update the manifest with the exact release values.

## Release Checklist

1. Build `ThinksnapSetup.exe`.
2. Publish it to a GitHub release.
3. Generate `ThinksnapSetup.exe.sha256.txt`.
4. Update the Winget manifest version, installer URL, and SHA256.
5. Validate the manifest with `winget validate`.
6. Submit it to the official `microsoft/winget-pkgs` repository.

## Expected Install Command

After acceptance into Windows Package Manager, users should be able to install Thinksnap with:

```powershell
winget install arlkn.Thinksnap
```

## Current Draft Values

The draft manifest currently targets the first public release:

- Package identifier: `arlkn.Thinksnap`
- Version: `0.1.0`
- Installer type: Inno Setup
- Installer scope: User
- License: MIT

Treat these files as release packaging metadata, not as the authoritative source for the current shipped installer. The GitHub release remains the source of truth.
