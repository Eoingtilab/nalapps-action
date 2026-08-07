$ErrorActionPreference = "Stop"

$Dotnet = "C:\Program Files\dotnet\dotnet.exe"
$Project = ".\src\NalaApps.Action.App\NalaApps.Action.App.csproj"
$Publish = ".\publish"

if (-not (Test-Path $Dotnet)) { throw ".NET SDK를 찾을 수 없습니다: $Dotnet" }
if (-not (Test-Path $Project)) { throw "프로젝트를 찾을 수 없습니다: $Project" }

$Version = (Get-Content ".\VERSION" -Raw).Trim()
$Manifest = Get-Content ".\release-manifest.json" -Raw | ConvertFrom-Json
if ($Version -ne "1.0.0") { throw "VERSION 불일치: $Version" }
if ($Manifest.version -ne $Version) { throw "release-manifest 버전이 일치하지 않습니다." }

Remove-Item $Publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\src\NalaApps.Action.App\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\src\NalaApps.Action.App\obj" -Recurse -Force -ErrorAction SilentlyContinue

& $Dotnet restore $Project
if ($LASTEXITCODE -ne 0) { throw "dotnet restore 실패" }

& $Dotnet build $Project -c Release --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) { throw "Release build 실패" }

& $Dotnet publish $Project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  --no-restore `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $Publish
if ($LASTEXITCODE -ne 0) { throw "Release publish 실패" }

$Exe = Resolve-Path ".\publish\NalaApps.Action.exe" -ErrorAction Stop
$Info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Exe)
if ($Info.FileVersion -ne "1.0.0.0") { throw "EXE FileVersion 불일치: $($Info.FileVersion)" }
if ($Info.ProductName -ne "날라액션") { throw "EXE ProductName 불일치: $($Info.ProductName)" }
if ((Get-Item $Exe).Length -lt 1MB) { throw "EXE 크기가 비정상적으로 작습니다." }

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "날라액션 v1.0.0 Release Build PASS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "EXE: $Exe"
Write-Host "Product: $($Info.ProductName)"
Write-Host "Version: $($Info.FileVersion)"

Start-Process $Exe
