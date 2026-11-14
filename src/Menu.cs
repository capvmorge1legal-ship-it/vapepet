using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

partial class VapePet
{
    // ---------- тёмное меню ----------
    static Bitmap Ico(string glyph, Color c)
    {
        Bitmap b = new Bitmap(36, 36);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Font f = new Font("Segoe UI Symbol", 27f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush br = new SolidBrush(c))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                g.DrawString(glyph, f, br, new RectangleF(0, 1, 36, 36), sf);
            }
        }
        return b;
    }

    void StyleMenu(ToolStripItemCollection items)
    {
        foreach (ToolStripItem it in items)
        {
            if (!(it is ToolStripLabel) && !(it is ToolStripTextBox)) it.ForeColor = Color.FromArgb(232, 235, 242);
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            if (mi != null && mi.HasDropDownItems)
            {
                mi.DropDown.Font = new Font("Segoe UI", 10f);
                ToolStripDropDownMenu dd = mi.DropDown as ToolStripDropDownMenu;
                if (dd != null) { dd.ImageScalingSize = new Size(20, 20); dd.Padding = new Padding(2, 6, 2, 6); }
                StyleMenu(mi.DropDownItems);
            }
            if (mi != null) mi.Padding = new Padding(4, 3, 4, 3);
        }
    }

    // ================= удобное меню: меньше пунктов, понятные группы, тумблеры =================
    static readonly Color MenuBg = Color.FromArgb(30, 32, 42), MenuHi = Color.FromArgb(58, 48, 40), MenuAc = Color.FromArgb(255, 158, 64);

    // значок: цветной скруглённый квадратик с белым символом
    static Bitmap Badge(string glyph, Color c)
    {
        Bitmap b = new Bitmap(40, 40, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (GraphicsPath p = new GraphicsPath())
            {
                float r = 11, x = 2, y = 2, w = 36, h = 36;
                p.AddArc(x, y, r * 2, r * 2, 180, 90); p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
                p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90); p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure();
                using (LinearGradientBrush br = new LinearGradientBrush(new PointF(0, 2), new PointF(0, 38), Light(c, 0.12f), Dark(c, 0.88f))) g.FillPath(br, p);
            }
            using (Font f = new Font("Segoe UI Symbol", 22f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush br = new SolidBrush(Color.White))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                g.DrawString(glyph, f, br, new RectangleF(0, 1.5f, 40, 40), sf);
            }
        }
        return b;
    }

    // тумблер для «вкл/выкл»
    static Bitmap SwitchBmp(bool on)
    {
        Bitmap b = new Bitmap(40, 40, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(1, 11, 38, 20);
            using (GraphicsPath p = new GraphicsPath()) { p.AddArc(r.X, r.Y, 20, 20, 90, 180); p.AddArc(r.Right - 20, r.Y, 20, 20, 270, 180); p.CloseFigure();
                using (SolidBrush br = new SolidBrush(on ? MenuAc : Color.FromArgb(78, 82, 100))) g.FillPath(br, p); }
            float kx = on ? r.Right - 18 : r.X + 2;
            using (SolidBrush br = new SolidBrush(Color.White)) g.FillEllipse(br, kx, r.Y + 2, 16, 16);
        }
        return b;
    }

    ToolStripLabel MenuHeader(string text)
    {
        ToolStripLabel l = new ToolStripLabel(text.ToUpperInvariant());
        l.ForeColor = Color.FromArgb(140, 146, 166); l.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
        l.Margin = new Padding(10, 6, 4, 1); l.Tag = "header";
        return l;
    }

    // найти пункт по тексту (в том числе внутри подменю) и вынуть его оттуда
    ToolStripItem Take(string text)
    {
        ToolStripItem found = FindItem(menu.Items, text);
        if (found != null && found.Owner != null) found.Owner.Items.Remove(found);
        return found;
    }
    static ToolStripItem FindItem(ToolStripItemCollection items, string text)
    {
        foreach (ToolStripItem it in items)
        {
            if (it.Text == text) return it;
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            if (mi != null && mi.HasDropDownItems) { ToolStripItem r = FindItem(mi.DropDownItems, text); if (r != null) return r; }
        }
        return null;
    }

    void Put(ToolStripMenuItem into, string text, string glyph, Color c, string newText = null)
    {
        ToolStripItem it = Take(text);
        if (it == null) return;
        if (glyph != null) it.Image = Badge(glyph, c);
        if (newText != null) it.Text = newText;
        into.DropDownItems.Add(it);
    }

    // пункт-переключатель: тумблер вместо галочки, меню при нажатии не закрывается
    void PutToggle(ToolStripMenuItem into, string text, string newText = null)
    {
        ToolStripMenuItem it = Take(text) as ToolStripMenuItem;
        if (it == null) return;
        if (newText != null) it.Text = newText;
        MakeToggle(it);
        into.DropDownItems.Add(it);
    }
    void MakeToggle(ToolStripMenuItem it)
    {
        it.Tag = "toggle";
        it.Image = SwitchBmp(it.Checked);
        it.CheckedChanged += delegate { it.Image = SwitchBmp(it.Checked); };
        it.MouseDown += delegate { keepMenuOpen = true; };
    }
    bool keepMenuOpen;

    void KeepOpenOnToggle(ToolStripDropDown dd)
    {
        dd.Closing += delegate (object o, ToolStripDropDownClosingEventArgs e)
        {
            if (keepMenuOpen && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
            keepMenuOpen = false;
        };
    }

    // скруглённые углы у всех выпадающих списков
    static void RoundDrop(ToolStripDropDown dd)
    {
        dd.Opened += delegate
        {
            try
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    float r = 9; Rectangle b = new Rectangle(0, 0, dd.Width, dd.Height);
                    p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
                    p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure();
                    dd.Region = new Region(p);
                }
            }
            catch { }
        };
    }

    void PolishDrop(ToolStripMenuItem mi)
    {
        ToolStripDropDownMenu dd = mi.DropDown as ToolStripDropDownMenu;
        if (dd == null) return;
        dd.ImageScalingSize = new Size(22, 22); dd.Padding = new Padding(4, 6, 4, 6); dd.Font = new Font("Segoe UI", 10.5f);
        dd.ShowCheckMargin = false; dd.ShowImageMargin = true;
        RoundDrop(dd); KeepOpenOnToggle(dd);
        dd.Opened += delegate { foreach (ToolStripItem it in dd.Items) StyleItem(it); };
        // подменю забирает фокус у поля «Спроси котика», иначе наведение мыши в подменю работает с задержкой и без подсветки
        dd.Opened += delegate { try { BeginInvoke((Action)delegate { try { if (dd.Visible) dd.Focus(); } catch { } }); } catch { } };
        dd.Closed += delegate
        {
            try
            {
                BeginInvoke((Action)delegate
                {
                    try
                    {
                        if (menu == null || !menu.Visible) return;
                        foreach (ToolStripItem it in menu.Items) { ToolStripMenuItem t = it as ToolStripMenuItem; if (t != null && t.DropDown.Visible) return; }
                        menu.Focus(); askBox.Focus();
                    }
                    catch { }
                });
            }
            catch { }
        };
    }
    static void StyleItem(ToolStripItem it)
    {
        ToolStripMenuItem mi = it as ToolStripMenuItem;
        if (mi != null) { mi.Padding = new Padding(4, 6, 10, 6); mi.ForeColor = Color.FromArgb(232, 235, 242); }
    }

    void ReorganizeMenu()
    {
        ToolStripMenuItem links = FindItem(menu.Items, "Ссылки") as ToolStripMenuItem;
        ToolStripMenuItem clip = FindItem(menu.Items, "Буфер обмена") as ToolStripMenuItem;
        ToolStripMenuItem scenes = new ToolStripMenuItem("Сценки", Badge("★", Color.FromArgb(150, 110, 226)));
        ToolStripMenuItem tools = new ToolStripMenuItem("Инструменты", Badge("⚒", Color.FromArgb(70, 170, 120)));
        ToolStripMenuItem sets = new ToolStripMenuItem("Настройки", Badge("⚙", Color.FromArgb(110, 118, 140)));

        // --- сценки
        scenes.DropDownItems.Add(MenuHeader("Котик"));
        Put(scenes, "Гулять", "\U0001F43E", Color.FromArgb(236, 140, 60), "Гулять в уголке");
        Put(scenes, "Погулять по экрану", "↔", Color.FromArgb(80, 150, 230));
        Put(scenes, "Затянуться", "☁", Color.FromArgb(120, 130, 160));
        Put(scenes, "Напугать котика", "!", Color.FromArgb(230, 90, 90));
        scenes.DropDownItems.Add(MenuHeader("Сцены"));
        Put(scenes, "Магазин: работать на кассе", "$", Color.FromArgb(70, 170, 100), "Магазин: на кассе");
        Put(scenes, "Диван: приставка и кальян", "♫", Color.FromArgb(80, 150, 230), "Диван: приставка и кальян");
        Put(scenes, "Разложить табак по стеллажу", "▤", Color.FromArgb(190, 140, 90), "Склад: разложить табак");
        Put(scenes, "Вечеринка у бассейна", "♨", Color.FromArgb(60, 180, 220), "Вечеринка у бассейна");
        Put(scenes, "Облава ОМОН", "⚠", Color.FromArgb(220, 70, 70), "Облава ОМОН");
        Put(scenes, "Ревизия от нач. кота", "☑", Color.FromArgb(150, 150, 170), "Ревизия от начальника");
        scenes.DropDownItems.Add(MenuHeader("Отдых"));
        Put(scenes, "Послушать музыку", "♪", Color.FromArgb(230, 180, 60), "Музыка (Яндекс Музыка)");
        Put(scenes, "Смотреть кино", "▶", Color.FromArgb(220, 90, 110), "Кино (Кинопоиск)");

        // --- инструменты
        tools.DropDownItems.Add(MenuHeader("Котик-помощник"));
        tools.DropDownItems.Add(new ToolStripMenuItem("Панель VapePet (погода, VPN, курсы…)", Badge("▦", Color.FromArgb(90, 150, 240)), delegate { BeginInvoke((Action)delegate { BigOpen(); }); }));
        tools.DropDownItems.Add(new ToolStripMenuItem("Спросить про экран   Ctrl+Shift+K", Badge("◉", Color.FromArgb(255, 158, 64)), delegate { BeginInvoke((Action)delegate { ScreenHelper(); }); }));
        tools.DropDownItems.Add(MenuHeader("Время"));
        ToolStripMenuItem timer = Take("Таймер") as ToolStripMenuItem;
        if (timer != null) { timer.Image = Badge("⏱", Color.FromArgb(230, 160, 50)); tools.DropDownItems.Add(timer); }
        Put(tools, "Напоминание…", "⏰", Color.FromArgb(230, 120, 60));
        Put(tools, "Отменить таймеры и напоминания", "✕", Color.FromArgb(150, 150, 170), "Отменить таймеры");
        tools.DropDownItems.Add(MenuHeader("Узнать"));
        Put(tools, "Курс доллара, евро, юаня", "₽", Color.FromArgb(70, 170, 100), "Курс валют (ЦБ)");
        Put(tools, "Проверить скорость", "⇅", Color.FromArgb(60, 160, 200), "Скорость интернета");
        tools.DropDownItems.Add(MenuHeader("ИИ"));
        Put(tools, "Открыть чат", "✉", Color.FromArgb(80, 150, 230), "Чат с котиком");
        Put(tools, "Замолчать", "■", Color.FromArgb(150, 150, 170), "Замолчать");

        // --- настройки
        sets.DropDownItems.Add(MenuHeader("Вид"));
        ToolStripMenuItem size = Take("Размер") as ToolStripMenuItem; if (size != null) { size.Image = Badge("⤢", Color.FromArgb(110, 118, 140)); sets.DropDownItems.Add(size); }
        ToolStripMenuItem wx = Take("Погода") as ToolStripMenuItem; if (wx != null) { wx.Image = Badge("☀", Color.FromArgb(230, 170, 40)); sets.DropDownItems.Add(wx); }
        if (wxShowItem != null) MakeToggle(wxShowItem);
        ToolStripMenuItem mon = new ToolStripMenuItem("Монитор", Badge("▭", Color.FromArgb(80, 150, 230)));
        mon.DropDownOpening += delegate { FillMonitorMenu(mon); }; FillMonitorMenu(mon); sets.DropDownItems.Add(mon);
        ToolStripMenuItem corner = new ToolStripMenuItem("Угол экрана", Badge("◢", Color.FromArgb(150, 110, 226)));
        ToolStripMenuItem cr = new ToolStripMenuItem("Справа", null, delegate { SetPetPlace(petMonitor, false); });
        ToolStripMenuItem cl = new ToolStripMenuItem("Слева", null, delegate { SetPetPlace(petMonitor, true); });
        corner.DropDownItems.Add(cr); corner.DropDownItems.Add(cl);
        corner.DropDownOpening += delegate { cr.Checked = !petLeft; cl.Checked = petLeft; };
        cr.Checked = !petLeft; cl.Checked = petLeft; sets.DropDownItems.Add(corner);
        ToolStripMenuItem fr = Take("Пугливость") as ToolStripMenuItem; if (fr != null) { fr.Image = Badge("!", Color.FromArgb(230, 90, 90)); sets.DropDownItems.Add(fr); }
        ToolStripMenuItem ai = new ToolStripMenuItem("Голос и ИИ", Badge("✉", Color.FromArgb(80, 150, 230)));
        Put(ai, "Настройки ИИ...", null, Color.Empty, "Подключение ИИ…");
        PutToggle(ai, "Озвучивать ответы");
        PutToggle(ai, "Голос: мой (нейросеть)", "Мой голос (нейросеть)");
        Put(ai, "Очистить память разговора", null, Color.Empty, "Очистить текущий разговор");
        ToolStripMenuItem act = new ToolStripMenuItem("Котик выполняет просьбы: таймеры, поиск, открыть, запомнить", null, delegate (object o, EventArgs e) { aiActions = !aiActions; ((ToolStripMenuItem)o).Checked = aiActions; SaveSettings(); });
        act.Checked = aiActions; MakeToggle(act); ai.DropDownItems.Add(act);
        ai.DropDownItems.Add(new ToolStripMenuItem("Память котика: что он знает о вас…", null, delegate { EditMemory(); }));
        sets.DropDownItems.Add(ai);
        ToolStripMenuItem pnl = new ToolStripMenuItem("Панель: интернет, курс, таймеры", null, delegate (object o, EventArgs e) { panelShow = !panelShow; ((ToolStripMenuItem)o).Checked = panelShow; SaveSettings(); });
        pnl.Checked = panelShow; MakeToggle(pnl); sets.DropDownItems.Add(pnl);
        sets.DropDownItems.Add(new ToolStripMenuItem("Токен МойСклад…", null, delegate { MsTokenDialog(); }));
        sets.DropDownItems.Add(MenuHeader("Котик сам"));
        PutToggle(sets, "Играть с окнами и часами", "Гуляет по экрану, играет с окнами");
        PutToggle(sets, "Следить за загрузками браузера", "Следит за загрузками");
        PutToggle(sets, "Убегать от уведомлений", "Убегает от уведомлений");
        PutToggle(sets, "Предупреждать, если пропал", "Предупреждает, если пропал интернет");
        PutToggle(sets, "Показывать скорость над котом", "Показывает скорость сети");
        ToolStripMenuItem hfs = new ToolStripMenuItem("Прячется в играх и фильмах на весь экран", null, delegate (object o, EventArgs e) { hideInFullscreen = !hideInFullscreen; ((ToolStripMenuItem)o).Checked = hideInFullscreen; SaveSettings(); });
        hfs.Checked = hideInFullscreen; MakeToggle(hfs); sets.DropDownItems.Add(hfs);
        sets.DropDownItems.Add(MenuHeader("Версия " + AppVersion));
        ToolStripMenuItem au = new ToolStripMenuItem("Обновляться автоматически", null, delegate (object o, EventArgs e) { autoUpdate = !autoUpdate; ((ToolStripMenuItem)o).Checked = autoUpdate; SaveSettings(); });
        au.Checked = autoUpdate; MakeToggle(au); sets.DropDownItems.Add(au);
        sets.DropDownItems.Add(new ToolStripMenuItem("Проверить обновления", Badge("↻", Color.FromArgb(80, 150, 230)), delegate { CheckUpdate(true); }));

        // --- пустые старые группы убрать
        foreach (string old in new string[] { "ИИ-ассистент", "Котик", "Полезное", "Интернет", "Отдых", "Сценарии" })
        { ToolStripItem o = FindItem(menu.Items, old); if (o != null && o.Owner == menu) menu.Items.Remove(o); }
        // лишние разделители подряд
        for (int i = menu.Items.Count - 1; i > 0; i--)
            if (menu.Items[i] is ToolStripSeparator && (menu.Items[i - 1] is ToolStripSeparator || i == menu.Items.Count - 1 || menu.Items[i + 1] is ToolStripSeparator)) menu.Items.RemoveAt(i);

        // --- порядок верхнего уровня
        int at = menu.Items.IndexOf(askBox) + 1;
        ToolStripLabel stHead = new ToolStripLabel("Остаток в МойСклад — название и Enter");
        stHead.ForeColor = Color.FromArgb(120, 200, 140); stHead.Font = new Font("Segoe UI", 9f, FontStyle.Bold); stHead.Image = Badge("⌕", Color.FromArgb(70, 170, 100));
        ToolStripTextBox stBox = new ToolStripTextBox();
        stBox.AutoSize = false; stBox.Size = new Size(300, 28); stBox.BackColor = Color.FromArgb(56, 60, 76); stBox.ForeColor = Color.White;
        stBox.BorderStyle = BorderStyle.FixedSingle; stBox.Font = new Font("Segoe UI", 10.5f); stBox.Margin = new Padding(6, 2, 6, 4);
        stBox.KeyDown += delegate (object o, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true; string q = stBox.Text.Trim(); stBox.Text = ""; menu.Close();
            if (q.Length > 0) BeginInvoke((Action)delegate { StockSearch(q); });
        };
        menu.Items.Insert(at++, stHead); menu.Items.Insert(at++, stBox);
        if (links != null) { menu.Items.Remove(links); links.Image = Badge("↗", Color.FromArgb(80, 150, 230)); menu.Items.Insert(at++, links); }
        if (clip != null) { menu.Items.Remove(clip); clip.Image = Badge("⎘", Color.FromArgb(150, 110, 226)); menu.Items.Insert(at++, clip); }
        menu.Items.Insert(at++, new ToolStripSeparator());
        menu.Items.Insert(at++, scenes);
        menu.Items.Insert(at++, tools);
        settingsRoot = sets;
        menu.Items.Insert(at++, new ToolStripMenuItem("Настройки…", Badge("⚙", Color.FromArgb(110, 118, 140)), delegate { OpenSettings(null); }));
        while (at < menu.Items.Count && menu.Items[at] is ToolStripSeparator) menu.Items.RemoveAt(at);
        // всё остальное, кроме «Выход», — в настройки (на случай пунктов, о которых меню не знает)
        for (int i = menu.Items.Count - 1; i >= at; i--)
        {
            ToolStripItem it = menu.Items[i];
            if (it.Text == "Выход" || it is ToolStripSeparator) continue;
            menu.Items.RemoveAt(i); sets.DropDownItems.Add(it);
        }
        ToolStripItem exit = FindItem(menu.Items, "Выход");
        if (exit != null) { menu.Items.Remove(exit); exit.Image = Badge("✕", Color.FromArgb(200, 70, 70)); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(exit); }
        for (int i = menu.Items.Count - 2; i > 0; i--) if (menu.Items[i] is ToolStripSeparator && menu.Items[i - 1] is ToolStripSeparator) menu.Items.RemoveAt(i);
        while (menu.Items.Count > 0 && menu.Items[0] is ToolStripSeparator) menu.Items.RemoveAt(0);

        // --- оформление
        menu.Font = new Font("Segoe UI", 10.5f); menu.ImageScalingSize = new Size(22, 22); menu.Padding = new Padding(4, 8, 4, 8);
        RoundDrop(menu); KeepOpenOnToggle(menu);
        PolishAll(menu.Items);
        // подменю с размерами/пугливостью/таймером — тоже со скруглением и отступами
        foreach (ToolStripMenuItem dyn in new ToolStripMenuItem[] { links, clip })
            if (dyn != null) dyn.DropDownOpened += delegate { foreach (ToolStripItem it in dyn.DropDownItems) { StyleItem(it); ToolStripMenuItem t = it as ToolStripMenuItem; if (t != null && t.Text == "Запоминать копирования" && !(t.Tag is string)) MakeToggle(t); } };
    }

    ToolStripMenuItem suppressOpen; DateTime menuKeepUntil = DateTime.MinValue;
    void ClickToggles(ToolStripMenuItem mi)
    {
        // WinForms сам открывает подменю при нажатии, поэтому закрываем только то,
        // что было открыто ещё до клика (наведением или прошлым кликом)
        int openedAt = 0;
        mi.DropDownOpened += delegate
        {
            openedAt = Environment.TickCount;
            if (suppressOpen == mi) { try { BeginInvoke((Action)delegate { if (suppressOpen == mi) mi.HideDropDown(); }); } catch { } }
        };
        mi.MouseDown += delegate (object o, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (mi.DropDown.Visible && unchecked(Environment.TickCount - openedAt) > 350) { suppressOpen = mi; mi.HideDropDown(); }
        };
        mi.MouseLeave += delegate { if (suppressOpen == mi) suppressOpen = null; };
    }

    void PolishAll(ToolStripItemCollection items)
    {
        foreach (ToolStripItem it in items)
        {
            StyleItem(it);
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            if (mi != null && (mi.HasDropDownItems || mi.DropDown.Items.Count > 0)) { PolishDrop(mi); ClickToggles(mi); PolishAll(mi.DropDownItems); }
        }
    }

    class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return MenuBg; } }
        public override Color ImageMarginGradientBegin { get { return MenuBg; } }
        public override Color ImageMarginGradientMiddle { get { return MenuBg; } }
        public override Color ImageMarginGradientEnd { get { return MenuBg; } }
        public override Color ImageMarginRevealedGradientBegin { get { return MenuBg; } }
        public override Color ImageMarginRevealedGradientMiddle { get { return MenuBg; } }
        public override Color ImageMarginRevealedGradientEnd { get { return MenuBg; } }
        public override Color ToolStripGradientBegin { get { return MenuBg; } }
        public override Color ToolStripGradientMiddle { get { return MenuBg; } }
        public override Color ToolStripGradientEnd { get { return MenuBg; } }
        public override Color MenuBorder { get { return Color.FromArgb(64, 68, 86); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return MenuHi; } }
        public override Color MenuItemSelectedGradientBegin { get { return MenuHi; } }
        public override Color MenuItemSelectedGradientEnd { get { return MenuHi; } }
        public override Color MenuItemPressedGradientBegin { get { return MenuHi; } }
        public override Color MenuItemPressedGradientEnd { get { return MenuHi; } }
        public override Color SeparatorDark { get { return Color.FromArgb(52, 56, 72); } }
        public override Color SeparatorLight { get { return MenuBg; } }
        public override Color CheckBackground { get { return Color.Transparent; } }
        public override Color CheckSelectedBackground { get { return Color.Transparent; } }
        public override Color CheckPressedBackground { get { return Color.Transparent; } }
    }

    class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (!(e.ToolStrip is ToolStripDropDown)) { base.OnRenderToolStripBorder(e); return; }
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle b = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (GraphicsPath p = new GraphicsPath())
            {
                float r = 9;
                p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
                p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure();
                using (Pen pen = new Pen(Color.FromArgb(70, 74, 94), 1.4f)) g.DrawPath(pen, p);
            }
        }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(MenuBg)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown) { using (SolidBrush b = new SolidBrush(MenuBg)) e.Graphics.FillRectangle(b, 0, 0, e.ToolStrip.Width, e.ToolStrip.Height); return; }
            base.OnRenderToolStripBackground(e);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            using (GraphicsPath p = new GraphicsPath())
            {
                float rr = 7;
                p.AddArc(r.X, r.Y, rr * 2, rr * 2, 180, 90); p.AddArc(r.Right - rr * 2, r.Y, rr * 2, rr * 2, 270, 90);
                p.AddArc(r.Right - rr * 2, r.Bottom - rr * 2, rr * 2, rr * 2, 0, 90); p.AddArc(r.X, r.Bottom - rr * 2, rr * 2, rr * 2, 90, 90); p.CloseFigure();
                using (SolidBrush b = new SolidBrush(MenuHi)) g.FillPath(b, p);
            }
            using (SolidBrush b = new SolidBrush(MenuAc)) g.FillRectangle(b, r.X + 1, r.Y + 6, 3, Math.Max(2, r.Height - 12));
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(Color.FromArgb(52, 56, 72))) e.Graphics.DrawLine(p, 12, y, e.Item.Width - 12, y);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item != null && e.Item.Selected ? MenuAc : Color.FromArgb(130, 136, 156);
            base.OnRenderArrow(e);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem) e.TextColor = !e.Item.Enabled ? Color.FromArgb(120, 126, 146) : (e.Item.Selected ? Color.FromArgb(255, 214, 170) : Color.FromArgb(232, 235, 242));
            base.OnRenderItemText(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item.Tag is string && (string)e.Item.Tag == "toggle") return;
            // выбранный вариант (размер, пугливость): оранжевая галочка
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle;
            using (Pen p = new Pen(MenuAc, 2.6f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.DrawLines(p, new PointF[] { new PointF(r.X + r.Width * 0.2f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.74f), new PointF(r.X + r.Width * 0.82f, r.Y + r.Height * 0.28f) }); }
        }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            ToolStripMenuItem mi = e.Item as ToolStripMenuItem;
            if (mi != null && mi.Checked && mi.Image != null && mi.Tag is string && (string)mi.Tag == "toggle")
            { e.Graphics.DrawImage(mi.Image, e.ImageRectangle); return; }
            base.OnRenderItemImage(e);
        }
    }

    void AiSettings()
    {
        using (Form f = new Form())
        {
            f.Text = "Настройки ИИ-котика"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterScreen;
            f.MinimizeBox = false; f.MaximizeBox = false; f.TopMost = true; f.ClientSize = new Size(460, 250); f.ShowInTaskbar = false;
            Label l1 = new Label(); l1.Text = "Сервис:"; l1.SetBounds(12, 16, 110, 20);
            ComboBox cb = new ComboBox(); cb.DropDownStyle = ComboBoxStyle.DropDownList; cb.Items.AddRange(new object[] { "Claude (Anthropic)", "OpenAI-совместимый", "Google Gemini (бесплатно)", "Groq (бесплатно)" });
            cb.SelectedIndex = aiFormat == "anthropic" ? 0 : (aiUrl.Contains("generativelanguage.googleapis.com") ? 2 : aiUrl.Contains("api.groq.com") ? 3 : 1); cb.SetBounds(130, 12, 316, 24);
            Label l2 = new Label(); l2.Text = "Адрес API:"; l2.SetBounds(12, 52, 110, 20);
            TextBox tu = new TextBox(); tu.Text = aiUrl; tu.SetBounds(130, 48, 316, 24);
            Label l3 = new Label(); l3.Text = "Модель:"; l3.SetBounds(12, 88, 110, 20);
            TextBox tm = new TextBox(); tm.Text = aiModel; tm.SetBounds(130, 84, 316, 24);
            Label l4 = new Label(); l4.Text = "Ключ API:"; l4.SetBounds(12, 124, 110, 20);
            TextBox tk = new TextBox(); tk.Text = aiKey; tk.UseSystemPasswordChar = true; tk.SetBounds(130, 120, 316, 24);
            Label hint = new Label(); hint.Text = "Ключ хранится только на этом компьютере.\nGroq / Gemini: бесплатный ключ, «Получить ключ» внизу (нужен VPN)."; hint.ForeColor = Color.DimGray; hint.SetBounds(12, 154, 434, 36);
            cb.SelectedIndexChanged += delegate
            {
                if (cb.SelectedIndex == 0) { tu.Text = "https://api.anthropic.com/v1/messages"; tm.Text = "claude-haiku-5-5"; }
                else if (cb.SelectedIndex == 2) { tu.Text = GeminiUrl; tm.Text = "gemini-3.8-flash"; }
                else if (cb.SelectedIndex == 3) { tu.Text = GroqUrl; tm.Text = "openai/gpt-oss-120b"; }
                if (cb.SelectedIndex >= 2) tk.Text = "";
                else { tu.Text = "https://api.openai.com/v1/chat/completions"; tm.Text = "gpt-4o-mini"; }
            };
            LinkLabel getKey = new LinkLabel(); getKey.Text = "Получить ключ"; getKey.SetBounds(12, 210, 200, 20);
            getKey.LinkClicked += delegate { try { ShellOpen(cb.SelectedIndex == 3 ? "https://console.groq.com/keys" : cb.SelectedIndex == 0 ? "https://platform.claude.com" : "https://aistudio.google.com/apikey"); } catch { } };
            Button ok = new Button(); ok.Text = "Сохранить"; ok.DialogResult = DialogResult.OK; ok.SetBounds(262, 206, 90, 28);
            Button cn = new Button(); cn.Text = "Отмена"; cn.DialogResult = DialogResult.Cancel; cn.SetBounds(358, 206, 88, 28);
            f.Controls.AddRange(new Control[] { l1, cb, l2, tu, l3, tm, l4, tk, hint, getKey, ok, cn });
            f.AcceptButton = ok; f.CancelButton = cn;
            if (f.ShowDialog() != DialogResult.OK) return;
            aiFormat = cb.SelectedIndex == 0 ? "anthropic" : "openai";
            aiUrl = tu.Text.Trim(); aiModel = tm.Text.Trim(); aiKey = tk.Text.Trim();
            SaveSettings();
            Say(aiKey.Length > 0 ? "Мур! Теперь я умный" : "Ключ убран", me.X, CatTop(me) - U(1), 'U');
            me.HappyT = 50;
        }
    }

    void UpdateAI()
    {
        if (aiBusy && tick % 24 == 0)
        {
            PointF h = Local(me, 2, -27);
            Particle p = new Particle(); p.Kind = 9; p.X = h.X + U((float)rnd.NextDouble() * 4 - 2); p.Y = h.Y; p.VY = -0.25f * u; p.Max = 40; p.Col = '?';
            parts.Add(p);
            me.BlinkT = 4;
        }
    }

    // ---- эффекты ----
    void SpawnSmoke(Cat c)
    {
        PointF from = c.Vape ? Local(c, 8.0f, -14.9f) : Local(c, 3.6f, -15.4f);
        Particle p = new Particle();
        p.Kind = 0; p.X = from.X; p.Y = from.Y;
        p.VX = c.Dir * (0.4f + (float)rnd.NextDouble() * 0.8f) * u / 3f;
        p.VY = -(0.5f + (float)rnd.NextDouble() * 0.8f) * u / 3f;
        p.R = (1.0f + (float)rnd.NextDouble() * 1.4f) * u;
        p.Max = 50 + rnd.Next(40);
        parts.Add(p);
    }

    void Poof(float x, float y)
    {
        for (int i = 0; i < 10; i++)
        {
            Particle p = new Particle();
            p.Kind = 0; p.X = x; p.Y = y;
            double a = rnd.NextDouble() * Math.PI * 2;
            p.VX = (float)Math.Cos(a) * 0.5f * u; p.VY = (float)Math.Sin(a) * 0.4f * u - 0.15f * u;
            p.R = U(2f); p.Max = 30 + rnd.Next(15);
            parts.Add(p);
        }
    }

    void Splash(float x)
    {
        for (int i = 0; i < 14; i++)
        {
            Particle p = new Particle();
            p.Kind = 1; p.X = x + (float)(rnd.NextDouble() - 0.5) * U(8); p.Y = Ground - U(11);
            p.VX = (float)(rnd.NextDouble() - 0.5) * u; p.VY = -(0.7f + (float)rnd.NextDouble()) * u;
            p.R = U(0.5f + (float)rnd.NextDouble() * 0.6f);
            p.Max = 28; p.Col = rnd.Next(2) == 0 ? 'u' : 'w';
            parts.Add(p);
        }
    }

    void Fly(char kind, char col, float x0, float y0, float x1, float y1, int dur, Action done)
    {
        Flyer f = new Flyer();
        f.Kind = kind; f.Col = col; f.X0 = x0; f.Y0 = y0; f.X1 = x1; f.Y1 = y1; f.Dur = dur; f.Done = done;
        flyers.Add(f);
    }

    void Say(string t, float x, float y, char col)
    {
        // кот гуляет по окнам — его слова говорит он сам, а не пустой уголок
        if (roaming && me.Hidden && Math.Abs(x - me.X) < U(30)) { rSay = t; rSayT = 50 + t.Length * 2; return; }
        foreach (FText o in texts)
            if (Math.Abs(o.Y - y) < U(7) && Math.Abs(o.X - x) < U(14) + (o.Text.Length + t.Length) * U(1.1f)) o.Y -= U(7);
        FText f = new FText();
        f.Text = t; f.X = x; f.Y = y; f.Max = 60 + t.Length * 2; f.Col = col;
        texts.Add(f);
    }

    // точка в "координатах котика" (смотрит вправо, 0 - под лапами, y вверх отрицательный)
    PointF Local(Cat c, float lx, float ly)
    {
        float off = (c.Sitting || c.Swimming) ? 3.5f : 0, k = c.Scale * ZK(c.Z);
        return new PointF(c.X + c.Dir * lx * u * k, Ground + ZY(c.Z) - c.Y + (ly + off) * u * k);
    }
    float CatTop(Cat c) { if (c.Lying) return Ground + ZY(c.Z) - c.Y - U(10) * c.Scale * ZK(c.Z); return Local(c, 0, -26f).Y; }
    PointF Paw(Cat c) { return Local(c, 4.8f, -8.8f); }

    float Appear(int i)
    {
        float a = (enterT - i * 10) / 15f;
        if (pendingMode >= 0) a = Math.Min(1f, leaveT / 20f);
        if (a < 0) a = 0; if (a > 1) a = 1;
        return 1 - (1 - a) * (1 - a);
    }
}
