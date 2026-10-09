# ElecTool

AutoCAD 2026 plugin (C# / .NET 8) that assists with building electrical drafting.
Stage 1 = core only: loading, logging, language, `ELEC` command, enable/disable.

- Target framework: `net8.0-windows` (verified against the installed AutoCAD runtime: .NET 8.0.x)
- Build:   `dotnet build ElecTool.sln -c Release`
- Install: `powershell -ExecutionPolicy Bypass -File tools\Install-Bundle.ps1`
- Remove:  `powershell -ExecutionPolicy Bypass -File tools\Install-Bundle.ps1 -Remove`
- Tests:   `dotnet test tests\ElecTool.Core.Tests`

The installer touches only `%APPDATA%\Autodesk\ApplicationPlugins\ElecTool.bundle`.
It never modifies AutoCAD program files.
