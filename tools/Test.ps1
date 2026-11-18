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
    Invoke-Suite 'tests/SimCheck.tscn' 'ALL OK' 'simulation.log'

    $godotExecutable = (Get-Command $Godot -ErrorAction Stop).Source
    $serverArguments = @('--headless', '--path', ('"' + $projectPath + '"'),
        '--log-file', ('"' + (Join-Path $logDirectory 'network-server.log') + '"'),
        'tests/NetworkCheck.tscn', '--quit-after', '1800', '--', '--network-server')
    $serverOptions = @{ FilePath = $godotExecutable; ArgumentList = $serverArguments; PassThru = $true }
    if ($env:OS -eq 'Windows_NT') { $serverOptions.WindowStyle = 'Hidden' }
    $testServer = Start-Process @serverOptions
    try {
        Invoke-Suite 'tests/NetworkCheck.tscn' 'NETWORK ALL OK' 'network-client.log'
    }
    finally {
        if (-not $testServer.HasExited) {
            if ($env:OS -eq 'Windows_NT') { & taskkill.exe /PID $testServer.Id /T /F | Out-Null }
            else { Stop-Process -Id $testServer.Id -ErrorAction SilentlyContinue }
        }
    }
    Write-Output 'All C# checks passed.'
}
finally { Pop-Location }
