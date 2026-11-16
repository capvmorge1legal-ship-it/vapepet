# Сборка VapePet.exe (.NET 10, один файл со всем внутри — ставить .NET не нужно)
# Нужен .NET 10 SDK: https://dotnet.microsoft.com/download  (winget install Microsoft.DotNet.SDK.10)
# Запуск: powershell -ExecutionPolicy Bypass -File build\build.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { 'Не найден dotnet (.NET 10 SDK)'; exit 1 }
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$log = & dotnet publish "$root\VapePet.csproj" -c Release -o $dist --nologo 2>&1
if ($LASTEXITCODE -ne 0) { 'ОШИБКА сборки:'; $log | Select-String -Pattern 'error' ; exit 1 }
Get-ChildItem $dist -Filter *.pdb | Remove-Item -Force
Copy-Item "$root\installer\*" $dist -Force
"Готово: $dist\VapePet.exe"
