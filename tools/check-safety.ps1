param(
    [switch]$AllowExperimentalAi,
    [switch]$AllowDirectDefenseCommand,
    [switch]$AllowArmyDirector
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot "Source\CalradiaStrategicMind"

$forbiddenPatterns = @(
    "AddBehaviorScore",
    "SetBehaviorScore",
    "AIBehaviorData",
    "PartyThinkParams",
    "SetPartyAiAction",
    "SetMoveDefendSettlement",
    "SetMoveGoToSettlement",
    "SetMoveBesiegeSettlement",
    "SetTargetSettlement",
    "SetPartyObjective",
    "CreateArmy",
    "GatherArmyAction",
    "DisbandArmyAction",
    "DisbandArmy",
    "SiegeEventManager.StartSiegeEvent",
    "LiftSiegeAction",
    "Harmony",
    "Reflection"
)

$allowedFindings = @(
    @{
        Path = ".\Source\CalradiaStrategicMind\Logging\CsmLogger.cs"
        Pattern = "Reflection"
        Line = "using System.Reflection;"
    },
    @{
        Path = ".\Source\CalradiaStrategicMind\Strategic\DefenseScoreSimulator.cs"
        Pattern = "PartyThinkParams"
        Line = 'return CreateReport(summary, actionPlan, false, false, "Hypothetical score calculated only; not inserted into PartyThinkParams");'
    }
)

$experimentalAiAllowedPath = ".\Source\CalradiaStrategicMind\Behaviors\ExperimentalDefenseScoreInfluenceBehavior.cs"
$experimentalAiAllowedPatterns = @(
    "AddBehaviorScore",
    "SetBehaviorScore",
    "AIBehaviorData",
    "PartyThinkParams"
)

$directDefenseCommandAllowedPath = ".\Source\CalradiaStrategicMind\Strategic\DirectDefenseCommandController.cs"
$directDefenseCommandAllowedPatterns = @(
    "SetMoveDefendSettlement"
)

$armyDirectorAllowedPaths = @(
    ".\Source\CalradiaStrategicMind\Strategic\CsmArmyDirector.cs",
    ".\Source\CalradiaStrategicMind\Strategic\CsmArmyFormationDirector.cs",
    ".\Source\CalradiaStrategicMind\Strategic\CsmArmyOperationalDirector.cs"
)
$armyDirectorAllowedPatterns = @(
    "SetMoveDefendSettlement",
    "SetMoveBesiegeSettlement",
    "SetMoveGoToSettlement",
    "CreateArmy"
)

function Test-IsAllowedFinding {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Line
    )

    foreach ($allowedFinding in $allowedFindings) {
        if ($Path -eq $allowedFinding.Path -and
            $Pattern -eq $allowedFinding.Pattern -and
            $Line -eq $allowedFinding.Line) {
            return $true
        }
    }

    if ($AllowExperimentalAi -and
        $Path -eq $experimentalAiAllowedPath -and
        $experimentalAiAllowedPatterns -contains $Pattern) {
        return $true
    }

    if ($AllowDirectDefenseCommand -and
        $Path -eq $directDefenseCommandAllowedPath -and
        $directDefenseCommandAllowedPatterns -contains $Pattern) {
        return $true
    }

    if ($AllowArmyDirector -and
        $armyDirectorAllowedPaths -contains $Path -and
        $armyDirectorAllowedPatterns -contains $Pattern) {
        return $true
    }

    return $false
}

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    Write-Error "Source directory not found: $sourceRoot"
    exit 1
}

$files = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter "*.cs" -File |
    Where-Object {
        $_.FullName -notmatch "\\bin\\" -and
        $_.FullName -notmatch "\\obj\\"
    }
$findings = @()

foreach ($file in $files) {
    foreach ($pattern in $forbiddenPatterns) {
        $escapedPattern = [regex]::Escape($pattern)
        $matches = Select-String -LiteralPath $file.FullName -Pattern "(?<![A-Za-z0-9_])$escapedPattern(?![A-Za-z0-9_])"
        foreach ($match in $matches) {
            $relativePath = Resolve-Path -LiteralPath $match.Path -Relative
            $line = $match.Line.Trim()
            if (Test-IsAllowedFinding -Path $relativePath -Pattern $pattern -Line $line) {
                continue
            }

            $findings += [PSCustomObject]@{
                Path = $relativePath
                LineNumber = $match.LineNumber
                Pattern = $pattern
                Line = $line
            }
        }
    }
}

if ($findings.Count -eq 0) {
    Write-Output "Safety check passed."
    exit 0
}

Write-Output "Safety check failed. Forbidden API strings found:"
foreach ($finding in $findings) {
    Write-Output ("{0}:{1}: [{2}] {3}" -f $finding.Path, $finding.LineNumber, $finding.Pattern, $finding.Line)
}

exit 1
