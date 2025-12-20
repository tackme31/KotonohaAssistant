#!/usr/bin/env pwsh
# Test Coverage Report Generator
# Runs tests with coverage and generates HTML report

param(
    [switch]$SkipTests,
    [switch]$SkipReport,
    [switch]$Open = $true
)

$ErrorActionPreference = "Stop"

# Paths
$testProject = "KotonohaAssistant.AI.Tests/KotonohaAssistant.AI.Tests.csproj"
$runSettings = "KotonohaAssistant.AI.Tests/coverlet.runsettings"
$coveragePattern = "KotonohaAssistant.AI.Tests/TestResults/**/coverage.cobertura.xml"
$reportDir = "TestResults/CoverageReport"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test Coverage Report Generator" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Step 1: Run tests with coverage
if (-not $SkipTests) {
    Write-Host "[1/3] Running tests with coverage..." -ForegroundColor Yellow

    # Clean previous results
    if (Test-Path "KotonohaAssistant.AI.Tests/TestResults") {
        Remove-Item "KotonohaAssistant.AI.Tests/TestResults" -Recurse -Force
        Write-Host "  → Cleaned previous test results" -ForegroundColor Gray
    }

    # Run tests
    dotnet test $testProject --settings $runSettings

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n✗ Tests failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "  ✓ Tests passed" -ForegroundColor Green
} else {
    Write-Host "[1/3] Skipping tests (--SkipTests)" -ForegroundColor Gray
}

# Step 2: Check if coverage data exists
$coverageFiles = Get-ChildItem -Path "KotonohaAssistant.AI.Tests/TestResults" -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue

if (-not $coverageFiles) {
    Write-Host "`n✗ No coverage data found!" -ForegroundColor Red
    Write-Host "  Run tests first: dotnet test $testProject --settings $runSettings" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n  → Found coverage data: $($coverageFiles.FullName)" -ForegroundColor Gray

# Step 3: Generate HTML report
if (-not $SkipReport) {
    Write-Host "`n[2/3] Generating HTML report..." -ForegroundColor Yellow

    # Check if reportgenerator is installed
    $reportGen = Get-Command reportgenerator -ErrorAction SilentlyContinue

    if (-not $reportGen) {
        Write-Host "  → ReportGenerator not found. Installing..." -ForegroundColor Yellow
        dotnet tool install -g dotnet-reportgenerator-globaltool

        if ($LASTEXITCODE -ne 0) {
            Write-Host "`n✗ Failed to install ReportGenerator!" -ForegroundColor Red
            exit $LASTEXITCODE
        }

        Write-Host "  ✓ ReportGenerator installed" -ForegroundColor Green
    }

    # Generate report
    reportgenerator -reports:$coveragePattern -targetdir:$reportDir -reporttypes:Html

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n✗ Failed to generate report!" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "  ✓ Report generated at: $reportDir/index.html" -ForegroundColor Green
} else {
    Write-Host "`n[2/3] Skipping report generation (--SkipReport)" -ForegroundColor Gray
}

# Step 4: Open report in browser
if ($Open -and -not $SkipReport) {
    Write-Host "`n[3/3] Opening report in browser..." -ForegroundColor Yellow

    $indexPath = Join-Path $reportDir "index.html"

    if (Test-Path $indexPath) {
        Start-Process $indexPath
        Write-Host "  ✓ Opened $indexPath" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Report not found: $indexPath" -ForegroundColor Red
    }
} else {
    Write-Host "`n[3/3] Skipping browser open" -ForegroundColor Gray
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Coverage report complete!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
