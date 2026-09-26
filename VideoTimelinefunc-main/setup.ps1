# Setup script for VideoTimelineApp (ASP.NET MVC 5 / .NET 4.5.2)
# Run this script from D:\VideoTimelineApp\
# Requires: NuGet CLI or Visual Studio

Write-Host ""
Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   VideoTimelineApp – Setup Script" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# 1. Create uploads directory
$uploadsPath = Join-Path $PSScriptRoot "uploads\videos"
if (-not (Test-Path $uploadsPath)) {
    New-Item -ItemType Directory -Force -Path $uploadsPath | Out-Null
    Write-Host "✅ Created: uploads\videos\" -ForegroundColor Green
} else {
    Write-Host "✅ uploads\videos\ already exists" -ForegroundColor Green
}

# 2. Download nuget.exe if not present
$nugetPath = Join-Path $PSScriptRoot "nuget.exe"
if (-not (Test-Path $nugetPath)) {
    Write-Host "⬇️  Downloading NuGet CLI..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest `
            -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" `
            -OutFile $nugetPath `
            -UseBasicParsing
        Write-Host "✅ nuget.exe downloaded" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to download nuget.exe. Download manually from:" -ForegroundColor Red
        Write-Host "   https://www.nuget.org/downloads" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "✅ nuget.exe found" -ForegroundColor Green
}

# 3. Restore NuGet packages
Write-Host ""
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
$result = & $nugetPath restore (Join-Path $PSScriptRoot "VideoTimelineApp.sln") 2>&1
Write-Host $result

Write-Host ""
Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Open VideoTimelineApp.sln in Visual Studio" -ForegroundColor White
Write-Host "  2. Press F5 to build and run" -ForegroundColor White
Write-Host "  3. The database will be created automatically" -ForegroundColor White
Write-Host ""
Write-Host "Connection: (localdb)\mssqllocaldb → VideoTimelineDb" -ForegroundColor DarkGray
Write-Host "Upload dir: uploads\videos\" -ForegroundColor DarkGray
Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan
