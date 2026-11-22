param(
    [string]$Godot = 'godot',
    [string]$Dotnet = 'dotnet',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logDirectory = Join-Path $projectPath '.tmp/csharp-tests'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
Push-Location $projectPath
try {
    if (-not $SkipBuild) {
        & $Dotnet build
        if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    }

    function Invoke-Suite([string]$Scene, [string]$Marker, [string]$LogName) {
        # Check both the exit code and success marker: an engine script error can otherwise exit zero.
        $ErrorActionPreference = 'Continue'
        $logPath = Join-Path $logDirectory $LogName
        & $Godot --headless --path $projectPath --log-file $logPath $Scene --quit-after 1800 2>&1 | Out-Host
        $engineExit = $LASTEXITCODE
        if ($engineExit -ne 0 -or -not (Select-String -LiteralPath $logPath -SimpleMatch $Marker -Quiet)) {
            throw "Suite failed: $Scene. See $logPath"
        }
    }

    Invoke-Suite 'tests/GeometryCheck.tscn' 'GEOMETRY ALL OK' 'geometry.log'
    Invoke-Suite 'tests/LandmarkGroundCheck.tscn' 'LANDMARK GROUND ALL OK' 'landmark-ground.log'
    Invoke-Suite 'tests/HarvestCheck.tscn' 'HARVEST ALL OK' 'harvest.log'
    Invoke-Suite 'tests/HarvestAnimCheck.tscn' 'HARVEST ANIM ALL OK' 'harvest-anim.log'
    Invoke-Suite 'tests/SimCheck.tscn' 'ALL OK' 'simulation.log'
    Invoke-Suite 'tests/NightSpawnCheck.tscn' 'NIGHT SPAWN ALL OK' 'night-spawn.log'
    Invoke-Suite 'tests/TacticalCraftCheck.tscn' 'TACTICAL CRAFT ALL OK' 'tactical-craft.log'

    # Multiplayer is removed for now; the network suite lives in git commit cf44b5f (tests/NetworkCheck.cs).
    Write-Output 'All C# checks passed.'
}
finally { Pop-Location }
