# PowerShell Build, Test & Packaging Script for Limbus Split Pro
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$CertThumbprint = ""
)

$ErrorActionPreference = "Stop"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " Building Limbus Split Pro ($Configuration | $Platform)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Restore & Compile Solution
Write-Host "`n[1/5] Compilando solución .NET 8..." -ForegroundColor Yellow
dotnet restore LimbusSplitPro.sln
dotnet build LimbusSplitPro.sln -c $Configuration -r win-x64 --no-restore

# 2. Run Unit Tests
Write-Host "`n[2/5] Ejecutando pruebas unitarias..." -ForegroundColor Yellow
dotnet test src\LimbusSplitPro.Tests\LimbusSplitPro.Tests.csproj -c $Configuration --no-build

# 3. Publish Self-Contained App
Write-Host "`n[3/5] Publicando ejecutable autocontenido win-x64..." -ForegroundColor Yellow
dotnet publish src\LimbusSplitPro.App\LimbusSplitPro.App.csproj -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=false -o src\LimbusSplitPro.App\bin\Release\net8.0-windows\win-x64\publish

# 4. Generate SBOM
Write-Host "`n[4/5] Generando SBOM CycloneDX..." -ForegroundColor Yellow
python scripts/generate_sbom.py

# 5. Build Installer (if ISCC.exe is available)
Write-Host "`n[5/5] Generando instalador de Windows (Inno Setup)..." -ForegroundColor Yellow
$innoPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (Test-Path $innoPath) {
    & $innoPath installer\setup.iss
    Write-Host "Instalador generado con éxito en dist\LimbusSplitPro_v1.0.0_Setup.exe" -ForegroundColor Green
} else {
    Write-Host "ISCC.exe no encontrado. Omitiendo generación de ejecutable Setup.exe." -ForegroundColor Warning
}

# 6. Authenticode Signing (if thumbprint provided)
if ($CertThumbprint -ne "") {
    Write-Host "`n[Sign] Firmando binarios con Authenticode..." -ForegroundColor Yellow
    powershell -ExecutionPolicy Bypass -File scripts\sign.ps1 -Thumbprint $CertThumbprint
}

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host " Compilación finalizada con éxito!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
