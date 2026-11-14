# Установка вейп-котика: VapePet.exe в Документы\VapePet, автозагрузка, ярлык на рабочем столе, запуск.
$ErrorActionPreference = 'Stop'
$src = Split-Path -Parent $MyInvocation.MyCommand.Path
$dst = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'VapePet'
Write-Host "Ставлю котика в $dst ..."
function Get-Pets { Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'VapePet.exe' -or ($_.Name -like 'powershell*' -and $_.ProcessId -ne $PID -and $_.CommandLine -match '\\vape-pet\.ps1"?\s*$') } }
Get-Pets | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Milliseconds 900
New-Item -ItemType Directory -Force -Path $dst | Out-Null
$same = (Resolve-Path $src).Path -eq (Resolve-Path $dst).Path
if (-not $same) {
    if (Test-Path "$dst\VapePet.exe") { Copy-Item "$dst\VapePet.exe" ("$dst\VapePet-backup-" + (Get-Date -Format 'yyyy-MM-dd-HHmm') + ".exe") -Force }
    Copy-Item "$src\VapePet.exe" $dst -Force
}
Get-ChildItem $dst -File | Unblock-File
$exe = "$dst\VapePet.exe"
$sh = New-Object -ComObject WScript.Shell
foreach ($lnk in @((Join-Path ([Environment]::GetFolderPath('Startup')) 'VapePet.lnk'), (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Вейп-котики.lnk'))) {
    $s = $sh.CreateShortcut($lnk)
    $s.TargetPath = $exe; $s.Arguments = ''; $s.WorkingDirectory = $dst; $s.IconLocation = "$exe,0"
    $s.Save()
}
Start-Process $exe -WorkingDirectory $dst
Write-Host "Запускаю котика..."
Start-Sleep 5
if (-not (Get-Process VapePet -ErrorAction SilentlyContinue)) {
    Write-Host "Котик не запустился. Подробности: $env:APPDATA\VapePet_crash.log"
    Read-Host 'Нажмите Enter'; exit 1
}
Write-Host ""
Write-Host "Готово! Котик в правом нижнем углу, стоит в автозагрузке, на рабочем столе ярлык 'Вейп-котики'."
Start-Sleep 3
