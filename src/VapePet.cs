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

public partial class VapePet : Form
{
    static readonly char[] ItemColors = new char[] { 'R', 'Y', 'N', 'U', 'L', 'P' };
    static readonly string[] Phrases = new string[] { "Мне клубнику!", "Есть арбуз?", "Одну мятную!", "Самую холодную!", "Как обычно!", "Что новенького?" };
    static readonly int[] Prices = new int[] { 350, 450, 500, 590, 650 };
    static readonly Color[,] CatColors = new Color[,] {
        { Color.FromArgb(165,168,180), Color.FromArgb(238,238,244) },
        { Color.FromArgb(248,246,242), Color.FromArgb(255,222,210) },
        { Color.FromArgb(78,74,88),    Color.FromArgb(170,164,178) },
        { Color.FromArgb(242,208,155), Color.FromArgb(255,246,228) },
        { Color.FromArgb(172,110,66),  Color.FromArgb(242,220,192) },
        { Color.FromArgb(205,178,228), Color.FromArgb(250,242,255) },
        { Color.FromArgb(226,116,48),  Color.FromArgb(252,232,205) }
    };
    static readonly float[] SizeSteps = new float[] { 1.35f, 1.8f, 2.5f };
    static readonly string[] SizeNames = new string[] { "Маленький", "Средний", "Крупный" };

    class Cat
    {
        public float X, Y, TX, Speed = 1.4f; public float Z, TZ; public bool ZMove; public int View = 1; public int LandT, TurnT, LastDir = 1, LastView = 1;
        public int Dir = 1, Id, Phase, T, PuffT, BlinkT;
        public bool Walking, Sitting, Swimming, Vape, Jumping, Hidden, Boss, Stiff, Impatient;
        public Color IrisC = Color.FromArgb(120, 190, 80); public bool Stripes; public float ItemRot;
        public int Outfit, Hat; public Color C1 = Color.Gray, C2 = Color.DimGray, HatC = Color.Red; public bool HatBack, Glasses, Lying, Cuffed, Bald, Chain, SmallShades; public int DialogV = -1;
        public float Patience = 100, Temper = 1; public int ActT = 60, RageHits, LookT;
        public float Scale = 1f; public int Shake, Script, AngryT, StompT, HappyT, SweatT;
        public char Hold = ' ', HoldCol = 'R';
        public Color MainC, BellyC;
        public float JX0, JX1, JY0, JY1, JH; public int JT, JDur; public Action JDone;
        public Cat Clone() { return (Cat)MemberwiseClone(); }
    }
    // Личность и текущее состояние питомца. Значения 0..100.
    class PetBrain
    {
        public float Curiosity = 65f, Playfulness = 60f, Sociability = 50f;
        public float Laziness = 45f, Timidity = 25f;
        public float Affection = 20f, Boredom = 10f, Energy = 85f;
        public int Mood; // 0 спокойно, 1 радость, 2 сонливость, 3 интерес, 4 скука
        public int ThinkTick, ActionCool, HoverTicks, LastSaveTick;
    }
    class Particle { public int Kind; public float X, Y, VX, VY, R; public int Life, Max; public char Col; }
    class Flyer { public char Kind; public char Col; public float X0, Y0, X1, Y1; public int T, Dur; public Action Done; }
    class FText { public string Text; public float X, Y; public int Life, Max; public char Col; }

    const float AreaL = 0.655f, AreaR = 0.996f;
    float dpi, u, uw;
    int sizeIdx = 1;
    int HH, Ground;
    float U(float n) { return n * u; }
    float W(float n) { return n * uw; }

    System.Windows.Forms.Timer timer;
    Random rnd = new Random();
    Dictionary<char, Color> pal = new Dictionary<char, Color>();
    Font fnt, fntSign, fntTiny, fntWxT, fntWxD, fntWxS;
    ContextMenuStrip menu;
    ToolStripMenuItem[] sizeItems;
    Bitmap buf;

    Cat me;
    List<Cat> guests = new List<Cat>();
    List<Particle> parts = new List<Particle>();
    List<Flyer> flyers = new List<Flyer>();
    List<FText> texts = new List<FText>();
    int tick, catIds = 1;
    PetBrain petBrain = new PetBrain();

    // режимы: 0 гуляет, 1 магазин, 2 диван, 3 табак, 4 вечеринка
    int mode = 0, pendingMode = -1, leaveT, enterT;
    float A;

    int lastDialog = -1;
    int spawnT, regFlash; bool bottleOnReg, devOnReg, cardOnReg, digging; char regCol; int lastScript = -1;
    List<Func<bool>> script; int scriptIdx;
    bool caseVisible, consoleOn; int tvT;
    List<float[]> pile = new List<float[]>();
    char[] slots = new char[15]; bool[] slotRes = new bool[15];
    int NextFreeSlot() { for (int i = 0; i < 15; i++) if (slots[i] == ' ' && !slotRes[i]) return i; return -1; }
    int doorOpen, guestsSpawned; bool loungerBusy;

    bool mouseDown, dragging; Point downPt; float dragOffset;

    // ключи ИИ и МойСклад храним зашифрованными защитой Windows (DPAPI): прочитать может только эта учётная запись на этом ПК
    bool keysPlain;
    static string Seal(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        try { return "dpapi:" + Convert.ToBase64String(System.Security.Cryptography.ProtectedData.Protect(Encoding.UTF8.GetBytes(v), Encoding.ASCII.GetBytes("VapePet"), System.Security.Cryptography.DataProtectionScope.CurrentUser)); }
        catch { return v; }
    }
    static string Unseal(string v)
    {
        if (v == null || !v.StartsWith("dpapi:")) return v ?? "";
        try { return Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(v.Substring(6)), Encoding.ASCII.GetBytes("VapePet"), System.Security.Cryptography.DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }

    // ---- где живёт котик: монитор и угол ----
    string petMonitor = ""; bool petLeft;
    Screen PetScreen
    {
        get
        {
            if (petMonitor.Length > 0) foreach (Screen sc in Screen.AllScreens) if (sc.DeviceName == petMonitor) return sc;
            return Screen.PrimaryScreen;
        }
    }
    public void SetPetPlace(string monitor, bool left)
    {
        petMonitor = monitor ?? ""; petLeft = left; SaveSettings();
        try { if (roaming) EndRoam(); } catch { }
        ResetRendering("смена места"); hudDirty = true;
        parts.Clear(); texts.Clear(); SetMode(mode);
    }
    void FillMonitorMenu(ToolStripMenuItem mon)
    {
        mon.DropDownItems.Clear();
        Screen cur = PetScreen; int n = 1;
        foreach (Screen sc in Screen.AllScreens)
        {
            string name = (sc.Primary ? "Основной" : "Монитор " + (++n)) + " · " + sc.Bounds.Width + "×" + sc.Bounds.Height;
            string dev = sc.Primary ? "" : sc.DeviceName;
            ToolStripMenuItem it = new ToolStripMenuItem(name, null, delegate { SetPetPlace(dev, petLeft); });
            it.Checked = sc.DeviceName == cur.DeviceName;
            mon.DropDownItems.Add(it);
        }
    }
    float HudX(float w) { return petLeft ? W(2) : Width - w - W(2); }

    static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet.txt"); } }

    public VapePet(float dpiValue)
    {
        dpi = dpiValue;
        LoadSettings();
        if (!linksSaved) DefaultLinks();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        pal['K'] = Color.FromArgb(70, 50, 42); pal['W'] = Color.FromArgb(250, 242, 228);
        pal['P'] = Color.FromArgb(255, 135, 160); pal['G'] = Color.FromArgb(72, 77, 92);
        pal['g'] = Color.FromArgb(150, 156, 170); pal['B'] = Color.FromArgb(180, 130, 90);
        pal['b'] = Color.FromArgb(128, 90, 62); pal['R'] = Color.FromArgb(230, 96, 96);
        pal['Y'] = Color.FromArgb(246, 198, 86); pal['N'] = Color.FromArgb(118, 186, 112);
        pal['U'] = Color.FromArgb(86, 140, 214); pal['u'] = Color.FromArgb(136, 200, 255);
        pal['L'] = Color.FromArgb(176, 128, 210); pal['w'] = Color.FromArgb(238, 238, 244);
        pal['T'] = Color.FromArgb(26, 30, 42); pal['E'] = Color.FromArgb(250, 230, 196);
        pal['D'] = Color.FromArgb(196, 64, 70); pal['n'] = Color.FromArgb(40, 150, 70);
        pal['O'] = Color.FromArgb(236, 156, 88);

        ApplySize();

        me = new Cat();
        me.Id = 0; me.Vape = true;
        me.MainC = pal['O']; me.BellyC = pal['W']; me.Stripes = true; me.IrisC = Color.FromArgb(130, 200, 70); me.Outfit = 1; me.C1 = Color.FromArgb(40, 42, 56);
        me.X = Width / 3;

        ToolStripManager.Renderer = new DarkMenuRenderer();
        menu = new ContextMenuStrip();
        menu.Font = new Font("Segoe UI", 10f);
        menu.ShowImageMargin = true; menu.ImageScalingSize = new Size(20, 20);
        menu.Padding = new Padding(2, 6, 2, 6);

        ToolStripLabel askHead = new ToolStripLabel("Спроси котика — Enter, и он ответит");
        askHead.ForeColor = Color.FromArgb(255, 170, 80); askHead.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        askHead.Image = Ico("\U0001F43E", Color.FromArgb(255, 170, 80));
        menu.Items.Add(askHead);
        askBox = new ToolStripTextBox();
        askBox.AutoSize = false; askBox.Size = new Size(300, 28);
        askBox.BackColor = Color.FromArgb(56, 60, 76); askBox.ForeColor = Color.White;
        askBox.BorderStyle = BorderStyle.FixedSingle; askBox.Font = new Font("Segoe UI", 10.5f);
        askBox.Margin = new Padding(6, 2, 6, 4);
        askBox.KeyDown += delegate (object o, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string q = askBox.Text.Trim(); askBox.Text = "";
                menu.Close();
                // вопрос про наличие в любой форме: «есть ли у нас хаски?», «сколько осталось эльфбаров на 88км»
                if (Regex.IsMatch(q, @"остат|в\s+наличии|на\s+склад|сколько\s+(?:осталось|есть|штук|шт)|есть\s+ли|есть\s+у\s+нас", RegexOptions.IgnoreCase))
                {
                    string item = Regex.Replace(q, @"(?i)\b(?:какой|какие|какая|какое|остаток|остатки|остатков|в\s+наличии|на\s+складах?|сколько|осталось|есть\s+ли|есть|у\s+нас|штук|шт|ещё|еще|пожалуйста|котик|покажи|глянь|скажи|по|у|ли)\b|[?!,]|\.(?!\d)", " ");
                    item = Regex.Replace(item, @"\s+", " ").Trim();
                    StockSearch(item.Length >= 2 ? item : q);
                }
                else if (q.Length > 0) AskAI(q, true);
            }
        };
        menu.Items.Add(askBox);
        // смена раскладки (Alt+Shift, Ctrl+Shift, Win+Пробел) не должна закрывать меню
        menu.Closing += delegate (object o, ToolStripDropDownClosingEventArgs e)
        {
            bool esc = (GetAsyncKeyState(0x1B) & 0x8000) != 0;
            bool mouse = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
            if ((e.CloseReason == ToolStripDropDownCloseReason.Keyboard && !esc) || (e.CloseReason == ToolStripDropDownCloseReason.AppFocusChange && !mouse && !esc))
            {
                if (menuKeepUntil < DateTime.Now) menuKeepUntil = DateTime.Now.AddSeconds(30);
                e.Cancel = true;
                BeginInvoke((Action)delegate { try { SetForegroundWindow(menu.Handle); askBox.Focus(); } catch { } });
            }
        };
        menu.Closed += delegate { menuKeepUntil = DateTime.MinValue; };
        // меню открылось — сразу можно печатать (окно котика не активное, поэтому забираем фокус явно)
        menu.Opened += delegate
        {
            BeginInvoke((Action)delegate
            {
                try { SetForegroundWindow(menu.Handle); menu.Focus(); askBox.Focus(); askBox.TextBox.Focus(); askBox.SelectAll(); } catch { }
            });
        };

        ToolStripMenuItem aiMenu = new ToolStripMenuItem("ИИ-ассистент", Ico("\U0001F4AC", Color.FromArgb(120, 190, 255)));
        aiMenu.DropDownItems.Add("Открыть чат", Ico("\U0001F4AC", Color.FromArgb(120, 190, 255)), delegate { OpenChat(); });
        aiMenu.DropDownItems.Add("Настройки ИИ...", Ico("\u2699", Color.FromArgb(190, 196, 210)), delegate { AiSettings(); });
        voiceItem = new ToolStripMenuItem("Озвучивать ответы", Ico("\U0001F50A", Color.FromArgb(84, 200, 104)), delegate { aiVoice = !aiVoice; voiceItem.Checked = aiVoice; if (!aiVoice) StopSpeak(); SaveSettings(); });
        voiceItem.Checked = aiVoice;
        aiMenu.DropDownItems.Add(voiceItem);
        cloneItem = new ToolStripMenuItem("Голос: мой (нейросеть)", Ico("\U0001F3A4", Color.FromArgb(255, 170, 80)), delegate { cloneVoice = !cloneVoice; cloneItem.Checked = cloneVoice; SaveSettings(); if (cloneVoice) EnsureVoiceServer(true); });
        cloneItem.Checked = cloneVoice;
        aiMenu.DropDownItems.Add(cloneItem);
        aiMenu.DropDownItems.Add("Замолчать", Ico("\U0001F507", Color.FromArgb(190, 196, 210)), delegate { StopSpeak(); });
        aiMenu.DropDownItems.Add("Очистить память разговора", Ico("\u2716", Color.FromArgb(255, 110, 110)), delegate { aiHistory.Clear(); if (chatLog != null) chatLog.Clear(); Say("Всё забыл. Мяу?", me.X, CatTop(me) - U(1), 'U'); });
        menu.Items.Add(aiMenu);
        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem catMenu = new ToolStripMenuItem("Котик", Ico("\U0001F408", Color.FromArgb(255, 158, 64)));
        catMenu.DropDownItems.Add("Гулять", Ico("\U0001F43E", Color.FromArgb(255, 158, 64)), delegate { SetMode(0); });
        catMenu.DropDownItems.Add("Затянуться", Ico("\u2601", Color.FromArgb(200, 210, 230)), delegate
        {
            if (me.PuffT == 0) me.PuffT = 90;
            if (wxShow) { wxPeekT = 220; string wr = WeatherRemark(); if (wr != null) Say(wr, me.X, CatTop(me) - U(1), 'U'); }
        });
        catMenu.DropDownItems.Add(new ToolStripSeparator());
        catMenu.DropDownItems.Add(BuildFrightMenu());
        catMenu.DropDownItems.Add("Напугать котика", Ico("!", Color.FromArgb(255, 120, 120)), delegate
        {
            if (chasing) return;
            if (roaming) EndRoam();
            if (mode != 0 || pendingMode >= 0) { SetMode(0); chaseSoon = 45; } else if (!StartChase(true)) chaseSoon = 20;
        });
        catMenu.DropDownItems.Add("Погулять по экрану", Ico("\U0001F5A5", Color.FromArgb(120, 190, 255)), delegate
        {
            if (roaming) return;
            if (mode != 0 || pendingMode >= 0) { SetMode(0); roamSoon = 45; } else if (!StartRoam(true)) roamSoon = 20;
        });
        ToolStripMenuItem playItem = new ToolStripMenuItem("Играть с окнами и часами", null, delegate (object o, EventArgs e)
        {
            interfacePlay = !interfacePlay; ((ToolStripMenuItem)o).Checked = interfacePlay; SaveSettings();
            if (!interfacePlay && roaming) EndRoam();
        });
        playItem.Checked = interfacePlay;
        catMenu.DropDownItems.Add(playItem);
        menu.Items.Add(catMenu);
        BuildUsefulMenu();
        BuildNetMenu();

        ToolStripMenuItem funMenu = new ToolStripMenuItem("Отдых", Ico("\U0001F3B5", Color.FromArgb(255, 210, 90)));
        funMenu.DropDownItems.Add("Послушать музыку", Ico("\U0001F3B5", Color.FromArgb(255, 210, 90)), delegate
        {
            OpenUrl("https://music.yandex.ru/");
            movieT = 0; if (me.Hold == 'Q') me.Hold = ' ';
            musicT = 600;
            string[] l = { "Включаю музончик!", "Погнали, бит!", "Музыка — это жизнь" };
            Say(l[rnd.Next(l.Length)], me.X, CatTop(me) - U(1), 'U');
        });
        funMenu.DropDownItems.Add("Смотреть кино", Ico("\U0001F3AC", Color.FromArgb(232, 100, 110)), delegate
        {
            OpenUrl("https://www.kinopoisk.ru/");
            musicT = 0; me.Y = 0;
            movieT = 600;
            if (mode == 0 && me.Hold == ' ') me.Hold = 'Q';
            string[] l = { "Попкорн готов!", "Что посмотрим?", "Кинчик — святое", "Я вернусь... после титров", "Да пребудет с тобой попкорн", "Хьюстон, у нас попкорн" };
            Say(l[rnd.Next(l.Length)], me.X, CatTop(me) - U(1), 'U');
        });
        menu.Items.Add(funMenu);

        ToolStripMenuItem scMenu = new ToolStripMenuItem("Сценарии", Ico("\u2605", Color.FromArgb(160, 110, 226)));
        scMenu.DropDownItems.Add("Магазин: работать на кассе", Ico("$", Color.FromArgb(84, 200, 104)), delegate { SetMode(1); });
        scMenu.DropDownItems.Add("Диван: приставка и кальян", Ico("\U0001F3AE", Color.FromArgb(120, 190, 255)), delegate { SetMode(2); });
        scMenu.DropDownItems.Add("Разложить табак по стеллажу", Ico("\U0001F4E6", Color.FromArgb(200, 150, 100)), delegate { SetMode(3); });
        scMenu.DropDownItems.Add("Вечеринка у бассейна", Ico("\U0001F3CA", Color.FromArgb(80, 200, 240)), delegate { SetMode(4); });
        scMenu.DropDownItems.Add("Облава ОМОН", Ico("\U0001F6A8", Color.FromArgb(255, 90, 90)), delegate { SetMode(5); });
        scMenu.DropDownItems.Add("Ревизия от нач. кота", Ico("\U0001F4CB", Color.FromArgb(230, 230, 240)), delegate { SetMode(6); });
        menu.Items.Add(scMenu);
        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem sizeMenu = new ToolStripMenuItem("Размер", Ico("\u2922", Color.FromArgb(190, 196, 210)));
        sizeItems = new ToolStripMenuItem[SizeSteps.Length];
        for (int i = 0; i < SizeSteps.Length; i++)
        {
            int idx = i;
            sizeItems[i] = new ToolStripMenuItem(SizeNames[i], null, delegate { ChangeSize(idx); });
            sizeItems[i].Checked = (i == sizeIdx);
            sizeMenu.DropDownItems.Add(sizeItems[i]);
        }
        menu.Items.Add(sizeMenu);
        ToolStripMenuItem wxMenu = new ToolStripMenuItem("Погода", Ico("\u2600", Color.FromArgb(255, 200, 64)));
        wxShowItem = new ToolStripMenuItem("Показывать погоду", null, delegate { wxShow = !wxShow; UpdateWeatherMenu(); SaveSettings(); if (wxShow && !wxOk) FetchWeather(); });
        wxCityItem = new ToolStripMenuItem("Город", null, delegate { AskCity(); });
        wxMenu.DropDownItems.Add(wxShowItem);
        wxMenu.DropDownItems.Add(wxCityItem);
        wxMenu.DropDownItems.Add("Обновить сейчас", null, delegate { FetchWeather(); });
        menu.Items.Add(wxMenu);
        UpdateWeatherMenu();
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", Ico("\u2715", Color.FromArgb(255, 110, 110)), delegate { Application.Exit(); });
        StyleMenu(menu.Items);
        try { ReorganizeMenu(); } catch (Exception ex) { GuardLog("меню", ex); }
        if (cloneVoice) EnsureVoiceServer(true);

        timer = new System.Windows.Forms.Timer();
        timer.Interval = 33;
        timer.Tick += delegate
        {
            long pf0 = prof ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            try { MenuClickAway(); GameGuard(); Step(); }
            catch (Exception ex) { GuardLog("step", ex); }
            bool menuUp = MenuOpen;
            menuThrottleT = menuUp ? menuThrottleT + 1 : 0;
            try { UpdateTick(); } catch { }
            if (prof) ProfAdd(0, pf0);
            try
            {
                if (++fsCheckT >= 30) { fsCheckT = 0; FullscreenCheck(); }
                if (!fsHidden)
                {
                    bool calm = !menuUp && CalmNow();
                    calmT = calm ? calmT + 1 : 0;
                    if (menuUp ? menuThrottleT % 6 == 1 : (calmT < 15 || calmT % 3 == 0)) RenderFrame();
                    WindowGuard();
                }
            }
            catch (Exception ex) { RenderFailed(ex); }
            if (prof) ProfTick();
            try
            {
                if (roamSoon > 0 && --roamSoon == 0 && !StartRoam(true) && (pendingMode >= 0 || mode == 0)) roamSoon = 15;
                if (roaming) { RoamStep(); if (roaming && roamWin != null) roamWin.Render(); }
                if (chaseSoon > 0 && --chaseSoon == 0 && !StartChase(true) && (pendingMode >= 0 || mode == 0)) chaseSoon = 15;
                if (chaseCool > 0) chaseCool--; if (frightWindow > 0) frightWindow--;
                if (chasing) { ChaseStep(); if (chasing && chaseWin != null) chaseWin.Render(); }
            }
            catch (Exception ex) { GuardLog("прогулка/сценка", ex); try { EndRoam(); } catch { } try { EndChase(); } catch { } }
        };
        Microsoft.Win32.SystemEvents.SessionSwitch += delegate { needReset = 3; };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { needReset = 3; };
        Microsoft.Win32.SystemEvents.PowerModeChanged += delegate { needReset = 3; };
        timer.Start();
        if (wxShow) FetchWeather();
    }

    void ApplySize()
    {
        u = SizeSteps[sizeIdx] * dpi / 96f;
        uw = Math.Max(u, 1.8f * dpi / 96f);
        HH = (int)U(92) + DepthPx;
        if (fnt != null) fnt.Dispose();
        if (fntSign != null) fntSign.Dispose();
        fnt = new Font("Segoe UI", U(4.4f), FontStyle.Bold, GraphicsUnit.Pixel);
        fntSign = new Font("Segoe UI", U(3.2f), FontStyle.Bold, GraphicsUnit.Pixel);
        if (fntWxT != null) { fntWxT.Dispose(); fntWxD.Dispose(); fntWxS.Dispose(); }
        fntWxT = new Font("Segoe UI", W(7.6f), FontStyle.Bold, GraphicsUnit.Pixel);
        fntWxD = new Font("Segoe UI", W(4.0f), FontStyle.Bold, GraphicsUnit.Pixel);
        fntWxS = new Font("Segoe UI", W(3.2f), FontStyle.Regular, GraphicsUnit.Pixel);
        if (fntBigSerif != null) { fntBigSerif.Dispose(); fntBigBold.Dispose(); fntMatrix.Dispose(); }
        fntBigSerif = new Font("Georgia", W(10f), FontStyle.Regular, GraphicsUnit.Pixel);
        fntBigBold = new Font("Arial Black", W(9f), FontStyle.Bold, GraphicsUnit.Pixel);
        fntMatrix = new Font("Consolas", U(4.2f), FontStyle.Bold, GraphicsUnit.Pixel);
        if (fntHud != null) fntHud.Dispose();
        fntHud = new Font("Arial Black", W(5.4f), FontStyle.Regular, GraphicsUnit.Pixel);
        if (fntTiny != null) fntTiny.Dispose();
        fntTiny = new Font("Segoe UI", U(2.0f), FontStyle.Bold, GraphicsUnit.Pixel);
        if (logoFonts != null) foreach (Font lf in logoFonts) lf.Dispose();
        logoFonts = new Font[Games.Length];
        for (int i = 0; i < Games.Length; i++) logoFonts[i] = new Font(GameFontNames[i], U(3.6f), GameFontStyles[i], GraphicsUnit.Pixel);
        PlaceWindow();
    }

    void ChangeSize(int i)
    {
        sizeIdx = i;
        for (int k = 0; k < sizeItems.Length; k++) sizeItems[k].Checked = (k == i);
        SaveSettings();
        ApplySize();
        parts.Clear(); texts.Clear();
        SetMode(mode);
    }

    void PlaceWindow()
    {
        Rectangle wa = PetScreen.WorkingArea;
        // зона котика: правый (или левый) нижний угол выбранного монитора (доли ширины рабочей области)
        float al = petLeft ? 1f - AreaR : AreaL, ar = petLeft ? 1f - AreaL : AreaR;
        int l = wa.Left + (int)(wa.Width * al);
        int r = wa.Left + (int)(wa.Width * ar);
        Rectangle want = new Rectangle(l, wa.Bottom - HH, r - l, HH);
        if (Bounds != want) Bounds = want;
        Ground = HH - DepthPx;
        if (me != null && me.X > Width - U(12)) me.X = Width - U(12);
    }

    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000;     // WS_EX_LAYERED - настоящая прозрачность
            cp.ExStyle |= 0x80;        // не видно в Alt+Tab
            cp.ExStyle |= 0x08000000;  // не забирает фокус
            return cp;
        }
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }
}
