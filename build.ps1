param(
    [Parameter(Mandatory = $true)]
    [string]$BannerlordDir
)

$ErrorActionPreference = "Stop"

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }
}

$RepoRoot = $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot "Source\CalradiaStrategicMind\CalradiaStrategicMind.csproj"
$SourceModuleDir = Join-Path $RepoRoot "Module"
$ModuleBinDir = Join-Path $SourceModuleDir "bin\Win64_Shipping_Client"
$GameBinDir = Join-Path $BannerlordDir "bin\Win64_Shipping_Client"
$OutputDir = Join-Path $RepoRoot "Build\CalradiaStrategicMind"
$OutputBinDir = Join-Path $OutputDir "bin\Win64_Shipping_Client"
$OutputModuleDataDir = Join-Path $OutputDir "ModuleData"

if (-not (Test-Path -LiteralPath $BannerlordDir -PathType Container)) {
    throw "Bannerlord directory was not found: $BannerlordDir"
}

if (-not (Test-Path -LiteralPath $GameBinDir -PathType Container)) {
    throw "Bannerlord Win64_Shipping_Client directory was not found: $GameBinDir"
}

Assert-FileExists -Path (Join-Path $GameBinDir "TaleWorlds.Core.dll") -Description "TaleWorlds.Core.dll"
Assert-FileExists -Path (Join-Path $GameBinDir "TaleWorlds.Library.dll") -Description "TaleWorlds.Library.dll"
Assert-FileExists -Path (Join-Path $GameBinDir "TaleWorlds.MountAndBlade.dll") -Description "TaleWorlds.MountAndBlade.dll"

New-Item -ItemType Directory -Force -Path $ModuleBinDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputBinDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputModuleDataDir | Out-Null

dotnet build $ProjectPath `
    -c Release `
    -p:BannerlordDir="$BannerlordDir"

$ProjectOutputDir = Join-Path $RepoRoot "Source\CalradiaStrategicMind\bin\Release"
$ModDll = Join-Path $ProjectOutputDir "CalradiaStrategicMind.dll"

Assert-FileExists -Path $ModDll -Description "Built mod DLL"

Copy-Item -LiteralPath $ModDll -Destination $ModuleBinDir -Force
Copy-Item -LiteralPath $ModDll -Destination $OutputBinDir -Force
Copy-Item -LiteralPath (Join-Path $SourceModuleDir "SubModule.xml") -Destination (Join-Path $OutputDir "SubModule.xml") -Force

Write-Host ""
Write-Host "Build completed successfully."
Write-Host "Ready module folder:"
Write-Host "  $OutputDir"
Write-Host ""
Write-Host "To test in Bannerlord, copy this folder to:"
Write-Host "  $BannerlordDir\Modules\CalradiaStrategicMind"
