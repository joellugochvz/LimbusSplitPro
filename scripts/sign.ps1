param(
    [Parameter(Mandatory=$true)]
    [string]$Thumbprint
)

$ErrorActionPreference = "Stop"

$signtool = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
if (-not (Test-Path $signtool)) {
    $signtool = "signtool.exe"
}

$filesToSign = @(
    "src\LimbusSplitPro.App\bin\Release\net8.0-windows\win-x64\publish\LimbusSplitPro.exe",
    "src\LimbusSplitPro.App\bin\Release\net8.0-windows\win-x64\publish\LimbusSplitPro.Core.dll",
    "dist\LimbusSplitPro_v1.0.0_Setup.exe"
)

foreach ($file in $filesToSign) {
    if (Test-Path $file) {
        Write-Host "Firmando con timestamp RFC 3161: $file" -ForegroundColor Cyan
        & $signtool sign /sha1 $Thumbprint /tr http://timestamp.digicert.com /td sha256 /fd sha256 $file
    }
}
