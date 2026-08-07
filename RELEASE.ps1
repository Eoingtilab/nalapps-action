$ErrorActionPreference = "Stop"

$Dotnet = "C:\Program Files\dotnet\dotnet.exe"
$Project = ".\src\NalaApps.Action.App\NalaApps.Action.App.csproj"
$Publish = ".\publish"
$Artifacts = ".\artifacts"
$Manifest = Get-Content ".\release-manifest.json" -Raw | ConvertFrom-Json
$Version = (Get-Content ".\VERSION" -Raw).Trim()

if ($Version -ne "1.0.0") { throw "정식 릴리즈 VERSION은 1.0.0이어야 합니다. 현재: $Version" }
if ($Manifest.version -ne $Version) { throw "release-manifest 버전 불일치" }
if ($Manifest.source_tag -ne "v$Version") { throw "source_tag 불일치" }
if ($Manifest.release_tag -ne "utility-action-v$Version") { throw "release_tag 불일치" }

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "Git이 설치되어 있지 않습니다." }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "GitHub CLI(gh)가 필요합니다. https://cli.github.com/" }
if (-not (Test-Path $Dotnet)) { throw ".NET SDK를 찾을 수 없습니다: $Dotnet" }

$Branch = (git branch --show-current).Trim()
if ($Branch -ne "main") { throw "정식 릴리즈는 main 브랜치에서만 가능합니다. 현재: $Branch" }
if (git status --porcelain) { throw "Git 작업 트리가 깨끗하지 않습니다. 변경사항을 먼저 커밋하세요." }

git fetch origin --tags
git pull --ff-only origin main
if ($LASTEXITCODE -ne 0) { throw "origin/main 동기화 실패" }

& $Dotnet restore $Project
if ($LASTEXITCODE -ne 0) { throw "restore 실패" }

& $Dotnet build $Project -c Release --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) { throw "build 실패" }

Remove-Item $Publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $Artifacts -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $Artifacts | Out-Null

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
if ($LASTEXITCODE -ne 0) { throw "publish 실패" }

$Exe = Resolve-Path ".\publish\NalaApps.Action.exe" -ErrorAction Stop
$Info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Exe)
if ($Info.FileVersion -ne "1.0.0.0") { throw "EXE FileVersion 불일치: $($Info.FileVersion)" }
if ($Info.ProductName -ne "날라액션") { throw "EXE ProductName 불일치: $($Info.ProductName)" }
if ((Get-Item $Exe).Length -lt 1MB) { throw "EXE 크기가 비정상적으로 작습니다." }

$Zip = Join-Path $Artifacts $Manifest.artifact_name
$Checksum = Join-Path $Artifacts $Manifest.checksum_name
Compress-Archive -Path ".\publish\*" -DestinationPath $Zip -CompressionLevel Optimal -Force
$Hash = (Get-FileHash $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$Hash  $($Manifest.artifact_name)" | Set-Content $Checksum -Encoding ascii

$ExistingSourceTag = git tag -l $Manifest.source_tag
if ([string]::IsNullOrWhiteSpace($ExistingSourceTag)) {
  git tag -a $Manifest.source_tag -m "날라액션 $Version verified stable"
  git push origin $Manifest.source_tag
  if ($LASTEXITCODE -ne 0) { throw "소스 태그 push 실패" }
} else {
  $TagCommit = (git rev-list -n 1 $Manifest.source_tag).Trim()
  $HeadCommit = (git rev-parse HEAD).Trim()
  if ($TagCommit -ne $HeadCommit) {
    throw "기존 $($Manifest.source_tag) 태그가 현재 HEAD와 다릅니다. 정식 태그는 이동하지 않습니다."
  }
}

$SourceNotes = ".\docs\RELEASE_NOTES_v1.0.0.md"
gh release view $Manifest.source_tag --repo $Manifest.source_repository *> $null
if ($LASTEXITCODE -eq 0) {
  gh release upload $Manifest.source_tag $Zip $Checksum --repo $Manifest.source_repository --clobber
} else {
  gh release create $Manifest.source_tag $Zip $Checksum --repo $Manifest.source_repository --title "날라액션 v1.0.0" --notes-file $SourceNotes
}
if ($LASTEXITCODE -ne 0) { throw "소스 GitHub Release 생성 실패" }

$DistributionNotes = ".\docs\RELEASE_NOTES_v1.0.0.md"
gh release view $Manifest.release_tag --repo $Manifest.release_repository *> $null
if ($LASTEXITCODE -eq 0) {
  gh release upload $Manifest.release_tag $Zip $Checksum --repo $Manifest.release_repository --clobber
} else {
  gh release create $Manifest.release_tag $Zip $Checksum --repo $Manifest.release_repository --title "날라액션 v1.0.0" --notes-file $DistributionNotes --latest
}
if ($LASTEXITCODE -ne 0) { throw "통합 배포 GitHub Release 생성 실패" }

$Release = gh release view $Manifest.release_tag --repo $Manifest.release_repository --json tagName,assets,url | ConvertFrom-Json
$AssetNames = @($Release.assets | ForEach-Object { $_.name })
if ($Release.tagName -ne $Manifest.release_tag) { throw "최종 릴리즈 태그 검증 실패" }
if ($AssetNames -notcontains $Manifest.artifact_name) { throw "정식 ZIP 검증 실패" }
if ($AssetNames -notcontains $Manifest.checksum_name) { throw "정식 SHA-256 검증 실패" }

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "날라액션 v1.0.0 STABLE RELEASE PASS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Source tag: $($Manifest.source_tag)"
Write-Host "Distribution tag: $($Manifest.release_tag)"
Write-Host "SHA-256: $Hash"
Write-Host "Release: $($Release.url)"
Write-Host "EDD version: $($Manifest.edd_version)"
Write-Host "EDD download: https://github.com/Eoingtilab/nalapps-releases/releases/download/$($Manifest.release_tag)/$($Manifest.artifact_name)"
