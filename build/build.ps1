# Сборка VapePet.exe (.NET 10, один файл, ничего ставить на компьютер не нужно)
# Запуск: powershell -ExecutionPolicy Bypass -File build\build.ps1
# Нужен .NET SDK 10: https://dotnet.microsoft.com/download (или переменная DOTNET_ROOT с распакованным SDK)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnet = 'dotnet'
if ($env:DOTNET_ROOT -and (Test-Path (Join-Path $env:DOTNET_ROOT 'dotnet.exe'))) { $dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe' }
$dist = Join-Path $root 'dist'
& $dotnet publish (Join-Path $root 'VapePet.csproj') -c Release -o $dist --nologo -v q
if ($LASTEXITCODE -ne 0) { 'ОШИБКА сборки'; exit 1 }
Get-ChildItem $dist -Exclude 'VapePet.exe' | Remove-Item -Recurse -Force
Copy-Item "$root\installer\*" $dist -Force
"Готово: $dist\VapePet.exe"
