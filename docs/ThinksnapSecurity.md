# Thinksnap Security

Thinksnap releases are published through GitHub Releases as a Windows setup file. The current public installer is safe to verify by checksum, and future public releases should be Authenticode signed with a trusted OV or EV code-signing certificate to reduce Windows SmartScreen warnings.

## Installer Verification

Download both files from the same release:

- `ThinksnapSetup.exe`
- `ThinksnapSetup.exe.sha256.txt`

Then verify the installer hash:

```powershell
Get-FileHash .\ThinksnapSetup.exe -Algorithm SHA256
```

The hash should match the value in `ThinksnapSetup.exe.sha256.txt`.

## SmartScreen

Windows SmartScreen may show "Windows protected your PC" for new or unsigned installers. This warning is usually related to publisher reputation, not necessarily malware detection.

The public release target is:

- Authenticode-sign the installer with a trusted OV or EV code-signing certificate.
- Timestamp the signature so it remains valid after certificate expiration.
- Publish the signed installer and checksum together.
- Keep the release assets minimal: setup, checksum, and icon assets only.

## Signing Path

Azure Artifact Signing was evaluated for Thinksnap, but its public trust flow is region and identity limited. For this project, the preferred path is a public OV or EV code-signing certificate from a trusted certificate authority such as DigiCert, Sectigo, or GlobalSign.

The certificate publisher name must match the verified legal person, company, or registered trade name. A GitHub username such as `arlkn` can appear as the publisher only if it is also a verifiable legal or trade name accepted by the certificate authority.

## Current Release Safety

The repository includes scripts for the release workflow:

- `scripts\Sign-ThinksnapInstaller.ps1`
- `scripts\New-ReleaseChecksums.ps1`

Checksum verification proves the downloaded installer matches the file published in the release. Code signing is still required for a stronger publisher identity and better SmartScreen reputation.
