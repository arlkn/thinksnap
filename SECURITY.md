# Security

## Windows SmartScreen

Thinksnap is distributed as an Inno Setup installer. Windows SmartScreen can still warn about new installers until the file, publisher, or signing certificate has enough reputation.

For public releases, sign `artifacts\installer\ThinksnapSetup.exe` with an Authenticode code-signing certificate issued by a trusted certificate authority and include a trusted timestamp. The repository includes `scripts\Sign-ThinksnapInstaller.ps1` for that workflow.

Until a public code-signing certificate is available, verify the release checksum before installing:

```powershell
Get-FileHash .\ThinksnapSetup.exe -Algorithm SHA256
```

Compare the result with `ThinksnapSetup.exe.sha256.txt` from the same GitHub release.

## Release Checklist

- Build the app with `dotnet publish`.
- Compile `installer\Thinksnap.iss` with Inno Setup.
- Sign `artifacts\installer\ThinksnapSetup.exe`.
- Generate `ThinksnapSetup.exe.sha256.txt`.
- Upload only the installer, checksum, and icon assets to GitHub Releases.
- If SmartScreen still blocks a signed release, submit the file to Microsoft for review.
