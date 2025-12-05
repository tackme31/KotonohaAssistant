param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Define paths
$publishRoot = "publish"
$versionPath = Join-Path $publishRoot $Version

# Find MSBuild for .NET Framework projects
function Find-MSBuild {
    # Try to find vswhere first
    $vsWherePath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Msbuild\Current\Bin\MSBuild.exe"

    if (Test-Path $vsWherePath) {
        $msbuildPath = & $vsWherePath -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
        if ($msbuildPath -and (Test-Path $msbuildPath)) {
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
                return $msbuildPath
            }
        }
    }

    throw "MSBuild not found. Please ensure Visual Studio is installed."
}

# Projects to publish
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

Write-Host "Starting release build for version: $Version" -ForegroundColor Green

# Find MSBuild if needed
$msbuildPath = $null
$needsMSBuild = $projects | Where-Object { $_.Type -eq "Framework" -or $_.Type -eq "MAUI" }
if ($needsMSBuild) {
    Write-Host "`nLocating MSBuild..." -ForegroundColor Cyan
    $msbuildPath = Find-MSBuild
    Write-Host "  Found: $msbuildPath" -ForegroundColor Green
}

# Create publish directory structure
Write-Host "`nCreating directory structure..." -ForegroundColor Cyan
if (Test-Path $versionPath) {
    Write-Host "Warning: Version directory already exists. Cleaning up..." -ForegroundColor Yellow
    Remove-Item -Path $versionPath -Recurse -Force
}
New-Item -ItemType Directory -Path $versionPath -Force | Out-Null

# Build and publish each project
foreach ($project in $projects) {
    $projectName = $project.Name
    $projectPath = $project.Path
    $projectType = $project.Type
    $outputPath = Join-Path $versionPath $projectName

    Write-Host "`nPublishing $projectName..." -ForegroundColor Cyan
    Write-Host "  Project: $projectPath" -ForegroundColor Gray
    Write-Host "  Type: $projectType" -ForegroundColor Gray
    Write-Host "  Output: $outputPath" -ForegroundColor Gray

    if ($projectType -eq "Framework") {
        # Use MSBuild for .NET Framework projects
        Write-Host "  Using MSBuild..." -ForegroundColor Gray

        # Restore NuGet packages first
        & $msbuildPath $projectPath /t:Restore /v:minimal

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Failed to restore packages for $projectName" -ForegroundColor Red
            exit 1
        }

        & $msbuildPath $projectPath /p:Configuration=Release /p:Platform=AnyCPU /t:Build /v:minimal

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Failed to build $projectName" -ForegroundColor Red
            exit 1
        }

        # Copy output files
        $sourceDir = Join-Path (Split-Path $projectPath -Parent) "bin\Release"
        if (Test-Path $sourceDir) {
            New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
            Copy-Item -Path "$sourceDir\*" -Destination $outputPath -Recurse -Force
            Write-Host "  ✓ Built and copied successfully" -ForegroundColor Green
        } else {
            Write-Host "Error: Output directory not found: $sourceDir" -ForegroundColor Red
            exit 1
        }
    } elseif ($projectType -eq "MAUI") {
        # Use MSBuild for MAUI projects (Windows App SDK doesn't support dotnet publish)
        Write-Host "  Using MSBuild (MAUI)..." -ForegroundColor Gray

        $framework = $project.Framework
        if ($framework) {
            Write-Host "  Framework: $framework" -ForegroundColor Gray
        }

        # Build the project with MSBuild
        & $msbuildPath $projectPath /p:Configuration=Release /p:Platform=AnyCPU /t:Restore,Rebuild /v:minimal

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Failed to build $projectName" -ForegroundColor Red
            exit 1
        }

        # Copy output files - MAUI outputs to bin\Release\{framework}\win10-x64
        $sourceDir = Join-Path (Split-Path $projectPath -Parent) "bin\Release\$framework\win10-x64"
        if (Test-Path $sourceDir) {
            New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
            Copy-Item -Path "$sourceDir\*" -Destination $outputPath -Recurse -Force
            Write-Host "  ✓ Built and copied successfully" -ForegroundColor Green
        } else {
            Write-Host "Error: Output directory not found: $sourceDir" -ForegroundColor Red
            exit 1
        }
    } else {
        # Use dotnet publish for modern .NET projects
        Write-Host "  Using dotnet publish..." -ForegroundColor Gray

        dotnet publish $projectPath `
            --configuration Release `
            --output $outputPath `
            --self-contained false `
            /p:PublishSingleFile=false

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Failed to publish $projectName" -ForegroundColor Red
            exit 1
        }

        Write-Host "  ✓ Published successfully" -ForegroundColor Green
    }
}

# Copy .env.example to .env in version folder
Write-Host "`nCopying .env.example to .env..." -ForegroundColor Cyan
$envExamplePath = ".env.example"
$envDestPath = Join-Path $versionPath ".env"

if (Test-Path $envExamplePath) {
    Copy-Item -Path $envExamplePath -Destination $envDestPath
    Write-Host "  ✓ .env file created" -ForegroundColor Green
} else {
    Write-Host "  Warning: .env.example not found, skipping .env creation" -ForegroundColor Yellow
}

# Copy start.bat launcher to version folder
Write-Host "`nCopying start.bat launcher..." -ForegroundColor Cyan
$startBatPath = "start.bat"
$startBatDestPath = Join-Path $versionPath "start.bat"

if (Test-Path $startBatPath) {
    Copy-Item -Path $startBatPath -Destination $startBatDestPath
    Write-Host "  ✓ start.bat copied" -ForegroundColor Green
} else {
    Write-Host "  Warning: start.bat not found, skipping launcher creation" -ForegroundColor Yellow
}

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