# build.ps1 - Builds entire solution and creates setup package
$root = Split-Path $PSScriptRoot -Parent
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
$output = Join-Path $PSScriptRoot "Output"

Write-Host "Building WPF-LoginForm..." -ForegroundColor Cyan
& $msbuild (Join-Path $root "WPF-LoginForm\WPF-LoginForm.csproj") "/p:Configuration=Release" "/v:q" "/nologo"
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED" -ForegroundColor Red; exit 1 }

Write-Host "Building SetupWizard..." -ForegroundColor Cyan
& $msbuild (Join-Path $root "SetupWizard\SetupWizard.csproj") "/p:Configuration=Release" "/v:q" "/nologo"
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED" -ForegroundColor Red; exit 1 }

if (Test-Path $output) { Remove-Item $output -Recurse -Force }
New-Item $output -ItemType Directory | Out-Null

Write-Host "Copying files..." -ForegroundColor Cyan
Copy-Item (Join-Path $root "WPF-LoginForm\bin\Release\*") $output -Recurse
Copy-Item (Join-Path $root "SetupWizard\bin\Release\SetupWizard.exe") $output

Write-Host "`nSetup package created at: $output" -ForegroundColor Green
Write-Host "`nNext: Place postgresql-portable.zip in the Output folder, then run SetupWizard.exe" -ForegroundColor Yellow
