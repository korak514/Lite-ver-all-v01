# Build and Run Script for WPF Project
param (
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [Parameter(Mandatory=$false)]
    [switch]$Run = $false,

    [Parameter(Mandatory=$false)]
    [switch]$Restore = $false
)

$ErrorActionPreference = "Stop"

# 0. Kill any running instance (file lock prevention)
$procName = "WPF-LoginForm"
$running = Get-Process -Name $procName -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Killing running $procName (PID $($running.Id))..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 1. Locate MSBuild
$msBuildPaths = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)

$msBuild = $null
foreach ($path in $msBuildPaths) {
    if (Test-Path $path) {
        $msBuild = $path
        break
    }
}

if (-not $msBuild) {
    Write-Error "MSBuild.exe not found. Please ensure Visual Studio is installed."
    exit 1
}

Write-Host "Using MSBuild: $msBuild" -ForegroundColor Cyan

$projectFile = "WPF-LoginForm\WPF-LoginForm.csproj"
$binPath = "WPF-LoginForm\bin\$Configuration\WPF-LoginForm.exe"

# 2. Restore Packages
if ($Restore) {
    Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
    & $msBuild /t:restore $projectFile
}

# 3. Build Project
Write-Host "Building project ($Configuration)..." -ForegroundColor Yellow
& $msBuild $projectFile /p:Configuration=$Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit $LASTEXITCODE
}

Write-Host "Build successful!" -ForegroundColor Green

# 4. Run Project
if ($Run) {
    if (Test-Path $binPath) {
        Write-Host "Starting application: $binPath" -ForegroundColor Cyan
        Start-Process $binPath
    } else {
        Write-Error "Executable not found at $binPath"
    }
}
