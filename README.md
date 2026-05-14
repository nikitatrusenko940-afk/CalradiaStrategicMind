# Calradia Strategic Mind

CalradiaStrategicMind is a Mount & Blade II: Bannerlord singleplayer mod for future improvements to strategic campaign AI.

Current state: this repository contains only a minimal, safe project skeleton. It does not change lord behavior, army behavior, diplomacy, sieges, garrisons, or any other strategic AI systems yet.

## Requirements

- Mount & Blade II: Bannerlord War Sails v1.4.4
- .NET SDK that can build `net472` projects
- Local Bannerlord installation

Do not commit the game folder or Bannerlord DLL files to this repository. The project references TaleWorlds assemblies from your local game installation during build.

## Build

Run PowerShell from the repository root:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\build.ps1 -BannerlordDir "D:\Steam\steamapps\common\Mount & Blade II Bannerlord"
```

The script checks the Bannerlord directory, verifies the required TaleWorlds DLL files, builds the C# project, and creates a ready module folder at:

```text
Build\CalradiaStrategicMind
```

## Run In Game

After a successful build, copy `Build\CalradiaStrategicMind` to your Bannerlord `Modules` directory as:

```text
Mount & Blade II Bannerlord\Modules\CalradiaStrategicMind
```

Then start the Bannerlord launcher, enable `Calradia Strategic Mind`, and launch singleplayer.

## Notes

- No Harmony dependency is included.
- No MCM dependency is included.
- No game DLLs are copied into Git.
- Logging and safe execution helpers are present so future systems can fail safely instead of crashing the campaign.
