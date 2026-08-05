$ErrorActionPreference = "Stop"

$Dotnet = "C:\Program Files\dotnet\dotnet.exe"
$Project = ".\src\NalaApps.Action.App\NalaApps.Action.App.csproj"
$Publish = ".\publish"

Remove-Item $Publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\src\NalaApps.Action.App\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\src\NalaApps.Action.App\obj" -Recurse -Force -ErrorAction SilentlyContinue

& $Dotnet publish $Project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o $Publish

if ($LASTEXITCODE -eq 0) {
  Start-Process ".\publish\NalaApps.Action.exe"
}
