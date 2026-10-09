<#
.SYNOPSIS
  Installs (or removes) the ElecTool bundle for the CURRENT USER only.

.DESCRIPTION
  Touches ONLY this folder:  %APPDATA%\Autodesk\ApplicationPlugins\ElecTool.bundle
  It never modifies AutoCAD program files. Close AutoCAD before running it.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\Install-Bundle.ps1
  powershell -ExecutionPolicy Bypass -File tools\Install-Bundle.ps1 -Remove
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$repo   = Split-Path -Parent $PSScriptRoot
$target = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\ElecTool.bundle'

if ((Split-Path -Leaf $target) -ne 'ElecTool.bundle') {
    throw "Safety check failed for target path: $target"
}

if (Get-Process -Name acad -ErrorAction SilentlyContinue) {
    throw 'AutoCAD is running. Close it first, then run this script again.'
}

if ($Remove) {
    if (Test-Path $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "Removed: $target"
    } else {
        Write-Host "Nothing to remove: $target"
    }
    return
}

$build = Join-Path $repo "src\ElecTool.AutoCAD\bin\$Configuration\net8.0-windows"
if (-not (Test-Path (Join-Path $build 'ElecTool.AutoCAD.dll'))) {
    throw "Build output not found in: $build`nRun the build first (dotnet build ElecTool.sln -c $Configuration)."
}

if (Test-Path $target) {
    Remove-Item -LiteralPath $target -Recurse -Force
}

$contents = Join-Path $target 'Contents'
New-Item -ItemType Directory -Path $contents -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $repo 'bundle\ElecTool.bundle\PackageContents.xml') -Destination $target

$files = 'ElecTool.AutoCAD.dll', 'ElecTool.AutoCAD.deps.json', 'ElecTool.AutoCAD.pdb',
         'ElecTool.Core.dll', 'ElecTool.Core.pdb'
foreach ($f in $files) {
    $src = Join-Path $build $f
    if (Test-Path $src) {
        Copy-Item -LiteralPath $src -Destination $contents
    }
}

Write-Host "Installed to: $target"
Get-ChildItem -Path $target -Recurse -File | ForEach-Object { Write-Host ("  " + $_.FullName.Substring($target.Length)) }
Write-Host 'Start AutoCAD now.'
