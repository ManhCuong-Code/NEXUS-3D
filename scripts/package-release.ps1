# ==============================================================================
# NEXUS-3D – Automated Release Packaging & Artifact Generator
# Produces: Self-contained WPF, Standalone CLI, Portable ZIP, SDK NuGet, Checksums
# ==============================================================================

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$rootDir = (Get-Item $PSScriptRoot).Parent.FullName
$distDir = Join-Path $rootDir "dist"
$publishWpfDir = Join-Path $distDir "publish\wpf"
$publishCliDir = Join-Path $distDir "publish\cli"
$nugetDir = Join-Path $distDir "nuget"
$pluginsDir = Join-Path $distDir "plugins"
$installerOutDir = Join-Path $distDir "installer"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "             NEXUS-3D v$Version RELEASE PACKAGING ENGINE              " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Configuration : $Configuration"
Write-Host "Runtime       : $Runtime"
Write-Host "Root Directory: $rootDir"
Write-Host "Dist Directory: $distDir"
Write-Host ""

# Clean previous dist
if (Test-Path $distDir) {
    Write-Host "[1/7] Cleaning previous dist directory..." -ForegroundColor Yellow
    Remove-Item -Path $distDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishWpfDir -Force | Out-Null
New-Item -ItemType Directory -Path $publishCliDir -Force | Out-Null
New-Item -ItemType Directory -Path $nugetDir -Force | Out-Null
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutDir -Force | Out-Null

# 1. Publish WPF App (Self-contained)
Write-Host "[2/7] Publishing WPF Desktop Application (self-contained, $Runtime)..." -ForegroundColor Green
$wpfProj = Join-Path $rootDir "src\NEXUS3D.App\NEXUS3D.App.csproj"
dotnet publish $wpfProj -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=false -p:PublishReadyToRun=true -o $publishWpfDir

# 2. Publish Standalone CLI (Self-contained)
Write-Host "[3/7] Publishing Standalone Headless CLI (self-contained, $Runtime)..." -ForegroundColor Green
$cliProj = Join-Path $rootDir "src\Nexus3D.CLI\Nexus3D.CLI.csproj"
dotnet publish $cliProj -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:PublishReadyToRun=true -o $publishCliDir

# 3. Pack Plugin SDK NuGet Package
Write-Host "[4/7] Packaging NEXUS-3D Plugin SDK NuGet Package..." -ForegroundColor Green
$sdkProj = Join-Path $rootDir "src\NEXUS3D.Plugin.SDK\NEXUS3D.Plugin.SDK.csproj"
dotnet pack $sdkProj -c $Configuration -o $nugetDir -p:PackageVersion=$Version

# 4. Pack Sample Plugin (.nexusplugin package)
Write-Host "[5/7] Creating Sample Plugin Package (.nexusplugin)..." -ForegroundColor Green
$sampleProj = Join-Path $rootDir "plugins\NEXUS3D.Sample.Plugin\NEXUS3D.Sample.Plugin.csproj"
dotnet build $sampleProj -c $Configuration | Out-Null
$samplePluginBuildDir = Join-Path $rootDir "plugins\NEXUS3D.Sample.Plugin\bin\$Configuration\net10.0"
$sampleManifest = Join-Path $rootDir "plugins\NEXUS3D.Sample.Plugin\manifest.json"
$samplePackageOut = Join-Path $pluginsDir "com.nexus3d.formats.rawvox-$Version.nexusplugin"

$sampleStaging = Join-Path $distDir "staging_sample_plugin"
New-Item -ItemType Directory -Path $sampleStaging -Force | Out-Null
Copy-Item (Join-Path $samplePluginBuildDir "*") -Destination $sampleStaging -Recurse -Force
Copy-Item $sampleManifest -Destination $sampleStaging -Force
$sampleTempZip = Join-Path $distDir "temp_plugin.zip"
if (Test-Path $sampleTempZip) { Remove-Item $sampleTempZip -Force }
Compress-Archive -Path (Join-Path $sampleStaging "*") -DestinationPath $sampleTempZip -Force
Move-Item $sampleTempZip $samplePackageOut -Force
Remove-Item -Path $sampleStaging -Recurse -Force

# 5. Build Portable Distribution Package
Write-Host "[6/7] Assembling Portable Package and Zipping..." -ForegroundColor Green
$portableStaging = Join-Path $distDir "NEXUS-3D-Portable-v$Version-$Runtime"
New-Item -ItemType Directory -Path $portableStaging -Force | Out-Null

# Copy WPF application files
Copy-Item (Join-Path $publishWpfDir "*") -Destination $portableStaging -Recurse -Force

# Copy CLI executable
Copy-Item (Join-Path $publishCliDir "nexus3d.exe") -Destination $portableStaging -Force

# Add Plugins directory with Sample Plugin
$portablePluginsDir = Join-Path $portableStaging "Plugins"
New-Item -ItemType Directory -Path $portablePluginsDir -Force | Out-Null
Copy-Item $samplePackageOut -Destination $portablePluginsDir -Force

# Add Docs & Readme
$portableDocsDir = Join-Path $portableStaging "Documentation"
New-Item -ItemType Directory -Path $portableDocsDir -Force | Out-Null
if (Test-Path (Join-Path $rootDir "README.md")) { Copy-Item (Join-Path $rootDir "README.md") -Destination $portableDocsDir -Force }
if (Test-Path (Join-Path $rootDir "RELEASE_NOTES.md")) { Copy-Item (Join-Path $rootDir "RELEASE_NOTES.md") -Destination $portableDocsDir -Force }
if (Test-Path (Join-Path $rootDir "LICENSE")) { Copy-Item (Join-Path $rootDir "LICENSE") -Destination $portableStaging -Force }

# Create portable README.txt
$portableReadmeContent = @"
======================================================================
NEXUS-3D – Universal 3D Model Converter & Editor (Portable Edition)
Version: $Version
Architecture: $Runtime
======================================================================

QUICK START:
1. Double-click 'NEXUS3D.App.exe' to launch the Desktop GUI Studio.
2. Run 'nexus3d.exe --help' in terminal for the Headless CLI.

INCLUDED:
- Universal 3D Scene Model (USM) Conversion Pipeline
- Sponge v2/v3 & Legacy Minecraft Schematic support (.schem, .schematic)
- Wavefront OBJ / MTL Mesh Engine (.obj, .mtl)
- Minecraft Bedrock Geometry & Voxel JSON Engine (.json, .geo.json)
- Conservative 3D Voxelization Engine
- Plugin SDK & Extension Hub (Bundled with RawVox 3D plugin)

DATA & STORAGE:
In Portable Mode, configuration and local cache are preserved cleanly.
No installation or admin privileges required.
"@
Set-Content -Path (Join-Path $portableStaging "README.txt") -Value $portableReadmeContent -Encoding UTF8

$portableZipPath = Join-Path $distDir "NEXUS-3D-Portable-v$Version-$Runtime.zip"
Compress-Archive -Path (Join-Path $portableStaging "*") -DestinationPath $portableZipPath -Force
Remove-Item -Path $portableStaging -Recurse -Force

# Also create Standalone CLI ZIP
$cliStaging = Join-Path $distDir "nexus3d-cli-v$Version-$Runtime"
New-Item -ItemType Directory -Path $cliStaging -Force | Out-Null
Copy-Item (Join-Path $publishCliDir "*") -Destination $cliStaging -Recurse -Force
if (Test-Path (Join-Path $rootDir "LICENSE")) { Copy-Item (Join-Path $rootDir "LICENSE") -Destination $cliStaging -Force }
$cliZipPath = Join-Path $distDir "nexus3d-cli-v$Version-$Runtime.zip"
Compress-Archive -Path (Join-Path $cliStaging "*") -DestinationPath $cliZipPath -Force
Remove-Item -Path $cliStaging -Recurse -Force

# 6. Check Inno Setup and build Installer if available
$isccCandidates = @(
    "iscc.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$isccPath = $null
foreach ($cand in $isccCandidates) {
    if (Get-Command $cand -ErrorAction SilentlyContinue) {
        $isccPath = (Get-Command $cand).Source
        break
    }
    if (Test-Path $cand) {
        $isccPath = $cand
        break
    }
}

if ($isccPath) {
    Write-Host "[7/7] Inno Setup compiler found at '$isccPath'. Compiling Windows Installer..." -ForegroundColor Green
    $issFile = Join-Path $rootDir "installer\nexus3d_setup.iss"
    & $isccPath $issFile
} else {
    Write-Host "[7/7] ISCC.exe not found on build host. Installer script ready at 'installer/nexus3d_setup.iss'." -ForegroundColor Yellow
}

# 7. Generate Checksums
Write-Host "Generating SHA-256 Checksums for Release Artifacts..." -ForegroundColor Green
$checksumsFile = Join-Path $distDir "SHA256SUMS.txt"
$artifacts = Get-ChildItem -Path $distDir -File -Filter "*.zip"
$artifacts += Get-ChildItem -Path $distDir -Recurse -File -Filter "*.nupkg"
$artifacts += Get-ChildItem -Path $distDir -Recurse -File -Filter "*.nexusplugin"
if (Test-Path (Join-Path $installerOutDir "*.exe")) {
    $artifacts += Get-ChildItem -Path $installerOutDir -File -Filter "*.exe"
}

$checksumLines = @()
foreach ($file in $artifacts) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $relName = $file.FullName.Substring($distDir.Length + 1).Replace("\", "/")
    $checksumLines += "$hash  $relName"
}

$checksumLines | Set-Content -Path $checksumsFile -Encoding ASCII

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "                   RELEASE PACKAGING COMPLETED!                       " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Get-ChildItem -Path $distDir -File | Format-Table Name, Length, LastWriteTime
Get-Content -Path $checksumsFile
