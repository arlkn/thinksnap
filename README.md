# Thinksnap

Thinksnap is a lightweight Windows screenshot MVP inspired by Flameshot. It focuses on region capture, pixelate censorship, annotations, undo, clipboard copy, and PNG save.

## Requirements

- Windows
- .NET 8 SDK

## Run

```powershell
dotnet run --project src/Thinksnap.App/Thinksnap.App.csproj
```

## Test

```powershell
dotnet test Thinksnap.sln
```

## MVP Scope

- Capture a selected screen region.
- Annotate screenshots with basic drawing tools.
- Pixelate sensitive areas before sharing.
- Undo recent edits during an annotation session.
- Copy the edited screenshot to the clipboard.
- Save the edited screenshot as a PNG file.

Pixelate censorship is real bitmap censorship in the output, not just a visual overlay.
