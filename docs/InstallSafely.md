# Installing Thinksnap Safely

Thinksnap is currently distributed through GitHub Releases. Until a trusted code-signing path is available, Windows SmartScreen can still warn on first install. Use the checks below to make sure the installer came from the official release.

## Official Download

Download Thinksnap only from:

https://github.com/arlkn/thinksnap/releases/latest

Avoid installers shared through mirrors, chat attachments, or re-uploaded download links.

## Verify The Installer

Each public release should include:

- `ThinksnapSetup.exe`
- `ThinksnapSetup.exe.sha256.txt`

After downloading both files into the same folder, run:

```powershell
Get-FileHash .\ThinksnapSetup.exe -Algorithm SHA256
Get-Content .\ThinksnapSetup.exe.sha256.txt
```

The SHA256 value printed by `Get-FileHash` must match the value in `ThinksnapSetup.exe.sha256.txt`.

## SmartScreen Notice

SmartScreen reputation is separate from the MIT license and checksum verification. An unsigned or newly distributed installer can show a warning even when the file is legitimate.

For now, the recommended trust path is:

1. Download only from the official GitHub release.
2. Verify the SHA256 checksum.
3. Check that the installer filename and version match the release.
4. Install only if all values match.

## Future Distribution Goals

The planned safer distribution path is:

- Keep GitHub Releases as the source of truth.
- Publish SHA256 checksums for every release.
- Add a Windows Package Manager manifest.
- Revisit Microsoft Store or code signing if a supported publisher identity becomes available.
