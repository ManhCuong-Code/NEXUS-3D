$ErrorActionPreference = "Stop"
Write-Host "=== NEXUS 3D Build & Test Verification Script ===" -ForegroundColor Cyan

dotnet restore NEXUS-3D.sln
dotnet build NEXUS-3D.sln -c Debug --no-restore
dotnet build NEXUS-3D.sln -c Release --no-restore
dotnet test NEXUS-3D.sln -c Release --no-build --verbosity normal

Write-Host "=== All builds and tests succeeded! ===" -ForegroundColor Green

