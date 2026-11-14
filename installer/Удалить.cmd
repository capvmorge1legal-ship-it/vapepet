@echo off
chcp 65001 >nul
taskkill /im VapePet.exe /f >nul 2>&1
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-CimInstance Win32_Process | ? { $_.Name -like 'powershell*' -and $_.CommandLine -match 'vape-pet\.ps1' } | %% { Stop-Process -Id $_.ProcessId -Force }; Remove-Item (Join-Path ([Environment]::GetFolderPath('Startup')) 'VapePet.lnk'), (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Вейп-котики.lnk') -ErrorAction SilentlyContinue; 'Котик закрыт, автозагрузка и ярлык убраны. Папку Документы\VapePet можно удалить вручную.'"
pause
