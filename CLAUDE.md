# VapePet — заметки для разработки

- Весь код — один файл `src/VapePet.cs` (класс `VapePet : Form`, ~12k строк). Разделы помечены `// =====`.
- Компилятор — `csc.exe` из .NET Framework 4: только **C# 5** (нет `$"..."`, `?.`, `=>`-членов, `out var`, кортежей, `nameof`).
- Проверять сборку можно только на Windows: `build\build.ps1`. После правок обязательно собрать.
- Версия — `AppVersion` в `src/VapePet.cs`; релиз — тег `vX.Y.Z` (workflow `.github/workflows/release.yml`). Котики обновляются из последнего релиза (`VapePet.exe` в ассетах).
- Меню: пункты строятся в `ContextMenuStrip menu` (старый код), показывает их собственное окно `CatMenu`. Новые пункты добавлять в `menu`/`ReorganizeMenu()`; тумблеры — `MakeToggle` (Tag="toggle", меню не закрывается).
- Отрисовка: окно-слой (UpdateLayeredWindow), 30 кадров/с. Погода и панель кэшируются в `hudCache` (`RebuildHud`), перерисовка 2 раза/с или по `HudChanged()`.
- Котик рисуется кодом (GDI+), картинок нет — так и оставляем.
