# Сборка VapePet.exe из src\VapePet.cs (компилятор .NET Framework 4, есть в каждой Windows 10/11)
# Запуск: powershell -ExecutionPolicy Bypass -File build\build.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { 'Не найден csc.exe (.NET Framework 4)'; exit 1 }
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$out = Join-Path $dist 'VapePet.exe'
$args = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/nowarn:0108,0114,0169,0414,0649,0219,0168',
          "/win32icon:$root\build\vapepet.ico", '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
          "/out:$out", "$root\src\VapePet.cs")
$log = & $csc @args 2>&1
if ($LASTEXITCODE -ne 0) { 'ОШИБКА сборки:'; $log; exit 1 }
Copy-Item "$root\installer\*" $dist -Force
"Готово: $out"
