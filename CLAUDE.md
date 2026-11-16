# VapePet — заметки для разработки

- Код — `partial class VapePet : Form`, разложен по файлам в `src/`:
  `VapePet.cs` (поля, конструктор, размер/место окна), `Logic.cs` (главный цикл, сцены), `Scenes.cs` (сценарии: ОМОН, склад, ревизия, гости, очередь),
  `Weather.cs`, `Ai.cs` (ИИ, голос), `Menu.cs` (сборка пунктов меню), `CatMenu.cs` (окно меню и поиск), `Settings.cs` (окно настроек),
  `Drawing.cs` (весь рисунок котика и сцен), `Window.cs` (прозрачное окно, защита, мышь, запуск), `Roam.cs` (прогулка по экрану),
  `Life.cs` (реакции, испуг), `Useful.cs` (загрузки, таймеры, курс, буфер, интернет, панель), `SmartClip.cs`, `Stock.cs` (МойСклад),
  `BigPanel.cs`, `System.cs` (полноэкранный режим, экономия кадров, обновления, версия), `Program.cs` (Main).
- Проект — `VapePet.csproj`: **.NET 10** (`net10.0-windows`, WinForms + GDI+), современный C#. Публикуется одним самодостаточным exe (`dotnet publish`, ставить .NET не нужно, ~50 МБ).
- Сборка: `build\build.ps1` (нужен .NET 10 SDK) или GitHub Actions: ветки — workflow `build` (артефакт + пре-релиз `test-<ветка>`, в автообновление не попадает), main — `release`. После правок обязательно собрать.
- Ссылки/файлы открывать через `ShellOpen(...)` (в .NET 10 `Process.Start(строка)` не открывает URL). Windows-1251 доступна после `Encoding.RegisterProvider` в `Start()`.
- Версия — `AppVersion` в `src/System.cs`; релиз выпускается сам при пуше в main с новой версией (workflow `.github/workflows/release.yml`). Котики обновляются из последнего релиза (`VapePet.exe` в ассетах).
- Меню: пункты строятся в `ContextMenuStrip menu` (`Menu.cs`), показывает их окно `CatMenu`. Настройки — подменю `settingsRoot`, его показывает окно `SettingsWin`: заголовки → вкладки, тумблеры (`MakeToggle`, Tag="toggle") → переключатели, подменю с одним отмеченным вариантом → кнопки выбора. Новую настройку достаточно добавить пунктом в `settingsRoot`.
- Ключи (`ai_key`, `ms_token`) в `%APPDATA%\VapePet.txt` шифруются DPAPI (`Seal`/`Unseal`).
- Отрисовка: окно-слой (UpdateLayeredWindow), 30 кадров/с, в покое 10. Погода и панель кэшируются в `hudCache` (`RebuildHud`), перерисовка 2 раза/с или по `HudChanged()`. При полноэкранной игре/видео окно прячется (`FullscreenCheck`).
- Котик рисуется кодом (GDI+), картинок нет — так и оставляем.
