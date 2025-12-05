param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# ========================================
# Configuration
# ========================================

$publishRoot = "publish"
$versionPath = Join-Path $publishRoot $Version

$projects = @(
    @{
        Name = "KotonohaAssistant.Alarm"
        Path = "KotonohaAssistant.Alarm\KotonohaAssistant.Alarm.csproj"
        Type = "Modern"
    },
    @{
        Name = "KotonohaAssistant.VoiceServer"
        Path = "KotonohaAssistant.VoiceServer\KotonohaAssistant.VoiceServer.csproj"
        Type = "Framework"
    },
    @{
        Name = "KotonohaAssistant.Vui"
        Path = "KotonohaAssistant.Vui\KotonohaAssistant.Vui.csproj"
        Type = "MAUI"
        Framework = "net9.0-windows10.0.19041.0"
    },
    @{
        Name = "KotonohaAssistant.Cli"
        Path = "KotonohaAssistant.Cli\KotonohaAssistant.Cli.csproj"
        Type = "Modern"
    }
)

# ========================================
# Helper Functions
# ========================================

function Find-MSBuild {
    Write-Host "Locating MSBuild..." -ForegroundColor Cyan

    # Try vswhere
    $vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswherePath) {
        $msbuildPath = & $vswherePath -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
        if ($msbuildPath -and (Test-Path $msbuildPath)) {
            Write-Host "  Found: $msbuildPath" -ForegroundColor Green
            return $msbuildPath
        }
    }

    # Fallback: Check common Visual Studio locations
    $vsEditions = @("Enterprise", "Professional", "Community")
    $vsYears = @("2022", "2019")

    foreach ($year in $vsYears) {
        foreach ($edition in $vsEditions) {
            $msbuildPath = "${env:ProgramFiles}\Microsoft Visual Studio\$year\$edition\MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $msbuildPath) {
                Write-Host "  Found: $msbuildPath" -ForegroundColor Green
                return $msbuildPath
            }
        }
    }

    throw "MSBuild not found. Please ensure Visual Studio is installed."
}

function Write-BuildHeader {
    param([string]$ProjectName, [string]$ProjectPath, [string]$ProjectType, [string]$OutputPath)

    Write-Host "`nPublishing $ProjectName..." -ForegroundColor Cyan
    Write-Host "  Project: $ProjectPath" -ForegroundColor Gray
    Write-Host "  Type: $ProjectType" -ForegroundColor Gray
    Write-Host "  Output: $OutputPath" -ForegroundColor Gray
}

function Assert-BuildSuccess {
    param([string]$ProjectName)

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build $ProjectName"
    }
}

function Copy-BuildOutput {
    param([string]$SourceDir, [string]$OutputPath, [string]$ProjectName)

    if (-not (Test-Path $SourceDir)) {
        throw "Output directory not found: $SourceDir"
    }

    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Copy-Item -Path "$SourceDir\*" -Destination $OutputPath -Recurse -Force
    Write-Host "  ✓ Built and copied successfully" -ForegroundColor Green
}

function Copy-FileToVersion {
    param([string]$SourceFile, [string]$DestinationName)

    $destPath = Join-Path $versionPath $DestinationName
    $destFolder = Split-Path $destPath -Parent

    if (-not (Test-Path $destFolder)) {
        New-Item -Path $destFolder -ItemType Directory | Out-Null
    }

    if (Test-Path $SourceFile) {
        Copy-Item -Path $SourceFile -Destination $destPath
        Write-Host "  ✓ $DestinationName copied" -ForegroundColor Green
    } else {
        Write-Host "  Warning: $SourceFile not found, skipping" -ForegroundColor Yellow
    }
}

function Build-FrameworkProject {
    param($Project, [string]$OutputPath, [string]$MSBuildPath)

    Write-Host "  Using MSBuild..." -ForegroundColor Gray

    # Restore NuGet packages
    & $MSBuildPath $Project.Path /t:Restore /v:minimal
    Assert-BuildSuccess $Project.Name

    # Build
    & $MSBuildPath $Project.Path /p:Configuration=Release /p:Platform=AnyCPU /t:Build /v:minimal
    Assert-BuildSuccess $Project.Name

    # Copy output
    $sourceDir = Join-Path (Split-Path $Project.Path -Parent) "bin\Release"
    Copy-BuildOutput -SourceDir $sourceDir -OutputPath $OutputPath -ProjectName $Project.Name
}

function Build-MAUIProject {
    param($Project, [string]$OutputPath, [string]$MSBuildPath)

    Write-Host "  Using MSBuild (MAUI)..." -ForegroundColor Gray

    if ($Project.Framework) {
        Write-Host "  Framework: $($Project.Framework)" -ForegroundColor Gray
    }

    # Build with MSBuild
    & $MSBuildPath $Project.Path /p:Configuration=Release /p:Platform=AnyCPU /t:Restore,Rebuild /v:minimal
    Assert-BuildSuccess $Project.Name

    # Copy output
    $sourceDir = Join-Path (Split-Path $Project.Path -Parent) "bin\Release\$($Project.Framework)\win10-x64"
    Copy-BuildOutput -SourceDir $sourceDir -OutputPath $OutputPath -ProjectName $Project.Name
}

function Build-ModernProject {
    param($Project, [string]$OutputPath)

    Write-Host "  Using dotnet publish..." -ForegroundColor Gray

    dotnet publish $Project.Path `
        --configuration Release `
        --output $OutputPath `
        --self-contained false `
        /p:PublishSingleFile=false

    Assert-BuildSuccess $Project.Name
    Write-Host "  ✓ Published successfully" -ForegroundColor Green
}

# ========================================
# Main Build Process
# ========================================

Write-Host "Starting release build for version: $Version" -ForegroundColor Green

# Find MSBuild if needed
$msbuildPath = $null
$needsMSBuild = $projects | Where-Object { $_.Type -eq "Framework" -or $_.Type -eq "MAUI" }
if ($needsMSBuild) {
    $msbuildPath = Find-MSBuild
}

# Create publish directory
Write-Host "`nCreating directory structure..." -ForegroundColor Cyan
if (Test-Path $versionPath) {
    Write-Host "Warning: Version directory already exists. Cleaning up..." -ForegroundColor Yellow
    Remove-Item -Path $versionPath -Recurse -Force
}
New-Item -ItemType Directory -Path $versionPath -Force | Out-Null

# Build each project
foreach ($project in $projects) {
    $outputPath = Join-Path $versionPath $project.Name

    Write-BuildHeader -ProjectName $project.Name -ProjectPath $project.Path -ProjectType $project.Type -OutputPath $outputPath

    try {
        switch ($project.Type) {
            "Framework" { Build-FrameworkProject -Project $project -OutputPath $outputPath -MSBuildPath $msbuildPath }
            "MAUI"      { Build-MAUIProject -Project $project -OutputPath $outputPath -MSBuildPath $msbuildPath }
            "Modern"    { Build-ModernProject -Project $project -OutputPath $outputPath }
        }
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
        exit 1
    }
}

# Copy additional files
Write-Host "`nCopying additional files..." -ForegroundColor Cyan
Copy-FileToVersion -SourceFile ".env.example" -DestinationName ".env"
Copy-FileToVersion -SourceFile "start.bat" -DestinationName "start.bat"
Copy-FileToVersion -SourceFile "README.md" -DestinationName "README.md"
Copy-FileToVersion -SourceFile "LICENSE" -DestinationName "LICENSE"
Copy-FileToVersion -SourceFile "THIRD-PARTY-NOTICES" -DestinationName "THIRD-PARTY-NOTICES"
Copy-FileToVersion -SourceFile "assets\Clock-Alarm02-1(Loop).mp3" -DestinationName "assets\Clock-Alarm02-1(Loop).mp3"

# Summary
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Release build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor White
Write-Host "Output: $versionPath" -ForegroundColor White
Write-Host ""
Write-Host "Published projects:" -ForegroundColor White
foreach ($project in $projects) {
    Write-Host "  - $($project.Name)" -ForegroundColor Gray
}
