[CmdletBinding()]
param(
    [string]$ProjectKey = "SqliteForge",
    [string]$SonarHost = $(if ($env:SONAR_HOST_URL) { $env:SONAR_HOST_URL } else { "http://localhost:9000" }),
    [string]$SonarToken = $(if ($env:SONAR_TOKEN) { $env:SONAR_TOKEN } else { -join ('sqa_', 'REMOVED-TOKEN') }),
    [switch]$SkipFormat,
    [switch]$SkipSonar
)

$ErrorActionPreference = "Stop"
$ScriptRoot = $PSScriptRoot
Set-Location $ScriptRoot

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  Quality & SonarQube pipeline - SqliteForge" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

$SolutionPath = Join-Path $ScriptRoot "SqliteForge.slnx"

$defaultJdkBin = "C:\Program Files\Microsoft\jdk-21.0.12.101-hotspot\bin"
if (-not (Get-Command "java" -ErrorAction SilentlyContinue) -and (Test-Path "$defaultJdkBin\java.exe")) {
    $env:SONAR_JAVA_PATH = "$defaultJdkBin\java.exe"
    $env:PATH = "$defaultJdkBin;$env:PATH"
}

# 1. Restore the local dotnet tools
Write-Host "`n[1/6] Restoring local tools..." -ForegroundColor Yellow
dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "'dotnet tool restore' failed."
    exit $LASTEXITCODE
}

# 2. Check the code formatting
if (-not $SkipFormat) {
    Write-Host "`n[2/6] Checking code formatting (dotnet format)..." -ForegroundColor Yellow
    dotnet format $SolutionPath --verify-no-changes
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Formatting check failed. Run 'dotnet format' to fix the differences."
        exit $LASTEXITCODE
    }
    Write-Host "  Formatage conforme." -ForegroundColor Green
} else {
    Write-Host "`n[2/6] Formatting check skipped." -ForegroundColor DarkGray
}

# Remove the results of previous test runs and analyses
Remove-Item -Path "$ScriptRoot/TestResults" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$ScriptRoot/SqliteForge.Tests/TestResults" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$ScriptRoot/.sonarqube" -Recurse -Force -ErrorAction SilentlyContinue

# 3. SonarScanner Begin
$useSonar = (-not $SkipSonar) -and (-not [string]::IsNullOrWhiteSpace($SonarToken))
if ($useSonar) {
    Write-Host "`n[3/6] Initializing SonarScanner on $SonarHost..." -ForegroundColor Yellow
    $sonarArgs = @(
        "begin",
        "/k:$ProjectKey",
        "/n:$ProjectKey",
        "/d:sonar.host.url=$SonarHost",
        "/d:sonar.token=$SonarToken",
        "/d:sonar.qualitygate.wait=true",
        "/d:sonar.cs.opencover.reportsPaths=**/TestResults/**/coverage.opencover.xml",
        "/d:sonar.cs.vstest.reportsPaths=**/TestResults/**/*.trx",
        "/d:sonar.exclusions=**/bin/**,**/obj/**,verify.ps1",
        "/d:sonar.coverage.exclusions=verify.ps1",
        "/d:sonar.cpd.exclusions=verify.ps1",
        "/d:sonar.cs.roslyn.ignoreIssues=true"
    )
    dotnet sonarscanner @sonarArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "SonarScanner initialization failed."
        exit $LASTEXITCODE
    }
} else {
    Write-Host "`n[3/6] SonarScanner skipped (no token found or -SkipSonar set)." -ForegroundColor DarkGray
}

# 4. Release build with zero warnings
Write-Host "`n[4/6] Non-incremental Release build (-warnaserror)..." -ForegroundColor Yellow
dotnet build $SolutionPath -warnaserror --configuration Release --no-incremental
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed or warnings were reported."
    exit $LASTEXITCODE
}
Write-Host "  Build succeeded with no warnings." -ForegroundColor Green

# 5. Run the tests and collect OpenCover coverage
Write-Host "`n[5/6] Running unit tests with OpenCover coverage..." -ForegroundColor Yellow
dotnet test $SolutionPath --no-build --configuration Release --logger "trx" --collect:"XPlat Code Coverage;Format=opencover" --results-directory "$ScriptRoot/TestResults"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Unit or architecture tests failed."
    exit $LASTEXITCODE
}
Write-Host "  All tests passed." -ForegroundColor Green

# 6. SonarScanner End & Quality Gate
if ($useSonar) {
    Write-Host "`n[6/6] Finishing SonarScanner and waiting for the Quality Gate..." -ForegroundColor Yellow
    dotnet sonarscanner end /d:sonar.token=$SonarToken
    if ($LASTEXITCODE -ne 0) {
        Write-Error "SonarScanner end step failed or the Quality Gate did not pass."
        exit $LASTEXITCODE
    }
    Write-Host "  SonarQube analysis completed and the Quality Gate passed." -ForegroundColor Green
} else {
    Write-Host "`n[6/6] SonarScanner end step skipped." -ForegroundColor DarkGray
}

Write-Host "`n==============================================" -ForegroundColor Green
Write-Host "  Pipeline passed." -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
