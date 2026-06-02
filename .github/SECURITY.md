# Thinksnap Security Policy

## Reporting Security Issues

If you believe you have found a security issue in Thinksnap, please report it privately before opening a public issue.

Use GitHub Security Advisories when available, or contact the repository owner through GitHub:

https://github.com/arlkn/thinksnap/security/advisories

## Windows Installer Safety

Thinksnap is distributed as an Inno Setup installer. Windows SmartScreen can warn about new installers until the file, publisher, or signing certificate has enough reputation.

Public releases should be signed with an Authenticode OV or EV code-signing certificate issued by a trusted certificate authority and include a trusted timestamp.

Until a public code-signing certificate is available, verify the installer checksum before installing:

```powershell
Get-FileHash .\ThinksnapSetup.exe -Algorithm SHA256
```

Compare the result with `ThinksnapSetup.exe.sha256.txt` from the same GitHub release.

## Release Checklist

- Build the app with `dotnet publish`.
- Compile `installer\Thinksnap.iss` with Inno Setup.
- Sign `artifacts\installer\ThinksnapSetup.exe` with a trusted OV or EV code-signing certificate.
- Generate `ThinksnapSetup.exe.sha256.txt`.
- Upload only the installer, checksum, and icon assets to GitHub Releases.
- If SmartScreen still blocks a signed release, submit the file to Microsoft for review.
