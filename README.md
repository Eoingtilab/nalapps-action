# 날라액션

Windows에서 사용자가 수행한 마우스와 키보드 동작을 기록하고, 포토샵 액션처럼 편집·저장·재생하는 NALLA APPS 자동화 유틸리티입니다.

## 정식 버전

- 버전: `1.0.0`
- 태그: `v1.0.0`
- 플랫폼: Windows 10/11 x64
- 배포 형식: self-contained single-file EXE + ZIP
- 배포 저장소: `Eoingtilab/nalapps-releases`
- EDD 제품명: `날라액션`
- EDD slug: `nalla-action`

## 주요 기능

- 마우스 이동, 좌/우/가운데 버튼, 휠 녹화·재생
- 키보드 일반키 및 Ctrl/Alt/Shift/Win 조합키 녹화·재생
- 녹화 후 저장하지 않고 즉시 실행
- 녹화 시작/중지, 실행/중지 2버튼 제어
- `Ctrl + Shift + F12` 전역 긴급 중지
- 단계 추가, 편집, 삭제, 활성/비활성, 순서 변경
- 텍스트 입력 및 대기 시간 수동 단계
- `.nlaction` 저장/불러오기
- 액션 파일 크기/스키마/단계/입력값 fail-closed 검증
- 원자적 저장 및 미저장 변경사항 경고
- 날라액션 자체 UI 동작 녹화 제외
- NALLA APPS 5초 인트로 및 중앙 실행
- 날라액션 전용 Windows 아이콘

## 빌드

```powershell
cd "$HOME\Desktop\nalapps-action"

git reset --hard HEAD
git pull origin main

Remove-Item ".\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\src\NalaApps.Action.App\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\src\NalaApps.Action.App\obj" -Recurse -Force -ErrorAction SilentlyContinue

& "C:\Program Files\dotnet\dotnet.exe" publish ".\src\NalaApps.Action.App\NalaApps.Action.App.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ".\publish"

if ($LASTEXITCODE -eq 0) {
    Start-Process ".\publish\NalaApps.Action.exe"
}
```

## 정식 릴리즈 자동화

`v1.0.0`과 같은 `v*` 태그가 push되면 GitHub Actions가 Windows 러너에서 Release 빌드, EXE 제품/버전 검증, ZIP 압축, SHA-256 생성을 수행합니다. 정식 배포 시 저장소 Secret `NALAPPS_RELEASE_TOKEN`이 필요하며, 검증된 ZIP과 체크섬을 `Eoingtilab/nalapps-releases` GitHub Release에 업로드합니다.

## 제한 사항

관리자 권한으로 실행된 프로그램을 자동화하려면 날라액션도 같은 권한 수준으로 실행해야 합니다. UAC Secure Desktop, Windows 잠금 화면 등 운영체제가 입력 주입을 차단하는 보안 영역은 자동화할 수 없습니다.

© EoingtiLab Inc.
