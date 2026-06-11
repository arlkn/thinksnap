# Thinksnap📷

Thinksnap is a lightweight Windows screenshot MVP app. It focuses on region capture, pixelate censorship, annotations, undo, clipboard copy, and PNG save.

<p align="center">
  <a href="https://github.com/arlkn/thinksnap/releases/latest/download/ThinksnapSetup.exe">
    <img src="src/Thinksnap.App/Assets/extension_icon.png" alt="Download Thinksnap" width="128" />
  </a>
</p>

<p align="center">
  <strong>Click the icon to download the Thinksnap installer</strong>
</p>

## Install with PowerShell Command

```powershell
$setup = Join-Path $env:TEMP "ThinksnapSetup.exe"
Invoke-WebRequest "https://github.com/arlkn/thinksnap/releases/latest/download/ThinksnapSetup.exe" -OutFile $setup
Start-Process $setup -ArgumentList "/SILENT /NORESTART" -Wait
```

This downloads the latest official GitHub release and installs Thinksnap for the current Windows user.

Installer verification and SmartScreen notes are documented in [Installing Thinksnap Safely](docs/InstallSafely.md) and [Thinksnap Security](docs/ThinksnapSecurity.md). Verify each release with `ThinksnapSetup.exe.sha256.txt` from the same GitHub release.

## Requirements

- Windows 10 or Windows 11 (x64)
- No separate .NET installation is required

## Run Thinksnap

```powershell
Start-Process "$env:LOCALAPPDATA\Programs\Thinksnap\Thinksnap.exe"
```

Thinksnap runs from the system tray. Right-click the tray icon and choose `Take Screenshot`, or press `PrintScreen`.

## Run from Source

```powershell
dotnet run --project src/Thinksnap.App/Thinksnap.App.csproj
```

Running from source requires the .NET 8 SDK.

## Test

```powershell
dotnet test Thinksnap.sln
```

## Build Installer

```powershell
dotnet publish src/Thinksnap.App/Thinksnap.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/publish/win-x64
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\Thinksnap.iss
.\scripts\New-ReleaseChecksums.ps1
```

The installer is written to `artifacts/installer/ThinksnapSetup.exe`.

Use `scripts\Sign-ThinksnapInstaller.ps1` with a trusted code-signing certificate before publishing public releases.

## Safe Distribution

Thinksnap is currently distributed through GitHub Releases with SHA256 checksum files. A draft Windows Package Manager manifest is available in [packaging/winget](packaging/winget), with submission notes in [docs/Winget.md](docs/Winget.md).

## MVP Scope

- Capture a selected screen region.
- Annotate screenshots with basic drawing tools.
- Pixelate sensitive areas before sharing.
- Undo recent edits during an annotation session.
- Copy the edited screenshot to the clipboard.
- Save the edited screenshot as a PNG file.

Pixelate censorship is real bitmap censorship in the output, not just a visual overlay.

## License

Thinksnap is licensed under the [MIT License](LICENSE).
