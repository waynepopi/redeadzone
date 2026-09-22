#Requires -Version 5.1
<#
.SYNOPSIS
    Packages Deadzone for release: restore, build, test, publish, ZIP, installer, SHA-256.

.PARAMETER Version
    Release version string, e.g. "0.1.0"

.EXAMPLE
    .\scripts\package.ps1 -Version 0.1.0
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$RepoRoot    = Split-Path -Parent $PSScriptRoot
$PublishDir  = Join-Path $RepoRoot "artifacts\publish\win-x64"
$ReleaseDir  = Join-Path $RepoRoot "artifacts\release"
$ProjectFile = Join-Path $RepoRoot "src\Deadzone.App\Deadzone.App.csproj"
$IssFile     = Join-Path $RepoRoot "installer\Deadzone.iss"
$ZipName     = "Deadzone-v$Version-win-x64.zip"
$SetupName   = "Deadzone-v$Version-Setup.exe"
$ZipPath     = Join-Path $ReleaseDir $ZipName
$SetupPath   = Join-Path $ReleaseDir $SetupName
$SumsPath    = Join-Path $ReleaseDir "SHA256SUMS.txt"

# Locate dotnet SDK
$DotnetExe = Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $DotnetExe) {
    $candidates = @(
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
        "C:\Program Files\dotnet\dotnet.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $DotnetExe = $c; break }
    }
}
if (-not $DotnetExe -or -not (Test-Path $DotnetExe)) {
    Write-Error "dotnet SDK not found. Install .NET 10 SDK from https://aka.ms/dotnet/download"
}

Write-Host "Using dotnet: $DotnetExe" -ForegroundColor Cyan
& $DotnetExe --version

# Locate Inno Setup compiler
$IsccExe = "C:\Program Files (x86)\Inno Setup 7\ISCC.exe"
if (-not (Test-Path $IsccExe)) {
    $IsccExe = "C:\Program Files\Inno Setup 7\ISCC.exe"
}
if (-not (Test-Path $IsccExe)) {
    Write-Error "Inno Setup 7 not found. Install from https://jrsoftware.org/isinfo.php`nExpected at: C:\Program Files (x86)\Inno Setup 7\ISCC.exe"
}
Write-Host "Using ISCC: $IsccExe" -ForegroundColor Cyan

# --- Clean old artifacts ---
Write-Host "`n[1/8] Cleaning old artifacts..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $ReleaseDir) { Remove-Item $ReleaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null

# --- Restore ---
Write-Host "`n[2/8] Restoring NuGet packages..." -ForegroundColor Yellow
& $DotnetExe restore (Join-Path $RepoRoot "Deadzone.sln")
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet restore failed." }

# --- Build ---
Write-Host "`n[3/8] Building Release..." -ForegroundColor Yellow
& $DotnetExe build (Join-Path $RepoRoot "Deadzone.sln") -c Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet build failed." }

# --- Test ---
Write-Host "`n[4/8] Running tests..." -ForegroundColor Yellow
& $DotnetExe test (Join-Path $RepoRoot "Deadzone.sln") -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet test failed. Packaging aborted." }

# --- Publish ---
Write-Host "`n[5/8] Publishing self-contained win-x64..." -ForegroundColor Yellow
& $DotnetExe publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed." }

# --- Verify critical output files ---
Write-Host "`n[5b] Verifying publish output..." -ForegroundColor Yellow
$required = @("Deadzone.exe", "HIDMaestro.Core.dll", "SDL3.dll")
foreach ($f in $required) {
    $fp = Join-Path $PublishDir $f
    if (-not (Test-Path $fp)) {
        Write-Error "Required file missing from publish output: $f`nCheck that NuGet native assets are copied correctly."
    }
    Write-Host "  OK: $f ($([math]::Round((Get-Item $fp).Length / 1MB, 2)) MB)" -ForegroundColor Green
}

# --- ZIP ---
Write-Host "`n[6/8] Creating ZIP..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PublishDir, $ZipPath)
Write-Host "  Created: $ZipPath ($([math]::Round((Get-Item $ZipPath).Length / 1MB, 2)) MB)" -ForegroundColor Green

# --- Installer ---
Write-Host "`n[7/8] Building installer..." -ForegroundColor Yellow
# Pass version to ISCC via define override
& $IsccExe "/DMyAppVersion=$Version" $IssFile
if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup compilation failed." }
if (-not (Test-Path $SetupPath)) {
    Write-Error "Installer not found at expected path: $SetupPath"
}
Write-Host "  Created: $SetupPath ($([math]::Round((Get-Item $SetupPath).Length / 1MB, 2)) MB)" -ForegroundColor Green

# --- SHA-256 ---
Write-Host "`n[8/8] Computing SHA-256 hashes..." -ForegroundColor Yellow
$sumsContent = @()
foreach ($file in @($ZipPath, $SetupPath)) {
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash
    $name = Split-Path -Leaf $file
    $sumsContent += "$hash  $name"
    Write-Host "  $hash  $name" -ForegroundColor Cyan
}
$sumsContent | Set-Content -Path $SumsPath -Encoding UTF8
Write-Host "  Written: $SumsPath" -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "  Deadzone v$Version release COMPLETE" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "  Installer : $SetupPath"
Write-Host "  ZIP       : $ZipPath"
Write-Host "  SHA256    : $SumsPath"
