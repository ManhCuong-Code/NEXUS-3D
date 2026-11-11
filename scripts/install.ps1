# ==============================================================================
# NEXUS-3D – Windows User Installation Script
# Installs NEXUS-3D Desktop GUI & CLI, creates Desktop and Start Menu shortcuts,
# configures User PATH, and generates uninstaller.
# ==============================================================================

[CmdletBinding()]
param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\NEXUS-3D",
    [switch]$SkipPublish = $false,
    [switch]$LaunchAfterInstall = $false
)

$ErrorActionPreference = "Stop"
$rootDir = (Get-Item $PSScriptRoot).Parent.FullName

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "       NEXUS-3D – Universal 3D & Schematic Converter Installer        " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Installation target: $InstallDir" -ForegroundColor Gray

# 1. Publish latest binaries if not skipped
if (-not $SkipPublish) {
    Write-Host "`n[1/5] Building & Publishing latest Release binaries..." -ForegroundColor Green
    $wpfProj = Join-Path $rootDir "src\NEXUS3D.App\NEXUS3D.App.csproj"
    $cliProj = Join-Path $rootDir "src\Nexus3D.CLI\Nexus3D.CLI.csproj"

    $publishWpf = Join-Path $rootDir "dist\publish\wpf"
    $publishCli = Join-Path $rootDir "dist\publish\cli"

    New-Item -ItemType Directory -Path $publishWpf -Force | Out-Null
    New-Item -ItemType Directory -Path $publishCli -Force | Out-Null

    Write-Host "  -> Publishing Desktop Studio (NEXUS3D.App) [Self-Contained win-x64]..." -ForegroundColor Gray
    dotnet publish $wpfProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishWpf --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "WPF publish failed." }

    Write-Host "  -> Publishing Headless CLI (nexus3d.exe) [Self-Contained win-x64]..." -ForegroundColor Gray
    dotnet publish $cliProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishCli --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "CLI publish failed." }
} else {
    $publishWpf = Join-Path $rootDir "dist\publish\wpf"
    $publishCli = Join-Path $rootDir "dist\publish\cli"
}

# 2. Deploy files to InstallDir
Write-Host "`n[2/5] Deploying files to '$InstallDir'..." -ForegroundColor Green
$appDir = Join-Path $InstallDir "Application"
$cliDir = Join-Path $InstallDir "CLI"
$pluginsDir = Join-Path $InstallDir "Plugins"
$docsDir = Join-Path $InstallDir "Documentation"

New-Item -ItemType Directory -Path $appDir -Force | Out-Null
New-Item -ItemType Directory -Path $cliDir -Force | Out-Null
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

Write-Host "  -> Copying Application binaries..." -ForegroundColor Gray
Copy-Item (Join-Path $publishWpf "*") -Destination $appDir -Recurse -Force

# Ensure Resources\app.ico is in Application
$targetIcon = Join-Path $appDir "Resources\app.ico"
if (-not (Test-Path $targetIcon)) {
    $sourceIcon = Join-Path $rootDir "assets\branding\app.ico"
    New-Item -ItemType Directory -Path (Join-Path $appDir "Resources") -Force | Out-Null
    Copy-Item $sourceIcon -Destination $targetIcon -Force
}

Write-Host "  -> Copying CLI binaries..." -ForegroundColor Gray
Copy-Item (Join-Path $publishCli "*") -Destination $cliDir -Recurse -Force

Write-Host "  -> Copying Sample Plugins & Documentation..." -ForegroundColor Gray
$samplePluginBuild = Join-Path $rootDir "plugins\NEXUS3D.Sample.Plugin\bin\Release\net10.0"
if (Test-Path $samplePluginBuild) {
    $sampleDest = Join-Path $pluginsDir "NEXUS3D.Sample.Plugin"
    New-Item -ItemType Directory -Path $sampleDest -Force | Out-Null
    Copy-Item (Join-Path $samplePluginBuild "*") -Destination $sampleDest -Recurse -Force
    $manifestSrc = Join-Path $rootDir "plugins\NEXUS3D.Sample.Plugin\manifest.json"
    if (Test-Path $manifestSrc) { Copy-Item $manifestSrc -Destination $sampleDest -Force }
}

if (Test-Path (Join-Path $rootDir "README.md")) { Copy-Item (Join-Path $rootDir "README.md") -Destination $docsDir -Force }
if (Test-Path (Join-Path $rootDir "RELEASE_NOTES.md")) { Copy-Item (Join-Path $rootDir "RELEASE_NOTES.md") -Destination $docsDir -Force }
if (Test-Path (Join-Path $rootDir "LICENSE")) { Copy-Item (Join-Path $rootDir "LICENSE") -Destination $InstallDir -Force }

# 3. Create Windows Shortcuts (Desktop & Start Menu)
Write-Host "`n[3/5] Creating Windows Shortcuts..." -ForegroundColor Green
$desktopPath = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$startMenuPrograms = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)) "NEXUS-3D"
New-Item -ItemType Directory -Path $startMenuPrograms -Force | Out-Null

$appExe = Join-Path $appDir "NEXUS3D.App.exe"
$cliExe = Join-Path $cliDir "nexus3d.exe"

$wsh = New-Object -ComObject WScript.Shell

# A. Desktop Shortcut
$desktopShortcutPath = Join-Path $desktopPath "NEXUS-3D.lnk"
Write-Host "  -> Creating Desktop Shortcut: $desktopShortcutPath" -ForegroundColor Gray
$sc = $wsh.CreateShortcut($desktopShortcutPath)
$sc.TargetPath = $appExe
$sc.WorkingDirectory = $appDir
$sc.IconLocation = "$targetIcon,0"
$sc.WindowStyle = 1
$sc.Description = "NEXUS-3D – Universal 3D & Schematic Converter"
$sc.Save()

# B. Start Menu Shortcuts
$smAppShortcutPath = Join-Path $startMenuPrograms "NEXUS-3D.lnk"
Write-Host "  -> Creating Start Menu Shortcut: $smAppShortcutPath" -ForegroundColor Gray
$scApp = $wsh.CreateShortcut($smAppShortcutPath)
$scApp.TargetPath = $appExe
$scApp.WorkingDirectory = $appDir
$scApp.IconLocation = "$targetIcon,0"
$scApp.WindowStyle = 1
$scApp.Description = "NEXUS-3D – Universal 3D & Schematic Converter"
$scApp.Save()

$smCliShortcutPath = Join-Path $startMenuPrograms "NEXUS-3D CLI.lnk"
Write-Host "  -> Creating Start Menu CLI Shortcut: $smCliShortcutPath" -ForegroundColor Gray
$scCli = $wsh.CreateShortcut($smCliShortcutPath)
$scCli.TargetPath = $cliExe
$scCli.WorkingDirectory = $cliDir
$scCli.IconLocation = "$targetIcon,0"
$scCli.WindowStyle = 1
$scCli.Description = "NEXUS-3D Headless CLI"
$scCli.Save()

# 4. Configure User PATH
Write-Host "`n[4/5] Adding CLI to User PATH environment variable..." -ForegroundColor Green
$userPath = [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::User)
if ($userPath -split ';' -notcontains $cliDir) {
    $newUserPath = if ([string]::IsNullOrWhiteSpace($userPath)) { $cliDir } else { "$userPath;$cliDir" }
    [Environment]::SetEnvironmentVariable("Path", $newUserPath, [EnvironmentVariableTarget]::User)
    Write-Host "  -> Successfully added '$cliDir' to User PATH." -ForegroundColor Gray
} else {
    Write-Host "  -> CLI directory already present in User PATH." -ForegroundColor Gray
}

# 5. Create uninstaller
Write-Host "`n[5/5] Creating Uninstaller..." -ForegroundColor Green
$uninstallerPath = Join-Path $InstallDir "uninstall.ps1"
$uninstallScript = @"
Write-Host "Uninstalling NEXUS-3D from '$InstallDir'..." -ForegroundColor Yellow

# Remove Desktop Shortcut
`$desktopLnk = "$desktopShortcutPath"
if (Test-Path `$desktopLnk) {
    Remove-Item -Path `$desktopLnk -Force
    Write-Host "Removed desktop shortcut." -ForegroundColor Gray
}

# Remove Start Menu
`$startMenuDir = "$startMenuPrograms"
if (Test-Path `$startMenuDir) {
    Remove-Item -Path `$startMenuDir -Recurse -Force
    Write-Host "Removed Start Menu shortcuts." -ForegroundColor Gray
}

# Remove PATH entry
`$cliDir = "$cliDir"
`$userPath = [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::User)
`$paths = `$userPath -split ';' | Where-Object { `$_ -ne `$cliDir }
[Environment]::SetEnvironmentVariable("Path", (`$paths -join ';'), [EnvironmentVariableTarget]::User)
Write-Host "Cleaned up PATH environment variable." -ForegroundColor Gray

Write-Host "To complete uninstallation, delete the folder: $InstallDir" -ForegroundColor Green
"@
Set-Content -Path $uninstallerPath -Value $uninstallScript -Encoding UTF8

Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host "               NEXUS-3D INSTALLED SUCCESSFULLY!                       " -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "Installation Directory: $InstallDir"
Write-Host "Desktop Shortcut:       $desktopShortcutPath"
Write-Host "Start Menu:             $startMenuPrograms"
Write-Host "CLI Command:            nexus3d (available in terminal)`n"

if ($LaunchAfterInstall) {
    Write-Host "Launching NEXUS-3D..." -ForegroundColor Cyan
    Start-Process -FilePath $appExe -WorkingDirectory $appDir
}

