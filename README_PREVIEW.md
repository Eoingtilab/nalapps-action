# NalaApps Action Preview

현재 `main` 브랜치는 Windows에서 빌드와 UI·액션 파일 저장 흐름을 검증하기 위한 공개 프리뷰입니다.

```powershell
cd "$HOME\Desktop\nalapps-action"
git reset --hard HEAD
git pull origin main
powershell -ExecutionPolicy Bypass -File .\BUILD.ps1
```

빌드 결과:

`publish\NalaApps.Action.exe`
