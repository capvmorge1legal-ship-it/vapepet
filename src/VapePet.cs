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

public class VapePet : Form
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
            try { if (!menuUp || menuThrottleT % 6 == 1) RenderFrame(); WindowGuard(); }
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
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        // зона котика: правый нижний угол экрана (доли ширины рабочей области)
        int l = wa.Left + (int)(wa.Width * AreaL);
        int r = wa.Left + (int)(wa.Width * AreaR);
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

    // ================= логика =================
    void SetMode(int m)
    {
        if (roaming) EndRoam();
        if (chasing) EndChase();
        sleeping = false; coffeeT = 0; yawnT = 0; partyPending = 0;
        foreach (Cat g in guests) Poof(g.X, CatTop(g) + U(8));
        guests.Clear();
        flyers.Clear();
        if (me.Sitting || me.Swimming || me.Jumping) Poof(me.X, CatTop(me) + U(8));
        me.Sitting = false; me.Swimming = false; me.Jumping = false; me.JDone = null;
        me.ItemRot = 0; me.Y = 0; me.Hold = ' '; me.Lying = false; me.Cuffed = false;
        egg = null; bigText = null; matrixT = 0; pickups.Clear(); me.Walking = false; me.Speed = 1.4f; me.Hidden = false; me.BlinkT = 0;
        script = null; digging = false; devOnReg = false; cardOnReg = false; bottleOnReg = false;
        pendingMode = m;
        leaveT = (mode == 0) ? 0 : 20;
    }

    void DressMe(int m)
    {
        me.Hat = 0; me.Glasses = false; me.HatBack = false;
        switch (m)
        {
            case 1: case 5: case 6: me.Outfit = 5; me.C1 = Color.FromArgb(60, 150, 95); me.C2 = Color.FromArgb(60, 90, 150); break;
            case 2: me.Outfit = 1; me.C1 = Color.FromArgb(70, 80, 110); me.Hat = 1; me.HatC = Color.FromArgb(220, 60, 70); me.HatBack = true; break;
            case 3: me.Outfit = 6; me.C1 = Color.FromArgb(70, 110, 190); me.Hat = 4; break;
            case 4: me.Outfit = 7; me.C1 = Color.FromArgb(40, 170, 190); me.C2 = Color.FromArgb(255, 230, 110); me.Glasses = true; break;
            default: me.Outfit = 1; me.C1 = Color.FromArgb(40, 42, 56); nightOutfit = -1; break;
        }
    }

    float SceneW(int m)
    {
        switch (m) { case 1: return U(200); case 2: return U(100); case 3: return U(165); case 4: return U(222); case 5: return U(200); case 6: return U(200); }
        return 0;
    }

    void ApplyMode(int m)
    {
        mode = m; pendingMode = -1; enterT = 0;
        DressMe(m);
        me.Phase = 0; me.T = 0; me.TZ = 0;
        me.Vape = (m != 2);
        float w = SceneW(m);
        A = me.X - w / 2;
        if (A > Width - w - U(4)) A = Width - w - U(4);
        if (A < U(4)) A = U(4);

        if (m == 1) { me.Speed = 2.3f; counterHP = 100; registerDown = false; registerFlying = false; sellerReactT = 0; spawnT = 70; regFlash = 0; bottleOnReg = false; devOnReg = false; cardOnReg = false; digging = false; script = null; }
        if (m == 2) { caseVisible = true; consoleOn = false; tvT = 0; tvState = 0; }
        if (m == 3)
        {
            pile.Clear(); dust.Clear(); boxes.Clear();
            for (int i = 0; i < 15; i++) { slots[i] = ' '; slotRes[i] = false; }
            rackAngle = 0; rackFalling = rackFallen = rackLifting = false; rackWobble = 0; grabRack = false;
            carState = 0; deliveryPending = true; trunkOpen = false; driverInCar = false; celebrated = false; script = null;
        }
        if (m == 5) SetupRaid();
        if (m == 6) SetupRevision();
        if (m == 4) { doorOpen = 0; guestsSpawned = 0; loungerBusy = false; Say("Вечеринка!", A + U(55), Ground - U(76), 'L'); }
    }

    void Step()
    {
        tick++;
        if (tick % 60 == 0) PlaceWindow();

        if (pendingMode >= 0)
        {
            if (leaveT > 0) leaveT--; else ApplyMode(pendingMode);
        }
        else
        {
            enterT++;
            switch (mode)
            {
                case 0: UpdateWalk(); break;
                case 1: UpdateShop(); break;
                case 2: UpdateChill(); break;
                case 3: UpdateTidy(); break;
                case 4: UpdateParty(); break;
                case 5: UpdateOmon(); break;
                case 6: UpdateRevision(); break;
            }
        }

        UpdateWeather();
        try { LifeStep(); LifeTimers(); if (tick % 30 == 0) ApplyTimeOutfit(); } catch (Exception ex) { GuardLog("жизнь котика", ex); }
        try { UsefulStep(); } catch (Exception ex) { GuardLog("полезное", ex); }
        try { if (!clipInited) { clipInited = true; ClipInit(); } NetTick(); PanelTick(); StockCacheTick(); BigTick(); } catch (Exception ex) { GuardLog("сеть/буфер", ex); }
        UpdateMedia();
        UpdateEggs();
        UpdateAI();
        UpdateGta();
        UpdateCat(me);
        foreach (Cat g in guests) UpdateCat(g);

        for (int i = flyers.Count - 1; i >= 0; i--)
        {
            Flyer f = flyers[i];
            f.T++;
            if (f.T >= f.Dur) { flyers.RemoveAt(i); if (f.Done != null) f.Done(); }
        }
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            Particle p = parts[i];
            p.Life++;
            if (p.Kind == 0 || p.Kind == 5) { p.X += p.VX; p.Y += p.VY; p.VX *= 0.97f; p.VY *= 0.985f; p.R += 0.06f * u; }
            else if (p.Kind == 1) { p.X += p.VX; p.Y += p.VY; p.VY += 0.09f * u; }
            else if (p.Kind == 6) { p.X += p.VX; p.Y += p.VY; }
            else if (p.Kind == 9) { p.Y += p.VY; p.X += (float)Math.Sin(p.Life * 0.2f) * 0.15f * u; }
            else if (p.Kind == 8) { p.Y += p.VY; if (rnd.Next(8) == 0) p.Col = "01ЖИЖАVAPE0110КОТ"[rnd.Next(17)]; }
            else if (p.Kind == 7) { p.X += p.VX + (float)Math.Sin((p.Life + p.R * 10) * 0.08f) * 0.12f * u; p.Y += p.VY; }
            else if (p.Kind == 4) { p.X += p.VX; p.Y += p.VY; p.VX *= 0.95f; p.VY *= 0.95f; }
            else if (p.Kind == 2 || p.Kind == 3) { p.Y += p.VY; p.X += (float)Math.Sin(p.Life * 0.15f) * 0.2f * u; }
            if (p.Life > p.Max) parts.RemoveAt(i);
        }
        for (int i = texts.Count - 1; i >= 0; i--)
        {
            texts[i].Life++; texts[i].Y -= 0.1f * u;
            if (texts[i].Life > texts[i].Max) texts.RemoveAt(i);
        }
        if (regFlash > 0) regFlash--;
        if (doorOpen > 0) doorOpen--;
        if (shakeT > 0) shakeT--;
        if (wxPeekT > 0) wxPeekT--;
        if (houstonPending) { houstonPending = false; Say("Хьюстон, у нас проблемы... нет интернета", me.X, CatTop(me) - U(1), 'U'); }
    }

    float meStillX; int meStillT, meIdleT, stillLogCool;
    void UpdateCat(Cat c)
    {
        if (c.Jumping)
        {
            c.JT++;
            float t = Math.Min(1f, c.JT / (float)c.JDur);
            c.X = c.JX0 + (c.JX1 - c.JX0) * t;
            c.Y = c.JY0 + (c.JY1 - c.JY0) * t + (float)Math.Sin(Math.PI * t) * c.JH;
            if (c.JX1 > c.JX0 + 0.5f) c.Dir = 1; else if (c.JX1 < c.JX0 - 0.5f) c.Dir = -1;
            if (t >= 1f) { c.Jumping = false; c.LandT = 8; Action d = c.JDone; c.JDone = null; if (d != null) d(); }
        }
        else if (c.Walking)
        {
            if (c.Speed < 0.3f) c.Speed = 1f;   // скорость обнулилась — иначе вечный шаг на месте
            float d = c.TX - c.X;
            float sp = c.Speed * u / 2.2f;
            if (Math.Abs(d) <= sp) { c.X = c.TX; c.Walking = false; }
            else { c.Z += (c.TZ - c.Z) * Math.Min(1f, sp / Math.Abs(d)); c.X += Math.Sign(d) * sp; c.Dir = Math.Sign(d); }
        }
        // сторож: «идёт», но с места не сдвинулся (кто-то держит X) — пусть стоит
        if (c == me)
        {
            if (c.Jumping || Math.Abs(c.X - meStillX) >= 0.05f) { meStillT = 0; meIdleT = 0; }
            else if (c.Walking)
            {
                meIdleT = 0;
                if (++meStillT >= 8)
                {
                    if (meStillT == 8 && stillLogCool == 0) { stillLogCool = 30 * 300; GuardLog("шаг на месте: mode=" + mode + " phase=" + c.Phase + " tx=" + (int)c.TX + " x=" + (int)c.X + " sp=" + c.Speed, null); }
                    c.Walking = false;
                }
            }
            else if (++meIdleT > 20) meStillT = 0;
            meStillX = c.X; if (stillLogCool > 0) stillLogCool--;
        }
        if (c.Lying || c.Cuffed || c.Sitting || c.Swimming || (mode == 5 && omonPhase != 0) || (c == me && pendingMode >= 0)) c.TZ = 0;
        c.ZMove = !c.Walking && !c.Jumping && c.Z != c.TZ;
        {
            int v = 1; float dzv = c.TZ - c.Z;
            if (!(c.Lying || c.Sitting || c.Swimming || c.Jumping || c.Cuffed))
            {
                if (c.ZMove) v = dzv < 0 ? 2 : 0;
                else if (c.Walking) { if (Math.Abs(dzv) * 22f > Math.Abs(c.TX - c.X) / u * 1.2f) v = dzv < 0 ? 2 : 0; }
                else if (c == me && ShopScene && c.Z <= ShelfZ + 0.05f && c.X < A + U(58)) v = 2;
                else if (c == me && mode == 0 && pendingMode < 0 && c.Hold == ' ') v = 0;
            }
            c.View = v;
        }
        if (c.ZMove) { float dz = c.TZ - c.Z; c.Z = Math.Abs(dz) <= 0.035f ? c.TZ : c.Z + Math.Sign(dz) * 0.035f; }
        if (c.Hidden) c.PuffT = 0;   // кот ушёл гулять — пар в уголке не идёт
        if (c.PuffT > 0)
        {
            c.PuffT--;
            if (c.PuffT < 55 && c.PuffT > 20) { SpawnSmoke(c); if (rnd.Next(2) == 0) SpawnSmoke(c); }
        }
        if (c.Shake > 0) c.Shake--;
        if (c.AngryT > 0) c.AngryT--;
        if (c.SweatT > 0) c.SweatT--;
        if (c.StompT > 0 && !c.Jumping)
        {
            c.StompT--;
            c.Y = Math.Abs((float)Math.Sin(c.StompT * 0.42f)) * U(2.4f);
            if (c.StompT % 10 == 0) { Particle d = new Particle(); d.Kind = 0; d.X = c.X; d.Y = Ground - U(1); d.VX = (float)(rnd.NextDouble() - 0.5) * u; d.VY = -0.1f * u; d.R = U(1.2f); d.Max = 20; parts.Add(d); }
            if (c.StompT == 0) c.Y = 0;
        }
        if (c.HappyT > 0 && !c.Jumping)
        {
            c.HappyT--;
            c.Y = Math.Abs((float)Math.Sin(c.HappyT * 0.3f)) * U(1.3f);
            if (c.HappyT % 14 == 0)
            {
                PointF h = Local(c, 1, -27);
                Particle hp = new Particle(); hp.Kind = 3; hp.X = h.X; hp.Y = h.Y; hp.VY = -0.25f * u; hp.Max = 45; hp.Col = 'P';
                parts.Add(hp);
            }
            if (c.HappyT == 0) c.Y = 0;
        }
        if (!c.Jumping && !c.Sitting && !c.Swimming && (c.Impatient || c.Stiff))
            c.Y = Math.Abs((float)Math.Sin(tick * (c.Stiff ? 0.5f : 0.35f))) * U(c.Stiff ? 2.2f : 1.4f);
        if (c.BlinkT > 0) c.BlinkT--;
        else if (rnd.Next(300) == 0) c.BlinkT = rnd.Next(4) == 0 ? 16 : 6;
        if (c.LandT > 0) c.LandT--;
        if (c.TurnT > 0) c.TurnT--;
        if (c.Dir != c.LastDir || c.View != c.LastView) { c.TurnT = 6; c.LastDir = c.Dir; c.LastView = c.View; }
    }

    // ---- глубина сцены: Z = 0 — линия мебели, Z > 0 — ближе к зрителю (ниже и крупнее), Z < 0 — вглубь (выше и мельче)
    int DepthPx { get { return (int)U(22); } }
    const float ShelfZ = -0.6f;
    float ZK(float z) { return z >= 0 ? 1f + 0.30f * z : 1f + 0.28f * z; }
    float ZY(float z) { return z >= 0 ? z * DepthPx : z * U(13); }
    bool ShopScene { get { return mode == 1 || mode == 6 || (mode == 5 && omonPhase == 0); } }
    float LaneFor(Cat c, float x)
    {
        if (c.Lying || c.Cuffed || c.Sitting || c.Swimming) return 0;
        if (c == me)
        {
            if (pendingMode >= 0) return 0;
            if (mode == 0) return Math.Max(0f, Math.Min(1f, c.Z + ((float)rnd.NextDouble() - 0.5f) * 2f * Math.Min(1f, Math.Abs(x - c.X) / U(90))));
            if (ShopScene && x < A + U(58)) return ShelfZ;
            return 0;
        }
        if (x < -U(2) || x > Width + U(2)) return 1f;
        if (mode == 1 || mode == 6) return 0.2f + (c.Id % 3) * 0.14f;
        return 0;
    }
    void Go(Cat c, float x)
    {
        if (c != me && (c.X < 0 || c.X > Width) && !c.Walking) c.Z = 1f;
        c.TX = x; c.Walking = Math.Abs(x - c.X) > 0.5f; c.TZ = LaneFor(c, x);
    }

    void Jump(Cat c, float x1, float y1, float h, int dur, Action done)
    {
        c.Walking = false; c.Jumping = true; c.JT = 0; c.JDur = dur;
        c.JX0 = c.X; c.JX1 = x1; c.JY0 = c.Y; c.JY1 = y1; c.JH = h; c.JDone = done;
    }

    // ---- гуляет ----
    void UpdateWalk()
    {
        if (dragging) { me.Walking = false; return; }
        if (sleeping || coffeeT > 0 || yawnT > 0) { me.Walking = false; return; }
        if (musicT > 0 || movieT > 0 || aiBusy)
        {
            me.Walking = false;
            if (musicT > 0 && !me.Jumping) { me.Y = Math.Abs((float)Math.Sin(tick * 0.22f)) * U(1.3f); if (tick % 40 == 0) me.Dir = -me.Dir; }
            me.Phase = 1; me.T = 30;
            return;
        }
        if (me.Phase == 0)
        {
            me.T--;
            if (!me.Walking || me.T <= 0) WalkNext();
        }
        else
        {
            if (--me.T <= 0 && me.PuffT == 0) WalkNext();
        }
    }

    int roamSoon, chaseSoon;
    void WalkNext()
    {
        me.Speed = 1.4f; me.TZ = me.Z;
        if (interfacePlay && tick > 30 * 25 && rnd.Next(100) < 7 && StartRoam(false)) return;
        int r = rnd.Next(100);
        if (r < 30) { me.PuffT = 90; me.Phase = 1; me.T = 100; me.Walking = false; return; }
        if (r < 55) { me.Phase = 1; me.T = 60 + rnd.Next(120); me.Walking = false; if (rnd.Next(100) < 35) { string wr = WeatherRemark(); if (wr != null) Say(wr, me.X, CatTop(me) - U(1), 'U'); } return; }
        me.Phase = 0; me.T = 900;
        if (rnd.Next(3) == 0) Go(me, Math.Max(U(14), Math.Min(Width - U(14), me.X + ((float)rnd.NextDouble() - 0.5f) * U(8))));
        else Go(me, U(14) + (float)rnd.NextDouble() * (Width - U(28)));
    }

    // ---- магазин ----
    float CashX { get { return A + U(68); } }
    float CounterX { get { return A + U(80); } }
    float CustFront { get { return A + U(117); } }

    void UpdateShop()
    {
        if (enterT < 50) return;

        spawnT--;
        int queued = 0;
        foreach (Cat g in guests) if (g.Phase <= 2) queued++;
        if (spawnT <= 0 && queued < 4)
        {
            Cat c = NewGuest();
            c.X = Width + U(14); c.Dir = -1;
            int sc;
            int roll = rnd.Next(100);
            if (roll < 22) sc = 0;
            else if (roll < 50 && lastScript != 9) sc = 9;
            else if (roll < 72 && lastScript != 8) sc = 8;
            else { do { sc = 1 + rnd.Next(7); } while (sc == lastScript); }
            lastScript = sc;
            c.Script = sc;
            if (sc == 2) { c.Hold = 'V'; c.HoldCol = 'G'; }
            if (sc == 6) { c.Hold = 'B'; c.HoldCol = 'N'; }
            if (sc == 8)
            {
                int v; do { v = rnd.Next(6); } while (v == lastDialog);
                lastDialog = v; c.DialogV = v;
                { c.MainC = Color.FromArgb(120, 112, 104); c.BellyC = Color.FromArgb(200, 192, 182); c.Speed = 1.0f; c.Outfit = 4; c.C1 = Color.FromArgb(40, 44, 60); c.C2 = Color.White; c.Hat = 1; c.HatC = Color.FromArgb(30, 30, 36); c.HatBack = false; }
            }
            if (sc == 9)
            {
                int id; do { id = rnd.Next(15); } while (id == lastCostume);
                lastCostume = id; c.DialogV = id;
                ApplyCostume(c, id);
            }
            c.Temper = 0.5f + (float)rnd.NextDouble() * 1.3f;
            if (sc == 8 && c.Outfit == 4) c.Temper = 2.0f;
            c.Patience = 100; c.ActT = 60 + rnd.Next(80); c.Speed = Math.Max(c.Speed, 1.5f);
            guests.Add(c);
            spawnT = 110 + rnd.Next(150);
        }

        int qi = 0;
        foreach (Cat g in guests)
        {
            if (g.Phase <= 2)
            {
                float qx = CustFront + qi * U(22);
                if (g.Phase < 2 && (Math.Abs(g.TX - qx) > 0.5f || (!g.Walking && Math.Abs(g.X - qx) > 0.5f))) Go(g, qx);
                if (!g.Walking)
                {
                    g.Dir = -1;
                    if (qi == 0 && g.Phase == 0) g.Phase = 1;
                }
                qi++;
            }
        }
        for (int i = guests.Count - 1; i >= 0; i--)
            if (guests[i].Phase == 3 && !guests[i].Walking) guests.RemoveAt(i);

        UpdateQueueMood();
        if (sellerReactT > 0) sellerReactT--;

        if (digging && tick % 7 == 0)
        {
            float dx = me.X - U(4 + (float)rnd.NextDouble() * 14);
            Fly('X', ItemColors[rnd.Next(ItemColors.Length)], CounterX + U(4), Ground - U(12), dx, Ground - U(2), 18, null);
        }

        if (script != null)
        {
            int guard = 0;
            while (script != null && scriptIdx < script.Count && guard++ < 12)
            {
                if (script[scriptIdx]()) scriptIdx++; else break;
            }
            if (script != null && scriptIdx >= script.Count) script = null;
            return;
        }

        if (me.Walking) return;
        if (registerDown && !registerFlying) { script = FixRegister(); scriptIdx = 0; return; }
        if (counterHP < 45 && !AnyRampage()) { script = FixCounter(); scriptIdx = 0; return; }
        if (Math.Abs(me.X - CashX) > 0.5f) { Go(me, CashX); return; }
        me.Dir = 1;
        Cat f = null;
        foreach (Cat g in guests) if (g.Phase <= 2) { f = g; break; }
        if (f != null && f.Phase == 1)
        {
            f.Phase = 2;
            script = BuildScript(f);
            scriptIdx = 0;
        }
        else if (me.PuffT == 0 && rnd.Next(300) == 0) me.PuffT = 90;
    }

    // ---- шаги сценок ----
    float ShelfX(int i) { return A + U(13 + 30 * i); }
    PointF RegPt { get { return new PointF(CounterX + U(9), Ground - U(25.2f)); } }
    PointF ShelfPt(int i) { return new PointF(ShelfX(i), Ground - U(22)); }

    Func<bool> Do(Action a) { return delegate { a(); return true; }; }
    Func<bool> Wait(int n) { int t = n; return delegate { return --t <= 0; }; }
    Func<bool> GoF(Func<Cat> c, Func<float> x)
    {
        bool started = false;
        return delegate { if (!started) { started = true; Go(c(), x()); } return !c().Walking; };
    }
    Func<bool> GoStep(Cat c, Func<float> x) { return GoF(delegate { return c; }, x); }
    Func<bool> SayF(Func<Cat> c, string text, int wait, char col)
    {
        bool said = false; int t = wait;
        return delegate
        {
            if (!said) { said = true; Cat cc = c(); Say(text, cc.Lying ? cc.X + cc.Dir * U(12) : cc.X, CatTop(cc) - U(1), col); }
            return --t <= 0;
        };
    }
    Func<bool> SayStep(Cat c, string text, int wait) { return SayF(delegate { return c; }, text, wait, c == me ? 'U' : 'K'); }
    Func<bool> FlyStep(char kind, char col, Func<PointF> from, Func<PointF> to, int dur)
    {
        bool started = false, done = false;
        return delegate
        {
            if (!started)
            {
                started = true;
                PointF a = from(), b = to();
                Fly(kind, col, a.X, a.Y, b.X, b.Y, dur, delegate { done = true; });
            }
            return done;
        };
    }
    Func<bool> LeaveShop(Cat f) { return Do(delegate { f.Phase = 3; Go(f, Width + U(16)); }); }

    void AddFetch(List<Func<bool>> s, char col)
    {
        int sh = rnd.Next(2);
        s.Add(GoStep(me, delegate { return ShelfX(sh); }));
        s.Add(Do(delegate { me.Dir = -1; }));
        s.Add(Wait(7));
        s.Add(Do(delegate { me.Hold = 'B'; me.HoldCol = col; }));
        s.Add(GoStep(me, delegate { return CashX; }));
        s.Add(Do(delegate { me.Dir = 1; }));
    }
    void AddScan(List<Func<bool>> s, char col)
    {
        s.Add(Do(delegate { me.Hold = ' '; }));
        s.Add(FlyStep('B', col, delegate { return Paw(me); }, delegate { return RegPt; }, 9));
        s.Add(Do(delegate { bottleOnReg = true; regCol = col; regFlash = 25; Say("ПИК!", CounterX + U(11), Ground - U(31), 'R'); }));
        s.Add(Wait(16));
    }
    void AddHandOver(List<Func<bool>> s, Cat f, char col, bool leave)
    {
        s.Add(Do(delegate { bottleOnReg = false; }));
        s.Add(FlyStep('B', col, delegate { return RegPt; }, delegate { return Paw(f); }, 11));
        s.Add(Do(delegate
        {
            f.Hold = 'B'; f.HoldCol = col;
            int price = Prices[rnd.Next(Prices.Length)];
            PointF cp = Paw(f);
            float px = CounterX - U(3.5f);
            Fly('O', 'Y', cp.X, cp.Y, px, Ground - U(3), 14, delegate { pickups.Add(new float[] { px, price }); });
            if (f.Temper < 1.6f && f.Patience > 30) f.HappyT = 40;
        }));
        if (leave) s.Add(LeaveShop(f));
        s.Add(Wait(15));
        s.Add(GoStep(me, delegate { return CounterX - U(3.5f); }));
        s.Add(Wait(6));
    }

    // реплика с анимацией: a - злится, s - топает, h - радуется, w - пот/вздох, n - ничего
    Func<bool> Line(Cat c, string text, int wait, char act)
    {
        bool done = false;
        Func<bool> say = SayStep(c, text, wait);
        return delegate
        {
            if (!done)
            {
                done = true;
                if (act == 'a') { c.AngryT = wait + 30; c.Shake = 30; }
                if (act == 's') { c.AngryT = wait + 30; c.StompT = 42; }
                if (act == 'h') { c.HappyT = Math.Max(40, wait); }
                if (act == 'w') { c.SweatT = wait + 40; c.BlinkT = 20; }
                if (mode == 1) c.Dir = (c == me) ? 1 : -1;
            }
            return say();
        };
    }

    void AddDialog(List<Func<bool>> s, Cat f)
    {
        int v = f.DialogV;
        if (v < 0) { do { v = rnd.Next(8); } while (v == lastDialog); lastDialog = v; }
        char col = ItemColors[rnd.Next(ItemColors.Length)];
        switch (v)
        {
            case 0: // синяя
                s.Add(Line(f, "Дай эту... как её... синюю!", 50, 'n'));
                s.Add(Line(me, "Синяя — черника, голубика и мятный лёд. Какую? :)", 70, 'h'));
                s.Add(Line(f, "СИНЮЮ, Я СКАЗАЛ!!!", 50, 's'));
                s.Add(Line(me, "Понял-понял! Несу все три, выбирай!", 40, 'h'));
                AddFetch(s, 'U');
                s.Add(Do(delegate { me.Hold = ' '; }));
                s.Add(FlyStep('B', 'U', delegate { return Paw(me); }, delegate { return new PointF(RegPt.X - U(3), RegPt.Y); }, 10));
                s.Add(FlyStep('B', 'u', delegate { return Paw(me); }, delegate { return RegPt; }, 10));
                s.Add(FlyStep('B', 'L', delegate { return Paw(me); }, delegate { return new PointF(RegPt.X + U(3), RegPt.Y); }, 10));
                s.Add(Line(f, "...", 45, 'n'));
                s.Add(Line(f, "А красная есть?", 45, 'n'));
                s.Add(Line(me, "*вздох*", 40, 'w'));
                s.Add(Line(me, "Сейчас будет и красная :)", 30, 'h'));
                s.Add(FlyStep('B', 'u', delegate { return RegPt; }, delegate { return ShelfPt(0); }, 16));
                AddFetch(s, 'R'); AddScan(s, 'R'); AddHandOver(s, f, 'R', false);
                s.Add(Line(f, "Долго!", 30, 'a'));
                s.Add(Line(me, "Заходи ещё! :)", 20, 'h'));
                s.Add(LeaveShop(f));
                break;

            case 1: // сдача
                AddFetch(s, col); AddScan(s, col);
                s.Add(Line(me, "С тебя 450, дружище!", 45, 'h'));
                s.Add(Line(f, "А чё не 400?!", 45, 'a'));
                s.Add(Line(me, "Потому что вкусно :)", 40, 'h'));
                s.Add(Line(f, "Тьфу. На.", 25, 'a'));
                s.Add(FlyStep('O', 'Y', delegate { return Paw(f); }, delegate { return new PointF(RegPt.X + U(4), RegPt.Y + U(2)); }, 12));
                s.Add(FlyStep('O', 'Y', delegate { return Paw(f); }, delegate { return new PointF(RegPt.X + U(5), RegPt.Y + U(2)); }, 12));
                s.Add(Line(me, "Эм... тут 45 рублей", 50, 'w'));
                s.Add(Line(f, "НУ И ЧТО?!", 45, 's'));
                s.Add(Line(me, "Ахах, ладно! Остальное потом занесёшь :)", 60, 'h'));
                AddHandOver(s, f, col, false);
                s.Add(Line(f, "...Нормальный ты кот вроде", 45, 'n'));
                s.Add(LeaveShop(f));
                break;

            case 2: // не парит
                s.Add(Do(delegate { f.Hold = 'V'; f.HoldCol = 'G'; }));
                s.Add(Line(f, "Эта штука НЕ ПАРИТ!!!", 45, 's'));
                s.Add(Do(delegate { f.Hold = ' '; }));
                s.Add(FlyStep('V', 'G', delegate { return Paw(f); }, delegate { return RegPt; }, 10));
                s.Add(Do(delegate { devOnReg = true; regFlash = 10; }));
                s.Add(Line(me, "Не кипятись, ща глянем :)", 45, 'h'));
                s.Add(Do(delegate { me.BlinkT = 30; }));
                s.Add(Wait(30));
                s.Add(Line(me, "Так картридж же пустой!", 45, 'w'));
                s.Add(Line(f, "И чё?", 35, 'a'));
                s.Add(Line(me, "Жижу заливать надо, бро :)", 45, 'h'));
                s.Add(Line(f, "...", 50, 'n'));
                s.Add(Line(f, "А так можно было?", 45, 'n'));
                AddFetch(s, col);
                s.Add(Do(delegate { me.Hold = ' '; }));
                s.Add(FlyStep('B', col, delegate { return Paw(me); }, delegate { return RegPt; }, 12));
                s.Add(Line(me, "Буль-буль... готово!", 30, 'h'));
                s.Add(Do(delegate { devOnReg = false; }));
                s.Add(FlyStep('V', 'G', delegate { return RegPt; }, delegate { return Paw(f); }, 14));
                s.Add(Do(delegate { f.Hold = 'V'; f.HoldCol = 'G'; f.PuffT = 90; f.Vape = true; }));
                s.Add(Wait(50));
                s.Add(Line(f, "Магия...", 40, 'n'));
                s.Add(LeaveShop(f));
                break;

            case 3: // обслужи
                s.Add(Line(f, "Э, кот! Обслужи давай!", 45, 'a'));
                s.Add(Line(me, "Привет-привет! Чем порадовать? :)", 50, 'h'));
                s.Add(Line(f, "Сам знаешь чем!", 40, 'a'));
                s.Add(Line(me, "Эээ... хорошим настроением?", 45, 'w'));
                s.Add(Line(f, "ЖИЖЕЙ!!!", 40, 's'));
                s.Add(Line(me, "Так бы сразу :)", 30, 'h'));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, false);
                s.Add(Line(f, "Пф.", 30, 'n'));
                s.Add(Line(me, "Хорошего дня! <3", 25, 'h'));
                s.Add(LeaveShop(f));
                break;

            case 4: // скидка
                s.Add(Line(f, "Скидку давай.", 40, 'a'));
                s.Add(Line(me, "Скидка по карте лояльности! :)", 50, 'h'));
                s.Add(Line(f, "Какой ещё карте?!", 40, 'a'));
                s.Add(Line(me, "Вот этой — держи, бесплатно!", 40, 'h'));
                s.Add(FlyStep('I', 'u', delegate { return Paw(me); }, delegate { return Paw(f); }, 14));
                s.Add(Do(delegate { f.Hold = 'I'; f.HoldCol = 'u'; f.BlinkT = 30; }));
                s.Add(Wait(35));
                s.Add(Line(f, "...а она съедобная?", 50, 'n'));
                s.Add(Line(me, "Ахаха, нет! Но жижа вкусная :)", 50, 'h'));
                s.Add(Do(delegate { f.Hold = ' '; }));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, false);
                s.Add(Line(f, "Ладно. Карту не отдам.", 40, 'n'));
                s.Add(LeaveShop(f));
                break;

            case 6: // красная или синяя
                s.Add(Line(f, "Мне что-нибудь... сам не знаю что", 45, 'n'));
                s.Add(Line(me, "Выбирай: красная или синяя?", 45, 'h'));
                s.Add(Do(delegate
                {
                    Fly('B', 'R', ShelfX(0), Ground - U(25), RegPt.X - U(2.5f), RegPt.Y, 18, null);
                    Fly('B', 'U', ShelfX(1), Ground - U(25), RegPt.X + U(2.5f), RegPt.Y, 18, null);
                }));
                s.Add(Wait(20));
                s.Add(Line(me, "Синяя — и сказке конец. Красная — и ты в стране чудес...", 65, 'n'));
                s.Add(Do(delegate { matrixT = 160; f.SweatT = 80; }));
                s.Add(Wait(40));
                s.Add(Line(f, "...а есть со вкусом пломбира?", 50, 'n'));
                s.Add(Line(me, "Эх, никто не ценит классику", 35, 'w'));
                AddFetch(s, 'Y'); AddScan(s, 'Y'); AddHandOver(s, f, 'Y', true);
                break;

            case 7: // брат
                s.Add(Line(f, "Скажи, брат, в чём сила?", 45, 'n'));
                s.Add(Line(me, "Сила в правде, брат. И в никотине.", 50, 'h'));
                s.Add(Line(f, "Вот и я так думаю. Дай самую крепкую.", 45, 'n'));
                AddFetch(s, 'K'); AddScan(s, 'K'); AddHandOver(s, f, 'K', false);
                s.Add(Line(f, "Бывай, брат.", 30, 'h'));
                s.Add(LeaveShop(f));
                break;

            default: // перепутал магазин
                s.Add(Line(f, "Дайте батон и кефир.", 45, 'n'));
                s.Add(Line(me, "Хех, это вейп-шоп, дружище :)", 50, 'h'));
                s.Add(Line(f, "А ГДЕ ХЛЕБ?!", 40, 's'));
                s.Add(Line(me, "Хлеба нет, есть жижа со вкусом печенья!", 55, 'h'));
                s.Add(Line(f, "...", 40, 'n'));
                s.Add(Line(f, "Беру. Две.", 35, 'a'));
                AddFetch(s, 'Y'); AddScan(s, 'Y'); AddHandOver(s, f, 'Y', false);
                s.Add(Line(me, "Приятного аппетита! :)", 25, 'h'));
                s.Add(Line(f, "Это шутка такая?!", 30, 'a'));
                s.Add(LeaveShop(f));
                break;
        }
    }

    List<Func<bool>> BuildScript(Cat f)
    {
        List<Func<bool>> s = new List<Func<bool>>();
        char col = ItemColors[rnd.Next(ItemColors.Length)];
        switch (f.Script)
        {
            case 1: // не холодную скалу
                s.Add(SayStep(f, "Не холодную скалу!", 45));
                AddFetch(s, 'u');
                AddScan(s, 'u');
                s.Add(Do(delegate { f.Shake = 45; }));
                s.Add(SayStep(f, "Я ж сказал — НЕ холодную!!!", 55));
                s.Add(SayStep(me, "Ой. Сорян.", 25));
                s.Add(Do(delegate { bottleOnReg = false; }));
                s.Add(FlyStep('B', 'u', delegate { return RegPt; }, delegate { return ShelfPt(1); }, 18));
                AddFetch(s, 'R');
                AddScan(s, 'R');
                AddHandOver(s, f, 'R', false);
                s.Add(SayStep(f, "Вот, другое дело", 30));
                s.Add(LeaveShop(f));
                break;

            case 2: // заглушка
                s.Add(SayStep(f, "А есть заглушка на эту @%$#@$%?", 60));
                s.Add(Do(delegate { f.Hold = ' '; }));
                s.Add(FlyStep('V', 'G', delegate { return Paw(f); }, delegate { return RegPt; }, 14));
                s.Add(Do(delegate { devOnReg = true; me.BlinkT = 25; }));
                s.Add(SayStep(me, "На... что?", 40));
                s.Add(SayStep(me, "Щас поищу...", 20));
                s.Add(Do(delegate { me.Sitting = true; digging = true; }));
                s.Add(Wait(85));
                s.Add(Do(delegate { me.Sitting = false; digging = false; }));
                s.Add(SayStep(me, "Нашёл!", 25));
                s.Add(FlyStep('Z', 'R', delegate { return Paw(me); }, delegate { return Paw(f); }, 14));
                s.Add(Do(delegate { devOnReg = false; }));
                s.Add(FlyStep('V', 'G', delegate { return RegPt; }, delegate { return Paw(f); }, 14));
                s.Add(Do(delegate { f.Hold = 'V'; f.HoldCol = 'G'; Say("+150 ₽", CounterX + U(10), Ground - U(33), 'n'); }));
                s.Add(SayStep(f, "Респект, братишка", 35));
                s.Add(LeaveShop(f));
                break;

            case 3: // паспорт / ID
                s.Add(SayStep(f, Phrases[rnd.Next(Phrases.Length)], 40));
                s.Add(SayStep(me, "Документик покажете?", 45));
                s.Add(SayStep(f, "Паспорта нет, есть ID", 50));
                s.Add(FlyStep('I', 'u', delegate { return Paw(f); }, delegate { return RegPt; }, 14));
                s.Add(Do(delegate { cardOnReg = true; me.BlinkT = 45; }));
                s.Add(SayStep(me, "Хмм... ID, значит...", 50));
                s.Add(Do(delegate { cardOnReg = false; }));
                s.Add(FlyStep('I', 'u', delegate { return RegPt; }, delegate { return Paw(f); }, 14));
                s.Add(SayStep(me, "Ладно, проходи", 20));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, true);
                break;

            case 4: // самое лютое
                s.Add(SayStep(f, "Самое лютое крепкое, чтобы лапки стояли!", 60));
                AddFetch(s, 'K'); AddScan(s, 'K'); AddHandOver(s, f, 'K', false);
                s.Add(Do(delegate { f.Vape = true; f.PuffT = 90; }));
                s.Add(Wait(55));
                s.Add(Do(delegate
                {
                    PointF m = Local(f, 4, -15);
                    Poof(m.X, m.Y); Poof(m.X - U(4), m.Y - U(3)); Poof(m.X + U(3), m.Y - U(5));
                    f.Stiff = true; f.Shake = 70;
                }));
                s.Add(SayStep(f, "УУХ!!! Лапки стоят!", 55));
                s.Add(SayStep(me, "Предупреждал же...", 10));
                s.Add(LeaveShop(f));
                break;

            case 5: // шаку
                s.Add(SayStep(f, "Шаку мне, шаку!", 25));
                s.Add(Do(delegate { f.Impatient = true; me.Speed = 2.8f; }));
                AddFetch(s, 'L');
                s.Add(SayStep(f, "ШААКУУ!!!", 10));
                AddScan(s, 'L');
                AddHandOver(s, f, 'L', false);
                s.Add(Do(delegate { f.Impatient = false; f.Y = 0; me.Speed = 2.3f; }));
                s.Add(SayStep(f, "Вот это сервис", 30));
                s.Add(LeaveShop(f));
                break;

            case 6: // возврат
                s.Add(SayStep(f, "Требую возврат!", 45));
                s.Add(Do(delegate { f.Shake = 35; f.Hold = ' '; }));
                s.Add(FlyStep('B', 'N', delegate { return Paw(f); }, delegate { return RegPt; }, 14));
                s.Add(Do(delegate { bottleOnReg = true; regCol = 'N'; }));
                s.Add(SayStep(me, "А что не так?", 35));
                s.Add(Do(delegate { f.Shake = 30; }));
                s.Add(SayStep(f, "Не парит! Ваще!", 45));
                s.Add(Do(delegate { me.BlinkT = 20; }));
                s.Add(SayStep(me, "...Ладно, держите", 35));
                s.Add(FlyStep('O', 'Y', delegate { return RegPt; }, delegate { return Paw(f); }, 15));
                s.Add(Do(delegate { Say("-450 ₽", CounterX + U(10), Ground - U(33), 'R'); bottleOnReg = false; }));
                s.Add(FlyStep('B', 'N', delegate { return RegPt; }, delegate { return ShelfPt(0); }, 18));
                s.Add(SayStep(f, "Так-то лучше", 30));
                s.Add(LeaveShop(f));
                break;

            case 7: // нач. кот
            {
                Cat boss = null;
                Func<Cat> bossF = delegate { return boss; };
                s.Add(SayStep(f, "Вы вообще адекватный тут кот?", 55));
                s.Add(SayStep(f, "Позовите нач. кота!", 45));
                s.Add(SayStep(me, "Щас позову...", 25));
                s.Add(GoStep(me, delegate { return ShelfX(0); }));
                s.Add(Do(delegate { Poof(me.X, CatTop(me) + U(8)); me.Hidden = true; }));
                s.Add(Wait(30));
                s.Add(Do(delegate
                {
                    boss = new Cat();
                    boss.Id = catIds++; boss.Boss = true; boss.Scale = 1.35f; boss.Vape = true; boss.Outfit = 3; boss.C1 = Color.FromArgb(38, 38, 48); boss.C2 = Color.FromArgb(210, 40, 50); boss.Glasses = true; boss.IrisC = Color.FromArgb(230, 170, 50);
                    boss.MainC = Color.FromArgb(52, 50, 60); boss.BellyC = Color.FromArgb(125, 120, 135);
                    boss.Phase = 9; boss.Speed = 1.1f; boss.X = ShelfX(0);
                    guests.Add(boss);
                    Poof(boss.X, CatTop(boss) + U(10));
                }));
                s.Add(Wait(15));
                s.Add(GoF(bossF, delegate { return CashX; }));
                s.Add(Do(delegate { boss.Dir = 1; f.Shake = 20; }));
                s.Add(SayF(bossF, "Я нач. кот. Какие проблемы?", 60, 'K'));
                s.Add(Do(delegate { f.Shake = 60; }));
                s.Add(SayStep(f, "Н-никаких... всё супер!", 40));
                s.Add(Do(delegate { f.Phase = 3; f.Speed = 3.4f; Go(f, Width + U(16)); }));
                s.Add(Do(delegate { boss.PuffT = 90; }));
                s.Add(SayF(bossF, "То-то же.", 70, 'K'));
                s.Add(GoF(bossF, delegate { return ShelfX(0); }));
                s.Add(Do(delegate
                {
                    Poof(boss.X, CatTop(boss) + U(10)); guests.Remove(boss);
                    me.Hidden = false; Poof(me.X, CatTop(me) + U(8));
                }));
                s.Add(Wait(20));
                break;
            }

            case 9:
                AddCostumeDialog(s, f);
                break;

            case 8:
                AddDialog(s, f);
                break;

            default:
                s.Add(SayStep(f, Phrases[rnd.Next(Phrases.Length)], 40));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, true);
                break;
        }
        return s;
    }

    Cat NewGuest()
    {
        Cat c = new Cat();
        c.Id = catIds++;
        int k = rnd.Next(CatColors.GetLength(0));
        c.MainC = CatColors[k, 0];
        c.BellyC = CatColors[k, 1];
        c.Stripes = rnd.Next(2) == 0;
        Color[] irises = { Color.FromArgb(230, 170, 50), Color.FromArgb(120, 190, 80), Color.FromArgb(90, 170, 240), Color.FromArgb(200, 120, 60) };
        c.IrisC = irises[rnd.Next(irises.Length)];
        int[] outfits = { 1, 2, 4, 7, 8, 9, 2, 1 };
        c.Outfit = outfits[rnd.Next(outfits.Length)];
        Color[] cloth = { Color.FromArgb(70, 120, 220), Color.FromArgb(220, 70, 80), Color.FromArgb(60, 170, 110), Color.FromArgb(240, 190, 60), Color.FromArgb(150, 90, 210), Color.FromArgb(60, 64, 78), Color.FromArgb(245, 245, 248), Color.FromArgb(255, 130, 170) };
        c.C1 = cloth[rnd.Next(cloth.Length)];
        c.C2 = c.Outfit == 2 ? Color.FromArgb(60, 90, 150) : cloth[rnd.Next(cloth.Length)];
        if (c.Outfit == 9) c.C1 = Color.FromArgb(34, 32, 38);
        if (c.Outfit == 7) c.C2 = Color.FromArgb(255, 240, 120);
        int hr = rnd.Next(100);
        c.Hat = c.Outfit == 8 ? 5 : (hr < 25 ? 1 : (hr < 40 ? 2 : 0));
        c.HatC = cloth[rnd.Next(cloth.Length)]; c.HatBack = rnd.Next(2) == 0;
        c.Glasses = rnd.Next(100) < 12;
        c.Vape = rnd.Next(2) == 0;
        c.Speed = 1.1f + (float)rnd.NextDouble() * 0.6f;
        return c;
    }

    // ---- диван ----
    static readonly string[] Games = { "GTA VI", "Resident Evil Requiem", "Phantom Blade Zero", "Forza Horizon 6", "Pragmata", "Marvel's Wolverine", "Metro 2039", "Control Resonant" };
    static readonly string[] GameFontNames = { "Impact", "Georgia", "Georgia", "Arial Black", "Consolas", "Impact", "Consolas", "Arial Black" };
    static readonly FontStyle[] GameFontStyles = { FontStyle.Italic, FontStyle.Bold, FontStyle.Italic, FontStyle.Italic, FontStyle.Bold, FontStyle.Regular, FontStyle.Bold, FontStyle.Regular };
    static readonly Color[] GameA = { Color.FromArgb(255, 120, 200), Color.FromArgb(235, 50, 45), Color.FromArgb(250, 250, 250), Color.FromArgb(255, 185, 50), Color.FromArgb(210, 250, 255), Color.FromArgb(255, 225, 70), Color.FromArgb(225, 205, 150), Color.FromArgb(255, 70, 70) };
    static readonly Color[] GameB = { Color.FromArgb(255, 175, 60), Color.FromArgb(120, 0, 0), Color.FromArgb(210, 40, 40), Color.FromArgb(255, 90, 150), Color.FromArgb(80, 200, 255), Color.FromArgb(220, 160, 20), Color.FromArgb(150, 130, 80), Color.FromArgb(255, 255, 255) };
    static readonly Color[] GameBg = { Color.FromArgb(40, 20, 60), Color.FromArgb(18, 10, 10), Color.FromArgb(22, 22, 26), Color.FromArgb(20, 40, 85), Color.FromArgb(10, 15, 38), Color.FromArgb(20, 30, 65), Color.FromArgb(22, 28, 22), Color.FromArgb(12, 12, 12) };
    Font[] logoFonts;
    int tvState, gameIdx, menuSel, menuSteps, playLen;

    float StandX { get { return A + U(56); } }
    float HookahX { get { return A + U(38); } }

    void SwitchGame(bool byUser)
    {
        if (!consoleOn || tvState < 2) return;
        tvState = 2; tvT = 0; menuSteps = 3 + rnd.Next(5);
        if (me.Sitting) Say(byUser ? "О, давай что-то другое!" : (rnd.Next(2) == 0 ? "Давай другую!" : "Прошёл! Следующая!"), me.X, CatTop(me) - U(1), 'U');
    }

    void UpdateChill()
    {
        if (consoleOn)
        {
            tvT++;
            if (tvState == 1 && tvT > 60) { tvState = 2; tvT = 0; menuSteps = 4 + rnd.Next(5); menuSel = rnd.Next(Games.Length); }
            else if (tvState == 2 && tvT % 22 == 0)
            {
                if (menuSteps-- > 0) menuSel = (menuSel + 1) % Games.Length;
                else
                {
                    gameIdx = menuSel; tvState = 3; tvT = 0;
                    if (me.Sitting) Say("Погнали: " + Games[gameIdx] + "!", me.X, CatTop(me) - U(1), 'U');
                }
            }
            else if (tvState == 3 && tvT > 80) { tvState = 4; tvT = 0; playLen = 700 + rnd.Next(700); }
            else if (tvState == 4 && tvT > playLen && me.Sitting) SwitchGame(false);
        }
        switch (me.Phase)
        {
            case 0:
                if (enterT > 70) { Go(me, StandX + U(37)); me.Phase = 1; }
                break;
            case 1:
                if (!me.Walking) { me.Dir = 1; me.T = 15; me.Phase = 2; }
                break;
            case 2:
                if (--me.T <= 0)
                {
                    caseVisible = false; me.Hold = 'D';
                    Say("Новая игра!", me.X, CatTop(me) - U(1), 'U');
                    Go(me, StandX - U(2)); me.Phase = 3;
                }
                break;
            case 3:
                if (!me.Walking)
                {
                    me.Dir = 1; me.Hold = ' ';
                    PointF pw = Paw(me);
                    Fly('D', 'u', pw.X, pw.Y, StandX + U(5), Ground - U(17), 18, delegate { consoleOn = true; tvState = 1; tvT = 0; });
                    me.Phase = 4; me.T = 45;
                }
                break;
            case 4:
                if (--me.T <= 0) { Go(me, A + U(15)); me.Phase = 5; }
                break;
            case 5:
                if (!me.Walking)
                {
                    me.Phase = 6;
                    Jump(me, me.X, U(10), U(6), 14, delegate
                    {
                        me.Sitting = true; me.Dir = 1; me.Hold = 'J';
                        me.Phase = 7; me.T = 150 + rnd.Next(150);
                    });
                }
                break;
            case 7:
                if (--me.T <= 0 && me.PuffT == 0)
                {
                    me.PuffT = 90; me.T = 260 + rnd.Next(300);
                }
                if (tvState == 4 && rnd.Next(600) == 0)
                {
                    string[] gq = { "Ах, чёрт, опять начинается...", "Не открывай эту дверь!!!", "Мой клинок жаждет...", "Ни газа, ни тормоза — только дрифт", "В космосе даже вейп не дымит", "Бородатый и злой — это я", "Патроны — это деньги, Артём", "Это просто... холодильник?" };
                    string[] w = { "Изи!", "GG!", "Да ладно?!", "Вот это графон", gq[gameIdx], gq[gameIdx] };
                    Say(w[rnd.Next(w.Length)], me.X, CatTop(me) - U(1), 'U');
                    if (rnd.Next(2) == 0) me.HappyT = 40; else me.SweatT = 60;
                }
                break;
        }
    }

    // ---- табак ----
    float RackX { get { return A + U(76); } }
    float rackAngle; bool rackFalling, rackFallen, rackLifting, fallByUser, celebrated;
    int rackFallT, rackWobble, shakeT, lastMishapSlot = -1;
    List<float[]> dust = new List<float[]>();

    PointF SlotPos(int s)
    {
        int level = s / 5, k = s % 5;
        float py = 3 + level * 13;
        return new PointF(RackX + U(3 + k * 6 + 2), Ground - U(py + 1.75f));
    }
    float PackX(float[] p) { return A + U(p[0] + 1.5f); }
    float PackY(float[] p) { return Ground - U(p[1] + 1.75f); }

    void AddPileAt(float screenX, char col)
    {
        screenX = Math.Max(U(4), Math.Min(Width - U(4), screenX));
        int stack = 0;
        foreach (float[] p in pile) if (Math.Abs(PackX(p) - screenX) < U(2.6f)) stack++;
        int ci = Array.IndexOf(ItemColors, col); if (ci < 0) ci = 0;
        pile.Add(new float[] { (screenX - A) / u - 1.5f, Math.Min(stack, 2) * 3.4f, ci });
    }

    void Stars(PointF p)
    {
        for (int i = 0; i < 6; i++)
        {
            Particle s = new Particle();
            s.Kind = 4; s.X = p.X; s.Y = p.Y;
            double a = i * Math.PI / 3;
            s.VX = (float)Math.Cos(a) * 0.35f * u; s.VY = (float)Math.Sin(a) * 0.2f * u - 0.1f * u;
            s.Max = 35; s.Col = 'Y';
            parts.Add(s);
        }
    }

    bool RunScript()
    {
        if (script == null) return false;
        int guard = 0;
        while (script != null && scriptIdx < script.Count && guard++ < 12)
        {
            if (script[scriptIdx]()) scriptIdx++; else break;
        }
        if (script != null && scriptIdx >= script.Count) script = null;
        return true;
    }

    void UpdateTidy()
    {
        UpdateCar();
        if (reactCooldown > 0) reactCooldown--;
        if (rackWobble > 0 && !grabRack) { rackWobble--; rackAngle = (float)Math.Cos((40 - rackWobble) * 0.7) * wobbleAmp * rackWobble / 40f; if (rackWobble == 0) rackAngle = 0; }
        if (rackFalling)
        {
            rackFallT++;
            float t = rackFallT / 24f;
            rackAngle = 88f * t * t;
            if (t >= 1f) RackImpact();
        }
        if (rackLifting)
        {
            rackAngle -= 1.1f; me.Shake = 4;
            if (rackAngle <= 0) { rackAngle = 0; rackLifting = false; rackFallen = false; }
        }

        if (enterT < 40) return;
        if (RunScript()) return;
        if (rackFalling || rackLifting || grabRack) return;
        script = NextTidy(); scriptIdx = 0;
    }

    List<Func<bool>> NextTidy()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        if (deliveryPending) { deliveryPending = false; AddDelivery(s); return s; }
        if (carState != 0) { s.Add(Wait(5)); return s; }
        if (rackFallen) { AddLift(s); return s; }
        if (dust.Count > 0) { AddSweep(s); return s; }
        if (me.Hold == 'W')
        {
            s.Add(Do(delegate { PointF pw = Paw(me); Poof(pw.X, pw.Y); me.Hold = ' '; me.ItemRot = 0; }));
            s.Add(SayStep(me, "Чистота!", 25));
            return s;
        }
        foreach (float[] bxx in boxes) if (bxx[1] == 2) { AddUnbox(s, bxx); return s; }
        if (pile.Count > 0 && NextFreeSlot() >= 0) { AddStockOne(s); return s; }
        if (!celebrated)
        {
            celebrated = true;
            s.Add(Do(delegate { me.HappyT = 50; }));
            s.Add(SayStep(me, rnd.Next(2) == 0 ? "Порядок!" : "Красота!", 40));
            s.Add(Wait(150 + rnd.Next(250)));
            return s;
        }
        if (rnd.Next(100) < 45) { AddLean(s); return s; }
        s.Add(Do(delegate { if (me.PuffT == 0) me.PuffT = 90; }));
        s.Add(Wait(200 + rnd.Next(200)));
        return s;
    }

    void AddUnbox(List<Func<bool>> s, float[] box)
    {
        float bx = box[0];
        s.Add(GoStep(me, delegate { return Math.Min(Width - U(10), bx + U(10)); }));
        s.Add(Do(delegate { me.Dir = -1; }));
        string[] l = { "Так, что тут у нас...", "Вскрываем!", "Ещё коробка...", "О, новинки!" };
        s.Add(SayStep(me, l[rnd.Next(l.Length)], 30));
        s.Add(Do(delegate
        {
            box[1] = 3;
            for (int i = 0; i < 5; i++)
            {
                char cc = ItemColors[rnd.Next(ItemColors.Length)];
                float tx = Math.Max(U(4), Math.Min(Width - U(4), bx + U(-16 + (float)rnd.NextDouble() * 22)));
                Fly('X', cc, bx, Ground - U(6), tx, Ground - U(1.75f), 14 + i * 3, delegate { AddPileAt(tx, cc); });
            }
        }));
        s.Add(Wait(14 + 5 * 3 + 5));
        s.Add(Do(delegate { boxes.Remove(box); Poof(bx, Ground - U(4)); }));
    }

    void AddStockOne(List<Func<bool>> s)
    {
        int bi = 0; float bd = float.MaxValue;
        for (int i = 0; i < pile.Count; i++)
        {
            float d = Math.Abs(PackX(pile[i]) - me.X) - pile[i][1] * 0.1f;
            if (d < bd) { bd = d; bi = i; }
        }
        float[] pk = pile[bi];
        float px = PackX(pk);
        char col = ItemColors[(int)pk[2]];
        bool fromRight = me.X >= px;
        float stand = fromRight ? px + U(8.5f) : px - U(8.5f);
        stand = Math.Max(U(10), Math.Min(Width - U(10), stand));
        bool mishap = rnd.Next(100) < 20;
        s.Add(GoStep(me, delegate { return stand; }));
        s.Add(Do(delegate { me.Dir = fromRight ? -1 : 1; }));
        s.Add(Wait(8));
        s.Add(Do(delegate { if (pile.Contains(pk)) { pile.Remove(pk); me.Hold = 'X'; me.HoldCol = col; } }));
        s.Add(delegate { if (me.Hold != 'X') script = null; return true; });
        s.Add(GoStep(me, delegate { return RackX - U(11); }));
        s.Add(Do(delegate { me.Dir = 1; }));
        s.Add(Do(delegate
        {
            lastMishapSlot = -1;
            int slot = NextFreeSlot();
            if (me.Hold != 'X' || rackFallen || rackFalling || slot < 0) return;
            slotRes[slot] = true;
            char cc = me.HoldCol;
            me.Hold = ' ';
            PointF pw = Paw(me), sp = SlotPos(slot);
            Fly('X', cc, pw.X, pw.Y, sp.X, sp.Y, 16, delegate { slots[slot] = cc; slotRes[slot] = false; });
            if (mishap && slot >= 5) lastMishapSlot = slot;
        }));
        s.Add(Wait(22));
        if (mishap)
        {
            s.Add(Wait(15));
            s.Add(Do(delegate
            {
                int ms = lastMishapSlot;
                if (ms < 0 || slots[ms] == ' ' || rackFallen || rackFalling) return;
                char cc = slots[ms];
                slots[ms] = ' ';
                PointF sp = SlotPos(ms), hd = Local(me, 1, -24);
                me.BlinkT = 10;
                Fly('X', cc, sp.X, sp.Y, hd.X, hd.Y, 12, delegate
                {
                    Stars(hd); me.Shake = 30; me.SweatT = 70; Hurt(12);
                    Say(rnd.Next(100) < 35 ? "Д'оу!" : "Ай!", me.X, CatTop(me) - U(2), 'R');
                    AddPileAt(me.X + U((float)rnd.NextDouble() * 10 - 5), cc);
                });
            }));
            s.Add(Wait(45));
        }
    }

    void AddLean(List<Func<bool>> s)
    {
        s.Add(SayStep(me, "Фух... можно и передохнуть", 40));
        s.Add(GoStep(me, delegate { return RackX - U(9); }));
        s.Add(Do(delegate { me.Dir = 1; wobbleAmp = 3; rackWobble = 40; }));
        s.Add(Wait(45));
        s.Add(Do(delegate { me.SweatT = 40; }));
        s.Add(SayStep(me, "Ой...", 12));
        s.Add(Do(delegate { StartRackFall(false); }));
    }

    void StartRackFall(bool byUser)
    {
        if (rackFalling || rackFallen || rackLifting) return;
        fallByUser = byUser; rackFalling = true; rackFallT = 0; rackWobble = 0;
        flyers.RemoveAll(delegate (Flyer f) { return f.Kind == 'X'; });
        for (int i = 0; i < 15; i++) slotRes[i] = false;
        grabRack = false;
        if (me.Hold == 'X') { AddPileAt(Paw(me).X, me.HoldCol); me.Hold = ' '; }
        script = null;
    }

    void RackImpact()
    {
        rackFalling = false; rackFallen = true; rackAngle = 88; celebrated = false; shakeT = 16;
        float pvx = RackX + U(34), pvy = Ground;
        double a = 88 * Math.PI / 180;
        float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
        for (int s = 0; s < 15; s++)
        {
            if (slots[s] == ' ') continue;
            PointF sp = SlotPos(s);
            float dx = sp.X - pvx, dy = sp.Y - pvy;
            float x1 = pvx + dx * ca - dy * sa, y1 = pvy + dx * sa + dy * ca;
            float tx = rnd.Next(100) < 65 ? pvx + U(3 + (float)rnd.NextDouble() * 42) : RackX - U(10 + (float)rnd.NextDouble() * 22);
            tx = Math.Max(U(4), Math.Min(Width - U(4), tx));
            char cc = slots[s];
            Fly('X', cc, x1, y1, tx, Ground - U(1.75f), 14 + rnd.Next(14), delegate { AddPileAt(tx, cc); });
            slots[s] = ' ';
        }
        for (int i = 0; i < 15; i++) slotRes[i] = false;
        for (int k = 0; k < 4; k++)
            dust.Add(new float[] { Math.Max(U(6), Math.Min(Width - U(6), pvx + U(-20 + (float)rnd.NextDouble() * 60))), 2.2f + (float)rnd.NextDouble() * 1.6f });
        for (int k = 0; k < 6; k++) Poof(pvx + U(k * 7), Ground - U(3));
        Say(fallByUser ? "Эй! Ты чё творишь?!" : "ОЙ-ОЙ-ОЙ!", me.X, CatTop(me) - U(2), 'R');
        if (rnd.Next(100) < 55) BigText("ПОТРАЧЕНО", 1, 110);
        else { List<Func<bool>> e = new List<Func<bool>>(); e.Add(Wait(60)); e.Add(Line(me, "Шёл, упал, очнулся — гипс...", 50, 'w')); egg = e; eggIdx = 0; }
        me.SweatT = 120; me.Shake = 30;
        if (me.X > RackX - U(12) && me.X < RackX + U(80)) Hurt(30);
        if (!me.Jumping && !me.Sitting) Jump(me, me.X, 0, U(5), 14, null);
    }

    void AddLift(List<Func<bool>> s)
    {
        s.Add(SayStep(me, "Так... поднимаем", 25));
        s.Add(GoStep(me, delegate { return Math.Min(Width - U(10), RackX + U(80)); }));
        s.Add(Do(delegate { me.Dir = -1; }));
        s.Add(SayStep(me, "Хы-ы-ып!", 15));
        s.Add(Do(delegate { rackLifting = true; }));
        s.Add(delegate { return !rackLifting; });
        s.Add(Do(delegate { me.Shake = 0; me.HappyT = 40; }));
        s.Add(SayStep(me, "Фух!", 20));
    }

    void AddSweep(List<Func<bool>> s)
    {
        int bi = 0; float bd = float.MaxValue;
        for (int i = 0; i < dust.Count; i++) { float d = Math.Abs(dust[i][0] - me.X); if (d < bd) { bd = d; bi = i; } }
        float[] dd = dust[bi];
        if (me.Hold != 'W')
        {
            s.Add(Do(delegate { me.Hold = 'W'; me.HoldCol = 'B'; PointF pw = Paw(me); Poof(pw.X, pw.Y); }));
            s.Add(SayStep(me, "Где моя метла... ага!", 30));
        }
        bool sneeze = rnd.Next(100) < 35;
        s.Add(GoStep(me, delegate { return Math.Max(U(10), dd[0] - U(9)); }));
        s.Add(Do(delegate { me.Dir = 1; }));
        s.Add(delegate
        {
            if (!dust.Contains(dd)) { me.ItemRot = 0; return true; }
            me.ItemRot = (float)Math.Sin(tick * 0.5) * 28;
            dd[1] -= 0.035f;
            if (tick % 3 == 0)
            {
                Particle p = new Particle();
                p.Kind = 5; p.X = dd[0] + U((float)rnd.NextDouble() * 4 - 2); p.Y = Ground - U(1);
                p.VX = (0.2f + (float)rnd.NextDouble() * 0.4f) * u; p.VY = -(0.1f + (float)rnd.NextDouble() * 0.25f) * u;
                p.R = U(0.8f + (float)rnd.NextDouble()); p.Max = 30;
                parts.Add(p);
            }
            if (dd[1] <= 0.3f) { dust.Remove(dd); me.ItemRot = 0; return true; }
            return false;
        });
        if (sneeze)
        {
            s.Add(Do(delegate { me.BlinkT = 25; }));
            s.Add(Wait(20));
            s.Add(Do(delegate { PointF m = Local(me, 4, -15.5f); Poof(m.X, m.Y); me.Shake = 20; Say("Апчхи!", me.X, CatTop(me) - U(2), 'U'); }));
            s.Add(Wait(30));
        }
    }

    // клики по сцене. true - клик обработан
    bool SceneClick(Point pt)
    {
        if (pendingMode >= 0) return false;
        if (WxVisible && WidgetRect().Contains(pt)) { FetchWeather(); string wr = WeatherRemark(); if (wr != null) Say(wr, me.X, CatTop(me) - U(1), 'U'); return true; }
        if (mode == 2 && R(StandX, Ground, 9, 10.4f, 28, 20).Contains(pt)) { SwitchGame(true); return true; }
        if (mode == 3)
        {
            if (!rackFallen && !rackFalling && NextFreeSlot() >= 0)
            {
                for (int i = pile.Count - 1; i >= 0; i--)
                {
                    float[] pk = pile[i];
                    if (Math.Abs(PackX(pk) - pt.X) < U(2) && Math.Abs(PackY(pk) - pt.Y) < U(2.2f))
                    {
                        pile.RemoveAt(i);
                        int slot = NextFreeSlot();
                        slotRes[slot] = true;
                        char cc = ItemColors[(int)pk[2]];
                        PointF sp = SlotPos(slot);
                        Fly('X', cc, PackX(pk), PackY(pk), sp.X, sp.Y, 20, delegate { slots[slot] = cc; slotRes[slot] = false; });
                        if (rnd.Next(2) == 0) Say(rnd.Next(2) == 0 ? "О, спасибо! <3" : "Помощник!", me.X, CatTop(me) - U(1), 'U');
                        me.HappyT = 40;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // ---- вечеринка ----
    float DoorX { get { return A + U(55); } }
    float PoolX { get { return A + U(132); } }
    float PoolR { get { return PoolX + U(60); } }
    float LoungerX { get { return A + U(200); } }

    void UpdateParty()
    {
        if (guestsSpawned < 4 && enterT == 70 + guestsSpawned * 35)
        {
            Cat c = NewGuest();
            c.X = DoorX; c.Vape = rnd.Next(3) > 0;
            if (rnd.Next(100) < 45) { c.Hat = 3; c.HatC = ItemColors.Length > 0 ? pal[ItemColors[rnd.Next(ItemColors.Length)]] : Color.Red; }
            if (rnd.Next(100) < 40) { c.Outfit = 7; c.C2 = Color.FromArgb(255, 240, 120); }
            c.Phase = 0; c.T = 30;
            Go(c, DoorX + U(rnd.Next(2) == 0 ? -20 : 20));
            guests.Add(c);
            doorOpen = 25;
            guestsSpawned++;
        }
        if (enterT > 40 && tick % 28 == 0)
        {
            Particle p = new Particle();
            p.Kind = 2; p.X = A + U(119.5f); p.Y = Ground - U(21); p.VY = -0.33f * u; p.Max = 90;
            p.Col = ItemColors[rnd.Next(ItemColors.Length)];
            parts.Add(p);
        }
        if (enterT > 40) PartyCat(me);
        foreach (Cat g in guests) PartyCat(g);
    }

    void PartyCat(Cat c)
    {
        if (c.Jumping) return;
        switch (c.Phase)
        {
            case 0:
                if (!c.Walking && --c.T <= 0) PartyNext(c);
                break;
            case 1:
                if (!c.Walking)
                {
                    float x1 = PoolX + U(10) + (float)rnd.NextDouble() * U(40);
                    c.Phase = 2;
                    Jump(c, x1, U(3.5f), U(16), 26, delegate
                    {
                        c.Swimming = true; c.Speed = 0.5f; c.T = 250 + rnd.Next(300);
                        if (rnd.Next(100) < 18) Say("С лёгким паром!", c.X, CatTop(c) - U(1), 'K');
                        Splash(c.X);
                    });
                }
                break;
            case 2:
                if (!c.Swimming) break;
                c.T--;
                if (!c.Walking) Go(c, PoolX + U(10) + (float)rnd.NextDouble() * U(40));
                if (c.T <= 0)
                {
                    bool left = (c.X - PoolX) < (PoolR - c.X);
                    Go(c, left ? PoolX + U(9) : PoolR - U(9));
                    c.Phase = 3;
                }
                break;
            case 3:
                if (!c.Walking)
                {
                    bool goLeft = (c.X - PoolX) < (PoolR - c.X);
                    c.Swimming = false;
                    Splash(c.X);
                    c.Phase = 0; c.T = 40;
                    Jump(c, goLeft ? PoolX - U(10) : PoolR + U(10), 0, U(16), 24, delegate { c.Speed = 1.3f; });
                }
                break;
            case 4:
                c.T--;
                c.Y = Math.Abs((float)Math.Sin(c.T * 0.25f)) * U(3);
                if (c.T % 20 == 0) c.Dir = -c.Dir;
                if (c.T <= 0) { c.Y = 0; PartyNext(c); }
                break;
            case 5:
                if (!c.Walking)
                {
                    c.Phase = 6;
                    Jump(c, c.X, U(5), U(4), 12, delegate { c.Sitting = true; c.Dir = -1; c.T = 250 + rnd.Next(250); });
                }
                break;
            case 6:
                if (!c.Sitting) break;
                if (--c.T <= 0)
                {
                    c.Sitting = false; c.Phase = 0; c.T = 20;
                    Jump(c, c.X - U(12), 0, U(5), 14, delegate { loungerBusy = false; });
                }
                else if (c.Vape && c.PuffT == 0 && rnd.Next(250) == 0) c.PuffT = 90;
                break;
        }
    }

    void PartyNext(Cat c)
    {
        int r = rnd.Next(100);
        if (r < 30)
        {
            bool left = Math.Abs(c.X - PoolX) < Math.Abs(c.X - PoolR);
            c.Phase = 1; Go(c, left ? PoolX - U(10) : PoolR + U(10));
            return;
        }
        if (r < 48) { c.Phase = 4; c.T = 160; c.Walking = false; return; }
        if (r < 62 && !loungerBusy) { loungerBusy = true; c.Phase = 5; Go(c, LoungerX + U(7)); return; }
        if (r < 77 && c.Vape) { c.PuffT = 90; c.Phase = 0; c.T = 100; c.Walking = false; return; }
        c.Phase = 0; c.T = 40 + rnd.Next(120);
        Go(c, A + U(10) + (float)rnd.NextDouble() * U(112));
    }

    // ================= сценарий: облава ОМОН =================
    List<Cat> omons = new List<Cat>(), inmates = new List<Cat>();
    Cat raidSeller, raidCustomer, guard;
    int omonPhase, jailEnterT, sirenT, vanState, tally, barsShake;
    float vanX, vanSpeed;
    float VanStop { get { return A + U(128); } }
    float VanDoor { get { return vanX + U(20); } }
    float JailX { get { return A + U(30); } }

    Cat NewOmon()
    {
        Cat o = NewGuest();
        o.Outfit = 11; o.C1 = Color.FromArgb(50, 56, 68); o.C2 = Color.FromArgb(34, 37, 46);
        o.Hat = 6; o.HatC = Color.FromArgb(42, 48, 62); o.Glasses = false; o.Vape = false; o.Speed = 3.6f;
        return o;
    }

    void SetupRaid()
    {
        omonPhase = 0; sirenT = 0; vanState = 0; barsShake = 0; tally = 1;
        omons.Clear(); inmates.Clear(); guard = null;
        raidSeller = NewGuest();
        raidSeller.Outfit = 5; raidSeller.C1 = Color.FromArgb(70, 120, 210); raidSeller.Hat = 0; raidSeller.Glasses = false; raidSeller.Vape = true;
        raidSeller.X = ShelfX(0) + U(8); raidSeller.Dir = 1;
        raidCustomer = NewGuest();
        raidCustomer.X = CustFront; raidCustomer.Dir = -1;
        guests.Add(raidSeller); guests.Add(raidCustomer);
        Poof(me.X, CatTop(me) + U(8));
        me.X = CashX; me.Dir = 1;
        script = BuildRaid(); scriptIdx = 0;
    }

    void StartRaid()
    {
        sirenT = 700; shakeT = 10;
        Cat[] targets = { raidCustomer, me, raidSeller };
        for (int i = 0; i < 3; i++)
        {
            Cat o = NewOmon();
            o.X = Width + U(8 + i * 12); o.Dir = -1;
            guests.Add(o); omons.Add(o);
            Go(o, targets[i].X + U(10));
        }
        foreach (Cat t in targets) { t.Shake = 25; t.SweatT = 200; t.Walking = false; t.Hold = ' '; t.PuffT = 0; }
    }

    bool AllStopped(List<Cat> list) { foreach (Cat c in list) if (c.Walking || c.Jumping) return false; return true; }

    List<Func<bool>> BuildRaid()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        Cat col = raidSeller, cust = raidCustomer;
        List<Cat> targets = new List<Cat> { cust, me, col };
        s.Add(Wait(45));
        s.Add(SayStep(cust, "Мне что-нибудь на арбузе", 40));
        s.Add(SayF(delegate { return col; }, "Ща подберём!", 25, 'U'));
        s.Add(Do(delegate { Go(col, ShelfX(1)); }));
        s.Add(Wait(20));
        s.Add(Do(delegate { StartRaid(); }));
        s.Add(SayF(delegate { return omons[0]; }, "ОМОН! ВСЕМ ЛЕЖАТЬ!!!", 40, 'R'));
        s.Add(delegate { return AllStopped(omons); });
        s.Add(Do(delegate
        {
            foreach (Cat t in targets) { t.Lying = true; t.Cuffed = true; t.Walking = false; t.Hold = ' '; Poof(t.X + t.Dir * U(12), Ground - U(2)); }
            Hurt(25);
            foreach (Cat o in omons) o.Dir = -1;
            shakeT = 10;
        }));
        if (rnd.Next(100) < 45) s.Add(Do(delegate { BigText("ВЫ ЗАДЕРЖАНЫ", 0, 110); }));
        s.Add(SayF(delegate { return omons[1]; }, "Лицом в пол! Руки за спину!", 45, 'R'));
        s.Add(Line(me, "Это не моё! Я просто на кассе стою!", 55, 'w'));
        s.Add(SayF(delegate { return omons[1]; }, "Разберёмся.", 30, 'K'));
        s.Add(Line(col, "У нас все документы есть! Честно-честно!", 55, 'w'));
        s.Add(Line(cust, "Я вообще за кефиром зашёл!!!", 50, 'w'));
        s.Add(SayF(delegate { return omons[0]; }, "Ага. В вейп-шоп. За кефиром.", 50, 'K'));
        s.Add(Line(me, "Начальник, это жижа со вкусом печенья...", 55, 'w'));
        s.Add(Line(col, "Можно я маме позвоню?", 45, 'w'));
        s.Add(Line(cust, "Я вообще не с ними!", 40, 'w'));
        s.Add(SayF(delegate { return omons[2]; }, "Все вопросы — в отделении. В автозак!", 50, 'R'));
        s.Add(Do(delegate { vanState = 1; vanX = Width + U(4); sirenT = 600; }));
        s.Add(delegate { return vanState == 2; });
        s.Add(Do(delegate
        {
            for (int i = 0; i < targets.Count; i++) { Cat t = targets[i]; t.Lying = false; t.Speed = 1.0f; Go(t, VanDoor + U(i * 2)); }
            for (int i = 0; i < omons.Count; i++) { omons[i].Speed = 1.0f; Go(omons[i], VanDoor + U(10 + i * 4)); }
        }));
        s.Add(Line(cust, "Я буду жаловаться!", 35, 'a'));
        s.Add(delegate { return AllStopped(targets); });
        s.Add(Do(delegate { foreach (Cat t in targets) { t.Hidden = true; } Poof(VanDoor, Ground - U(8)); }));
        s.Add(delegate { return AllStopped(omons); });
        s.Add(Do(delegate { foreach (Cat o in omons) o.Hidden = true; }));
        s.Add(Wait(20));
        s.Add(Do(delegate { vanState = 3; vanSpeed = 0; }));
        s.Add(delegate { return vanState == 0; });
        s.Add(Do(delegate { GoToJail(targets); }));
        s.Add(Wait(35));
        s.Add(Line(me, "Ну... приехали.", 45, 'w'));
        return s;
    }

    void GoToJail(List<Cat> targets)
    {
        omonPhase = 1; jailEnterT = 0; sirenT = 0;
        for (int k = 0; k < 6; k++) Poof(A + U(10 + k * 18), Ground - U(10));
        guests.Clear(); omons.Clear(); inmates.Clear();
        int i = 0;
        foreach (Cat c in new Cat[] { me, raidSeller, raidCustomer })
        {
            c.Hidden = false; c.Lying = false; c.Cuffed = false; c.Walking = false; c.Hold = ' ';
            c.Outfit = 10; c.C1 = Color.FromArgb(245, 245, 245); c.C2 = Color.FromArgb(70, 72, 80);
            c.Hat = 0; c.Glasses = false; c.Vape = false; c.Speed = 1.0f;
            c.X = JailX + U(20 + i * 24); c.Y = U(7); c.Sitting = true; c.Dir = (i % 2 == 0) ? 1 : -1;
            inmates.Add(c);
            if (c != me) guests.Add(c);
            i++;
        }
        guard = NewOmon(); guard.X = JailX + U(134); guard.Dir = -1; guard.Speed = 0.8f;
        guests.Add(guard);
        script = null;
    }

    static readonly string[] JailQuips = {
        "Ну и что теперь?", "Зато обед бесплатный", "Я же говорил: открываем шаурмичную!", "Мам, забери меня...",
        "Интересно, касса закрылась?", "Я вообще за кефиром заходил!!!", "Кто-нибудь умеет копать ложкой?",
        "Давайте в города? Москва.", "Анапа.", "...Апатиты", "Даже вейп отобрали...", "Скучаю по жиже с печеньем",
        "А тут вай-фай есть?", "Адвоката мне! Кошачьего!", "Сидим ровно, пацаны", "Это всё из-за арбузной..." };

    const string JailActs = "wwawnswnhnnwnaha";

    List<Func<bool>> NextJail()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        if (inmates.Count == 0) { s.Add(Wait(50)); return s; }
        Cat c = inmates[rnd.Next(inmates.Count)];
        int r = rnd.Next(100);
        if (r < 22)
        {
            s.Add(Do(delegate { Jump(c, c.X, 0, U(3), 10, delegate { c.Sitting = false; }); c.Sitting = false; }));
            s.Add(delegate { return !c.Jumping; });
            s.Add(Do(delegate { c.Dir = 1; barsShake = 35; c.Shake = 35; c.AngryT = 70; }));
            s.Add(SayStep(c, rnd.Next(2) == 0 ? "Выпустите! Я больше не буду!" : "Я требую звонок!", 50));
            if (guard != null)
            {
                Cat gd = guard;
                s.Add(Do(delegate { gd.AngryT = 50; gd.Dir = -1; }));
                s.Add(SayF(delegate { return gd; }, rnd.Next(2) == 0 ? "Тихо там!" : "Отбой был!", 40, 'R'));
            }
            s.Add(Do(delegate { Jump(c, c.X, U(7), U(4), 12, delegate { c.Sitting = true; }); }));
            s.Add(delegate { return !c.Jumping; });
        }
        else if (r < 32)
        {
            s.Add(Do(delegate { tally++; }));
            s.Add(Line(c, "День " + (tally + 1) + "...", 45, 'w'));
        }
        else if (r < 42 && guard != null)
        {
            Cat gd = guard;
            float gx = JailX + U(126 + (float)rnd.NextDouble() * 12);
            s.Add(GoStep(gd, delegate { return gx; }));
            s.Add(Do(delegate { gd.Dir = -1; }));
        }
        else
        {
            int qi = rnd.Next(JailQuips.Length);
            string q = JailQuips[qi];
            char act = JailActs[qi];
            s.Add(Line(c, q, 50, act));
            if (rnd.Next(100) < 40)
            {
                Cat c2 = inmates[rnd.Next(inmates.Count)];
                if (c2 != c)
                {
                    string[] answers = { "Ага...", "Не начинай", "Ха-ха", "Сам такой", "Согласен", "Тише ты" };
                    s.Add(Line(c2, answers[rnd.Next(answers.Length)], 35, 'n'));
                }
            }
        }
        s.Add(Wait(50 + rnd.Next(100)));
        return s;
    }

    void UpdateOmon()
    {
        if (sirenT > 0) sirenT--;
        if (barsShake > 0) barsShake--;
        if (vanState == 1)
        {
            float d = vanX - VanStop;
            vanX -= Math.Max(U(0.3f), d * 0.05f);
            if (tick % 3 == 0) Exhaust(vanX + U(54), Ground - U(3), 1);
            if (vanX - VanStop < U(0.3f)) { vanX = VanStop; vanState = 2; }
        }
        else if (vanState == 3)
        {
            vanSpeed += 0.04f * u; vanX += vanSpeed;
            if (tick % 2 == 0) Exhaust(vanX + U(54), Ground - U(3), 1);
            if (vanX > Width + U(4)) vanState = 0;
        }
        if (omonPhase == 1) jailEnterT++;
        if (enterT < 5) return;
        if (RunScript()) return;
        if (omonPhase == 1) { script = NextJail(); scriptIdx = 0; }
    }

    void Exhaust(float x, float y, int dir)
    {
        Particle p = new Particle();
        p.Kind = 5; p.X = x; p.Y = y;
        p.VX = dir * (0.2f + (float)rnd.NextDouble() * 0.3f) * u; p.VY = -(0.05f + (float)rnd.NextDouble() * 0.1f) * u;
        p.R = U(0.8f + (float)rnd.NextDouble()); p.Max = 30;
        parts.Add(p);
    }

    // ---- рисование: автозак, камера, мигалки ----
    PointF V(float x0, float x, float y) { return new PointF(x0 + U(x), Ground - U(y)); }

    void DrawWheel(Graphics g, float cx, float cy, float r, float rot)
    {
        using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 60, 62))) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
        using (Pen p = new Pen(InkC, Math.Max(1.2f, r * 0.16f))) g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2);
        using (Pen p = new Pen(Color.FromArgb(80, 255, 250, 240), Math.Max(1f, r * 0.1f))) g.DrawArc(p, cx - r * 0.8f, cy - r * 0.8f, r * 1.6f, r * 1.6f, 205, 70);
        float rr = r * 0.58f;
        using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 224, 210))) g.FillEllipse(b, cx - rr, cy - rr, rr * 2, rr * 2);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 172, 162))) g.FillPie(b, cx - rr, cy - rr, rr * 2, rr * 2, 10, 160);
        using (Pen p = new Pen(InkC, Math.Max(1f, r * 0.1f)))
        {
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            g.DrawEllipse(p, cx - rr, cy - rr, rr * 2, rr * 2);
            for (int i = 0; i < 5; i++)
            {
                double a = rot + i * Math.PI * 2 / 5;
                g.DrawLine(p, cx + (float)Math.Cos(a) * rr * 0.3f, cy + (float)Math.Sin(a) * rr * 0.3f, cx + (float)Math.Cos(a) * rr * 0.92f, cy + (float)Math.Sin(a) * rr * 0.92f);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(128, 116, 112))) g.FillEllipse(b, cx - rr * 0.32f, cy - rr * 0.32f, rr * 0.64f, rr * 0.64f);
            g.DrawEllipse(p, cx - rr * 0.32f, cy - rr * 0.32f, rr * 0.64f, rr * 0.64f);
        }
    }

    // стекло с бликами и контуром
    void CarGlass(Graphics g, PointF[] pts, Pen ink)
    {
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddPolygon(pts);
            RectangleF r = p.GetBounds();
            using (SolidBrush b = new SolidBrush(Color.FromArgb(176, 212, 224))) g.FillPath(b, p);
            GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 168, 190))) g.FillRectangle(b, r.X, r.Y + r.Height * 0.62f, r.Width, r.Height * 0.4f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
            {
                g.FillPolygon(b, new PointF[] { new PointF(r.X + r.Width * 0.14f, r.Bottom), new PointF(r.X + r.Width * 0.36f, r.Y), new PointF(r.X + r.Width * 0.5f, r.Y), new PointF(r.X + r.Width * 0.28f, r.Bottom) });
                g.FillPolygon(b, new PointF[] { new PointF(r.X + r.Width * 0.4f, r.Bottom), new PointF(r.X + r.Width * 0.62f, r.Y), new PointF(r.X + r.Width * 0.67f, r.Y), new PointF(r.X + r.Width * 0.45f, r.Bottom) });
            }
            g.Restore(cs);
            g.DrawPath(ink, p);
        }
    }

    void CarArch(Graphics g, float cx, float r)
    {
        using (SolidBrush b = new SolidBrush(Color.FromArgb(74, 56, 54))) g.FillPie(b, cx - r, Ground - U(3.4f) - r, r * 2, r * 2, 180, 180);
    }

    void DrawPoliceVan(Graphics g)
    {
        float x0 = vanX, lw = Math.Max(1.4f, U(0.55f));
        Shadow(g, x0 + U(27), U(56));
        Color wc = Color.FromArgb(248, 244, 234), wd = Color.FromArgb(214, 210, 204);
        using (Pen ink = new Pen(InkC, lw))
        using (Pen thin = new Pen(Color.FromArgb(210, InkC), Math.Max(1f, U(0.32f))))
        {
            ink.LineJoin = LineJoin.Round; ink.StartCap = LineCap.Round; ink.EndCap = LineCap.Round;
            PointF[] body = { V(x0, 0, 3.4f), V(x0, 0, 9.5f), V(x0, 2, 10.8f), V(x0, 7.6f, 20.8f), V(x0, 9.4f, 22.4f), V(x0, 52.8f, 22.4f), V(x0, 54.4f, 20.8f), V(x0, 54.4f, 3.4f) };
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(body);
                using (SolidBrush b = new SolidBrush(wc)) g.FillPath(b, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(wd)) g.FillRectangle(b, x0 - U(1), Ground - U(7.2f), U(57), U(5));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(78, 130, 204))) g.FillRectangle(b, x0, Ground - U(11.6f), U(56), U(3.4f));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(56, 104, 176))) g.FillRectangle(b, x0, Ground - U(9.2f), U(56), U(1f));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 84, 84))) g.FillRectangle(b, x0, Ground - U(8.1f), U(56), U(0.9f));
                CarArch(g, x0 + U(10), U(4.7f)); CarArch(g, x0 + U(44), U(4.7f));
                g.Restore(cs);
                g.DrawPath(ink, p);
            }
        using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString("ПОЛИЦИЯ", fntSign, b, new RectangleF(x0 + U(26), Ground - U(11.6f), U(26), U(3.6f)), sf); }
            CarGlass(g, new PointF[] { V(x0, 3.0f, 11.6f), V(x0, 8.0f, 19.8f), V(x0, 13.4f, 19.8f), V(x0, 13.4f, 11.6f) }, thin);
            // дверь и окошко с решёткой
            using (GraphicsPath p = RoundRect(x0 + U(15), Ground - U(20.6f), U(10), U(16.6f), U(0.8f))) g.DrawPath(thin, p);
            RectangleF w2 = new RectangleF(x0 + U(16.2f), Ground - U(19.6f), U(7.6f), U(6.4f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(84, 100, 120))) g.FillRectangle(b, w2);
            using (Pen pp = new Pen(Color.FromArgb(206, 200, 190), Math.Max(1, U(0.4f)))) for (int i = 1; i < 4; i++) g.DrawLine(pp, w2.X + w2.Width * i / 4, w2.Y, w2.X + w2.Width * i / 4, w2.Bottom);
            g.DrawRectangle(thin, w2.X, w2.Y, w2.Width, w2.Height);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 110, 108))) g.FillRectangle(b, x0 + U(22.6f), Ground - U(11.0f) - U(2.6f), U(1.6f), U(0.6f));
            // боковая дверь кузова, ручки, зеркало
            g.DrawLine(thin, x0 + U(34), Ground - U(21.4f), x0 + U(34), Ground - U(12.2f)); g.DrawLine(thin, x0 + U(34), Ground - U(7.6f), x0 + U(34), Ground - U(4.4f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 110, 108))) g.FillRectangle(b, x0 + U(35), Ground - U(14.4f), U(1.8f), U(0.6f));
            using (GraphicsPath p = RoundRect(x0 + U(5.0f), Ground - U(13.6f), U(1.6f), U(2.4f), U(0.6f))) { using (SolidBrush b = new SolidBrush(wd)) g.FillPath(b, p); g.DrawPath(thin, p); }
            // фара, бампер, подножка
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 240, 180))) g.FillEllipse(b, x0 + U(0.3f), Ground - U(8.8f), U(1.9f), U(1.9f));
            g.DrawEllipse(thin, x0 + U(0.3f), Ground - U(8.8f), U(1.9f), U(1.9f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 84, 84))) g.FillRectangle(b, x0 + U(53.4f), Ground - U(9.6f), U(1f), U(2.6f));
            using (GraphicsPath p = RoundRect(x0 - U(0.6f), Ground - U(4.6f), U(55.6f), U(2f), U(0.9f))) { using (SolidBrush b = new SolidBrush(Color.FromArgb(104, 96, 96))) g.FillPath(b, p); g.DrawPath(ink, p); }
            // мигалка
            bool f = (tick / 6) % 2 == 0;
            Color red = Color.FromArgb(f ? 255 : 150, 230, 70, 70), blue = Color.FromArgb(f ? 150 : 255, 70, 120, 240);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 236, 226))) g.FillRectangle(b, x0 + U(20), Ground - U(24.2f), U(12), U(1.9f));
            using (SolidBrush b = new SolidBrush(red)) g.FillRectangle(b, x0 + U(20), Ground - U(24.2f), U(6), U(1.9f));
            using (SolidBrush b = new SolidBrush(blue)) g.FillRectangle(b, x0 + U(26), Ground - U(24.2f), U(6), U(1.9f));
            using (GraphicsPath p = RoundRect(x0 + U(20), Ground - U(24.2f), U(12), U(1.9f), U(0.6f))) g.DrawPath(thin, p);
            if (sirenT > 0)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(f ? 90 : 30, 255, 70, 70))) g.FillEllipse(b, x0 + U(12), Ground - U(31), U(16), U(14));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(f ? 30 : 90, 80, 130, 255))) g.FillEllipse(b, x0 + U(24), Ground - U(31), U(16), U(14));
            }
        }
        float rot = vanX / U(3.6f);
        DrawWheel(g, x0 + U(10), Ground - U(3.6f), U(3.6f), rot);
        DrawWheel(g, x0 + U(44), Ground - U(3.6f), U(3.6f), rot);
    }

    void DrawSiren(Graphics g)
    {
        bool f = (tick / 6) % 2 == 0;
        RectangleF r = new RectangleF(A - U(10), Ground - U(60), U(220), U(70));
        using (SolidBrush b = new SolidBrush(f ? Color.FromArgb(28, 255, 30, 40) : Color.FromArgb(28, 40, 90, 255))) g.FillEllipse(b, r);
    }

    float JailA() { float a = Math.Min(1f, jailEnterT / 20f); return 1 - (1 - a) * (1 - a); }

    void DrawJailBack(Graphics g)
    {
        float by = Ground + (1 - JailA()) * U(62);
        float ox = JailX;
        Shadow(g, ox + U(60), U(124));
        Panel(g, Color.FromArgb(128, 132, 142), ox, by, 0, 0, 120, 54, 0.6f);
        RectangleF wr = R(ox, by, 0, 0, 120, 54);
        GraphicsState cs = g.Save();
        g.SetClip(wr);
        using (Pen p = new Pen(Color.FromArgb(40, 30, 30, 40), Math.Max(1, U(0.25f))))
            for (int row = 0; row < 14; row++)
            {
                float y = by - U(3 + row * 3.8f);
                g.DrawLine(p, ox, y, ox + U(120), y);
                for (int k = 0; k < 16; k++) { float x = ox + U(k * 8 + (row % 2) * 4); g.DrawLine(p, x, y, x, y - U(3.8f)); }
            }
        g.Restore(cs);
        Flat(g, Color.FromArgb(96, 100, 110), ox, by, 0, 0, 120, 3, 0);
        Panel(g, Color.FromArgb(170, 215, 255), ox, by, 52, 38, 16, 10, 0.4f);
        using (Pen p = new Pen(Color.FromArgb(60, 64, 72), Math.Max(1, U(0.6f)))) for (int i = 1; i < 4; i++) g.DrawLine(p, ox + U(52 + i * 4), by - U(38), ox + U(52 + i * 4), by - U(48));
        using (Pen p = new Pen(Color.FromArgb(150, 245, 245, 245), Math.Max(1, U(0.35f))))
            for (int i = 0; i < tally; i++)
            {
                int grp = i / 5, k = i % 5;
                float x = ox + U(92 + grp * 6 + k * 1.1f), y = by - U(30);
                if (k < 4) g.DrawLine(p, x, y, x, y - U(4));
                else g.DrawLine(p, ox + U(91.5f + grp * 6), y - U(0.6f), ox + U(96.5f + grp * 6), y - U(3.4f));
            }
        Flat(g, Color.FromArgb(60, 44, 34), ox, by, 12, 0, 1.6f, 5, 0.3f);
        Flat(g, Color.FromArgb(60, 44, 34), ox, by, 76, 0, 1.6f, 5, 0.3f);
        Wood(g, pal['B'], ox, by, 8, 4.6f, 74, 2.6f, 0.6f);
        Panel(g, Color.FromArgb(150, 155, 165), ox, by, 102, 0, 7, 6, 0.6f);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 240, 240, 240))) g.DrawString("Здесь был Стрелок", fntTiny, b, ox + U(6), by - U(32));
        Panel(g, Color.FromArgb(235, 235, 240), ox, by, 90, 0, 3, 3, 0.4f);
    }

    void DrawJailFront(Graphics g)
    {
        float by = Ground + (1 - JailA()) * U(62);
        float ox = JailX + (barsShake > 0 ? (float)Math.Sin(tick * 2.5) * U(0.35f) : 0);
        for (int i = 0; i <= 20; i++)
        {
            RectangleF r = R(ox, by, i * 6 - 0.5f, 0, 1.1f, 54);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, r.Width + 2, r.Height), Color.FromArgb(110, 115, 128), Color.FromArgb(48, 50, 58), 0f)) g.FillRectangle(b, r);
        }
        foreach (float yy in new float[] { 1, 27, 52.6f }) Panel(g, Color.FromArgb(80, 84, 96), ox, by, -0.5f, yy, 121, 1.4f, 0.4f);
        Panel(g, Color.FromArgb(200, 170, 60), ox, by, 63, 22, 4.4f, 5, 0.6f);
        Panel(g, Color.FromArgb(170, 40, 46), ox, by, 40, 55, 40, 5.6f, 1.2f);
        RectangleF sr = R(ox, by, 40, 55, 40, 5.6f);
        using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString("ИЗОЛЯТОР", fntSign, b, sr, sf); }
    }

    // ================= поставка на машине (склад) =================
    List<float[]> boxes = new List<float[]>();
    int carState; float carX, carSpeed; bool trunkOpen, deliveryPending, driverInCar;
    float CarStop { get { return A + U(2); } }

    Cat NewBald()
    {
        Cat d = NewGuest();
        d.MainC = Color.FromArgb(236, 194, 184); d.BellyC = Color.FromArgb(248, 222, 214); d.Stripes = false; d.Bald = true;
        d.IrisC = Color.FromArgb(90, 160, 230); d.Outfit = 4; d.C1 = Color.FromArgb(32, 32, 38); d.C2 = Color.White;
        d.Hat = 0; d.Glasses = false; d.Chain = true; d.Vape = true; d.Speed = 1.3f;
        return d;
    }

    void UpdateCar()
    {
        if (carState == 1)
        {
            float d = CarStop - carX;
            carX += Math.Max(U(0.3f), d * 0.05f);
            if (tick % 3 == 0) Exhaust(carX - U(0.5f), Ground - U(3), -1);
            if (CarStop - carX < U(0.3f)) { carX = CarStop; carState = 2; }
        }
        else if (carState == 3)
        {
            carSpeed += 0.04f * u; carX -= carSpeed;
            if (tick % 2 == 0) Exhaust(carX + U(46.5f), Ground - U(3), 1);
            if (carX < -U(60)) carState = 0;
        }
    }

    void AddDelivery(List<Func<bool>> s)
    {
        Cat drv = null;
        Func<Cat> D = delegate { return drv; };
        s.Add(Do(delegate { carState = 1; carX = -U(52); carSpeed = 0; trunkOpen = false; driverInCar = true; }));
        s.Add(delegate { return carState == 2; });
        s.Add(Wait(15));
        s.Add(Do(delegate
        {
            driverInCar = false;
            drv = NewBald(); drv.X = carX + U(30); drv.Dir = -1; guests.Add(drv);
        }));
        s.Add(SayF(D, "Здорово, братан! Поставка!", 45, 'K'));
        s.Add(Line(me, "О, привет! Заноси!", 30, 'h'));
        for (int k = 0; k < 3; k++)
        {
            float dx = A + U(16 + k * 13);
            s.Add(GoF(D, delegate { return carX + U(5); }));
            s.Add(Do(delegate { drv.Dir = -1; trunkOpen = true; }));
            s.Add(Wait(12));
            s.Add(Do(delegate { drv.Hold = 'K'; drv.HoldCol = 'B'; }));
            s.Add(GoF(D, delegate { return dx - U(4.8f); }));
            s.Add(Do(delegate { drv.Dir = 1; drv.Hold = ' '; boxes.Add(new float[] { dx, 2 }); Poof(dx, Ground - U(1)); }));
            if (k == 1) s.Add(SayF(D, "Тяжёлая, зараза...", 25, 'K'));
        }
        s.Add(SayF(D, "Распишись тут, братан", 40, 'K'));
        s.Add(GoF(delegate { return me; }, delegate { return Math.Min(Width - U(10), drv.X + U(10)); }));
        s.Add(Do(delegate { me.Dir = -1; drv.Dir = 1; }));
        s.Add(FlyStep('I', 'w', delegate { return Paw(drv); }, delegate { return Paw(me); }, 12));
        s.Add(Wait(15));
        s.Add(FlyStep('I', 'w', delegate { return Paw(me); }, delegate { return Paw(drv); }, 12));
        s.Add(Do(delegate { AddMoney(-(1500 + rnd.Next(25) * 100), true); }));
        s.Add(Line(me, "Готово! Спасибо!", 30, 'h'));
        s.Add(SayF(D, rnd.Next(100) < 45 ? "Я вернусь." : "Давай, на связи!", 30, 'K'));
        s.Add(GoF(D, delegate { return carX + U(30); }));
        s.Add(Do(delegate { guests.Remove(drv); driverInCar = true; trunkOpen = false; carState = 3; }));
        s.Add(delegate { return carState == 0; });
        s.Add(Do(delegate { driverInCar = false; if (rnd.Next(100) < 40) BigText("МИССИЯ ВЫПОЛНЕНА|+ уважение", 2, 110); }));
    }

    void DrawCar(Graphics g)
    {
        float x0 = carX, lw = Math.Max(1.4f, U(0.55f));
        Shadow(g, x0 + U(23), U(48));
        Color cc = Color.FromArgb(202, 80, 76), cd = Color.FromArgb(168, 60, 62), cl = Color.FromArgb(230, 122, 108);
        using (Pen ink = new Pen(InkC, lw))
        using (Pen thin = new Pen(Color.FromArgb(210, InkC), Math.Max(1f, U(0.32f))))
        {
            ink.LineJoin = LineJoin.Round; ink.StartCap = LineCap.Round; ink.EndCap = LineCap.Round; thin.StartCap = LineCap.Round; thin.EndCap = LineCap.Round;
            if (trunkOpen)
            {
                PointF[] lid = { V(x0, 0.6f, 16), V(x0, -1.6f, 24.6f), V(x0, 1.0f, 25.2f), V(x0, 2.6f, 16) };
                using (SolidBrush b = new SolidBrush(cd)) g.FillPolygon(b, lid);
                g.DrawPolygon(ink, lid);
            }
            PointF[] body = { V(x0, 0, 3.6f), V(x0, 0, 13.8f), V(x0, 1.6f, 16.2f), V(x0, 33.8f, 16.2f), V(x0, 40.4f, 10.0f), V(x0, 45.2f, 8.8f), V(x0, 46.4f, 7.0f), V(x0, 46.4f, 3.6f) };
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(body);
                using (SolidBrush b = new SolidBrush(cc)) g.FillPath(b, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(cd)) g.FillRectangle(b, x0 - U(1), Ground - U(6.2f), U(49), U(4));
                using (SolidBrush b = new SolidBrush(cl)) { g.FillRectangle(b, x0, Ground - U(10.0f), U(42), U(0.8f)); g.FillRectangle(b, x0 + U(2), Ground - U(16.2f), U(31), U(0.7f)); }
                CarArch(g, x0 + U(9), U(4.4f)); CarArch(g, x0 + U(37), U(4.4f));
                g.Restore(cs);
                g.DrawPath(ink, p);
            }
            CarGlass(g, new PointF[] { V(x0, 2.4f, 10.4f), V(x0, 2.4f, 15), V(x0, 13, 15), V(x0, 13, 10.4f) }, thin);
            CarGlass(g, new PointF[] { V(x0, 14.6f, 10.4f), V(x0, 14.6f, 15), V(x0, 25.4f, 15), V(x0, 25.4f, 10.4f) }, thin);
            CarGlass(g, new PointF[] { V(x0, 27, 10.4f), V(x0, 27, 15), V(x0, 33.4f, 15), V(x0, 38.0f, 10.4f) }, thin);
            if (driverInCar)
            {
                PointF h = V(x0, 31, 12.8f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 194, 184)))
                {
                    g.FillEllipse(b, h.X - U(2.2f), h.Y - U(2), U(4.4f), U(4));
                    g.FillPolygon(b, new PointF[] { new PointF(h.X - U(1.8f), h.Y - U(1.2f)), new PointF(h.X - U(1.4f), h.Y - U(3.6f)), new PointF(h.X - U(0.2f), h.Y - U(1.8f)) });
                    g.FillPolygon(b, new PointF[] { new PointF(h.X + U(0.4f), h.Y - U(1.8f)), new PointF(h.X + U(1.6f), h.Y - U(3.6f)), new PointF(h.X + U(2.0f), h.Y - U(1.0f)) });
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 26, 36))) g.FillEllipse(b, h.X + U(0.8f), h.Y - U(0.8f), U(0.8f), U(1.0f));
            }
            if (trunkOpen) using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 44, 42))) g.FillRectangle(b, new RectangleF(x0 + U(0.3f), Ground - U(15), U(1.6f), U(11)));
            // швы дверей, молдинг, ручки, рейлинги, зеркало
            g.DrawLine(thin, x0 + U(13.8f), Ground - U(4.6f), x0 + U(13.8f), Ground - U(10)); g.DrawLine(thin, x0 + U(26.2f), Ground - U(4.6f), x0 + U(26.2f), Ground - U(10));
            g.DrawLine(thin, x0 + U(0.6f), Ground - U(6.2f), x0 + U(45.6f), Ground - U(6.2f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 226, 210)))
            {
                using (GraphicsPath p = RoundRect(x0 + U(10.4f), Ground - U(9.3f), U(2.2f), U(0.7f), U(0.3f))) { g.FillPath(b, p); g.DrawPath(thin, p); }
                using (GraphicsPath p = RoundRect(x0 + U(22.8f), Ground - U(9.3f), U(2.2f), U(0.7f), U(0.3f))) { g.FillPath(b, p); g.DrawPath(thin, p); }
            }
            using (Pen pp = new Pen(Color.FromArgb(96, 86, 86), Math.Max(1.2f, U(0.55f)))) { pp.StartCap = LineCap.Round; pp.EndCap = LineCap.Round; g.DrawLine(pp, x0 + U(4), Ground - U(17.0f), x0 + U(31), Ground - U(17.0f)); g.DrawLine(pp, x0 + U(6), Ground - U(17.0f), x0 + U(6), Ground - U(16.3f)); g.DrawLine(pp, x0 + U(29), Ground - U(17.0f), x0 + U(29), Ground - U(16.3f)); }
            using (GraphicsPath p = RoundRect(x0 + U(37.2f), Ground - U(11.6f), U(2f), U(1.5f), U(0.6f))) { using (SolidBrush b = new SolidBrush(cd)) g.FillPath(b, p); g.DrawPath(thin, p); }
            // фара, задний фонарь, бампер
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 240, 180))) g.FillEllipse(b, x0 + U(43.2f), Ground - U(8.5f), U(2.6f), U(1.6f));
            g.DrawEllipse(thin, x0 + U(43.2f), Ground - U(8.5f), U(2.6f), U(1.6f));
            using (GraphicsPath p = RoundRect(x0 + U(0.1f), Ground - U(11.8f), U(1.1f), U(3.2f), U(0.4f))) { using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 96, 90))) g.FillPath(b, p); g.DrawPath(thin, p); }
            using (GraphicsPath p = RoundRect(x0 - U(0.6f), Ground - U(4.6f), U(47.6f), U(2f), U(0.9f))) { using (SolidBrush b = new SolidBrush(Color.FromArgb(104, 96, 96))) g.FillPath(b, p); g.DrawPath(ink, p); }
        }
        float rot = carX / U(3.3f);
        DrawWheel(g, x0 + U(9), Ground - U(3.3f), U(3.3f), rot);
        DrawWheel(g, x0 + U(37), Ground - U(3.3f), U(3.3f), rot);
    }

    // ================= тряска шкафа мышкой =================
    bool grabRack, grabMoved; int grabX0, reactCooldown; float shakeEnergy, wobbleAmp = 3;
    static readonly string[] ShakeLines = { "Эй, не тряси!", "Аккуратнее!!!", "Там же товар!", "Ну зачем...", "Пыль же!", "Я только убрался!" };

    bool RackGrabbable(Point pt)
    {
        return mode == 3 && pendingMode < 0 && !rackFallen && !rackFalling && !rackLifting && R(RackX, Ground, -2, 0, 38, 47).Contains(pt);
    }

    void RackShakeEvent(bool soft)
    {
        for (int k = 0; k < (soft ? 4 : 7); k++)
        {
            Particle p = new Particle();
            p.Kind = 5; p.X = RackX + U((float)rnd.NextDouble() * 34); p.Y = Ground - U(26 + (float)rnd.NextDouble() * 18);
            p.VX = (float)(rnd.NextDouble() - 0.5) * 0.6f * u; p.VY = (0.05f + (float)rnd.NextDouble() * 0.15f) * u;
            p.R = U(0.8f + (float)rnd.NextDouble() * 1.2f); p.Max = 40;
            parts.Add(p);
        }
        if (dust.Count < 7 && rnd.Next(100) < (soft ? 25 : 45))
            dust.Add(new float[] { Math.Max(U(6), Math.Min(Width - U(6), RackX + U(-8 + (float)rnd.NextDouble() * 50))), 1.2f + (float)rnd.NextDouble() * 1.2f });
        if (rnd.Next(100) < (soft ? 30 : 50))
        {
            List<int> filled = new List<int>();
            for (int i = 0; i < 15; i++) if (slots[i] != ' ') filled.Add(i);
            if (filled.Count > 0)
            {
                int k = filled[rnd.Next(filled.Count)];
                char cc = slots[k]; slots[k] = ' ';
                PointF sp = SlotPos(k);
                float tx = RackX + U(-10 + (float)rnd.NextDouble() * 54);
                Fly('X', cc, sp.X, sp.Y, tx, Ground - U(1.75f), 14, delegate { AddPileAt(tx, cc); });
                celebrated = false;
            }
        }
        if (reactCooldown <= 0)
        {
            Say(ShakeLines[rnd.Next(ShakeLines.Length)], me.X, CatTop(me) - U(1), 'R');
            me.SweatT = 60; me.Shake = 15; reactCooldown = 70;
        }
    }

    // ================= музыка и кино =================
    int musicT, movieT;

    void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(url); }
        catch (Exception ex) { Say("Не открылось: " + ex.Message, me.X, CatTop(me) - U(1), 'R'); }
    }

    void UpdateMedia()
    {
        if (musicT > 0)
        {
            musicT--;
            if (tick % 20 == 0)
            {
                PointF h = Local(me, 1, -24);
                Particle p = new Particle(); p.Kind = 2; p.X = h.X + U((float)rnd.NextDouble() * 6 - 3); p.Y = h.Y;
                p.VY = -0.3f * u; p.Max = 70; p.Col = ItemColors[rnd.Next(ItemColors.Length)];
                parts.Add(p);
            }
            if (musicT == 0 && mode == 0) me.Y = 0;
        }
        if (movieT > 0)
        {
            movieT--;
            if (movieT % 160 == 80) Say(rnd.Next(2) == 0 ? "Ням-ням" : (rnd.Next(2) == 0 ? "О, поворот!" : "Ахаха"), me.X, CatTop(me) - U(1), 'U');
            if (movieT == 0 && me.Hold == 'Q') me.Hold = ' ';
        }
    }

    // ================= погода =================
    string wxCity = "Егорьевск"; double wxLat = 55.383, wxLon = 39.036; bool wxShow = true;
    volatile bool wxOk, wxBusy; float wxTemp, wxFeels, wxWind; int wxCode = -1, wxCloud; bool wxIsDay = true; string wxErr;
    DateTime wxSunrise = DateTime.MinValue, wxSunset = DateTime.MinValue;
    int lastWxFetchTick = -100000, thunderT, nextThunder = 300;
    float forceNight = -1; // для отладки
    List<float[]> clouds = new List<float[]>(), fogBanks = new List<float[]>();

    static float PetStat(string value, float fallback)
    {
        float n;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) return fallback;
        if (n < 0f) return 0f;
        if (n > 100f) return 100f;
        return n;
    }

    void LoadSettings()
    {
        try
        {
            string[] lines = File.ReadAllLines(SettingsPath);
            int v;
            if (lines.Length == 1 && int.TryParse(lines[0].Trim(), out v)) { if (v >= 0 && v < SizeSteps.Length) sizeIdx = v; return; }
            foreach (string ln in lines)
            {
                int eq = ln.IndexOf('='); if (eq < 0) continue;
                string k = ln.Substring(0, eq).Trim(), val = ln.Substring(eq + 1).Trim();
                if (k == "size" && int.TryParse(val, out v) && v >= 0 && v < SizeSteps.Length) sizeIdx = v;
                if (k == "pet_curiosity") petBrain.Curiosity = PetStat(val, 65f);
                if (k == "pet_playfulness") petBrain.Playfulness = PetStat(val, 60f);
                if (k == "pet_sociability") petBrain.Sociability = PetStat(val, 50f);
                if (k == "pet_laziness") petBrain.Laziness = PetStat(val, 45f);
                if (k == "pet_timidity") petBrain.Timidity = PetStat(val, 25f);
                if (k == "pet_affection") petBrain.Affection = PetStat(val, 20f);
                if (k == "pet_boredom") petBrain.Boredom = PetStat(val, 10f);
                if (k == "pet_energy") petBrain.Energy = PetStat(val, 85f);
                if (k == "city" && val.Length > 0) wxCity = val;
                if (k == "lat") double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out wxLat);
                if (k == "lon") double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out wxLon);
                if (k == "weather") wxShow = val != "0";
                if (k == "roam") interfacePlay = val != "0";
                if (k == "party") partyDay = val;
                if (k == "fright") { int fl; if (int.TryParse(val, out fl) && fl >= 0 && fl <= 3) frightLevel = fl; }
                if (k == "dl") dlWatch = val != "0";
                if (k == "notes") dodgeNotes = val != "0";
                if (k == "clip") clipOn = val != "0";
                if (k == "smartclip") smartClip = val != "0";
                if (k == "bigseen") bigSeen = val == "1";
                if (k == "ms_token") msToken = val.Trim();
                if (k == "panel") panelShow = val != "0";
                if (k == "autoupd") autoUpdate = val != "0";
                if (k == "ver") savedVer = val;
                if (k == "net") netWatch = val != "0";
                if (k == "netspeed") netShowSpeed = val == "1";
                if (k == "links") linksSaved = true;
                if (k == "link") { int bar = val.IndexOf('|'); if (bar > 0) { links.Add(new string[] { val.Substring(0, bar), val.Substring(bar + 1) }); linksSaved = true; } }
                if (k == "money") long.TryParse(val, out money);
                if (k == "ai_format") aiFormat = val;
                if (k == "ai_url" && val.Length > 0) aiUrl = val;
                if (k == "ai_model" && val.Length > 0) aiModel = val;
                if (k == "ai_model" && val == "gemini-2.5-flash") aiModel = "gemini-3.8-flash";
                if (k == "ai_key") aiKey = val;
                if (k == "ai_voice") aiVoice = val != "0";
                if (k == "voice_clone") cloneVoice = val != "0";
                if (k == "voice_dir" && val.Length > 0) voiceDir = val;
            }
            if (aiKey.Length == 0 && aiFormat == "anthropic") { aiFormat = "openai"; aiUrl = GeminiUrl; aiModel = "gemini-3.8-flash"; }
        }
        catch { }
    }

    void SaveSettings()
    {
        try
        {
            List<string> sl = new List<string>(new string[] {
                "size=" + sizeIdx, "city=" + wxCity,
                "pet_curiosity=" + petBrain.Curiosity.ToString(CultureInfo.InvariantCulture), "pet_playfulness=" + petBrain.Playfulness.ToString(CultureInfo.InvariantCulture), "pet_sociability=" + petBrain.Sociability.ToString(CultureInfo.InvariantCulture), "pet_laziness=" + petBrain.Laziness.ToString(CultureInfo.InvariantCulture), "pet_timidity=" + petBrain.Timidity.ToString(CultureInfo.InvariantCulture), "pet_affection=" + petBrain.Affection.ToString(CultureInfo.InvariantCulture), "pet_boredom=" + petBrain.Boredom.ToString(CultureInfo.InvariantCulture), "pet_energy=" + petBrain.Energy.ToString(CultureInfo.InvariantCulture),
                "lat=" + wxLat.ToString(CultureInfo.InvariantCulture), "lon=" + wxLon.ToString(CultureInfo.InvariantCulture),
                "weather=" + (wxShow ? "1" : "0"), "roam=" + (interfacePlay ? "1" : "0"), "party=" + partyDay, "fright=" + frightLevel, "dl=" + (dlWatch ? "1" : "0"), "notes=" + (dodgeNotes ? "1" : "0"), "money=" + money, "ai_format=" + aiFormat, "ai_url=" + aiUrl, "ai_model=" + aiModel, "ai_key=" + aiKey, "ai_voice=" + (aiVoice ? "1" : "0"), "voice_clone=" + (cloneVoice ? "1" : "0"), "voice_dir=" + voiceDir
            });
            sl.Add("panel=" + (panelShow ? "1" : "0")); sl.Add("clip=" + (clipOn ? "1" : "0")); sl.Add("smartclip=" + (smartClip ? "1" : "0")); sl.Add("bigseen=" + (bigSeen ? "1" : "0")); sl.Add("ms_token=" + msToken); sl.Add("net=" + (netWatch ? "1" : "0")); sl.Add("netspeed=" + (netShowSpeed ? "1" : "0"));
            sl.Add("autoupd=" + (autoUpdate ? "1" : "0")); sl.Add("ver=" + AppVersion);
            sl.Add("links=1");
            foreach (string[] l in links) sl.Add("link=" + l[0].Replace("|", "/") + "|" + l[1]);
            File.WriteAllLines(SettingsPath, sl.ToArray(), Encoding.UTF8);
        }
        catch { }
    }

    static string Http(string url)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        ServicePointManager.Expect100Continue = false;
        using (WebClient wc = new WebClient())
        {
            wc.Encoding = Encoding.UTF8;
            wc.Headers.Add("User-Agent", "VapePet/1.0");
            return wc.DownloadString(url);
        }
    }

    static float JNum(string json, string key, float def)
    {
        Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?[0-9]+(\\.[0-9]+)?)");
        if (!m.Success) return def;
        return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    void FetchWeather()
    {
        if (wxBusy) return;
        wxBusy = true; lastWxFetchTick = tick;
        double lat = wxLat, lon = wxLon;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string url = "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString(CultureInfo.InvariantCulture) +
                    "&longitude=" + lon.ToString(CultureInfo.InvariantCulture) +
                    "&current=temperature_2m,apparent_temperature,weather_code,cloud_cover,wind_speed_10m,is_day&daily=sunrise,sunset,weather_code,temperature_2m_max&timezone=auto&forecast_days=4&wind_speed_unit=ms";
                ParseWeather(Http(url));
                wxErr = null;
            }
            catch (Exception ex) { bool first = wxErr == null; wxErr = ex.Message; if (first && mode == 0 && wxShow) houstonPending = true; }
            wxBusy = false;
        });
    }

    void ParseWeather(string json)
    {
        int ci = json.IndexOf("\"current\":{");
        if (ci < 0) throw new Exception("нет данных");
        int ce = json.IndexOf('}', ci);
        string cur = json.Substring(ci, ce - ci);
        wxTemp = JNum(cur, "temperature_2m", 0);
        wxFeels = JNum(cur, "apparent_temperature", wxTemp);
        wxCode = (int)JNum(cur, "weather_code", 0);
        wxCloud = (int)JNum(cur, "cloud_cover", 0);
        wxWind = JNum(cur, "wind_speed_10m", 0);
        wxIsDay = JNum(cur, "is_day", 1) > 0.5f;
        Match sr = Regex.Match(json, "\"sunrise\"\\s*:\\s*\\[\\s*\"([^\"]+)\"");
        Match ss = Regex.Match(json, "\"sunset\"\\s*:\\s*\\[\\s*\"([^\"]+)\"");
        DateTime d;
        if (sr.Success && DateTime.TryParseExact(sr.Groups[1].Value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) wxSunrise = d;
        if (ss.Success && DateTime.TryParseExact(ss.Groups[1].Value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) wxSunset = d;
        ParseDaily(json);
        wxOk = true;
    }

    void AskCity()
    {
        using (Form f = new Form())
        {
            f.Text = "Город для погоды"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterScreen;
            f.MinimizeBox = false; f.MaximizeBox = false; f.TopMost = true; f.ClientSize = new Size(320, 110); f.ShowInTaskbar = false;
            Label l = new Label(); l.Text = "Введите город (например: Егорьевск):"; l.SetBounds(12, 12, 296, 20);
            TextBox tb = new TextBox(); tb.Text = wxCity; tb.SetBounds(12, 36, 296, 24);
            Button ok = new Button(); ok.Text = "OK"; ok.DialogResult = DialogResult.OK; ok.SetBounds(152, 72, 75, 26);
            Button cn = new Button(); cn.Text = "Отмена"; cn.DialogResult = DialogResult.Cancel; cn.SetBounds(233, 72, 75, 26);
            f.Controls.AddRange(new Control[] { l, tb, ok, cn });
            f.AcceptButton = ok; f.CancelButton = cn;
            if (f.ShowDialog() != DialogResult.OK) return;
            string name = tb.Text.Trim();
            if (name.Length == 0) return;
            try
            {
                string json = Http("https://geocoding-api.open-meteo.com/v1/search?count=1&language=ru&format=json&name=" + Uri.EscapeDataString(name));
                int ri = json.IndexOf("\"results\"");
                if (ri < 0) { MessageBox.Show("Город «" + name + "» не найден", "Погода"); return; }
                string r = json.Substring(ri);
                wxLat = JNum(r, "latitude", (float)wxLat); wxLon = JNum(r, "longitude", (float)wxLon);
                Match nm = Regex.Match(r, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                wxCity = nm.Success ? nm.Groups[1].Value : name;
                SaveSettings(); UpdateWeatherMenu();
                wxOk = false; FetchWeather();
            }
            catch (Exception ex) { MessageBox.Show("Не удалось найти город: " + ex.Message, "Погода"); }
        }
    }

    ToolStripMenuItem wxCityItem, wxShowItem;
    void UpdateWeatherMenu()
    {
        if (wxCityItem != null) wxCityItem.Text = "Город: " + wxCity + "...";
        if (wxShowItem != null) wxShowItem.Checked = wxShow;
    }

    int wxPeekT; volatile bool houstonPending;
    bool WxVisible { get { return wxShow && ((mode == 0 && pendingMode < 0) || (pendingMode == 0) || wxPeekT > 0); } }
    bool WxActive { get { return WxVisible && wxOk; } }
    bool IsSnow { get { int c = wxCode; return WxActive && ((c >= 71 && c <= 77) || c == 85 || c == 86); } }
    bool IsRain { get { int c = wxCode; return WxActive && ((c >= 51 && c <= 67) || (c >= 80 && c <= 82) || c >= 95); } }
    bool IsFog { get { return WxActive && (wxCode == 45 || wxCode == 48); } }
    bool IsStorm { get { return WxActive && wxCode >= 95; } }
    bool IsCold { get { return WxActive && wxTemp < 5; } }
    bool IsFreezing { get { return WxActive && wxTemp < -5; } }
    int PrecipLevel
    {
        get
        {
            int c = wxCode;
            if (c == 51 || c == 56 || c == 61 || c == 71 || c == 80 || c == 85 || c == 77) return 1;
            if (c == 53 || c == 63 || c == 73 || c == 81 || c == 66) return 2;
            return 3;
        }
    }

    static string WxText(int c)
    {
        if (c == 0) return "Ясно";
        if (c == 1) return "Почти ясно";
        if (c == 2) return "Переменная облачность";
        if (c == 3) return "Пасмурно";
        if (c == 45 || c == 48) return "Туман";
        if (c >= 51 && c <= 55) return "Морось";
        if (c == 56 || c == 57) return "Ледяная морось";
        if (c >= 61 && c <= 65) return c == 65 ? "Сильный дождь" : "Дождь";
        if (c == 66 || c == 67) return "Ледяной дождь";
        if (c >= 71 && c <= 75) return c == 75 ? "Сильный снег" : "Снег";
        if (c == 77) return "Снежная крупа";
        if (c >= 80 && c <= 82) return "Ливень";
        if (c == 85 || c == 86) return "Снегопад";
        if (c >= 95) return "Гроза";
        return "—";
    }

    // 0 - день, 1 - ночь
    float NightK()
    {
        if (forceNight >= 0) return forceNight;
        if (!wxShow) return 0;
        if (wxOk && wxSunrise == DateTime.MinValue) return wxIsDay ? 0 : 1;
        DateTime now = DateTime.Now;
        DateTime sr = wxSunrise != DateTime.MinValue ? now.Date + wxSunrise.TimeOfDay : now.Date.AddHours(7.5);
        DateTime ss = wxSunset != DateTime.MinValue ? now.Date + wxSunset.TimeOfDay : now.Date.AddHours(17);
        double m = (now - now.Date).TotalMinutes, a = (sr - sr.Date).TotalMinutes, b = (ss - ss.Date).TotalMinutes;
        if (m < a - 40 || m > b + 40) return 1;
        if (m < a + 20) return (float)Math.Min(1, Math.Max(0, (a + 20 - m) / 60.0));
        if (m > b - 20) return (float)Math.Min(1, Math.Max(0, (m - (b - 20)) / 60.0));
        return 0;
    }

    void UpdateWeather()
    {
        int wxInterval = (wxOk && wxErr == null) ? 30 * 60 * 15 : 30 * 60;
        if (tick - lastWxFetchTick > wxInterval && wxShow) FetchWeather();
        if (!WxActive) { clouds.Clear(); return; }
        float wind = Math.Max(-4, Math.Min(4, wxWind)) * 0.12f * u;
        // осадки
        if (IsRain || IsSnow)
        {
            int lvl = PrecipLevel;
            int n = IsSnow ? (rnd.Next(100) < 55 ? lvl : 0) : lvl * 2;
            int count = 0; foreach (Particle p in parts) if (p.Kind == 6 || p.Kind == 7) count++;
            for (int i = 0; i < n && count < (IsSnow ? 200 : 260); i++, count++)
            {
                Particle p = new Particle();
                p.X = (float)rnd.NextDouble() * (Width + U(20)) - U(10); p.Y = -U(2);
                if (IsSnow) { p.Kind = 7; p.VY = (0.25f + (float)rnd.NextDouble() * 0.2f) * u; p.VX = wind * 0.6f; p.R = U(0.35f + (float)rnd.NextDouble() * 0.45f); }
                else { p.Kind = 6; p.VY = (2.0f + (float)rnd.NextDouble() * 0.6f) * u; p.VX = wind * 1.5f; p.R = U(1.4f + lvl * 0.4f); }
                p.Max = (int)(Ground / p.VY) + 2;
                parts.Add(p);
            }
        }
        // облака
        int want = wxCloud >= 85 ? 5 : wxCloud / 22;
        if (IsFog) want = 0;
        while (clouds.Count < want) clouds.Add(new float[] { (float)rnd.NextDouble() * Width, U(3 + (float)rnd.NextDouble() * 14), 0.7f + (float)rnd.NextDouble() * 0.6f });
        while (clouds.Count > want) clouds.RemoveAt(clouds.Count - 1);
        foreach (float[] c in clouds)
        {
            c[0] += (0.08f + Math.Abs(wind) * 0.2f) * u * (wxWind >= 0 ? 1 : -1) * 0.5f + 0.05f * u;
            if (c[0] > Width + U(30)) c[0] = -U(30);
            if (c[0] < -U(30)) c[0] = Width + U(30);
        }
        // туман: мягкие клубы у земли
        if (IsFog)
        {
            int fogN = Math.Max(10, (int)(Width / U(18)));
            while (fogBanks.Count < fogN)
                fogBanks.Add(new float[] { (float)rnd.NextDouble() * Width, 1 + (float)rnd.NextDouble() * 16, 20 + (float)rnd.NextDouble() * 22, 4 + (float)rnd.NextDouble() * 5, (0.03f + (float)rnd.NextDouble() * 0.06f) * (rnd.Next(2) == 0 ? 1 : -1) });
            foreach (float[] f in fogBanks)
            {
                f[0] += f[4] * u;
                if (f[0] > Width + U(f[2])) f[0] = -U(f[2]);
                if (f[0] < -U(f[2])) f[0] = Width + U(f[2]);
            }
        }
        else fogBanks.Clear();
        // гроза
        if (IsStorm)
        {
            if (thunderT > 0) thunderT--;
            if (--nextThunder <= 0)
            {
                thunderT = 8; nextThunder = 250 + rnd.Next(400); shakeT = 6;
                me.SweatT = 60; me.Shake = 15;
                if (rnd.Next(2) == 0) Say("Ой! Гром!", me.X, CatTop(me) - U(1), 'U');
            }
        }
        // пар изо рта на морозе
        if (IsCold && tick % 4 == 0)
        {
            List<Cat> all = new List<Cat>(guests); all.Add(me);
            foreach (Cat c in all)
            {
                if (c.Hidden || c.Lying || c.Vape || rnd.Next(60) != 0) continue;
                PointF m = Local(c, 4.2f, -15.6f);
                Particle p = new Particle(); p.Kind = 0; p.X = m.X; p.Y = m.Y;
                p.VX = c.Dir * 0.15f * u; p.VY = -0.08f * u; p.R = U(0.6f); p.Max = 30;
                parts.Add(p);
            }
        }
    }

    string WeatherRemark()
    {
        if (!WxActive) return null;
        int t = (int)Math.Round(wxTemp);
        string ts = (t < 0 ? "−" + (-t) : t.ToString()) + "°";
        List<string> l = new List<string>();
        if (IsStorm) l.Add("Гроза... страшно");
        if (IsSnow) { l.Add("Снежок!"); l.Add("Зима близко..."); }
        if (IsRain) { l.Add("Опять дождь..."); l.Add("Хорошо, что зонтик взял"); }
        if (IsFog) l.Add("Туман, ничего не видно");
        if (wxTemp < -10) l.Add("Брр, " + ts + "! Лапы мёрзнут");
        else if (wxTemp < 3) l.Add("Холодно, " + ts + "...");
        if (wxTemp > 25) l.Add("Жара, " + ts + "!");
        if (wxCode <= 1 && NightK() < 0.5f) l.Add("Солнышко!");
        if (NightK() > 0.7f) l.Add("Уже ночь... спать пора");
        if (wxWind > 8) l.Add("Ну и ветрище!");
        if (l.Count == 0) l.Add("На улице " + ts + ", " + WxText(wxCode).ToLower());
        return l[rnd.Next(l.Count)];
    }

    // ---- рисование погоды ----
    void DrawCloud(Graphics g, float x, float y, float s, int alpha, bool dark)
    {
        Color c = dark ? Color.FromArgb(alpha, 150, 156, 170) : Color.FromArgb(alpha, 250, 252, 255);
        using (SolidBrush b = new SolidBrush(c))
        {
            g.FillEllipse(b, x - U(9) * s, y - U(2.5f) * s, U(18) * s, U(6) * s);
            g.FillEllipse(b, x - U(5) * s, y - U(6) * s, U(9) * s, U(8) * s);
            g.FillEllipse(b, x + U(0.5f) * s, y - U(4.6f) * s, U(7) * s, U(6) * s);
        }
    }

    void DrawWeatherBack(Graphics g)
    {
        if (!WxActive) return;
        bool dark = wxCode >= 3 && (wxCode == 3 || IsRain || IsSnow);
        int alpha = dark ? 150 : 120;
        foreach (float[] c in clouds) DrawCloud(g, c[0], c[1], c[2], alpha, dark);
    }

    void DrawWeatherGround(Graphics g)
    {
        if (!WxActive) return;
        int month = DateTime.Now.Month;
        bool snowGround = IsSnow || (wxTemp <= 1 && (month >= 11 || month <= 3));
        if (snowGround)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                List<PointF> pts = new List<PointF>();
                for (int i = 0; i <= 40; i++)
                {
                    float x = Width * i / 40f;
                    pts.Add(new PointF(x, Ground - U(1.2f) - (float)Math.Abs(Math.Sin(i * 1.7)) * U(0.7f)));
                }
                pts.Add(new PointF(Width, Ground)); pts.Add(new PointF(0, Ground));
                p.AddClosedCurve(pts.ToArray(), 0.3f);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, Ground - U(2.2f), Width, U(2.3f)), Color.FromArgb(245, 255, 255, 255), Color.FromArgb(235, 215, 228, 245), 90f)) g.FillPath(b, p);
            }
        }
        else if (IsRain)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 120, 160, 210)))
                for (int i = 0; i < 6; i++)
                {
                    float x = Width * ((i * 37 % 100) / 100f);
                    g.FillEllipse(b, x, Ground - U(0.8f), U(8 + (i % 3) * 3), U(1.2f));
                }
        }
    }

    void DrawWeatherFront(Graphics g)
    {
        if (!WxActive) return;
        foreach (Particle p in parts)
        {
            if (p.Kind == 6)
                using (Pen pen = new Pen(Color.FromArgb(150, 170, 200, 240), Math.Max(1f, U(0.22f))))
                    g.DrawLine(pen, p.X, p.Y, p.X - p.VX * 1.5f, p.Y - p.R);
            else if (p.Kind == 7)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
        }

        if (thunderT > 0 && thunderT % 3 != 0)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillRectangle(b, 0, 0, Width, Height);
    }

    // туман: сцена становится бледной и дымчатой (не трогая экран вокруг)
    Bitmap buf3;
    Bitmap ApplyFog(Bitmap src)
    {
        if (buf3 == null || buf3.Width != src.Width || buf3.Height != src.Height)
        {
            if (buf3 != null) buf3.Dispose();
            buf3 = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
        }
        float k = 0.38f;
        ColorMatrix cm = new ColorMatrix(new float[][] {
            new float[] { 1 - k, 0, 0, 0, 0 },
            new float[] { 0, 1 - k, 0, 0, 0 },
            new float[] { 0, 0, 1 - k, 0, 0 },
            new float[] { 0, 0, 0, 0.9f, 0 },
            new float[] { k * 0.86f, k * 0.88f, k * 0.92f, 0, 1 } });
        using (ImageAttributes ia = new ImageAttributes())
        using (Graphics g = Graphics.FromImage(buf3))
        {
            ia.SetColorMatrix(cm);
            g.Clear(Color.Transparent);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
        }
        return buf3;
    }

    // затемнение сцены ночью
    Bitmap buf2;
    Bitmap ApplyNight(Bitmap src, float n)
    {
        if (buf2 == null || buf2.Width != src.Width || buf2.Height != src.Height)
        {
            if (buf2 != null) buf2.Dispose();
            buf2 = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
        }
        ColorMatrix cm = new ColorMatrix(new float[][] {
            new float[] { 1 - 0.45f * n, 0, 0, 0, 0 },
            new float[] { 0, 1 - 0.38f * n, 0, 0, 0 },
            new float[] { 0, 0, 1 - 0.16f * n, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 0.0f, 0.0f, 0.03f * n, 0, 1 } });
        using (ImageAttributes ia = new ImageAttributes())
        using (Graphics g = Graphics.FromImage(buf2))
        {
            ia.SetColorMatrix(cm);
            g.Clear(Color.Transparent);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
        }
        return buf2;
    }

    void Glow(Graphics g, float cx, float cy, float rx, float ry, Color c, int alpha)
    {
        if (alpha <= 0) return;
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
            using (PathGradientBrush b = new PathGradientBrush(p))
            {
                b.CenterColor = Color.FromArgb(Math.Min(255, alpha), c);
                b.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                g.FillPath(b, p);
            }
        }
    }

    void DrawStreetLamp(Graphics g, float x, float n)
    {
        using (Pen p = new Pen(Color.FromArgb(60, 64, 74), Math.Max(1, U(0.9f)))) { g.DrawLine(p, x, Ground, x, Ground - U(38)); g.DrawArc(p, x, Ground - U(40), U(6), U(4), 180, 180); }
        using (GraphicsPath p = RoundRect(x + U(4.4f), Ground - U(38.8f), U(3.4f), U(1.6f), U(0.5f))) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 74, 84))) g.FillPath(b, p);
        int a = (int)(255 * n);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Max(60, a), 255, 235, 170))) g.FillEllipse(b, x + U(4.8f), Ground - U(37.6f), U(2.6f), U(1.2f));
        if (n > 0.05f)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { new PointF(x + U(5), Ground - U(37)), new PointF(x + U(7.4f), Ground - U(37)), new PointF(x + U(17), Ground), new PointF(x - U(5), Ground) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x - U(6), Ground - U(38), U(24), U(39)), Color.FromArgb((int)(90 * n), 255, 230, 150), Color.FromArgb((int)(25 * n), 255, 230, 150), 90f)) g.FillPath(b, p);
            }
            Glow(g, x + U(6), Ground - U(37), U(5), U(4), Color.FromArgb(255, 235, 170), (int)(200 * n));
            Glow(g, x + U(6), Ground, U(12), U(2.2f), Color.FromArgb(255, 230, 150), (int)(120 * n));
        }
    }

    void DrawNightLights(Graphics g, float n)
    {
        int a = (int)(170 * n);
        Color warm = Color.FromArgb(255, 220, 140);
        if (mode == 0) { DrawStreetLamp(g, U(6), n); DrawStreetLamp(g, Width - U(14), n); }
        if (mode == 1 || mode == 6 || (mode == 5 && omonPhase == 0))
        {
            Glow(g, A + U(13), Ground - U(25), U(18), U(24), warm, a / 2);
            Glow(g, A + U(43), Ground - U(25), U(18), U(24), warm, a / 2);
            Glow(g, A + U(13), Ground - U(43), U(10), U(4), Color.FromArgb(255, 220, 80), a);
            Glow(g, A + U(43), Ground - U(43), U(10), U(4), Color.FromArgb(255, 220, 80), a);
            Glow(g, CounterX + U(8), Ground - U(19), U(7), U(5), Color.FromArgb(120, 255, 160), a);
        }
        if (mode == 2)
        {
            if (consoleOn) Glow(g, StandX + U(23), Ground - U(20), U(26), U(18), tvState >= 2 ? GameA[tvState == 2 ? menuSel : gameIdx] : Color.FromArgb(120, 170, 255), a);
            Glow(g, HookahX + U(4), Ground - U(23), U(6), U(5), Color.FromArgb(255, 140, 60), me.PuffT > 55 ? 220 : a / 2);
        }
        if (mode == 3) { Glow(g, RackX + U(17), Ground - U(44), U(16), U(8), Color.FromArgb(255, 200, 200), a / 2); if (carState == 1 || carState == 2 || carState == 3) Glow(g, carX + U(48), Ground - U(8), U(14), U(4), Color.FromArgb(255, 245, 200), a); }
        if (mode == 4)
        {
            float[][] wins = { new float[] { 10, 8 }, new float[] { 78, 8 }, new float[] { 10, 28 }, new float[] { 44, 28 }, new float[] { 78, 28 } };
            Color[] disco = { pal['L'], pal['Y'], pal['u'], pal['P'], pal['N'] };
            for (int i = 0; i < wins.Length; i++) Glow(g, A + U(wins[i][0] + 10), Ground - U(wins[i][1] + 6), U(16), U(12), disco[(tick / 15 + i * 2) % disco.Length], a);
            Glow(g, PoolX + U(30), Ground - U(7), U(34), U(8), Color.FromArgb(80, 220, 255), a);
        }
        if (mode == 5 && omonPhase == 1) Glow(g, JailX + U(60), Ground - U(43), U(14), U(10), Color.FromArgb(170, 200, 255), a / 2);
        if (mode == 5 && vanState != 0) Glow(g, vanX + U(26), Ground - U(23), U(14), U(8), (tick / 6) % 2 == 0 ? Color.Red : Color.FromArgb(60, 120, 255), a + 40);
        List<Cat> all = new List<Cat>(guests); all.Add(me);
        foreach (Cat c in all)
            if (!c.Hidden && c.Vape && c.PuffT > 55) { PointF t = Local(c, 7.6f, -14.9f); Glow(g, t.X, t.Y, U(3), U(3), Color.FromArgb(120, 255, 255), 200); }
    }

    void DrawWxIcon(Graphics g, float cx, float cy, float s)
    {
        bool night = !wxIconDay && NightK() > 0.5f;
        int c = wxCode;
        bool cloudy = c >= 2;
        if (c <= 2)
        {
            float ox = cloudy ? -s * 0.3f : 0, oy = cloudy ? -s * 0.25f : 0;
            if (night)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 240, 190))) g.FillEllipse(b, cx + ox - s * 0.5f, cy + oy - s * 0.5f, s, s);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(45, 52, 70))) g.FillEllipse(b, cx + ox - s * 0.2f, cy + oy - s * 0.65f, s, s);
            }
            else
            {
                using (Pen p = new Pen(Color.FromArgb(255, 200, 50), Math.Max(1, s * 0.09f)))
                    for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(p, cx + ox + (float)Math.Cos(a) * s * 0.5f, cy + oy + (float)Math.Sin(a) * s * 0.5f, cx + ox + (float)Math.Cos(a) * s * 0.7f, cy + oy + (float)Math.Sin(a) * s * 0.7f); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 210, 60))) g.FillEllipse(b, cx + ox - s * 0.4f, cy + oy - s * 0.4f, s * 0.8f, s * 0.8f);
            }
        }
        if (cloudy)
        {
            Color cc = (c == 2) ? Color.FromArgb(245, 248, 255) : Color.FromArgb(205, 212, 225);
            using (SolidBrush b = new SolidBrush(cc))
            {
                g.FillEllipse(b, cx - s * 0.6f, cy - s * 0.05f, s * 1.2f, s * 0.5f);
                g.FillEllipse(b, cx - s * 0.35f, cy - s * 0.35f, s * 0.6f, s * 0.6f);
                g.FillEllipse(b, cx + s * 0.0f, cy - s * 0.25f, s * 0.5f, s * 0.5f);
            }
        }
        if (IsRain)
            using (Pen p = new Pen(Color.FromArgb(120, 180, 255), Math.Max(1, s * 0.08f)))
                for (int i = 0; i < 3; i++) g.DrawLine(p, cx - s * 0.3f + i * s * 0.3f, cy + s * 0.5f, cx - s * 0.4f + i * s * 0.3f, cy + s * 0.75f);
        if (IsSnow)
            using (SolidBrush b = new SolidBrush(Color.White))
                for (int i = 0; i < 3; i++) g.FillEllipse(b, cx - s * 0.35f + i * s * 0.3f, cy + s * 0.5f + (i % 2) * s * 0.12f, s * 0.15f, s * 0.15f);
        if (IsStorm)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 60)))
                g.FillPolygon(b, new PointF[] { new PointF(cx, cy + s * 0.3f), new PointF(cx - s * 0.15f, cy + s * 0.6f), new PointF(cx, cy + s * 0.6f), new PointF(cx - s * 0.1f, cy + s * 0.9f), new PointF(cx + s * 0.2f, cy + s * 0.5f), new PointF(cx + s * 0.05f, cy + s * 0.5f) });
        if (IsFog)
            using (Pen p = new Pen(Color.FromArgb(220, 225, 235), Math.Max(1, s * 0.09f)))
                for (int i = 0; i < 3; i++) g.DrawLine(p, cx - s * 0.5f + (i % 2) * s * 0.1f, cy + s * 0.35f + i * s * 0.18f, cx + s * 0.5f, cy + s * 0.35f + i * s * 0.18f);
    }

    RectangleF WidgetRect()
    {
        float w = W(72), h = W(22);
        return new RectangleF(Width - w - W(2), W(1.5f), w, h);
    }

    void DrawWeatherWidget(Graphics g)
    {
        if (!WxVisible) return;
        RectangleF r = WidgetRect();
        using (GraphicsPath p = RoundRect(r.X, r.Y, r.Width, r.Height, W(3.5f)))
        {
            float n = NightK();
            Color top = n > 0.5f ? Color.FromArgb(215, 40, 46, 70) : Color.FromArgb(215, 70, 130, 210);
            Color bot = n > 0.5f ? Color.FromArgb(215, 25, 28, 45) : Color.FromArgb(215, 40, 90, 170);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), top, bot, 90f)) g.FillPath(b, p);
            using (Pen pp = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(1, W(0.25f)))) g.DrawPath(pp, p);
        }
        TextRenderingHint th = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using (SolidBrush w = new SolidBrush(Color.White))
        using (SolidBrush w2 = new SolidBrush(Color.FromArgb(210, 230, 240, 255)))
        {
            if (!wxOk)
            {
                g.DrawString(wxErr != null ? "Погода: нет связи" : "Загружаю погоду...", fntWxD, w, r.X + W(3), r.Y + W(6));
                g.DrawString(wxErr != null ? "повторю через минуту" : wxCity, fntWxS, w2, r.X + W(3), r.Y + W(12));
            }
            else
            {
                DrawWxIcon(g, r.X + W(10.5f), r.Y + W(10), W(11));
                int t = (int)Math.Round(wxTemp);
                string ts = (t < 0 ? "−" + (-t) : (t > 0 ? "+" + t : "0")) + "°";
                g.DrawString(ts, fntWxT, w, r.X + W(20), r.Y + W(0.4f));
                SizeF tsz = g.MeasureString(ts, fntWxT);
                int fl = (int)Math.Round(wxFeels);
                g.DrawString("ощущ. " + (fl < 0 ? "−" + (-fl) : fl.ToString()) + "°", fntWxS, w2, r.X + W(20.6f) + tsz.Width, r.Y + W(4.2f));
                g.DrawString(WxText(wxCode), fntWxD, w, r.X + W(20.6f), r.Y + W(10.4f));
                g.DrawString(wxCity + " · ветер " + wxWind.ToString("0", CultureInfo.InvariantCulture) + " м/с", fntWxS, w2, r.X + W(20.8f), r.Y + W(15.6f));
            }
        }
        g.TextRenderingHint = th;
    }

    // ---- одежда по погоде ----
    void DrawScarf(Graphics g, Cat c)
    {
        Color[] sc = { Color.FromArgb(220, 60, 70), Color.FromArgb(60, 120, 220), Color.FromArgb(250, 190, 60), Color.FromArgb(70, 170, 110), Color.FromArgb(170, 100, 220) };
        Color col = sc[Math.Abs(c.Id) % sc.Length];
        using (GraphicsPath p = RoundRect(-3.9f, -14.4f, 8.2f, 2.2f, 1.0f))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4, -14.5f, 8.4f, 2.4f), Light(col, 0.15f), Dark(col, 0.8f), 90f)) g.FillPath(b, p);
            using (Pen pp = new Pen(Dark(col, 0.55f), 0.25f)) g.DrawPath(pp, p);
        }
        float sw = (float)Math.Sin((tick + c.Id * 9) * 0.15) * 0.4f;
        PointF[] tail = { P(2.0f, -12.6f), P(3.4f, -12.6f), P(3.8f + sw, -9.0f), P(2.6f + sw, -9.2f) };
        using (SolidBrush b = new SolidBrush(Dark(col, 0.9f))) g.FillPolygon(b, tail);
        using (Pen pp = new Pen(Color.FromArgb(160, 255, 255, 255), 0.3f)) { g.DrawLine(pp, 2.4f + sw * 0.7f, -10.6f, 3.6f + sw * 0.8f, -10.6f); g.DrawLine(pp, -2.6f, -13.3f, -1.6f, -13.3f); g.DrawLine(pp, 0.2f, -13.3f, 1.2f, -13.3f); }
    }

    void DrawUmbrella(Graphics g, Cat c)
    {
        Color[] uc = { Color.FromArgb(230, 60, 80), Color.FromArgb(60, 130, 230), Color.FromArgb(250, 200, 60), Color.FromArgb(40, 40, 50), Color.FromArgb(150, 90, 220) };
        Color col = uc[Math.Abs(c.Id + 2) % uc.Length];
        using (Pen p = new Pen(Color.FromArgb(70, 60, 50), 0.35f)) { g.DrawLine(p, 2.8f, -15.2f, 2.6f, -30.0f); g.DrawArc(p, 2.2f, -15.6f, 1.2f, 1.6f, 0, 180); }
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddArc(-6.4f, -34.4f, 18f, 9f, 180, 180);
            float y = -29.9f;
            p.AddBezier(11.6f, y, 10.4f, y - 1.2f, 8.8f, y - 1.2f, 7.6f, y);
            p.AddBezier(7.6f, y, 6.4f, y - 1.2f, 4.0f, y - 1.2f, 2.6f, y);
            p.AddBezier(2.6f, y, 1.2f, y - 1.2f, -1.2f, y - 1.2f, -2.4f, y);
            p.AddBezier(-2.4f, y, -3.6f, y - 1.2f, -5.2f, y - 1.2f, -6.4f, y);
            p.CloseFigure();
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-6.5f, -34.5f, 18.2f, 5f), Light(col, 0.2f), Dark(col, 0.8f), 90f)) g.FillPath(b, p);
            using (Pen pp = new Pen(Dark(col, 0.5f), 0.25f)) g.DrawPath(pp, p);
        }
        using (Pen pp = new Pen(Color.FromArgb(70, 0, 0, 0), 0.2f)) { g.DrawLine(pp, 2.6f, -34.2f, -2.4f, -30f); g.DrawLine(pp, 2.6f, -34.2f, 7.6f, -30f); }
    }

    bool WantsUmbrella(Cat c)
    {
        if (!IsRain || c.Hold != ' ' || c.Sitting || c.Swimming || c.Lying || c.Hidden || c.Cuffed || c.Outfit == 11) return false;
        if (mode == 5 && omonPhase == 1) return false;
        return mode == 0 || mode == 4 || (mode == 1 && c != me && c.Walking);
    }

    // ================= сценарий: ревизия =================
    int countShelf = -1, countIdx, lastRev = -1; bool revPending;

    Cat MakeBoss()
    {
        Cat boss = new Cat();
        boss.Id = catIds++; boss.Boss = true; boss.Scale = 1.35f; boss.Vape = true; boss.Outfit = 3;
        boss.C1 = Color.FromArgb(38, 38, 48); boss.C2 = Color.FromArgb(210, 40, 50); boss.Glasses = true;
        boss.IrisC = Color.FromArgb(230, 170, 50);
        boss.MainC = Color.FromArgb(52, 50, 60); boss.BellyC = Color.FromArgb(125, 120, 135);
        boss.Phase = 9; boss.Speed = 1.0f;
        return boss;
    }

    Func<bool> CountStep()
    {
        int t = 0;
        return delegate
        {
            t++;
            if (t % 6 == 0)
            {
                countIdx++;
                if (countIdx % 6 == 0 && countIdx < 18) Say("*пик* " + countIdx, ShelfX(Math.Max(0, countShelf)) + U(14), Ground - U(50), 'K');
            }
            return countIdx >= 18;
        };
    }

    void SetupRevision()
    {
        countShelf = -1; digging = false; revPending = true;
        Poof(me.X, CatTop(me) + U(8));
        me.X = CashX; me.Dir = 1;
        script = null;
    }

    List<Func<bool>> BuildRevision()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        Cat boss = null;
        Func<Cat> B = delegate { return boss; };
        int variant; do { variant = rnd.Next(3); } while (variant == lastRev);
        lastRev = variant;
        int baseN = 30 + rnd.Next(25), miss = 2 + rnd.Next(3);

        s.Add(Wait(30));
        string[] idle = { "Ля-ля-ля... тихий денёк", "Сейчас бы чайку...", "Касса сошлась, красота" };
        s.Add(Line(me, idle[rnd.Next(idle.Length)], 40, 'h'));
        s.Add(Do(delegate
        {
            boss = MakeBoss(); boss.X = Width + U(16); boss.Dir = -1; boss.Hold = 'T'; boss.HoldCol = 'G';
            boss.Speed = 1.8f; guests.Add(boss); Go(boss, CounterX + U(34));
        }));
        s.Add(delegate { return !boss.Walking; });
        s.Add(Do(delegate { boss.Speed = 1.0f; }));
        if (rnd.Next(100) < 40) s.Add(SayF(B, "Продавец... я — твой начальник.", 50, 'K'));
        s.Add(SayF(B, "Так-так-так. Внеплановая ревизия!", 50, 'K'));
        s.Add(Do(delegate { me.SweatT = 150; me.Shake = 25; }));
        s.Add(Line(me, "О... з-здрасьте, начальник!", 40, 'w'));
        foreach (int sh in new int[] { 1, 0 })
        {
            int shelf = sh;
            s.Add(GoF(B, delegate { return ShelfX(shelf) + U(16); }));
            s.Add(Do(delegate { boss.Dir = -1; countShelf = shelf; countIdx = 0; }));
            s.Add(CountStep());
            s.Add(Do(delegate { countShelf = -1; }));
        }
        s.Add(GoF(B, delegate { return CounterX + U(34); }));
        s.Add(Do(delegate { boss.Dir = -1; }));
        s.Add(SayF(B, "По базе " + baseN + " флаконов. На полках — " + (baseN - miss) + ".", 55, 'K'));
        s.Add(Do(delegate { boss.AngryT = 70; boss.Shake = 20; }));
        s.Add(SayF(B, "Где ещё " + miss + "?!", 40, 'R'));

        string[,] pairs = {
            { "Это... усушка!", "Жижа не усыхает!" },
            { "Испарилась! Это же пар!", "Очень смешно." },
            { "Тестеры! Покупатели пробовали!", miss + " флакона?!" },
            { "Мышь утащила!", "Мышь. С вейпом." },
            { "Пересорт! В МойСклад не туда провели!", "Хм... проверю." },
            { "Это... инфляция?", "Это не так работает." },
            { "Их забрал лысый на Ларгусе!", "Лысый... опять он." } };
        List<int> idx = new List<int>();
        while (idx.Count < 3) { int k = rnd.Next(pairs.GetLength(0)); if (!idx.Contains(k)) idx.Add(k); }
        foreach (int k in idx)
        {
            string ex = pairs[k, 0], rep = pairs[k, 1];
            s.Add(Line(me, ex, 45, 'w'));
            s.Add(Do(delegate { boss.AngryT = 50; }));
            s.Add(SayF(B, rep, 40, 'K'));
        }

        if (variant == 0) // нашлись под прилавком
        {
            s.Add(Line(me, "Подождите! Щас...", 30, 'w'));
            s.Add(Do(delegate { me.Sitting = true; digging = true; }));
            s.Add(Wait(75));
            s.Add(Do(delegate
            {
                me.Sitting = false; digging = false;
                for (int i = 0; i < miss; i++)
                {
                    char cc = ItemColors[rnd.Next(ItemColors.Length)];
                    Fly('B', cc, CounterX + U(4), Ground - U(8), ShelfX(i % 2), Ground - U(30), 16 + i * 4, null);
                }
            }));
            s.Add(Line(me, "Нашёл! Закатились под прилавок!", 50, 'h'));
            s.Add(SayF(B, "Хм. Повезло тебе. Работай.", 45, 'K'));
        }
        else if (variant == 1) // в карманах продавца
        {
            s.Add(SayF(B, "А ну-ка, выверни карманы.", 40, 'K'));
            s.Add(Do(delegate
            {
                me.Shake = 40; me.SweatT = 100;
                for (int i = 0; i < miss; i++)
                {
                    PointF pw = Paw(me);
                    Fly('B', ItemColors[rnd.Next(ItemColors.Length)], pw.X, pw.Y, me.X + U(-6 + i * 4), Ground - U(1.5f), 14 + i * 5, null);
                }
            }));
            s.Add(Wait(30));
            s.Add(Line(me, "Это... не моё... оно само...", 45, 'w'));
            s.Add(Do(delegate { boss.AngryT = 90; boss.StompT = 42; }));
            s.Add(SayF(B, "Из зарплаты вычту!", 45, 'R'));
            s.Add(Do(delegate { AddMoney(-miss * 450, true); }));
            s.Add(Line(me, "Эх...", 30, 'w'));
        }
        else // у самого начальника
        {
            s.Add(Do(delegate
            {
                boss.Shake = 20;
                for (int i = 0; i < miss; i++)
                {
                    PointF bp = Local(boss, 0, -9);
                    Fly('B', ItemColors[rnd.Next(ItemColors.Length)], bp.X, bp.Y, boss.X + U(-5 + i * 4), Ground - U(1.5f), 14 + i * 5, null);
                }
            }));
            s.Add(Wait(30));
            s.Add(Line(me, "Начальник... а это не ваши?", 45, 'n'));
            s.Add(Do(delegate { boss.SweatT = 100; boss.Shake = 30; }));
            s.Add(SayF(B, "...Ревизия окончена.", 40, 'K'));
            if (rnd.Next(2) == 0) s.Add(SayF(B, "Я вернусь.", 35, 'K'));
            s.Add(Do(delegate { boss.Speed = 2.8f; }));
        }
        s.Add(GoF(B, delegate { return Width + U(18); }));
        s.Add(Do(delegate { guests.Remove(boss); }));
        string[] after = variant == 0 ? new string[] { "Фух... пронесло" } : (variant == 1 ? new string[] { "Ну и денёк..." } : new string[] { "Ха! Вот это поворот", "Ревизор, называется" });
        s.Add(Line(me, after[rnd.Next(after.Length)], 45, variant == 1 ? 'w' : 'h'));
        return s;
    }

    void UpdateRevision()
    {
        if (enterT < 40) return;
        if (digging && tick % 7 == 0)
        {
            float dx = me.X - U(4 + (float)rnd.NextDouble() * 14);
            Fly('X', ItemColors[rnd.Next(ItemColors.Length)], CounterX + U(4), Ground - U(12), dx, Ground - U(2), 18, null);
        }
        if (RunScript()) return;
        if (revPending) { revPending = false; script = BuildRevision(); }
        else
        {
            revPending = true;
            script = new List<Func<bool>>();
            script.Add(Do(delegate { if (me.Walking == false && Math.Abs(me.X - CashX) > U(1)) Go(me, CashX); }));
            script.Add(delegate { return !me.Walking; });
            script.Add(Do(delegate { me.Dir = 1; if (me.PuffT == 0) me.PuffT = 90; }));
            script.Add(Wait(300 + rnd.Next(300)));
        }
        scriptIdx = 0;
    }

    // ================= пасхалки =================
    List<Func<bool>> egg; int eggIdx, eggCooldown = 1800;
    string bigText; int bigT, bigMax, bigStyle, matrixT;
    Font fntBigSerif, fntBigBold, fntMatrix;

    void BigText(string t, int style, int dur) { bigText = t; bigStyle = style; bigT = 0; bigMax = dur; }

    void RunEgg()
    {
        if (egg == null) return;
        int guard = 0;
        while (egg != null && eggIdx < egg.Count && guard++ < 8) { if (egg[eggIdx]()) eggIdx++; else break; }
        if (egg != null && eggIdx >= egg.Count) egg = null;
    }

    void UpdateEggs()
    {
        if (bigText != null && ++bigT > bigMax) bigText = null;
        if (matrixT > 0)
        {
            matrixT--;
            if (tick % 2 == 0)
                for (int i = 0; i < 2; i++)
                {
                    Particle p = new Particle();
                    p.Kind = 8; p.X = (float)Math.Floor(rnd.NextDouble() * (Width / U(3))) * U(3); p.Y = -U(3);
                    p.VY = (0.6f + (float)rnd.NextDouble() * 0.6f) * u; p.Max = (int)(Height / p.VY) + 4;
                    p.Col = "01ЖИЖАVAPE0110КОТ"[rnd.Next(17)];
                    parts.Add(p);
                }
        }
        RunEgg();
        if (--eggCooldown <= 0)
        {
            eggCooldown = 2400 + rnd.Next(2400);
            if (egg == null && pendingMode < 0) TryRandomEgg();
        }
    }

    Cat AnyGuest()
    {
        List<Cat> l = new List<Cat>();
        foreach (Cat c in guests) if (!c.Hidden && c.Outfit != 11 && !c.Boss) l.Add(c);
        return l.Count == 0 ? null : l[rnd.Next(l.Count)];
    }

    void TryRandomEgg()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        int r = rnd.Next(100);
        switch (mode)
        {
            case 0:
                if ((IsSnow || IsCold) && r < 30) s.Add(Line(me, "Зима близко...", 45, 'n'));
                else if (r < 45) s.Add(Line(me, "Раньше я тоже гулял... пока не словил стрелу в колено", 60, 'w'));
                else if (r < 70) s.Add(Line(me, "Жизнь как коробка конфет: не знаешь, какая жижа попадётся", 60, 'h'));
                else
                {
                    s.Add(Do(delegate { me.SweatT = 120; matrixT = 170; }));
                    s.Add(Line(me, "Кажется... я вижу код", 50, 'w'));
                    s.Add(Wait(60));
                    s.Add(Line(me, "Ложки нет. Есть только жижа.", 50, 'h'));
                }
                break;
            case 1:
            {
                Cat f = null;
                foreach (Cat g in guests) if (g.Phase <= 2 && !g.Walking) { f = g; break; }
                if (f == null) { s.Add(Line(me, "Касса — мой путь. Таков путь.", 45, 'h')); break; }
                Cat ff = f;
                s.Add(SayStep(me, "Скажи моё имя.", 40));
                s.Add(SayF(delegate { return ff; }, "...Кассир?", 35, 'K'));
                s.Add(Line(me, "Ты чертовски прав.", 40, 'h'));
                break;
            }
            case 2:
                if (tvState == 4 && r < 45)
                {
                    s.Add(Do(delegate { me.SweatT = 60; }));
                    s.Add(Do(delegate { BigText("ВЫ ПОГИБЛИ", 0, 110); }));
                    s.Add(Wait(40));
                    s.Add(Line(me, "Опять с первого костра...", 45, 'w'));
                }
                else if (r < 70) s.Add(Line(me, "Когда уже Half-Life 3?", 45, 'w'));
                else s.Add(Line(me, "Ещё одну катку — и спать. Честно.", 45, 'h'));
                break;
            case 3:
                if (celebrated && r < 50) s.Add(Do(delegate { BigText("МИССИЯ ВЫПОЛНЕНА|+ уважение", 2, 110); me.HappyT = 60; }));
                else if (r < 75) s.Add(Line(me, "Это не склад, это квест какой-то", 45, 'w'));
                else s.Add(Line(me, "Работа не волк... но и не жижа", 45, 'n'));
                break;
            case 4:
            {
                if (r < 35)
                {
                    s.Add(SayF(delegate { return AnyGuest() ?? me; }, "Танцуют все!", 40, 'K'));
                    s.Add(Do(delegate { foreach (Cat c in guests) if (!c.Swimming && !c.Sitting && !c.Jumping) c.HappyT = 90; me.HappyT = 90; }));
                }
                else if (r < 65) s.Add(SayF(delegate { return AnyGuest() ?? me; }, "Акуна матата!", 40, 'K'));
                else { s.Add(Do(delegate { me.HappyT = 80; })); s.Add(Line(me, "Я король мира!", 45, 'h')); }
                break;
            }
            case 5:
                if (omonPhase != 1 || inmates.Count < 2) return;
                {
                    Cat a = inmates[rnd.Next(inmates.Count)], b = inmates[rnd.Next(inmates.Count)];
                    if (r < 25) s.Add(Line(a, "Пасть порву, моргалы выколю!", 50, 'a'));
                    else if (r < 45) s.Add(Line(a, "Граждане! Храните деньги в сберегательной кассе!", 60, 'h'));
                    else if (r < 65) s.Add(Line(a, "Пацаны не извиняются.", 45, 'n'));
                    else { s.Add(Line(a, "В чём сила, брат?", 40, 'n')); if (b != a) s.Add(Line(b, "Сила в правде.", 40, 'n')); }
                }
                break;
            case 6:
                s.Add(Line(me, r < 50 ? "Ревизор... как у Гоголя" : "Главное — не паниковать", 45, 'w'));
                break;
        }
        if (s.Count > 0) { egg = s; eggIdx = 0; }
    }

    void DrawBigText(Graphics g)
    {
        if (bigText == null) return;
        float k = bigT < 12 ? bigT / 12f : (bigT > bigMax - 18 ? Math.Max(0, (bigMax - bigT) / 18f) : 1f);
        int a = (int)(255 * k);
        float cy = Height * 0.45f;
        string[] lines = bigText.Split('|');
        if (bigStyle == 0)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, cy - U(10), Width, U(20)), Color.FromArgb((int)(170 * k), 0, 0, 0), Color.FromArgb((int)(170 * k), 0, 0, 0), 90f))
            {
                ColorBlend cb = new ColorBlend();
                cb.Colors = new Color[] { Color.FromArgb(0, 0, 0, 0), Color.FromArgb((int)(180 * k), 0, 0, 0), Color.FromArgb(0, 0, 0, 0) };
                cb.Positions = new float[] { 0, 0.5f, 1 };
                b.InterpolationColors = cb;
                g.FillRectangle(b, 0, cy - U(10), Width, U(20));
            }
            OutlinedText(g, lines[0], fntBigSerif, Color.FromArgb(a, 175, 25, 25), Color.FromArgb(a / 2, 0, 0, 0), Width / 2f, cy, 1.0f);
        }
        else if (bigStyle == 1)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, cy - U(12), Width, U(24)), Color.Black, Color.Black, 90f))
            {
                ColorBlend cb = new ColorBlend();
                cb.Colors = new Color[] { Color.FromArgb(0, 60, 60, 60), Color.FromArgb((int)(150 * k), 60, 60, 60), Color.FromArgb(0, 60, 60, 60) };
                cb.Positions = new float[] { 0, 0.5f, 1 };
                b.InterpolationColors = cb;
                g.FillRectangle(b, 0, cy - U(12), Width, U(24));
            }
            OutlinedText(g, lines[0], fntBigBold, Color.FromArgb(a, 235, 235, 235), Color.FromArgb(a, 0, 0, 0), Width / 2f, cy, 1.0f);
        }
        else
        {
            OutlinedText(g, lines[0], fntBigBold, Color.FromArgb(a, 255, 210, 60), Color.FromArgb(a, 0, 0, 0), Width / 2f, cy - (lines.Length > 1 ? U(4) : 0), 0.85f);
            if (lines.Length > 1) OutlinedText(g, lines[1], fnt, Color.FromArgb(a, 255, 255, 255), Color.FromArgb(a, 0, 0, 0), Width / 2f, cy + U(5), 1.0f);
        }
    }

    void OutlinedText(Graphics g, string t, Font f, Color fill, Color outline, float cx, float cy, float scale)
    {
        using (GraphicsPath p = new GraphicsPath())
        using (StringFormat sf = new StringFormat())
        {
            sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
            p.AddString(t, f.FontFamily, (int)f.Style, f.Size * scale, new PointF(cx, cy), sf);
            using (Pen pen = new Pen(outline, Math.Max(1.5f, f.Size * scale * 0.14f))) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
            using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
        }
    }

    // ================= GTA: деньги и здоровье =================
    List<float[]> pickups = new List<float[]>(); // x, сумма
    long money; float health = 100; int hudFlashT, hurtFlashT;
    Font fntHud;

    void AddMoney(long d, bool bubble)
    {
        money += d; if (money < 0) money = 0;
        hudFlashT = 25;
        if (bubble) Say((d >= 0 ? "+$" : "-$") + Math.Abs(d), me.X, CatTop(me) - U(1), d >= 0 ? 'n' : 'R');
        SaveSettings();
    }

    void Hurt(float dmg)
    {
        health -= dmg; hurtFlashT = 20;
        if (health <= 0)
        {
            health = 100;
            long fee = money / 10;
            BigText("ПОТРАЧЕНО", 1, 120);
            List<Func<bool>> e = new List<Func<bool>>();
            e.Add(Wait(70));
            e.Add(Do(delegate { if (fee > 0) AddMoney(-fee, false); }));
            e.Add(Line(me, fee > 0 ? "Больница: -$" + fee + "..." : "Больница... хорошо, что бесплатно", 55, 'w'));
            egg = e; eggIdx = 0;
        }
    }

    void UpdateGta()
    {
        if (health < 100) health = Math.Min(100, health + 0.03f);
        if (hudFlashT > 0) hudFlashT--;
        if (hurtFlashT > 0) hurtFlashT--;
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            float[] p = pickups[i];
            if (!me.Hidden && !me.Lying && Math.Abs(me.X - p[0]) < U(3.2f) && me.Y < U(2))
            {
                pickups.RemoveAt(i);
                for (int k = 0; k < 5; k++)
                {
                    Particle sp = new Particle(); sp.Kind = 4; sp.X = p[0]; sp.Y = Ground - U(3);
                    double a = k * Math.PI * 2 / 5; sp.VX = (float)Math.Cos(a) * 0.3f * u; sp.VY = (float)Math.Sin(a) * 0.3f * u - 0.15f * u; sp.Max = 25;
                    parts.Add(sp);
                }
                AddMoney((long)p[1], true);
            }
        }
    }

    void DrawPickups(Graphics g)
    {
        foreach (float[] p in pickups)
        {
            float cx = p[0];
            float bob = (float)Math.Sin((tick + cx) * 0.1) * U(0.5f);
            float cy = Ground - U(4.2f) + bob;
            Glow(g, cx, Ground - U(0.6f), U(4.5f), U(1.4f), Color.FromArgb(90, 255, 110), 150);
            Glow(g, cx, cy, U(4), U(4), Color.FromArgb(140, 255, 150), 70);
            float sc = (float)Math.Cos(tick * 0.12 + cx * 0.05);
            float w = U(6.2f) * Math.Max(0.12f, Math.Abs(sc));
            for (int i = 2; i >= 0; i--)
            {
                RectangleF r = new RectangleF(cx - w / 2, cy - U(1.5f) - i * U(0.6f), w, U(2.9f));
                using (GraphicsPath gp = RoundRect(r.X, r.Y, r.Width, r.Height, U(0.3f)))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width + 0.1f, r.Height + 2), Color.FromArgb(120, 200, 110), Color.FromArgb(40, 110, 50), 90f)) g.FillPath(b, gp);
                    using (Pen pp = new Pen(Color.FromArgb(25, 70, 30), Math.Max(1, U(0.2f)))) g.DrawPath(pp, gp);
                }
            }
            if (Math.Abs(sc) > 0.45f)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 230, 255, 220))) g.FillEllipse(b, cx - w * 0.22f, cy - U(1.1f) - U(0.1f), w * 0.44f, U(1.5f));
                using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 90, 35))) g.DrawString("$", fntTiny, b, cx, cy - U(0.35f), sf); }
            }
            if (tick % 18 == (int)cx % 18)
            {
                Particle s = new Particle(); s.Kind = 4; s.X = cx + U((float)rnd.NextDouble() * 4 - 2); s.Y = cy - U(2); s.VY = -0.1f * u; s.Max = 20;
                parts.Add(s);
            }
        }
    }

    void DrawHud(Graphics g)
    {
        try { DrawPanel(g); } catch (Exception ex) { GuardLog("панель", ex); }
    }


    // ================= костюмированные гости =================
    int lastCostume = -1;

    void ApplyCostume(Cat c, int id)
    {
        c.Hat = 0; c.Glasses = false; c.SmallShades = false; c.Speed = 1.0f; c.HatBack = false;
        switch (id)
        {
            case 0: c.Outfit = 12; c.C1 = Color.FromArgb(24, 24, 28); c.C2 = Color.FromArgb(20, 20, 24); c.SmallShades = true; c.MainC = Color.FromArgb(70, 66, 74); c.BellyC = Color.FromArgb(160, 154, 166); break;
            case 1: c.Outfit = 13; c.C1 = Color.FromArgb(196, 182, 150); c.C2 = Color.FromArgb(60, 86, 140); break;
            case 2: c.Outfit = 4; c.C1 = Color.FromArgb(28, 30, 40); c.C2 = Color.White; c.Hat = 1; c.HatC = Color.FromArgb(24, 24, 30); c.MainC = Color.FromArgb(120, 112, 104); c.BellyC = Color.FromArgb(200, 192, 182); break;
            case 3: c.Outfit = 9; c.C1 = Color.FromArgb(28, 26, 30); c.C2 = Color.FromArgb(40, 44, 60); c.Glasses = true; c.MainC = Color.FromArgb(150, 146, 140); c.BellyC = Color.FromArgb(220, 214, 206); c.Vape = false; break;
            case 4: c.Outfit = 12; c.C1 = Color.FromArgb(124, 86, 54); c.C2 = Color.FromArgb(96, 66, 42); c.Hat = 9; c.HatC = Color.FromArgb(110, 76, 48); c.MainC = Color.FromArgb(242, 208, 155); c.BellyC = Color.FromArgb(255, 246, 228); c.Vape = false; break;
            case 5: c.Outfit = 14; c.C1 = Color.FromArgb(176, 150, 110); c.C2 = Color.FromArgb(90, 74, 60); c.Hat = 7; c.HatC = Color.FromArgb(176, 150, 110); break;
            case 6: c.Outfit = 15; c.C1 = Color.FromArgb(96, 104, 70); c.C2 = Color.FromArgb(60, 64, 50); c.Hat = 8; c.HatC = Color.FromArgb(96, 104, 70); c.Vape = false; break;
            case 7: c.Outfit = 16; c.C1 = Color.FromArgb(245, 245, 248); c.C2 = Color.FromArgb(50, 46, 60); c.Hat = 10; c.HatC = Color.FromArgb(30, 28, 34); break;
            case 8: c.Outfit = 17; c.C1 = Color.FromArgb(236, 226, 205); c.C2 = Color.FromArgb(70, 90, 140); c.Hat = 11; c.HatC = Color.FromArgb(150, 100, 60); break;
            case 9: c.Outfit = 18; c.C1 = Color.FromArgb(248, 248, 250); c.C2 = Color.FromArgb(80, 120, 170); c.Hat = 16; c.HatC = Color.FromArgb(200, 205, 215); c.Vape = false; break;
            case 10: c.Outfit = 19; c.C1 = Color.FromArgb(240, 240, 244); c.C2 = Color.FromArgb(232, 232, 238); c.Hat = 12; c.Vape = false; break;
            case 11: c.Outfit = 20; c.C1 = Color.FromArgb(22, 22, 26); c.C2 = Color.FromArgb(22, 22, 26); c.MainC = Color.FromArgb(246, 246, 246); c.BellyC = Color.FromArgb(255, 220, 210); break;
            case 12: c.Outfit = 21; c.C1 = Color.FromArgb(40, 52, 96); c.C2 = Color.FromArgb(40, 40, 50); c.Hat = 13; c.HatC = Color.FromArgb(220, 40, 50); c.Vape = false; break;
            case 13: c.Outfit = 22; c.C1 = Color.FromArgb(50, 90, 210); c.C2 = Color.FromArgb(50, 90, 210); c.Hat = 14; c.HatC = Color.FromArgb(200, 30, 50); c.Vape = false; break;
            case 14: c.Outfit = 23; c.C1 = Color.FromArgb(90, 60, 160); c.C2 = Color.FromArgb(70, 46, 130); c.Hat = 15; c.HatC = Color.FromArgb(90, 60, 160); c.Vape = false; break;
        }
    }

    void AddCostumeDialog(List<Func<bool>> s, Cat f)
    {
        char col = ItemColors[rnd.Next(ItemColors.Length)];
        switch (f.DialogV)
        {
            case 0: f.DialogV = 6; AddDialog(s, f); return;
            case 1: f.DialogV = 7; AddDialog(s, f); return;
            case 2:
                s.Add(Line(f, "Ты с какого района, кассир?", 45, 'a'));
                s.Add(Line(me, "С вейп-района :)", 35, 'h'));
                s.Add(Line(f, "Ладно... дай жижу. Пацаны не извиняются.", 50, 'n'));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, false);
                s.Add(Line(f, "Бывай.", 25, 'n'));
                break;
            case 3:
                s.Add(Line(f, "Мне нужна твоя жижа, твой вейп и твой испаритель.", 60, 'n'));
                s.Add(Line(me, "Э-э... может, просто купите?", 40, 'w'));
                s.Add(Line(f, "...Хорошо.", 30, 'n'));
                AddFetch(s, 'K'); AddScan(s, 'K'); AddHandOver(s, f, 'K', false);
                s.Add(Line(f, "Я вернусь.", 35, 'n'));
                break;
            case 4:
                s.Add(Line(f, "Да пребудет с тобой Сила.", 45, 'n'));
                s.Add(Line(me, "И с вами... Вам что-нибудь подсказать?", 45, 'h'));
                s.Add(Do(delegate { f.HappyT = 40; }));
                s.Add(Line(f, "Это не та жижа, что ты ищешь. Дай мятную.", 55, 'n'));
                s.Add(Do(delegate { me.BlinkT = 40; }));
                s.Add(Line(me, "...Это не та жижа. Дам мятную.", 45, 'n'));
                AddFetch(s, 'N'); AddScan(s, 'N'); AddHandOver(s, f, 'N', false);
                break;
            case 5:
                s.Add(Line(f, "Элементарно! Вы курите клубнику и не завтракали.", 55, 'n'));
                s.Add(Line(me, "...Как вы узнали?!", 35, 'w'));
                s.Add(Line(f, "Дедукция. И крошки на фартуке.", 45, 'h'));
                AddFetch(s, 'R'); AddScan(s, 'R'); AddHandOver(s, f, 'R', false);
                break;
            case 6:
                s.Add(Line(f, "Здарова, кассир. Тут аномалия рядом — цены видал?", 55, 'n'));
                s.Add(Line(me, "Цены как цены :)", 30, 'h'));
                s.Add(Line(f, "Ладно. Дай что-нибудь от радиации. Мятное.", 50, 'n'));
                AddFetch(s, 'N'); AddScan(s, 'N'); AddHandOver(s, f, 'N', false);
                s.Add(Line(f, "Удачной охоты, кассир.", 35, 'n'));
                break;
            case 7:
                s.Add(Line(f, "Йо-хо-хо! Где тут ром?", 45, 'h'));
                s.Add(Line(me, "Рома нет. Есть жижа со вкусом рома!", 45, 'h'));
                s.Add(Line(f, "Тысяча чертей! Беру!", 35, 'h'));
                AddFetch(s, 'B'); AddScan(s, 'B'); AddHandOver(s, f, 'B', false);
                break;
            case 8:
                s.Add(Line(f, "Этот город слишком мал для нас двоих, кассир.", 55, 'a'));
                s.Add(Line(me, "Так я и не уезжаю...", 35, 'w'));
                s.Add(Line(f, "...Тогда дай жижу. Со вкусом виски.", 45, 'n'));
                AddFetch(s, 'Y'); AddScan(s, 'Y'); AddHandOver(s, f, 'Y', false);
                s.Add(Line(f, "Йи-ха!", 25, 'h'));
                break;
            case 9:
                s.Add(Line(f, "Минздрав предупреждает!", 45, 'a'));
                s.Add(Line(me, "Ой... а вы за чем?", 35, 'w'));
                s.Add(Line(f, "...Мне мятную. Только никому ни слова.", 50, 'n'));
                AddFetch(s, 'N'); AddScan(s, 'N'); AddHandOver(s, f, 'N', false);
                break;
            case 10:
                s.Add(Line(f, "Поехали!", 35, 'h'));
                s.Add(Line(me, "Куда?", 25, 'w'));
                s.Add(Line(f, "За жижей! На орбите закончилась.", 45, 'n'));
                AddFetch(s, 'U'); AddScan(s, 'U'); AddHandOver(s, f, 'U', false);
                s.Add(Line(f, "Земля, приём! Задание выполнено.", 40, 'h'));
                break;
            case 11:
                s.Add(Line(f, "Жижу. Взболтать, но не смешивать.", 50, 'n'));
                s.Add(Line(me, "Сделаем, агент.", 30, 'h'));
                AddFetch(s, col);
                s.Add(Do(delegate { me.Shake = 35; }));
                s.Add(Wait(35));
                AddScan(s, col); AddHandOver(s, f, col, false);
                s.Add(Line(f, "Превосходно.", 30, 'n'));
                break;
            case 12:
                s.Add(Line(f, "У самурая нет цели, только путь.", 50, 'n'));
                s.Add(Line(me, "Путь к кассе — вот здесь :)", 35, 'h'));
                s.Add(Line(f, "Мудро.", 25, 'n'));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, false);
                break;
            case 13:
                s.Add(Line(f, "Не бойтесь, граждане! Я спасу этот магазин!", 50, 'h'));
                s.Add(Line(me, "От чего?", 25, 'w'));
                s.Add(Line(f, "От лишней жижи! Беру три!", 40, 'h'));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, false);
                s.Add(Do(delegate { f.Speed = 3.6f; }));
                s.Add(Line(f, "Вперёд, навстречу подвигам!", 25, 'h'));
                break;
            default:
                s.Add(Line(f, "Ты волшебник, кассир!", 40, 'h'));
                s.Add(Line(me, "Я просто пробиваю товар...", 40, 'w'));
                s.Add(Line(f, "Тогда наколдуй мне арбузную.", 40, 'n'));
                s.Add(Do(delegate { Stars(Local(me, 1, -24)); Stars(Local(me, 3, -14)); me.HappyT = 40; }));
                AddFetch(s, 'R'); AddScan(s, 'R'); AddHandOver(s, f, 'R', false);
                s.Add(Line(f, "Магия!", 25, 'h'));
                break;
        }
        s.Add(LeaveShop(f));
    }

    void DrawCape(Graphics g, Cat c)
    {
        float sw = (float)Math.Sin((tick + c.Id * 13) * 0.15) * 0.8f + (c.Walking ? 1.2f : 0);
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddBezier(-1.8f, -12.8f, -4f, -9f, -6.5f - sw, -5f, -7.5f - sw, -1.2f);
            p.AddLine(-7.5f - sw, -1.2f, -1.2f - sw * 0.4f, -0.8f);
            p.AddBezier(-1.2f - sw * 0.4f, -0.8f, -0.6f, -5f, 1.2f, -9f, 2.0f, -12.8f);
            p.CloseFigure();
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-8.5f, -13f, 11f, 12.5f), Color.FromArgb(230, 40, 50), Color.FromArgb(140, 20, 30), 0f)) g.FillPath(b, p);
            using (Pen pp = new Pen(Color.FromArgb(90, 10, 20), 0.35f)) g.DrawPath(pp, p);
        }
    }

    void DrawCostumeTorso(Graphics g, Cat c, int o, Color c1, Color c2, Pen op)
    {
        Func<float, float, float, float, GraphicsPath> body = delegate (float x, float y, float w, float h) { return RoundRect(x, y, w, h, 3.0f); };
        if (o == 14) // клетчатая накидка сыщика
        {
            using (GraphicsPath p = body(-4.1f, -12.9f, 8.2f, 8.4f)) { using (SolidBrush b = new SolidBrush(c1)) g.FillPath(b, p); g.DrawPath(op, p); }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { P(-4.4f, -13.4f), P(4.6f, -13.4f), P(5.6f, -8.4f), P(-5.4f, -8.4f) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-5.5f, -13.5f, 11.2f, 5.2f), Light(c1, 0.1f), Dark(c1, 0.82f), 90f)) g.FillPath(b, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (Pen cp = new Pen(Color.FromArgb(90, 80, 50, 30), 0.25f))
                {
                    for (int i = 0; i < 9; i++) g.DrawLine(cp, -5.4f + i * 1.3f, -13.4f, -5.4f + i * 1.3f, -8.4f);
                    for (int i = 0; i < 5; i++) g.DrawLine(cp, -5.6f, -13f + i * 1.2f, 5.8f, -13f + i * 1.2f);
                }
                g.Restore(cs);
                g.DrawPath(op, p);
            }
        }
        else if (o == 15) // куртка сталкера
        {
            using (GraphicsPath p = body(-4.2f, -13.0f, 8.4f, 8.8f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.3f, -13.1f, 8.6f, 9f), Dark(c1, 0.8f), Light(c1, 0.1f), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
            }
            using (SolidBrush b = new SolidBrush(Dark(c1, 0.8f))) { g.FillRectangle(b, -3.2f, -9.6f, 2.4f, 2.0f); g.FillRectangle(b, 1.0f, -9.6f, 2.4f, 2.0f); }
            using (Pen zp = new Pen(Color.FromArgb(170, 170, 160), 0.22f)) g.DrawLine(zp, 0.2f, -12.8f, 0.2f, -4.6f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 60, 50))) g.FillEllipse(b, 2.2f, -11.8f, 0.9f, 0.9f);
        }
        else if (o == 16) // тельняшка пирата
        {
            using (GraphicsPath p = body(-4.1f, -12.9f, 8.2f, 8.3f))
            {
                using (SolidBrush b = new SolidBrush(c1)) g.FillPath(b, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 60, 140))) for (int i = 0; i < 6; i++) g.FillRectangle(b, -4.2f, -12.4f + i * 1.45f, 8.4f, 0.65f);
                g.Restore(cs);
                g.DrawPath(op, p);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 40, 40))) g.FillRectangle(b, -4.0f, -6.0f, 8.0f, 1.0f);
        }
        else if (o == 17) // жилетка ковбоя
        {
            using (GraphicsPath p = body(-4.1f, -12.9f, 8.2f, 8.3f)) { using (SolidBrush b = new SolidBrush(c1)) g.FillPath(b, p); g.DrawPath(op, p); }
            Color vest = Color.FromArgb(130, 82, 48);
            using (SolidBrush b = new SolidBrush(vest))
            {
                g.FillPolygon(b, new PointF[] { P(-4.1f, -12.4f), P(0.6f, -12.8f), P(0.6f, -5.0f), P(-4.0f, -5.0f) });
                g.FillPolygon(b, new PointF[] { P(2.6f, -12.8f), P(4.1f, -12.4f), P(4.0f, -5.0f), P(2.0f, -5.0f) });
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 200, 50)))
            {
                PointF ctr = P(-1.6f, -10.0f);
                PointF[] star = new PointF[10];
                for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; float r = i % 2 == 0 ? 0.9f : 0.4f; star[i] = P(ctr.X + (float)Math.Cos(a) * r, ctr.Y + (float)Math.Sin(a) * r); }
                g.FillPolygon(b, star);
            }
        }
        else if (o == 18) // халат врача
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { P(-4.0f, -13.0f), P(4.0f, -13.0f), P(4.6f, -8f), P(5.0f, -2.0f), P(-4.6f, -2.0f), P(-4.4f, -8f) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.7f, -13.1f, 9.8f, 11.2f), Color.FromArgb(220, 224, 232), Color.White, 0f)) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(150, 155, 170), 0.3f)) g.DrawPath(pp, p);
            }
            using (SolidBrush b = new SolidBrush(c2)) g.FillPolygon(b, new PointF[] { P(0.8f, -13f), P(3.6f, -13f), P(2.2f, -9.6f) });
            using (Pen sp = new Pen(Color.FromArgb(60, 64, 72), 0.3f)) { g.DrawArc(sp, -0.6f, -13.8f, 5.0f, 5.4f, 20, 150); g.DrawLine(sp, 3.8f, -10.0f, 3.6f, -8.2f); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 175, 185))) g.FillEllipse(b, 3.0f, -8.4f, 1.3f, 1.3f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 90, 200))) g.FillRectangle(b, -2.8f, -10.6f, 0.4f, 1.8f);
        }
        else if (o == 19) // скафандр
        {
            using (GraphicsPath p = body(-4.6f, -13.4f, 9.2f, 9.6f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.7f, -13.5f, 9.4f, 9.8f), Color.FromArgb(205, 208, 216), Color.White, 0f)) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(140, 145, 160), 0.3f)) g.DrawPath(pp, p);
            }
            using (GraphicsPath p = RoundRect(-0.6f, -11.4f, 3.8f, 2.8f, 0.4f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 86, 100))) g.FillPath(b, p);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 60, 60))) g.FillEllipse(b, -0.2f, -11.0f, 0.8f, 0.8f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 200, 90))) g.FillEllipse(b, 0.9f, -11.0f, 0.8f, 0.8f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 140, 240))) g.FillEllipse(b, 2.0f, -11.0f, 0.8f, 0.8f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 50, 50))) g.FillRectangle(b, -4.4f, -7.2f, 8.8f, 0.8f);
        }
        else if (o == 20) // смокинг
        {
            using (GraphicsPath p = body(-4.1f, -12.9f, 8.2f, 8.4f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.2f, -13f, 8.4f, 8.6f), Color.FromArgb(10, 10, 12), Color.FromArgb(50, 50, 58), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
            }
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new PointF[] { P(0.4f, -13f), P(4.0f, -13f), P(2.4f, -8.2f) });
            using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 14, 16)))
            {
                g.FillPolygon(b, new PointF[] { P(1.0f, -12.9f), P(2.2f, -12.4f), P(1.0f, -11.9f) });
                g.FillPolygon(b, new PointF[] { P(3.4f, -12.9f), P(2.2f, -12.4f), P(3.4f, -11.9f) });
                g.FillEllipse(b, 1.9f, -12.7f, 0.6f, 0.6f);
            }
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new PointF[] { P(-1.6f, -11.2f), P(-0.8f, -11.2f), P(-1.2f, -10.4f) });
        }
        else if (o == 21) // кимоно
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { P(-4.0f, -13.0f), P(4.0f, -13.0f), P(4.6f, -8f), P(4.8f, -3.0f), P(-4.6f, -3.0f), P(-4.4f, -8f) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.7f, -13.1f, 9.6f, 10.2f), Dark(c1, 0.8f), Light(c1, 0.15f), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
            }
            using (Pen wp = new Pen(Color.FromArgb(240, 240, 235), 0.6f)) { g.DrawLine(wp, -0.6f, -13.0f, 2.6f, -8.8f); g.DrawLine(wp, 3.8f, -13.0f, 2.0f, -10.4f); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 40, 50))) g.FillRectangle(b, -4.4f, -8.4f, 9.0f, 1.6f);
        }
        else if (o == 22) // трико супергероя
        {
            using (GraphicsPath p = body(-4.0f, -12.9f, 8.0f, 8.4f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.1f, -13f, 8.2f, 8.6f), Dark(c1, 0.75f), Light(c1, 0.15f), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 210, 50))) { g.FillEllipse(b, -0.4f, -11.8f, 3.4f, 3.0f); g.FillRectangle(b, -4.0f, -5.8f, 8.0f, 0.9f); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 40, 50)))
            {
                PointF ctr = P(1.3f, -10.3f);
                PointF[] star = new PointF[10];
                for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; float r = i % 2 == 0 ? 1.2f : 0.5f; star[i] = P(ctr.X + (float)Math.Cos(a) * r, ctr.Y + (float)Math.Sin(a) * r); }
                g.FillPolygon(b, star);
            }
        }
        else if (o == 23) // мантия волшебника
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { P(-3.8f, -13.0f), P(3.9f, -13.0f), P(4.6f, -8.0f), P(5.6f, -0.8f), P(-5.2f, -0.8f), P(-4.5f, -8.0f) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-5.3f, -13.1f, 11f, 12.5f), Dark(c1, 0.75f), Light(c1, 0.15f), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 220, 90)))
                    for (int i = 0; i < 8; i++) { float x = -4.2f + (i * 37 % 90) / 10f, y = -12.2f + (i * 53 % 105) / 10f; g.FillEllipse(b, x, y, 0.6f, 0.6f); }
                g.Restore(cs);
            }
        }
    }

    void DrawCostumeHat(Graphics g, Cat c)
    {
        Color hc = c.HatC;
        switch (c.Hat)
        {
            case 7: // кепка с двумя козырьками
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-4.0f, -24.6f, 10.0f, 7.0f, 180, 180); p.CloseFigure();
                    using (SolidBrush b = new SolidBrush(hc)) g.FillPath(b, p);
                    GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                    using (Pen cp = new Pen(Color.FromArgb(90, 70, 50, 30), 0.25f)) { for (int i = 0; i < 8; i++) g.DrawLine(cp, -4 + i * 1.4f, -25, -4 + i * 1.4f, -21); for (int i = 0; i < 4; i++) g.DrawLine(cp, -4, -24.4f + i * 1.0f, 6, -24.4f + i * 1.0f); }
                    g.Restore(cs);
                    using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Dark(hc, 0.85f))) { g.FillPolygon(b, new PointF[] { P(5.0f, -21.4f), P(7.6f, -21.0f), P(5.2f, -20.6f) }); g.FillPolygon(b, new PointF[] { P(-3.2f, -21.4f), P(-5.8f, -21.0f), P(-3.4f, -20.6f) }); }
                using (SolidBrush b = new SolidBrush(Dark(hc, 0.6f))) g.FillEllipse(b, 0.5f, -25.0f, 0.8f, 0.6f);
                break;
            case 8: // противогаз + капюшон
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-5.6f, -24.2f, 13.0f, 10.0f, 180, 180); p.AddLine(7.4f, -19.2f, 7.0f, -15.0f); p.AddLine(-5.2f, -15.0f, -5.6f, -19.2f); p.CloseFigure();
                    using (SolidBrush b = new SolidBrush(hc)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath p = RoundRect(-1.4f, -21.2f, 8.2f, 7.6f, 3.0f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.5f, -21.3f, 8.4f, 7.8f), Color.FromArgb(90, 94, 100), Color.FromArgb(40, 42, 48), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(20, 20, 24), 0.3f)) g.DrawPath(pp, p);
                }
                foreach (float ex in new float[] { 0.2f, 3.6f })
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 30, 34))) g.FillEllipse(b, ex - 0.2f, -20.4f, 2.8f, 2.8f);
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(ex, -20.2f, 2.4f, 2.4f), Color.FromArgb(160, 200, 220), Color.FromArgb(60, 80, 100), 45f)) g.FillEllipse(b, ex, -20.2f, 2.4f, 2.4f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, ex + 0.4f, -19.9f, 0.7f, 0.5f);
                }
                using (GraphicsPath p = RoundRect(3.0f, -16.2f, 3.2f, 3.0f, 0.8f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 74, 64))) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(40, 42, 36), 0.25f)) for (int i = 0; i < 3; i++) g.DrawLine(pp, 3.4f + i * 0.9f, -15.8f, 3.4f + i * 0.9f, -13.6f);
                break;
            case 9: // капюшон монаха
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-5.8f, -24.6f, 13.4f, 11.0f, 160, 220); p.AddLine(7.2f, -16.0f, 6.0f, -13.0f); p.AddLine(-4.6f, -13.0f, -5.6f, -17.0f); p.CloseFigure();
                    p.AddEllipse(-2.8f, -21.4f, 9.6f, 8.4f);
                    p.FillMode = FillMode.Alternate;
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-6f, -24.7f, 14f, 12f), Light(hc, 0.1f), Dark(hc, 0.75f), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(hc, 0.45f), 0.3f)) g.DrawPath(pp, p);
                }
                using (Pen sp = new Pen(Color.FromArgb(80, 0, 0, 0), 0.5f)) g.DrawArc(sp, -2.8f, -21.4f, 9.6f, 8.4f, 190, 160);
                break;
            case 10: // треуголка + повязка
                using (Pen pp = new Pen(Color.FromArgb(20, 18, 22), 0.3f)) g.DrawLine(pp, -4.6f, -20.8f, 6.6f, -18.6f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(20, 18, 22))) g.FillEllipse(b, 3.6f, -20.4f, 2.6f, 2.4f);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-6.0f, -21.0f), P(-2.0f, -26.6f), P(1.2f, -24.6f), P(4.4f, -26.6f), P(8.2f, -21.0f), P(1.2f, -22.4f) });
                    using (SolidBrush b = new SolidBrush(hc)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(230, 190, 70), 0.35f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, 0.6f, -24.8f, 1.2f, 1.0f);
                break;
            case 11: // ковбойская шляпа
                using (SolidBrush b = new SolidBrush(Dark(hc, 0.85f))) g.FillEllipse(b, -7.0f, -22.6f, 16.0f, 2.8f);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddBezier(-3.2f, -21.6f, -3.4f, -26.6f, 5.6f, -26.6f, 5.4f, -21.6f);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.5f, -26.7f, 9f, 5.2f), Light(hc, 0.15f), Dark(hc, 0.8f), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 40, 30))) g.FillRectangle(b, -3.2f, -23.0f, 8.6f, 0.9f);
                using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawEllipse(pp, -7.0f, -22.6f, 16.0f, 2.8f);
                break;
            case 12: // шлем-пузырь
                using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 180, 220, 255))) g.FillEllipse(b, -6.6f, -26.4f, 15.2f, 14.6f);
                using (Pen pp = new Pen(Color.FromArgb(200, 210, 225, 240), 0.4f)) g.DrawEllipse(pp, -6.6f, -26.4f, 15.2f, 14.6f);
                using (Pen hp = new Pen(Color.FromArgb(200, 255, 255, 255), 0.6f)) { hp.StartCap = LineCap.Round; hp.EndCap = LineCap.Round; g.DrawArc(hp, -5.4f, -25.2f, 12.8f, 12.2f, 200, 50); }
                using (GraphicsPath p = RoundRect(-5.0f, -13.6f, 12.0f, 2.0f, 0.8f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 156, 170))) g.FillPath(b, p);
                break;
            case 13: // пучок + повязка
                using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 24, 30))) { g.FillEllipse(b, -0.4f, -24.6f, 3.2f, 2.6f); g.FillRectangle(b, 0.6f, -22.6f, 1.2f, 0.9f); }
                using (GraphicsPath p = RoundRect(-4.6f, -21.4f, 11.6f, 1.3f, 0.6f)) using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(hc)) g.FillEllipse(b, 1.6f, -21.4f, 1.3f, 1.3f);
                using (Pen pp = new Pen(Color.White, 0.5f)) { g.DrawLine(pp, -4.4f, -20.8f, -6.0f, -19.2f); g.DrawLine(pp, -4.4f, -20.8f, -6.4f, -20.4f); }
                break;
            case 14: // маска супергероя
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.FillMode = FillMode.Alternate;
                    p.AddClosedCurve(new PointF[] { P(-1.2f, -20.4f), P(3.0f, -20.8f), P(7.0f, -20.4f), P(6.8f, -17.6f), P(3.0f, -18.0f), P(-1.0f, -17.6f) }, 0.4f);
                    p.AddEllipse(0.2f, -20.0f, 2.0f, 2.0f);
                    p.AddEllipse(3.8f, -20.0f, 2.0f, 2.0f);
                    using (SolidBrush b = new SolidBrush(hc)) g.FillPath(b, p);
                }
                using (Pen pp = new Pen(hc, 0.4f)) { g.DrawLine(pp, -1.2f, -19.4f, -3.4f, -18.4f); g.DrawLine(pp, -1.2f, -19.0f, -3.0f, -17.4f); }
                break;
            case 15: // шляпа волшебника
                using (SolidBrush b = new SolidBrush(Dark(hc, 0.8f))) g.FillEllipse(b, -6.0f, -22.6f, 14.0f, 2.6f);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-3.4f, -21.8f), P(5.6f, -21.8f), P(2.4f, -29.0f), P(-0.6f, -31.4f), P(0.8f, -28.6f) });
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.5f, -31.5f, 9.2f, 9.8f), Light(hc, 0.15f), Dark(hc, 0.8f), 0f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 220, 90))) { g.FillEllipse(b, 0.4f, -26.2f, 0.8f, 0.8f); g.FillEllipse(b, 2.6f, -24.0f, 0.6f, 0.6f); g.FillEllipse(b, -1.2f, -23.6f, 0.6f, 0.6f); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 220, 90))) g.FillRectangle(b, -3.2f, -22.8f, 8.6f, 0.7f);
                break;
            case 16: // зеркало врача
                using (Pen pp = new Pen(Color.FromArgb(70, 74, 84), 0.5f)) g.DrawArc(pp, -4.6f, -24.6f, 11.6f, 7.0f, 200, 140);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(1.2f, -24.0f, 3.2f, 3.2f), Color.White, Color.FromArgb(160, 170, 190), 45f)) g.FillEllipse(b, 1.2f, -24.0f, 3.2f, 3.2f);
                using (Pen pp = new Pen(Color.FromArgb(120, 125, 140), 0.25f)) g.DrawEllipse(pp, 1.2f, -24.0f, 3.2f, 3.2f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 46))) g.FillEllipse(b, 2.5f, -22.7f, 0.6f, 0.6f);
                break;
        }
    }

    // ================= живая очередь =================
    float counterHP = 100; bool registerDown, registerFlying; float registerX; int sellerReactT, rampageCD = 900;
    static readonly string[] IdleLines = { "♪ ля-ля-ля", "*зевает*", "Хм...", "Интересно, что новенького", "Эх...", "Сейчас бы на море" };
    static readonly string[] AnnoyLines = { "Ну давай уже...", "Касса, работаем!", "Я тут с утра стою!", "Можно побыстрее?", "Сколько можно?!", "Тут вообще очередь движется?" };
    static readonly string[] RageLines = { "ДА СКОЛЬКО МОЖНО?!", "Я СЕЙЧАС ВСЁ РАЗНЕСУ!", "ГДЕ ЖАЛОБНАЯ КНИГА?!", "ЭТО НЕ СЕРВИС, ЭТО ИЗДЕВАТЕЛЬСТВО!", "А-А-А-А!" };
    static readonly string[] SellerCalm = { "Спокойно, всех обслужу!", "Не толкаемся, пожалуйста!", "Тише, тише!", "Сейчас-сейчас!" };

    bool AnyRampage() { foreach (Cat g in guests) if (g.Phase == 4) return true; return false; }

    void SellerSay(string t)
    {
        if (sellerReactT > 0) return;
        sellerReactT = 90;
        me.SweatT = 70;
        Say(t, me.X, CatTop(me) - U(1), 'U');
    }

    void UpdateQueueMood()
    {
        if (rampageCD > 0) rampageCD--;
        List<Cat> queue = new List<Cat>();
        foreach (Cat g in guests) if (g.Phase <= 1) queue.Add(g);
        for (int qi = 0; qi < queue.Count; qi++)
        {
            Cat g = queue[qi];
            if (g.Walking && g.TX > Width) continue;
            g.Patience -= (0.03f + 0.024f * qi) * g.Temper;
            if (g.Patience < 0) g.Patience = 0;
            if (g.Patience < 40 && tick % 16 == g.Id % 16)
            {
                PointF h = Local(g, -2, -24);
                Particle p = new Particle(); p.Kind = 0; p.X = h.X; p.Y = h.Y; p.VX = -0.1f * u; p.VY = -0.25f * u; p.R = U(0.8f); p.Max = 25;
                parts.Add(p);
            }
            if (g.LookT > 0) { g.LookT--; g.Dir = 1; if (g.LookT == 0) g.Dir = -1; }
            if (--g.ActT > 0 || g.Walking) continue;
            float pt = g.Patience;
            if (pt > 65)
            {
                g.ActT = 110 + rnd.Next(150);
                int r = rnd.Next(5);
                if (r == 0) g.LookT = 30;
                else if (r == 1 && g.Hold == ' ') { g.Hold = 'F'; g.HoldCol = 'U'; g.ActT = 80; }
                else if (r == 2 && rnd.Next(2) == 0) Say(IdleLines[rnd.Next(IdleLines.Length)], g.X, CatTop(g) - U(1), 'K');
                else g.BlinkT = 8;
                if (r != 1 && g.Hold == 'F') g.Hold = ' ';
            }
            else if (pt > 40)
            {
                if (g.Hold == 'F') g.Hold = ' ';
                g.ActT = 90 + rnd.Next(110);
                if (rnd.Next(2) == 0) { g.StompT = 22; g.AngryT = 30; }
                else g.SweatT = 40;
                if (rnd.Next(100) < 35) Say(AnnoyLines[rnd.Next(AnnoyLines.Length)], g.X, CatTop(g) - U(1), 'K');
            }
            else if (pt > 15)
            {
                if (g.Hold == 'F') g.Hold = ' ';
                g.ActT = 70 + rnd.Next(80);
                g.AngryT = 70; g.StompT = 30;
                int r = rnd.Next(3);
                if (r == 0 && qi > 0 && queue[qi - 1].Phase == 0) Shove(g, queue[qi - 1]);
                else if (r == 1) ThrowAtRegister(g);
                else Say(AnnoyLines[rnd.Next(AnnoyLines.Length)].ToUpper(), g.X, CatTop(g) - U(1), 'R');
                SellerSay(SellerCalm[rnd.Next(SellerCalm.Length)]);
            }
            else if (!AnyRampage() && rampageCD <= 0)
            {
                rampageCD = 1800;
                g.Phase = 4; g.RageHits = 0; g.ActT = 10; g.Speed = 2.6f;
                Go(g, CounterX + U(30));
                Say(RageLines[rnd.Next(RageLines.Length)], g.X, CatTop(g) - U(1), 'R');
                g.AngryT = 200;
            }
        }
        foreach (Cat g in guests.ToArray()) if (g.Phase == 4) UpdateRampage(g);
    }

    void Shove(Cat pusher, Cat victim)
    {
        pusher.Shake = 10;
        Jump(victim, victim.X - U(4), 0, U(2.5f), 10, null);
        victim.Shake = 25; victim.AngryT = 80; victim.Patience = Math.Max(0, victim.Patience - 18);
        Say("Подвинься!", pusher.X, CatTop(pusher) - U(1), 'R');
        Say("Э! Не толкайся!", victim.X, CatTop(victim) - U(4), 'R');
        if (rnd.Next(2) == 0)
        {
            int a = guests.IndexOf(pusher), b = guests.IndexOf(victim);
            if (a >= 0 && b >= 0) { guests[a] = victim; guests[b] = pusher; pusher.Phase = 0; victim.Phase = 0; }
        }
    }

    void ThrowAtRegister(Cat g)
    {
        PointF pw = Local(g, 4.8f, -14f);
        char kind = rnd.Next(2) == 0 ? 'X' : 'B';
        char col = ItemColors[rnd.Next(ItemColors.Length)];
        g.HappyT = 0;
        Say(rnd.Next(2) == 0 ? "НА, ДЕРЖИ!" : "ЛОВИ!", g.X, CatTop(g) - U(1), 'R');
        if (registerDown) return;
        Fly(kind, col, pw.X, pw.Y, RegPt.X, RegPt.Y + U(2), 14, delegate
        {
            Stars(new PointF(RegPt.X, RegPt.Y + U(2)));
            regFlash = 14; counterHP = Math.Max(0, counterHP - 4); shakeT = 4;
            SellerSay(rnd.Next(2) == 0 ? "Эй! Не кидайтесь!" : "Касса-то тут при чём?!");
        });
    }

    void Chips(float x, float y)
    {
        for (int i = 0; i < 6; i++)
        {
            Particle p = new Particle();
            p.Kind = 1; p.X = x; p.Y = y;
            p.VX = (float)(rnd.NextDouble() - 0.5) * u; p.VY = -(0.4f + (float)rnd.NextDouble() * 0.6f) * u;
            p.R = U(0.4f + (float)rnd.NextDouble() * 0.4f); p.Max = 26; p.Col = rnd.Next(2) == 0 ? 'B' : 'b';
            parts.Add(p);
        }
    }

    void UpdateRampage(Cat g)
    {
        if (g.Walking || g.Jumping) return;
        g.Dir = -1;
        if (--g.ActT > 0) return;
        if (g.RageHits < 5)
        {
            g.ActT = 16;
            g.RageHits++;
            g.StompT = 12; g.Shake = 12; g.AngryT = 120;
            counterHP = Math.Max(0, counterHP - 9);
            shakeT = 5;
            Chips(CounterX + U(22), Ground - U(8 + (float)rnd.NextDouble() * 4));
            if (g.RageHits == 2) SellerSay("Не ломайте прилавок!!!");
            if (g.RageHits % 2 == 1) Say(new string[] { "БАХ!", "ХРЯСЬ!", "ТРЕСЬ!" }[rnd.Next(3)], CounterX + U(18), Ground - U(20), 'R');
            return;
        }
        if (g.RageHits == 5)
        {
            g.RageHits++;
            g.ActT = 40;
            if (!registerDown && !registerFlying && rnd.Next(100) < 40)
            {
                registerFlying = true;
                registerX = Math.Max(U(6), CounterX - U(6 + (float)rnd.NextDouble() * 10));
                Say("А КАССУ Я ЗАБЕРУ... НЕТ, ВЫБРОШУ!", g.X, CatTop(g) - U(1), 'R');
                Fly('G', 'G', RegPt.X, RegPt.Y + U(2), registerX, Ground - U(2.5f), 22, delegate
                {
                    registerFlying = false; registerDown = true; shakeT = 10; Poof(registerX, Ground - U(2));
                    me.SweatT = 120; me.Shake = 25;
                    sellerReactT = 0; SellerSay("Касса! Моя касса!");
                });
            }
            return;
        }
        // развязка
        if (rnd.Next(2) == 0)
        {
            Say("Ноги моей тут не будет!", g.X, CatTop(g) - U(1), 'R');
            g.Phase = 3; g.StompT = 30; g.Speed = 2.4f; Go(g, Width + U(16));
        }
        else
        {
            Say("...Ладно. Извините. Нервы.", g.X, CatTop(g) - U(1), 'K');
            g.AngryT = 0; g.SweatT = 60; g.Patience = 70; g.Temper *= 0.5f; g.Phase = 0; g.Speed = 1.6f;
            guests.Remove(g); guests.Insert(0, g);
        }
    }

    List<Func<bool>> FixRegister()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        float rx = registerX;
        s.Add(GoStep(me, delegate { return Math.Max(U(10), rx + U(6)); }));
        s.Add(Do(delegate { me.Dir = -1; registerDown = false; me.Hold = 'G'; me.HoldCol = 'G'; }));
        s.Add(Line(me, "Ох... ты как, касса?", 25, 'w'));
        s.Add(GoStep(me, delegate { return CashX; }));
        s.Add(Do(delegate { me.Dir = 1; me.Hold = ' '; regFlash = 30; }));
        s.Add(Line(me, "Работает! Вроде...", 25, 'h'));
        return s;
    }

    List<Func<bool>> FixCounter()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        s.Add(GoStep(me, delegate { return CashX; }));
        s.Add(Do(delegate { me.Dir = 1; me.Hold = 'A'; me.HoldCol = 'g'; }));
        for (int i = 0; i < 4; i++)
        {
            s.Add(Do(delegate { me.ItemRot = -40; }));
            s.Add(Wait(5));
            s.Add(Do(delegate { me.ItemRot = 10; counterHP = Math.Min(100, counterHP + 15); Stars(new PointF(CounterX + U(2), Ground - U(10))); Say("Тук!", CounterX + U(4), Ground - U(18), 'K'); }));
            s.Add(Wait(8));
        }
        s.Add(Do(delegate { counterHP = 100; me.Hold = ' '; me.ItemRot = 0; me.HappyT = 40; }));
        s.Add(Line(me, "Как новенький!", 30, 'h'));
        return s;
    }
    // ================= ИИ-ассистент =================
    const string GroqUrl = "https://api.groq.com/openai/v1/chat/completions";
    const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
    string aiFormat = "openai", aiUrl = GeminiUrl, aiModel = "gemini-3.8-flash", aiKey = "";
    volatile bool aiBusy;
    List<string[]> aiHistory = new List<string[]>();
    Form chatForm; RichTextBox chatLog; TextBox chatInput; Button chatSend; ToolStripTextBox askBox;
    const string AiSystem = "Ты — котик-ассистент, который живёт на рабочем столе Windows у владельца вейп-шопа. " +
        "Ты рыжий полосатый кот на двух лапах, дружелюбный, с лёгким кошачьим юмором (иногда «мяу», «мур»). " +
        "Отвечай по-русски, коротко и по делу: обычно 1–4 предложения, списки — только когда правда нужны. " +
        "Помогаешь с работой: тексты и сообщения, расчёты, наценка и закупки, учёт товара и МойСклад, Excel/Google Таблицы, идеи для магазина. " +
        "Если не знаешь — честно скажи. Не выдумывай цифры и факты. " +
        "Не используй эмодзи и markdown (звёздочки, решётки): окно показывает только обычный текст. Вместо смайликов можно =^.^= или ♥.";

    // эмодзи в окне Windows рисуются квадратиками — заменяем их на текстовые смайлики
    static string CleanText(string t)
    {
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (char.IsHighSurrogate(c) && i + 1 < t.Length)
            {
                int cp = char.ConvertToUtf32(c, t[i + 1]); i++;
                string rep = null;
                if ((cp >= 0x1F431 && cp <= 0x1F431) || (cp >= 0x1F638 && cp <= 0x1F640) || cp == 0x1F408 || cp == 0x1F43E) rep = "=^.^=";
                else if (cp >= 0x1F600 && cp <= 0x1F64F) rep = ":)";
                else if (cp >= 0x1F493 && cp <= 0x1F49F || cp == 0x1F9E1 || cp == 0x1F90D || cp == 0x1F90E) rep = "♥";
                else if (cp == 0x1F44D) rep = "(+)";
                if (rep != null) { if (b.Length > 0 && b[b.Length - 1] != ' ') b.Append(' '); b.Append(rep); }
                continue;
            }
            if (c == '\uFE0F' || c == '\u200D' || c == '\uFE0E') continue;
            if (c >= '\u2600' && c <= '\u27BF' && c != '\u2665' && c != '\u2605' && c != '\u2713' && c != '\u2714') { if (c == '\u2764') b.Append('♥'); continue; }
            if (c == '\u00A0' || c == '\u202F' || c == '\u2009' || c == '\u2007') c = ' ';
            if (c == '*' && !(i + 1 < t.Length && char.IsDigit(t[i + 1]) && b.Length > 0 && (char.IsDigit(b[b.Length - 1]) || b[b.Length - 1] == ' '))) continue;
            if (c == ' ' && b.Length > 0 && b[b.Length - 1] == ' ') continue;
            b.Append(c);
        }
        string r = b.ToString();
        r = Regex.Replace(r, @" +\n", "\n");
        r = Regex.Replace(r, @"(?m)^#+ *", "");
        return r.Trim();
    }

    static string JEsc(string s)
    {
        StringBuilder b = new StringBuilder();
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\\': b.Append("\\\\"); break;
                case '"': b.Append("\\\""); break;
                case '\n': b.Append("\\n"); break;
                case '\r': break;
                case '\t': b.Append("\\t"); break;
                default: if (ch < 32) b.Append("\\u" + ((int)ch).ToString("x4")); else b.Append(ch); break;
            }
        }
        return b.ToString();
    }

    static string JReadString(string s, int quote)
    {
        StringBuilder b = new StringBuilder();
        for (int i = quote + 1; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '"') break;
            if (ch == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                switch (n)
                {
                    case 'n': b.Append('\n'); break;
                    case 't': b.Append('\t'); break;
                    case 'r': break;
                    case 'u': if (i + 4 < s.Length) { b.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; } break;
                    default: b.Append(n); break;
                }
            }
            else b.Append(ch);
        }
        return b.ToString();
    }

    static string JValueAfter(string json, string anchor, string key)
    {
        int a = anchor == null ? 0 : json.IndexOf(anchor);
        if (a < 0) return null;
        Match m = new Regex("\"" + key + "\"\\s*:\\s*\"").Match(json, a);
        if (!m.Success) return null;
        return JReadString(json, m.Index + m.Length - 1);
    }

    string CallAI(List<string[]> hist, string format, string url, string model, string key)
    {
        StringBuilder body = new StringBuilder();
        if (format == "anthropic")
        {
            body.Append("{\"model\":\"").Append(JEsc(model)).Append("\",\"max_tokens\":800,\"system\":\"").Append(JEsc(AiSystem)).Append("\",\"messages\":[");
            for (int i = 0; i < hist.Count; i++)
            {
                if (i > 0) body.Append(",");
                body.Append("{\"role\":\"").Append(hist[i][0]).Append("\",\"content\":\"").Append(JEsc(hist[i][1])).Append("\"}");
            }
            body.Append("]}");
        }
        else
        {
            bool gem = url.Contains("generativelanguage.googleapis.com");
            body.Append("{\"model\":\"").Append(JEsc(model)).Append(gem || url.Contains("api.groq.com") ? "\",\"max_tokens\":2000," : "\",\"max_tokens\":800,");
            if (gem && model.StartsWith("gemini-2.5")) body.Append("\"reasoning_effort\":\"none\",");
            if (url.Contains("api.groq.com") && model.Contains("gpt-oss")) body.Append("\"reasoning_effort\":\"low\",");
            body.Append("\"messages\":[{\"role\":\"system\",\"content\":\"").Append(JEsc(AiSystem)).Append("\"}");
            foreach (string[] h in hist) body.Append(",{\"role\":\"").Append(h[0]).Append("\",\"content\":\"").Append(JEsc(h[1])).Append("\"}");
            body.Append("]}");
        }
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        ServicePointManager.Expect100Continue = false;
        using (WebClient wc = new WebClient())
        {
            wc.Encoding = Encoding.UTF8;
            wc.Headers[HttpRequestHeader.ContentType] = "application/json";
            if (format == "anthropic") { wc.Headers["x-api-key"] = key; wc.Headers["anthropic-version"] = "2023-06-01"; }
            else wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + key;
            string resp = null;
            for (int attempt = 1; ; attempt++)
            {
            try { resp = wc.UploadString(url, "POST", body.ToString()); break; }
            catch (WebException we)
            {
                if (we.Response == null && attempt < 4) { Thread.Sleep(1500 * attempt); continue; }
                string msg = we.Message;
                if (we.Response != null)
                    using (StreamReader sr = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8))
                    {
                        string err = sr.ReadToEnd();
                        string m2 = JValueAfter(err, "\"error\"", "message");
                        if (m2 != null) msg = m2;
                    }
                if (we.Response == null) msg = "нет связи с сервером ИИ (проверь VPN): " + msg;
                throw new Exception(msg);
            }
            }
            string text = format == "anthropic" ? JValueAfter(resp, "\"content\"", "text") : JValueAfter(resp, "\"message\"", "content");
            if (text == null) throw new Exception("непонятный ответ сервера");
            return CleanText(text);
        }
    }

    void ChatAppend(string who, string text, Color col)
    {
        if (chatLog == null) return;
        chatLog.SelectionStart = chatLog.TextLength;
        chatLog.SelectionColor = col;
        chatLog.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        chatLog.AppendText(who + "\n");
        chatLog.SelectionColor = Color.FromArgb(235, 238, 245);
        chatLog.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        chatLog.AppendText(text + "\n\n");
        chatLog.ScrollToCaret();
    }

    void OpenChat()
    {
        if (chatForm == null || chatForm.IsDisposed)
        {
            chatForm = new Form();
            chatForm.Text = "Котик-ассистент";
            chatForm.Size = new Size(440, 520);
            chatForm.TopMost = true; chatForm.ShowInTaskbar = true;
            chatForm.BackColor = Color.FromArgb(30, 32, 42);
            chatForm.StartPosition = FormStartPosition.Manual;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            chatForm.Location = new Point(wa.Right - 460, Math.Max(wa.Top, wa.Bottom - HH - 540));
            chatLog = new RichTextBox();
            chatLog.ReadOnly = true; chatLog.BorderStyle = BorderStyle.None;
            chatLog.BackColor = Color.FromArgb(30, 32, 42); chatLog.ForeColor = Color.White;
            chatLog.Font = new Font("Segoe UI", 10f);
            chatLog.Dock = DockStyle.Fill; chatLog.DetectUrls = true;
            Panel bottom = new Panel(); bottom.Dock = DockStyle.Bottom; bottom.Height = 44; bottom.BackColor = Color.FromArgb(42, 45, 58); bottom.Padding = new Padding(8);
            chatInput = new TextBox(); chatInput.Font = new Font("Segoe UI", 11f); chatInput.Dock = DockStyle.Fill;
            chatInput.BackColor = Color.FromArgb(56, 60, 76); chatInput.ForeColor = Color.White; chatInput.BorderStyle = BorderStyle.FixedSingle;
            chatSend = new Button(); chatSend.Text = "Отправить"; chatSend.Dock = DockStyle.Right; chatSend.Width = 100;
            chatSend.FlatStyle = FlatStyle.Flat; chatSend.BackColor = Color.FromArgb(255, 158, 64); chatSend.ForeColor = Color.FromArgb(30, 30, 36);
            chatSend.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            chatSend.Click += delegate { SendChat(); };
            chatInput.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendChat(); } };
            bottom.Controls.Add(chatInput); bottom.Controls.Add(chatSend);
            Panel pad = new Panel(); pad.Dock = DockStyle.Fill; pad.Padding = new Padding(12, 10, 8, 6); pad.Controls.Add(chatLog);
            chatForm.Controls.Add(pad); chatForm.Controls.Add(bottom);
            chatForm.FormClosing += delegate (object o, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; chatForm.Hide(); } };
            ChatAppend("Котик", aiKey.Length == 0
                ? "Мяу! Я котик-ассистент. Чтобы я начал отвечать, добавь ключ ИИ: меню котика → «ИИ-ассистент» → «Настройки ИИ»."
                : "Мур! Спрашивай что угодно: тексты, расчёты, наценка, МойСклад, идеи для магазина.", Color.FromArgb(255, 170, 80));
        }
        chatForm.Show(); chatForm.Activate(); chatInput.Focus();
        me.HappyT = 40;
    }

    void SendChat()
    {
        string q = chatInput.Text.Trim();
        if (q.Length == 0 || aiBusy) return;
        chatInput.Text = "";
        AskAI(q, false);
    }

    void ChatBusy(bool busy)
    {
        if (chatSend == null) return;
        chatSend.Enabled = !busy; chatSend.Text = busy ? "Думаю..." : "Отправить";
    }

    void AskAI(string q, bool card)
    {
        if (aiBusy) { if (card) ShowCard(q, "Погоди, я ещё прошлый вопрос обдумываю...", false); return; }
        ChatAppend("Вы", q, Color.FromArgb(120, 190, 255));
        if (aiKey.Length == 0)
        {
            string m = "Мяу... без ключа ИИ я только мурчать умею. Меню котика → «ИИ-ассистент» → «Настройки ИИ».";
            ChatAppend("Котик", m, Color.FromArgb(255, 170, 80));
            if (card) ShowCard(q, m, true);
            Say("Нужен ключ ИИ...", me.X, CatTop(me) - U(1), 'U'); me.SweatT = 60;
            return;
        }
        aiHistory.Add(new string[] { "user", q });
        while (aiHistory.Count > 14) aiHistory.RemoveAt(0);
        if (aiHistory[0][0] != "user") aiHistory.RemoveAt(0);
        aiBusy = true; ChatBusy(true);
        if (card) ShowCard(q, "Хм... думаю", false);
        Say("Хм... думаю", me.X, CatTop(me) - U(1), 'U');
        List<string[]> hist = new List<string[]>(aiHistory);
        string fmt = aiFormat, url = aiUrl, model = aiModel, key = aiKey;
        string prevQ = null; for (int i = aiHistory.Count - 3; i >= 0; i--) if (aiHistory[i][0] == "user") { prevQ = aiHistory[i][1]; break; }
        ThreadPool.QueueUserWorkItem(delegate
        {
            string ans = null, err = null;
            // нужен ли интернет? — ИИ сам решает и, если надо, котик ищет в DuckDuckGo
            try
            {
                string web = WebAugment(q, prevQ);
                if (web != null)
                {
                    hist[hist.Count - 1] = new string[] { "user", q + "\n\n[Котик нашёл в интернете, сегодня " + DateTime.Now.ToString("d MMMM yyyy", new CultureInfo("ru-RU")) + "]\n" + web +
                        "\n[Ответь по-русски, опираясь на найденное; в конце коротко укажи сайт-источник. Если в найденном нет ответа — честно скажи.]" };
                }
            }
            catch { }
            try { ans = CallAI(hist, fmt, url, model, key); }
            catch (Exception ex) { err = ex.Message; }
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    aiBusy = false; ChatBusy(false);
                    if (err != null)
                    {
                        if (aiHistory.Count > 0) aiHistory.RemoveAt(aiHistory.Count - 1);
                        ChatAppend("Котик", "Мяу, не получилось: " + err, Color.FromArgb(255, 110, 110));
                        if (card) ShowCard(q, "Мяу, не получилось: " + err, true);
                        Say("Ой, связь пропала...", me.X, CatTop(me) - U(1), 'R'); me.SweatT = 80;
                    }
                    else
                    {
                        aiHistory.Add(new string[] { "assistant", ans });
                        ChatAppend("Котик", ans, Color.FromArgb(255, 170, 80));
                        Speak(ans);
                        if (card) ShowCard(q, ans, true);
                        else
                        {
                            string shortAns = ans.Replace("\n", " ");
                            if (shortAns.Length > 70) { int cut = shortAns.LastIndexOf(' ', 66); shortAns = shortAns.Substring(0, cut > 30 ? cut : 66) + "…"; }
                            Say(shortAns, me.X, CatTop(me) - U(1), 'U');
                        }
                        me.HappyT = 50;
                    }
                });
            }
            catch { }
        });
    }

    string WebAugment(string q, string prevQ)
    {
        string sys = "Сегодня " + DateTime.Now.ToString("d MMMM yyyy", new CultureInfo("ru-RU")) + ". Ты решаешь, нужен ли поиск в интернете, чтобы точно ответить на вопрос. " +
            "Поиск нужен для фактов (фильмы, сериалы, люди, даты, цены, новости, расписания, характеристики, адреса, «сколько», «когда», «кто»). " +
            "Не нужен для болтовни, советов, шуток, переводов, математики и вопросов о самом котике. " +
            "Ответь строго одной строкой: НЕТ — или ИСКАТЬ: <короткий поисковый запрос на русском>.";
        string ask = (prevQ != null ? "Предыдущий вопрос: «" + prevQ + "»\n" : "") + "Вопрос: «" + q + "»";
        string r = CallAIRaw(sys, ask, null, 200);
        Match m = Regex.Match(r, @"ИСКАТЬ\s*:\s*(.+)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        string query = m.Groups[1].Value.Trim().Trim('"', '«', '»');
        if (query.Length < 2) return null;
        try { BeginInvoke((Action)delegate { Say("Ищу в интернете…", me.X, CatTop(me) - U(1), 'U'); if (card != null && card.Visible) ShowCard(q, "Ищу в интернете: " + query + "…", false); }); } catch { }
        return WebSearch(query);
    }

    static string WebSearch(string query)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        HttpWebRequest rq = (HttpWebRequest)WebRequest.Create("https://html.duckduckgo.com/html/?kl=ru-ru&q=" + Uri.EscapeDataString(query));
        rq.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130 Safari/537.36";
        rq.Timeout = 12000; rq.ReadWriteTimeout = 12000; rq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        string html;
        using (WebResponse rs = rq.GetResponse()) using (StreamReader sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8)) html = sr.ReadToEnd();
        StringBuilder b = new StringBuilder(); int n = 0;
        foreach (Match m in Regex.Matches(html, "<a[^>]*class=\"result__a\"[^>]*href=\"([^\"]*)\"[^>]*>(.*?)</a>.*?class=\"result__snippet\"[^>]*>(.*?)</a>", RegexOptions.Singleline))
        {
            string href = WebUtility.HtmlDecode(m.Groups[1].Value);
            Match ud = Regex.Match(href, "uddg=([^&]+)"); if (ud.Success) href = Uri.UnescapeDataString(ud.Groups[1].Value);
            if (href.Contains("duckduckgo.com/y.js")) continue;   // реклама
            string host = href; try { host = new Uri(href.StartsWith("//") ? "https:" + href : href).Host.Replace("www.", ""); } catch { }
            string title = WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<[^>]+>", "")).Trim();
            string snip = WebUtility.HtmlDecode(Regex.Replace(m.Groups[3].Value, "<[^>]+>", "")).Trim();
            b.Append("— ").Append(title).Append(" (").Append(host).Append("): ").Append(snip).Append("\n");
            if (++n >= 6) break;
        }
        return n > 0 ? b.ToString() : null;
    }

    // ---------- озвучка (встроенный голос Windows, через рефлексию — без лишних зависимостей) ----------
    bool aiVoice = true; ToolStripMenuItem voiceItem;
    object synth; Type synthType; bool synthFailed;

    void Speak(string text)
    {
        if (!aiVoice) return;
        StopSpeak();
        if (cloneVoice && File.Exists(Path.Combine(voiceDir, "server.py"))) { SpeakClone(text); return; }
        SpeakIrina(text);
    }

    // ---------- свой голос: локальный сервер XTTS (D:\VapePetVoice) ----------
    bool cloneVoice = true; string voiceDir = @"D:\VapePetVoice"; ToolStripMenuItem cloneItem;
    volatile int speakGen; System.Media.SoundPlayer clonePlayer; DateTime voiceStartAt = DateTime.MinValue;

    bool VoiceHealthy()
    {
        try
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:5055/health");
            r.Timeout = 1500; r.Proxy = null;
            using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse()) return resp.StatusCode == HttpStatusCode.OK;
        }
        catch { return false; }
    }

    void EnsureVoiceServer(bool async)
    {
        WaitCallback job = delegate
        {
            try
            {
                string py = Path.Combine(voiceDir, @"venv\Scripts\pythonw.exe"), sv = Path.Combine(voiceDir, "server.py");
                if (!File.Exists(py) || !File.Exists(sv)) return;
                try
                {
                    HttpWebRequest r = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:5055/health"); r.Timeout = 1500; r.Proxy = null;
                    using (r.GetResponse()) { } return;
                }
                catch (WebException we) { if (we.Response != null) return; }   // 503 = уже грузится
                if ((DateTime.Now - voiceStartAt).TotalSeconds < 120) return;
                voiceStartAt = DateTime.Now;
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(py, "\"" + sv + "\"");
                psi.WorkingDirectory = voiceDir; psi.UseShellExecute = false; psi.CreateNoWindow = true;
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        };
        if (async) ThreadPool.QueueUserWorkItem(job); else job(null);
    }

    static List<string> SplitSpeech(string t, int maxLen)
    {
        List<string> res = new List<string>();
        foreach (Match m in Regex.Matches(t, @"[^.!?…\n]+[.!?…]*"))
        {
            string sn = m.Value.Trim(); if (sn.Length < 2) continue;
            while (sn.Length > maxLen) { int c = sn.LastIndexOfAny(new char[] { ',', ';', ':', ' ' }, maxLen); if (c < 40) c = maxLen; res.Add(sn.Substring(0, c + 1).Trim()); sn = sn.Substring(c + 1).Trim(); }
            if (sn.Length > 0)
            {
                if (res.Count > 0 && res[res.Count - 1].Length + sn.Length < 90) res[res.Count - 1] += " " + sn;
                else res.Add(sn);
            }
        }
        return res;
    }

    void SpeakClone(string text)
    {
        int gen = ++speakGen;
        string t = text.Replace("=^.^=", " мур! ").Replace("♥", " ").Replace(":)", " ");
        t = Regex.Replace(t, @"https?://\S+", "ссылка");
        if (t.Length > 520) { int cut = t.LastIndexOf('.', 520); t = t.Substring(0, cut > 150 ? cut + 1 : 520); }
        List<string> parts = SplitSpeech(t, 220);
        if (parts.Count == 0) return;
        ThreadPool.QueueUserWorkItem(delegate
        {
            if (!VoiceHealthy())
            {
                EnsureVoiceServer(false);
                try { BeginInvoke((MethodInvoker)delegate { if (gen == speakGen) SpeakIrina(text); }); } catch { }
                return;
            }
            byte[][] ready = new byte[parts.Count][]; bool[] failed = new bool[parts.Count];
            object sync = new object();
            ThreadPool.QueueUserWorkItem(delegate
            {
                for (int i = 0; i < parts.Count && gen == speakGen; i++)
                {
                    byte[] data = null;
                    try
                    {
                        HttpWebRequest r = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:5055/tts");
                        r.Method = "POST"; r.Timeout = 180000; r.ReadWriteTimeout = 180000; r.Proxy = null;
                        byte[] b = Encoding.UTF8.GetBytes(parts[i]); r.ContentLength = b.Length;
                        using (Stream rs = r.GetRequestStream()) rs.Write(b, 0, b.Length);
                        using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
                        using (Stream st = resp.GetResponseStream())
                        using (MemoryStream ms = new MemoryStream()) { st.CopyTo(ms); data = ms.ToArray(); }
                    }
                    catch { }
                    lock (sync) { ready[i] = data; failed[i] = data == null; Monitor.PulseAll(sync); }
                }
            });
            for (int i = 0; i < parts.Count && gen == speakGen; i++)
            {
                byte[] data;
                lock (sync) { while (ready[i] == null && !failed[i] && gen == speakGen) Monitor.Wait(sync, 500); data = ready[i]; }
                if (data == null || gen != speakGen) continue;
                try
                {
                    System.Media.SoundPlayer pl = new System.Media.SoundPlayer(new MemoryStream(data));
                    clonePlayer = pl; talkUntil = DateTime.Now.AddSeconds(Math.Max(1, (data.Length - 44) / 48000.0));
                    pl.PlaySync();
                }
                catch { }
            }
        });
    }
    DateTime talkUntil = DateTime.MinValue;

    void SpeakIrina(string text)
    {
        if (synthFailed) return;
        try
        {
            if (synth == null)
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.Load("System.Speech, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                synthType = asm.GetType("System.Speech.Synthesis.SpeechSynthesizer");
                synth = Activator.CreateInstance(synthType);
                synthType.GetMethod("SetOutputToDefaultAudioDevice").Invoke(synth, null);
                System.Collections.IEnumerable voices = (System.Collections.IEnumerable)synthType.GetMethod("GetInstalledVoices", Type.EmptyTypes).Invoke(synth, null);
                foreach (object iv in voices)
                {
                    object vi = iv.GetType().GetProperty("VoiceInfo").GetValue(iv, null);
                    CultureInfo ci = (CultureInfo)vi.GetType().GetProperty("Culture").GetValue(vi, null);
                    if (ci != null && ci.Name.StartsWith("ru"))
                    {
                        synthType.GetMethod("SelectVoice").Invoke(synth, new object[] { (string)vi.GetType().GetProperty("Name").GetValue(vi, null) });
                        break;
                    }
                }
                synthType.GetProperty("Rate").SetValue(synth, 1, null);
            }
            string t = text.Replace("=^.^=", " мур! ").Replace("♥", " ").Replace(":)", " ");
            t = Regex.Replace(t, @"https?://\S+", "ссылка");
            if (t.Length > 700) { int cut = t.LastIndexOf('.', 700); t = t.Substring(0, cut > 200 ? cut + 1 : 700); }
            synthType.GetMethod("SpeakAsyncCancelAll").Invoke(synth, null);
            synthType.GetMethod("SpeakAsync", new Type[] { typeof(string) }).Invoke(synth, new object[] { t });
        }
        catch { synthFailed = true; }
    }

    void StopSpeak()
    {
        speakGen++;
        try { if (clonePlayer != null) clonePlayer.Stop(); } catch { }
        talkUntil = DateTime.MinValue;
        try { if (synth != null) synthType.GetMethod("SpeakAsyncCancelAll").Invoke(synth, null); } catch { }
    }

    // ---------- карточка ответа над котиком ----------
    class CardForm : Form
    {
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80 | 0x8; return cp; }
        }
    }
    CardForm card; Label cardQ, cardA; System.Windows.Forms.Timer cardTimer;

    void ShowCard(string q, string a, bool done)
    {
        if (card == null || card.IsDisposed)
        {
            card = new CardForm();
            card.FormBorderStyle = FormBorderStyle.None; card.ShowInTaskbar = false; card.TopMost = true;
            card.BackColor = Color.FromArgb(30, 32, 42); card.StartPosition = FormStartPosition.Manual;
            card.Padding = new Padding(14, 10, 14, 12);
            cardQ = new Label(); cardQ.AutoSize = true; cardQ.MaximumSize = new Size(400, 0);
            cardQ.ForeColor = Color.FromArgb(120, 190, 255); cardQ.Font = new Font("Segoe UI", 9f, FontStyle.Italic);
            cardA = new Label(); cardA.AutoSize = true; cardA.MaximumSize = new Size(400, 0);
            cardA.ForeColor = Color.FromArgb(235, 238, 245); cardA.Font = new Font("Segoe UI", 10.5f);
            Label hint = new Label(); hint.AutoSize = true; hint.Name = "hint";
            hint.Text = "клик — открыть чат  ·  правый клик — закрыть"; hint.ForeColor = Color.FromArgb(110, 116, 135); hint.Font = new Font("Segoe UI", 8f);
            card.Controls.Add(cardQ); card.Controls.Add(cardA); card.Controls.Add(hint);
            MouseEventHandler click = delegate (object o, MouseEventArgs e) { card.Hide(); StopSpeak(); if (e.Button == MouseButtons.Left) OpenChat(); };
            card.MouseClick += click; cardQ.MouseClick += click; cardA.MouseClick += click; hint.MouseClick += click;
            card.Paint += delegate (object o, PaintEventArgs e)
            {
                using (Pen pn = new Pen(Color.FromArgb(255, 158, 64), 2)) e.Graphics.DrawRectangle(pn, 1, 1, card.Width - 2, card.Height - 2);
            };
            cardTimer = new System.Windows.Forms.Timer();
            cardTimer.Tick += delegate { cardTimer.Stop(); if (card != null && !card.IsDisposed) card.Hide(); };
        }
        cardQ.Text = "Вы: " + (q.Length > 120 ? q.Substring(0, 117) + "…" : q);
        string at = a.Length > 1400 ? a.Substring(0, 1400) + "…\n(полностью — в чате)" : a;
        cardA.Text = "Котик: " + at;
        cardA.ForeColor = a.StartsWith("Мяу, не получилось") ? Color.FromArgb(255, 130, 130) : Color.FromArgb(235, 238, 245);
        Control hintC = card.Controls["hint"];
        cardQ.Location = new Point(14, 10);
        cardA.Location = new Point(14, cardQ.Bottom + 6);
        hintC.Location = new Point(14, cardA.Bottom + 8);
        int w = Math.Max(Math.Max(cardQ.Right, cardA.Right), hintC.Right) + 14;
        int h = hintC.Bottom + 10;
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        int cx = Left + (int)me.X;
        int x = Math.Max(wa.Left + 8, Math.Min(wa.Right - w - 8, cx - w / 2));
        int y = Math.Max(wa.Top + 8, Top - h - 6);
        card.Bounds = new Rectangle(x, y, w, h);
        if (!card.Visible) card.Show();
        card.Invalidate();
        cardTimer.Stop();
        if (done) { cardTimer.Interval = Math.Min(60000, 8000 + a.Length * 60); cardTimer.Start(); }
    }

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
        ToolStripMenuItem fr = Take("Пугливость") as ToolStripMenuItem; if (fr != null) { fr.Image = Badge("!", Color.FromArgb(230, 90, 90)); sets.DropDownItems.Add(fr); }
        ToolStripMenuItem ai = new ToolStripMenuItem("Голос и ИИ", Badge("✉", Color.FromArgb(80, 150, 230)));
        Put(ai, "Настройки ИИ...", null, Color.Empty, "Подключение ИИ…");
        PutToggle(ai, "Озвучивать ответы");
        PutToggle(ai, "Голос: мой (нейросеть)", "Мой голос (нейросеть)");
        Put(ai, "Очистить память разговора", null, Color.Empty);
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
        menu.Items.Insert(at++, sets);
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
            getKey.LinkClicked += delegate { try { System.Diagnostics.Process.Start(cb.SelectedIndex == 3 ? "https://console.groq.com/keys" : cb.SelectedIndex == 0 ? "https://platform.claude.com" : "https://aistudio.google.com/apikey"); } catch { } };
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

    // ================= рисование =================
    static Color Light(Color c, float t)
    {
        return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t));
    }
    static Color Dark(Color c, float k)
    {
        return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
    }
    static readonly Color InkC = Color.FromArgb(78, 54, 42);
    static Color Mix(Color a, Color b, float t) { return Color.FromArgb(a.A, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
    static Color Ink(Color c) { return c.GetBrightness() < 0.4f ? Color.FromArgb(46, 34, 32) : Mix(Dark(c, 0.42f), InkC, 0.6f); }
    static Color Alpha(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B); }

    static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        GraphicsPath p = new GraphicsPath();
        float d = Math.Min(r * 2, Math.Min(w, h));
        if (d <= 0.01f) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    RectangleF R(float ox, float by, float px, float py, float pw, float ph)
    {
        return new RectangleF(ox + U(px), by - U(py + ph), U(pw), U(ph));
    }

    // объёмная панель: градиент + мягкий контур
    void Panel(Graphics g, Color c, float ox, float by, float px, float py, float pw, float ph, float rad, bool outline = true)
    {
        c = Mix(c, Color.FromArgb(c.A, 236, 214, 170), 0.14f);
        RectangleF r = R(ox, by, px, py, pw, ph);
        if (r.Width < 0.5f || r.Height < 0.5f) return;
        using (GraphicsPath path = RoundRect(r.X, r.Y, r.Width, r.Height, U(rad)))
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, path);
            if (r.Height > U(2.2f) && r.Width > U(2.2f))
            {
                GraphicsState st = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (GraphicsPath sp = new GraphicsPath())
                {
                    sp.AddRectangle(RectangleF.Inflate(r, 2, 2));
                    sp.AddEllipse(r.X - r.Width * 0.3f, r.Y - r.Height * 0.45f, r.Width * 1.22f, r.Height * 1.36f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(c.A * 130 / 255, Dark(c, 0.8f)))) g.FillPath(b, sp);
                }
                g.Restore(st);
            }
            if (outline)
                using (Pen p = new Pen(Color.FromArgb(c.A, Mix(Dark(c, 0.5f), InkC, 0.65f)), Math.Max(1.2f, U(0.55f))))
                { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
        }
    }
    void Wood(Graphics g, Color c, float ox, float by, float px, float py, float pw, float ph, float rad)
    {
        Panel(g, c, ox, by, px, py, pw, ph, rad);
        RectangleF r = R(ox, by, px, py, pw, ph);
        if (r.Width < 3 || r.Height < 3) return;
        GraphicsState st = g.Save();
        using (GraphicsPath path = RoundRect(r.X, r.Y, r.Width, r.Height, U(rad))) g.SetClip(path, CombineMode.Intersect);
        int seed = (int)(px * 31 + py * 17 + pw * 7);
        bool vertical = r.Height > r.Width * 1.5f;
        using (Pen p = new Pen(Color.FromArgb(26, 90, 50, 20), Math.Max(1f, U(0.22f))))
        {
            int n = Math.Max(2, (int)((vertical ? pw : ph) / 1.6f));
            for (int i = 0; i < n; i++)
            {
                float k = (i + 0.5f) / n;
                float amp = U(0.35f) * (float)Math.Sin(seed * 0.7 + i * 1.9);
                if (vertical)
                {
                    float x = r.X + k * r.Width;
                    g.DrawBezier(p, x, r.Y, x + amp, r.Y + r.Height * 0.33f, x - amp, r.Y + r.Height * 0.66f, x + amp * 0.5f, r.Bottom);
                }
                else
                {
                    float y = r.Y + k * r.Height;
                    g.DrawBezier(p, r.X, y, r.X + r.Width * 0.33f, y + amp, r.X + r.Width * 0.66f, y - amp, r.Right, y + amp * 0.5f);
                }
            }
            if (seed % 3 == 0 && r.Width > U(8) && r.Height > U(3))
                g.DrawEllipse(p, r.X + r.Width * 0.6f, r.Y + r.Height * 0.35f, U(1.4f), U(0.7f));
        }
        g.Restore(st);
    }

    void Flat(Graphics g, Color c, float ox, float by, float px, float py, float pw, float ph, float rad)
    {
        RectangleF r = R(ox, by, px, py, pw, ph);
        if (r.Width < 0.3f || r.Height < 0.3f) return;
        using (GraphicsPath path = RoundRect(r.X, r.Y, r.Width, r.Height, U(rad)))
        using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, path);
    }
    void Shadow(Graphics g, float cx, float w)
    {
        using (SolidBrush b = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
            g.FillEllipse(b, cx - w / 2, Ground - U(1.6f), w, U(2.4f));
    }

    Bitmap hudBuf;
    int menuThrottleT;   // пока открыто меню — рисуем реже, чтобы меню не тормозило
    void RenderFrame()
    {
        if (Width <= 0 || Height <= 0) return;
        if (buf == null || buf.Width != Width || buf.Height != Height)
        {
            if (buf != null) buf.Dispose();
            buf = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        }
        long pf = prof ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        using (Graphics g = Graphics.FromImage(buf))
        {
            g.Clear(Color.Transparent);
            DrawAll(g);
        }
        if (prof) { ProfAdd(1, pf); pf = System.Diagnostics.Stopwatch.GetTimestamp(); }
        Bitmap outB = buf;
        float n = NightK();
        if (IsFog) outB = ApplyFog(outB);
        if (n > 0.02f) outB = ApplyNight(outB, n);
        if (n > 0.02f) using (Graphics g = Graphics.FromImage(outB)) { g.SmoothingMode = SmoothingMode.AntiAlias; DrawNightLights(g, n); }
        // погода и панель — слоем ПОД котом. Сам слой перерисовывается редко (2 раза в секунду или при изменении),
        // а в каждом кадре только склеивается с котом в небольшом прямоугольнике панели.
        if (hudBuf == null || hudBuf.Width != Width || hudBuf.Height != Height)
        {
            if (hudBuf != null) hudBuf.Dispose();
            hudBuf = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            hudDirty = true;
        }
        bool hudOn = !HudUnderNote() && !bigOpen;
        if (hudOn != hudWasOn) { hudWasOn = hudOn; hudDirty = true; }
        if (hudDirty || ++hudAge >= 15) RebuildHud(hudOn);
        using (Graphics g = Graphics.FromImage(hudBuf))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(outB, 0, 0);
            if (hudRect.Width > 0 && hudRect.Height > 0)
            {
                g.DrawImage(hudCache, hudRect, hudRect.X, hudRect.Y, hudRect.Width, hudRect.Height, GraphicsUnit.Pixel);
                g.CompositingMode = CompositingMode.SourceOver;
                g.DrawImage(outB, hudRect, hudRect.X, hudRect.Y, hudRect.Width, hudRect.Height, GraphicsUnit.Pixel);
            }
        }
        if (prof) { ProfAdd(2, pf); pf = System.Diagnostics.Stopwatch.GetTimestamp(); }
        PushLayered(hudBuf);
        if (prof) ProfAdd(3, pf);
    }

    Bitmap hudCache; Rectangle hudRect; int hudAge; bool hudDirty = true, hudWasOn;
    RectangleF panelCardRect;
    public void HudChanged() { hudDirty = true; }
    void RebuildHud(bool on)
    {
        hudDirty = false; hudAge = 0;
        if (hudCache == null || hudCache.Width != Width || hudCache.Height != Height)
        {
            if (hudCache != null) hudCache.Dispose();
            hudCache = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        }
        panelCardRect = RectangleF.Empty;
        using (Graphics g = Graphics.FromImage(hudCache))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (on) { DrawWeatherWidget(g); DrawHud(g); } else panelHits.Clear();
        }
        RectangleF r = RectangleF.Empty;
        if (on && WxVisible) r = WidgetRect();
        if (on && !panelCardRect.IsEmpty) r = r.IsEmpty ? panelCardRect : RectangleF.Union(r, panelCardRect);
        Rectangle ri = Rectangle.Round(RectangleF.Inflate(r, 3, 3));
        ri.Intersect(new Rectangle(0, 0, Width, Height));
        hudRect = ri;
    }

    // ПРОФИЛЬ: создайте файл %APPDATA%\VapePet_profile.flag — раз в 10 с пишет время по этапам в VapePet_profile.log
    static readonly bool prof = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_profile.flag"));
    long[] profT = new long[6]; int profN;
    void ProfAdd(int i, long from) { profT[i] += System.Diagnostics.Stopwatch.GetTimestamp() - from; }
    void ProfTick()
    {
        if (++profN < 300) return;
        double k = 1000.0 / System.Diagnostics.Stopwatch.Frequency / profN;
        string line = DateTime.Now.ToString("HH:mm:ss") + " мс/кадр: логика " + (profT[0] * k).ToString("0.00") + ", котик " + (profT[1] * k).ToString("0.00") + ", панель+слой " + (profT[2] * k).ToString("0.00") + ", вывод " + (profT[3] * k).ToString("0.00") + ", окно " + Width + "x" + Height + "\r\n";
        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_profile.log"), line, Encoding.UTF8); } catch { }
        profN = 0; for (int i = 0; i < profT.Length; i++) profT[i] = 0;
    }

    void DrawAll(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if (shakeT > 0) g.TranslateTransform((float)(rnd.NextDouble() - 0.5) * U(1.4f), (float)(rnd.NextDouble() - 0.5) * U(0.8f));

        DrawWeatherBack(g);
        if (mode == 1 || mode == 6) DrawShop(g);
        if (mode == 2) DrawChill(g);
        if (mode == 3) DrawTidy(g);
        if (mode == 5) { if (omonPhase == 0) DrawShop(g); else DrawJailBack(g); if (vanState != 0) DrawPoliceVan(g); }
        if (mode == 4)
        {
            DrawPartyBack(g);
            foreach (Cat c in guests) if (c.Swimming) DrawCat(g, c);
            if (me.Swimming) DrawCat(g, me);
            DrawPoolFront(g);
        }

        DrawWeatherGround(g);
        DrawPickups(g);
        List<Cat> zl = new List<Cat>();
        foreach (Cat c in guests) if (!c.Swimming) zl.Add(c);
        if (!me.Swimming) zl.Add(me);
        for (int i = 1; i < zl.Count; i++) { Cat t = zl[i]; int j = i - 1; while (j >= 0 && zl[j].Z > t.Z) { zl[j + 1] = zl[j]; j--; } zl[j + 1] = t; }
        foreach (Cat c in zl) DrawCat(g, c);

        if (mode == 2 && pendingMode < 0 && me.Sitting) DrawHose(g);
        if (mode == 5 && omonPhase == 1) DrawJailFront(g);
        if (mode == 5 && sirenT > 0) DrawSiren(g);

        foreach (Flyer f in flyers)
        {
            float t = f.T / (float)f.Dur;
            float x = f.X0 + (f.X1 - f.X0) * t;
            float y = f.Y0 + (f.Y1 - f.Y0) * t - (float)Math.Sin(Math.PI * t) * U(6);
            DrawItem(g, f.Kind, f.Col, x, y);
        }

        foreach (Particle p in parts)
        {
            float k = 1f, fade = p.Max * 0.55f;
            if (p.Life > fade) k = 1f - (p.Life - fade) / (p.Max - fade);
            if (p.Kind == 0)
            {
                float fin = Math.Min(1f, p.Life / 6f);
                int a = (int)(170 * k * fin);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a / 2, 225, 228, 236)))
                    g.FillEllipse(b, p.X - p.R * 1.3f, p.Y - p.R * 1.3f, p.R * 2.6f, p.R * 2.6f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 246, 247, 250)))
                    g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
            }
            else if (p.Kind == 1)
            {
                using (SolidBrush b = new SolidBrush(Alpha(pal[p.Col], (int)(230 * k))))
                    g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
            }
            else if (p.Kind == 2 && k > 0.05f) DrawItem(g, 'M', p.Col, p.X, p.Y, (int)(255 * k));
            else if (p.Kind == 3 && k > 0.05f) DrawItem(g, 'H', p.Col, p.X, p.Y, (int)(255 * k));
            else if (p.Kind == 4 && k > 0.05f) DrawItem(g, 'S', 'Y', p.X, p.Y, (int)(255 * k), p.Life * 12);
            else if (p.Kind == 5)
            {
                int a5 = (int)(150 * k * Math.Min(1f, p.Life / 5f));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a5, 190, 180, 160)))
                    g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
            }
        }

        foreach (Particle p in parts)
            if (p.Kind == 8)
            {
                int ma = (int)(220 * Math.Max(0, 1 - p.Life / (float)p.Max * 0.6f));
                using (SolidBrush b = new SolidBrush(p.Life % 6 < 2 ? Color.FromArgb(ma, 200, 255, 200) : Color.FromArgb(ma, 40, 230, 90))) g.DrawString(p.Col.ToString(), fntMatrix, b, p.X, p.Y);
            }
        foreach (Particle p in parts)
            if (p.Kind == 9)
            {
                int qa = (int)(255 * Math.Max(0, 1 - p.Life / (float)p.Max));
                OutlinedText(g, "?", fnt, Color.FromArgb(qa, 255, 200, 80), Color.FromArgb(qa, 40, 30, 20), p.X, p.Y, 1.1f);
            }
        DrawWeatherFront(g);
        DrawBigText(g);
        foreach (FText f in texts)
        {
            float k = f.Life < 6 ? f.Life / 6f : (f.Life > f.Max - 12 ? (f.Max - f.Life) / 12f : 1f);
            SizeF sz = g.MeasureString(f.Text, fnt);
            float w = sz.Width + U(2.4f), h = sz.Height + U(0.8f);
            float x = f.X - w / 2, y = f.Y - h;
            if (x < 2) x = 2; if (x + w > Width - 2) x = Width - 2 - w;
            using (GraphicsPath path = RoundRect(x, y, w, h, h / 2))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(248 * k), 255, 251, 238))) g.FillPath(b, path);
                using (Pen p = new Pen(Color.FromArgb((int)(220 * k), 78, 54, 42), Math.Max(1f, U(0.4f)))) g.DrawPath(p, path);
            }
            Color tc = pal.ContainsKey(f.Col) ? pal[f.Col] : pal['K'];
            using (SolidBrush b = new SolidBrush(Alpha(tc, (int)(255 * k))))
                g.DrawString(f.Text, fnt, b, x + U(1.2f), y + U(0.4f));
        }
    }

    // ---------- котик (на двух лапах) ----------
    void Limb(Graphics g, PointF a, PointF b, Color fill, Color line, float w)
    {
        using (Pen p = new Pen(line, w + 0.55f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawLine(p, a, b); }
        using (Pen p = new Pen(fill, w)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawLine(p, a, b); }
    }

    static PointF P(float x, float y) { return new PointF(x, y); }

    bool HasSleeves(int o) { return o == 1 || o == 3 || o == 4 || o == 6 || o == 9 || o == 10 || o == 11 || o == 12 || o == 13 || (o >= 14 && o != 16 && o != 17); }
    bool ShortSleeves(int o) { return o == 2 || o == 5 || o == 7 || o == 16 || o == 17; }
    int PantsType(int o) { if (o == 2 || o == 3 || o == 4 || o == 6 || o >= 10) return 2; if (o == 7) return 1; return 0; }

    // ---------- мультяшные детали ----------
    void TaperLimb(Graphics g, PointF a, PointF b, float wa, float wb, Color fill, Color line, bool outline)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f) len = 0.01f;
        float nx = -dy / len, ny = dx / len;
        using (GraphicsPath p = new GraphicsPath())
        {
            p.FillMode = FillMode.Winding;
            p.AddPolygon(new PointF[] { P(a.X + nx * wa, a.Y + ny * wa), P(b.X + nx * wb, b.Y + ny * wb), P(b.X - nx * wb, b.Y - ny * wb), P(a.X - nx * wa, a.Y - ny * wa) });
            p.AddEllipse(a.X - wa, a.Y - wa, wa * 2, wa * 2);
            p.AddEllipse(b.X - wb, b.Y - wb, wb * 2, wb * 2);
            if (outline) using (Pen pen = new Pen(line, 1.1f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
            using (SolidBrush br = new SolidBrush(fill)) g.FillPath(br, p);
        }
    }

    void Mitten(Graphics g, PointF p, Color fill, Color line, float r)
    {
        using (SolidBrush b = new SolidBrush(line)) g.FillEllipse(b, p.X - r - 0.42f, p.Y - r - 0.42f, (r + 0.42f) * 2, (r + 0.42f) * 2);
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(p.X - r, p.Y - r - 0.1f, r * 2, r * 2 + 0.2f), Light(fill, 0.06f), Dark(fill, 0.95f), 90f)) g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        using (Pen tp = new Pen(Color.FromArgb(150, line), 0.18f)) { g.DrawLine(tp, p.X + r * 0.15f, p.Y + r * 0.2f, p.X + r * 0.25f, p.Y + r * 0.9f); g.DrawLine(tp, p.X + r * 0.6f, p.Y, p.X + r * 0.75f, p.Y + r * 0.7f); }
    }

    void Foot(Graphics g, PointF p, Color fill, Color line)
    {
        RectangleF fr = new RectangleF(p.X - 1.2f, p.Y - 0.95f, 3.3f, 1.9f);
        using (SolidBrush b = new SolidBrush(line)) g.FillEllipse(b, fr.X - 0.42f, fr.Y - 0.42f, fr.Width + 0.84f, fr.Height + 0.84f);
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(fr.X, fr.Y - 0.1f, fr.Width, fr.Height + 0.2f), Light(fill, 0.2f), Dark(fill, 0.88f), 90f)) g.FillEllipse(b, fr);
        using (Pen tp = new Pen(Color.FromArgb(150, line), 0.18f)) { g.DrawLine(tp, fr.Right - 0.9f, fr.Y + 0.4f, fr.Right - 0.8f, fr.Bottom - 0.2f); g.DrawLine(tp, fr.Right - 1.7f, fr.Y + 0.3f, fr.Right - 1.6f, fr.Bottom - 0.2f); }
    }

    static PointF Bez(PointF a, PointF b, PointF c, PointF d, float t)
    {
        float m = 1 - t;
        return new PointF(m * m * m * a.X + 3 * m * m * t * b.X + 3 * m * t * t * c.X + t * t * t * d.X, m * m * m * a.Y + 3 * m * m * t * b.Y + 3 * m * t * t * c.Y + t * t * t * d.Y);
    }

    void FluffyTail(Graphics g, Cat c, Color main, Color tip, Color line, float ts)
    {
        PointF a = P(-2.6f, -6.6f), b = P(-7.4f, -6.0f), cc = P(-8.4f + ts, -12.6f), d = P(-5.6f + ts * 1.2f, -15.8f);
        int n = 16;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                PointF p = Bez(a, b, cc, d, t);
                float r = 1.2f - 0.5f * t + (t > 0.82f ? (t - 0.82f) * 1.6f : 0);
                if (t > 0.95f) r *= 0.75f;
                if (pass == 0) { using (SolidBrush br = new SolidBrush(line)) g.FillEllipse(br, p.X - r - 0.42f, p.Y - r - 0.42f, (r + 0.42f) * 2, (r + 0.42f) * 2); }
                else
                {
                    Color col = t > 0.8f ? tip : (c.Stripes && ((int)(t * 9) % 2 == 1) ? Dark(main, 0.78f) : main);
                    using (SolidBrush br = new SolidBrush(col)) g.FillEllipse(br, p.X - r, p.Y - r, r * 2, r * 2);
                }
            }
    }

    // глаз в рисованной манере: emo 0 обычный, 1 радость, 2 злость, 3 испуг
    void ToonEye(Graphics g, Cat c, float cx, float cy, float w, float h, int emo, bool blink, bool near, Color line, Color fur)
    {
        Color dk = Color.FromArgb(52, 36, 30);
        if (blink)
        {
            using (Pen ep = new Pen(dk, 0.5f)) { ep.StartCap = LineCap.Round; ep.EndCap = LineCap.Round; g.DrawArc(ep, cx - w / 2, cy - h * 0.15f, w, h * 0.5f, 15, 150); }
            return;
        }
        if (emo == 1)
        {
            using (Pen ep = new Pen(dk, 0.55f)) { ep.StartCap = LineCap.Round; ep.EndCap = LineCap.Round; g.DrawArc(ep, cx - w / 2, cy - h * 0.2f, w, h * 0.65f, 200, 140); }
            return;
        }
        if (emo == 3)
        {
            RectangleF er = new RectangleF(cx - w / 2, cy - h / 2, w, h);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, er);
            using (Pen lp = new Pen(dk, 0.36f)) g.DrawEllipse(lp, er);
            using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, cx - w * 0.16f, cy - h * 0.16f, w * 0.32f, h * 0.32f);
            return;
        }
        float ew = w * 0.74f, eh = h * 0.98f;
        using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, cx - ew / 2, cy - eh / 2, ew, eh);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(150, c.IrisC))) g.FillPie(b, cx - ew * 0.36f, cy - eh * 0.2f, ew * 0.72f, eh * 0.62f, 20, 140);
        using (SolidBrush b = new SolidBrush(Color.White))
        {
            g.FillEllipse(b, cx - ew * 0.36f, cy - eh * 0.4f, ew * 0.5f, ew * 0.54f);
            g.FillEllipse(b, cx + ew * 0.1f, cy + eh * 0.16f, ew * 0.24f, ew * 0.24f);
        }
        if (emo == 2)
        {
            float sgn = near ? -1 : 1;
            PointF[] lid = new PointF[] { P(cx - w * 0.6f, cy - h * 0.7f), P(cx + w * 0.6f, cy - h * 0.7f), P(cx + w * 0.6f, cy - h * (0.2f + 0.16f * sgn)), P(cx - w * 0.6f, cy - h * (0.2f - 0.16f * sgn)) };
            using (SolidBrush b = new SolidBrush(fur)) g.FillPolygon(b, lid);
            using (Pen lp = new Pen(dk, 0.5f)) { lp.StartCap = LineCap.Round; lp.EndCap = LineCap.Round; g.DrawLine(lp, lid[3], lid[2]); }
        }
    }

    void DrawCat(Graphics g, Cat c)
    {
        GraphicsState gs = g.Save();
        try { DrawCatZ(g, c); }
        catch (Exception ex) { GuardLog("рисование кота", ex); }
        finally { g.Restore(gs); }
    }

    void DrawCatZ(Graphics g, Cat c)
    {
        if (c.Z == 0) { DrawCatBase(g, c); return; }
        int g0 = Ground; float s0 = c.Scale, z0 = c.Z;
        Ground = g0 + (int)Math.Round(ZY(z0)); c.Scale = s0 * ZK(z0); c.Z = 0;
        try { DrawCatBase(g, c); } finally { Ground = g0; c.Scale = s0; c.Z = z0; }
    }

    // ================= рисованный котик: вид спереди, сбоку и со спины =================
    const float NK = 0.24f;
    static GraphicsPath NEll(float x, float y, float w, float h) { GraphicsPath p = new GraphicsPath(); p.AddEllipse(x, y, w, h); return p; }
    static GraphicsPath NPoly(params float[] v)
    {
        GraphicsPath p = new GraphicsPath(); PointF[] pt = new PointF[v.Length / 2];
        for (int i = 0; i < pt.Length; i++) pt[i] = new PointF(v[i * 2], v[i * 2 + 1]);
        p.AddPolygon(pt); return p;
    }
    // заливка + мягкая тень справа-снизу + тёплый контур
    void NBlob(Graphics g, GraphicsPath p, Color fill, Color shade, Pen pen)
    {
        using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
        if (shade.A > 0)
        {
            GraphicsState st = g.Save();
            g.SetClip(p, CombineMode.Intersect);
            RectangleF r = p.GetBounds();
            using (GraphicsPath sp = new GraphicsPath())
            {
                sp.AddRectangle(RectangleF.Inflate(r, 4, 4));
                sp.AddEllipse(r.X - r.Width * 0.22f, r.Y - r.Height * 0.3f, r.Width * 1.1f, r.Height * 1.14f);
                using (SolidBrush b = new SolidBrush(shade)) g.FillPath(b, sp);
            }
            g.Restore(st);
        }
        if (pen != null) g.DrawPath(pen, p);
        p.Dispose();
    }

    void NTail(Graphics g, Cat c, Color ink, float x, float y, float dir, float tw)
    {
        float tk = c.AngryT > 0 ? 1.45f : 1f;
        PointF p0 = new PointF(x, y), p1 = new PointF(x + dir * 22, y - 2), p2 = new PointF(x + dir * 30 + tw * 4, y - 26), p3 = new PointF(x + dir * 20 + tw * 7, y - 40);
        using (GraphicsPath p = new GraphicsPath())
        using (Pen o = new Pen(ink, 15.5f * tk))
        using (Pen f = new Pen(c.MainC, 9.6f * tk))
        {
            o.StartCap = LineCap.Round; o.EndCap = LineCap.Round; f.StartCap = LineCap.Round; f.EndCap = LineCap.Round;
            p.AddBezier(p0, p1, p2, p3);
            g.DrawPath(o, p); g.DrawPath(f, p);
            if (c.Stripes && !c.Bald)
                using (Pen sp = new Pen(Dark(c.MainC, 0.78f), 3.4f * tk))
                {
                    sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round;
                    float[] ts = { 0.34f, 0.52f, 0.7f, 0.86f };
                    foreach (float t in ts)
                    {
                        float u1 = 1 - t;
                        float bx = u1 * u1 * u1 * p0.X + 3 * u1 * u1 * t * p1.X + 3 * u1 * t * t * p2.X + t * t * t * p3.X;
                        float by = u1 * u1 * u1 * p0.Y + 3 * u1 * u1 * t * p1.Y + 3 * u1 * t * t * p2.Y + t * t * t * p3.Y;
                        float dx = 3 * u1 * u1 * (p1.X - p0.X) + 6 * u1 * t * (p2.X - p1.X) + 3 * t * t * (p3.X - p2.X);
                        float dy = 3 * u1 * u1 * (p1.Y - p0.Y) + 6 * u1 * t * (p2.Y - p1.Y) + 3 * t * t * (p3.Y - p2.Y);
                        float len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 0.01f) continue;
                        float nx = -dy / len * 4.2f * tk, ny = dx / len * 4.2f * tk;
                        g.DrawLine(sp, bx - nx, by - ny, bx + nx, by + ny);
                    }
                }
        }
    }

    void NToes(Graphics g, Cat c, Pen thin, float cx, float dy)
    {
        using (SolidBrush b = new SolidBrush(Mix(c.MainC, c.BellyC, 0.55f))) g.FillEllipse(b, cx - 5.2f, -5.6f + dy, 10.4f, 5f);
        g.DrawLine(thin, cx - 1.8f, -3.6f + dy, cx - 1.8f, -1.2f + dy); g.DrawLine(thin, cx + 1.8f, -3.6f + dy, cx + 1.8f, -1.2f + dy);
    }

    void NEars(Graphics g, Cat c, Pen pen, float dx)
    {
        int ph = (tick + c.Id * 97) % 260;
        float tw = ph < 7 ? (float)Math.Sin(ph / 7f * Math.PI) : 0;
        float droop = c.SweatT > 0 || c.Shake > 0 ? 1f : 0f;
        for (int k = -1; k <= 1; k += 2)
        {
            float tx = k * (3 * (k > 0 ? tw : 0) + 6 * droop), ty = 5 * (k > 0 ? tw : 0) + 9 * droop;
            NBlob(g, NPoly(dx + k * 28, -78, dx + k * 25 + tx, -108 + ty, dx + k * 5, -92), c.MainC, Color.Empty, pen);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(246, 172, 160))) using (GraphicsPath p = NPoly(dx + k * 24, -85, dx + k * 23 + tx * 0.8f, -101 + ty * 0.8f, dx + k * 12, -92)) g.FillPath(b, p);
        }
    }

    void NStripes(Graphics g, Cat c, float dx)
    {
        if (!c.Stripes || c.Bald) return;
        using (Pen sp = new Pen(Dark(c.MainC, 0.8f), 3.6f))
        {
            sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round;
            g.DrawLine(sp, dx - 8, -95, dx - 6.5f, -86.5f); g.DrawLine(sp, dx, -96.5f, dx, -85.5f); g.DrawLine(sp, dx + 8, -95, dx + 6.5f, -86.5f);
            // по бокам головы
            g.DrawLine(sp, dx - 30.5f, -80, dx - 24.5f, -79); g.DrawLine(sp, dx - 30, -73.5f, dx - 25, -73.5f);
            g.DrawLine(sp, dx + 30.5f, -80, dx + 24.5f, -79); g.DrawLine(sp, dx + 30, -73.5f, dx + 25, -73.5f);
        }
    }

    // мордочка: emo 0 обычная, 1 радость, 2 злость, 3 испуг
    void NFace(Graphics g, Cat c, Pen thin, float dx, int emo, bool blink)
    {
        Color dk = Color.FromArgb(50, 36, 30);
        using (SolidBrush b = new SolidBrush(c.BellyC)) g.FillEllipse(b, dx - 18, -73, 36, 23);
        bool puffing = c.Vape && c.PuffT > 55;
        if (puffing)
            using (SolidBrush b = new SolidBrush(c.BellyC))
            using (Pen cp = new Pen(Color.FromArgb(150, InkC), 1.2f))
                for (int k = -1; k <= 1; k += 2) { RectangleF cr = new RectangleF(dx + k * 17 - 8, -73, 16, 14); g.FillEllipse(b, cr); g.DrawArc(cp, cr, k > 0 ? -60 : 120, 120); }
        using (SolidBrush b = new SolidBrush(Color.FromArgb(puffing ? 160 : 110, 250, 130, 130))) { g.FillEllipse(b, dx - 26, -70, 10, 6.5f); g.FillEllipse(b, dx + 16, -70, 10, 6.5f); }
        float look = (c.Walking || c.ZMove) ? 0 : (float)Math.Round(Math.Sin((tick + c.Id * 53) * 0.017) * 1.3) * 1.3f;
        using (Pen ep = new Pen(dk, 2f))
        {
            ep.StartCap = LineCap.Round; ep.EndCap = LineCap.Round;
            for (int k = -1; k <= 1; k += 2)
            {
                float ex = dx + k * 14, ew = (dx > 0 && k > 0) ? 8.4f : 10.6f;
                if (blink) g.DrawArc(ep, ex - 4, -79, 8, 6, 0, 180);
                else if (emo == 1) g.DrawArc(ep, ex - 4.5f, -80, 9, 9, 180, 180);
                else if (emo == 3)
                {
                    g.FillEllipse(Brushes.White, ex - 5.2f, -84, 10.4f, 12.5f); g.DrawEllipse(thin, ex - 5.2f, -84, 10.4f, 12.5f);
                    using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, ex - 1.7f, -79.4f, 3.4f, 3.6f);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 16, 18))) g.FillEllipse(b, ex - ew / 2 + look, -85, ew, 13.5f);
                    g.FillEllipse(Brushes.White, ex - ew / 2 + 1.4f + look, -83.2f, ew * 0.46f, ew * 0.46f); g.FillEllipse(Brushes.White, ex + ew * 0.12f + look, -76.6f, ew * 0.2f, ew * 0.2f);
                    if (emo == 2) g.DrawLine(ep, ex + k * 6.5f, -90.5f, ex - k * 4.5f, -85);
                }
                g.DrawLine(thin, dx + k * 19, -65.5f, dx + k * 34, -68f); g.DrawLine(thin, dx + k * 19, -62f, dx + k * 33, -60.5f);
            }
        }
        using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 120, 120))) using (GraphicsPath p = NPoly(dx - 3, -67.5f, dx + 3, -67.5f, dx, -64)) g.FillPath(b, p);
        if (c.Vape)
        {
            bool on = c.PuffT > 55;
            GraphicsState vs = g.Save();
            g.TranslateTransform(dx + 1, -63.5f); g.RotateTransform(14);
            NBlob(g, RoundRect(0, -3.6f, 25, 7.2f, 2.4f), Color.FromArgb(62, 70, 86), Color.FromArgb(70, 10, 12, 20), thin);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillRectangle(b, 3, -2.6f, 17, 1.3f);
            if (on) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 120, 255, 255))) g.FillEllipse(b, 14, -7, 12, 12);
            using (SolidBrush b = new SolidBrush(on ? Color.FromArgb(150, 255, 255) : Color.FromArgb(70, 200, 220))) g.FillEllipse(b, 17.5f, -1.6f, 3.4f, 3.2f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 32, 40))) g.FillRectangle(b, 23.4f, -2.2f, 1.2f, 4.4f);
            g.Restore(vs);
        }
        else if (emo == 2) { using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 50, 56))) g.FillEllipse(b, dx - 4, -63.6f, 8, 5.6f); g.DrawEllipse(thin, dx - 4, -63.6f, 8, 5.6f); }
        else if (emo == 1) { using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 60, 70))) g.FillPie(b, dx - 5, -68, 10, 9, 0, 180); g.DrawArc(thin, dx - 5, -68, 10, 9, 0, 180); g.DrawLine(thin, dx - 5, -63.5f, dx + 5, -63.5f); }
        else if (emo == 3) g.DrawLines(thin, new PointF[] { P(dx - 5, -61.5f), P(dx - 2.5f, -63.5f), P(dx, -61.5f), P(dx + 2.5f, -63.5f), P(dx + 5, -61.5f) });
        else { g.DrawArc(thin, dx - 5, -66, 5, 4.4f, 0, 180); g.DrawArc(thin, dx, -66, 5, 4.4f, 0, 180); }
        if (c.Glasses || c.SmallShades)
        {
            float gw = c.Glasses ? 17 : 13, gh = c.Glasses ? 13 : 9, gy = c.Glasses ? -85 : -82;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 36, 30, 34)))
                for (int k = -1; k <= 1; k += 2) using (GraphicsPath p = RoundRect(dx + k * 13 - gw / 2, gy, gw, gh, 4)) g.FillPath(b, p);
            using (Pen p = new Pen(Color.FromArgb(36, 30, 34), 2f)) { g.DrawLine(p, dx - 5, gy + 4, dx + 5, gy + 4); g.DrawLine(p, dx - 13 - gw / 2, gy + 4, dx - 30, gy + 6); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) { g.FillEllipse(b, dx - 18, gy + 2, 5, 2.4f); g.FillEllipse(b, dx + 8, gy + 2, 5, 2.4f); }
        }
    }

    // старые костюмы надеваются на новое тело: рисуем их в прежних координатах, обрезая по силуэту
    int curView;
    void NHoodie(Graphics g, Cat c, Pen pen, float bx, float by, float bw, float bh)
    {
        Color hc = c.C1, hl = Light(c.C1, 0.18f), hd = Dark(c.C1, 0.8f);
        float cx = bx + bw / 2;
        NBlob(g, NEll(bx, by, bw, bh), hc, Color.FromArgb(120, hd), pen);
        using (Pen thin = new Pen(Color.FromArgb(34, 26, 28), 1.8f))
        using (Pen seam = new Pen(hl, 1.6f))
        {
            thin.StartCap = LineCap.Round; thin.EndCap = LineCap.Round; seam.StartCap = LineCap.Round; seam.EndCap = LineCap.Round;
            if (curView == 2)
            {
                // со спины: капюшон
                NBlob(g, NPoly(cx - 17, by - 2, cx + 17, by - 2, cx + 11, by + 15, cx, by + 19, cx - 11, by + 15), hl, Color.FromArgb(110, hd), pen);
                g.DrawLine(seam, cx, by + 1, cx, by + 15);
                return;
            }
            // капюшон лежит на плечах
            NBlob(g, RoundRect(bx - 1, by - 4, bw + 2, 11, 5.5f), hl, Color.FromArgb(90, hd), pen);
            if (curView == 1)
            {
                // сбоку: передняя половина кармана и один шнурок
                using (GraphicsPath p = NPoly(cx - 2, by + bh * 0.52f, cx + 13, by + bh * 0.52f, cx + 16, by + bh * 0.84f, cx - 2, by + bh * 0.84f))
                { using (SolidBrush b = new SolidBrush(Dark(hc, 0.9f))) g.FillPath(b, p); g.DrawPath(thin, p); }
                g.DrawLine(seam, cx + 9, by + 5, cx + 10, by + 16);
                using (SolidBrush b = new SolidBrush(hl)) g.FillEllipse(b, cx + 8.6f, by + 15, 2.8f, 3.6f);
                return;
            }
            // карман-кенгуру
            using (GraphicsPath p = NPoly(cx - 14, by + bh * 0.52f, cx + 14, by + bh * 0.52f, cx + 17, by + bh * 0.84f, cx - 17, by + bh * 0.84f))
            { using (SolidBrush b = new SolidBrush(Dark(hc, 0.9f))) g.FillPath(b, p); g.DrawPath(thin, p); }
            g.DrawLine(seam, cx - 13, by + bh * 0.56f, cx + 13, by + bh * 0.56f);
            // шнурки
            g.DrawLine(seam, cx - 5, by + 5, cx - 6, by + 17); g.DrawLine(seam, cx + 5, by + 5, cx + 6, by + 17);
            using (SolidBrush b = new SolidBrush(hl)) { g.FillEllipse(b, cx - 7.4f, by + 16, 2.8f, 3.6f); g.FillEllipse(b, cx + 4.6f, by + 16, 2.8f, 3.6f); }
            // нашивка
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 50, 56))) g.FillRectangle(b, cx + 9, by + 11, 6, 4.6f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 236, 230))) g.FillRectangle(b, cx + 9.8f, by + 11.8f, 4.4f, 1.1f);
        }
    }

    void NOutfit(Graphics g, Cat c, Pen pen, float bx, float by, float bw, float bh)
    {
        if (c.Outfit == 1) NHoodie(g, c, pen, bx, by, bw, bh);
        else if (c.Outfit != 0)
        {
            GraphicsState s = g.Save();
            using (GraphicsPath p = NEll(bx, by, bw, bh)) g.SetClip(p, CombineMode.Intersect);
            g.ScaleTransform(1f / NK, 1f / NK);
            g.TranslateTransform((bx + bw / 2) * NK, -8.6f); g.ScaleTransform(1.42f, 1.22f); g.TranslateTransform(0, 8.6f);
            DrawOutfitTorso(g, c, new RectangleF(-3.9f, -13.0f, 7.8f, 8.8f));
            g.Restore(s);
            g.DrawEllipse(pen, bx, by, bw, bh);
        }
        if (c.Chain) using (Pen cp = new Pen(Color.FromArgb(246, 200, 70), 2.2f)) g.DrawArc(cp, bx + bw / 2 - 11, by - 8, 22, 22, 20, 140);
        if (IsCold && c.Outfit != 11 && c.Outfit != 10)
        {
            GraphicsState s = g.Save();
            g.ScaleTransform(1f / NK, 1f / NK);
            g.TranslateTransform((bx + bw / 2) * NK, -13f); g.ScaleTransform(1.3f, 1.15f); g.TranslateTransform(0, 13f);
            DrawScarf(g, c);
            g.Restore(s);
        }
    }

    void NHead(Graphics g, Cat c, float hx)
    {
        GraphicsState s = g.Save();
        g.ScaleTransform(1f / NK, 1f / NK);
        g.TranslateTransform(hx * NK, -17f); g.ScaleTransform(1.16f, 1.12f); g.TranslateTransform(-1.1f, 17f);
        {
            int hatSave = c.Hat; Color hatCSave = c.HatC;
            if (IsFreezing && c.Hat == 0 && c.Outfit != 10 && c.Outfit != 11) { c.Hat = 2; Color[] hc = { Color.FromArgb(220, 60, 70), Color.FromArgb(60, 120, 220), Color.FromArgb(250, 190, 60), Color.FromArgb(70, 170, 110), Color.FromArgb(170, 100, 220) }; c.HatC = hc[Math.Abs(c.Id) % hc.Length]; }
            DrawHat(g, c);
            c.Hat = hatSave; c.HatC = hatCSave;
            if (c == me && musicT > 0)
            {
                using (Pen hp = new Pen(Color.FromArgb(40, 40, 48), 0.8f)) g.DrawArc(hp, -4.6f, -26.0f, 11.0f, 11.0f, 195, 150);
                using (GraphicsPath p = RoundRect(-5.6f, -20.2f, 2.2f, 3.8f, 0.9f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 60, 70))) g.FillPath(b, p);
                using (GraphicsPath p = RoundRect(5.4f, -20.4f, 2.2f, 3.8f, 0.9f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 60, 70))) g.FillPath(b, p);
            }

        }
        g.Restore(s);
    }

    void DrawCatBase(Graphics g, Cat c)
    {
        if (c.Hidden) return;
        bool low = c.Sitting || c.Swimming;
        if (c.Lying) Shadow(g, c.X + c.Dir * U(11) * c.Scale, U(26) * c.Scale);
        else if (!low) Shadow(g, c.X, U(12) * c.Scale * Math.Max(0.4f, 1 - c.Y / U(40)));

        GraphicsState st = g.Save();
        float shx = c.Shake > 0 ? (float)Math.Sin(tick * 2.1) * U(0.6f) : 0;
        g.TranslateTransform(c.X + shx, Ground - c.Y);
        g.ScaleTransform(c.Dir * u * c.Scale, u * c.Scale);
        if (c.Lying) { g.TranslateTransform(0, -4.2f); g.RotateTransform(90); }
        if (low) g.TranslateTransform(0, 3.5f);

        int view = (c.Lying || low) ? 1 : c.View; curView = view;
        Color fur = c.MainC, belly = c.BellyC, shadeC = Dark(fur, 0.84f);
        Color ink = Color.FromArgb(34, 26, 28);
        int o = c.Outfit; Color c1 = c.C1, c2 = c.C2;
        int pants = PantsType(o);
        Color pantsC = (o == 3) ? c1 : (o == 6 ? c1 : c2);
        bool walking = (c.Walking || c.ZMove) && !c.Jumping && !c.Stiff && !low && !c.Lying;
        int emo = c.AngryT > 0 ? 2 : (c.SweatT > 0 || c.Shake > 0 ? 3 : (c.HappyT > 0 || (c == me && musicT > 0) ? 1 : 0));
        float ph = (tick + c.Id * 10) * 0.42f;
        float sw = walking ? (float)Math.Sin(ph) : 0;
        float bob = walking ? Math.Abs((float)Math.Sin(ph)) * 3f : (float)Math.Sin(tick * 0.07f + c.Id) * 0.8f + 0.8f;
        float tw = (float)Math.Sin((tick + c.Id * 37) * (walking ? 0.2f : (c.HappyT > 0 ? 0.32f : 0.075f))) * (c.AngryT > 0 ? 1.6f : 1f) + (walking ? 0.4f : 0);
        bool blink = c.PuffT > 55 || (c.BlinkT > 0 && (c.BlinkT <= 6 ? true : (c.BlinkT > 13 || (c.BlinkT > 6 && c.BlinkT <= 9))));
        bool umb = WantsUmbrella(c), hold = c.Hold != ' ';
        Color armC = HasSleeves(o) ? c1 : fur, armD = Dark(armC, 0.84f);
        Color legC = pants == 2 ? pantsC : fur, legD = Dark(legC, 0.84f);
        bool reach = view == 2 && c == me && !c.Walking && !c.ZMove && !hold && ShopScene;
        if (o == 22) DrawCape(g, c);

        GraphicsState ks = g.Save();
        float sx = 1, sy = 1;
        if (!c.Lying)
        {
            float br = (float)Math.Sin(tick * 0.085f + c.Id);
            if (!walking && !c.Jumping) { sy += 0.018f * br; sx -= 0.012f * br; }
            if (walking) { float contact = 1 - Math.Abs((float)Math.Sin(ph)); sy -= 0.05f * contact; sx += 0.04f * contact; }
            if (c.Jumping) { float jt = Math.Min(1f, c.JT / (float)Math.Max(1, c.JDur)); float st2 = Math.Abs((float)Math.Cos(Math.PI * jt)); sy += 0.11f * st2; sx -= 0.07f * st2; }
            if (c.LandT > 0) { float lk = c.LandT / 8f; lk = lk * lk; sy -= 0.17f * lk; sx += 0.13f * lk; }
            if (c.HappyT > 0) { float hk = (float)Math.Sin(c.HappyT * 0.6f); sy += 0.05f * hk; sx -= 0.035f * hk; }
            if (c.StompT > 0) { float hk = Math.Abs((float)Math.Sin(c.StompT * 0.42f)); sy -= 0.07f * (1 - hk); sx += 0.05f * (1 - hk); }
            if (c.TurnT > 0) sx *= 1 - 0.32f * (float)Math.Sin(Math.PI * (1 - c.TurnT / 6f));
        }
        g.ScaleTransform(sx, sy);
        float headY = walking ? (float)Math.Sin(ph * 2 - 1.3f) * 2.2f : 0;
        float headR = c.Lying || low ? 0 : (walking ? (float)Math.Sin(ph) * 2.6f
            : (c.HappyT > 0 ? (float)Math.Sin(tick * 0.28f) * 8f
            : (c.AngryT > 0 ? (float)Math.Sin(tick * 0.9f) * 3f
            : (c.Shake > 0 || c.SweatT > 0 ? (float)Math.Sin(tick * 1.7f) * 2.5f
            : (float)Math.Sin((tick + c.Id * 41) * 0.022f) * 3.5f))));
        if (view == 2) headR *= 0.5f;
        g.ScaleTransform(NK, NK);
        using (Pen pen = new Pen(ink, 3.3f))
        using (Pen thin = new Pen(ink, 1.8f))
        {
            pen.LineJoin = LineJoin.Round; pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; thin.StartCap = LineCap.Round; thin.EndCap = LineCap.Round;
            Color sh = Color.FromArgb(150, shadeC), ash = Color.FromArgb(150, armD), lsh = Color.FromArgb(150, legD);
            float ll = Math.Max(0, sw) * 5, lr = Math.Max(0, -sw) * 5;
            if (view == 0)
            {
                NTail(g, c, ink, 16, -20, 1, tw);
                NBlob(g, RoundRect(-16, -17 - ll, 13, 17, 5.5f), legC, lsh, pen); NBlob(g, RoundRect(3, -17 - lr, 13, 17, 5.5f), legC, lsh, pen); NToes(g, c, thin, -9.5f, -ll); NToes(g, c, thin, 9.5f, -lr);
                g.TranslateTransform(0, -bob);
                bool pocket = c.Outfit == 1 && !walking && !hold && !umb && c.HappyT == 0 && !c.Cuffed && c.AngryT == 0;
                if (pocket) { NBlob(g, NEll(-27, -48, 12, 22), armC, ash, pen); NBlob(g, NEll(15, -48, 12, 22), armC, ash, pen); }
                else if (!c.Cuffed) NBlob(g, NEll(-30, -47 + sw * 2, 12, 21), armC, ash, pen);
                NBlob(g, NEll(-22, -52, 44, 43), fur, sh, pen);
                using (SolidBrush b = new SolidBrush(belly)) g.FillEllipse(b, -13, -44, 26, 31);
                NOutfit(g, c, pen, -22, -52, 44, 43);
                GraphicsState hs = g.Save(); g.TranslateTransform(0, -50 + headY); g.RotateTransform(headR); g.ScaleTransform(1.1f, 1.1f); g.TranslateTransform(0, 50); NEars(g, c, pen, 0); NBlob(g, NEll(-31, -97, 62, 50), fur, sh, pen); NStripes(g, c, 0);
                NFace(g, c, thin, 0, emo, blink);
                NHead(g, c, 0); g.Restore(hs);
                if (c.Cuffed) { }
                else if (umb || c.HappyT > 0) NBlob(g, NEll(18, -72 - (c.HappyT > 0 ? Math.Abs((float)Math.Sin(tick * 0.5)) * 4 : 0), 12, 25), armC, ash, pen);
                else if (hold) NBlob(g, NEll(17, -47, 13, 18), armC, ash, pen);
                else if (!pocket) NBlob(g, NEll(18, -47 - sw * 2, 12, 21), armC, ash, pen);
            }
            else if (view == 2)
            {
                NBlob(g, RoundRect(-16, -17 - ll, 13, 17, 5.5f), legC, lsh, pen); NBlob(g, RoundRect(3, -17 - lr, 13, 17, 5.5f), legC, lsh, pen); NToes(g, c, thin, -9.5f, -ll); NToes(g, c, thin, 9.5f, -lr);
                g.TranslateTransform(0, -bob);
                NBlob(g, NEll(-30, -47 + sw * 2, 12, 21), armC, ash, pen);
                if (reach) NBlob(g, NEll(18, -76, 12, 27), armC, ash, pen); else NBlob(g, NEll(18, -47 - sw * 2, 12, 21), armC, ash, pen);
                NBlob(g, NEll(-22, -52, 44, 43), fur, sh, pen);
                if (c.Stripes && o == 0) using (Pen sp = new Pen(shadeC, 3.2f)) { sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round; for (int i = -1; i <= 1; i++) g.DrawLine(sp, -9, -36 + i * 8, 9, -36 + i * 8); }
                NOutfit(g, c, pen, -22, -52, 44, 43);
                GraphicsState hs = g.Save(); g.TranslateTransform(0, -50 + headY); g.RotateTransform(headR); g.ScaleTransform(1.1f, 1.1f); g.TranslateTransform(0, 50); NEars(g, c, pen, 0); NBlob(g, NEll(-31, -97, 62, 50), fur, sh, pen); NStripes(g, c, 0);
                if (c.Stripes && !c.Bald) using (Pen sp = new Pen(shadeC, 3.2f)) { sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round; g.DrawLine(sp, -8, -72, 8, -72); g.DrawLine(sp, -6, -64, 6, -64); }
                NHead(g, c, 0); g.Restore(hs);
                NTail(g, c, ink, 0, -16, tw * 0.35f, tw);
            }
            else
            {
                NTail(g, c, ink, -14, -20, -1, tw);
                if (!c.Cuffed) { GraphicsState a1 = g.Save(); g.TranslateTransform(10, -45); g.RotateTransform(sw * 22); g.TranslateTransform(-10, 45); NBlob(g, NEll(4, -47, 12, 21), armD, Color.Empty, pen); g.Restore(a1); }
                if (low)
                {
                    NBlob(g, RoundRect(0, -24, 27, 12, 5.5f), legD, Color.Empty, pen); NBlob(g, NEll(22, -27, 9, 15), Dark(belly, 0.9f), Color.Empty, pen);
                    NBlob(g, RoundRect(3, -19, 27, 12, 5.5f), legC, lsh, pen); NBlob(g, NEll(25, -22, 9, 15), belly, Color.Empty, pen);
                }
                else
                {
                    NBlob(g, RoundRect(-3 - sw * 8, -17 - lr * 1.3f, 13, 17, 5.5f), legD, Color.Empty, pen); NBlob(g, RoundRect(-9 + sw * 8, -17 - ll * 1.3f, 13, 17, 5.5f), legC, lsh, pen);
                }
                g.TranslateTransform(0, -bob);
                NBlob(g, NEll(-19, -52, 40, 43), fur, sh, pen);
                using (SolidBrush b = new SolidBrush(belly)) g.FillEllipse(b, -2, -44, 20, 31);
                NOutfit(g, c, pen, -19, -52, 40, 43);
                GraphicsState hs = g.Save(); g.TranslateTransform(0, -50 + headY); g.RotateTransform(headR); g.ScaleTransform(1.1f, 1.1f); g.TranslateTransform(0, 50); NEars(g, c, pen, 5); NBlob(g, NEll(-26, -97, 62, 50), fur, sh, pen); NStripes(g, c, 3);
                NFace(g, c, thin, 11, emo, blink);
                NHead(g, c, 5); g.Restore(hs);
                if (c.Cuffed) NBlob(g, NEll(-27, -45, 12, 18), armC, ash, pen);
                else if (umb) NBlob(g, NEll(6, -72, 12, 27), armC, ash, pen);
                else if (hold) NBlob(g, NEll(7, -45, 17, 11.5f), armC, ash, pen);
                else if (c.HappyT > 0) NBlob(g, NEll(13, -73 - Math.Abs((float)Math.Sin(tick * 0.5)) * 4, 12, 26), armC, ash, pen);
                else if (c.AngryT > 0) NBlob(g, NEll(9, -51 + (float)Math.Sin(tick * 0.8) * 3, 19, 11.5f), armC, ash, pen);
                else { GraphicsState a2 = g.Save(); g.TranslateTransform(2, -45); g.RotateTransform(-sw * 22); g.TranslateTransform(-2, 45); NBlob(g, NEll(-4, -47, 12, 21), armC, ash, pen); g.Restore(a2); }
            }
        }
        g.Restore(ks);
        if (umb) { g.TranslateTransform(0, -bob * NK); DrawUmbrella(g, c); }
        g.Restore(st);

        if (c.Outfit == 11 && !c.Lying && fntTiny != null)
        {
            PointF tp = Local(c, 0.3f, -9.6f);
            using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString("ОМОН", fntTiny, b, tp.X, tp.Y, sf); }
        }

        if (c.AngryT > 0)
        {
            PointF ap = Local(c, 5.0f, -24.5f);
            float r = U(1.3f) * c.Scale * (1 + 0.15f * (float)Math.Sin(tick * 0.6));
            using (Pen p = new Pen(Color.FromArgb(235, 40, 50), Math.Max(1.2f, U(0.55f))))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                for (int q = 0; q < 4; q++)
                {
                    float ang = q * 90 + 45;
                    float cx = ap.X + (float)Math.Cos(ang * Math.PI / 180) * r, cy = ap.Y + (float)Math.Sin(ang * Math.PI / 180) * r;
                    g.DrawArc(p, cx - r * 0.7f, cy - r * 0.7f, r * 1.4f, r * 1.4f, ang + 90, 90);
                }
            }
        }
        if (c.SweatT > 0)
        {
            PointF sp = Local(c, -3.4f, -21.5f);
            float r = U(0.9f) * c.Scale;
            float drop = (60 - (c.SweatT % 60)) * 0.04f * u;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(sp.X, sp.Y - r * 1.8f + drop, sp.X + r * 1.1f, sp.Y + drop, sp.X + r, sp.Y + r + drop, sp.X, sp.Y + r + drop);
                p.AddBezier(sp.X, sp.Y + r + drop, sp.X - r, sp.Y + r + drop, sp.X - r * 1.1f, sp.Y + drop, sp.X, sp.Y - r * 1.8f + drop);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 120, 190, 255))) g.FillPath(b, p);
            }
        }

        if (c.Hold != ' ')
        {
            PointF pw = Paw(c);
            float hb = (c.Hold == 'J' && tick % 12 < 6) ? U(0.4f) : 0;
            DrawItem(g, c.Hold, c.HoldCol, pw.X, pw.Y + hb, 255, (c.Hold == 'W' || c.Hold == 'A' || c.Hold == 'Y') ? c.ItemRot * c.Dir : 0);
        }
    }

    static PointF Lerp(PointF a, PointF b, float t) { return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }

    void DrawEye(Graphics g, Cat c, float x, float y, float w, float h, bool blink)
    {
        Color dk = Color.FromArgb(25, 22, 30);
        if (blink)
        {
            using (Pen ep = new Pen(dk, 0.45f)) { ep.StartCap = LineCap.Round; ep.EndCap = LineCap.Round; g.DrawArc(ep, x, y + h * 0.35f, w, h * 0.5f, 20, 140); }
            return;
        }
        RectangleF er = new RectangleF(x, y, w, h);
        using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, er);
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y - 0.1f, w, h + 0.2f), Light(c.IrisC, 0.35f), Dark(c.IrisC, 0.5f), 90f))
            g.FillEllipse(b, x + w * 0.1f, y + h * 0.12f, w * 0.8f, h * 0.8f);
        using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, x + w * 0.25f, y + h * 0.2f, w * 0.5f, h * 0.62f);
        using (SolidBrush b = new SolidBrush(Color.White))
        {
            g.FillEllipse(b, x + w * 0.45f, y + h * 0.14f, w * 0.38f, w * 0.38f);
            g.FillEllipse(b, x + w * 0.2f, y + h * 0.66f, w * 0.18f, w * 0.18f);
        }
    }

    void DrawOutfitTorso(Graphics g, Cat c, RectangleF br)
    {
        int o = c.Outfit;
        if (o == 0) return;
        if (o >= 14) { using (Pen op0 = new Pen(Dark(c.C1, 0.55f), 0.35f)) { op0.LineJoin = LineJoin.Round; DrawCostumeTorso(g, c, o, c.C1, c.C2, op0); } return; }
        Color c1 = c.C1, c2 = c.C2, ln = Dark(c1, 0.55f);
        using (Pen op = new Pen(ln, 0.35f))
        {
            op.LineJoin = LineJoin.Round;
            if (o == 1 || o == 9 || o == 4 || o == 3 || o == 2 || o == 7)
            {
                float bottom = (o == 2 || o == 7) ? -5.6f : -4.6f;
                using (GraphicsPath p = RoundRect(-4.1f, -12.9f, 8.2f, bottom + 12.9f, 3.2f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.2f, -13f, 8.4f, 9f), Dark(c1, 0.82f), Light(c1, 0.12f), 0f)) g.FillPath(b, p);
                    GraphicsState cs = g.Save();
                    g.SetClip(p, CombineMode.Intersect);
                    if (o == 7) // гавайка: цветочки
                        for (int i = 0; i < 9; i++)
                        {
                            float fx = -3.6f + (i * 37 % 70) / 10f, fy = -12.4f + (i * 53 % 66) / 10f;
                            using (SolidBrush b = new SolidBrush(i % 2 == 0 ? c2 : Color.FromArgb(255, 245, 230))) g.FillEllipse(b, fx, fy, 1.2f, 1.2f);
                        }
                    if (o == 4) // две полосы спортивки
                        using (SolidBrush b = new SolidBrush(c2)) { g.FillRectangle(b, 1.6f, -13f, 0.45f, 9f); g.FillRectangle(b, 2.4f, -13f, 0.45f, 9f); }
                    if (o == 1) // карман худи
                        using (GraphicsPath pk = RoundRect(-1.0f, -8.4f, 4.6f, 2.6f, 0.8f)) { using (SolidBrush b = new SolidBrush(Dark(c1, 0.88f))) g.FillPath(b, pk); g.DrawPath(op, pk); }
                    if (o == 9) // кожанка: молния
                        using (Pen zp = new Pen(Color.FromArgb(200, 200, 210), 0.25f)) g.DrawLine(zp, 2.2f, -12.6f, 2.0f, -4.6f);
                    if (o == 3) // пиджак: рубашка и галстук
                    {
                        using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new PointF[] { P(0.6f, -13f), P(4.2f, -13f), P(2.6f, -8.6f) });
                        using (SolidBrush b = new SolidBrush(c2)) g.FillPolygon(b, new PointF[] { P(2.1f, -12.6f), P(2.9f, -12.6f), P(2.8f, -9.6f), P(2.5f, -9.0f), P(2.2f, -9.6f) });
                    }
                    g.Restore(cs);
                    g.DrawPath(op, p);
                }
                if (o == 1) // капюшон и шнурки
                {
                    using (GraphicsPath hp = new GraphicsPath())
                    {
                        hp.AddArc(-4.4f, -14.6f, 6.4f, 3.6f, 180, 180);
                        using (SolidBrush b = new SolidBrush(Dark(c1, 0.75f))) g.FillPath(b, hp);
                    }
                    using (Pen sp = new Pen(Color.White, 0.22f)) { g.DrawLine(sp, 2.2f, -12.4f, 2.0f, -10.2f); g.DrawLine(sp, 3.0f, -12.4f, 3.1f, -10.4f); }
                }
                if (o == 2 || o == 7) // ремень/шорты верх
                    using (SolidBrush b = new SolidBrush(Dark(c2, 0.7f))) g.FillRectangle(b, -3.6f, -5.8f, 7.2f, 0.7f);
            }
            if (o == 10 || o == 11)
            {
                using (GraphicsPath p = RoundRect(-4.1f, -12.9f, 8.2f, 8.3f, 3.2f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.2f, -13f, 8.4f, 9f), Dark(c1, 0.85f), Light(c1, 0.1f), 0f)) g.FillPath(b, p);
                    GraphicsState cs = g.Save();
                    g.SetClip(p, CombineMode.Intersect);
                    if (o == 10)
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 46)))
                            for (int i = 0; i < 5; i++) g.FillRectangle(b, -4.2f, -12.6f + i * 1.7f, 8.4f, 0.8f);
                    else
                    {
                        using (SolidBrush b = new SolidBrush(Light(c1, 0.12f))) { g.FillRectangle(b, -3.0f, -8.4f, 2.6f, 2.2f); g.FillRectangle(b, 0.8f, -8.4f, 2.6f, 2.2f); }
                        using (SolidBrush b = new SolidBrush(Dark(c1, 0.7f))) g.FillRectangle(b, -4.2f, -6.0f, 8.4f, 0.8f);
                    }
                    g.Restore(cs);
                    g.DrawPath(op, p);
                }
                if (o == 10) using (SolidBrush b = new SolidBrush(Color.White)) { g.FillRectangle(b, 1.2f, -11.4f, 2.2f, 1.2f); }
            }
            if (o == 12) // длинный чёрный плащ
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-3.8f, -13.0f), P(3.9f, -13.0f), P(4.6f, -8.0f), P(5.6f, -0.8f), P(-5.2f, -0.8f), P(-4.5f, -8.0f) });
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-5.3f, -13.1f, 11f, 12.5f), Color.FromArgb(48, 48, 56), Color.FromArgb(14, 14, 18), 0f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(5, 5, 8), 0.35f)) g.DrawPath(pp, p);
                }
                using (Pen sp = new Pen(Color.FromArgb(90, 90, 104), 0.25f))
                {
                    g.DrawLine(sp, 2.2f, -12.6f, 1.2f, -0.9f);
                    g.DrawLine(sp, 1.0f, -13.0f, 2.4f, -10.4f); g.DrawLine(sp, 3.8f, -12.8f, 2.6f, -10.4f);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillRectangle(b, -3.2f, -12.4f, 0.6f, 10f);
            }
            if (o == 13) // толстый вязаный свитер с горлом
            {
                using (GraphicsPath p = RoundRect(-4.3f, -13.2f, 8.6f, 8.8f, 3.0f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.4f, -13.3f, 8.8f, 9f), Dark(c1, 0.82f), Light(c1, 0.12f), 0f)) g.FillPath(b, p);
                    GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                    using (Pen rp = new Pen(Color.FromArgb(70, Dark(c1, 0.5f)), 0.22f))
                        for (int i = 0; i < 9; i++) { float x = -4.0f + i * 1.0f; g.DrawLine(rp, x, -12.6f, x + 0.2f, -4.6f); }
                    using (Pen zp = new Pen(Color.FromArgb(80, Dark(c1, 0.55f)), 0.3f))
                        for (int i = 0; i < 4; i++) { float y = -10.6f + i * 1.6f; g.DrawLines(zp, new PointF[] { P(-4f, y), P(-2.6f, y - 0.5f), P(-1.2f, y), P(0.2f, y - 0.5f), P(1.6f, y), P(3.0f, y - 0.5f), P(4.4f, y) }); }
                    g.Restore(cs);
                    using (Pen pp = new Pen(Dark(c1, 0.55f), 0.35f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath p = RoundRect(-2.4f, -14.2f, 5.6f, 2.0f, 0.9f))
                {
                    using (SolidBrush b = new SolidBrush(Dark(c1, 0.9f))) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(c1, 0.55f), 0.3f)) g.DrawPath(pp, p);
                }
            }
            if (o == 5) // футболка + фартук продавца
            {
                using (GraphicsPath p = RoundRect(-4.1f, -12.9f, 8.2f, 7.4f, 3.2f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(245, 245, 248))) { g.FillPath(b, p); }
                using (GraphicsPath p = RoundRect(-0.6f, -12.0f, 4.6f, 8.6f, 0.8f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-0.7f, -12.1f, 4.8f, 8.8f), Light(c1, 0.12f), Dark(c1, 0.8f), 90f)) g.FillPath(b, p);
                    g.DrawPath(op, p);
                }
                using (Pen sp = new Pen(Dark(c1, 0.7f), 0.3f)) { g.DrawLine(sp, -0.4f, -11.6f, -2.6f, -13.0f); g.DrawLine(sp, -0.6f, -8.6f, -3.6f, -8.4f); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 80))) g.FillRectangle(b, 0.4f, -11.2f, 2.2f, 1.2f);
                using (Pen sp = new Pen(Color.FromArgb(120, 0, 0, 0), 0.15f)) g.DrawLine(sp, 0.7f, -10.6f, 2.3f, -10.6f);
                using (SolidBrush b = new SolidBrush(Dark(c1, 0.85f))) g.FillRectangle(b, 0.2f, -7.6f, 3.4f, 1.6f);
            }
            if (o == 6) // рабочий комбинезон
            {
                using (GraphicsPath p = RoundRect(-3.0f, -10.4f, 6.8f, 6.2f, 1.4f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.1f, -10.5f, 7f, 6.4f), Light(c1, 0.1f), Dark(c1, 0.8f), 90f)) g.FillPath(b, p);
                    g.DrawPath(op, p);
                }
                using (Pen sp = new Pen(c1, 0.8f)) { g.DrawLine(sp, -1.6f, -10.2f, -1.2f, -13.0f); g.DrawLine(sp, 2.6f, -10.2f, 2.4f, -13.0f); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 210, 60))) { g.FillEllipse(b, -1.7f, -10.4f, 0.7f, 0.7f); g.FillEllipse(b, 2.3f, -10.4f, 0.7f, 0.7f); }
                using (SolidBrush b = new SolidBrush(Dark(c1, 0.85f))) g.FillRectangle(b, -0.2f, -9.2f, 2.6f, 1.8f);
            }
            if (o == 8) // сарафан
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-2.6f, -12.6f), P(3.4f, -12.6f), P(5.0f, -3.4f), P(-4.4f, -3.4f) });
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.5f, -12.7f, 9.6f, 9.4f), Light(c1, 0.15f), Dark(c1, 0.8f), 90f)) g.FillPath(b, p);
                    g.DrawPath(op, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                    for (int i = 0; i < 5; i++) g.FillEllipse(b, -3.2f + i * 1.8f, -5.2f - (i % 2) * 2.4f, 0.8f, 0.8f);
            }
        }
    }

    void DrawHat(Graphics g, Cat c)
    {
        if (c.Hat >= 7 && c.Hat <= 16) { DrawCostumeHat(g, c); return; }
        switch (c.Hat)
        {
            case 1: // кепка (козырьком назад если Seed чётный)
            {
                bool back = c.HatBack;
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-3.8f, -24.0f, 9.4f, 6.2f, 180, 180);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.9f, -24.1f, 9.6f, 3.4f), Light(c.HatC, 0.2f), Dark(c.HatC, 0.8f), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(c.HatC, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath v = RoundRect(back ? -7.0f : 4.4f, -21.4f, 3.6f, 1.0f, 0.5f))
                using (SolidBrush b = new SolidBrush(Dark(c.HatC, 0.75f))) g.FillPath(b, v);
                using (SolidBrush b = new SolidBrush(Dark(c.HatC, 0.6f))) g.FillEllipse(b, 0.4f, -24.4f, 0.8f, 0.6f);
                break;
            }
            case 2: // шапка-бини с помпоном
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-3.9f, -25.0f, 9.6f, 7.4f, 180, 180);
                    p.CloseFigure();
                    using (SolidBrush b = new SolidBrush(c.HatC)) g.FillPath(b, p);
                }
                using (GraphicsPath p = RoundRect(-4.2f, -22.0f, 10.2f, 1.6f, 0.7f)) using (SolidBrush b = new SolidBrush(Dark(c.HatC, 0.8f))) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(60, 0, 0, 0), 0.2f)) for (int i = 0; i < 6; i++) g.DrawLine(pp, -3.4f + i * 1.7f, -22.0f, -3.4f + i * 1.7f, -20.4f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 250, 250))) g.FillEllipse(b, -0.1f, -26.4f, 2.2f, 2.2f);
                break;
            case 3: // праздничный колпак
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-1.6f, -21.6f), P(3.6f, -21.6f), P(1.4f, -28.4f) });
                    using (SolidBrush b = new SolidBrush(c.HatC)) g.FillPath(b, p);
                    GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                    using (Pen sp = new Pen(Color.FromArgb(230, 255, 255, 255), 0.6f)) for (int i = 0; i < 4; i++) g.DrawLine(sp, -2f, -22f - i * 1.8f, 5f, -23.5f - i * 1.8f);
                    g.Restore(cs);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 60))) g.FillEllipse(b, 0.7f, -29.2f, 1.5f, 1.5f);
                break;
            case 4: // строительная каска
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-3.9f, -25.0f, 9.8f, 7.6f, 180, 180);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4f, -25.1f, 10f, 4f), Color.FromArgb(255, 230, 90), Color.FromArgb(230, 170, 20), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(160, 110, 10), 0.3f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath p = RoundRect(-4.8f, -21.6f, 11.6f, 1.0f, 0.5f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 180, 30))) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillRectangle(b, 0.6f, -24.8f, 0.9f, 3.0f);
                break;
            case 6: // шлем ОМОН с забралом
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-4.4f, -25.4f, 10.8f, 8.6f, 180, 180);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.5f, -25.5f, 11f, 4.6f), Light(c.HatC, 0.25f), Dark(c.HatC, 0.8f), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(c.HatC, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath p = RoundRect(-0.6f, -21.4f, 7.4f, 5.2f, 1.6f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 150, 185, 220))) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(160, 60, 70, 90), 0.25f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 255, 255, 255))) g.FillRectangle(b, 3.6f, -20.8f, 0.6f, 3.4f);
                using (Pen pp = new Pen(Color.FromArgb(30, 30, 36), 0.3f)) g.DrawLine(pp, -3.4f, -21.0f, -1.8f, -14.4f);
                break;
            case 5: // бантик на ухе
                using (SolidBrush b = new SolidBrush(c.HatC))
                {
                    g.FillPolygon(b, new PointF[] { P(4.2f, -22.6f), P(2.6f, -23.8f), P(2.6f, -21.4f) });
                    g.FillPolygon(b, new PointF[] { P(4.2f, -22.6f), P(5.8f, -23.8f), P(5.8f, -21.4f) });
                    g.FillEllipse(b, 3.7f, -23.1f, 1.0f, 1.0f);
                }
                break;
        }
    }

    // ---------- предметы ----------
    void DrawItem(Graphics g, char kind, char col, float cx, float cy, int alpha = 255, float rot = 0)
    {
        Color c = pal.ContainsKey(col) ? pal[col] : pal['R'];
        GraphicsState st = g.Save();
        g.TranslateTransform(cx, cy);
        if (rot != 0) g.RotateTransform(rot);
        g.ScaleTransform(u, u);
        using (Pen op = new Pen(Alpha(Mix(Dark(c, 0.45f), InkC, 0.65f), alpha), 0.4f))
        using (Pen dk = new Pen(Alpha(InkC, alpha), 0.4f))
        {
            switch (kind)
            {
                case 'B': // флакон
                    using (GraphicsPath p = RoundRect(-0.72f, -3.2f, 1.44f, 1.9f, 0.3f))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 74, 62, 64))) g.FillPath(b, p);
                        g.DrawPath(dk, p);
                    }
                    using (GraphicsPath p = RoundRect(-1.25f, -1.6f, 2.5f, 4.8f, 0.7f))
                    {
                        using (SolidBrush b = new SolidBrush(Alpha(c, alpha))) g.FillPath(b, p);
                        GraphicsState bs = g.Save();
                        g.SetClip(p, CombineMode.Intersect);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha * 70 / 255, 70, 30, 20))) g.FillRectangle(b, 0.35f, -1.7f, 1f, 5f);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha * 240 / 255, 255, 251, 238))) g.FillRectangle(b, -1.3f, 0.2f, 2.6f, 1.6f);
                        g.Restore(bs);
                        g.DrawPath(dk, p);
                    }
                    break;
                case 'D': // диск
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-2f, -2f, 4f, 4f), Color.FromArgb(235, 240, 250), Color.FromArgb(150, 190, 255), 45f))
                        g.FillEllipse(b, -1.9f, -1.9f, 3.8f, 3.8f);
                    using (Pen p = new Pen(Color.FromArgb(120, 130, 150), 0.25f)) g.DrawEllipse(p, -1.9f, -1.9f, 3.8f, 3.8f);
                    using (Pen p = new Pen(Color.FromArgb(120, 255, 160, 220), 0.25f)) g.DrawArc(p, -1.3f, -1.3f, 2.6f, 2.6f, 200, 100);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 205, 215))) g.FillEllipse(b, -0.7f, -0.7f, 1.4f, 1.4f);
                    using (SolidBrush b = new SolidBrush(pal['T'])) g.FillEllipse(b, -0.3f, -0.3f, 0.6f, 0.6f);
                    break;
                case 'X': // пачка табака
                    using (GraphicsPath p = RoundRect(-1.45f, -1.75f, 2.9f, 3.5f, 0.4f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.5f, -1.8f, 3f, 3.6f), Light(c, 0.2f), Dark(c, 0.82f), 90f)) g.FillPath(b, p);
                        g.DrawPath(op, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 255, 255))) g.FillEllipse(b, -1.0f, -0.55f, 2.0f, 1.1f);
                    using (Pen p = new Pen(Color.FromArgb(120, 0, 0, 0), 0.2f)) g.DrawLine(p, -1.3f, -1.1f, 1.3f, -1.1f);
                    break;
                case 'J': // геймпад
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddEllipse(-2.7f, -1.0f, 2.4f, 2.4f);
                        p.AddEllipse(0.3f, -1.0f, 2.4f, 2.4f);
                        p.AddRectangle(new RectangleF(-1.6f, -1.1f, 3.2f, 1.8f));
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 64, 78))) g.FillPath(b, p);
                    }
                    using (SolidBrush b = new SolidBrush(pal['R'])) g.FillEllipse(b, 1.1f, -0.7f, 0.6f, 0.6f);
                    using (SolidBrush b = new SolidBrush(pal['U'])) g.FillEllipse(b, 1.6f, -0.2f, 0.6f, 0.6f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 185, 195))) { g.FillRectangle(b, -1.9f, -0.5f, 1.2f, 0.4f); g.FillRectangle(b, -1.5f, -0.9f, 0.4f, 1.2f); }
                    break;
                case 'C': // коробка с игрой
                    using (GraphicsPath p = RoundRect(-1.4f, -1.9f, 2.8f, 3.8f, 0.3f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.5f, -2f, 3f, 4f), Color.FromArgb(70, 130, 240), Color.FromArgb(30, 70, 170), 90f)) g.FillPath(b, p);
                        g.DrawPath(dk, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 250, 210, 80))) g.FillRectangle(b, -1.0f, -0.6f, 2.0f, 1.2f);
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, -1.4f, -1.9f, 2.8f, 0.5f);
                    break;
                case 'V': // под-система
                    using (GraphicsPath p = RoundRect(-1.0f, -2.2f, 2.0f, 4.4f, 0.7f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.1f, -2.3f, 2.2f, 4.6f), Color.FromArgb(150, 90, 220), Color.FromArgb(70, 40, 120), 0f)) g.FillPath(b, p);
                        g.DrawPath(dk, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 50))) g.FillRectangle(b, -0.5f, -2.9f, 1.0f, 0.8f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255))) g.FillEllipse(b, -0.3f, 1.1f, 0.6f, 0.6f);
                    break;
                case 'Z': // заглушка
                    using (GraphicsPath p = RoundRect(-0.8f, -0.6f, 1.6f, 1.2f, 0.4f))
                    using (SolidBrush b = new SolidBrush(c)) { g.FillPath(b, p); g.DrawPath(op, p); }
                    break;
                case 'I': // ID-карта
                    using (GraphicsPath p = RoundRect(-2.0f, -1.3f, 4.0f, 2.6f, 0.35f))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(225, 240, 255))) g.FillPath(b, p);
                        using (Pen pp = new Pen(Color.FromArgb(80, 110, 160), 0.2f)) g.DrawPath(pp, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 170, 90))) g.FillRectangle(b, -1.6f, -0.8f, 1.2f, 1.5f);
                    using (Pen pp = new Pen(Color.FromArgb(120, 130, 160), 0.2f)) { g.DrawLine(pp, 0, -0.5f, 1.6f, -0.5f); g.DrawLine(pp, 0, 0.1f, 1.6f, 0.1f); g.DrawLine(pp, 0, 0.7f, 1.1f, 0.7f); }
                    break;
                case 'O': // монетка
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.5f, -1.5f, 3f, 3f), Color.FromArgb(255, 230, 120), Color.FromArgb(220, 160, 30), 45f))
                        g.FillEllipse(b, -1.4f, -1.4f, 2.8f, 2.8f);
                    using (Pen pp = new Pen(Color.FromArgb(170, 110, 20), 0.25f)) { g.DrawEllipse(pp, -1.4f, -1.4f, 2.8f, 2.8f); g.DrawEllipse(pp, -0.9f, -0.9f, 1.8f, 1.8f); }
                    break;
                case 'T': // планшет
                    using (GraphicsPath p = RoundRect(-2.4f, -1.7f, 4.8f, 3.4f, 0.4f))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 42, 50))) g.FillPath(b, p);
                    using (SolidBrush b = new SolidBrush(countShelf >= 0 && tick % 10 < 6 ? Color.FromArgb(150, 230, 160) : Color.FromArgb(120, 190, 255))) g.FillRectangle(b, -2.0f, -1.3f, 4.0f, 2.6f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 255, 255, 255))) { g.FillRectangle(b, -1.6f, -0.9f, 2.4f, 0.4f); g.FillRectangle(b, -1.6f, -0.2f, 3.0f, 0.4f); g.FillRectangle(b, -1.6f, 0.5f, 1.8f, 0.4f); }
                    break;
                case 'Q': // ведро попкорна
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddPolygon(new PointF[] { new PointF(-2.2f, -1.6f), new PointF(2.2f, -1.6f), new PointF(1.6f, 2.6f), new PointF(-1.6f, 2.6f) });
                        using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
                        GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 50, 60))) for (int i = 0; i < 3; i++) g.FillRectangle(b, -2.0f + i * 1.5f, -1.6f, 0.75f, 4.4f);
                        g.Restore(cs);
                        using (Pen pp = new Pen(Color.FromArgb(150, 40, 40), 0.2f)) g.DrawPath(pp, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 245, 210)))
                        for (int i = 0; i < 6; i++) g.FillEllipse(b, -2.2f + i * 0.75f, -2.6f - (i % 2) * 0.5f, 1.1f, 1.1f);
                    break;
                case 'G': // касса (переносная)
                    using (GraphicsPath p = RoundRect(-3.4f, -2.0f, 6.8f, 4.0f, 0.6f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.5f, -2.1f, 7f, 4.2f), Color.FromArgb(120, 126, 142), Color.FromArgb(70, 74, 88), 90f)) g.FillPath(b, p);
                        using (Pen pp = new Pen(Color.FromArgb(40, 42, 50), 0.25f)) g.DrawPath(pp, p);
                    }
                    using (SolidBrush b = new SolidBrush(registerDown ? Color.FromArgb(40, 60, 50) : Color.FromArgb(110, 230, 150))) g.FillRectangle(b, -2.6f, -1.4f, 3.0f, 1.4f);
                    if (registerDown) using (Pen cp = new Pen(Color.White, 0.15f)) { g.DrawLine(cp, -2.4f, -1.3f, -1.0f, -0.2f); g.DrawLine(cp, -1.0f, -0.2f, 0.2f, -1.2f); }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 205, 215))) for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) g.FillRectangle(b, 1.0f + i * 0.9f, -1.4f + j * 0.9f, 0.6f, 0.6f);
                    break;
                case 'F': // телефон
                    using (GraphicsPath p = RoundRect(-0.9f, -1.6f, 1.8f, 3.2f, 0.4f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 30, 36))) g.FillPath(b, p);
                    using (SolidBrush b = new SolidBrush(tick % 30 < 25 ? Color.FromArgb(150, 210, 255) : Color.FromArgb(200, 240, 255))) g.FillRectangle(b, -0.7f, -1.3f, 1.4f, 2.4f);
                    break;
                case 'A': // молоток
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 100, 60))) g.FillRectangle(b, -0.25f, -3.0f, 0.5f, 5.2f);
                    using (GraphicsPath p = RoundRect(-1.6f, -4.0f, 3.2f, 1.4f, 0.3f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 125, 140))) g.FillPath(b, p);
                    break;
                case 'K': // коробка
                    using (GraphicsPath p = RoundRect(-2.4f, -2.0f, 4.8f, 4.0f, 0.3f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-2.5f, -2.1f, 5f, 4.2f), Color.FromArgb(220, 178, 120), Color.FromArgb(170, 125, 75), 90f)) g.FillPath(b, p);
                        using (Pen pp = new Pen(Color.FromArgb(120, 85, 45), 0.25f)) g.DrawPath(pp, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 235, 215, 170))) g.FillRectangle(b, -0.4f, -2.0f, 0.8f, 4.0f);
                    break;
                case 'W': // метла
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 100, 60))) g.FillRectangle(b, -0.25f, -5.5f, 0.5f, 10f);
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddPolygon(new PointF[] { new PointF(-1.1f, 4.2f), new PointF(1.1f, 4.2f), new PointF(1.9f, 6.4f), new PointF(-1.9f, 6.4f) });
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 196, 96))) g.FillPath(b, p);
                        using (Pen pp = new Pen(Color.FromArgb(160, 120, 40), 0.18f))
                        {
                            g.DrawPath(pp, p);
                            for (int i = -2; i <= 2; i++) g.DrawLine(pp, i * 0.4f, 4.6f, i * 0.65f, 6.3f);
                        }
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 50, 50))) g.FillRectangle(b, -1.1f, 4.0f, 2.2f, 0.5f);
                    break;
                case 'Y': // швабра
                    using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(176, 130, 86), alpha))) g.FillRectangle(b, -0.28f, -9.5f, 0.56f, 15.5f);
                    g.DrawRectangle(dk, -0.28f, -9.5f, 0.56f, 15.5f);
                    using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(120, 130, 150), alpha))) g.FillRectangle(b, -1.6f, 5.6f, 3.2f, 1.0f);
                    g.DrawRectangle(dk, -1.6f, 5.6f, 3.2f, 1.0f);
                    using (Pen mp = new Pen(Alpha(Color.FromArgb(214, 218, 226), alpha), 0.55f))
                    using (Pen mo = new Pen(Alpha(InkC, alpha), 0.95f))
                    {
                        mp.StartCap = LineCap.Round; mp.EndCap = LineCap.Round; mo.StartCap = LineCap.Round; mo.EndCap = LineCap.Round;
                        for (int i = -3; i <= 3; i++) { float sw = (float)Math.Sin(tick * 0.3f + i) * 0.35f; g.DrawLine(mo, i * 0.45f, 6.6f, i * 0.62f + sw, 9.4f); }
                        for (int i = -3; i <= 3; i++) { float sw = (float)Math.Sin(tick * 0.3f + i) * 0.35f; g.DrawLine(mp, i * 0.45f, 6.6f, i * 0.62f + sw, 9.4f); }
                    }
                    break;
                case 'U': // кружка кофе
                    g.ScaleTransform(1.6f, 1.6f);
                    using (GraphicsPath p = RoundRect(-1.5f, -1.3f, 3.0f, 2.9f, 0.6f))
                    {
                        using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(250, 246, 238), alpha))) g.FillPath(b, p);
                        using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(214, 120, 110), alpha))) g.FillRectangle(b, -1.5f, 0.1f, 3.0f, 0.6f);
                        g.DrawPath(dk, p);
                    }
                    g.DrawArc(dk, 1.0f, -0.7f, 1.4f, 1.5f, -90, 180);
                    using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(120, 76, 50), alpha))) g.FillEllipse(b, -1.3f, -1.55f, 2.6f, 0.55f);
                    using (Pen sp = new Pen(Color.FromArgb(alpha * 150 / 255, 255, 255, 255), 0.3f))
                    {
                        sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round;
                        float w = (float)Math.Sin(tick * 0.15f) * 0.3f;
                        g.DrawBezier(sp, -0.5f, -1.9f, -0.9f + w, -2.6f, -0.1f - w, -3.0f, -0.5f, -3.7f);
                        g.DrawBezier(sp, 0.6f, -1.9f, 0.2f - w, -2.5f, 1.0f + w, -2.9f, 0.6f, -3.5f);
                    }
                    break;
                case 'S': // звёздочка
                    using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(255, 220, 60), alpha)))
                        g.FillPolygon(b, new PointF[] { new PointF(0, -1.2f), new PointF(0.3f, -0.3f), new PointF(1.2f, 0), new PointF(0.3f, 0.3f), new PointF(0, 1.2f), new PointF(-0.3f, 0.3f), new PointF(-1.2f, 0), new PointF(-0.3f, -0.3f) });
                    break;
                case 'H': // сердечко
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddBezier(0f, 1.6f, -2.2f, 0f, -1.6f, -1.8f, 0f, -0.7f);
                        p.AddBezier(0f, -0.7f, 1.6f, -1.8f, 2.2f, 0f, 0f, 1.6f);
                        using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(255, 110, 150), alpha))) g.FillPath(b, p);
                    }
                    break;
                case 'M': // нота
                    using (SolidBrush b = new SolidBrush(Alpha(c, alpha)))
                    {
                        g.FillEllipse(b, -1.6f, 0.6f, 1.9f, 1.4f);
                        g.FillRectangle(b, 0.0f, -2.6f, 0.35f, 3.6f);
                        using (GraphicsPath p = new GraphicsPath())
                        {
                            p.AddBezier(0.2f, -2.6f, 1.4f, -2.0f, 1.8f, -1.2f, 1.2f, -0.2f);
                            p.AddBezier(1.2f, -0.2f, 1.4f, -1.1f, 0.9f, -1.6f, 0.2f, -1.8f);
                            g.FillPath(b, p);
                        }
                    }
                    break;
            }
        }
        g.Restore(st);
    }

    // ---------- магазин ----------
    void DrawShop(Graphics g)
    {
        for (int s = 0; s < 2; s++)
        {
            float by = Ground + (1 - Appear(s)) * U(48);
            float ox = A + U(30 * s);
            GraphicsState zs = g.Save();
            g.TranslateTransform(ox + U(13), Ground + ZY(ShelfZ)); g.ScaleTransform(ZK(ShelfZ), ZK(ShelfZ)); g.TranslateTransform(-(ox + U(13)), -Ground);
            Shadow(g, ox + U(13), U(30));
            Wood(g, pal['B'], ox, by, 0, 0, 26, 40, 1.2f);
            using (LinearGradientBrush b = new LinearGradientBrush(R(ox, by, 2, 2, 22, 36), Color.FromArgb(92, 60, 40), Color.FromArgb(70, 44, 30), 90f))
                g.FillRectangle(b, R(ox, by, 2, 2, 22, 36));
            float[] boards = new float[] { 1, 13, 25 };
            for (int lv = 0; lv < 3; lv++)
            {
                for (int k = 0; k < 6; k++)
                {
                    char col = ItemColors[(s * 5 + lv * 3 + k * 7) % ItemColors.Length];
                    DrawItem(g, 'B', col, ox + U(3.8f + k * 3.7f), by - U(boards[lv] + 1.6f + 3.2f));
                }
                Wood(g, pal['B'], ox, by, 1, boards[lv], 24, 1.6f, 0.4f);
            }
            if (mode == 6 && countShelf == s && countIdx < 18)
            {
                int lv2 = countIdx / 6, k2 = countIdx % 6;
                float cx = ox + U(3.8f + k2 * 3.7f), cy = by - U(boards[lv2] + 1.6f + 3.2f);
                Glow(g, cx, cy, U(3.2f), U(4.2f), Color.FromArgb(255, 240, 120), 200);
                using (Pen p = new Pen(Color.FromArgb(255, 230, 60), Math.Max(1, U(0.35f)))) g.DrawEllipse(p, cx - U(1.9f), cy - U(3.6f), U(3.8f), U(7.2f));
            }
            // подсветка полки
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 240, 200))) g.FillRectangle(b, R(ox, by, 2, 30, 22, 8));
            Panel(g, pal['Y'], ox, by, 4, 40.6f, 18, 5, 1.2f);
            RectangleF sr = R(ox, by, 4, 40.6f, 18, 5);
            using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(pal['K'])) g.DrawString("VAPE", fntSign, b, sr, sf); }
            g.Restore(zs);
        }
        {
            float by = Ground + (1 - Appear(2)) * U(28);
            float ox = CounterX;
            Shadow(g, ox + U(12), U(28));
            Wood(g, pal['B'], ox, by, 0, 0, 24, 12.5f, 1);
            Flat(g, Color.FromArgb(60, 0, 0, 0), ox, by, 3, 3, 18, 0.6f, 0.3f);
            Flat(g, Color.FromArgb(60, 0, 0, 0), ox, by, 3, 7, 18, 0.6f, 0.3f);
            Wood(g, pal['b'], ox, by, -1, 12, 26, 2.4f, 0.8f);
            // трещины прилавка
            if (counterHP < 100 && (mode == 1))
            {
                int n = (int)((100 - counterHP) / 12);
                using (Pen cp = new Pen(Color.FromArgb(200, 50, 30, 20), Math.Max(1, U(0.3f))))
                    for (int i = 0; i < n; i++)
                    {
                        float x0 = 3 + (i * 7.3f) % 18, y0 = 2 + (i * 3.7f) % 9;
                        g.DrawLines(cp, new PointF[] { new PointF(ox + U(x0), by - U(y0)), new PointF(ox + U(x0 + 1.5f), by - U(y0 + 1.8f)), new PointF(ox + U(x0 + 0.8f), by - U(y0 + 3.2f)), new PointF(ox + U(x0 + 2.4f), by - U(y0 + 4.4f)) });
                    }
                if (counterHP <= 0) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 30, 20, 15))) g.FillPolygon(b, new PointF[] { new PointF(ox + U(16), by), new PointF(ox + U(24), by), new PointF(ox + U(24), by - U(7)), new PointF(ox + U(20), by - U(4)) });
            }
            // касса
            if (!registerDown && !registerFlying || mode != 1)
            {
            Panel(g, Color.FromArgb(90, 96, 112), ox, by, 3, 14, 14, 7.5f, 1.4f);
            Color scr = regFlash > 0 && tick % 6 < 3 ? Color.FromArgb(255, 230, 120) : Color.FromArgb(110, 230, 150);
            if (regFlash > 0) using (SolidBrush b = new SolidBrush(Color.FromArgb(80, scr))) g.FillEllipse(b, R(ox, by, 3, 15, 10, 7));
            Panel(g, scr, ox, by, 5, 17.3f, 6, 2.8f, 0.5f);
            for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
                    Flat(g, Color.FromArgb(200, 205, 215), ox, by, 12.3f + i * 1.8f, 15.6f + j * 1.8f, 1.2f, 1.2f, 0.4f);
            }
            if (registerDown && mode == 1) DrawItem(g, 'G', 'G', registerX, Ground - U(2.5f), 255, 18);
            Panel(g, Color.FromArgb(60, 62, 72), ox, by, 17.5f, 14, 4.5f, 3.4f, 0.6f);
            Flat(g, Color.FromArgb(120, 230, 150), ox, by, 18.4f, 16, 2.6f, 0.8f, 0.2f);
            if (bottleOnReg) DrawItem(g, 'B', regCol, ox + U(9), by - U(25.2f));
            if (devOnReg) DrawItem(g, 'V', 'G', ox + U(6.5f), by - U(24.5f));
            if (cardOnReg) DrawItem(g, 'I', 'u', ox + U(11.5f), by - U(23.6f));
        }
    }

    // ---------- диван ----------
    void DrawChill(Graphics g)
    {
        {
            float by = Ground + (1 - Appear(0)) * U(24);
            float ox = A;
            Shadow(g, ox + U(15), U(32));
            Color sofa = Color.FromArgb(150, 100, 210);
            Flat(g, pal['b'], ox, by, 2, 0, 2, 3, 0.5f); Flat(g, pal['b'], ox, by, 26, 0, 2, 3, 0.5f);
            Panel(g, sofa, ox, by, 0, 2, 6, 18.5f, 2.6f);
            Panel(g, sofa, ox, by, 0, 2, 30, 7, 1.6f);
            Panel(g, Light(sofa, 0.15f), ox, by, 5, 7.6f, 20.5f, 2.8f, 1.3f);
            using (Pen p = new Pen(Color.FromArgb(50, 60, 30, 90), Math.Max(1, U(0.25f))))
                for (int i = 1; i < 3; i++) g.DrawLine(p, ox + U(5 + i * 6.8f), by - U(7.8f), ox + U(5 + i * 6.8f), by - U(10.2f));
            Panel(g, pal['Y'], ox, by, 4.2f, 10, 3.6f, 6, 1.6f);
            Panel(g, sofa, ox, by, 24.5f, 2, 5.5f, 11, 2.6f);
        }
        {
            float by = Ground + (1 - Appear(1)) * U(30);
            float ox = HookahX;
            Shadow(g, ox + U(4), U(9));
            RectangleF vr = R(ox, by, 0.4f, 0, 7.2f, 7.6f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 70, 130, 230))) g.FillEllipse(b, vr);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 150, 210, 255))) g.FillEllipse(b, R(ox, by, 1.2f, 0.6f, 5.6f, 3.6f));
            using (Pen p = new Pen(Color.FromArgb(40, 70, 140), Math.Max(1, U(0.35f)))) g.DrawEllipse(p, vr);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(b, R(ox, by, 1.6f, 3.6f, 1.2f, 2.6f));
            if (me.PuffT > 55)
                for (int k = 0; k < 4; k++)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 230, 245, 255)))
                        g.FillEllipse(b, R(ox, by, 2.6f + (k % 2) * 1.6f, 1 + ((tick / 2 + k * 3) % 5), 0.9f, 0.9f));
            Panel(g, Color.FromArgb(70, 130, 230), ox, by, 2.6f, 6.8f, 2.8f, 3, 0.9f);
            Panel(g, Color.FromArgb(190, 195, 205), ox, by, 3.3f, 9, 1.4f, 11, 0.6f);
            Panel(g, Color.FromArgb(190, 195, 205), ox, by, 0.4f, 18.4f, 7.2f, 1.6f, 0.8f);
            using (GraphicsPath p = new GraphicsPath())
            {
                RectangleF b2 = R(ox, by, 2, 20, 4, 3.6f);
                p.AddPolygon(new PointF[] { new PointF(b2.Left + U(0.8f), b2.Bottom), new PointF(b2.Right - U(0.8f), b2.Bottom), new PointF(b2.Right, b2.Top), new PointF(b2.Left, b2.Top) });
                using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 96, 64))) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(110, 50, 30), Math.Max(1, U(0.3f)))) g.DrawPath(pp, p);
            }
            bool hot = me.PuffT > 55;
            if (hot) using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 255, 160, 60))) g.FillEllipse(b, R(ox, by, 0.6f, 21.6f, 6.8f, 4.4f));
            Flat(g, hot && tick % 8 < 5 ? Color.FromArgb(255, 210, 90) : Color.FromArgb(220, 70, 50), ox, by, 2.4f, 23.4f, 3.2f, 1.3f, 0.6f);
        }
        {
            float by = Ground + (1 - Appear(2)) * U(40);
            float ox = StandX;
            Shadow(g, ox + U(18), U(38));
            Wood(g, pal['B'], ox, by, 0, 0, 36, 9, 1);
            Flat(g, Color.FromArgb(60, 0, 0, 0), ox, by, 2, 4.2f, 32, 0.5f, 0.2f);
            Flat(g, Color.FromArgb(200, 225, 200, 160), ox, by, 9, 1.8f, 3, 0.8f, 0.4f);
            Flat(g, Color.FromArgb(200, 225, 200, 160), ox, by, 24, 1.8f, 3, 0.8f, 0.4f);
            // приставка
            Panel(g, Color.FromArgb(242, 242, 246), ox, by, 2, 9, 6, 12, 1.6f);
            Flat(g, Color.FromArgb(30, 32, 40), ox, by, 4.6f, 9.6f, 0.8f, 10.8f, 0.3f);
            if (consoleOn) using (SolidBrush b = new SolidBrush(Color.FromArgb(tick % 40 < 25 ? 140 : 70, 80, 160, 255))) g.FillEllipse(b, R(ox, by, 2.2f, 8.8f, 5.6f, 2.4f));
            Flat(g, consoleOn ? Color.FromArgb(90, 170, 255) : Color.FromArgb(160, 165, 175), ox, by, 3.4f, 9.8f, 3.2f, 0.4f, 0.2f);
            // телевизор
            Panel(g, Color.FromArgb(40, 44, 56), ox, by, 21, 9, 4.4f, 1.6f, 0.5f);
            if (consoleOn && tvState >= 2)
            {
                Color glow = tvState == 2 ? Color.FromArgb(90, 140, 255) : GameA[gameIdx];
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, glow))) g.FillEllipse(b, R(ox, by, 5, 6, 36, 28));
            }
            Panel(g, Color.FromArgb(34, 37, 48), ox, by, 9, 10.4f, 28, 18.6f, 1.2f);
            RectangleF scr = R(ox, by, 10, 11.4f, 26, 16.6f);
            GraphicsState gs = g.Save();
            using (GraphicsPath clip = RoundRect(scr.X, scr.Y, scr.Width, scr.Height, U(0.6f))) g.SetClip(clip);
            DrawTV(g, scr);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
                g.FillPolygon(b, new PointF[] { new PointF(scr.X + U(2), scr.Y), new PointF(scr.X + U(7), scr.Y), new PointF(scr.X + U(1), scr.Bottom), new PointF(scr.X - U(4), scr.Bottom) });
            g.Restore(gs);
            if (consoleOn && tvState >= 2) DrawGamePlate(g, ox + U(23), by - U(31.5f));
            if (caseVisible) DrawItem(g, 'C', 'U', ox + U(41.5f), by - U(1.9f));
        }
    }

    void DrawTV(Graphics g, RectangleF r)
    {
        if (!consoleOn)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(28, 34, 52), Color.FromArgb(12, 14, 22), 90f)) g.FillRectangle(b, r);
            return;
        }
        if (tvState == 1)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 12, 20))) g.FillRectangle(b, r);
            float k = Math.Min(1f, tvT / 30f);
            float cr = r.Height * 0.22f * k;
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(200 * k), 90, 160, 255))) g.FillEllipse(b, r.X + r.Width / 2 - cr, r.Y + r.Height * 0.42f - cr, cr * 2, cr * 2);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 70, 90))) g.FillRectangle(b, r.X + r.Width * 0.25f, r.Y + r.Height * 0.78f, r.Width * 0.5f, U(0.8f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 190, 255))) g.FillRectangle(b, r.X + r.Width * 0.25f, r.Y + r.Height * 0.78f, r.Width * 0.5f * Math.Min(1f, tvT / 60f), U(0.8f));
            return;
        }
        if (tvState == 2)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(30, 40, 80), Color.FromArgb(10, 12, 30), 90f)) g.FillRectangle(b, r);
            for (int k = -1; k <= 1; k++)
            {
                int gi = (menuSel + k + Games.Length) % Games.Length;
                float w = r.Width * (k == 0 ? 0.32f : 0.24f), h = r.Height * (k == 0 ? 0.62f : 0.48f);
                float cx = r.X + r.Width / 2 + k * r.Width * 0.32f, cy = r.Y + r.Height * 0.47f;
                RectangleF tr = new RectangleF(cx - w / 2, cy - h / 2, w, h);
                using (GraphicsPath p = RoundRect(tr.X, tr.Y, tr.Width, tr.Height, U(0.5f)))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(tr.X, tr.Y - 1, tr.Width, tr.Height + 2), GameBg[gi], GameB[gi], 90f)) g.FillPath(b, p);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(220, GameA[gi]))) g.FillRectangle(b, tr.X + w * 0.15f, tr.Y + h * 0.62f, w * 0.7f, h * 0.12f);
                    if (k == 0) using (Pen pp = new Pen(Color.White, Math.Max(1, U(0.35f)))) g.DrawPath(pp, p);
                }
            }
            return;
        }
        if (tvState == 3)
        {
            Color a = GameA[gameIdx], b2 = GameB[gameIdx];
            using (LinearGradientBrush b = new LinearGradientBrush(r, GameBg[gameIdx], Dark(b2, 0.6f), 90f)) g.FillRectangle(b, r);
            float k = (tvT % 40) / 40f;
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * (1 - k)), a))) g.FillEllipse(b, r.X + r.Width / 2 - r.Width * k, r.Y + r.Height / 2 - r.Width * k, r.Width * 2 * k, r.Width * 2 * k);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(230, a))) g.FillRectangle(b, r.X + r.Width * 0.15f, r.Y + r.Height * 0.45f, r.Width * 0.7f, r.Height * 0.1f);
            return;
        }
        DrawGame(g, gameIdx, r, tvT);
    }

    void DrawGamePlate(Graphics g, float cx, float bottom)
    {
        int gi = tvState == 2 ? menuSel : gameIdx;
        string text = Games[gi];
        Font f = logoFonts[gi];
        SizeF sz = g.MeasureString(text, f);
        float w = sz.Width + U(3), h = sz.Height + U(0.8f);
        float x = cx - w / 2, y = bottom - h;
        if (x < 2) x = 2; if (x + w > Width - 2) x = Width - 2 - w;
        int alpha = tvState == 3 ? 255 : 235;
        using (GraphicsPath p = RoundRect(x, y, w, h, U(1)))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y - 1, w, h + 2), Color.FromArgb(alpha, Light(GameBg[gi], 0.12f)), Color.FromArgb(alpha, GameBg[gi]), 90f)) g.FillPath(b, p);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x - 1, y, w + 2, h), GameA[gi], GameB[gi], 0f))
            using (Pen pp = new Pen(b, Math.Max(1, U(0.35f)))) g.DrawPath(pp, p);
        }
        if (tvState == 3)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60 + (int)(50 * Math.Sin(tick * 0.3)), GameA[gi]))) g.FillRectangle(b, x + U(1), y + h - U(0.8f), w - U(2), U(0.4f));
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y, w, h), GameA[gi], GameB[gi], 90f))
            g.DrawString(text, f, b, x + U(1.5f), y + U(0.4f));
        if (tvState == 2)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.DrawString("выбор игры...", fntSign, b, x + U(1), y - U(4.2f));
    }

    // мини-сценки по мотивам игр (без настоящих логотипов и персонажей)
    void DrawGame(Graphics g, int gi, RectangleF r, int t)
    {
        float X = r.X, Y = r.Y, W = r.Width, H = r.Height;
        Func<float, float> fx = delegate (float v) { return X + v * W; };
        Func<float, float> fy = delegate (float v) { return Y + v * H; };
        switch (gi)
        {
            case 0: // закатный неоновый город у моря
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(255, 110, 170), Color.FromArgb(255, 195, 110), 90f)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 235, 160))) g.FillEllipse(b, fx(0.58f), fy(0.22f), W * 0.24f, W * 0.24f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 40, 110)))
                {
                    float[] hs = { 0.35f, 0.5f, 0.28f, 0.6f, 0.4f, 0.55f, 0.3f };
                    for (int i = 0; i < hs.Length; i++) g.FillRectangle(b, fx(0.02f + i * 0.14f), fy(0.78f - hs[i]), W * 0.11f, H * hs[i]);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 120)))
                    for (int i = 0; i < 12; i++) g.FillRectangle(b, fx(0.04f + (i % 7) * 0.14f), fy(0.55f + (i / 7) * 0.1f), W * 0.02f, H * 0.03f);
                using (Pen p = new Pen(Color.FromArgb(50, 20, 60), Math.Max(1, U(0.5f))))
                {
                    g.DrawBezier(p, fx(0.12f), fy(0.8f), fx(0.13f), fy(0.6f), fx(0.1f), fy(0.45f), fx(0.08f), fy(0.35f));
                    for (int i = 0; i < 4; i++) g.DrawArc(p, fx(0.0f + i * 0.04f), fy(0.3f), W * 0.1f, H * 0.12f, 180 + i * 20, 70);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 30, 60))) g.FillRectangle(b, X, fy(0.8f), W, H * 0.2f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 120)))
                    for (int i = 0; i < 4; i++) g.FillRectangle(b, X + ((i * W * 0.3f - t * U(0.4f)) % W + W) % W, fy(0.89f), W * 0.1f, H * 0.025f);
                float carX = X + ((t * U(0.5f)) % (W + U(8))) - U(6);
                using (GraphicsPath p = RoundRect(carX, fy(0.8f), U(6), H * 0.1f, U(0.6f)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 80, 160))) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(20, 20, 20))) { g.FillEllipse(b, carX + U(0.8f), fy(0.87f), U(1.3f), U(1.3f)); g.FillEllipse(b, carX + U(4), fy(0.87f), U(1.3f), U(1.3f)); }
                break;
            }
            case 1: // тёмный коридор и фонарик
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(30, 14, 14), Color.FromArgb(5, 5, 5), 90f)) g.FillRectangle(b, r);
                using (Pen p = new Pen(Color.FromArgb(70, 110, 80, 80), 1))
                {
                    g.DrawLine(p, X, Y, fx(0.38f), fy(0.35f)); g.DrawLine(p, X + W, Y, fx(0.62f), fy(0.35f));
                    g.DrawLine(p, X, Y + H, fx(0.38f), fy(0.65f)); g.DrawLine(p, X + W, Y + H, fx(0.62f), fy(0.65f));
                    g.DrawRectangle(p, fx(0.38f), fy(0.35f), W * 0.24f, H * 0.3f);
                }
                bool flick = (t / 3) % 11 != 0;
                if (flick)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 240, 200)))
                        g.FillPolygon(b, new PointF[] { new PointF(fx(0.45f), Y), new PointF(fx(0.55f), Y), new PointF(fx(0.8f), Y + H), new PointF(fx(0.2f), Y + H) });
                float zk = 0.4f + ((t % 300) / 300f) * 0.6f;
                float zx = fx(0.5f), zy = fy(0.5f + 0.15f * zk);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 15, 10, 10)))
                {
                    g.FillEllipse(b, zx - U(1.8f) * zk, zy - U(3) * zk, U(3.6f) * zk, U(6) * zk);
                    g.FillEllipse(b, zx - U(1.1f) * zk, zy - U(5) * zk, U(2.2f) * zk, U(2.2f) * zk);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 30, 30))) { g.FillEllipse(b, zx - U(0.6f) * zk, zy - U(4.3f) * zk, U(0.4f), U(0.4f)); g.FillEllipse(b, zx + U(0.2f) * zk, zy - U(4.3f) * zk, U(0.4f), U(0.4f)); }
                if (t % 160 < 6) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 0, 0))) g.FillRectangle(b, r);
                break;
            }
            case 2: // тушь, горы, взмахи клинков
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 230, 218))) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(215, 50, 45))) g.FillEllipse(b, fx(0.66f), fy(0.12f), W * 0.16f, W * 0.16f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 40, 40, 45)))
                    g.FillPolygon(b, new PointF[] { new PointF(X, fy(0.7f)), new PointF(fx(0.25f), fy(0.3f)), new PointF(fx(0.45f), fy(0.62f)), new PointF(fx(0.7f), fy(0.38f)), new PointF(X + W, fy(0.7f)), new PointF(X + W, Y + H), new PointF(X, Y + H) });
                using (SolidBrush b = new SolidBrush(Color.FromArgb(25, 25, 28)))
                {
                    g.FillRectangle(b, X, fy(0.86f), W, H * 0.14f);
                    float dx = (float)Math.Sin(t * 0.08) * W * 0.06f;
                    g.FillEllipse(b, fx(0.3f) + dx, fy(0.6f), U(2.2f), U(4.5f)); g.FillEllipse(b, fx(0.3f) + dx + U(0.3f), fy(0.53f), U(1.6f), U(1.6f));
                    g.FillEllipse(b, fx(0.64f) - dx, fy(0.6f), U(2.2f), U(4.5f)); g.FillEllipse(b, fx(0.64f) - dx + U(0.3f), fy(0.53f), U(1.6f), U(1.6f));
                }
                int st = t % 50;
                if (st < 12)
                    using (Pen p = new Pen(Color.FromArgb(255 - st * 20, 200, 20, 20), Math.Max(1, U(0.6f))))
                    using (Pen p2 = new Pen(Color.FromArgb(255 - st * 20, 255, 255, 255), Math.Max(1, U(0.25f))))
                    {
                        g.DrawArc(p, fx(0.35f), fy(0.4f), W * 0.3f, H * 0.4f, 200, 140);
                        g.DrawArc(p2, fx(0.35f), fy(0.4f), W * 0.3f, H * 0.4f, 200, 140);
                    }
                break;
            }
            case 3: // гонка у снежной горы, сакура
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(110, 180, 255), Color.FromArgb(225, 240, 255), 90f)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 150, 190))) g.FillPolygon(b, new PointF[] { new PointF(fx(0.15f), fy(0.62f)), new PointF(fx(0.5f), fy(0.15f)), new PointF(fx(0.85f), fy(0.62f)) });
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new PointF[] { new PointF(fx(0.4f), fy(0.29f)), new PointF(fx(0.5f), fy(0.15f)), new PointF(fx(0.6f), fy(0.29f)), new PointF(fx(0.55f), fy(0.26f)), new PointF(fx(0.5f), fy(0.31f)), new PointF(fx(0.45f), fy(0.26f)) });
                using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 180, 100))) g.FillRectangle(b, X, fy(0.6f), W, H * 0.4f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 84, 96))) g.FillPolygon(b, new PointF[] { new PointF(fx(0.46f), fy(0.6f)), new PointF(fx(0.54f), fy(0.6f)), new PointF(fx(0.9f), Y + H), new PointF(fx(0.1f), Y + H) });
                using (SolidBrush b = new SolidBrush(Color.White))
                    for (int i = 0; i < 4; i++)
                    {
                        float k = ((t * 0.02f + i * 0.25f) % 1f);
                        float yy = fy(0.6f + 0.4f * k), ww = U(0.3f + 0.8f * k);
                        g.FillRectangle(b, fx(0.5f) - ww / 2, yy, ww, U(0.3f + 1.2f * k));
                    }
                float sway = (float)Math.Sin(t * 0.07) * W * 0.12f;
                using (GraphicsPath p = RoundRect(fx(0.4f) + sway, fy(0.78f), W * 0.2f, H * 0.12f, U(0.8f)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 140, 30))) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 50))) g.FillRectangle(b, fx(0.43f) + sway, fy(0.8f), W * 0.14f, H * 0.03f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 255, 170, 200)))
                    for (int i = 0; i < 7; i++)
                    {
                        float px = X + ((i * W * 0.17f + (float)Math.Sin(t * 0.05 + i) * U(1.5f)) % W);
                        float py = Y + ((t * U(0.15f) + i * H * 0.23f) % H);
                        g.FillEllipse(b, px, py, U(0.7f), U(0.5f));
                    }
                break;
            }
            case 4: // космос и голограммы
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(6, 8, 20))) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.White))
                    for (int i = 0; i < 18; i++) { float sx = fx((i * 37 % 100) / 100f), sy = fy((i * 61 % 70) / 100f); float sz = ((t / 10 + i) % 5 == 0) ? U(0.5f) : U(0.3f); g.FillEllipse(b, sx, sy, sz, sz); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 120, 220))) g.FillEllipse(b, fx(0.7f), fy(0.08f), W * 0.2f, W * 0.2f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 150, 160))) g.FillEllipse(b, fx(-0.3f), fy(0.72f), W * 1.6f, H);
                using (Pen p = new Pen(Color.FromArgb(90, 80, 230, 255), 1))
                    for (int i = 0; i < 5; i++) g.DrawLine(p, fx(0.1f + i * 0.2f), fy(0.25f), fx(0.1f + i * 0.2f), fy(0.7f));
                float hy = (float)Math.Sin(t * 0.08) * H * 0.05f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 80, 230, 255))) g.FillEllipse(b, fx(0.42f), fy(0.42f) + hy, W * 0.16f, H * 0.3f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 245, 255)))
                {
                    g.FillEllipse(b, fx(0.46f), fy(0.45f) + hy, W * 0.08f, W * 0.08f);
                    using (GraphicsPath p = RoundRect(fx(0.455f), fy(0.55f) + hy, W * 0.09f, H * 0.14f, U(0.5f))) g.FillPath(b, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 230, 255))) g.FillRectangle(b, fx(0.475f), fy(0.475f) + hy, W * 0.04f, H * 0.03f);
                break;
            }
            case 5: // ночной снежный лес, следы когтей
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(20, 30, 70), Color.FromArgb(60, 80, 130), 90f)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 240, 250))) g.FillRectangle(b, X, fy(0.82f), W, H * 0.18f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(15, 35, 40)))
                    for (int i = 0; i < 6; i++)
                    {
                        float tx = fx(0.05f + i * 0.17f), th = H * (0.4f + (i % 3) * 0.12f);
                        g.FillPolygon(b, new PointF[] { new PointF(tx, fy(0.84f) - th), new PointF(tx - W * 0.07f, fy(0.84f)), new PointF(tx + W * 0.07f, fy(0.84f)) });
                    }
                using (SolidBrush b = new SolidBrush(Color.White))
                    for (int i = 0; i < 14; i++)
                    {
                        float px = X + ((i * W * 0.13f + (float)Math.Sin(t * 0.04 + i) * U(1)) % W);
                        float py = Y + ((t * U(0.12f) + i * H * 0.31f) % H);
                        g.FillEllipse(b, px, py, U(0.45f), U(0.45f));
                    }
                int ct = t % 90;
                if (ct < 18)
                    using (Pen p = new Pen(Color.FromArgb(255 - ct * 13, 230, 235, 245), Math.Max(1, U(0.45f))))
                        for (int i = 0; i < 3; i++) g.DrawLine(p, fx(0.3f + i * 0.12f), fy(0.2f), fx(0.45f + i * 0.12f), fy(0.75f));
                break;
            }
            case 6: // тоннель метро и свет поезда
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 12, 10))) g.FillRectangle(b, r);
                float cx = fx(0.5f), cy = fy(0.45f);
                using (Pen p = new Pen(Color.FromArgb(60, 70, 60), 1))
                    for (int i = 1; i <= 5; i++) { float rr = W * 0.11f * i; g.DrawEllipse(p, cx - rr, cy - rr * 0.8f, rr * 2, rr * 1.6f); }
                using (Pen p = new Pen(Color.FromArgb(150, 140, 120), Math.Max(1, U(0.3f))))
                {
                    g.DrawLine(p, cx - U(0.6f), cy + U(1), fx(0.2f), Y + H); g.DrawLine(p, cx + U(0.6f), cy + U(1), fx(0.8f), Y + H);
                }
                float k = ((t % 200) / 200f);
                float lr = W * (0.03f + 0.35f * k * k);
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(80 + 150 * k), 255, 240, 190))) g.FillEllipse(b, cx - lr, cy - lr * 0.8f, lr * 2, lr * 1.6f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 250, 220))) g.FillEllipse(b, cx - lr * 0.3f, cy - lr * 0.25f, lr * 0.6f, lr * 0.5f);
                break;
            }
            default: // красно-чёрная геометрия
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 10, 10))) g.FillRectangle(b, r);
                GraphicsState gs = g.Save();
                g.TranslateTransform(fx(0.5f), fy(0.5f));
                using (Pen p = new Pen(Color.FromArgb(230, 40, 40), Math.Max(1, U(0.4f))))
                    for (int i = 0; i < 3; i++)
                    {
                        g.RotateTransform(t * 0.6f + i * 30);
                        float s = H * (0.25f + i * 0.13f);
                        g.DrawRectangle(p, -s / 2, -s / 2, s, s);
                        g.RotateTransform(-(t * 0.6f + i * 30));
                    }
                g.Restore(gs);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 200, 205)))
                    for (int i = 0; i < 4; i++) g.FillRectangle(b, fx(0.12f + i * 0.22f), fy(0.75f) + (float)Math.Sin(t * 0.06 + i) * H * 0.08f, W * 0.07f, H * 0.08f);
                if ((t / 4) % 17 == 0) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillRectangle(b, X, fy(0.3f + ((t / 4) % 5) * 0.1f), W, H * 0.05f);
                break;
            }
        }
    }

    void DrawHose(Graphics g)
    {
        float hx = HookahX + U(0.8f), hy = Ground - U(4.2f);
        PointF m = Local(me, 3.6f, -15.3f);
        PointF[] pts = { new PointF(hx, hy), new PointF(hx - U(6), hy + U(3)), new PointF(m.X + U(3), m.Y + U(10)), m };
        using (Pen p = new Pen(Color.FromArgb(30, 60, 70), U(1.0f))) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawBezier(p, pts[0], pts[1], pts[2], pts[3]); }
        using (Pen p = new Pen(Color.FromArgb(70, 150, 160), U(0.45f))) g.DrawBezier(p, pts[0], pts[1], pts[2], pts[3]);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 205, 215))) g.FillEllipse(b, m.X - U(0.7f), m.Y - U(0.7f), U(1.4f), U(1.4f));
    }

    void DrawBox(Graphics g, float bx, bool open)
    {
        float byb = Ground;

            Shadow(g, bx, U(16));
            Color cb = Color.FromArgb(206, 160, 104);
            if (open)
            {
                using (SolidBrush b = new SolidBrush(Dark(cb, 0.75f)))
                {
                    g.FillPolygon(b, new PointF[] { new PointF(bx - U(7), byb - U(10)), new PointF(bx - U(12), byb - U(14)), new PointF(bx - U(9), byb - U(15)), new PointF(bx - U(2), byb - U(10)) });
                    g.FillPolygon(b, new PointF[] { new PointF(bx + U(7), byb - U(10)), new PointF(bx + U(12), byb - U(14)), new PointF(bx + U(9), byb - U(15)), new PointF(bx + U(2), byb - U(10)) });
                }
            }
            Panel(g, cb, bx, byb, -7, 0, 14, 10, 0.5f);
            Flat(g, Color.FromArgb(140, 230, 210, 160), bx, byb, -1.2f, 0, 2.4f, 10, 0);
            Flat(g, Color.FromArgb(230, 255, 255, 255), bx, byb, -5.5f, 3, 3.6f, 2.4f, 0.3f);
            using (Pen p = new Pen(Color.FromArgb(180, 220, 50, 50), Math.Max(1, U(0.25f)))) { g.DrawLine(p, bx + U(2.5f), byb - U(3), bx + U(5.5f), byb - U(6)); g.DrawLine(p, bx + U(5.5f), byb - U(3), bx + U(2.5f), byb - U(6)); }
            if (open) Flat(g, Color.FromArgb(90, 60, 30), bx, byb, -6.5f, 9, 13, 1.2f, 0.3f);
            }

    // ---------- табак ----------
    void DrawTidy(Graphics g)
    {
        if (carState != 0) DrawCar(g);
        foreach (float[] bxx in boxes) DrawBox(g, bxx[0], bxx[1] == 3);

        foreach (float[] d in dust)
        {
            float rr = U(d[1]);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 175, 165, 150)))
            {
                g.FillEllipse(b, d[0] - rr * 1.6f, Ground - rr * 0.9f, rr * 3.2f, rr * 1.0f);
                g.FillEllipse(b, d[0] - rr * 0.9f, Ground - rr * 1.4f, rr * 1.6f, rr * 1.3f);
                g.FillEllipse(b, d[0] - rr * 0.1f, Ground - rr * 1.1f, rr * 1.4f, rr * 1.0f);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 120, 110, 95))) g.FillEllipse(b, d[0] - rr * 0.5f, Ground - rr * 0.6f, rr * 0.6f, rr * 0.4f);
        }

        {
            float by = Ground + (1 - Appear(0)) * U(50);
            float ox = RackX;
            if (rackAngle < 30) Shadow(g, ox + U(17), U(38));
            else Shadow(g, ox + U(54), U(48));
            GraphicsState st = g.Save();
            if (Math.Abs(rackAngle) > 0.01f)
            {
                float px = ox + U(rackAngle >= 0 ? 34 : 0);
                g.TranslateTransform(px, by); g.RotateTransform(rackAngle); g.TranslateTransform(-px, -by);
            }
            using (LinearGradientBrush b = new LinearGradientBrush(R(ox, by, 1, 1, 32, 38), Color.FromArgb(96, 64, 42), Color.FromArgb(72, 46, 30), 90f))
                g.FillRectangle(b, R(ox, by, 1, 1, 32, 38));
            Wood(g, pal['B'], ox, by, 0, 0, 3, 41, 0.8f);
            Wood(g, pal['B'], ox, by, 31, 0, 3, 41, 0.8f);
            foreach (float bd in new float[] { 0, 13, 26, 38 }) Wood(g, pal['B'], ox, by, 0, bd, 34, 3, 0.8f);
            for (int s = 0; s < 15; s++)
            {
                if (slots[s] == ' ') continue;
                PointF sp = SlotPos(s);
                DrawItem(g, 'X', slots[s], sp.X, sp.Y + (by - Ground));
            }
            Panel(g, pal['D'], ox, by, 6, 41.4f, 22, 5.2f, 1.4f);
            RectangleF sr = R(ox, by, 6, 41.4f, 22, 5.2f);
            using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString("ТАБАК", fntSign, b, sr, sf); }
            g.Restore(st);
        }
        foreach (float[] p in pile)
            DrawItem(g, 'X', ItemColors[(int)p[2]], PackX(p), PackY(p));
    }

    // ---------- вечеринка ----------
    void DrawPartyBack(Graphics g)
    {
        {
            float by = Ground + (1 - Appear(0)) * U(80);
            float ox = A;
            Shadow(g, ox + U(55), U(112));
            Panel(g, Color.FromArgb(140, 90, 70), ox, by, 80, 56, 8, 18, 0.6f);
            Panel(g, pal['E'], ox, by, 2, 0, 106, 46, 0.6f);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { new PointF(ox - U(3), by - U(45)), new PointF(ox + U(113), by - U(45)), new PointF(ox + U(92), by - U(70)), new PointF(ox + U(18), by - U(70)) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(ox, by - U(71), U(110), U(27)), Color.FromArgb(222, 94, 92), Color.FromArgb(160, 50, 56), 90f)) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(110, 34, 40), Math.Max(1, U(0.45f)))) g.DrawPath(pp, p);
            }
            using (Pen sp = new Pen(Color.FromArgb(60, 80, 20, 20), Math.Max(1, U(0.3f))))
                for (int i = 1; i < 6; i++)
                {
                    float yy = 45 + i * 4.2f;
                    float inset = (yy - 45) * 21f / 25f;
                    g.DrawLine(sp, ox + U(-3 + inset), by - U(yy), ox + U(113 - inset), by - U(yy));
                }
            Color[] disco = { pal['L'], pal['Y'], pal['u'], pal['P'], pal['N'] };
            float[][] wins = new float[][] {
                new float[] { 10, 8 }, new float[] { 78, 8 }, new float[] { 10, 28 }, new float[] { 44, 28 }, new float[] { 78, 28 } };
            for (int i = 0; i < wins.Length; i++)
            {
                Color wc = (enterT > 40) ? disco[(tick / 15 + i * 2) % disco.Length] : Color.FromArgb(170, 215, 255);
                if (enterT > 40)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(60, wc))) g.FillEllipse(b, R(ox, by, wins[i][0] - 4, wins[i][1] - 4, 28, 21));
                Panel(g, Color.White, ox, by, wins[i][0] - 0.8f, wins[i][1] - 0.8f, 21.6f, 14.6f, 0.8f);
                using (LinearGradientBrush b = new LinearGradientBrush(R(ox, by, wins[i][0], wins[i][1], 20, 13), Light(wc, 0.35f), wc, 90f))
                    g.FillRectangle(b, R(ox, by, wins[i][0], wins[i][1], 20, 13));
                Flat(g, Color.White, ox, by, wins[i][0] + 9.4f, wins[i][1], 1.2f, 13, 0);
                Flat(g, Color.White, ox, by, wins[i][0], wins[i][1] + 5.9f, 20, 1.2f, 0);
            }
            if (doorOpen > 0) Panel(g, Color.FromArgb(40, 30, 30), ox, by, 48, 0, 14, 24, 1);
            else
            {
                Wood(g, Color.FromArgb(150, 92, 58), ox, by, 48, 0, 14, 24, 1);
                Flat(g, Color.FromArgb(50, 0, 0, 0), ox, by, 50, 13, 10, 8, 0.6f);
                Flat(g, Color.FromArgb(50, 0, 0, 0), ox, by, 50, 2.5f, 10, 8, 0.6f);
                Flat(g, pal['Y'], ox, by, 58.2f, 10.6f, 1.6f, 1.6f, 0.8f);
            }
            Panel(g, Color.FromArgb(200, 200, 210), ox, by, 45, 23.6f, 20, 1.8f, 0.6f);
            Panel(g, pal['N'], ox, by, 3, 0, 12, 6, 3);
            Panel(g, pal['N'], ox, by, 95, 0, 12, 6, 3);
        }
        {
            float by = Ground + (1 - Appear(1)) * U(24);
            float ox = A + U(114);
            Shadow(g, ox + U(5.5f), U(13));
            Panel(g, Color.FromArgb(58, 62, 76), ox, by, 0, 0, 11, 19, 1.4f);
            float pulse = (tick % 18 < 4) ? 0.5f : 0;
            RectangleF w1 = R(ox, by, 2 - pulse, 2.5f - pulse, 7 + pulse * 2, 7 + pulse * 2);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(25, 25, 32))) g.FillEllipse(b, w1);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 125, 140))) g.FillEllipse(b, R(ox, by, 4, 4.5f, 3, 3));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(25, 25, 32))) g.FillEllipse(b, R(ox, by, 3.5f, 12, 4, 4));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 125, 140))) g.FillEllipse(b, R(ox, by, 4.5f, 13, 2, 2));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(tick % 18 < 4 ? 255 : 120, 120, 255, 200))) g.FillEllipse(b, R(ox, by, 8.6f, 16.8f, 1, 1));
        }
        {
            float by = Ground + (1 - Appear(3)) * U(14);
            float ox = LoungerX;
            Shadow(g, ox + U(10), U(20));
            using (Pen p = new Pen(Color.FromArgb(150, 156, 170), U(0.6f))) { g.DrawLine(p, ox + U(1.5f), by, ox + U(3), by - U(3.5f)); g.DrawLine(p, ox + U(17), by, ox + U(15.5f), by - U(3.5f)); }
            Panel(g, Color.White, ox, by, 0, 3, 19, 2.4f, 1.1f);
            Panel(g, Color.White, ox, by, 15, 4, 4, 8.5f, 1.6f);
            for (int i = 0; i < 3; i++) Flat(g, Color.FromArgb(90, 160, 240), ox, by, 2.5f + i * 4.5f, 3.6f, 2, 1.2f, 0.3f);
            Flat(g, Color.FromArgb(90, 160, 240), ox, by, 15.8f, 7, 2.4f, 1.6f, 0.4f);
        }
        {
            float by = Ground + (1 - Appear(2)) * U(22);
            float ox = PoolX;
            Shadow(g, ox + U(30), U(66));
            using (Pen p = new Pen(Color.FromArgb(185, 190, 205), U(0.7f)))
            {
                g.DrawLine(p, ox - U(3.6f), by, ox - U(3.6f), by - U(17));
                g.DrawLine(p, ox - U(0.8f), by, ox - U(0.8f), by - U(17));
                for (int i = 0; i < 4; i++) g.DrawLine(p, ox - U(3.6f), by - U(2.5f + i * 4), ox - U(0.8f), by - U(2.5f + i * 4));
            }
            // задняя стенка — плитка
            using (LinearGradientBrush b = new LinearGradientBrush(R(ox, by, 2, 2, 56, 12), Color.FromArgb(215, 238, 250), Color.FromArgb(160, 210, 240), 90f))
                g.FillRectangle(b, R(ox, by, 2, 2, 56, 12));
            using (Pen p = new Pen(Color.FromArgb(50, 60, 120, 170), 1))
                for (int i = 1; i < 14; i++) g.DrawLine(p, ox + U(2 + i * 4), by - U(2), ox + U(2 + i * 4), by - U(14));
        }
    }

    void DrawPoolFront(Graphics g)
    {
        float by = Ground + (1 - Appear(2)) * U(22);
        float ox = PoolX;
        RectangleF wr = R(ox, by, 2, 2, 56, 9.2f);
        using (GraphicsPath p = new GraphicsPath())
        {
            List<PointF> pts = new List<PointF>();
            for (int i = 0; i <= 28; i++)
            {
                float x = wr.X + wr.Width * i / 28f;
                float y = wr.Y + (float)Math.Sin(i * 0.9 + tick * 0.12) * U(0.35f);
                pts.Add(new PointF(x, y));
            }
            pts.Add(new PointF(wr.Right, wr.Bottom));
            pts.Add(new PointF(wr.X, wr.Bottom));
            p.AddPolygon(pts.ToArray());
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(wr.X, wr.Y - U(1), wr.Width, wr.Height + U(1)), Color.FromArgb(150, 110, 200, 255), Color.FromArgb(205, 40, 120, 220), 90f))
                g.FillPath(b, p);
            using (Pen wp = new Pen(Color.FromArgb(220, 235, 250, 255), Math.Max(1, U(0.45f))))
                g.DrawLines(wp, pts.GetRange(0, 29).ToArray());
        }
        for (int i = 0; i < 5; i++)
        {
            float gx = ox + U(6 + ((tick * 0.08f + i * 11) % 48));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(b, gx, by - U(6 + (i % 3) * 1.5f), U(3), U(0.5f));
        }
        Panel(g, Color.FromArgb(236, 240, 246), ox, by, 0, 0, 60, 2.6f, 0.8f);
        Panel(g, Color.FromArgb(236, 240, 246), ox, by, 0, 0, 2.8f, 14.2f, 1);
        Panel(g, Color.FromArgb(236, 240, 246), ox, by, 57.2f, 0, 2.8f, 14.2f, 1);
    }

    // ================= прозрачное окно =================
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; public POINT(int a, int b) { x = a; y = b; } }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; public SIZE(int a, int b) { cx = a; cy = b; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

    // ---- вывод кадра в окно: один постоянный DIB, кадр копируется в него с premultiplied-альфой
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr h, uint flags);

    IntPtr dibDc = IntPtr.Zero, dib = IntPtr.Zero, dibOld = IntPtr.Zero, dibBits = IntPtr.Zero;
    int dibW, dibH; bool dibBroken;

    void FreeDib()
    {
        try
        {
            if (dibDc != IntPtr.Zero && dibOld != IntPtr.Zero) SelectObject(dibDc, dibOld);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (dibDc != IntPtr.Zero) DeleteDC(dibDc);
        }
        catch { }
        dibDc = dib = dibOld = dibBits = IntPtr.Zero; dibW = dibH = 0;
    }

    bool EnsureDib(int w, int h)
    {
        if (dib != IntPtr.Zero && dibW == w && dibH == h) return true;
        FreeDib();
        IntPtr screenDc = GetDC(IntPtr.Zero);
        try
        {
            dibDc = CreateCompatibleDC(screenDc);
            BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr bits;
            dib = CreateDIBSection(dibDc, ref bi, 0, out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero) { FreeDib(); return false; }
            dibBits = bits; dibOld = SelectObject(dibDc, dib); dibW = w; dibH = h;
            return true;
        }
        finally { ReleaseDC(IntPtr.Zero, screenDc); }
    }

    void PushLayered(Bitmap bmp)
    {
        bool ok = false;
        if (!dibBroken && EnsureDib(bmp.Width, bmp.Height))
        {
            try
            {
                BitmapData bd = new BitmapData();
                bd.Width = dibW; bd.Height = dibH; bd.Stride = dibW * 4; bd.PixelFormat = PixelFormat.Format32bppPArgb; bd.Scan0 = dibBits;
                bmp.LockBits(new Rectangle(0, 0, dibW, dibH), ImageLockMode.ReadOnly | ImageLockMode.UserInputBuffer, PixelFormat.Format32bppPArgb, bd);
                bmp.UnlockBits(bd);
                ok = Present(dibDc, dibW, dibH);
            }
            catch (Exception ex) { GuardLog("dib", ex); dibBroken = true; FreeDib(); }
        }
        if (!ok) ok = PushLayeredOld(bmp);
        if (ok) presentFails = 0;
        else if (++presentFails % 30 == 1) { FreeDib(); dibBroken = false; }
    }

    bool Present(IntPtr srcDc, int w, int h)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        try
        {
            SIZE size = new SIZE(w, h);
            POINT src = new POINT(0, 0);
            POINT dst = new POINT(Left, Top);
            BLENDFUNCTION bf = new BLENDFUNCTION();
            bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
            return UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, srcDc, ref src, 0, ref bf, 2);
        }
        finally { ReleaseDC(IntPtr.Zero, screenDc); }
    }

    // запасной путь — как раньше
    bool PushLayeredOld(Bitmap bmp)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
        try
        {
            hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            old = SelectObject(memDc, hBmp);
            return Present(memDc, bmp.Width, bmp.Height);
        }
        catch (Exception ex) { GuardLog("hbitmap", ex); return false; }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
            if (hBmp != IntPtr.Zero) { SelectObject(memDc, old); DeleteObject(hBmp); }
            DeleteDC(memDc);
        }
    }

    // ================= защита от «пустого котика» =================
    int presentFails, renderFails, lostTicks, emptyChecks, guardTick, needReset;
    DateTime lastLog = DateTime.MinValue; int logSkipped;

    static bool Bad(float v) { return float.IsNaN(v) || float.IsInfinity(v); }

    void GuardLog(string what, Exception ex)
    {
        if ((DateTime.Now - lastLog).TotalSeconds < 60) { logSkipped++; return; }
        lastLog = DateTime.Now;
        CrashLog(new Exception("[защита] " + what + (logSkipped > 0 ? " (ещё " + logSkipped + " похожих за минуту)" : ""), ex));
        logSkipped = 0;
    }

    // состояние игры: NaN-координаты, котик спрятан или ушёл за край слишком долго
    void GameGuard()
    {
        if (me == null) return;
        bool broken = Bad(me.X) || Bad(me.Y) || Bad(me.TX) || Bad(me.Z) || Bad(me.TZ) || Bad(me.Scale) || me.Scale <= 0.05f;
        bool lost = !roaming && !chasing && (me.Hidden || me.X < -U(25) || me.X > Width + U(25));
        lostTicks = lost ? lostTicks + 1 : 0;
        if (broken || lostTicks > 30 * 90)
        {
            GuardLog("котик потерялся: broken=" + broken + " mode=" + mode + " X=" + me.X, null);
            ResetCat(); lostTicks = 0;
        }
        for (int i = guests.Count - 1; i >= 0; i--)
        {
            Cat c = guests[i];
            if (Bad(c.X) || Bad(c.Y) || Bad(c.Z) || Bad(c.Scale)) { c.X = Width * 0.5f; c.Y = 0; c.TX = c.X; c.Z = c.TZ = 0; if (Bad(c.Scale) || c.Scale <= 0.05f) c.Scale = 1; }
        }
    }

    void ResetCat()
    {
        try { SetMode(0); } catch (Exception ex) { GuardLog("SetMode", ex); }
        me.X = Width * 0.6f; me.TX = me.X; me.Y = 0; me.Z = me.TZ = 0; me.Scale = 1f; me.Hidden = false;
        me.Jumping = false; me.Walking = false; me.Sitting = false; me.Swimming = false; me.Lying = false; me.Cuffed = false;
    }

    // окно: видимо, не свёрнуто, на экране и поверх всех; кадр не пустой
    void WindowGuard()
    {
        guardTick++;
        if (needReset > 0) { needReset--; if (needReset == 0) ResetRendering("смена сеанса/экрана"); }
        if (guardTick % 60 != 0) return;
        try
        {
            if (!IsWindowVisible(Handle) || IsIconic(Handle)) { ShowWindow(Handle, 4); GuardLog("окно было скрыто", null); }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (!wa.IntersectsWith(Bounds) || Bounds.Bottom != wa.Bottom) PlaceWindow();
            if (!NoteOver && !MenuOpen) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
            if (bigOpen && bigWin != null) SetWindowPos(bigWin.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            if (guardTick % 300 == 0)
            {
                uint gdi = GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle, 0);
                if (gdi > 6000) { GuardLog("слишком много GDI-объектов: " + gdi, null); RestartSelf(); return; }
            }
        }
        catch (Exception ex) { GuardLog("окно", ex); }
        // пустой кадр: в buf не нарисовано ни одного пикселя
        if (!roaming && !chasing && buf != null && FrameEmpty(buf))
        {
            emptyChecks++;
            if (emptyChecks == 4) { GuardLog("пустой кадр 8 c, перезапуск сцены", null); me.Hidden = false; try { SetMode(mode); } catch { } }
            else if (emptyChecks == 8) { GuardLog("пустой кадр 16 c, сброс котика", null); ResetCat(); ResetRendering("пустой кадр"); }
            else if (emptyChecks >= 14) { GuardLog("пустой кадр 28 c, перезапуск программы", null); RestartSelf(); }
        }
        else emptyChecks = 0;
    }

    bool FrameEmpty(Bitmap b)
    {
        BitmapData d = null;
        try
        {
            d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int n = d.Stride * b.Height; byte[] px = new byte[n];
            Marshal.Copy(d.Scan0, px, 0, n);
            for (int i = 3; i < n; i += 16) if (px[i] > 8) return false;
            return true;
        }
        catch { return false; }
        finally { if (d != null) b.UnlockBits(d); }
    }

    void ResetRendering(string why)
    {
        try
        {
            FreeDib(); dibBroken = false;
            if (buf != null) { buf.Dispose(); buf = null; }
            if (buf2 != null) { buf2.Dispose(); buf2 = null; }
            if (buf3 != null) { buf3.Dispose(); buf3 = null; }
            PlaceWindow();
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
        }
        catch (Exception ex) { GuardLog("сброс отрисовки (" + why + ")", ex); }
        GC.Collect(); GC.WaitForPendingFinalizers();
    }

    void RenderFailed(Exception ex)
    {
        renderFails++;
        GuardLog("кадр не отрисовался (" + renderFails + " подряд)", ex);
        if (renderFails % 15 == 0) ResetRendering("ошибки отрисовки");
        // при заблокированном экране ошибки ожидаемы — не перезапускаемся, ждём
        if (renderFails >= 30 * 60 && !Locked()) RestartSelf();
    }

    static bool Locked()
    {
        try { foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName("LogonUI")) return true; } catch { }
        return false;
    }

    bool restarting;
    void RestartSelf()
    {
        if (restarting) return; restarting = true;
        try
        {
            CrashLog(new Exception("[защита] перезапуск котика"));
            string vbs = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VapePet"), "start-pet.vbs");
            try
            {
                // папка, откуда запущен котик (powershell ... -File "...\vape-pet.ps1")
                foreach (string arg in Environment.GetCommandLineArgs())
                    if (arg.EndsWith("vape-pet.ps1", StringComparison.OrdinalIgnoreCase))
                    {
                        string own = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(arg)), "start-pet.vbs");
                        if (File.Exists(own)) vbs = own;
                    }
            }
            catch { }
            string exe = Application.ExecutablePath;
            bool asExe = !Path.GetFileName(exe).StartsWith("powershell", StringComparison.OrdinalIgnoreCase);
            if (asExe)
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c timeout /t 3 /nobreak >nul & start \"\" \"" + exe + "\"");
                psi.CreateNoWindow = true; psi.UseShellExecute = false; psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                System.Diagnostics.Process.Start(psi);
            }
            else if (File.Exists(vbs))
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c timeout /t 3 /nobreak >nul & start \"\" wscript.exe \"" + vbs + "\"");
                psi.CreateNoWindow = true; psi.UseShellExecute = false; psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                System.Diagnostics.Process.Start(psi);
            }
        }
        catch { }
        try { timer.Stop(); FreeDib(); } catch { }
        Application.Exit();
    }

    // ================= мышь =================
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && RackGrabbable(e.Location))
        {
            grabRack = true; grabMoved = false; grabX0 = e.X; shakeEnergy = 0; rackWobble = 0;
            return;
        }
        if (e.Button == MouseButtons.Left)
        {
            mouseDown = true; dragging = false;
            downPt = Cursor.Position;
            dragOffset = e.X - me.X;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (grabRack)
        {
            if (rackFalling || rackFallen) { grabRack = false; return; }
            if (Math.Abs(e.X - grabX0) > 3) grabMoved = true;
            float target = Math.Max(-13f, Math.Min(17f, (e.X - grabX0) / U(1.1f)));
            shakeEnergy += Math.Abs(target - rackAngle);
            rackAngle = target;
            while (shakeEnergy > 22) { shakeEnergy -= 22; RackShakeEvent(false); }
            if (rackAngle >= 16.5f) { grabRack = false; StartRackFall(true); }
            return;
        }
        if (!mouseDown) return;
        if (!dragging && mode == 0 && pendingMode < 0 &&
            (Math.Abs(Cursor.Position.X - downPt.X) > 4 || Math.Abs(Cursor.Position.Y - downPt.Y) > 4))
            dragging = true;
        if (dragging) me.X = Math.Max(U(12), Math.Min(Width - U(12), e.X - dragOffset));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        if (grabRack)
        {
            grabRack = false;
            if (!rackFalling && !rackFallen)
            {
                wobbleAmp = grabMoved ? (Math.Abs(rackAngle) < 3 ? 3 * Math.Sign(rackAngle + 0.01f) : rackAngle) : 3;
                rackWobble = 40;
                if (!grabMoved) RackShakeEvent(true);
            }
            return;
        }
        bool wasDrag = dragging;
        mouseDown = false; dragging = false;
        if (wasDrag) { me.Phase = 1; me.T = 40; }
        else if (PanelClick(e.Location) || UsefulClick()) { }
        else if (!SceneClick(e.Location)) ShowMenu(Cursor.Position);
    }

    // ================= запуск =================
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

    bool lmbWasDown;
    // меню закрывается левым кликом в любом месте вне него (в т.ч. по котику)
    void MenuClickAway()
    {
        MenuWatch();
        if (DateTime.Now < talkUntil && me != null && me.HappyT < 10) me.HappyT = 10;   // котик довольный, пока говорит
    }

    static bool InMenu(ToolStripDropDown dd, Point p)
    {
        if (!dd.Visible) return false;
        if (dd.Bounds.Contains(p)) return true;
        foreach (ToolStripItem it in dd.Items)
        {
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            if (mi != null && mi.HasDropDownItems && mi.DropDown.Visible && InMenu(mi.DropDown, p)) return true;
        }
        return false;
    }

    // ================= прогулка по экрану: окна, крестик, часы, курсор =================
    // Котик выходит из своего уголка в маленькое прозрачное окно, которое движется вместе с ним поверх всего.
    // Ничего не нажимает: только сидит на окнах, «тыкает» крестик понарошку, «уносит» часы и ловит курсор.

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string name);
    [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc, int x, int y);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);

    class RWin { public IntPtr H; public Rectangle R; }

    // ---- окружение (в тестовом режиме — выдуманные окна, без Windows)
    public static bool RoamFake;
    internal List<Rectangle> fakeWins = new List<Rectangle>();
    public Rectangle fakeClock; public Point fakeCursor = new Point(-9999, -9999);
    static readonly uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

    Rectangle ScreenWork { get { return RoamFake ? new Rectangle(0, 0, 1920, 1040) : Screen.PrimaryScreen.WorkingArea; } }
    public int FakeShift { get { return RoamFake ? Screen.PrimaryScreen.WorkingArea.Bottom - 1040 : 0; } }
    Rectangle ScreenFull { get { return Screen.PrimaryScreen.Bounds; } }
    float Dpi1 { get { return dpi / 96f; } }

    List<RWin> FindWindowsOnScreen()
    {
        List<RWin> res = new List<RWin>();
        if (RoamFake) { for (int i = 0; i < fakeWins.Count; i++) { RWin w = new RWin(); w.H = new IntPtr(i + 1); w.R = fakeWins[i]; res.Add(w); } return res; }
        Rectangle full = ScreenFull;
        EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            try
            {
                if (!IsWindowVisible(h) || IsIconic(h) || IsZoomed(h) || GetWindowTextLength(h) == 0) return true;
                if ((GetWindowLong(h, -20) & 0x80) != 0) return true;           // окна-инструменты
                uint pid; GetWindowThreadProcessId(h, out pid); if (pid == myPid) return true;
                int cloaked; if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
                StringBuilder sb = new StringBuilder(64); GetClassName(h, sb, 64); string cls = sb.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return true;
                Rectangle r; if (!WinRect(h, out r)) return true;
                if (r.Width < 220 || r.Height < 120) return true;
                if (r.Top < full.Top + 70 || r.Top > ScreenWork.Bottom - 60) return true;   // котику должно хватать места над окном
                if (r.Right < full.Left + 120 || r.Left > full.Right - 120) return true;     // только основной монитор
                RWin w = new RWin(); w.H = h; w.R = r; res.Add(w);
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return res;
    }

    bool WinRect(IntPtr h, out Rectangle r)
    {
        r = Rectangle.Empty;
        try { return WinRect0(h, out r); } catch { return false; }
    }
    bool WinRect0(IntPtr h, out Rectangle r)
    {
        r = Rectangle.Empty;
        if (RoamFake) { int i = h.ToInt32() - 1; if (i < 0 || i >= fakeWins.Count) return false; r = fakeWins[i]; return true; }
        if (!IsWindow(h) || !IsWindowVisible(h) || IsIconic(h)) return false;
        RECT rc;
        if (DwmGetWindowAttribute(h, 9, out rc, 16) != 0 && !GetWindowRect(h, out rc)) return false;
        r = Rectangle.FromLTRB(rc.L, rc.T, rc.R, rc.B);
        return r.Width > 0 && r.Height > 0;
    }

    // точка на окне не закрыта другими окнами?
    bool PointOnWin(IntPtr h, int x, int y)
    {
        try { return PointOnWin0(h, x, y); } catch { return false; }
    }
    bool PointOnWin0(IntPtr h, int x, int y)
    {
        if (RoamFake)
        {
            int i = h.ToInt32() - 1;
            for (int k = i + 1; k < fakeWins.Count; k++) if (fakeWins[k].Contains(x, y)) return false;
            return i >= 0 && i < fakeWins.Count && fakeWins[i].Contains(x, y);
        }
        IntPtr w = WindowFromPoint(new POINT(x, y));
        if (w == IntPtr.Zero) return false;
        IntPtr root = GetAncestor(w, 2);
        if (root == h || w == h) return true;
        uint pid; GetWindowThreadProcessId(root, out pid);
        return pid == myPid;   // свой кот и своё окно не в счёт
    }

    Rectangle ClockRect()
    {
        if (RoamFake) return fakeClock;
        Rectangle found = Rectangle.Empty;
        try
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero)
            {
                EnumChildWindows(tray, delegate (IntPtr h, IntPtr l)
                {
                    StringBuilder sb = new StringBuilder(64); GetClassName(h, sb, 64);
                    if (sb.ToString() == "TrayClockWClass") { RECT r; if (GetWindowRect(h, out r)) found = Rectangle.FromLTRB(r.L, r.T, r.R, r.B); return false; }
                    return true;
                }, IntPtr.Zero);
                if (found.IsEmpty) { RECT t; if (GetWindowRect(tray, out t) && t.B - t.T < 120) found = Rectangle.FromLTRB(t.R - (int)(119 * Dpi1), t.T, t.R - (int)(53 * Dpi1), t.B); }
            }
        }
        catch { }
        return found;
    }

    Point CursorNow() { if (RoamFake) return fakeCursor; try { return Cursor.Position; } catch { return new Point(-9999, -9999); } }

    Color SampleScreen(int x, int y)
    {
        if (RoamFake) return Color.FromArgb(32, 34, 42);
        IntPtr dc = GetDC(IntPtr.Zero);
        try { uint c = GetPixel(dc, x, y); if (c == 0xFFFFFFFF) return Color.FromArgb(32, 34, 42); return Color.FromArgb((int)(c & 0xFF), (int)((c >> 8) & 0xFF), (int)((c >> 16) & 0xFF)); }
        finally { ReleaseDC(IntPtr.Zero, dc); }
    }

    // ---- состояние прогулки
    public bool roaming; public int forcePlan = -1;
    public bool interfacePlay = true;
    Cat rc;                                   // кот на экране (его X/Y внутри не используются: позиция — rx, ry)
    public float rx, ry, rvy;                         // лапы кота в пикселях экрана
    IntPtr rSurf = IntPtr.Zero;                       // на чём стоит: Zero — панель задач, иначе окно
    Rectangle rSurfRect;
    List<Func<bool>> rq = new List<Func<bool>>();
    public string rSay; int rSayT;
    public bool rCarryClock, rHole; public Rectangle rHoleRect; public Color rHoleCol;
    public PointF rTapAt; public int rTapT;
    public float rRot; public int rPose; public bool fakeMax;
    int roamT, roamPounces; Point lastCursor; int cursorIdle;
    List<Particle> rParts = new List<Particle>();
    RoamWin roamWin; Form holeWin;

    float TaskbarY { get { return ScreenWork.Bottom; } }
    float FeetScreenY(Cat c) { return Top - FakeShift + Ground + ZY(c.Z); }

    public bool StartRoam(bool force)
    {
        if (roaming || mode != 0 || pendingMode >= 0 || egg != null || dragging || musicT > 0 || movieT > 0 || aiBusy) return false;
        if (me.Jumping || me.Sitting || me.Hidden) return false;
        rc = me.Clone();
        rc.X = 0; rc.TX = 0; rc.Y = 0; rc.Z = rc.TZ = 0; rc.Walking = false; rc.Jumping = false; rc.Sitting = false; rc.Hold = ' ';
        rc.HappyT = 0; rc.AngryT = 0; rc.SweatT = 0; rc.StompT = 0; rc.PuffT = 0; rc.View = 1; rc.LandT = 0; rc.TurnT = 0; rc.JDone = null;
        rx = Left + me.X; ry = FeetScreenY(me); rvy = 0; rSurf = IntPtr.Zero;
        rq.Clear(); rSay = null; rSayT = 0; rCarryClock = false; rHole = false; rTapT = 0; roamT = 0; roamPounces = 0; rParts.Clear(); rRot = 0; rPose = 0; falling = false;
        me.Hidden = true; me.Walking = false; me.PuffT = 0;
        texts.Clear(); parts.RemoveAll(delegate (Particle p) { return p.Kind == 0; });
        roaming = true;
        try { BuildRoamPlan(force); }
        catch (Exception ex) { GuardLog("план прогулки", ex); EndRoam(); return false; }
        if (!RoamFake)
        {
            try
            {
                if (roamWin == null || roamWin.IsDisposed) roamWin = new RoamWin(this);
                roamWin.Render();
                if (!roamWin.Visible) roamWin.Show();
                roamHiddenT = 0;
            }
            catch (Exception ex) { GuardLog("окно прогулки", ex); EndRoam(); return false; }
        }
        return true;
    }

    public void EndRoam()
    {
        if (!roaming) return;
        roaming = false; rq.Clear(); rCarryClock = false; HideHole();
        // котик возвращается в свой уголок ровно туда, где стоит
        float lx = rx - Left;
        me.X = Math.Max(U(12), Math.Min(Width - U(12), lx)); me.TX = me.X; me.Z = me.TZ = 1f; me.Y = 0;
        me.Dir = rc != null ? rc.Dir : me.Dir; me.Hidden = false; me.Walking = false; me.Phase = 1; me.T = 60;
        try { if (roamWin != null) roamWin.Hide(); } catch { }
    }

    void BuildRoamPlan(bool force)
    {
        // сначала спрыгнуть на линию панели задач
        rq.Add(RJump(rx, TaskbarY, IntPtr.Zero, U(5)));
        List<int> opts = new List<int>();
        List<RWin> wins = new List<RWin>(); Rectangle clock = Rectangle.Empty;
        try { wins = FindWindowsOnScreen(); } catch (Exception ex) { GuardLog("поиск окон", ex); }
        try { clock = ClockRect(); } catch (Exception ex) { GuardLog("поиск часов", ex); }
        bool maxX = false;
        try { maxX = MaxCloseVisible(); } catch (Exception ex) { GuardLog("развёрнутое окно", ex); }
        if (wins.Count > 0) { opts.Add(0); opts.Add(0); opts.Add(1); }
        if (!clock.IsEmpty) opts.Add(2);
        opts.Add(4);
        if (maxX) { opts.Add(4); opts.Add(4); }
        int pick = forcePlan >= 0 ? forcePlan : opts[rnd.Next(opts.Count)];
        if ((pick == 0 || pick == 1) && wins.Count == 0) pick = 4;
        if (pick == 2 && clock.IsEmpty) pick = 4;
        if (pick == 0 || pick == 1) PlanWindow(wins, pick == 1);
        else if (pick == 2) PlanClock(clock);
        else if (pick == 4) PlanClimb(maxX);
        else PlanStroll();
        PlanHome();
    }

    void PlanWindow(List<RWin> wins, bool wantTap)
    {
        // окно, у которого виден верхний край
        for (int tries = 0; tries < 12; tries++)
        {
            RWin w = wins[rnd.Next(wins.Count)];
            Rectangle full = ScreenFull;
            int x0 = Math.Max(w.R.Left + (int)U(14), full.Left + (int)U(14)), x1 = Math.Min(w.R.Right - (int)U(40), full.Right - (int)U(14));
            if (x1 <= x0) continue;
            int x = x0 + rnd.Next(x1 - x0);
            if (!PointOnWin(w.H, x, w.R.Top + 3)) continue;
            int bx = w.R.Right - (int)(23 * Dpi1), by = w.R.Top + (int)(15 * Dpi1);
            bool tapOk = PointOnWin(w.H, bx, by) && bx < full.Right - (int)U(8);
            IntPtr h = w.H;
            rq.Add(RWalk(x));
            rq.Add(RSayStep(rnd.Next(2) == 0 ? "Хоп!" : "Залезу-ка", 30));
            rq.Add(RJumpWin(h, x));
            rq.Add(RSayStep(new string[] { "Отсюда всё видно", "Тёпленькое окошко", "Посижу тут", "Чем занят?" }[rnd.Next(4)], 70));
            rq.Add(RSit(150 + rnd.Next(150)));
            if (rnd.Next(100) < 45 && rc.Vape) rq.Add(RPuff());
            if ((wantTap || rnd.Next(100) < 35) && tapOk)
            {
                rq.Add(RWalkTo(delegate { Rectangle r; return WinRect(h, out r) ? r.Right - 23 * Dpi1 : rx; }));
                rq.Add(RFace(0));
                rq.Add(RSayStep("А это что за кнопка?", 60));
                rq.Add(RTap(h)); rq.Add(RWait(14)); rq.Add(RTap(h));
                rq.Add(RDo(delegate { rc.HappyT = 50; }));
                rq.Add(RSayStep("Шучу, не закрою", 70));
            }
            rq.Add(RJumpDown());
            return;
        }
        PlanStroll();
    }

    void PlanClock(Rectangle clock)
    {
        float cx = clock.Left + clock.Width / 2f;
        rq.Add(RWalk(cx));
        rq.Add(RFace(0));
        rq.Add(RSayStep("О, часики!", 45));
        rq.Add(RDo(delegate
        {
            Rectangle c2 = ClockRect(); if (c2.IsEmpty) c2 = clock;
            rHoleRect = c2; rHoleCol = SampleScreen(c2.Left + 2, c2.Top + 2);
            ShowHole(); rCarryClock = true; rc.HappyT = 9999; rc.LandT = 6;
        }));
        rq.Add(RWait(20));
        rq.Add(RWalk(Math.Max(ScreenFull.Left + U(40), cx - U(80))));
        rq.Add(RFace(0));
        rq.Add(RSayStep(new string[] { "Теперь время моё!", "Который час? Не скажу", "Часы котика" }[rnd.Next(3)], 80));
        rq.Add(RWait(40));
        rq.Add(RWalk(cx));
        rq.Add(RDo(delegate { rCarryClock = false; rc.HappyT = 0; rc.LandT = 6; HideHole(); }));
        rq.Add(RSayStep("Ладно, держите", 50));
    }

    // крестик развёрнутого окна в правом верхнем углу виден?
    bool MaxCloseVisible()
    {
        if (RoamFake) return fakeMax;
        Rectangle full = ScreenFull;
        int x = full.Right - (int)(23 * Dpi1), y = full.Top + (int)(15 * Dpi1);
        IntPtr w = WindowFromPoint(new POINT(x, y));
        if (w == IntPtr.Zero) return false;
        IntPtr root = GetAncestor(w, 2);
        uint pid; GetWindowThreadProcessId(root, out pid);
        return pid != myPid && IsZoomed(root) && IsWindowVisible(root);
    }

    void PlanClimb(bool tapMax)
    {
        Rectangle full = ScreenFull;
        float wallX = full.Right - 1, ceilY = full.Top + 1;
        rq.Add(RWalk(full.Right - U(12)));
        rq.Add(RSayStep(new string[] { "Полезу-ка наверх", "А что там сверху?", "Скалолаз!" }[rnd.Next(3)], 40));
        rq.Add(RTurn(-90, 12, wallX, float.NaN, 1));
        rq.Add(RClimbTo(ceilY + U(8)));
        rq.Add(RTurn(-180, 12, wallX, ceilY, 2));
        if (tapMax)
        {
            PointF btn = new PointF(full.Right - 23 * Dpi1, full.Top + 15 * Dpi1);
            rq.Add(RCeilTo(btn.X));
            rq.Add(RSayStep("А это что за кнопка?", 60));
            rq.Add(RTapAt(btn)); rq.Add(RWait(14)); rq.Add(RTapAt(btn));
            rq.Add(RDo(delegate { rc.HappyT = 50; }));
            rq.Add(RSayStep("Шучу, не закрою", 70));
        }
        rq.Add(RCeilTo(full.Right - U(50) - rnd.Next((int)U(60))));
        rq.Add(RSayStep(new string[] { "Я как муха", "Вид сверху — красота", "Ой, голова кружится" }[rnd.Next(3)], 70));
        rq.Add(RWait(20));
        rq.Add(RDo(delegate { StartFall("Аааа!"); }));
        rq.Add(RWait(10));
        rq.Add(RSayStep("Приземлился!", 40));
    }

    // поворот у угла: плавно к углу rot, лапы едут к (x, y); pose — новая поверхность
    Func<bool> RTurn(float rot, int ticks, float x, float y, int pose)
    {
        int n = 0; float r0 = 0, x0 = 0, y0 = 0;
        return delegate
        {
            if (n == 0) { r0 = rRot; x0 = rx; y0 = ry; rc.Walking = false; rc.View = 1; rc.Dir = 1; }
            n++;
            float k = Math.Min(1f, n / (float)ticks), e = k * k * (3 - 2 * k);
            rRot = r0 + (rot - r0) * e;
            if (!float.IsNaN(x)) rx = x0 + (x - x0) * e;
            if (!float.IsNaN(y)) ry = y0 + (y - y0) * e;
            if (k >= 1f) { rPose = pose; rc.LandT = 6; return true; }
            return false;
        };
    }
    Func<bool> RClimbTo(float y)
    {
        return delegate
        {
            float d = y - ry, sp = U(1.1f);
            if (Math.Abs(d) <= sp) { ry = y; rc.Walking = false; return true; }
            ry += Math.Sign(d) * sp; rc.Walking = true; rc.Dir = d < 0 ? 1 : -1; rc.View = 1;
            return false;
        };
    }
    Func<bool> RCeilTo(float x)
    {
        return delegate
        {
            float d = x - rx, sp = U(0.9f);
            if (Math.Abs(d) <= sp) { rx = x; rc.Walking = false; return true; }
            rx += Math.Sign(d) * sp; rc.Walking = true; rc.Dir = d < 0 ? 1 : -1; rc.View = 1;
            return false;
        };
    }
    Func<bool> RTapAt(PointF at)
    {
        int n = 0;
        return delegate
        {
            n++;
            if (n == 1) rc.LandT = 8;
            if (n == 6) { rTapAt = at; rTapT = 20; rSay = "Тык!"; rSayT = 24; rc.LandT = 8; }
            return n >= 16;
        };
    }

    void PlanStroll()
    {
        Rectangle full = ScreenFull;
        rq.Add(RWalk(Math.Max(full.Left + U(30), rx - U(60) - rnd.Next((int)U(80)))));
        rq.Add(RSayStep("Прогуляюсь", 40));
        rq.Add(RWait(40));
    }

    void PlanHome()
    {
        rq.Add(RWalkTo(delegate { return Math.Max(Left + U(20), Math.Min(Right - U(20), rx)); }));
        rq.Add(RDo(delegate { EndRoam(); }));
    }

    // ---- шаги прогулки
    Func<bool> RWait(int t) { int n = t; return delegate { return --n <= 0; }; }
    Func<bool> RDo(Action a) { return delegate { a(); return true; }; }
    Func<bool> RFace(int view) { return delegate { rc.View = view; return true; }; }
    Func<bool> RSayStep(string text, int t)
    {
        bool said = false; int n = t;
        return delegate { if (!said) { rSay = text; rSayT = t + 20; said = true; } return --n <= 0; };
    }
    Func<bool> RWalk(float x) { return RWalkTo(delegate { return x; }); }
    Func<bool> RWalkTo(Func<float> fx)
    {
        return delegate
        {
            float x = fx();
            float lo, hi; SurfRange(out lo, out hi); x = Math.Max(lo, Math.Min(hi, x));
            float d = x - rx, sp = U(1.25f);
            if (Math.Abs(d) <= sp) { rx = x; rc.Walking = false; return true; }
            rc.Dir = Math.Sign(d); rx += Math.Sign(d) * sp; rc.Walking = true; rc.View = 1;
            return false;
        };
    }
    Func<bool> RSit(int t)
    {
        int n = t;
        return delegate
        {
            if (n == t) { rc.Sitting = true; rc.Walking = false; }
            if (--n <= 0) { rc.Sitting = false; return true; }
            return false;
        };
    }
    Func<bool> RPuff() { bool st = false; return delegate { if (!st) { rc.PuffT = 90; st = true; } return rc.PuffT <= 0; }; }
    Func<bool> RTap(IntPtr h)
    {
        int n = 0;
        return delegate
        {
            n++;
            if (n == 1) { rc.LandT = 8; }
            if (n == 6)
            {
                Rectangle r; if (WinRect(h, out r)) rTapAt = new PointF(r.Right - 23 * Dpi1, r.Top + 15 * Dpi1); else rTapAt = new PointF(rx, ry + 15 * Dpi1);
                rTapT = 20; rSay = "Тык!"; rSayT = 24; rc.LandT = 8;
            }
            return n >= 16;
        };
    }

    // прыжок по дуге в точку (tx, ty), после приземления стоит на surf
    Func<bool> RJump(float tx, float ty, IntPtr surf, float hgt)
    {
        float x0 = 0, y0 = 0, dur = 1; int t = -1;
        return delegate
        {
            if (t < 0)
            {
                x0 = rx; y0 = ry; t = 0;
                float dist = (float)Math.Sqrt((tx - x0) * (tx - x0) + (ty - y0) * (ty - y0));
                dur = Math.Max(14, Math.Min(46, dist / U(3.2f)));
                rc.Dir = tx >= x0 ? 1 : -1; rc.Sitting = false; rc.Walking = false; rc.View = 1;
                rc.Jumping = true; rc.JDur = (int)dur; rc.JT = 0; rSurf = IntPtr.Zero;
            }
            t++; rc.JT = t;
            float k = Math.Min(1f, t / dur), top = Math.Min(y0, ty) - hgt;
            // парабола через вершину top
            float a = y0 - top, b = ty - top, s = (float)Math.Sqrt(Math.Max(0.01f, a)) / ((float)Math.Sqrt(Math.Max(0.01f, a)) + (float)Math.Sqrt(Math.Max(0.01f, b)));
            rx = x0 + (tx - x0) * k;
            ry = k < s ? top + a * (1 - k / s) * (1 - k / s) : top + b * ((k - s) / (1 - s)) * ((k - s) / (1 - s));
            if (k >= 1f)
            {
                rx = tx; ry = ty; rc.Jumping = false; rc.LandT = 8; rSurf = surf;
                if (surf != IntPtr.Zero) WinRect(surf, out rSurfRect);
                return true;
            }
            return false;
        };
    }

    Func<bool> RJumpWin(IntPtr h, float x)
    {
        Func<bool> inner = null;
        return delegate
        {
            if (inner == null)
            {
                Rectangle r; if (!WinRect(h, out r)) return true;
                inner = RJump(x, r.Top, h, U(16));
            }
            return inner();
        };
    }

    Func<bool> RJumpDown()
    {
        Func<bool> inner = null;
        return delegate
        {
            if (rSurf == IntPtr.Zero) return true;
            if (inner == null)
            {
                float tx = rx + rc.Dir * U(22);
                Rectangle full = ScreenFull; tx = Math.Max(full.Left + U(14), Math.Min(full.Right - U(14), tx));
                inner = RJump(tx, TaskbarY, IntPtr.Zero, U(10));
            }
            return inner();
        };
    }

    void SurfRange(out float lo, out float hi)
    {
        Rectangle full = ScreenFull;
        lo = full.Left + U(10); hi = full.Right - U(10);
        if (rSurf != IntPtr.Zero) { lo = Math.Max(lo, rSurfRect.Left + U(6)); hi = Math.Min(hi, rSurfRect.Right - U(6)); }
    }

    // ---- кадр прогулки
    int roamHiddenT;
    public void RoamStep()
    {
        if (!roaming) return;
        roamT++;
        // страховка: окно прогулки должно быть видно, иначе кот возвращается в уголок
        if (!RoamFake)
        {
            bool vis = false;
            try { vis = roamWin != null && !roamWin.IsDisposed && roamWin.Visible && IsWindowVisible(roamWin.Handle); } catch { }
            if (!vis)
            {
                try { if (roamWin != null && !roamWin.IsDisposed) roamWin.Show(); } catch { }
                if (++roamHiddenT > 30) { GuardLog("окно прогулки не видно — кот вернулся", null); EndRoam(); return; }
            }
            else roamHiddenT = 0;
        }
        // таймеры кота (свои, без логики уголка)
        if (rc.BlinkT > 0) rc.BlinkT--; else if (rnd.Next(300) == 0) rc.BlinkT = rnd.Next(4) == 0 ? 16 : 6;
        if (rc.LandT > 0) rc.LandT--;
        if (rc.TurnT > 0) rc.TurnT--;
        if (rc.Dir != rc.LastDir || rc.View != rc.LastView) { rc.TurnT = 6; rc.LastDir = rc.Dir; rc.LastView = rc.View; }
        if (rc.HappyT > 0 && rc.HappyT < 9000) rc.HappyT--;
        if (rc.PuffT > 0)
        {
            rc.PuffT--;
            if (rc.PuffT < 50 && rc.PuffT > 18 && rc.PuffT % 3 == 0)
            {
                Particle p = new Particle(); p.Kind = 0;
                p.X = rx + rc.Dir * U(6.5f) * rc.Scale; p.Y = ry - U(15.5f) * rc.Scale;
                p.VX = rc.Dir * U(0.35f) + (float)(rnd.NextDouble() - 0.5) * U(0.2f); p.VY = -U(0.25f) - (float)rnd.NextDouble() * U(0.2f);
                p.R = U(1.2f); p.Max = p.Life = 50; rParts.Add(p);
            }
        }
        for (int i = rParts.Count - 1; i >= 0; i--) { Particle p = rParts[i]; p.X += p.VX; p.Y += p.VY; p.R += U(0.06f); if (--p.Life <= 0) rParts.RemoveAt(i); }
        if (rSayT > 0 && --rSayT == 0) rSay = null;
        if (rTapT > 0) rTapT--;

        // стоим на окне: окно двигают — едем вместе с ним; закрыли или перекрыли — падаем
        if (rSurf != IntPtr.Zero && !rc.Jumping)
        {
            Rectangle r;
            bool ok = WinRect(rSurf, out r);
            if (ok && (r.Left != rSurfRect.Left || r.Top != rSurfRect.Top)) { rx += r.Left - rSurfRect.Left; ry = r.Top; }
            if (ok) rSurfRect = r;
            if (ok && roamT % 10 == 0 && !PointOnWin(rSurf, (int)rx, r.Top + 3)) ok = false;
            if (!ok || rx < r.Left || rx > r.Right) StartFall("Ай!");
        }

        // падение
        if (falling)
        {
            rvy += U(0.32f); ry += rvy; rc.Jumping = true; rc.JDur = 20; rc.JT = 10;
            rRot += (0 - rRot) * 0.12f; if (Math.Abs(rRot) < 1) rRot = 0;
            if (ry >= TaskbarY) { ry = TaskbarY; rvy = 0; falling = false; rc.Jumping = false; rc.LandT = 8; rSurf = IntPtr.Zero; rc.SweatT = 40; rRot = 0; }
            return;
        }

        // курсор: смотрит на него, а если мышка замерла рядом — ловит
        Point cp = CursorNow();
        if (cp == lastCursor) cursorIdle++; else { cursorIdle = 0; lastCursor = cp; }
        float dx = cp.X - rx, dy = cp.Y - ry;
        bool near = rPose == 0 && Math.Abs(dx) < U(70) && dy > -U(60) && dy < U(4);
        if (near && !rc.Walking && !rc.Jumping && !rc.Sitting && !rCarryClock && rTapT == 0) { int d = dx >= 0 ? 1 : -1; if (d != rc.Dir) rc.Dir = d; rc.View = 1; }
        if (near && cursorIdle > 40 && roamPounces < 2 && !rc.Jumping && !rCarryClock && rq.Count > 0 && Math.Abs(dx) > U(10) && rnd.Next(60) == 0)
        {
            roamPounces++; cursorIdle = 0;
            IntPtr back = rSurf; float floorY = rSurf != IntPtr.Zero ? rSurfRect.Top : TaskbarY;
            rq.Insert(0, RJump(cp.X, Math.Min(floorY, cp.Y + U(10)), IntPtr.Zero, U(8)));
            rq.Insert(1, RDo(delegate { rSay = "Поймал!"; rSayT = 50; rc.HappyT = 40; StartFallTo(back); }));
            rq.Insert(2, RWait(20));
        }

        // очередь действий
        float bx = rx, by = ry;
        int guard = 0;
        while (rq.Count > 0 && guard++ < 6)
        {
            Func<bool> f0 = rq[0];
            if (!f0()) break;
            if (rq.Count > 0 && rq[0] == f0) rq.RemoveAt(0);
        }
        // стоит на месте — не перебирает лапами (шаг мог прерваться ожиданием, «Тык!», прыжком)
        if (rc.Walking && Math.Abs(rx - bx) < 0.05f && Math.Abs(ry - by) < 0.05f) { if (++rStillT >= 2) rc.Walking = false; }
        else rStillT = 0;
        if (rq.Count == 0 && roaming) EndRoam();
        if (roamT > 30 * 120 && roaming && !falling) { rq.Clear(); if (rPose != 0) StartFall("Ой!"); else if (rSurf != IntPtr.Zero) rq.Add(RJumpDown()); else if (ry < TaskbarY - 1) StartFall(null); PlanHome(); roamT = 0; }
    }

    bool falling; int rStillT;
    void StartFall(string say) { falling = true; rvy = 0; rSurf = IntPtr.Zero; rPose = 0; if (rx > ScreenFull.Right - U(8)) rx = ScreenFull.Right - U(8); rc.Sitting = false; rc.Walking = false; if (say != null) { rSay = say; rSayT = 40; } rc.SweatT = 60; }
    // после прыжка за курсором: вернуться на окно, если под лапами оно, иначе падать на панель
    void StartFallTo(IntPtr back)
    {
        Rectangle r;
        if (back != IntPtr.Zero && WinRect(back, out r) && rx > r.Left + U(4) && rx < r.Right - U(4) && ry <= r.Top + 1)
        {
            if (ry >= r.Top - 0.5f) { rSurf = back; rSurfRect = r; ry = r.Top; return; }
            rq.Insert(1, RJump(rx, r.Top, back, U(2)));
            return;
        }
        if (ry < TaskbarY - 1) StartFall(null);
    }

    // ---- рисование кота в окне прогулки
    public int RoamW { get { return (int)U(124); } }
    public int RoamH { get { return (int)U(124); } }
    public float RoamFeetX { get { return RoamW / 2f; } }
    public float RoamFeetY { get { return RoamH / 2f; } }

    public void RoamDraw(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float fx = RoamFeetX, fy = RoamFeetY;
        // клик-круги по крестику
        if (rTapT > 0)
        {
            float tx = rTapAt.X - rx + fx, ty = rTapAt.Y - ry + fy, k = 1 - rTapT / 20f;
            using (Pen p = new Pen(Color.FromArgb((int)(220 * (1 - k)), 255, 250, 236), Math.Max(1.5f, U(0.6f)))) g.DrawEllipse(p, tx - U(2 + 6 * k), ty - U(2 + 6 * k), U(4 + 12 * k), U(4 + 12 * k));
            using (Pen p = new Pen(Color.FromArgb((int)(200 * (1 - k)), InkC), Math.Max(1f, U(0.35f)))) g.DrawEllipse(p, tx - U(1 + 4 * k), ty - U(1 + 4 * k), U(2 + 8 * k), U(2 + 8 * k));
        }
        foreach (Particle p in rParts)
        {
            float k = p.Life / (float)p.Max;
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(150 * k), 214, 226, 238))) g.FillEllipse(b, p.X - rx + fx - p.R + U(0.3f), p.Y - ry + fy - p.R + U(0.4f), p.R * 2, p.R * 2);
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(215 * k), 255, 255, 255))) g.FillEllipse(b, p.X - rx + fx - p.R, p.Y - ry + fy - p.R, p.R * 2, p.R * 1.9f);
        }
        GraphicsState st = g.Save();
        int g0 = Ground;
        try
        {
            if (rRot != 0) { g.TranslateTransform(fx, fy); g.RotateTransform(rRot); g.TranslateTransform(-fx, -fy); }
            GraphicsState st2 = g.Save();
            g.TranslateTransform(fx, fy - Ground);
            rc.X = 0; rc.Y = 0;
            DrawCat(g, rc);
            g.Restore(st2);
            if (rCarryClock) DrawHeldClock(g, fx, fy - U(34) * rc.Scale);
        }
        finally { Ground = g0; g.Restore(st); }
        if (rSay != null)
        {
            float k = Math.Min(1f, rSayT / 8f);
            SizeF sz = g.MeasureString(rSay, fnt);
            float w = sz.Width + U(2.4f), h = sz.Height + U(0.8f);
            double ang = rRot * Math.PI / 180; float hd = U(rCarryClock ? 46 : 32) * rc.Scale;
            float hx = fx + (float)Math.Sin(ang) * hd, hy = fy - (float)Math.Cos(ang) * hd;
            float x = hx - w / 2, y = rPose == 2 ? hy + U(2) : hy - h;
            if (rPose == 1) { x = hx - w - U(2); y = hy - h; }
            // не вылезать за край экрана
            float wl = rx - RoamFeetX, wt = ry - RoamFeetY; Rectangle sf = ScreenFull;
            float minX = Math.Max(1, sf.Left - wl + 2), maxX = Math.Min(RoamW - 1, sf.Right - wl - 2) - w, minY = Math.Max(1, sf.Top - wt + 2);
            x = Math.Max(minX, Math.Min(maxX, x)); y = Math.Max(minY, Math.Min(RoamH - h - 1, y));
            using (GraphicsPath path = RoundRect(x, y, w, h, h / 2))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(248 * k), 255, 251, 238))) g.FillPath(b, path);
                using (Pen p = new Pen(Color.FromArgb((int)(220 * k), 78, 54, 42), Math.Max(1f, U(0.4f)))) g.DrawPath(p, path);
            }
            using (SolidBrush b = new SolidBrush(Alpha(pal['K'], (int)(255 * k)))) g.DrawString(rSay, fnt, b, x + U(1.2f), y + U(0.4f));
        }
    }

    void DrawHeldClock(Graphics g, float cx, float cy)
    {
        float bob = (float)Math.Sin(tick * 0.5f) * U(0.6f);
        RectangleF r = new RectangleF(cx - U(13), cy - U(7) + bob, U(26), U(14));
        using (GraphicsPath p = RoundRect(r.X, r.Y, r.Width, r.Height, U(2)))
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, rHoleCol.R, rHoleCol.G, rHoleCol.B))) g.FillPath(b, p);
            using (Pen pen = new Pen(InkC, Math.Max(1.2f, U(0.5f)))) g.DrawPath(pen, p);
        }
        DateTime now = DateTime.Now;
        bool darkBg = rHoleCol.GetBrightness() < 0.55f;
        Color tc = darkBg ? Color.White : Color.FromArgb(30, 30, 36);
        using (Font f1 = new Font("Segoe UI", U(4.0f), FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font f2 = new Font("Segoe UI", U(2.7f), FontStyle.Regular, GraphicsUnit.Pixel))
        using (SolidBrush b = new SolidBrush(tc))
        using (StringFormat sf = new StringFormat())
        {
            sf.Alignment = StringAlignment.Center;
            g.DrawString(now.ToString("HH:mm"), f1, b, new RectangleF(r.X, r.Y + U(0.6f), r.Width, U(6)), sf);
            g.DrawString(now.ToString("dd.MM.yyyy"), f2, b, new RectangleF(r.X, r.Y + U(7.2f), r.Width, U(5)), sf);
        }
    }

    // ---- «дырка» вместо часов, пока кот их держит
    void ShowHole()
    {
        rHole = true;
        if (RoamFake) return;
        try
        {
            if (holeWin == null || holeWin.IsDisposed)
            {
                holeWin = new HoleForm();
            }
            holeWin.BackColor = rHoleCol;
            holeWin.Bounds = rHoleRect;
            holeWin.Show();
            SetWindowPos(holeWin.Handle, new IntPtr(-1), rHoleRect.X, rHoleRect.Y, rHoleRect.Width, rHoleRect.Height, 0x0010 | 0x0040);
        }
        catch (Exception ex) { GuardLog("часы", ex); }
    }
    void HideHole() { rHole = false; try { if (holeWin != null && !holeWin.IsDisposed) holeWin.Hide(); } catch { } }

    class HoleForm : Form
    {
        public HoleForm() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000; return cp; } }
    }

    // ---- окно прогулки: маленькое, прозрачное, едет за котом
    class RoamWin : Form
    {
        VapePet p; Bitmap bmp;
        IntPtr dc = IntPtr.Zero, dib = IntPtr.Zero, old = IntPtr.Zero, bits = IntPtr.Zero; int dw, dh;
        public RoamWin(VapePet owner)
        {
            p = owner; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(-30000, -30000, p.RoamW, p.RoamH);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80 | 0x08000000; return cp; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right) { p.EndRoam(); p.ShowMenu(Cursor.Position); return; }
            if (p.rc != null) { p.rc.HappyT = Math.Max(p.rc.HappyT, 50); p.rSay = new string[] { "Мур!", "Мяу!", "Я гуляю!" }[p.rnd.Next(3)]; p.rSayT = 45; }
        }
        void FreeDib()
        {
            try { if (dc != IntPtr.Zero && old != IntPtr.Zero) SelectObject(dc, old); if (dib != IntPtr.Zero) DeleteObject(dib); if (dc != IntPtr.Zero) DeleteDC(dc); } catch { }
            dc = dib = old = bits = IntPtr.Zero; dw = dh = 0;
        }
        protected override void OnFormClosed(FormClosedEventArgs e) { FreeDib(); base.OnFormClosed(e); }
        public void Render()
        {
            int w = p.RoamW, h = p.RoamH;
            if (bmp == null || bmp.Width != w || bmp.Height != h) { if (bmp != null) bmp.Dispose(); bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb); }
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); p.RoamDraw(g); }
            if (dib == IntPtr.Zero || dw != w || dh != h)
            {
                FreeDib();
                IntPtr sdc = GetDC(IntPtr.Zero);
                try
                {
                    dc = CreateCompatibleDC(sdc);
                    BITMAPINFOHEADER bi = new BITMAPINFOHEADER(); bi.biSize = 40; bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
                    IntPtr b; dib = CreateDIBSection(dc, ref bi, 0, out b, IntPtr.Zero, 0);
                    if (dib == IntPtr.Zero) { FreeDib(); return; }
                    bits = b; old = SelectObject(dc, dib); dw = w; dh = h;
                }
                finally { ReleaseDC(IntPtr.Zero, sdc); }
            }
            BitmapData bd = new BitmapData(); bd.Width = w; bd.Height = h; bd.Stride = w * 4; bd.PixelFormat = PixelFormat.Format32bppPArgb; bd.Scan0 = bits;
            bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly | ImageLockMode.UserInputBuffer, PixelFormat.Format32bppPArgb, bd);
            bmp.UnlockBits(bd);
            int x = (int)Math.Round(p.rx - p.RoamFeetX), y = (int)Math.Round(p.ry - p.RoamFeetY);
            IntPtr screen = GetDC(IntPtr.Zero);
            try
            {
                SIZE size = new SIZE(w, h); POINT src = new POINT(0, 0), dst = new POINT(x, y);
                BLENDFUNCTION bf = new BLENDFUNCTION(); bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
                UpdateLayeredWindow(Handle, screen, ref dst, ref size, dc, ref src, 0, ref bf, 2);
            }
            finally { ReleaseDC(IntPtr.Zero, screen); }
            if (p.tick % 30 == 0) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }
    }

    // ================= жизнь котика: реакции на вас и время суток =================
    // Клавиши не читаются: «печатаете» = было действие, а мышка не сдвинулась.

    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO lii);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

    public static bool LifeFake; public int fakeIdleMs; public DateTime fakeNow = DateTime.MinValue;
    public bool sleeping; int sleepZ, wakeCool, shakeCool, typingT, typingLong, typingSayCool, lifeTick;
    uint lastInputStamp; Point lastMouse; float mouseSpeed;
    HashSet<string> saidToday = new HashSet<string>();
    string partyDay = "";
    int nightOutfit = -1;   // -1 не знаем, 0 дневной, 1 ночной

    DateTime Now { get { return LifeFake && fakeNow != DateTime.MinValue ? fakeNow : DateTime.Now; } }

    int IdleMs()
    {
        if (LifeFake) return fakeIdleMs;
        try { LASTINPUTINFO l = new LASTINPUTINFO(); l.cbSize = 8; if (GetLastInputInfo(ref l)) { lastInputStampNow = l.dwTime; return (int)((uint)Environment.TickCount - l.dwTime); } } catch { }
        return 0;
    }
    uint lastInputStampNow;

    bool LifeFree { get { return mode == 0 && pendingMode < 0 && !roaming && !chasing && egg == null && musicT == 0 && movieT == 0 && !aiBusy && !dragging && !me.Jumping; } }

    bool Once(string key)
    {
        string k = key + ":" + Now.ToString("yyyy-MM-dd");
        if (saidToday.Contains(k)) return false;
        saidToday.Add(k); return true;
    }

    void LifeSay(string t) { Say(t, me.X, CatTop(me) - U(1), 'K'); }

    public void LifeStep()
    {
        lifeTick++;
        if (wakeCool > 0) wakeCool--; if (shakeCool > 0) shakeCool--; if (typingSayCool > 0) typingSayCool--;
        int idle = IdleMs();
        DateTime now = Now;
        int h = now.Hour;
        bool night = h >= 23 || h < 6;

        // --- мышка рядом и резкий рывок
        Point m = LifeFake ? fakeCursor : CursorNowSafe();
        float dmx = m.X - lastMouse.X, dmy = m.Y - lastMouse.Y;
        mouseSpeed = mouseSpeed * 0.6f + (float)Math.Sqrt(dmx * dmx + dmy * dmy) * 0.4f;
        bool moved = m != lastMouse; Point prevMouse = lastMouse; lastMouse = m;

        // --- печатаете: было действие, а мышка стоит
        if (!LifeFake && lastInputStampNow != lastInputStamp) { if (!moved && lastInputStamp != 0) typingT = 45; lastInputStamp = lastInputStampNow; }
        if (LifeFake && fakeTyping) typingT = 45;
        if (typingT > 0) { typingT--; typingLong++; } else typingLong = Math.Max(0, typingLong - 3);

        // --- сон / пробуждение
        int sleepAfter = (night ? 60 : 180) * 1000;
        if (sleeping)
        {
            if (!LifeFree && mode != 0) { sleeping = false; me.Lying = false; }
            else if (idle < 1500) Wake();
            else
            {
                me.Lying = true; me.Walking = false; me.BlinkT = 5; me.PuffT = 0;
                if (++sleepZ % 110 == 0) Say(sleepZ % 220 == 0 ? "Zzz…" : "Мрр… zz", me.X + me.Dir * U(8), CatTop(me) - U(2), 'U');
                if (lifeTick % 30 == 0) petBrain.Energy = Math.Min(100f, petBrain.Energy + 1.5f);
                return;
            }
        }
        if (!LifeFree) return;
        if (idle > sleepAfter && me.PuffT == 0)
        {
            sleeping = true; sleepZ = 0; me.Lying = true; me.Walking = false; me.TX = me.X; me.Hold = ' ';
            LifeSay(night ? "Спокойной ночи…" : "Посплю, пока тебя нет");
            return;
        }

        // --- вздрагивает от резкого рывка мышкой рядом
        float cx = Left + me.X, cy = Top + Ground;
        bool mouseNear = NearCat(m, cx, cy);
        // быстрый рывок: мышка могла пролететь над котом между кадрами
        bool flewOver = (m.X - cx) * (prevMouse.X - cx) <= 0 && Math.Abs(m.X - prevMouse.X) < U(400)
                        && Math.Min(m.Y, prevMouse.Y) < cy + U(20) && Math.Max(m.Y, prevMouse.Y) > cy - U(70);
        bool jerkNear = mouseNear || NearCat(prevMouse, cx, cy) || flewOver;
        if (frightLevel > 0 && jerkNear && mouseSpeed > FrightSpeed * Dpi1 && shakeCool == 0)
        {
            shakeCool = 45; me.SweatT = 40; me.Walking = false;
            Jump(me, me.X, 0, U(9), 14, null);
            LifeSay(new string[] { "Ой!", "Напугал!", "Аккуратнее!" }[rnd.Next(3)]);
            Frightened(mouseSpeed > FrightStrong * Dpi1);
            return;
        }
        // смотрит на мышку, если она рядом и кот стоит
        if (mouseNear && !me.Walking && lifeTick % 10 == 0) me.Dir = m.X >= cx ? 1 : -1;

        // --- смотрит на окно, где вы печатаете
        if (typingT > 0 && !me.Walking && lifeTick % 15 == 0)
        {
            float fx = ForegroundCenterX();
            if (!float.IsNaN(fx)) me.Dir = fx >= cx ? 1 : -1;
        }
        if (typingT > 0 && typingSayCool == 0 && rnd.Next(30 * 40) == 0)
        {
            typingSayCool = 30 * 180;
            LifeSay(new string[] { "Что пишешь?", "Печатаешь быстрее меня", "Не опечатайся", "Я тоже умею: мяу" }[rnd.Next(4)]);
        }
        if (typingLong > 30 * 60 * 25 && Once("break" + (h / 2)))
        {
            typingLong = 0; LifeSay("Давно печатаешь. Перерывчик?");
        }

        // --- время суток и день недели (не чаще раза в 10 секунд)
        if (lifeTick % 60 == 30) TimeOfDay(now);

        // Новая автономная система личности работает только в свободном режиме.
        PetBrainStep(idle, mouseNear, m);
    }

    void PetBrainStep(int idle, bool mouseNear, Point cursor)
    {
        if (!LifeFree || sleeping || me.Hidden || chasing || roaming) return;

        petBrain.ThinkTick++;
        if (petBrain.ActionCool > 0) petBrain.ActionCool--;

        // Обновление потребностей раз в секунду, без тяжёлых операций в каждом кадре.
        if (petBrain.ThinkTick % 30 == 0)
        {
            if (idle > 60000) petBrain.Boredom = Math.Min(100f, petBrain.Boredom + 0.7f);
            else petBrain.Boredom = Math.Max(0f, petBrain.Boredom - 0.45f);

            if (typingT > 0) petBrain.Boredom = Math.Max(0f, petBrain.Boredom - 0.3f);
            petBrain.Energy = Math.Max(0f, petBrain.Energy - (me.Walking ? 0.12f : 0.025f));

            // Наведение курсора на котика и спокойное удержание воспринимается как внимание.
            bool overHead = mouseNear && Math.Abs(cursor.X - (Left + me.X)) < U(38) &&
                            cursor.Y < Top + Ground - U(22);
            if (overHead && mouseSpeed < U(8))
            {
                petBrain.HoverTicks++;
                if (petBrain.HoverTicks >= 2)
                {
                    petBrain.Affection = Math.Min(100f, petBrain.Affection + 0.25f);
                    petBrain.Boredom = Math.Max(0f, petBrain.Boredom - 0.5f);
                    if (petBrain.HoverTicks == 2 && petBrain.ActionCool == 0)
                    {
                        me.HappyT = Math.Max(me.HappyT, 24);
                        petBrain.ActionCool = 30 * 25;
                    }
                }
            }
            else petBrain.HoverTicks = 0;

            // Настроение вычисляется из потребностей, а не выбирается случайно.
            if (petBrain.Energy < 22f || (petBrain.Laziness > 78f && petBrain.Energy < 45f)) petBrain.Mood = 2;
            else if (petBrain.Boredom > 68f) petBrain.Mood = 4;
            else if (mouseNear || (petBrain.Curiosity > 78f && idle < 30000)) petBrain.Mood = 3;
            else if (petBrain.Affection > 55f && petBrain.Boredom < 35f) petBrain.Mood = 1;
            else petBrain.Mood = 0;
        }

        // Редкие микро-события. Не запускаем большие сцены и не меняем режимы.
        if (petBrain.ThinkTick % (30 * 20) == 0 && petBrain.ActionCool == 0)
        {
            int chance = 8 + (int)(petBrain.Curiosity * 0.10f) + (int)(petBrain.Playfulness * 0.08f) + (int)(petBrain.Sociability * 0.05f);
            if (petBrain.Boredom > 60f) chance += 8;
            if (petBrain.Energy < 20f) chance /= 3;
            if (rnd.Next(100) < chance)
            {
                int action = rnd.Next(4);
                if (action == 0)
                {
                    me.BlinkT = 5;
                    me.Dir = cursor.X >= Left + me.X ? 1 : -1;
                }
                else if (action == 1 && petBrain.Playfulness > 45f && petBrain.Sociability > 30f && petBrain.Timidity < 85f)
                {
                    me.HappyT = Math.Max(me.HappyT, 35);
                    LifeSay(new string[] { "Мяу!", "Хороший денёк!", "Поиграем?" }[rnd.Next(3)]);
                }
                else if (action == 2 && petBrain.Curiosity > 45f)
                {
                    me.Dir = rnd.Next(2) == 0 ? -1 : 1;
                    me.BlinkT = 5;
                    LifeSay(new string[] { "Что там?", "Интересно…", "Мр?" }[rnd.Next(3)]);
                }
                else
                {
                    me.BlinkT = 5;
                    if (petBrain.Mood == 4) LifeSay("Мне ску-у-учно…");
                }
                petBrain.ActionCool = 30 * 70;
            }
        }

        // Сохраняем редко, не пишем на диск каждый кадр.
        if (petBrain.ThinkTick - petBrain.LastSaveTick >= 30 * 60)
        {
            petBrain.LastSaveTick = petBrain.ThinkTick;
            SaveSettings();
        }
    }

    public bool fakeTyping;
    bool NearCat(Point p, float cx, float cy) { return Math.Abs(p.X - cx) < U(80) && p.Y > cy - U(70) && p.Y < cy + U(20); }

    Point CursorNowSafe() { try { return Cursor.Position; } catch { return lastMouse; } }

    float ForegroundCenterX()
    {
        if (LifeFake) return Left - 400;
        try
        {
            IntPtr fw = GetForegroundWindow(); if (fw == IntPtr.Zero) return float.NaN;
            uint pid; GetWindowThreadProcessId(fw, out pid); if (pid == myPid) return float.NaN;
            Rectangle r; if (!WinRect(fw, out r)) return float.NaN;
            return r.Left + r.Width / 2f;
        }
        catch { return float.NaN; }
    }

    void DropSleepTalk() { texts.RemoveAll(delegate (FText o) { return o.Text == "Мрр… zz" || o.Text == "Zzz…"; }); }

    void Wake()
    {
        DropSleepTalk();
        sleeping = false; me.Lying = false; me.BlinkT = 0; me.SweatT = 30; me.Walking = false; me.Phase = 1; me.T = 90;
        Jump(me, me.X, 0, U(10), 16, delegate { me.HappyT = 50; });
        int h = Now.Hour;
        LifeSay(h >= 6 && h < 11 ? "Доброе утро!" : (h >= 23 || h < 6 ? "Ты чего не спишь?" : new string[] { "О, ты вернулся!", "Я не спал, честно", "Мяу! Привет!" }[rnd.Next(3)]));
        wakeCool = 30 * 20;
    }

    // наряд по времени: ночью пижама и колпак (только на прогулке в уголке)
    public void ApplyTimeOutfit()
    {
        if (mode != 0) return;
        int hh = Now.Hour; bool night = hh >= 23 || hh < 6;
        int want = night ? 1 : 0;
        if (want == nightOutfit) return;
        nightOutfit = want;
        if (night) { me.Outfit = 1; me.C1 = Color.FromArgb(150, 180, 228); me.Hat = 2; me.HatC = Color.FromArgb(120, 150, 214); }
        else { me.Outfit = 1; me.C1 = Color.FromArgb(40, 42, 56); if (me.Hat == 2) me.Hat = 0; }
    }

    void TimeOfDay(DateTime now)
    {
        ApplyTimeOutfit();
        int h = now.Hour; DayOfWeek d = now.DayOfWeek;
        if (me.PuffT > 0 || me.Hold != ' ') return;
        // пятница вечером и суббота — вечеринка сама
        bool partyTime = (d == DayOfWeek.Friday && h >= 18) || (d == DayOfWeek.Saturday && h >= 16 && h < 23);
        string today = now.ToString("yyyy-MM-dd");
        if (partyTime && partyDay != today && wakeCool == 0)
        {
            partyDay = today; SaveSettings();
            me.Walking = false; LifeSay(d == DayOfWeek.Friday ? "Пятница! Вечеринка!" : "Суббота! Зову гостей!");
            partyPending = 70;
            return;
        }
        if (h >= 6 && h < 11 && Once("coffee") ) { Coffee(d == DayOfWeek.Monday ? "Понедельник… кофе двойной" : "Утро. Сначала кофе"); return; }
        if (h >= 6 && h < 11 && rnd.Next(100) < 2) { Yawn(); return; }
        if (h == 13 && Once("lunch")) { LifeSay("Обед! Где мой корм?"); me.HappyT = 40; return; }
        if (h >= 18 && h < 23 && d != DayOfWeek.Friday && Once("evening")) { LifeSay(new string[] { "Вечер… может, кино?", "Рабочий день всё?", "Вечерком самое то попарить" }[rnd.Next(3)]); return; }
        if (h >= 23 && Once("bed")) { LifeSay("Я в пижаме. Пора спать!"); Yawn(); return; }
        if ((h >= 1 && h < 5) && rnd.Next(100) < 2 && Once("late" + h)) { LifeSay("Ты чего не спишь?"); return; }
        if (h >= 22 || h < 6) { if (rnd.Next(100) < 3) Yawn(); }
    }

    int partyPending, yawnT;
    void Yawn() { yawnT = 40; me.BlinkT = 5; LifeSay("Ааа-уф…"); }
    void Coffee(string phrase)
    {
        me.Hold = 'U'; me.HoldCol = 'W'; me.TX = me.X; me.Phase = 1; me.T = 30 * 14; me.Walking = false;
        LifeSay(phrase); coffeeT = 30 * 14;
    }
    int coffeeT;

    // вызывается каждый кадр из Step (после логики сцены)
    public void LifeTimers()
    {
        if (!roaming && rHole) HideHole();
        if (yawnT > 0) { yawnT--; me.BlinkT = 5; }
        if (coffeeT > 0)
        {
            coffeeT--;
            if (mode != 0 || pendingMode >= 0) coffeeT = 0;
            else if (coffeeT % 90 == 45) Say(coffeeT % 180 == 45 ? "*сёрб*" : "Ммм, кофе", me.X, CatTop(me) - U(1), 'b');
            if (coffeeT == 0 && me.Hold == 'U') me.Hold = ' ';
        }
        if (partyPending > 0 && --partyPending == 0 && mode == 0 && pendingMode < 0) SetMode(4);
    }
    // ================= испуг → лужица → уборщик со шваброй → погоня по экрану =================
    class CActor
    {
        public Cat c; public float x, y, vx, vy, rot, spin; public string say; public int sayT; public int stunT, starsT;
        public bool air; public Action land;
    }
    class CProp { public int Kind; public float x; public char Letter; public int T; public bool Used; }   // 0 шкурка, 1 буква, 2 мокрый след

    public bool chasing;
    CActor ch, cj;                                  // наш котик и уборщик
    float puddleX, puddleR, puddleMax;
    List<CProp> cprops = new List<CProp>();
    List<Func<bool>> cq = new List<Func<bool>>();
    LayerWin chaseWin; int chaseT, chaseCool, frightCount, frightWindow;
    public int chaseForceEnd = -1;                  // тест: 0 — догнал, 1 — убежал

    public int ChaseW { get { return ScreenFull.Width; } }
    public int ChaseH { get { return (int)U(76); } }
    public float ChaseFeetY { get { return ChaseH - U(10); } }
    public int ChaseLeft { get { return ScreenFull.Left; } }
    public int ChaseTop { get { return (int)Math.Round(TaskbarY - ChaseFeetY); } }

    // сильный испуг: два испуга подряд или очень резкий рывок
    public void Frightened(bool strong)
    {
        if (frightWindow > 0) frightCount++; else frightCount = 1;
        frightWindow = 30 * 15;
        if ((strong || frightCount >= FrightNeed) && chaseCool == 0) { frightCount = 0; if (!StartChase(false)) chaseSoon = 20; }
        else if (frightCount >= FrightNeed && chaseCool > 0) LifeSay("Да хватит пугать!");
    }

    public bool StartChase(bool force)
    {
        if (chasing || roaming || mode != 0 || pendingMode >= 0 || egg != null || dragging || musicT > 0 || movieT > 0) return false;
        if (!force && (me.Hidden || me.Jumping)) return false;
        if (sleeping) { sleeping = false; me.Lying = false; }
        ch = new CActor(); ch.c = me.Clone();
        Cat h = ch.c; h.X = 0; h.Y = 0; h.Z = h.TZ = 0; h.Walking = false; h.Jumping = false; h.Sitting = false; h.Lying = false; h.Hold = ' ';
        h.HappyT = 0; h.AngryT = 0; h.PuffT = 0; h.View = 1; h.Speed = 1.4f; h.JDone = null;
        ch.x = Left + me.X; if (ch.x < ChaseLeft + U(20)) ch.x = ChaseLeft + U(20); if (ch.x > ChaseLeft + ChaseW - U(20)) ch.x = ChaseLeft + ChaseW - U(20);
        cj = null; cprops.Clear(); cq.Clear(); chaseT = 0; puddleR = 0; puddleX = ch.x; puddleMax = U(13);
        me.Hidden = true; me.Walking = false;
        chasing = true;
        try { BuildChase(); }
        catch (Exception ex) { GuardLog("сценка с уборщиком", ex); EndChase(); return false; }
        if (!RoamFake)
        {
            try
            {
                if (chaseWin == null || chaseWin.IsDisposed) chaseWin = new LayerWin(this, delegate { return new Size(ChaseW, ChaseH); }, delegate { return new Point(ChaseLeft, ChaseTop); }, ChaseDraw);
                chaseWin.Render(); if (!chaseWin.Visible) chaseWin.Show();
            }
            catch (Exception ex) { GuardLog("окно сценки", ex); EndChase(); return false; }
        }
        return true;
    }

    public void EndChase()
    {
        if (!chasing) return;
        chasing = false; cq.Clear(); cprops.Clear();
        float hx = ch != null ? ch.x : Left + me.X;
        me.X = Math.Max(U(12), Math.Min(Width - U(12), hx - Left)); me.TX = me.X; me.Z = me.TZ = 1f; me.Y = 0;
        if (ch != null) me.Dir = ch.c.Dir;
        me.Hidden = false; me.Walking = false; me.Phase = 1; me.T = 90; me.Lying = false;
        chaseCool = 30 * 45;
        try { if (chaseWin != null) chaseWin.Hide(); } catch { }
    }

    CActor NewJanitor(float x)
    {
        CActor a = new CActor(); Cat c = new Cat();
        c.Id = 777; c.MainC = Color.FromArgb(150, 150, 160); c.BellyC = Color.FromArgb(236, 236, 240); c.Stripes = false;
        c.IrisC = Color.FromArgb(220, 170, 60); c.Outfit = 6; c.C1 = Color.FromArgb(84, 120, 170); c.C2 = Color.FromArgb(60, 90, 140);
        c.Hat = 2; c.HatC = Color.FromArgb(230, 120, 80); c.Scale = 1.08f; c.Vape = false; c.Hold = 'Y'; c.HoldCol = 'g'; c.Dir = -1; c.View = 1;
        a.c = c; a.x = x; return a;
    }

    // ---- сценарий
    void BuildChase()
    {
        Rectangle full = ScreenFull;
        float L = full.Left + U(16), R = full.Right - U(16);
        bool fromRight = (ch.x - full.Left) < full.Width * 0.6f;
        cq.Add(CDo(delegate { ch.c.SweatT = 9999; ch.c.Shake = 60; Talk(ch, new string[] { "Ой-ой-ой…", "Ааа!", "Мамочки…" }[rnd.Next(3)], 50); }));
        cq.Add(CWaitF(delegate { puddleR = Math.Min(puddleMax, puddleR + U(0.35f)); return puddleR >= puddleMax; }));
        cq.Add(CDo(delegate { Talk(ch, new string[] { "Это не я…", "Оно само!", "Упс…" }[rnd.Next(3)], 60); }));
        cq.Add(CWait(40));
        cq.Add(CDo(delegate
        {
            cj = NewJanitor(fromRight ? full.Right + U(20) : full.Left - U(20));
            cj.c.Dir = fromRight ? -1 : 1;
            Talk(cj, "Кто тут насвинячил?!", 60);
        }));
        cq.Add(CWalk(delegate { return cj; }, delegate { return puddleX + (fromRight ? 1 : -1) * U(24); }, U(2.4f)));
        cq.Add(CDo(delegate { cj.c.Dir = fromRight ? -1 : 1; cj.c.AngryT = 9999; ch.c.Dir = fromRight ? 1 : -1; }));
        string[] yell = { "Я только что помыла!", "Ты что наделал?!", "Ну всё, держись!" };
        for (int i = 0; i < 3; i++) { string y0 = yell[i]; cq.Add(CDo(delegate { Talk(cj, y0, 46); cj.c.StompT = 20; })); cq.Add(CWait(48)); }
        cq.Add(CDo(delegate { Talk(ch, "Бежим!", 30); ch.c.SweatT = 9999; ch.c.Shake = 0; }));
        cq.Add(CChase(L, R));
    }

    // ---- погоня: ~5 секунд + случайные события, потом развязка
    Func<bool> CChase(float L, float R)
    {
        int t = 0, evAt = 30 + rnd.Next(60), ev2At = evAt + 50 + rnd.Next(40); bool caught = false, tripped = false, ended = false;
        int dur = 150 + rnd.Next(40);
        int outcome = chaseForceEnd >= 0 ? chaseForceEnd : rnd.Next(2);   // 0 — догонит, 1 — убежит
        return delegate
        {
            if (ended) return true;
            t++;
            // случайные события
            if (t == evAt || t == ev2At) SpawnEvent(L, R, t == ev2At);
            // наш котик: убегает от уборщика, у края перепрыгивает его
            if (ch.stunT > 0) { ch.stunT--; ch.c.Walking = false; if (ch.stunT == 0) { ch.c.Lying = false; Talk(ch, "Ой-ой…", 25); } }
            else if (!ch.air)
            {
                int away = ch.x >= cj.x ? 1 : -1;
                if ((away > 0 && ch.x > R - U(6)) || (away < 0 && ch.x < L + U(6)))
                {
                    if (t > dur && outcome == 1) { ended = true; Escape(away); return true; }
                    Leap(ch, cj.x - away * U(26), U(22), 22);
                    Talk(ch, new string[] { "Хоп!", "Не догонишь!", "Оп-ля!" }[rnd.Next(3)], 26);
                }
                else CMove(ch, away, U(2.9f) * (OnWet(ch.x) ? 1.6f : 1f));
            }
            // уборщик
            if (cj.stunT > 0) { cj.stunT--; cj.c.Walking = false; if (cj.stunT == 0) { cj.c.Lying = false; Talk(cj, new string[] { "Ну погоди!", "Я тебе!", "Убью!" }[rnd.Next(3)], 30); } }
            else if (!cj.air)
            {
                int dir = ch.x >= cj.x ? 1 : -1;
                float sp = U(2.55f) + (t > dur && outcome == 0 ? U(0.9f) : 0) + (ch.stunT > 0 ? U(1.2f) : 0);
                CMove(cj, dir, sp);
                cj.c.ItemRot = (float)Math.Sin(t * 0.5f) * 40;
                if (t % 50 == 25) Talk(cj, new string[] { "Стой!", "А ну иди сюда!", "Шваброй получишь!", "Стоять!" }[rnd.Next(4)], 30);
                // догнал
                if (Math.Abs(cj.x - ch.x) < U(14) && !ch.air && (t > dur && outcome == 0 || ch.stunT > 0 || tripped) && !caught)
                {
                    caught = true; ended = true; Bonk(); return true;
                }
            }
            // шкурка и буква
            foreach (CProp p in cprops)
            {
                if (p.Used) continue;
                if (p.Kind == 0 && !cj.air && cj.stunT == 0 && Math.Abs(cj.x - p.x) < U(4)) { p.Used = true; Slip(cj, "Аааа!"); }
                if (p.Kind == 1 && !ch.air && ch.stunT == 0 && Math.Abs(ch.x - p.x) < U(4)) { p.Used = true; tripped = true; Trip(ch); }
            }
            if (t > dur + 240 && !ended) { ended = true; if (outcome == 1) Escape(ch.x >= cj.x ? 1 : -1); else Bonk(); return true; }
            return false;
        };
    }

    void SpawnEvent(float L, float R, bool second)
    {
        int k = rnd.Next(3);
        int dir = ch.x >= cj.x ? 1 : -1;
        if (k == 0)
        {
            CProp p = new CProp(); p.Kind = 0;
            p.x = cj.x + dir * U(36); if (p.x < L || p.x > R) p.x = cj.x - dir * U(30);
            cprops.Add(p);
            Talk(ch, "Опа, шкурка!", 30);
        }
        else if (k == 1)
        {
            CProp p = new CProp(); p.Kind = 1; p.Letter = "АБВГДЖЗКМНПРСФЧШЭЮЯ"[rnd.Next(19)];
            p.x = ch.x + dir * U(28); if (p.x < L || p.x > R) p.x = ch.x - dir * U(28);
            cprops.Add(p);
        }
        else
        {
            CProp p = new CProp(); p.Kind = 2; p.x = ch.x + dir * U(40); p.T = 200;
            cprops.Add(p);
            Talk(cj, "Осторожно, мокрый пол!", 36);
        }
    }

    bool OnWet(float x) { foreach (CProp p in cprops) if (p.Kind == 2 && Math.Abs(p.x - x) < U(22)) return true; return false; }

    void CMove(CActor a, int dir, float sp)
    {
        Rectangle full = ScreenFull;
        a.c.Dir = dir; a.c.Walking = true; a.c.View = 1;
        a.x += dir * sp;
        a.x = Math.Max(full.Left - U(40), Math.Min(full.Right + U(40), a.x));
    }

    void Leap(CActor a, float tx, float h, int dur)
    {
        a.air = true; a.c.Jumping = true; a.c.JDur = dur; a.c.JT = 0; a.c.Walking = false; a.c.Dir = tx >= a.x ? 1 : -1;
        a.vx = (tx - a.x) / dur; a.vy = U(0.3f) * dur / 2f;
        a.land = delegate { a.c.Jumping = false; a.c.LandT = 8; a.vx = 0; };
    }

    // поскользнулся: подлетает, крутится и падает
    void Slip(CActor a, string say)
    {
        a.air = true; a.vy = U(3.2f); a.vx = a.c.Dir * U(0.8f); a.spin = 22 * -a.c.Dir; a.c.Walking = false; Talk(a, say, 40);
        a.land = delegate { a.rot = 0; a.spin = 0; a.vx = 0; a.c.Lying = true; a.stunT = 60; a.starsT = 60; Talk(a, "Ой-ой-ой…", 40); };
    }
    void Trip(CActor a)
    {
        a.c.Walking = false; a.c.Lying = true; a.stunT = 45; a.starsT = 30; Talk(a, new string[] { "Ай!", "Ой, буква!", "Кто тут букву бросил?!" }[rnd.Next(3)], 40);
    }

    void Bonk()
    {
        cq.Insert(0, CDo(delegate { cj.c.ItemRot = -80; Talk(cj, "Получай!", 30); }));
        cq.Insert(1, CWait(6));
        cq.Insert(2, CDo(delegate { cj.c.ItemRot = 60; ch.c.LandT = 12; ch.starsT = 70; ch.c.Lying = false; ch.stunT = 0; Talk(ch, "Бдыщ!", 30); shakeT = 6; }));
        cq.Insert(3, CWait(10));
        cq.Insert(4, CDo(delegate { cj.c.ItemRot = -60; ch.c.LandT = 12; Talk(ch, "Ай-ай-ай!", 36); }));
        cq.Insert(5, CWait(8));
        cq.Insert(6, CDo(delegate { cj.c.ItemRot = 50; ch.c.LandT = 12; }));
        cq.Insert(7, CWait(30));
        cq.Insert(8, CDo(delegate { cj.c.ItemRot = 0; Talk(cj, "Вот так! Будешь знать.", 50); ch.c.SweatT = 0; ch.c.Dir = cj.x > ch.x ? 1 : -1; }));
        cq.Insert(9, CWait(50));
        cq.Insert(10, CDo(delegate { Talk(ch, "За что-о…", 50); }));
        int at = 11;
        foreach (Func<bool> f in MopAndLeave()) cq.Insert(at++, f);
        cq.Insert(at++, CWalk(delegate { return ch; }, delegate { return Math.Max(Left + U(20), Math.Min(Right - U(20), ch.x)); }, U(1.3f)));
        cq.Insert(at++, CDo(delegate { EndChase(); }));
    }

    void Escape(int away)
    {
        Rectangle full = ScreenFull;
        float off = away > 0 ? full.Right + U(30) : full.Left - U(30);
        int at = 0;
        cq.Insert(at++, CDo(delegate { Talk(ch, "Пока-пока!", 30); }));
        cq.Insert(at++, CWalk(delegate { return ch; }, delegate { return off; }, U(3f)));
        cq.Insert(at++, CDo(delegate { cj.c.Walking = false; cj.c.AngryT = 9999; Talk(cj, "Ещё попадёшься!", 50); }));
        cq.Insert(at++, CWait(50));
        foreach (Func<bool> f in MopAndLeave()) cq.Insert(at++, f);
        // котик осторожно возвращается
        cq.Insert(at++, CDo(delegate { ch.x = away > 0 ? full.Right + U(16) : full.Left - U(16); ch.c.SweatT = 0; }));
        cq.Insert(at++, CWalk(delegate { return ch; }, delegate { return away > 0 ? full.Right - U(30) : full.Left + U(30); }, U(1.1f)));
        cq.Insert(at++, CDo(delegate { Talk(ch, new string[] { "Ушла?", "Фух, пронесло", "Я тут ни при чём" }[rnd.Next(3)], 50); ch.c.HappyT = 40; }));
        cq.Insert(at++, CWait(50));
        cq.Insert(at++, CWalk(delegate { return ch; }, delegate { return Math.Max(Left + U(20), Math.Min(Right - U(20), Left + Width * 0.5f)); }, U(1.4f)));
        cq.Insert(at++, CDo(delegate { EndChase(); }));
    }

    List<Func<bool>> MopAndLeave()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        s.Add(CWalk(delegate { return cj; }, delegate { return puddleX + U(9); }, U(1.8f)));
        s.Add(CDo(delegate { cj.c.Dir = -1; cj.c.AngryT = 0; Talk(cj, "Опять мне убирать…", 50); }));
        int n = 0;
        s.Add(delegate
        {
            n++; cj.c.ItemRot = (float)Math.Sin(n * 0.4f) * 35; cj.x = puddleX + U(9) + (float)Math.Sin(n * 0.2f) * U(4);
            puddleR = Math.Max(0, puddleR - U(0.12f));
            return puddleR <= 0;
        });
        s.Add(CDo(delegate { cj.c.ItemRot = 0; cprops.RemoveAll(delegate (CProp p) { return p.Kind == 2; }); Talk(cj, "Чистота!", 40); cj.c.HappyT = 40; }));
        s.Add(CWait(40));
        s.Add(CWalk(delegate { return cj; }, delegate { return ScreenFull.Right + U(40); }, U(2f)));
        s.Add(CDo(delegate { cj = null; cprops.Clear(); }));
        return s;
    }

    // ---- шаги сценки
    Func<bool> CWait(int n) { int k = n; return delegate { return --k <= 0; }; }
    Func<bool> CWaitF(Func<bool> f) { return f; }
    Func<bool> CDo(Action a) { return delegate { a(); return true; }; }
    Func<bool> CWalk(Func<CActor> who, Func<float> fx, float sp)
    {
        return delegate
        {
            CActor a = who(); if (a == null) return true;
            float x = fx(), d = x - a.x;
            if (Math.Abs(d) <= sp) { a.x = x; a.c.Walking = false; return true; }
            a.c.Dir = Math.Sign(d); a.c.Walking = true; a.c.View = 1; a.x += Math.Sign(d) * sp;
            return false;
        };
    }
    void Talk(CActor a, string t, int n) { if (a == null) return; a.say = t; a.sayT = n + 16; }

    // ---- кадр сценки
    public void ChaseStep()
    {
        if (!chasing) return;
        chaseT++;
        if (!RoamFake)
        {
            bool vis = false;
            try { vis = chaseWin != null && !chaseWin.IsDisposed && chaseWin.Visible && IsWindowVisible(chaseWin.Handle); } catch { }
            if (!vis) { try { if (chaseWin != null && !chaseWin.IsDisposed) chaseWin.Show(); } catch { } if (chaseT > 30) { GuardLog("окно сценки не видно — кот вернулся", null); EndChase(); return; } }
        }
        foreach (CActor a in new CActor[] { ch, cj })
        {
            if (a == null) continue;
            Cat c = a.c;
            if (c.BlinkT > 0) c.BlinkT--; else if (rnd.Next(300) == 0) c.BlinkT = 6;
            if (c.LandT > 0) c.LandT--;
            if (c.TurnT > 0) c.TurnT--;
            if (c.Dir != c.LastDir) { c.TurnT = 6; c.LastDir = c.Dir; }
            if (c.HappyT > 0 && c.HappyT < 9000) c.HappyT--;
            if (c.StompT > 0) c.StompT--;
            if (c.Shake > 0) c.Shake--;
            if (a.sayT > 0 && --a.sayT == 0) a.say = null;
            if (a.starsT > 0) a.starsT--;
            if (a.air && a.land != null)
            {
                a.vy -= U(0.3f); a.y += a.vy; a.x += a.vx; a.rot += a.spin; a.c.JT++;
                if (a.y <= 0) { a.y = 0; a.air = false; Action l = a.land; a.land = null; l(); }
            }
        }
        foreach (CProp p in cprops) if (p.Kind == 2 && p.T > 0) p.T--;
        cprops.RemoveAll(delegate (CProp p) { return p.Kind == 2 && p.T <= 0; });
        int guard = 0;
        while (cq.Count > 0 && guard++ < 6)
        {
            Func<bool> f0 = cq[0];
            if (!f0()) break;
            if (cq.Count > 0 && cq[0] == f0) cq.RemoveAt(0);
        }
        if (chasing && cq.Count == 0) EndChase();
        if (chasing && chaseT > 30 * 90) EndChase();   // страховка
    }

    public void ChaseDraw(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float fy = ChaseFeetY;
        // лужица
        if (puddleR > 0.5f)
        {
            float px = puddleX - ChaseLeft;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 150, 200, 240))) g.FillEllipse(b, px - puddleR * 1.6f, fy - puddleR * 0.28f, puddleR * 3.2f, puddleR * 0.62f);
            using (Pen p = new Pen(Color.FromArgb(160, 90, 140, 190), Math.Max(1, U(0.35f)))) g.DrawEllipse(p, px - puddleR * 1.6f, fy - puddleR * 0.28f, puddleR * 3.2f, puddleR * 0.62f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, px - puddleR * 0.9f, fy - puddleR * 0.16f, puddleR * 0.8f, puddleR * 0.14f);
        }
        foreach (CProp p in cprops)
        {
            if (p.Used && p.Kind != 2) continue;
            float px = p.x - ChaseLeft;
            if (p.Kind == 0)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(246, 214, 80)))
                using (Pen pen = new Pen(InkC, Math.Max(1.2f, U(0.4f))))
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        GraphicsState s = g.Save();
                        g.TranslateTransform(px, fy - U(0.6f)); g.RotateTransform(k * 38);
                        using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(-U(0.9f), -U(4.2f), U(1.8f), U(4.4f)); g.FillPath(b, gp); g.DrawPath(pen, gp); }
                        g.Restore(s);
                    }
                    using (SolidBrush b2 = new SolidBrush(Color.FromArgb(120, 90, 50))) g.FillEllipse(b2, px - U(0.6f), fy - U(1.2f), U(1.2f), U(1.2f));
                }
            }
            else if (p.Kind == 1)
            {
                using (Font f = new Font("Segoe UI", U(9), FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    string s = p.Letter.ToString(); SizeF sz = g.MeasureString(s, f);
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddString(s, f.FontFamily, (int)FontStyle.Bold, U(9), new PointF(px, fy - sz.Height * 0.92f), sf);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 96, 96))) g.FillPath(b, gp);
                        using (Pen pen = new Pen(InkC, Math.Max(1.2f, U(0.45f)))) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, gp); }
                    }
                }
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 150, 200, 240))) g.FillEllipse(b, px - U(22), fy - U(1.2f), U(44), U(2.6f));
                // табличка «мокрый пол»
                float sx = px + U(18);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 210, 60)))
                using (Pen pen = new Pen(InkC, Math.Max(1.2f, U(0.4f))))
                {
                    PointF[] tri = { new PointF(sx, fy - U(11)), new PointF(sx + U(4), fy), new PointF(sx - U(4), fy) };
                    g.FillPolygon(b, tri); g.DrawPolygon(pen, tri);
                    g.DrawLine(pen, sx, fy - U(7.5f), sx, fy - U(3.5f)); g.FillEllipse(Brushes.Black, sx - U(0.4f), fy - U(2.6f), U(0.8f), U(0.8f));
                }
            }
        }
        foreach (CActor a in new CActor[] { cj, ch })
        {
            if (a == null) continue;
            float ax = a.x - ChaseLeft;
            if (ax < -U(60) || ax > ChaseW + U(60)) continue;
            GraphicsState st = g.Save(); int g0 = Ground;
            try
            {
                g.TranslateTransform(ax, fy - a.y);
                if (a.rot != 0) { g.TranslateTransform(0, -U(10)); g.RotateTransform(a.rot); g.TranslateTransform(0, U(10)); }
                g.TranslateTransform(0, -Ground);
                a.c.X = 0; a.c.Y = 0; DrawCat(g, a.c);
            }
            finally { Ground = g0; g.Restore(st); }
            // звёздочки над головой
            if (a.starsT > 0)
            {
                float hy = fy - a.y - (a.c.Lying ? U(8) : U(26) * a.c.Scale), hx = ax + (a.c.Lying ? a.c.Dir * U(12) : 0);
                for (int k = 0; k < 3; k++)
                {
                    double ang = tick * 0.25 + k * Math.PI * 2 / 3;
                    DrawItem(g, 'S', 'Y', hx + (float)Math.Cos(ang) * U(6), hy + (float)Math.Sin(ang) * U(1.8f), 255, 0);
                }
            }
        }
        foreach (CActor a in new CActor[] { cj, ch })
        {
            if (a == null || a.say == null) continue;
            float k = Math.Min(1f, a.sayT / 8f);
            SizeF sz = g.MeasureString(a.say, fnt);
            float w = sz.Width + U(2.4f), h = sz.Height + U(0.8f);
            float top = fy - a.y - (a.c.Lying ? U(14) : U(31) * a.c.Scale) - h;
            if (a == cj && ch != null && ch.say != null && Math.Abs(cj.x - ch.x) < U(40)) top -= h + U(1);
            float x = Math.Max(1, Math.Min(ChaseW - w - 1, a.x - ChaseLeft - w / 2)), y = Math.Max(1, top);
            using (GraphicsPath path = RoundRect(x, y, w, h, h / 2))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(248 * k), 255, 251, 238))) g.FillPath(b, path);
                using (Pen p = new Pen(Color.FromArgb((int)(220 * k), 78, 54, 42), Math.Max(1f, U(0.4f)))) g.DrawPath(p, path);
            }
            Color tc = a == cj ? Color.FromArgb(200, 60, 60) : pal['K'];
            using (SolidBrush b = new SolidBrush(Alpha(tc, (int)(255 * k)))) g.DrawString(a.say, fnt, b, x + U(1.2f), y + U(0.4f));
        }
    }

    // ---- универсальное прозрачное окно поверх всего
    class LayerWin : Form
    {
        VapePet p; Bitmap bmp; Func<Size> size; Func<Point> pos; Action<Graphics> draw;
        IntPtr dc = IntPtr.Zero, dib = IntPtr.Zero, old = IntPtr.Zero, bits = IntPtr.Zero; int dw, dh;
        public LayerWin(VapePet owner, Func<Size> sz, Func<Point> ps, Action<Graphics> dr)
        {
            p = owner; size = sz; pos = ps; draw = dr;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(-30000, -30000, 10, 10);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80 | 0x08000000; return cp; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
        public Action<Point> LeftClick, Hover; public byte Alpha = 255;
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Right) { p.EndChase(); p.ShowMenu(Cursor.Position); } else if (e.Button == MouseButtons.Left && LeftClick != null) LeftClick(e.Location); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Hover != null) Hover(e.Location); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (Hover != null) Hover(new Point(-100, -100)); }
        void FreeDib()
        {
            try { if (dc != IntPtr.Zero && old != IntPtr.Zero) SelectObject(dc, old); if (dib != IntPtr.Zero) DeleteObject(dib); if (dc != IntPtr.Zero) DeleteDC(dc); } catch { }
            dc = dib = old = bits = IntPtr.Zero; dw = dh = 0;
        }
        protected override void OnFormClosed(FormClosedEventArgs e) { FreeDib(); base.OnFormClosed(e); }
        public void Render()
        {
            Size s = size(); int w = Math.Max(1, s.Width), h = Math.Max(1, s.Height);
            if (bmp == null || bmp.Width != w || bmp.Height != h) { if (bmp != null) bmp.Dispose(); bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb); }
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); draw(g); }
            if (dib == IntPtr.Zero || dw != w || dh != h)
            {
                FreeDib();
                IntPtr sdc = GetDC(IntPtr.Zero);
                try
                {
                    dc = CreateCompatibleDC(sdc);
                    BITMAPINFOHEADER bi = new BITMAPINFOHEADER(); bi.biSize = 40; bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
                    IntPtr b; dib = CreateDIBSection(dc, ref bi, 0, out b, IntPtr.Zero, 0);
                    if (dib == IntPtr.Zero) { FreeDib(); return; }
                    bits = b; old = SelectObject(dc, dib); dw = w; dh = h;
                }
                finally { ReleaseDC(IntPtr.Zero, sdc); }
            }
            BitmapData bd = new BitmapData(); bd.Width = w; bd.Height = h; bd.Stride = w * 4; bd.PixelFormat = PixelFormat.Format32bppPArgb; bd.Scan0 = bits;
            bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly | ImageLockMode.UserInputBuffer, PixelFormat.Format32bppPArgb, bd);
            bmp.UnlockBits(bd);
            Point at = pos();
            IntPtr screen = GetDC(IntPtr.Zero);
            try
            {
                SIZE sz2 = new SIZE(w, h); POINT src = new POINT(0, 0), dst = new POINT(at.X, at.Y);
                BLENDFUNCTION bf = new BLENDFUNCTION(); bf.SourceConstantAlpha = Alpha; bf.AlphaFormat = 1;
                UpdateLayeredWindow(Handle, screen, ref dst, ref sz2, dc, ref src, 0, ref bf, 2);
            }
            finally { ReleaseDC(IntPtr.Zero, screen); }
            if (p.tick % 30 == 0) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }
    }

    // ================= польза: загрузки, таймер, напоминания, курс, диск, уведомления =================
    public int frightLevel = 2;                       // 0 не пугается, 1 слабая, 2 умеренная, 3 сильная пугливость
    static readonly float[] FrightSpeedT = { 1e9f, 52, 34, 22 };
    static readonly float[] FrightStrongT = { 1e9f, 120, 75, 48 };
    static readonly int[] FrightNeedT = { 99, 3, 2, 1 };
    float FrightSpeed { get { return FrightSpeedT[frightLevel]; } }
    float FrightStrong { get { return FrightStrongT[frightLevel]; } }
    int FrightNeed { get { return FrightNeedT[frightLevel]; } }
    ToolStripMenuItem[] frightItems;
    static readonly string[] FrightNames = { "Не пугается", "Слабая", "Умеренная", "Сильная" };

    public bool dlWatch = true, dodgeNotes = true;
    int usefulTick;

    // ---------- уведомления других программ: кот отбегает и не спорит за передний план
    public Rectangle noteRect = Rectangle.Empty; int noteSeenT, noteSaidCool;
    public bool NoteOver { get { return noteSeenT > 0; } }

    void ScanNotes()
    {
        Rectangle found = Rectangle.Empty;
        try
        {
            Rectangle mine = Bounds;
            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                try
                {
                    if (!IsWindowVisible(h) || IsIconic(h)) return true;
                    if ((GetWindowLong(h, -20) & 0x8) == 0) return true;               // только «поверх всех»
                    uint pid; GetWindowThreadProcessId(h, out pid); if (pid == myPid) return true;
                    int cloaked; if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
                    StringBuilder sb = new StringBuilder(64); GetClassName(h, sb, 64); string cls = sb.ToString();
                    if (cls == "Shell_TrayWnd" || cls == "Progman" || cls == "WorkerW") return true;
                    RECT rc; if (!GetWindowRect(h, out rc)) return true;
                    Rectangle r = Rectangle.FromLTRB(rc.L, rc.T, rc.R, rc.B);
                    if (r.Width < 40 || r.Height < 30 || r.Width > 760 || r.Height > 560) return true;
                    if (!r.IntersectsWith(mine)) return true;
                    found = found.IsEmpty ? r : Rectangle.Union(found, r);
                }
                catch { }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        if (!found.IsEmpty) { noteRect = found; noteSeenT = 45; }
    }

    // уведомление закрывает погоду/панель → прячем их, чтобы не было каши
    bool HudUnderNote()
    {
        if (!NoteOver || noteRect.IsEmpty) return false;
        Rectangle hud = new Rectangle(Left + Width - (int)W(90), Top, (int)W(90), (int)(W(30) + W(9.6f) * 7));
        return hud.IntersectsWith(noteRect);
    }

    void NoteStep()
    {
        if (noteSeenT > 0) noteSeenT--;
        if (noteSaidCool > 0) noteSaidCool--;
        if (!dodgeNotes || !NoteOver || mode != 0 || pendingMode >= 0 || me.Hidden || dragging) return;
        // занятая полоса в координатах окна котика
        float l = noteRect.Left - Left - U(16), r = noteRect.Right - Left + U(16);
        float catSY = Top + Ground;
        bool covered = me.X > l && me.X < r && noteRect.Bottom > catSY - U(40) && noteRect.Top < catSY + U(4);
        if (!covered) return;
        float tx = l > U(20) ? Math.Max(U(14), l - U(10)) : Math.Min(Width - U(14), r + U(10));
        if (Math.Abs(tx - me.X) < U(3)) return;
        if (sleeping) { sleeping = false; me.Lying = false; DropSleepTalk(); }
        me.Phase = 0; me.T = 300; me.PuffT = 0; me.Hold = me.Hold == 'U' ? ' ' : me.Hold;
        Go(me, tx); me.TZ = me.Z; me.Speed = 2.6f;
        if (noteSaidCool == 0) { noteSaidCool = 30 * 20; Say(new string[] { "Не буду мешать!", "Уведомление! Отбегаю", "Ой, тебе пишут" }[rnd.Next(3)], me.X, CatTop(me) - U(1), 'U'); }
    }

    // ---------- загрузки браузера
    class DlFile { public string Path; public long Size, Prev; public float Speed; public DateTime Seen; }
    Dictionary<string, DlFile> dls = new Dictionary<string, DlFile>();
    List<string> dlDirs;
    string dlDonePath; int dlDoneT;

    List<string> DownloadDirs()
    {
        List<string> d = new List<string>();
        try { d.Add(Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", "Downloads")); } catch { }
        string la = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
        string[] prefs = {
            @"Yandex\YandexBrowser\User Data\Default\Preferences", @"Google\Chrome\User Data\Default\Preferences",
            @"Microsoft\Edge\User Data\Default\Preferences", @"BraveSoftware\Brave-Browser\User Data\Default\Preferences" };
        foreach (string pf in prefs)
        {
            try
            {
                string f = Path.Combine(la, pf); if (!File.Exists(f)) continue;
                string txt;
                using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8)) txt = sr.ReadToEnd();
                Match m = Regex.Match(txt, "\"default_directory\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                if (m.Success) d.Add(Regex.Unescape(m.Groups[1].Value));
            }
            catch { }
        }
        List<string> res = new List<string>();
        foreach (string x in d)
        {
            try { if (x.Length > 0 && Directory.Exists(x)) { string full = Path.GetFullPath(x).TrimEnd('\\'); bool dup = false; foreach (string y in res) if (string.Equals(y, full, StringComparison.OrdinalIgnoreCase)) dup = true; if (!dup) res.Add(full); } } catch { }
        }
        return res;
    }

    // Windows сам сообщает о новых и переименованных файлах — так ловятся даже мгновенные загрузки
    List<FileSystemWatcher> dlWatchers = new List<FileSystemWatcher>();
    List<string> dlEvents = new List<string>();
    Dictionary<string, int> dlPending = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    HashSet<string> dlAnnounced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    static bool IsPartial(string path)
    {
        string n = Path.GetFileName(path).ToLowerInvariant();
        return n.EndsWith(".crdownload") || n.EndsWith(".part") || n.EndsWith(".partial") || n.EndsWith(".tmp") || n.EndsWith(".download") || n.StartsWith("~$") || n == "desktop.ini" || n.StartsWith(".");
    }

    void SetupWatchers()
    {
        foreach (FileSystemWatcher w in dlWatchers) { try { w.EnableRaisingEvents = false; w.Dispose(); } catch { } }
        dlWatchers.Clear();
        foreach (string dir in dlDirs)
        {
            try
            {
                FileSystemWatcher w = new FileSystemWatcher(dir);
                w.IncludeSubdirectories = false;
                w.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite;
                w.Created += delegate (object o, FileSystemEventArgs e) { lock (dlEvents) dlEvents.Add(e.FullPath); };
                w.Renamed += delegate (object o, RenamedEventArgs e) { lock (dlEvents) dlEvents.Add(e.FullPath); };
                w.EnableRaisingEvents = true;
                dlWatchers.Add(w);
            }
            catch (Exception ex) { GuardLog("слежение за " + dir, ex); }
        }
    }

    void ScanDownloads()
    {
        if (dlDirs == null || usefulTick % (30 * 120) == 15)
        {
            List<string> nd = DownloadDirs();
            bool changed = dlDirs == null || nd.Count != dlDirs.Count;
            if (!changed) for (int i = 0; i < nd.Count; i++) if (!string.Equals(nd[i], dlDirs[i], StringComparison.OrdinalIgnoreCase)) changed = true;
            dlDirs = nd;
            if (changed || dlWatchers.Count == 0) SetupWatchers();
        }
        // 1) сигналы от Windows: новый готовый файл — ждём секунду, пока допишется
        List<string> ev;
        lock (dlEvents) { ev = new List<string>(dlEvents); dlEvents.Clear(); }
        foreach (string f in ev) if (!IsPartial(f) && !dlAnnounced.Contains(f)) dlPending[f] = usefulTick;
        // 2) недокачанные файлы (большие загрузки): размер и скорость
        HashSet<string> now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in dlDirs)
        {
            foreach (string pat in new string[] { "*.crdownload", "*.part", "*.partial" })
            {
                string[] fs; try { fs = Directory.GetFiles(dir, pat); } catch { continue; }
                foreach (string f in fs)
                {
                    long sz = 0; try { sz = new FileInfo(f).Length; } catch { continue; }
                    now.Add(f);
                    DlFile d;
                    if (!dls.TryGetValue(f, out d))
                    {
                        d = new DlFile(); d.Path = f; d.Prev = sz; d.Seen = DateTime.Now; dls[f] = d;
                        if (dls.Count == 1) Say(new string[] { "О, что-то качается!", "Слежу за загрузкой", "Качаем!" }[rnd.Next(3)], me.X, CatTop(me) - U(1), 'U');
                    }
                    d.Speed = d.Speed * 0.6f + Math.Max(0, sz - d.Size) * 2 * 0.4f;
                    d.Size = sz;
                }
            }
        }
        List<string> gone = new List<string>();
        foreach (string k in dls.Keys) if (!now.Contains(k)) gone.Add(k);
        foreach (string k in gone)
        {
            DlFile d = dls[k]; dls.Remove(k);
            bool hasDone = false;
            foreach (string p in dlPending.Keys) if (string.Equals(Path.GetDirectoryName(p), Path.GetDirectoryName(k), StringComparison.OrdinalIgnoreCase)) hasDone = true;
            if (!hasDone)
            {
                string done = NewestFinished(Path.GetDirectoryName(k), d.Seen);
                if (done != null && !dlAnnounced.Contains(done)) dlPending[done] = usefulTick - 60;
                else if (done == null) Say("Загрузка отменилась", me.X, CatTop(me) - U(1), 'U');
            }
        }
        // 3) объявить готовые (одной фразой, если файлов несколько)
        List<string> ready = new List<string>();
        foreach (KeyValuePair<string, int> kv in dlPending)
            if (usefulTick - kv.Value >= 30) ready.Add(kv.Key);
        List<string> ok = new List<string>();
        foreach (string f in ready)
        {
            dlPending.Remove(f);
            try { if (File.Exists(f) && !IsPartial(f)) ok.Add(f); } catch { }
        }
        if (ok.Count > 0)
        {
            foreach (string f in ok) dlAnnounced.Add(f);
            if (dlAnnounced.Count > 500) dlAnnounced.Clear();
            string last = ok[ok.Count - 1];
            dlDonePath = last; dlDoneT = 30 * 25;
            string name = Path.GetFileName(last); if (name.Length > 26) name = name.Substring(0, 23) + "…";
            CatAttention();
            Say(ok.Count > 1 ? "Готово! Скачалось файлов: " + ok.Count : "Готово! " + name, me.X, CatTop(me) - U(1), 'G');
            Say("Нажми на меня — открою папку", me.X, CatTop(me) - U(8), 'U');
            me.HappyT = 60;
            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
        }
    }

    string NewestFinished(string dir, DateTime since)
    {
        try
        {
            FileInfo best = null;
            foreach (FileInfo f in new DirectoryInfo(dir).GetFiles())
            {
                string ext = f.Extension.ToLowerInvariant();
                if (ext == ".crdownload" || ext == ".part" || ext == ".partial" || ext == ".tmp") continue;
                if (f.LastWriteTime < since.AddSeconds(-5) && f.CreationTime < since.AddSeconds(-5)) continue;
                if ((DateTime.Now - f.LastWriteTime).TotalSeconds > 20 && (DateTime.Now - f.CreationTime).TotalSeconds > 20) continue;
                if (best == null || f.LastWriteTime > best.LastWriteTime) best = f;
            }
            return best != null ? best.FullName : null;
        }
        catch { return null; }
    }

    static string Mb(double bytes)
    {
        if (bytes >= 1024.0 * 1024 * 1024) return (bytes / 1024 / 1024 / 1024).ToString("0.0") + " ГБ";
        if (bytes >= 1024.0 * 1024) return (bytes / 1024 / 1024).ToString(bytes >= 100.0 * 1024 * 1024 ? "0" : "0.0") + " МБ";
        return Math.Max(1, (int)(bytes / 1024)) + " КБ";
    }

    // ---------- таймер и напоминания
    class Alarm { public DateTime At; public string Text; public bool Timer; public int Minutes; }
    List<Alarm> alarms = new List<Alarm>();
    string alarmText; int alarmT, alarmRepeat;

    void AddTimer(int minutes)
    {
        Alarm a = new Alarm(); a.At = DateTime.Now.AddMinutes(minutes); a.Timer = true; a.Minutes = minutes; a.Text = "Время вышло! (" + minutes + " мин)";
        alarms.Add(a);
        Say("Засекаю " + minutes + " мин", me.X, CatTop(me) - U(1), 'U');
    }

    void AskReminder()
    {
        using (Form f = new Form())
        {
            f.Text = "Напоминание"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterScreen;
            f.MinimizeBox = false; f.MaximizeBox = false; f.TopMost = true; f.ClientSize = new Size(360, 150); f.ShowInTaskbar = false;
            Label l1 = new Label(); l1.Text = "Когда (например 14:30 или через 20 мин — просто 20):"; l1.SetBounds(10, 10, 340, 18); f.Controls.Add(l1);
            TextBox t1 = new TextBox(); t1.SetBounds(10, 30, 120, 22); t1.Text = DateTime.Now.AddMinutes(30).ToString("HH:mm"); f.Controls.Add(t1);
            Label l2 = new Label(); l2.Text = "О чём напомнить:"; l2.SetBounds(10, 60, 340, 18); f.Controls.Add(l2);
            TextBox t2 = new TextBox(); t2.SetBounds(10, 80, 340, 22); t2.Text = "Позвонить поставщику"; f.Controls.Add(t2);
            Button ok = new Button(); ok.Text = "Напомнить"; ok.SetBounds(250, 112, 100, 28); ok.DialogResult = DialogResult.OK; f.Controls.Add(ok); f.AcceptButton = ok;
            if (f.ShowDialog() != DialogResult.OK) return;
            string w = t1.Text.Trim(); DateTime at;
            int mins;
            if (int.TryParse(w, out mins) && mins > 0 && mins < 24 * 60) at = DateTime.Now.AddMinutes(mins);
            else
            {
                TimeSpan ts;
                if (!TimeSpan.TryParse(w.Replace('.', ':'), out ts)) { Say("Не понял время :(", me.X, CatTop(me) - U(1), 'R'); return; }
                at = DateTime.Today.Add(ts); if (at <= DateTime.Now) at = at.AddDays(1);
            }
            Alarm a = new Alarm(); a.At = at; a.Text = t2.Text.Trim().Length > 0 ? t2.Text.Trim() : "Напоминание!"; alarms.Add(a);
            Say("Напомню в " + at.ToString("HH:mm"), me.X, CatTop(me) - U(1), 'U');
        }
    }

    void AskTimer()
    {
        using (Form f = new Form())
        {
            f.Text = "Таймер"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterScreen;
            f.MinimizeBox = false; f.MaximizeBox = false; f.TopMost = true; f.ClientSize = new Size(260, 90); f.ShowInTaskbar = false;
            Label l1 = new Label(); l1.Text = "Сколько минут:"; l1.SetBounds(10, 12, 240, 18); f.Controls.Add(l1);
            TextBox t1 = new TextBox(); t1.SetBounds(10, 34, 80, 22); t1.Text = "20"; f.Controls.Add(t1);
            Button ok = new Button(); ok.Text = "Старт"; ok.SetBounds(150, 52, 100, 28); ok.DialogResult = DialogResult.OK; f.Controls.Add(ok); f.AcceptButton = ok;
            if (f.ShowDialog() != DialogResult.OK) return;
            int m; if (int.TryParse(t1.Text.Trim(), out m) && m > 0 && m <= 24 * 60) AddTimer(m); else Say("Не понял :(", me.X, CatTop(me) - U(1), 'R');
        }
    }

    void AlarmStep()
    {
        for (int i = alarms.Count - 1; i >= 0; i--)
            if (DateTime.Now >= alarms[i].At)
            {
                alarmText = alarms[i].Text; alarmT = 30 * 60 * 3; alarmRepeat = 0; alarms.RemoveAt(i);
                CatAttention();
                try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
            }
        if (alarmT > 0)
        {
            alarmT--;
            if (alarmRepeat-- <= 0)
            {
                alarmRepeat = 30 * 5;
                Say(alarmText, me.X, CatTop(me) - U(1), 'R');
                if (!me.Jumping && !me.Hidden) Jump(me, me.X, 0, U(8), 14, null);
                me.HappyT = 30;
            }
            if (alarmT == 0) alarmText = null;
        }
    }

    // кот должен быть на виду: вернуть из прогулки/сценки, разбудить, в уголок
    void CatAttention()
    {
        try { if (roaming) EndRoam(); if (chasing) EndChase(); } catch { }
        if (sleeping) { sleeping = false; me.Lying = false; }
    }

    // клик по коту: выключить будильник / открыть скачанное
    bool UsefulClick()
    {
        if (alarmT > 0) { alarmT = 0; alarmText = null; Say("Понял, принял!", me.X, CatTop(me) - U(1), 'U'); return true; }
        if (dlDoneT > 0 && dlDonePath != null)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + dlDonePath + "\""); } catch { }
            dlDoneT = 0; return true;
        }
        return false;
    }

    // ---------- курс валют ЦБ
    void ShowRates()
    {
        Say("Смотрю курс…", me.X, CatTop(me) - U(1), 'U');
        ThreadPool.QueueUserWorkItem(delegate
        {
            string msg;
            try
            {
                msg = RatesText(true);
            }
            catch { msg = "Не достучался до ЦБ :("; }
            try { BeginInvoke((Action)delegate { Say(msg, me.X, CatTop(me) - U(1), 'G'); }); } catch { }
        });
    }
    static string HttpT(string url, int ms, Encoding enc)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url); rq.Timeout = ms; rq.ReadWriteTimeout = ms; rq.UserAgent = "Mozilla/5.0 VapePet/1.0";
        using (WebResponse rs = rq.GetResponse()) using (StreamReader sr = new StreamReader(rs.GetResponseStream(), enc)) return sr.ReadToEnd();
    }

    // курс: сначала cbr-xml-daily (со стрелками), если не отвечает — напрямую с cbr.ru
    static string RatesText(bool cny)
    {
        try
        {
            string j = HttpT("https://www.cbr-xml-daily.ru/daily_json.js", 7000, Encoding.UTF8);
            return "$ " + Rate(j, "USD") + "   € " + Rate(j, "EUR") + (cny ? "   ¥ " + Rate(j, "CNY") : "");
        }
        catch { }
        string x = HttpT("https://www.cbr.ru/scripts/XML_daily.asp", 15000, Encoding.GetEncoding(1251));
        return "$ " + RateX(x, "USD") + "   € " + RateX(x, "EUR") + (cny ? "   ¥ " + RateX(x, "CNY") : "");
    }
    static string RateX(string x, string code)
    {
        Match m = Regex.Match(x, "<CharCode>" + code + "</CharCode>.*?<Nominal>([0-9]+)</Nominal>.*?<Value>([0-9]+,[0-9]+)</Value>", RegexOptions.Singleline);
        if (!m.Success) throw new Exception("нет " + code);
        double v = double.Parse(m.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture) / double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        return v.ToString("0.00", new CultureInfo("ru-RU"));
    }

    static string Rate(string j, string code)
    {
        Match m = Regex.Match(j, "\"" + code + "\"\\s*:\\s*\\{[^}]*?\"Nominal\"\\s*:\\s*([0-9]+)[^}]*?\"Value\"\\s*:\\s*([0-9.]+)[^}]*?\"Previous\"\\s*:\\s*([0-9.]+)", RegexOptions.Singleline);
        if (!m.Success) return "?";
        double nom = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        double v = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / nom, p = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) / nom;
        string arrow = v > p + 0.005 ? "↑" : (v < p - 0.005 ? "↓" : "");
        return v.ToString("0.00", new CultureInfo("ru-RU")) + arrow;
    }

    // ---------- место на диске
    void CheckDisk()
    {
        try
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady || d.TotalSize < 20L * 1024 * 1024 * 1024) continue;
                double freeGb = d.AvailableFreeSpace / 1024.0 / 1024 / 1024;
                if (freeGb < 5 && Once("disk" + d.Name)) Say("На диске " + d.Name.TrimEnd('\\') + " осталось " + freeGb.ToString("0.0") + " ГБ!", me.X, CatTop(me) - U(1), 'R');
            }
        }
        catch { }
    }

    // ---------- значки над котом: таймеры и загрузки
    void DrawBadges(Graphics g)
    {
        List<string> lines = new List<string>();
        foreach (Alarm a in alarms)
        {
            TimeSpan left = a.At - DateTime.Now; if (left.TotalSeconds < 0) left = TimeSpan.Zero;
            if (a.Timer) lines.Add("Таймер " + (left.TotalHours >= 1 ? ((int)left.TotalHours) + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00") : left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00")));
            else if (left.TotalMinutes < 60) lines.Add("В " + a.At.ToString("HH:mm") + ": " + (a.Text.Length > 16 ? a.Text.Substring(0, 15) + "…" : a.Text));
        }
        if (dls.Count > 0)
        {
            long total = 0; float sp = 0; foreach (DlFile d in dls.Values) { total += d.Size; sp += d.Speed; }
            lines.Add("Качаю " + (dls.Count > 1 ? dls.Count + " файла · " : "") + Mb(total) + (sp > 1024 ? " · " + Mb(sp) + "/с" : ""));
        }
        NetBadges(lines);
        if (lines.Count == 0) return;
        float x = me.Hidden ? Width - U(40) : me.X, y = (me.Hidden ? Ground - U(30) : CatTop(me)) - U(9);
        using (Font f = new Font("Segoe UI", U(3.2f), FontStyle.Bold, GraphicsUnit.Pixel))
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                SizeF sz = g.MeasureString(lines[i], f);
                float w = sz.Width + U(2.4f), h = sz.Height + U(0.6f);
                float bx = Math.Max(1, Math.Min(Width - w - 1, x - w / 2)), by = y - h;
                using (GraphicsPath p = RoundRect(bx, by, w, h, h / 2))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 40, 42, 56))) g.FillPath(b, p);
                    using (Pen pen = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(1f, U(0.3f)))) g.DrawPath(pen, p);
                }
                using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString(lines[i], f, b, bx + U(1.2f), by + U(0.3f));
                y = by - U(1);
            }
        }
    }

    bool clipInited;
    public void UsefulStep()
    {
        usefulTick++;
        if (dodgeNotes && usefulTick % 10 == 3) ScanNotes();
        NoteStep();
        if (dlWatch && usefulTick % 15 == 7) ScanDownloads();
        if (dlDoneT > 0) dlDoneT--;
        AlarmStep();
        if (usefulTick % (30 * 600) == 300) CheckDisk();
    }

    void BuildUsefulMenu()
    {
        ToolStripMenuItem um = new ToolStripMenuItem("Полезное", Ico("✔", Color.FromArgb(120, 220, 140)));
        ToolStripMenuItem tm = new ToolStripMenuItem("Таймер", Ico("⏱", Color.FromArgb(255, 200, 90)));
        foreach (int m0 in new int[] { 5, 10, 15, 25, 30, 60 }) { int m = m0; tm.DropDownItems.Add(m + " мин", null, delegate { AddTimer(m); }); }
        tm.DropDownItems.Add("Своё время…", null, delegate { AskTimer(); });
        um.DropDownItems.Add(tm);
        um.DropDownItems.Add("Напоминание…", Ico("\U0001F514", Color.FromArgb(255, 210, 90)), delegate { AskReminder(); });
        um.DropDownItems.Add("Отменить таймеры и напоминания", null, delegate { alarms.Clear(); alarmT = 0; alarmText = null; Say("Всё отменил", me.X, CatTop(me) - U(1), 'U'); });
        um.DropDownItems.Add(new ToolStripSeparator());
        um.DropDownItems.Add("Курс доллара, евро, юаня", Ico("$", Color.FromArgb(120, 220, 140)), delegate { ShowRates(); });
        um.DropDownItems.Add(new ToolStripSeparator());
        ToolStripMenuItem dl = new ToolStripMenuItem("Следить за загрузками браузера", null, delegate (object o, EventArgs e) { dlWatch = !dlWatch; ((ToolStripMenuItem)o).Checked = dlWatch; if (!dlWatch) dls.Clear(); SaveSettings(); });
        dl.Checked = dlWatch; um.DropDownItems.Add(dl);
        ToolStripMenuItem nt = new ToolStripMenuItem("Убегать от уведомлений", null, delegate (object o, EventArgs e) { dodgeNotes = !dodgeNotes; ((ToolStripMenuItem)o).Checked = dodgeNotes; SaveSettings(); });
        nt.Checked = dodgeNotes; um.DropDownItems.Add(nt);
        menu.Items.Add(um);
    }

    ToolStripMenuItem BuildFrightMenu()
    {
        ToolStripMenuItem fm = new ToolStripMenuItem("Пугливость", Ico("!", Color.FromArgb(255, 120, 120)));
        frightItems = new ToolStripMenuItem[4];
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            frightItems[i] = new ToolStripMenuItem(FrightNames[i], null, delegate
            {
                frightLevel = idx; for (int k = 0; k < 4; k++) frightItems[k].Checked = k == idx; SaveSettings();
                Say(idx == 0 ? "Меня не напугать!" : (idx == 3 ? "Ой, я теперь всего боюсь…" : "Понял"), me.X, CatTop(me) - U(1), 'U');
            });
            frightItems[i].Checked = i == frightLevel;
            fm.DropDownItems.Add(frightItems[i]);
        }
        return fm;
    }
    // ================= буфер обмена, интернет и скорость, быстрые ссылки =================

    // ---------- история буфера обмена (только в памяти, на диск не пишется)
    [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr hwnd);
    List<string> clipHist = new List<string>();
    bool clipSelf; public bool clipOn = true;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x031D) { try { OnClipboard(); } catch (Exception ex) { GuardLog("буфер обмена", ex); } }
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == 0x4B1D) { try { ScreenHelper(); } catch (Exception ex) { GuardLog("экран", ex); } }
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == 0x4B1E) { try { if (MenuOpen) CloseMenu(); else ShowMenu(Cursor.Position); } catch (Exception ex) { GuardLog("меню", ex); } }
    }

    void ClipInit() { try { AddClipboardFormatListener(Handle); } catch (Exception ex) { GuardLog("буфер обмена", ex); } HotkeyInit(); }

    void OnClipboard()
    {
        if (!clipOn && !smartClip) return;
        if (clipSelf) { clipSelf = false; return; }
        bool smartOk = true;
        IDataObject d = null;
        for (int k = 0; k < 3 && d == null; k++) { try { d = Clipboard.GetDataObject(); } catch { Thread.Sleep(30); } }
        if (d == null) return;
        // менеджеры паролей помечают пароли — такое не запоминаем
        if (d.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing") || d.GetDataPresent("Clipboard Viewer Ignore")) return;
        if (!d.GetDataPresent(DataFormats.UnicodeText)) return;
        string t = d.GetData(DataFormats.UnicodeText) as string;
        if (string.IsNullOrEmpty(t) || t.Trim().Length == 0 || t.Length > 20000) return;
        if (clipOn) clipHist.Remove(t); if (clipOn) clipHist.Insert(0, t);
        while (clipHist.Count > 10) clipHist.RemoveAt(clipHist.Count - 1);
        if (smartOk) { string tt = t; BeginInvoke((Action)delegate { try { SmartClip(tt); } catch (Exception ex) { GuardLog("умный буфер", ex); } }); }
    }

    static string ClipLabel(string t)
    {
        string s = Regex.Replace(t.Trim(), "\\s+", " ");
        if (s.Length > 46) s = s.Substring(0, 44) + "…";
        return s.Replace("&", "&&");
    }

    void FillClipMenu(ToolStripMenuItem cm)
    {
        cm.DropDownItems.Clear();
        if (clipHist.Count == 0) { ToolStripMenuItem e = new ToolStripMenuItem("Пока пусто — скопируйте что-нибудь"); e.Enabled = false; cm.DropDownItems.Add(e); }
        for (int i = 0; i < clipHist.Count; i++)
        {
            string t = clipHist[i];
            cm.DropDownItems.Add((i + 1) + ". " + ClipLabel(t), null, delegate
            {
                try { clipSelf = true; Clipboard.SetText(t); clipHist.Remove(t); clipHist.Insert(0, t); Say("Скопировал, вставляй (Ctrl+V)", me.X, CatTop(me) - U(1), 'U'); }
                catch { clipSelf = false; Say("Буфер занят, попробуй ещё", me.X, CatTop(me) - U(1), 'R'); }
            });
        }
        cm.DropDownItems.Add(new ToolStripSeparator());
        cm.DropDownItems.Add("Очистить историю", null, delegate { clipHist.Clear(); Say("История буфера очищена", me.X, CatTop(me) - U(1), 'U'); });
        ToolStripMenuItem on = new ToolStripMenuItem("Запоминать копирования", null, delegate { clipOn = !clipOn; if (!clipOn) clipHist.Clear(); SaveSettings(); });
        on.Checked = clipOn; cm.DropDownItems.Add(on);
        ToolStripMenuItem sm = new ToolStripMenuItem("Подсказки при копировании (перевод, исправить…)", null, delegate { smartClip = !smartClip; SaveSettings(); });
        sm.Checked = smartClip; cm.DropDownItems.Add(sm);
    }

    // ---------- интернет: пропал / вернулся / тормозит, и замер скорости
    public bool netWatch = true, netShowSpeed;
    volatile bool netChecking; volatile int netLastMs = -1; volatile bool netLastOk = true;
    int netFails, netSlow, netCheckT; bool netDown; DateTime netDownSince; bool netSlowSaid;
    DateTime netBytesAt = DateTime.MinValue; float netRx, netTx;

    void NetCheckAsync()
    {
        if (netChecking) return;
        netChecking = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            bool ok = false; int ms = -1;
            foreach (string url in new string[] { "http://www.msftconnecttest.com/connecttest.txt", "https://www.google.com/generate_204", "https://ya.ru/" })
            {
                try
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
                    rq.Method = "HEAD"; rq.Timeout = 5000; rq.ReadWriteTimeout = 5000; rq.AllowAutoRedirect = false; rq.UserAgent = "VapePet/1.0";
                    rq.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                    using (HttpWebResponse rs = (HttpWebResponse)rq.GetResponse()) { }
                    ms = (int)sw.ElapsedMilliseconds; ok = true; break;
                }
                catch (WebException wex)
                {
                    if (wex.Response != null) { ok = true; ms = 999; try { wex.Response.Close(); } catch { } break; }   // сервер ответил ошибкой — сеть есть
                }
                catch { }
            }
            netLastOk = ok; netLastMs = ms; netChecking = false;
        });
    }

    void NetStep()
    {
        if (!netWatch) return;
        if (++netCheckT % (30 * 10) == 20) NetCheckAsync();          // раз в 10 секунд
        if (netCheckT % (30 * 10) != 20 + 30 * 7) return;            // результат смотрим через 7 с
        if (!netLastOk)
        {
            netFails++;
            if (netFails == 2 && !netDown)
            {
                netDown = true; netDownSince = DateTime.Now.AddSeconds(-10);
                CatAttention(); me.SweatT = 60;
                Say(new string[] { "Интернета нет!", "Интернет пропал!", "Сеть отвалилась!" }[rnd.Next(3)], me.X, CatTop(me) - U(1), 'R');
                try { System.Media.SystemSounds.Hand.Play(); } catch { }
            }
            else if (netDown && netFails % 18 == 0) Say("Интернета всё ещё нет…", me.X, CatTop(me) - U(1), 'R');
        }
        else
        {
            if (netDown)
            {
                TimeSpan off = DateTime.Now - netDownSince;
                Say("Интернет вернулся! Не было " + (off.TotalMinutes >= 1 ? (int)off.TotalMinutes + " мин" : (int)off.TotalSeconds + " с"), me.X, CatTop(me) - U(1), 'G');
                me.HappyT = 60;
            }
            netDown = false; netFails = 0;
            if (netLastMs > 2500) { if (++netSlow == 3 && !netSlowSaid) { netSlowSaid = true; Say("Интернет еле живой: отклик " + (netLastMs / 1000.0).ToString("0.0") + " с", me.X, CatTop(me) - U(1), 'R'); } }
            else { netSlow = 0; if (netLastMs > 0 && netLastMs < 800) netSlowSaid = false; }
        }
    }

    // текущий трафик компьютера (сколько сейчас качается/отдаётся)
    void NetTraffic()
    {
        if (!netShowSpeed && !bigOpen) return;
        try
        {
            long rx = 0, tx = 0;
            foreach (System.Net.NetworkInformation.NetworkInterface ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;
                System.Net.NetworkInformation.IPv4InterfaceStatistics st = ni.GetIPv4Statistics();
                rx += st.BytesReceived; tx += st.BytesSent;
            }
            DateTime now = DateTime.Now;
            if (netBytesAt != DateTime.MinValue)
            {
                double dt = Math.Max(0.2, (now - netBytesAt).TotalSeconds);
                float r = (float)((rx - netRxPrev) / dt), t = (float)((tx - netTxPrev) / dt);
                netRx = netRx * 0.5f + Math.Max(0, r) * 0.5f; netTx = netTx * 0.5f + Math.Max(0, t) * 0.5f;
            }
            netRxPrev = rx; netTxPrev = tx; netBytesAt = now;
        }
        catch { }
    }
    long netRxPrev, netTxPrev;

    static string Mbit(double bytesPerSec)
    {
        double mb = bytesPerSec * 8 / 1000000.0;
        if (mb >= 10) return mb.ToString("0") + " Мбит/с";
        if (mb >= 0.1) return mb.ToString("0.0") + " Мбит/с";
        return (bytesPerSec * 8 / 1000).ToString("0") + " Кбит/с";
    }

    // замер скорости: качаем тестовый файл ~10 МБ
    volatile bool speedRunning;
    // байт/с или -1
    static int MeasureSpeed(int capMs, long capBytes)
    {
        foreach (string url in new string[] { "https://speed.cloudflare.com/__down?bytes=" + capBytes, "https://speedtest.selectel.ru/10MB", "https://proof.ovh.net/files/10Mb.dat" })
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url); rq.Timeout = 10000; rq.ReadWriteTimeout = 10000; rq.UserAgent = "VapePet/1.0";
                rq.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                using (WebResponse rs = rq.GetResponse())
                using (Stream st = rs.GetResponseStream())
                {
                    byte[] buf = new byte[65536]; long total = 0; int n;
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    while ((n = st.Read(buf, 0, buf.Length)) > 0) { total += n; if (sw.ElapsedMilliseconds > capMs || total >= capBytes) break; }
                    if (total < 150000) continue;
                    return (int)(total / Math.Max(0.05, sw.Elapsed.TotalSeconds));
                }
            }
            catch { }
        }
        return -1;
    }

    void SpeedTest()
    {
        if (speedRunning) return;
        speedRunning = true;
        Say("Замеряю скорость…", me.X, CatTop(me) - U(1), 'U');
        ThreadPool.QueueUserWorkItem(delegate
        {
            string msg = null;
            int ping = -1;
            try
            {
                System.Diagnostics.Stopwatch pw = System.Diagnostics.Stopwatch.StartNew();
                HttpWebRequest pr = (HttpWebRequest)WebRequest.Create("http://www.msftconnecttest.com/connecttest.txt"); pr.Method = "HEAD"; pr.Timeout = 5000; pr.AllowAutoRedirect = false;
                try { using (pr.GetResponse()) { } } catch (WebException we) { if (we.Response == null) throw; we.Response.Close(); }
                ping = (int)pw.ElapsedMilliseconds;
            }
            catch { }
            foreach (string url in new string[] { "https://speed.cloudflare.com/__down?bytes=10000000", "https://speedtest.selectel.ru/10MB", "https://proof.ovh.net/files/10Mb.dat" })
            {
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url); rq.Timeout = 10000; rq.ReadWriteTimeout = 10000; rq.UserAgent = "VapePet/1.0";
                    rq.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                    using (WebResponse rs = rq.GetResponse())
                    using (Stream st = rs.GetResponseStream())
                    {
                        byte[] buf = new byte[65536]; long total = 0;
                        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                        int n;
                        while ((n = st.Read(buf, 0, buf.Length)) > 0) { total += n; if (sw.ElapsedMilliseconds > 10000) break; }
                        double sec = Math.Max(0.05, sw.Elapsed.TotalSeconds);
                        if (total < 200000) continue;
                        msg = "Скорость: " + Mbit(total / sec) + (ping > 0 ? ", отклик " + ping + " мс" : "");
                        netSpeedBps = (int)(total / sec); speedAt = DateTime.Now;
                        break;
                    }
                }
                catch { }
            }
            if (msg == null) msg = ping > 0 ? "Не получилось замерить, но сеть есть (отклик " + ping + " мс)" : "Не получилось: похоже, интернета нет";
            speedRunning = false;
            try { BeginInvoke((Action)delegate { Say(msg, me.X, CatTop(me) - U(1), 'G'); me.HappyT = 30; }); } catch { }
        });
    }

    // ---------- быстрые ссылки
    public List<string[]> links = new List<string[]>(); bool linksSaved;
    void DefaultLinks()
    {
        links.Clear();
        links.Add(new string[] { "МойСклад", "https://online.moysklad.ru/app/" });
        links.Add(new string[] { "Saby (СБИС)", "https://online.saby.ru/" });
        links.Add(new string[] { "Яндекс Почта", "https://mail.yandex.ru/" });
        links.Add(new string[] { "Google Таблицы", "https://docs.google.com/spreadsheets/" });
        links.Add(new string[] { "Папка «Загрузки»", Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", "Downloads") });
    }

    void OpenLink(string name, string target)
    {
        try
        {
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) || File.Exists(target) || Directory.Exists(target)) System.Diagnostics.Process.Start(target);
            else { Say("Не нашёл: " + target, me.X, CatTop(me) - U(1), 'R'); return; }
            Say("Открываю: " + name, me.X, CatTop(me) - U(1), 'U');
        }
        catch (Exception ex) { Say("Не открылось: " + ex.Message, me.X, CatTop(me) - U(1), 'R'); }
    }

    void FillLinksMenu(ToolStripMenuItem lm)
    {
        lm.DropDownItems.Clear();
        foreach (string[] l0 in links) { string[] l = l0; lm.DropDownItems.Add(l[0].Replace("&", "&&"), null, delegate { OpenLink(l[0], l[1]); }); }
        lm.DropDownItems.Add(new ToolStripSeparator());
        lm.DropDownItems.Add("Добавить ссылку или папку…", null, delegate { EditLink(-1); });
        if (links.Count > 0)
        {
            ToolStripMenuItem ed = new ToolStripMenuItem("Изменить или удалить");
            for (int i = 0; i < links.Count; i++) { int idx = i; ed.DropDownItems.Add(links[i][0].Replace("&", "&&"), null, delegate { EditLink(idx); }); }
            lm.DropDownItems.Add(ed);
        }
    }

    void EditLink(int idx)
    {
        using (Form f = new Form())
        {
            f.Text = idx < 0 ? "Новая ссылка" : "Ссылка"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterScreen;
            f.MinimizeBox = false; f.MaximizeBox = false; f.TopMost = true; f.ClientSize = new Size(420, 160); f.ShowInTaskbar = false;
            Label l1 = new Label(); l1.Text = "Название:"; l1.SetBounds(10, 10, 400, 18); f.Controls.Add(l1);
            TextBox t1 = new TextBox(); t1.SetBounds(10, 30, 400, 22); f.Controls.Add(t1);
            Label l2 = new Label(); l2.Text = "Адрес сайта или путь к папке/файлу:"; l2.SetBounds(10, 60, 400, 18); f.Controls.Add(l2);
            TextBox t2 = new TextBox(); t2.SetBounds(10, 80, 400, 22); f.Controls.Add(t2);
            if (idx >= 0) { t1.Text = links[idx][0]; t2.Text = links[idx][1]; }
            else { try { string c = Clipboard.GetText(); if (c != null && (c.StartsWith("http") || c.Contains(":\\"))) t2.Text = c.Trim(); } catch { } }
            Button ok = new Button(); ok.Text = "Сохранить"; ok.SetBounds(310, 120, 100, 28); ok.DialogResult = DialogResult.OK; f.Controls.Add(ok); f.AcceptButton = ok;
            Button del = new Button(); del.Text = "Удалить"; del.SetBounds(10, 120, 100, 28); del.DialogResult = DialogResult.Abort; del.Visible = idx >= 0; f.Controls.Add(del);
            DialogResult r = f.ShowDialog();
            if (r == DialogResult.Abort && idx >= 0) { Say("Удалил «" + links[idx][0] + "»", me.X, CatTop(me) - U(1), 'U'); links.RemoveAt(idx); SaveSettings(); return; }
            if (r != DialogResult.OK) return;
            string n = t1.Text.Trim(), u2 = t2.Text.Trim().Trim('"');
            if (u2.Length == 0) return;
            if (!u2.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !u2.Contains(":\\") && !u2.StartsWith("\\\\") && u2.Contains(".")) u2 = "https://" + u2;
            if (n.Length == 0) n = u2.Length > 30 ? u2.Substring(0, 30) : u2;
            n = n.Replace("|", "/");
            if (idx >= 0) links[idx] = new string[] { n, u2 }; else links.Add(new string[] { n, u2 });
            SaveSettings();
            Say("Запомнил «" + n + "»", me.X, CatTop(me) - U(1), 'U');
        }
    }

    // ---------- меню и шаг
    void BuildNetMenu()
    {
        ToolStripMenuItem lm = new ToolStripMenuItem("Ссылки", Ico("↗", Color.FromArgb(120, 190, 255)));
        lm.DropDownItems.Add("…");
        lm.DropDownOpening += delegate { FillLinksMenu(lm); };
        menu.Items.Insert(0, lm);
        ToolStripMenuItem cm = new ToolStripMenuItem("Буфер обмена", Ico("⎘", Color.FromArgb(230, 230, 240)));
        cm.DropDownItems.Add("…");
        cm.DropDownOpening += delegate { FillClipMenu(cm); };
        menu.Items.Insert(1, cm);
        ToolStripMenuItem nm = new ToolStripMenuItem("Интернет", Ico("⇅", Color.FromArgb(120, 220, 140)));
        nm.DropDownItems.Add("Проверить скорость", null, delegate { SpeedTest(); });
        ToolStripMenuItem w = new ToolStripMenuItem("Предупреждать, если пропал", null, delegate (object o, EventArgs e) { netWatch = !netWatch; ((ToolStripMenuItem)o).Checked = netWatch; if (!netWatch) netDown = false; SaveSettings(); });
        w.Checked = netWatch; nm.DropDownItems.Add(w);
        ToolStripMenuItem s = new ToolStripMenuItem("Показывать скорость над котом", null, delegate (object o, EventArgs e) { netShowSpeed = !netShowSpeed; ((ToolStripMenuItem)o).Checked = netShowSpeed; netBytesAt = DateTime.MinValue; SaveSettings(); });
        s.Checked = netShowSpeed; nm.DropDownItems.Add(s);
        menu.Items.Insert(2, nm);
        menu.Items.Insert(3, new ToolStripSeparator());
    }

    public void NetTick()
    {
        NetStep();
        if ((netShowSpeed || bigOpen) && tick % 30 == 11) NetTraffic();
    }

    // строки для значков над котом
    void NetBadges(List<string> lines)
    {
        if (netDown) lines.Add("Нет интернета " + ((int)(DateTime.Now - netDownSince).TotalMinutes) + " мин");
        if (netShowSpeed && netBytesAt != DateTime.MinValue) lines.Add("Сеть ↓ " + Mbit(netRx) + "  ↑ " + Mbit(netTx));
    }
    // ================= панель полезного (вместо здоровья и денег) =================
    public bool panelShow = true;
    string rateLine; bool rateFail; DateTime rateAt = DateTime.MinValue; volatile bool rateBusy;
    static string RateCache { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_rates.txt"); } }
    // автозамер скорости раз в 20 минут (короткий, до 6 с)
    volatile int netSpeedBps = -1; DateTime speedAt = DateTime.MinValue;
    List<KeyValuePair<RectangleF, Action>> panelHits = new List<KeyValuePair<RectangleF, Action>>();
    bool netChecked;

    void RatesQuiet()
    {
        if (rateBusy) return; rateBusy = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string l = RatesText(false);
                try { File.WriteAllText(RateCache, l, Encoding.UTF8); } catch { }
                BeginInvoke((Action)delegate { rateLine = l; rateFail = false; rateAt = DateTime.Now; });
            }
            catch { try { BeginInvoke((Action)delegate { rateFail = true; rateAt = DateTime.Now.AddMinutes(-118); }); } catch { } }
            rateBusy = false;
        });
    }

    // ---------- VPN и где «находится» компьютер
    volatile string vpnName;      // имя подключённого VPN или null
    volatile string vpnApp;       // запущено VPN-приложение, но туннеля нет
    string vpnAppPath;
    volatile string geoLine;      // «Финляндия, Хельсинки»
    DateTime vpnAt = DateTime.MinValue, geoAt = DateTime.MinValue; volatile bool vpnBusy; string vpnPrevState = "?";
    static readonly Regex VpnRx = new Regex(@"happ|xray|v2ray|wireguard|wintun|warp|outline|amnezia|openvpn|tap-windows|\btun\b|tunnel|vpn|nekoray|hiddify|clash|sing-box|proton|nord|windscribe|adguard", RegexOptions.IgnoreCase);
    static readonly string[] VpnProcs = { "Happ", "happd", "xray", "v2rayN", "AmneziaVPN", "Outline", "WireGuard", "Cloudflare WARP", "nekoray", "Hiddify", "clash-verge", "sing-box", "ProtonVPN", "NordVPN", "Windscribe", "openvpn-gui", "AdGuardVpn" };

    static string VpnTitle(string s)
    {
        string l = s.ToLowerInvariant();
        if (l.Contains("happ")) return "Happ"; if (l.Contains("amnezia")) return "Amnezia"; if (l.Contains("outline")) return "Outline";
        if (l.Contains("warp")) return "WARP"; if (l.Contains("wireguard")) return "WireGuard"; if (l.Contains("hiddify")) return "Hiddify";
        if (l.Contains("nekoray")) return "NekoRay"; if (l.Contains("proton")) return "Proton VPN"; if (l.Contains("nord")) return "NordVPN";
        if (l.Contains("openvpn") || l.Contains("tap-windows")) return "OpenVPN"; if (l.Contains("xray") || l.Contains("v2ray")) return "Xray";
        return s.Length > 14 ? s.Substring(0, 13) + "…" : s;
    }

    void VpnCheck(bool forceGeo)
    {
        if (vpnBusy) return; vpnBusy = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string name = null;
                foreach (System.Net.NetworkInformation.NetworkInterface ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    string id = ni.Name + " " + ni.Description;
                    if (Regex.IsMatch(id, "Miniport|Loopback|Hyper-V|VirtualBox|VMware|Teredo|isatap|6to4|IP-HTTPS", RegexOptions.IgnoreCase)) continue;
                    if (VpnRx.IsMatch(id)) { name = VpnTitle(id); break; }
                }
                string app = null, appPath = null;
                System.Diagnostics.Process[] all = System.Diagnostics.Process.GetProcesses();
                try
                {
                    foreach (string pn in VpnProcs)
                    {
                        foreach (System.Diagnostics.Process p in all)
                            if (string.Equals(p.ProcessName, pn, StringComparison.OrdinalIgnoreCase)) { app = VpnTitle(pn); try { appPath = p.MainModule.FileName; } catch { } break; }
                        if (app != null) break;
                    }
                }
                finally { foreach (System.Diagnostics.Process p in all) p.Dispose(); }
                if (appPath != null && appPath.EndsWith("happd.exe", StringComparison.OrdinalIgnoreCase)) appPath = Path.Combine(Path.GetDirectoryName(appPath), "Happ.exe");
                vpnName = name; vpnApp = name == null ? app : null; if (appPath != null) vpnAppPath = appPath;
                string state = name ?? "-";
                bool changed = state != vpnPrevState;
                if (changed && vpnPrevState != "?")
                {
                    string said = name != null ? "VPN " + name + " включился" : "VPN отключился!";
                    try { BeginInvoke((Action)delegate { Say(said, me.X, CatTop(me) - U(1), name != null ? 'G' : 'R'); }); } catch { }
                }
                vpnPrevState = state;
                if (changed || forceGeo || (DateTime.Now - geoAt).TotalMinutes >= 10)
                {
                    if (changed) geoLine = null;
                    geoAt = DateTime.Now;
                    try
                    {
                        if (changed) Thread.Sleep(1500);
                        geoLine = GeoLookup();
                    }
                    catch { }
                    if (geoLine == null) geoAt = DateTime.Now.AddMinutes(-9);
                }
            }
            catch { }
            vpnAt = DateTime.Now; vpnBusy = false;
        });
    }

    static string GeoLookup()
    {
        string[] urls = { "http://ip-api.com/json/?fields=status,country,city&lang=ru", "https://ipwho.is/?lang=ru", "https://ipinfo.io/json" };
        foreach (string u in urls)
        {
            try
            {
                string j = HttpT(u, 5000, Encoding.UTF8);
                Match c = Regex.Match(j, "\"country\"\\s*:\\s*\"([^\"]*)\""), t = Regex.Match(j, "\"city\"\\s*:\\s*\"([^\"]*)\"");
                if (!c.Success || c.Groups[1].Value.Length == 0) continue;
                string country = Regex.Unescape(c.Groups[1].Value), city = t.Success ? Regex.Unescape(t.Groups[1].Value) : "";
                if (country.Length == 2) country = CountryRu(country);
                return country + (city.Length > 0 ? ", " + city : "");
            }
            catch { }
        }
        try
        {
            string tr = HttpT("https://www.cloudflare.com/cdn-cgi/trace", 7000, Encoding.UTF8);
            Match m = Regex.Match(tr, "loc=([A-Z]{2})"), co = Regex.Match(tr, "colo=([A-Z]{3})");
            if (m.Success) { string city = co.Success && ColoCC(co.Groups[1].Value) == m.Groups[1].Value ? ColoCity(co.Groups[1].Value) : null; return CountryRu(m.Groups[1].Value) + (city != null ? ", " + city : ""); }
        }
        catch { }
        return null;
    }
    // город дата-центра Cloudflare, через который идёт трафик
    static string ColoCity(string c)
    {
        switch (c)
        {
            case "HEL": return "Хельсинки"; case "AMS": return "Амстердам"; case "FRA": return "Франкфурт"; case "DME": case "SVO": case "VKO": return "Москва";
            case "LED": return "Санкт-Петербург"; case "ARN": return "Стокгольм"; case "WAW": return "Варшава"; case "RIX": return "Рига"; case "TLL": return "Таллин";
            case "VNO": return "Вильнюс"; case "IST": return "Стамбул"; case "LHR": return "Лондон"; case "CDG": return "Париж"; case "VIE": return "Вена";
            case "PRG": return "Прага"; case "ZRH": return "Цюрих"; case "DUS": return "Дюссельдорф"; case "HAM": return "Гамбург"; case "MUC": return "Мюнхен";
            case "BER": case "TXL": return "Берлин"; case "ALA": return "Алматы"; case "NQZ": return "Астана"; case "EVN": return "Ереван"; case "TBS": return "Тбилиси";
            case "DXB": return "Дубай"; case "KJA": return "Красноярск"; case "SVX": return "Екатеринбург"; case "OVB": return "Новосибирск"; case "KZN": return "Казань";
            case "MSQ": return "Минск"; case "KBP": return "Киев"; case "OSL": return "Осло"; case "CPH": return "Копенгаген"; case "MAD": return "Мадрид";
            case "MXP": return "Милан"; case "SIN": return "Сингапур"; case "NRT": return "Токио"; case "IAD": return "Вашингтон"; case "EWR": return "Нью-Йорк";
            default: return null;
        }
    }

    static string ColoCC(string c)
    {
        switch (c)
        {
            case "HEL": return "FI"; case "AMS": return "NL"; case "FRA": case "DUS": case "HAM": case "MUC": case "BER": case "TXL": return "DE";
            case "DME": case "SVO": case "VKO": case "LED": case "KJA": case "SVX": case "OVB": case "KZN": return "RU";
            case "ARN": return "SE"; case "WAW": return "PL"; case "RIX": return "LV"; case "TLL": return "EE"; case "VNO": return "LT"; case "IST": return "TR";
            case "LHR": return "GB"; case "CDG": return "FR"; case "VIE": return "AT"; case "PRG": return "CZ"; case "ZRH": return "CH"; case "ALA": case "NQZ": return "KZ";
            case "EVN": return "AM"; case "TBS": return "GE"; case "DXB": return "AE"; case "MSQ": return "BY"; case "KBP": return "UA"; case "OSL": return "NO";
            case "CPH": return "DK"; case "MAD": return "ES"; case "MXP": return "IT"; case "SIN": return "SG"; case "NRT": return "JP"; case "IAD": case "EWR": return "US";
            default: return "";
        }
    }

    static string CountryRu(string cc)
    {
        switch (cc.ToUpperInvariant())
        {
            case "RU": return "Россия"; case "FI": return "Финляндия"; case "NL": return "Нидерланды"; case "DE": return "Германия";
            case "SE": return "Швеция"; case "EE": return "Эстония"; case "LV": return "Латвия"; case "LT": return "Литва"; case "PL": return "Польша";
            case "KZ": return "Казахстан"; case "TR": return "Турция"; case "US": return "США"; case "GB": return "Великобритания"; case "FR": return "Франция";
            case "AE": return "ОАЭ"; case "AM": return "Армения"; case "GE": return "Грузия"; case "BY": return "Беларусь"; case "UA": return "Украина";
            case "JP": return "Япония"; case "SG": return "Сингапур"; case "CH": return "Швейцария"; case "AT": return "Австрия"; case "CZ": return "Чехия";
            default: return cc;
        }
    }

    void OpenVpnApp()
    {
        try { if (vpnAppPath != null && File.Exists(vpnAppPath)) { System.Diagnostics.Process.Start(vpnAppPath); return; } } catch { }
        string h = @"C:\Program Files\FlyFrogLLC\Happ\Happ.exe";
        try { if (File.Exists(h)) System.Diagnostics.Process.Start(h); else VpnCheck(true); } catch { }
    }

    void PanelTick()
    {
        if (panelShow && tick > 30 * 3 && (DateTime.Now - vpnAt).TotalSeconds >= 6) VpnCheck(false);
        if (!panelShow) return;
        if (rateLine == null && rateAt == DateTime.MinValue) { try { if (File.Exists(RateCache)) rateLine = File.ReadAllText(RateCache, Encoding.UTF8).Trim(); } catch { } }
        if ((DateTime.Now - rateAt).TotalHours >= 2 && tick > 30 * 8) RatesQuiet();
        if (netWatch && !netDown && !speedRunning && tick > 30 * 20 && (DateTime.Now - speedAt).TotalMinutes >= 20)
        {
            speedAt = DateTime.Now; speedRunning = true;
            ThreadPool.QueueUserWorkItem(delegate { try { int b = MeasureSpeed(6000, 4000000); if (b > 0) netSpeedBps = b; } catch { } speedRunning = false; });
        }
        if (netLastMs != -1 || !netLastOk) netChecked = true;
    }

    // строки панели: текст, цвет точки (Empty — без точки), действие по клику
    class PRow { public string Text; public Color Dot; public Action Click; public Color TextC = Color.FromArgb(232, 235, 242); }

    List<PRow> PanelRows()
    {
        List<PRow> rows = new List<PRow>();
        PRow r;
        if (netWatch)
        {
            r = new PRow(); r.Click = delegate { SpeedTest(); };
            if (netDown) { r.Text = "Нет интернета · " + (int)(DateTime.Now - netDownSince).TotalMinutes + " мин"; r.Dot = Color.FromArgb(240, 80, 80); r.TextC = Color.FromArgb(255, 170, 170); }
            else if (!netChecked) { r.Text = "Интернет: проверяю…"; r.Dot = Color.FromArgb(150, 156, 175); }
            else
            {
                int sp = netSpeedBps;
                bool slow = sp > 0 ? sp < 125000 : netLastMs > 2500;
                r.Text = (slow ? "Интернет медленный" : "Интернет есть") + (sp > 0 ? " · " + Mbit(sp) : (speedRunning ? " · замеряю…" : ""));
                r.Dot = slow ? Color.FromArgb(240, 190, 60) : Color.FromArgb(90, 210, 120);
            }
            rows.Add(r);
        }
        if (vpnPrevState != "?")
        {
            r = new PRow(); r.Click = delegate { OpenVpnApp(); };
            if (vpnName != null) { r.Text = "VPN " + vpnName + (geoLine != null ? " · " + geoLine : " · подключён"); r.Dot = Color.FromArgb(90, 210, 120); }
            else if (vpnApp != null) { r.Text = "VPN " + vpnApp + " не подключён" + (geoLine != null ? " · " + geoLine : ""); r.Dot = Color.FromArgb(240, 190, 60); r.TextC = Color.FromArgb(255, 220, 150); }
            else { r.Text = "VPN выкл" + (geoLine != null ? " · " + geoLine : ""); r.Dot = Color.FromArgb(150, 156, 175); }
            rows.Insert(0, r);
        }
        if (netShowSpeed && netBytesAt != DateTime.MinValue) { r = new PRow(); r.Text = "↓ " + Mbit(netRx) + "   ↑ " + Mbit(netTx); r.Dot = Color.Empty; rows.Add(r); }
        r = new PRow(); r.Text = rateLine ?? (rateFail ? "Курс: ЦБ не ответил, повторю" : "Курс: загружаю…"); r.Dot = Color.Empty; r.Click = delegate { RatesQuiet(); ShowRates(); }; rows.Add(r);
        foreach (Alarm a in alarms)
        {
            TimeSpan left = a.At - DateTime.Now; if (left.TotalSeconds < 0) left = TimeSpan.Zero;
            r = new PRow(); r.Dot = Color.FromArgb(255, 170, 70);
            if (a.Timer) r.Text = "Таймер " + (left.TotalHours >= 1 ? ((int)left.TotalHours) + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00") : left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00"));
            else { if (left.TotalHours > 12) continue; r.Text = a.At.ToString("HH:mm") + " · " + (a.Text.Length > 18 ? a.Text.Substring(0, 17) + "…" : a.Text); }
            rows.Add(r);
        }
        if (alarmT > 0 && alarmText != null) { r = new PRow(); r.Text = "Звенит! Нажми на кота"; r.Dot = Color.FromArgb(240, 80, 80); rows.Add(r); }
        if (dls.Count > 0)
        {
            long total = 0; float sp = 0; foreach (DlFile d in dls.Values) { total += d.Size; sp += d.Speed; }
            r = new PRow(); r.Dot = Color.FromArgb(90, 160, 240);
            r.Text = "Качаю " + (dls.Count > 1 ? dls.Count + " ф. · " : "") + Mb(total) + (sp > 1024 ? " · " + Mb(sp) + "/с" : "");
            string first = null; foreach (DlFile d in dls.Values) { first = d.Path; break; }
            r.Click = delegate { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + first + "\""); } catch { } };
            rows.Add(r);
        }
        else if (dlDoneT > 0 && dlDonePath != null)
        {
            r = new PRow(); r.Dot = Color.FromArgb(90, 210, 120); string n = Path.GetFileName(dlDonePath); if (n.Length > 20) n = n.Substring(0, 19) + "…";
            r.Text = "Скачано: " + n; string pth = dlDonePath;
            r.Click = delegate { try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + pth + "\""); } catch { } }; rows.Add(r);
        }
        return rows;
    }

    void DrawPanel(Graphics g)
    {
        panelHits.Clear();
        if (!panelShow) return;
        List<PRow> rows = PanelRows();
        if (rows.Count == 0) return;
        // Компактная стеклянная панель: вся прежняя информация и клики сохранены.
        // Эффекты статические — без дополнительных таймеров и тяжёлых анимаций.
        float w = W(84), rh = W(9.6f), pad = W(1.8f), head = W(6.2f);
        float x = Width - w - W(2), y = (WxVisible ? WidgetRect().Bottom : 0) + W(2);
        RectangleF card = new RectangleF(x, y, w, rows.Count * rh + pad * 2 + head);
        panelCardRect = card;
        using (GraphicsPath p = RoundRect(card.X, card.Y, card.Width, card.Height, W(3.2f)))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(card,
                Color.FromArgb(202, 36, 43, 59), Color.FromArgb(190, 17, 22, 34), 90f)) g.FillPath(b, p);
            using (Pen pen = new Pen(Color.FromArgb(145, 190, 210, 238), Math.Max(1f, W(0.28f)))) g.DrawPath(pen, p);
            RectangleF shine = new RectangleF(card.X + W(1), card.Y + W(0.7f), card.Width - W(2), W(0.35f));
            using (GraphicsPath sp = RoundRect(shine.X, shine.Y, shine.Width, shine.Height, W(0.2f)))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(105, 255, 255, 255))) g.FillPath(sb, sp);
        }
        System.Drawing.Text.TextRenderingHint oldHint = g.TextRenderingHint;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using (Font hf = new Font("Segoe UI Semibold", W(3.5f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (SolidBrush hb = new SolidBrush(Color.FromArgb(205, 205, 220, 242)))
        using (Font f = new Font("Segoe UI Semibold", W(4.35f), FontStyle.Regular, GraphicsUnit.Pixel))
        using (StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone())
        {
            sf.LineAlignment = StringAlignment.Center; sf.Trimming = StringTrimming.EllipsisCharacter; sf.FormatFlags = StringFormatFlags.NoWrap;
            Paw(g, x + pad + W(2.2f), y + W(3.3f), W(1.5f), Color.FromArgb(150, 200, 255));
            g.DrawString("VapePet", hf, hb, new PointF(x + pad + W(4.4f), y + W(1.4f)));
            RectangleF ob = new RectangleF(x + w - pad - W(27), y + W(0.9f), W(26), W(4.6f));
            using (GraphicsPath op = RoundRect(ob.X, ob.Y, ob.Width, ob.Height, ob.Height / 2))
            {
                using (LinearGradientBrush lb = new LinearGradientBrush(ob, Color.FromArgb(230, 90, 150, 255), Color.FromArgb(230, 50, 100, 220), 90f)) g.FillPath(lb, op);
                using (Pen opn = new Pen(Color.FromArgb(160, 255, 255, 255), 1f)) g.DrawPath(opn, op);
            }
            using (StringFormat oc = new StringFormat()) { oc.Alignment = StringAlignment.Center; oc.LineAlignment = StringAlignment.Center; g.DrawString("Открыть ▸", hf, Brushes.White, ob, oc); }
            panelHits.Add(new KeyValuePair<RectangleF, Action>(new RectangleF(x, y, w, head), delegate { BigOpen(); }));
            using (Pen hp = new Pen(Color.FromArgb(65, 210, 225, 245), 1f))
                g.DrawLine(hp, x + pad, y + head - W(0.3f), x + w - pad, y + head - W(0.3f));
            for (int i = 0; i < rows.Count; i++)
            {
                PRow r = rows[i];
                RectangleF rr = new RectangleF(x + pad, y + head + pad + i * rh, w - pad * 2, rh - W(0.55f));
                using (GraphicsPath rp = RoundRect(rr.X, rr.Y, rr.Width, rr.Height, W(1.8f)))
                {
                    using (SolidBrush rb = new SolidBrush(Color.FromArgb(42, 230, 239, 255))) g.FillPath(rb, rp);
                    using (Pen rpen = new Pen(Color.FromArgb(35, 235, 242, 255), 1f)) g.DrawPath(rpen, rp);
                }
                float tx = rr.X + W(2.1f);
                if (r.Dot != Color.Empty)
                {
                    float d = W(2.25f), cy = rr.Y + rr.Height / 2;
                    using (SolidBrush halo = new SolidBrush(Color.FromArgb(38, r.Dot))) g.FillEllipse(halo, tx - W(0.75f), cy - W(1.85f), d + W(1.5f), d + W(1.5f));
                    using (SolidBrush dot = new SolidBrush(r.Dot)) g.FillEllipse(dot, tx, cy - d / 2, d, d);
                    tx += d + W(1.6f);
                }
                using (SolidBrush tb = new SolidBrush(r.TextC)) g.DrawString(r.Text, f, tb, new RectangleF(tx, rr.Y, rr.Right - tx - W(1.1f), rr.Height), sf);
                if (r.Click != null) panelHits.Add(new KeyValuePair<RectangleF, Action>(rr, r.Click));
            }
        }
        g.TextRenderingHint = oldHint;
    }

    bool PanelClick(Point pt)
    {
        foreach (KeyValuePair<RectangleF, Action> h in panelHits) if (h.Key.Contains(pt)) { try { h.Value(); } catch { } hudDirty = true; return true; }
        return false;
    }
    // ================= умный буфер обмена и «котик видит экран» =================
    public bool smartClip = true;
    volatile bool smartBusy;

    // единый вызов ИИ без «кошачьей» роли; картинка — base64 JPEG или null
    string CallAIRaw(string sys, string user, string imgB64, int maxTokens)
    {
        string fmt = aiFormat, url = aiUrl, model = aiModel, key = aiKey;
        if (key.Length == 0) throw new Exception("нет ключа ИИ: меню котика → Настройки → Настройки ИИ");
        bool groq = url.Contains("api.groq.com");
        if (imgB64 != null && groq) model = "qwen/qwen3.8-27b";          // у Groq картинки понимает эта модель
        StringBuilder b = new StringBuilder();
        if (fmt == "anthropic")
        {
            b.Append("{\"model\":\"").Append(JEsc(model)).Append("\",\"max_tokens\":").Append(maxTokens).Append(",\"system\":\"").Append(JEsc(sys)).Append("\",\"messages\":[{\"role\":\"user\",\"content\":[");
            if (imgB64 != null) b.Append("{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/jpeg\",\"data\":\"").Append(imgB64).Append("\"}},");
            b.Append("{\"type\":\"text\",\"text\":\"").Append(JEsc(user)).Append("\"}]}]}");
        }
        else
        {
            b.Append("{\"model\":\"").Append(JEsc(model)).Append("\",\"max_tokens\":").Append(Math.Max(maxTokens, groq || url.Contains("generativelanguage") ? 2000 : maxTokens)).Append(",");
            if (groq && model.Contains("gpt-oss")) b.Append("\"reasoning_effort\":\"low\",");
            if (groq && model.Contains("qwen")) b.Append("\"reasoning_effort\":\"none\",");
            b.Append("\"messages\":[{\"role\":\"system\",\"content\":\"").Append(JEsc(sys)).Append("\"},{\"role\":\"user\",\"content\":");
            if (imgB64 == null) b.Append("\"").Append(JEsc(user)).Append("\"");
            else b.Append("[{\"type\":\"text\",\"text\":\"").Append(JEsc(user)).Append("\"},{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/jpeg;base64,").Append(imgB64).Append("\"}}]");
            b.Append("}]}");
        }
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        ServicePointManager.Expect100Continue = false;
        byte[] data = Encoding.UTF8.GetBytes(b.ToString());
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
                rq.Method = "POST"; rq.ContentType = "application/json"; rq.Timeout = 90000; rq.ReadWriteTimeout = 90000;
                if (fmt == "anthropic") { rq.Headers["x-api-key"] = key; rq.Headers["anthropic-version"] = "2023-06-01"; }
                else rq.Headers[HttpRequestHeader.Authorization] = "Bearer " + key;
                rq.ContentLength = data.Length;
                using (Stream s = rq.GetRequestStream()) s.Write(data, 0, data.Length);
                string resp;
                using (WebResponse rs = rq.GetResponse()) using (StreamReader sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8)) resp = sr.ReadToEnd();
                string text = fmt == "anthropic" ? JValueAfter(resp, "\"content\"", "text") : JValueAfter(resp, "\"message\"", "content");
                if (text == null) throw new Exception("непонятный ответ сервера");
                return CleanText(text);
            }
            catch (WebException we)
            {
                if (we.Response == null && attempt < 3) { Thread.Sleep(1500 * attempt); continue; }
                string msg = we.Message;
                if (we.Response != null)
                    try { using (StreamReader sr = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8)) { string m2 = JValueAfter(sr.ReadToEnd(), "\"error\"", "message"); if (m2 != null) msg = m2; } } catch { }
                if (we.Response == null) msg = "нет связи с сервером ИИ (проверь VPN)";
                throw new Exception(msg);
            }
        }
    }

    // ---------------- общее тёмное окошко с кнопками ----------------
    static readonly Color SBg = Color.FromArgb(28, 30, 40), SBg2 = Color.FromArgb(44, 47, 62), STx = Color.FromArgb(235, 238, 245), SDim = Color.FromArgb(140, 146, 165), SAcc = Color.FromArgb(255, 158, 64);

    static Button SButtonS(string text, bool accent)
    {
        Button b = SButton(text, accent); b.Font = new Font("Segoe UI Semibold", 8.5f); b.Padding = new Padding(4, 0, 4, 0); b.Margin = new Padding(0, 0, 4, 4);
        return b;
    }

    static Button SButton(string text, bool accent)
    {
        Button b = new Button(); b.Text = text; b.FlatStyle = FlatStyle.Flat; b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        b.FlatAppearance.BorderSize = 0; b.BackColor = accent ? SAcc : SBg2; b.ForeColor = accent ? Color.FromArgb(30, 20, 10) : STx;
        b.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(255, 180, 100) : Color.FromArgb(62, 66, 86);
        b.Font = new Font("Segoe UI Semibold", 9.5f); b.Padding = new Padding(8, 3, 8, 3); b.Margin = new Padding(0, 0, 6, 6); b.Cursor = Cursors.Hand; b.TabStop = false;
        return b;
    }

    class NoActForm : Form
    {
        public bool NoAct = true;
        protected override bool ShowWithoutActivation { get { return NoAct; } }
        protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x8; if (NoAct) cp.ExStyle |= 0x08000000; return cp; } }
    }

    static void RoundForm(Form f)
    {
        f.Paint += delegate (object o, PaintEventArgs e) { using (Pen p = new Pen(Color.FromArgb(70, 76, 98), 1)) e.Graphics.DrawRectangle(p, 0, 0, f.Width - 1, f.Height - 1); };
    }

    // ================= 1. умный буфер обмена =================
    NoActForm clipPop; FlowLayoutPanel clipBtns; Label clipHead; TextBox clipOut; System.Windows.Forms.Timer clipPopT;
    string clipLastOffer, clipOrig, clipResult; int clipPopLeft; DateTime clipOfferAt;

    void SmartClip(string t)
    {
        if (!smartClip || smartBusy) return;
        if (t == clipResult && clipOut != null && clipOut.Visible) return;                 // это мы сами положили ответ ИИ
        if (t == clipLastOffer && (DateTime.Now - clipOfferAt).TotalMilliseconds < 1500) return;   // двойное событие от программы
        try { uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid); if (pid == myPid) return; } catch { }
        string s = t.Trim();
        List<KeyValuePair<string, Action>> acts = new List<KeyValuePair<string, Action>>();
        string info = null;

        // деньги в валюте → рубли
        Match mm = Regex.Match(s, @"^(?:(?<c>[$€¥])\s*(?<n>\d[\d\s.,]*)|(?<n>\d[\d\s.,]*)\s*(?<c>\$|€|¥|usd|eur|cny|долл\w*|евро|юан\w*))$", RegexOptions.IgnoreCase);
        if (mm.Success && s.Length < 40)
        {
            double n; string ns = Regex.Replace(mm.Groups["n"].Value, @"\s", "");
            if (ns.Contains(",") && !ns.Contains(".")) ns = ns.Replace(',', '.'); else ns = ns.Replace(",", "");
            string c = mm.Groups["c"].Value.ToLowerInvariant();
            double rate = c.StartsWith("$") || c.StartsWith("usd") || c.StartsWith("долл") ? RateNum('$') : c.StartsWith("€") || c.StartsWith("eur") || c.StartsWith("евр") ? RateNum('€') : RateNum('¥');
            if (double.TryParse(ns, NumberStyles.Float, CultureInfo.InvariantCulture, out n) && rate > 0)
            {
                string rub = (n * rate).ToString("#,0", new CultureInfo("ru-RU")) + " ₽";
                info = s + " ≈ " + rub;
                string rubCopy = Math.Round(n * rate).ToString(CultureInfo.InvariantCulture);
                acts.Add(new KeyValuePair<string, Action>("Скопировать " + rub, delegate { ClipPut(rubCopy); ClipPopHide(); Say("Скопировал " + rub, me.X, CatTop(me) - U(1), 'G'); }));
            }
        }
        // трек-номер
        else if (Regex.IsMatch(s, @"^[A-Z]{2}\d{9}[A-Z]{2}$") || Regex.IsMatch(s, @"^\d{14}$"))
        {
            info = "Похоже на трек-номер";
            acts.Add(new KeyValuePair<string, Action>("Где посылка?", delegate { OpenUrl("https://www.pochta.ru/tracking?barcode=" + s); ClipPopHide(); }));
        }
        else if (s.Length >= 12 && s.Contains(" ") && s.Length <= 12000)
        {
            int lat = 0, cyr = 0;
            foreach (char ch in s) { if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z')) lat++; else if (ch >= 'а' && ch <= 'я' || ch >= 'А' && ch <= 'Я' || ch == 'ё' || ch == 'Ё') cyr++; }
            if (Regex.IsMatch(s, @"^\s*(https?://|www\.)\S+\s*$")) return;
            if (lat > 10 && lat > cyr * 3)
            {
                info = "Текст на иностранном";
                acts.Add(AiAct("Перевести", "Переведи на русский язык. Сохрани смысл и формат. Выведи только перевод.", s));
                if (s.Length > 500) acts.Add(AiAct("Кратко по-русски", "Перескажи этот текст по-русски в 3–5 пунктах. Только суть.", s));
            }
            else if (cyr >= 15)
            {
                acts.Add(AiAct("Исправить ошибки", "Исправь орфографию, пунктуацию и опечатки. Стиль и смысл не меняй. Выведи только исправленный текст.", s));
                acts.Add(AiAct("Короче", "Сократи текст, сохранив смысл и тон. Выведи только результат.", s));
                acts.Add(AiAct("Вежливее", "Перепиши вежливо и дружелюбно, деловым тоном. Выведи только результат.", s));
                if (s.Length > 600) acts.Add(AiAct("Пересказать", "Перескажи суть в 3–5 пунктах.", s));
            }
        }
        if (acts.Count == 0) return;
        clipLastOffer = t; clipOfferAt = DateTime.Now;
        string lab = ClipLabel(s).Replace("&&", "&"); if (lab.Length > 28) lab = lab.Substring(0, 27) + "…";
        ClipPopShow(info ?? ("«" + lab + "»"), acts);
    }

    double RateNum(char cur)
    {
        if (rateLine == null) return 0;
        Match m = Regex.Match(rateLine, Regex.Escape(cur.ToString()) + @"\s*([0-9]+,[0-9]+)");
        double v; return m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
    }

    void ClipPut(string text) { try { clipSelf = true; Clipboard.SetText(text); } catch { clipSelf = false; } }

    KeyValuePair<string, Action> AiAct(string title, string task, string text)
    {
        return new KeyValuePair<string, Action>(title, delegate { ClipAi(title, task, text); });
    }

    void ClipPopBuild()
    {
        if (clipPop != null && !clipPop.IsDisposed) return;
        clipPop = new NoActForm(); clipPop.FormBorderStyle = FormBorderStyle.None; clipPop.ShowInTaskbar = false; clipPop.TopMost = true;
        clipPop.BackColor = SBg; clipPop.StartPosition = FormStartPosition.Manual; clipPop.Padding = new Padding(12, 10, 10, 6);
        RoundForm(clipPop);
        clipPop.Padding = new Padding(8, 6, 6, 4);
        clipHead = new Label(); clipHead.AutoSize = true; clipHead.MaximumSize = new Size(300, 0); clipHead.ForeColor = SDim; clipHead.Font = new Font("Segoe UI", 8.25f);
        clipHead.Location = new Point(8, 6);
        clipBtns = new FlowLayoutPanel(); clipBtns.AutoSize = true; clipBtns.AutoSizeMode = AutoSizeMode.GrowAndShrink; clipBtns.MaximumSize = new Size(460, 0); clipBtns.BackColor = SBg; clipBtns.WrapContents = true;
        clipOut = new TextBox(); clipOut.Multiline = true; clipOut.ReadOnly = true; clipOut.BorderStyle = BorderStyle.None; clipOut.BackColor = SBg2; clipOut.ForeColor = STx;
        clipOut.Font = new Font("Segoe UI", 9f); clipOut.ScrollBars = ScrollBars.Vertical; clipOut.Visible = false; clipOut.Width = 360;
        Label x = new Label(); x.Text = "✕"; x.ForeColor = SDim; x.AutoSize = true; x.Cursor = Cursors.Hand; x.Font = new Font("Segoe UI", 9f); x.Name = "x";
        x.Click += delegate { ClipPopHide(); };
        clipPop.Controls.Add(clipHead); clipPop.Controls.Add(clipBtns); clipPop.Controls.Add(clipOut); clipPop.Controls.Add(x);
        clipPopT = new System.Windows.Forms.Timer(); clipPopT.Interval = 250;
        clipPopT.Tick += delegate
        {
            if (clipPop == null || !clipPop.Visible) { clipPopT.Stop(); return; }
            if (clipPop.Bounds.Contains(Cursor.Position) || smartBusy) { if (clipPopLeft < 16) clipPopLeft = 16; return; }
            if (--clipPopLeft <= 0) ClipPopHide();
        };
    }

    void ClipLayout()
    {
        // размеры считаем явно: до показа окна AutoSize ещё не посчитан
        clipHead.Size = clipHead.GetPreferredSize(new Size(300, 0));
        clipBtns.MaximumSize = new Size(clipOut.Visible ? 370 : 300, 0);
        clipBtns.Size = clipBtns.GetPreferredSize(new Size(clipOut.Visible ? 370 : 300, 0));
        int y = clipHead.Bottom + 5;
        if (clipOut.Visible)
        {
            // высота по числу строк (с переносами), как считает сам TextBox
            Size ms = TextRenderer.MeasureText(clipOut.Text, clipOut.Font, new Size(clipOut.Width - 22, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int lines = 1; foreach (char ch in clipOut.Text) if (ch == '\n') lines++;
            int hh = Math.Max(ms.Height, lines * clipOut.Font.Height) + 8;
            clipOut.Height = Math.Min(260, Math.Max(30, hh));
            clipOut.Location = new Point(8, y); y = clipOut.Bottom + 6;
        }
        clipBtns.Location = new Point(8, y);
        int w = Math.Max(Math.Max(clipHead.Right, clipBtns.Right), clipOut.Visible ? clipOut.Right : 0) + 24;
        w = Math.Max(w, 150);
        int h = clipBtns.Bottom + 2;
        Control x = clipPop.Controls["x"]; x.Location = new Point(w - 18, 3);
        // ставим слева от погоды и панели, чтобы ничего не перекрывать
        Rectangle wa = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2)).WorkingArea;
        int right = Left + Width - (int)W(90) - 10;
        int top = Top + (int)(WxVisible ? WidgetRect().Top : W(2));
        int left = right - w;
        if (left < wa.Left + 8) { left = wa.Left + 8; }
        top = Math.Max(wa.Top + 8, Math.Min(wa.Bottom - h - 8, top));
        clipPop.Bounds = new Rectangle(left, top, w, h);
        clipPop.Invalidate();
    }

    void ClipToTop() { try { if (clipPop != null && clipPop.Visible) SetWindowPos(clipPop.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); } catch { } }

    void ClipPopShow(string head, List<KeyValuePair<string, Action>> acts)
    {
        ClipPopBuild();
        clipHead.Text = head; clipOut.Visible = false;
        clipBtns.Controls.Clear();
        foreach (KeyValuePair<string, Action> a in acts)
        {
            Button b = SButtonS(a.Key, clipBtns.Controls.Count == 0); Action act = a.Value;
            b.Click += delegate { try { act(); } catch (Exception ex) { GuardLog("буфер: действие", ex); } };
            clipBtns.Controls.Add(b);
        }
        ClipLayout();
        if (!clipPop.Visible) clipPop.Show();
        ClipLayout(); ClipToTop();
        clipPopLeft = 4 * 9; clipPopT.Start();
        me.HappyT = 20;
    }

    void ClipPopHide() { try { if (clipPop != null && !clipPop.IsDisposed) clipPop.Hide(); } catch { } }

    void ClipAi(string title, string task, string text)
    {
        if (smartBusy) return; smartBusy = true;
        clipOrig = text;
        clipHead.Text = title + " — котик думает…"; clipBtns.Controls.Clear(); clipOut.Visible = false; ClipLayout();
        Say("Хм… " + title.ToLowerInvariant(), me.X, CatTop(me) - U(1), 'U');
        string sys = "Ты редактор и переводчик. Отвечай только результатом, без вступлений, кавычек и пояснений.";
        ThreadPool.QueueUserWorkItem(delegate
        {
            string ans = null, err = null;
            try { ans = CallAIRaw(sys, task + "\n\nТекст:\n" + text, null, 1500); } catch (Exception ex) { err = ex.Message; }
            try
            {
                BeginInvoke((Action)delegate
                {
                    smartBusy = false;
                    if (clipPop == null || clipPop.IsDisposed) return;
                    clipBtns.Controls.Clear();
                    if (err != null)
                    {
                        clipHead.Text = "Не получилось: " + err; clipOut.Visible = false;
                        Button r = SButtonS("Ещё раз", true); r.Click += delegate { ClipAi(title, task, text); }; clipBtns.Controls.Add(r);
                    }
                    else
                    {
                        clipResult = ans; ClipPut(ans);
                        clipHead.Text = title + " — готово, уже в буфере (Ctrl+V)";
                        clipOut.Text = ans.Replace("\r\n", "\n").Replace("\n", "\r\n"); clipOut.Visible = true; clipOut.Select(0, 0);
                        Button back = SButtonS("Вернуть оригинал", false); back.Click += delegate { ClipPut(clipOrig); clipHead.Text = "Вернул оригинал в буфер"; ClipLayout(); };
                        Button ok = SButtonS("Готово", true); ok.Click += delegate { ClipPopHide(); };
                        clipBtns.Controls.Add(ok); clipBtns.Controls.Add(back);
                        me.HappyT = 40; Say("Готово! Вставляй", me.X, CatTop(me) - U(1), 'G');
                    }
                    ClipLayout(); clipPopLeft = 4 * 25; clipPopT.Start();
                    if (!clipPop.Visible) clipPop.Show();
                    ClipToTop();
                });
            }
            catch { smartBusy = false; }
        });
    }

    // ================= 2. котик видит экран (Ctrl+Shift+K) =================
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    bool hotkeyOk;

    void HotkeyInit()
    {
        try { hotkeyOk = RegisterHotKey(Handle, 0x4B1D, 0x2 | 0x4 | 0x4000, 0x4B); } catch { hotkeyOk = false; }   // Ctrl+Shift+K, без автоповтора
        try { RegisterHotKey(Handle, 0x4B1E, 0x2 | 0x4 | 0x4000, 0x4D); } catch { }   // Ctrl+Shift+M — меню котика где угодно
        if (!hotkeyOk) GuardLog("Ctrl+Shift+K занято другой программой", null);
    }

    class SnipForm : Form
    {
        public Bitmap Shot; public Rectangle Virt; Point a; Rectangle sel; bool drag; public Rectangle Result = Rectangle.Empty; public bool Done;
        public SnipForm(Bitmap shot, Rectangle virt)
        {
            Shot = shot; Virt = virt; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            Bounds = virt; DoubleBuffered = true; Cursor = Cursors.Cross; KeyPreview = true;
            KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { Done = true; Close(); } };
            MouseDown += delegate (object o, MouseEventArgs e) { if (e.Button == MouseButtons.Right) { Done = true; Close(); return; } a = e.Location; drag = true; sel = Rectangle.Empty; };
            MouseMove += delegate (object o, MouseEventArgs e) { if (!drag) return; sel = Rectangle.FromLTRB(Math.Min(a.X, e.X), Math.Min(a.Y, e.Y), Math.Max(a.X, e.X), Math.Max(a.Y, e.Y)); Invalidate(); };
            MouseUp += delegate (object o, MouseEventArgs e)
            {
                if (!drag || e.Button != MouseButtons.Left) return; drag = false;
                if (sel.Width < 12 || sel.Height < 12)
                {
                    Screen sc = Screen.FromPoint(new Point(e.X + Virt.Left, e.Y + Virt.Top));
                    Result = new Rectangle(sc.Bounds.Left - Virt.Left, sc.Bounds.Top - Virt.Top, sc.Bounds.Width, sc.Bounds.Height);
                }
                else Result = sel;
                Done = true; Close();
            };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImageUnscaled(Shot, 0, 0);
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                if (sel.Width > 0) { using (Region r = new Region(ClientRectangle)) { r.Exclude(sel); g.FillRegion(dim, r); } using (Pen p = new Pen(Color.FromArgb(255, 158, 64), 2)) g.DrawRectangle(p, sel); }
                else g.FillRectangle(dim, ClientRectangle);
            }
            Screen sc = Screen.FromPoint(Cursor.Position);
            string hint = "Выдели область мышкой  ·  просто клик — весь экран  ·  Esc — отмена";
            using (Font f = new Font("Segoe UI Semibold", 13f))
            {
                SizeF sz = g.MeasureString(hint, f);
                float x = sc.Bounds.Left - Virt.Left + (sc.Bounds.Width - sz.Width) / 2, y = sc.Bounds.Top - Virt.Top + 24;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 28, 30, 40))) g.FillRectangle(b, x - 14, y - 8, sz.Width + 28, sz.Height + 16);
                g.DrawString(hint, f, Brushes.White, x, y);
            }
        }
    }

    Form scrForm; PictureBox scrPic; TextBox scrAsk, scrOut; FlowLayoutPanel scrBtns; Label scrHead; string scrImg; Bitmap scrThumb;

    public void ScreenHelper()
    {
        if (smartBusy) { Say("Погоди, я ещё думаю…", me.X, CatTop(me) - U(1), 'U'); return; }
        try { if (scrForm != null && !scrForm.IsDisposed) scrForm.Hide(); ClipPopHide(); } catch { }
        Rectangle virt = SystemInformation.VirtualScreen;
        Bitmap shot;
        try { shot = new Bitmap(virt.Width, virt.Height, PixelFormat.Format24bppRgb); using (Graphics g = Graphics.FromImage(shot)) g.CopyFromScreen(virt.Left, virt.Top, 0, 0, virt.Size); }
        catch (Exception ex) { GuardLog("снимок экрана", ex); Say("Не смог сфотографировать экран", me.X, CatTop(me) - U(1), 'R'); return; }
        SnipForm sf = new SnipForm(shot, virt);
        sf.FormClosed += delegate
        {
            Rectangle r = sf.Result;
            if (r.IsEmpty) { shot.Dispose(); return; }
            try
            {
                r.Intersect(new Rectangle(0, 0, shot.Width, shot.Height));
                using (Bitmap crop = shot.Clone(r, PixelFormat.Format24bppRgb)) PrepareShot(crop);
            }
            catch (Exception ex) { GuardLog("вырезка экрана", ex); }
            finally { shot.Dispose(); }
            ScreenAskShow();
        };
        sf.Show(); sf.Activate();
    }

    void PrepareShot(Bitmap crop)
    {
        float k = Math.Min(1f, 1600f / Math.Max(crop.Width, crop.Height));
        int w = Math.Max(1, (int)(crop.Width * k)), h = Math.Max(1, (int)(crop.Height * k));
        using (Bitmap small = new Bitmap(w, h, PixelFormat.Format24bppRgb))
        {
            using (Graphics g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(crop, 0, 0, w, h); }
            ImageCodecInfo jpg = null; foreach (ImageCodecInfo ci in ImageCodecInfo.GetImageEncoders()) if (ci.MimeType == "image/jpeg") jpg = ci;
            using (EncoderParameters ep = new EncoderParameters(1))
            using (MemoryStream ms = new MemoryStream())
            {
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 82L);
                small.Save(ms, jpg, ep); scrImg = Convert.ToBase64String(ms.ToArray());
            }
            float t = Math.Min(200f / w, 130f / h);
            if (scrThumb != null) scrThumb.Dispose();
            scrThumb = new Bitmap(small, Math.Max(1, (int)(w * t)), Math.Max(1, (int)(h * t)));
        }
    }

    void ScreenAskShow()
    {
        if (scrImg == null) return;
        if (scrForm == null || scrForm.IsDisposed)
        {
            scrForm = new Form(); scrForm.FormBorderStyle = FormBorderStyle.None; scrForm.ShowInTaskbar = false; scrForm.TopMost = true; scrForm.KeyPreview = true;
            scrForm.BackColor = SBg; scrForm.StartPosition = FormStartPosition.Manual; scrForm.Size = new Size(560, 260); RoundForm(scrForm);
            scrForm.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) scrForm.Hide(); };
            Point dragAt = Point.Empty; bool dragging2 = false;
            MouseEventHandler md = delegate (object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging2 = true; dragAt = e.Location; } };
            MouseEventHandler mv = delegate (object o, MouseEventArgs e) { if (dragging2) scrForm.Location = new Point(scrForm.Left + e.X - dragAt.X, scrForm.Top + e.Y - dragAt.Y); };
            MouseEventHandler mu = delegate { dragging2 = false; };
            scrForm.MouseDown += md; scrForm.MouseMove += mv; scrForm.MouseUp += mu;
            scrPic = new PictureBox(); scrPic.SizeMode = PictureBoxSizeMode.CenterImage; scrPic.Location = new Point(12, 38); scrPic.Size = new Size(200, 130); scrPic.BackColor = SBg2;
            scrHead = new Label(); scrHead.Text = "Котик смотрит на экран"; scrHead.ForeColor = SAcc; scrHead.Font = new Font("Segoe UI Semibold", 10.5f); scrHead.AutoSize = true; scrHead.Location = new Point(12, 10);
            scrHead.MouseDown += md; scrHead.MouseMove += mv; scrHead.MouseUp += mu;
            Label x = new Label(); x.Text = "✕"; x.ForeColor = SDim; x.AutoSize = true; x.Cursor = Cursors.Hand; x.Font = new Font("Segoe UI", 10f); x.Location = new Point(534, 8); x.Click += delegate { scrForm.Hide(); };
            scrBtns = new FlowLayoutPanel(); scrBtns.Location = new Point(224, 38); scrBtns.Size = new Size(326, 96); scrBtns.BackColor = SBg;
            string[][] quick = {
                new string[] { "Что тут?", "Кратко объясни, что изображено на скриншоте и что тут важно." },
                new string[] { "Переведи", "Переведи весь текст со скриншота на русский. Только перевод." },
                new string[] { "Перескажи", "Перескажи суть текста со скриншота в 3–5 пунктах." },
                new string[] { "Найди ошибки", "Найди ошибки на скриншоте (опечатки, ошибки в цифрах, формулах, коде, логике) и коротко перечисли, как исправить." },
                new string[] { "Как решить?", "На скриншоте проблема или ошибка. Объясни простыми словами, в чём дело, и дай пошаговое решение." },
                new string[] { "Текст", "Перепиши весь текст со скриншота как есть, без пояснений." } };
            foreach (string[] q in quick) { string[] qq = q; Button b = SButton(qq[0], false); b.Click += delegate { ScreenAsk(qq[0], qq[1]); }; scrBtns.Controls.Add(b); }
            scrAsk = new TextBox(); scrAsk.Location = new Point(224, 142); scrAsk.Width = 326; scrAsk.BorderStyle = BorderStyle.FixedSingle; scrAsk.BackColor = SBg2; scrAsk.ForeColor = STx; scrAsk.Font = new Font("Segoe UI", 10.5f);
            scrAsk.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && scrAsk.Text.Trim().Length > 0) { e.SuppressKeyPress = true; ScreenAsk(scrAsk.Text.Trim(), scrAsk.Text.Trim()); } };
            Label hint = new Label(); hint.Text = "свой вопрос — Enter"; hint.ForeColor = SDim; hint.Font = new Font("Segoe UI", 8.5f); hint.AutoSize = true; hint.Location = new Point(224, 170);
            scrOut = new TextBox(); scrOut.Multiline = true; scrOut.ReadOnly = true; scrOut.BorderStyle = BorderStyle.None; scrOut.BackColor = SBg2; scrOut.ForeColor = STx; scrOut.Font = new Font("Segoe UI", 10.5f);
            scrOut.ScrollBars = ScrollBars.Vertical; scrOut.Location = new Point(12, 196); scrOut.Size = new Size(536, 10); scrOut.Visible = false;
            Button copy = SButton("Копировать ответ", true); copy.Name = "copy"; copy.Visible = false; copy.Click += delegate { if (scrOut.Text.Length > 0) { ClipPut(scrOut.Text); Say("Ответ в буфере", me.X, CatTop(me) - U(1), 'G'); } };
            Button again = SButton("Новый снимок", false); again.Name = "again"; again.Click += delegate { scrForm.Hide(); BeginInvoke((Action)delegate { ScreenHelper(); }); };
            scrForm.Controls.AddRange(new Control[] { scrHead, x, scrPic, scrBtns, scrAsk, hint, scrOut, copy, again });
            scrForm.Deactivate += delegate { };
        }
        scrPic.Image = scrThumb; scrOut.Visible = false; scrOut.Text = ""; scrAsk.Text = ""; scrHead.Text = "Котик смотрит на экран — что сделать?";
        ScrLayout();
        Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        scrForm.Location = new Point(wa.Left + (wa.Width - scrForm.Width) / 2, wa.Top + (wa.Height - scrForm.Height) / 3);
        scrForm.Show(); scrForm.Activate(); scrAsk.Focus();
        me.HappyT = 30; Say("Смотрю!", me.X, CatTop(me) - U(1), 'U');
    }

    void ScrLayout()
    {
        int y = 196;
        if (scrOut.Visible)
        {
            Size ms = TextRenderer.MeasureText(scrOut.Text, scrOut.Font, new Size(scrOut.Width - 24, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int lines = 1; foreach (char ch in scrOut.Text) if (ch == '\n') lines++;
            scrOut.Height = Math.Min(380, Math.Max(40, Math.Max(ms.Height, lines * scrOut.Font.Height) + 10));
            y = scrOut.Bottom + 10;
        }
        Control copy = scrForm.Controls["copy"], again = scrForm.Controls["again"];
        copy.Visible = scrOut.Visible && scrOut.Text.Length > 0 && !smartBusy;
        copy.Location = new Point(12, y); again.Location = new Point(copy.Visible ? copy.Right + 6 : 12, y);
        scrForm.Height = again.Bottom + 10;
        Rectangle wa = Screen.FromControl(scrForm).WorkingArea;
        if (scrForm.Bottom > wa.Bottom - 8) scrForm.Top = Math.Max(wa.Top + 8, wa.Bottom - 8 - scrForm.Height);
        scrForm.Invalidate();
    }

    void ScreenAsk(string title, string q)
    {
        if (smartBusy || scrImg == null) return; smartBusy = true;
        scrHead.Text = title + " — котик думает…"; scrOut.Visible = true; scrOut.Text = "…"; ScrLayout();
        string img = scrImg;
        string sys = "Ты внимательный помощник. Тебе прислали скриншот экрана Windows. Отвечай по-русски, понятно и по делу, без таблиц и markdown. Если на скриншоте мало видно — так и скажи.";
        ThreadPool.QueueUserWorkItem(delegate
        {
            string ans = null, err = null;
            try { ans = CallAIRaw(sys, q, img, 1500); } catch (Exception ex) { err = ex.Message; }
            try
            {
                BeginInvoke((Action)delegate
                {
                    smartBusy = false;
                    if (scrForm == null || scrForm.IsDisposed) return;
                    scrHead.Text = err != null ? "Не получилось" : title;
                    scrOut.Text = (err != null ? "Мяу, не получилось: " + err : ans).Replace("\r\n", "\n").Replace("\n", "\r\n");
                    scrOut.Select(0, 0); ScrLayout();
                    if (err == null) { me.HappyT = 40; Speak(ans.Length > 300 ? ans.Substring(0, 300) : ans); }
                });
            }
            catch { smartBusy = false; }
        });
    }
    // ================= быстрый поиск остатка в МойСклад =================
    string msToken = "";   // свой токен из настроек; пусто — берём из коннектора МойСклад в Claude
    volatile bool stockBusy;

    string MsToken()
    {
        if (msToken.Length > 0) return msToken;
        try
        {
            // Claude из Microsoft Store хранит настройки в своей «песочнице» Packages\Claude_*\LocalCache
            List<string> cands = new List<string>();
            cands.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Claude\claude_desktop_config.json"));
            try
            {
                string pk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
                if (Directory.Exists(pk)) foreach (string dir in Directory.GetDirectories(pk, "Claude*")) cands.Add(Path.Combine(dir, @"LocalCache\Roaming\Claude\claude_desktop_config.json"));
            }
            catch { }
            foreach (string cfg in cands)
            {
                try { if (File.Exists(cfg)) { Match m = Regex.Match(File.ReadAllText(cfg), "\"MOYSKLAD_TOKEN\"\\s*:\\s*\"([^\"]+)\""); if (m.Success) return m.Groups[1].Value; } } catch { }
            }
        }
        catch { }
        return "";
    }

    static string MsGet(string token, string path)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        HttpWebRequest rq = (HttpWebRequest)WebRequest.Create("https://api.moysklad.ru/api/remap/1.2/" + path);
        rq.Headers[HttpRequestHeader.Authorization] = "Bearer " + token; rq.Headers[HttpRequestHeader.AcceptEncoding] = "gzip";
        rq.AutomaticDecompression = DecompressionMethods.GZip; rq.Timeout = 30000; rq.ReadWriteTimeout = 30000; rq.UserAgent = "VapePet/1.0";
        try { using (WebResponse rs = rq.GetResponse()) using (StreamReader sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); }
        catch (WebException we)
        {
            HttpWebResponse r = we.Response as HttpWebResponse;
            if (r != null && (int)r.StatusCode == 401) throw new Exception("МойСклад не пустил: проверь токен");
            if (r == null) throw new Exception("нет связи с МойСклад");
            throw new Exception("МойСклад ответил ошибкой " + (int)r.StatusCode);
        }
    }

    // кусок JSON-массива "rows":[ {...}, {...} ] → отдельные объекты верхнего уровня
    static List<string> JRows(string json, string key)
    {
        List<string> res = new List<string>();
        int i = json.IndexOf("\"" + key + "\""); if (i < 0) return res;
        i = json.IndexOf('[', i); if (i < 0) return res;
        int depth = 0, start = -1; bool str = false;
        for (int k = i + 1; k < json.Length; k++)
        {
            char c = json[k];
            if (str) { if (c == '\\') k++; else if (c == '"') str = false; continue; }
            if (c == '"') { str = true; continue; }
            if (c == '{') { if (depth == 0) start = k; depth++; }
            else if (c == '}') { depth--; if (depth == 0 && start >= 0) { res.Add(json.Substring(start, k - start + 1)); start = -1; } }
            else if (c == ']' && depth == 0) break;
        }
        return res;
    }
    static string JStr(string obj, string key) { Match m = Regex.Match(obj, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); return m.Success ? Regex.Unescape(m.Groups[1].Value) : null; }
    static double JD(string obj, string key) { Match m = Regex.Match(obj, "\"" + key + "\"\\s*:\\s*(-?[0-9.]+)"); double v; return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0; }
    static string MsId(string obj) { Match m = Regex.Match(obj, "/entity/(?:product|variant|bundle|service)/([0-9a-f-]{36})"); return m.Success ? m.Groups[1].Value : null; }

    void MsTokenDialog()
    {
        using (Form f = new Form())
        {
            f.Text = "Токен МойСклад"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterScreen; f.TopMost = true;
            f.ClientSize = new Size(460, 150); f.MaximizeBox = false; f.MinimizeBox = false; f.BackColor = SBg; f.ForeColor = STx; f.Font = new Font("Segoe UI", 9.5f);
            Label l = new Label(); l.SetBounds(12, 10, 436, 54); l.ForeColor = SDim;
            l.Text = "Нужен только для поиска остатков. Если оставить пустым, котик возьмёт токен из коннектора МойСклад в Claude" + (MsToken().Length > 0 && msToken.Length == 0 ? " (он найден ✓)." : ".");
            TextBox t = new TextBox(); t.SetBounds(12, 70, 436, 26); t.UseSystemPasswordChar = true; t.Text = msToken; t.BackColor = SBg2; t.ForeColor = STx; t.BorderStyle = BorderStyle.FixedSingle;
            Button ok = SButton("Сохранить", true); ok.Location = new Point(12, 108); ok.DialogResult = DialogResult.OK;
            f.Controls.AddRange(new Control[] { l, t, ok }); f.AcceptButton = ok;
            if (f.ShowDialog() == DialogResult.OK) { msToken = t.Text.Trim(); SaveSettings(); Say("Токен сохранил", me.X, CatTop(me) - U(1), 'G'); }
        }
    }

    // слова запроса → «основы» (первые 5 букв), ё=е, без мусора
    static List<string> QWords(string q)
    {
        List<string> r = new List<string>();
        foreach (string w0 in Regex.Split(q.ToLowerInvariant().Replace('ё', 'е'), @"[\s\-–—/,()«»""']+"))
        {
            string w = w0.Trim('.'); if (w.Length == 0) continue;
            if (Regex.IsMatch(w, @"^\d")) { r.Add(w); continue; }
            r.Add(w.Length > 5 ? w.Substring(0, w.Length > 7 ? 5 : w.Length - 1) : w);
        }
        return r;
    }
    static int Score(string name, List<string> ws)
    {
        string n = name.ToLowerInvariant().Replace('ё', 'е'); int sc = 0;
        foreach (string w in ws) if (n.Contains(w)) sc++;
        return sc;
    }
    // из набора строк оставить только лучшие по совпадению слов
    static List<string> BestRows(IEnumerable<string> rows, List<string> ws, int minScore, out int best)
    {
        best = 0; List<string> res = new List<string>(); HashSet<string> seen = new HashSet<string>();
        List<KeyValuePair<int, string>> sc = new List<KeyValuePair<int, string>>();
        foreach (string row in rows) { string id = MsId(row) ?? JStr(row, "name"); if (id == null || !seen.Add(id)) continue; int s = Score(JStr(row, "name") ?? "", ws); if (s > best) best = s; sc.Add(new KeyValuePair<int, string>(s, row)); }
        if (best < minScore) return res;
        foreach (KeyValuePair<int, string> kv in sc) if (kv.Key == best) res.Add(kv.Value);
        return res;
    }
    static string WrapRows(List<string> rows)
    {
        StringBuilder b = new StringBuilder("{\"rows\":[");
        for (int i = 0; i < rows.Count; i++) { if (i > 0) b.Append(","); b.Append(rows[i]); }
        return b.Append("],\"meta\":{\"size\":").Append(rows.Count).Append("}}").ToString();
    }

    public void StockSearchLive(string q)
    {
        q = q.Trim(); if (q.Length < 2) return;
        string token = MsToken();
        if (token.Length == 0) { ShowInfoPop("Остатки", "Нет токена МойСклад. Меню → Настройки → «Токен МойСклад».", null); return; }
        if (stockBusy) return; stockBusy = true;
        ShowInfoPop("Ищу «" + q + "» в МойСклад…", null, null);
        Say("Ищу на складах…", me.X, CatTop(me) - U(1), 'U');
        ThreadPool.QueueUserWorkItem(delegate
        {
            string text = null, err = null, link = null;
            try
            {
                string qs = Uri.EscapeDataString(q);
                string all, by = null, note = null, bySearch = qs;
                List<string> zeroRows = new List<string>();      // точные карточки без остатка
                List<string> qw = QWords(q);

                // 1) точное совпадение в остатках
                all = MsGet(token, "report/stock/all?limit=12&filter=search=" + qs);
                if (JRows(all, "rows").Count == 0)
                {
                    // 2) точная карточка есть, но остатка нет
                    try { foreach (string r in JRows(MsGet(token, "entity/product?limit=6&search=" + qs), "rows")) zeroRows.Add(r); } catch { }
                    // 3) по словам в любом порядке: лучший результат по числу совпавших слов
                    if (zeroRows.Count == 0 && qw.Count > 1)
                    {
                        List<string> pool = new List<string>();
                        List<string> keys = new List<string>(qw); keys.Sort(delegate (string x, string y) { return y.Length.CompareTo(x.Length); });
                        int used = 0;
                        foreach (string k in keys) { if (k.Length < 3) continue; try { pool.AddRange(JRows(MsGet(token, "report/stock/all?limit=300&filter=search=" + Uri.EscapeDataString(k)), "rows")); } catch { } if (++used >= 2) break; }
                        int best; List<string> top = BestRows(pool, qw, Math.Max(2, qw.Count - 1), out best);
                        if (top.Count > 0) { all = WrapRows(top); if (best < qw.Count) note = "точного нет, ближайшее"; bySearch = Uri.EscapeDataString(keys[0]); }
                        else
                        {
                            List<string> ppool = new List<string>();
                            foreach (string k in keys) { if (k.Length < 3) continue; try { ppool.AddRange(JRows(MsGet(token, "entity/product?limit=100&search=" + Uri.EscapeDataString(k)), "rows")); } catch { } break; }
                            int pb; List<string> ptop = BestRows(ppool, qw, qw.Count, out pb);
                            zeroRows.AddRange(ptop);
                        }
                    }
                    // 4) по смыслу (ИИ), только если ничего похожего не нашлось
                    if (JRows(all, "rows").Count == 0 && zeroRows.Count == 0 && aiKey.Length > 0)
                    {
                        List<string> aiVars = new List<string>();
                        try
                        {
                            string sys = "Ты помогаешь искать товары в каталоге вейп-шопа (МойСклад). Бренды в каталоге обычно латиницей (Elf Bar, Lost Mary, HQD, PAFOS, Husky, Vaporesso XROS, Geek Vape, WAKA SoPRO, Puffmi, Brusko, Duall, Catswill), вкусы — по-русски или по-английски, крепость 20mg/50мг/2%, объём 30ml, затяжки 20000. Исправляй опечатки и транслит (эльф бар → elf bar, хаски → husky, пафос → pafos, иксрос → xros, вака → waka).";
                            string ask = "Запрос продавца: «" + q + "».\nДай до 4 вариантов поискового запроса (2–4 слова: бренд + модель/затяжки + вкус), от самого точного к общему. Каждый с новой строки, без нумерации и пояснений.";
                            foreach (string line in CallAIRaw(sys, ask, null, 300).Split('\n'))
                            {
                                string v = Regex.Replace(line, @"^[\s\-–•*\d.)]+(?=\D)", "").Trim().Trim('"', '«', '»');
                                if (v.Length >= 2 && v.Length <= 50 && !aiVars.Contains(v.ToLowerInvariant())) aiVars.Add(v.ToLowerInvariant());
                                if (aiVars.Count >= 4) break;
                            }
                        }
                        catch { }
                        foreach (string v in aiVars)
                        {
                            List<string> vw = QWords(v); if (vw.Count == 0) continue;
                            List<string> keys = new List<string>(vw); keys.Sort(delegate (string x, string y) { return y.Length.CompareTo(x.Length); });
                            List<string> pool = new List<string>();
                            try { pool.AddRange(JRows(MsGet(token, "report/stock/all?limit=300&filter=search=" + Uri.EscapeDataString(keys[0])), "rows")); } catch { continue; }
                            // считаем совпадения и со словами исходного запроса — так вкус не потеряется
                            List<string> both = new List<string>(vw); foreach (string w in qw) if (!both.Contains(w)) both.Add(w);
                            int best; List<string> top = BestRows(pool, both, Math.Max(1, vw.Count), out best);
                            if (top.Count > 0) { all = WrapRows(top); note = "по смыслу: «" + v + "»"; bySearch = Uri.EscapeDataString(keys[0]); break; }
                        }
                        if (JRows(all, "rows").Count == 0 && aiVars.Count > 0) note = "пробовал: " + string.Join(", ", aiVars.ToArray());
                    }
                }
                if (JRows(all, "rows").Count > 0) { try { by = MsGet(token, "report/stock/bystore?limit=100&filter=search=" + bySearch); } catch { } }

                Dictionary<string, List<KeyValuePair<string, double>>> stores = new Dictionary<string, List<KeyValuePair<string, double>>>();
                if (by != null)
                    foreach (string row in JRows(by, "rows"))
                    {
                        string id = MsId(row); if (id == null) continue;
                        List<KeyValuePair<string, double>> l = new List<KeyValuePair<string, double>>();
                        foreach (string st in JRows(row, "stockByStore"))
                        {
                            string sn = JStr(st, "name") ?? "?"; double n = JD(st, "stock");
                            if (n <= 0 || sn.StartsWith("Я ")) continue;
                            l.Add(new KeyValuePair<string, double>(sn.Replace("Склад ", ""), n));
                        }
                        stores[id] = l;
                    }
                StringBuilder sb = new StringBuilder(); int shown = 0;
                foreach (string row in zeroRows)
                {
                    string nm = JStr(row, "name") ?? "?";
                    Match pv = Regex.Match(row, "\"salePrices\"\\s*:\\s*\\[\\s*\\{\\s*\"value\"\\s*:\\s*([0-9.]+)");
                    double price = 0; if (pv.Success) double.TryParse(pv.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out price);
                    sb.Append(nm).Append("\r\n   нет в наличии");
                    if (price > 0) sb.Append(" · ").Append((price / 100).ToString("#,0", new CultureInfo("ru-RU"))).Append(" ₽");
                    sb.Append("\r\n"); if (++shown >= 10) break;
                }
                foreach (string row in JRows(all, "rows"))
                {
                    if (shown >= 10) break;
                    string name = JStr(row, "name") ?? "?"; double stock = JD(row, "stock"), price = JD(row, "salePrice") / 100.0;
                    string id = MsId(row);
                    sb.Append(name).Append("\r\n   ");
                    sb.Append(stock > 0 ? "всего " + stock.ToString("0.##", CultureInfo.InvariantCulture) + " шт" : "нет в наличии");
                    if (price > 0) sb.Append(" · ").Append(price.ToString("#,0", new CultureInfo("ru-RU"))).Append(" ₽");
                    List<KeyValuePair<string, double>> l;
                    if (id != null && stores.TryGetValue(id, out l) && l.Count > 0)
                    {
                        sb.Append("\r\n   ");
                        for (int k = 0; k < l.Count; k++) sb.Append(k > 0 ? ", " : "").Append(l[k].Key).Append(": ").Append(l[k].Value.ToString("0.##", CultureInfo.InvariantCulture));
                    }
                    sb.Append("\r\n"); shown++;
                }
                int total = zeroRows.Count + JRows(all, "rows").Count;
                if (shown == 0) text = "Ничего не нашёл по «" + q + "»" + (note != null ? "\r\n" + note : "");
                else
                {
                    text = sb.ToString().TrimEnd() + (total > shown ? "\r\n… и ещё " + (total - shown) + " — уточни название" : "");
                    if (note != null) text = "(" + note + ")\r\n" + text;
                }
                link = "https://online.moysklad.ru/app/#stockReport?global_searchFilter=" + qs;
            }
            catch (Exception ex) { err = ex.Message; }
            try
            {
                BeginInvoke((Action)delegate
                {
                    stockBusy = false;
                    if (err != null) { ShowInfoPop("Остатки: не получилось", err, null); Say("Склад не отвечает…", me.X, CatTop(me) - U(1), 'R'); }
                    else { stockShownQ = q; ShowInfoPop("Остатки: «" + q + "»", text, link); me.HappyT = 30; }
                });
            }
            catch { stockBusy = false; }
        });
    }

    // ================= копия остатков у котика: мгновенный поиск =================
    class StockItem { public string Id, Name, Norm, Flat; public HashSet<string> Skel; public double Price, Stock; public List<KeyValuePair<string, double>> Stores = new List<KeyValuePair<string, double>>(); }
    volatile List<StockItem> stockCache; DateTime stockCacheAt = DateTime.MinValue; volatile bool stockCacheBusy; DateTime stockCacheTry = DateTime.MinValue; bool stockCacheLoaded;
    static string StockCachePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_stock.tsv"); } }

    static string NormName(string s) { return s.ToLowerInvariant().Replace('ё', 'е'); }
    // «скелет» латинского слова без гласных: haski ≈ husky (hsk)
    static string Skel(string w) { StringBuilder b = new StringBuilder(); foreach (char c in w) if (c >= 'a' && c <= 'z' && "aeiouy".IndexOf(c) < 0) { if (b.Length == 0 || b[b.Length - 1] != c) b.Append(c); } return b.ToString(); }
    static HashSet<string> SkelSet(string norm) { HashSet<string> h = new HashSet<string>(); foreach (string w in Regex.Split(Translit(norm), @"[^a-z]+")) { string k = Skel(w); if (k.Length >= 3) h.Add(k); } return h; }

    static string Translit(string s)
    {
        string[] map = { "а","a","б","b","в","v","г","g","д","d","е","e","ж","zh","з","z","и","i","й","y","к","k","л","l","м","m","н","n","о","o","п","p","р","r","с","s","т","t","у","u","ф","f","х","h","ц","c","ч","ch","ш","sh","щ","sh","ъ","","ы","y","ь","","э","e","ю","yu","я","ya" };
        StringBuilder b = new StringBuilder();
        foreach (char c in s) { string ch = c.ToString(), r = null; for (int i = 0; i < map.Length; i += 2) if (map[i] == ch) { r = map[i + 1]; break; } b.Append(r ?? ch); }
        return b.ToString();
    }

    void StockCacheTick()
    {
        if (stockCacheBusy) return;
        if (!stockCacheLoaded) { stockCacheLoaded = true; LoadStockCacheFile(); }
        bool due = (DateTime.Now - stockCacheAt).TotalMinutes >= 25 && (DateTime.Now - stockCacheTry).TotalMinutes >= 3 && tick > 30 * 40;
        if (!due) return;
        string token = MsToken(); if (token.Length == 0) return;
        stockCacheBusy = true; stockCacheTry = DateTime.Now;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                Dictionary<string, StockItem> d = new Dictionary<string, StockItem>();
                for (int off = 0; off < 30000; off += 1000)
                {
                    string js = MsGet(token, "report/stock/all?limit=1000&offset=" + off);
                    List<string> rows = JRows(js, "rows");
                    foreach (string row in rows)
                    {
                        string id = MsId(row); if (id == null) continue;
                        StockItem it = new StockItem(); it.Id = id; it.Name = JStr(row, "name") ?? "?"; it.Price = JD(row, "salePrice") / 100.0; it.Stock = JD(row, "stock");
                        d[id] = it;
                    }
                    if (rows.Count < 1000) break;
                }
                for (int off = 0; off < 30000; off += 1000)
                {
                    string js = MsGet(token, "report/stock/bystore?limit=1000&offset=" + off);
                    List<string> rows = JRows(js, "rows");
                    foreach (string row in rows)
                    {
                        string id = MsId(row); StockItem it; if (id == null || !d.TryGetValue(id, out it)) continue;
                        foreach (string st in JRows(row, "stockByStore"))
                        {
                            string sn = JStr(st, "name") ?? "?"; double n = JD(st, "stock");
                            if (n <= 0 || sn.StartsWith("Я ")) continue;
                            it.Stores.Add(new KeyValuePair<string, double>(sn.Replace("Склад ", ""), n));
                        }
                    }
                    if (rows.Count < 1000) break;
                }
                List<StockItem> list = new List<StockItem>(d.Values);
                foreach (StockItem it in list) { it.Norm = NormName(it.Name); it.Flat = it.Norm.Replace(" ", "").Replace("-", ""); it.Skel = SkelSet(it.Norm); }
                if (list.Count > 0)
                {
                    stockCache = list; stockCacheAt = DateTime.Now;
                    SaveStockCacheFile(list);
                }
            }
            catch (Exception ex) { try { BeginInvoke((Action)delegate { GuardLog("копия остатков", ex); }); } catch { } }
            stockCacheBusy = false;
        });
    }

    void SaveStockCacheFile(List<StockItem> list)
    {
        try
        {
            StringBuilder b = new StringBuilder();
            b.Append("#").Append(DateTime.Now.ToString("o")).Append("\n");
            foreach (StockItem it in list)
            {
                b.Append(it.Id).Append('\t').Append(it.Name.Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(it.Price.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(it.Stock.ToString(CultureInfo.InvariantCulture)).Append('\t');
                for (int i = 0; i < it.Stores.Count; i++) b.Append(i > 0 ? "|" : "").Append(it.Stores[i].Key.Replace('|', '/').Replace(':', ' ')).Append(':').Append(it.Stores[i].Value.ToString(CultureInfo.InvariantCulture));
                b.Append('\n');
            }
            File.WriteAllText(StockCachePath, b.ToString(), Encoding.UTF8);
        }
        catch { }
    }

    void LoadStockCacheFile()
    {
        try
        {
            if (!File.Exists(StockCachePath)) return;
            string[] lines = File.ReadAllLines(StockCachePath, Encoding.UTF8);
            List<StockItem> list = new List<StockItem>(); DateTime at = File.GetLastWriteTime(StockCachePath);
            foreach (string ln in lines)
            {
                if (ln.StartsWith("#")) { DateTime t; if (DateTime.TryParse(ln.Substring(1), null, DateTimeStyles.RoundtripKind, out t)) at = t; continue; }
                string[] p = ln.Split('\t'); if (p.Length < 5) continue;
                StockItem it = new StockItem(); it.Id = p[0]; it.Name = p[1];
                double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out it.Price); double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out it.Stock);
                foreach (string s in p[4].Split('|')) { int c = s.LastIndexOf(':'); double n; if (c > 0 && double.TryParse(s.Substring(c + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out n)) it.Stores.Add(new KeyValuePair<string, double>(s.Substring(0, c), n)); }
                it.Norm = NormName(it.Name); it.Flat = it.Norm.Replace(" ", "").Replace("-", ""); it.Skel = SkelSet(it.Norm);
                list.Add(it);
            }
            if (list.Count > 0) { stockCache = list; stockCacheAt = at; }
        }
        catch { }
    }

    // поиск по копии: лучшие по числу совпавших слов (с транслитом и без пробелов)
    List<StockItem> CacheFind(string q, out bool exact)
    {
        exact = false; List<StockItem> res = new List<StockItem>();
        List<StockItem> c = stockCache; if (c == null) return res;
        string nq = NormName(q).Trim();
        foreach (StockItem it in c) if (it.Norm.Contains(nq)) res.Add(it);
        if (res.Count > 0) { exact = true; return res; }
        List<string> ws = QWords(q); if (ws.Count == 0) return res;
        int best = 0; List<KeyValuePair<int, StockItem>> sc = new List<KeyValuePair<int, StockItem>>();
        List<string> tw = new List<string>(); foreach (string w in ws) tw.Add(Translit(w));
        List<string> sk = new List<string>(); foreach (string w in tw) { string k = Skel(w); sk.Add(k.Length >= 3 && !Regex.IsMatch(w, @"^\d") ? k : null); }
        foreach (StockItem it in c)
        {
            int s = 0;
            for (int i = 0; i < ws.Count; i++) if (it.Norm.Contains(ws[i]) || it.Flat.Contains(ws[i]) || (tw[i] != ws[i] && tw[i].Length >= 3 && (it.Norm.Contains(tw[i]) || it.Flat.Contains(tw[i]))) || (sk[i] != null && it.Skel != null && it.Skel.Contains(sk[i]))) s++;
            if (s == 0) continue;
            if (s > best) best = s; sc.Add(new KeyValuePair<int, StockItem>(s, it));
        }
        int need = ws.Count <= 2 ? ws.Count : Math.Max(2, ws.Count - 1);
        if (best < need) return res;
        exact = best == ws.Count;
        foreach (KeyValuePair<int, StockItem> kv in sc) if (kv.Key == best) res.Add(kv.Value);
        res.Sort(delegate (StockItem a, StockItem b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
        return res;
    }

    static string StockText(List<StockItem> items, int max)
    {
        StringBuilder sb = new StringBuilder(); int shown = 0;
        foreach (StockItem it in items)
        {
            sb.Append(it.Name).Append("\r\n   ");
            sb.Append(it.Stock > 0 ? "всего " + it.Stock.ToString("0.##", CultureInfo.InvariantCulture) + " шт" : "нет в наличии");
            if (it.Price > 0) sb.Append(" · ").Append(it.Price.ToString("#,0", new CultureInfo("ru-RU"))).Append(" ₽");
            if (it.Stores.Count > 0)
            {
                sb.Append("\r\n   ");
                for (int k = 0; k < it.Stores.Count; k++) sb.Append(k > 0 ? ", " : "").Append(it.Stores[k].Key).Append(": ").Append(it.Stores[k].Value.ToString("0.##", CultureInfo.InvariantCulture));
            }
            sb.Append("\r\n");
            if (++shown >= max) break;
        }
        string t = sb.ToString().TrimEnd();
        if (items.Count > shown) t += "\r\n… и ещё " + (items.Count - shown) + " — уточни название";
        return t;
    }

    string stockShownQ;
    public void StockSearch(string q)
    {
        q = q.Trim(); if (q.Length < 2) return;
        bool exact = false;
        List<StockItem> found = stockCache != null ? CacheFind(q, out exact) : null;
        if (found == null || found.Count == 0) { StockSearchLive(q); return; }
        string age = stockCacheAt == DateTime.MinValue ? "" : " · данные " + stockCacheAt.ToString("HH:mm");
        string link = "https://online.moysklad.ru/app/#stockReport?global_searchFilter=" + Uri.EscapeDataString(q);
        stockShownQ = q;
        string near = exact ? "" : "(точного нет, ближайшее)\r\n";
        ShowInfoPop("Остатки: «" + q + "»" + age + " · уточняю…", near + StockText(found, 10), link);
        me.HappyT = 30;
        // уточняем цифры по найденным позициям прямо из МойСклад
        string token = MsToken(); if (token.Length == 0) return;
        List<StockItem> shownItems = found.Count > 10 ? found.GetRange(0, 10) : found;
        List<string> ws = QWords(q); string key = ws.Count > 0 ? ws[0] : q;
        foreach (string w in ws) if (w.Length > key.Length) key = w;
        // ключ для запроса — самое длинное слово, которое реально есть в названии
        foreach (string w in ws) if (w.Length >= key.Length && shownItems[0].Norm.Contains(w)) key = w;
        if (!shownItems[0].Norm.Contains(key)) { string[] nw = shownItems[0].Norm.Split(' '); key = nw[0]; foreach (string w in nw) if (w.Length > key.Length) key = w; }
        string k2 = key, qq = q, lk = link; bool ex2 = exact;
        ThreadPool.QueueUserWorkItem(delegate
        {
            bool ok = false; string zero = null;
            // точного в остатках нет — может, такая карточка есть, но закончилась
            if (!ex2)
            {
                try
                {
                    StringBuilder z = new StringBuilder();
                    foreach (string row in JRows(MsGet(token, "entity/product?limit=5&search=" + Uri.EscapeDataString(qq)), "rows"))
                    {
                        Match pv = Regex.Match(row, "\"salePrices\"\\s*:\\s*\\[\\s*\\{\\s*\"value\"\\s*:\\s*([0-9.]+)");
                        double price = 0; if (pv.Success) double.TryParse(pv.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out price);
                        z.Append(JStr(row, "name") ?? "?").Append("\r\n   нет в наличии");
                        if (price > 0) z.Append(" · ").Append((price / 100).ToString("#,0", new CultureInfo("ru-RU"))).Append(" ₽");
                        z.Append("\r\n");
                    }
                    if (z.Length > 0) zero = z.ToString() + "\r\nПохожие в наличии:\r\n";
                }
                catch { }
            }
            try
            {
                string by = MsGet(token, "report/stock/bystore?limit=1000&filter=search=" + Uri.EscapeDataString(k2));
                Dictionary<string, List<KeyValuePair<string, double>>> fresh = new Dictionary<string, List<KeyValuePair<string, double>>>();
                Dictionary<string, double> tot = new Dictionary<string, double>();
                foreach (string row in JRows(by, "rows"))
                {
                    string id = MsId(row); if (id == null) continue;
                    List<KeyValuePair<string, double>> l = new List<KeyValuePair<string, double>>(); double t = 0;
                    foreach (string st in JRows(row, "stockByStore"))
                    {
                        string sn = JStr(st, "name") ?? "?"; double n = JD(st, "stock"); t += n;
                        if (n <= 0 || sn.StartsWith("Я ")) continue;
                        l.Add(new KeyValuePair<string, double>(sn.Replace("Склад ", ""), n));
                    }
                    fresh[id] = l; tot[id] = t;
                }
                foreach (StockItem it in shownItems)
                {
                    List<KeyValuePair<string, double>> l;
                    if (fresh.TryGetValue(it.Id, out l)) { it.Stores = l; it.Stock = tot[it.Id]; }
                    else { it.Stores = new List<KeyValuePair<string, double>>(); it.Stock = 0; }   // в отчёте нет — закончился
                }
                ok = true;
            }
            catch { }
            try
            {
                BeginInvoke((Action)delegate
                {
                    if (stockShownQ != qq || clipPop == null || !clipPop.Visible) return;
                    ShowInfoPop("Остатки: «" + qq + "»" + (ok ? " · сейчас ✓" : " · данные " + stockCacheAt.ToString("HH:mm")), (zero ?? near) + StockText(found, 10), lk);
                });
            }
            catch { }
        });
    }

    // маленькое окошко с текстом (то же, что у буфера обмена)
    void ShowInfoPop(string head, string body, string link)
    {
        ClipPopBuild();
        clipHead.Text = head; clipBtns.Controls.Clear();
        clipOut.Visible = body != null; if (body != null) { clipOut.Text = body; clipOut.Select(0, 0); }
        if (body != null)
        {
            Button ok = SButtonS("Готово", true); ok.Click += delegate { ClipPopHide(); }; clipBtns.Controls.Add(ok);
            if (link != null) { Button op = SButtonS("Открыть в МойСклад", false); op.Click += delegate { OpenUrl(link); ClipPopHide(); }; clipBtns.Controls.Add(op); }
        }
        ClipLayout();
        if (!clipPop.Visible) clipPop.Show();
        ClipLayout(); ClipToTop();
        clipPopLeft = body == null ? 4 * 60 : 4 * 40; clipPopT.Start();
    }
    // ================= большая панель «VapePet — твой цифровой друг» =================
    LayerWin bigWin; public bool bigOpen; int bigHover = -1; int bigIdleT; float bk = 1f;
    List<KeyValuePair<RectangleF, Action>> bigHits = new List<KeyValuePair<RectangleF, Action>>();
    float[] netHist = new float[48]; int netHistN;
    // прогноз на 3 дня
    int[] wxDayCode = new int[] { -1, -1, -1, -1 }; float[] wxDayMax = new float[4];
    // курсы для панели
    float usdNow, usdPrev, eurNow, eurPrev, btcNow, btcPct; float[] usdHist; DateTime fxAt = DateTime.MinValue; volatile bool fxBusy;
    bool wxIconDay;

    float B(float v) { return v * bk * 0.9f; }

    // ---------- настоящее «матовое стекло»: размытый снимок того, что под панелью ----------
    [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr h, uint aff);
    bool bigBlurOk; Bitmap bigBlur; public Bitmap PreviewBehind;
    void GrabBehind()
    {
        try
        {
            Size sz = BigSize(); Point at = BigPos();
            Bitmap src = null;
            if (PreviewBehind != null) src = PreviewBehind.Clone(new Rectangle(0, 0, Math.Min(sz.Width, PreviewBehind.Width), Math.Min(sz.Height, PreviewBehind.Height)), PixelFormat.Format24bppRgb);
            else if (bigBlurOk) { src = new Bitmap(sz.Width, sz.Height, PixelFormat.Format24bppRgb); using (Graphics g = Graphics.FromImage(src)) g.CopyFromScreen(at.X, at.Y, 0, 0, sz); }
            if (src == null) return;
            int sw = Math.Max(4, sz.Width / 14), sh = Math.Max(4, sz.Height / 14);
            using (src)
            using (Bitmap small = new Bitmap(sw, sh, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(src, 0, 0, sw, sh); }
                // плавное увеличение в два шага — без «квадратиков»
                Bitmap big = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppPArgb);
                using (Bitmap mid = new Bitmap(sw * 4, sh * 4, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(mid)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(small, new Rectangle(-2, -2, mid.Width + 4, mid.Height + 4)); }
                    using (Bitmap mid2 = new Bitmap(sw, sh, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(mid2)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(mid, 0, 0, sw, sh); }
                        using (Graphics g = Graphics.FromImage(mid)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(mid2, new Rectangle(-2, -2, mid.Width + 4, mid.Height + 4)); }
                    }
                    using (Graphics g = Graphics.FromImage(big)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(mid, new Rectangle(-4, -4, sz.Width + 8, sz.Height + 8)); }
                }
                if (bigBlur != null) bigBlur.Dispose(); bigBlur = big;
            }
        }
        catch { }
    }
    Size BigSize() { return new Size((int)B(340), (int)B(468)); }
    public bool bigHot;   // мышь над панелью — полная яркость, иначе полупрозрачная
    Point BigPos()
    {
        Rectangle wa = Screen.PrimaryScreen.WorkingArea; Size s = BigSize();
        return new Point(wa.Right - s.Width - (int)B(10), wa.Bottom - s.Height - (int)B(10));
    }

    public bool bigSeen;
    void BigFirstShow() { if (!bigSeen && tick >= 30 * 12 && !roaming && mode == 0) { bigSeen = true; SaveSettings(); BigOpen(); } }

    public void BigOpen()
    {
        try
        {
            using (Graphics g0 = Graphics.FromHwnd(IntPtr.Zero)) bk = g0.DpiX / 96f;
            if (bigWin == null || bigWin.IsDisposed)
            {
                bigWin = new LayerWin(this, BigSize, BigPos, DrawBig);
                bigWin.LeftClick = delegate (Point p) { BigClick(p); };
                try { bigWin.Show(); bigWin.Hide(); bigBlurOk = SetWindowDisplayAffinity(bigWin.Handle, 0x11); } catch { bigBlurOk = false; }
                bigWin.Hover = delegate (Point p)
                {
                    int h = BigHitAt(p); bigIdleT = 0; bool hot = p.X >= 0;
                    if (h != bigHover || hot != bigHot) { bigHover = h; bigHot = hot; bigWin.Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; BigRender(); }
                };
            }
            bigOpen = true; bigIdleT = 0; bigHover = -1;
            if ((DateTime.Now - fxAt).TotalHours >= 2) FxFetch();
            if (wxDayCode[0] < 0) FetchWeather();
            GrabBehind(); BigRender(); bigWin.Show();
            me.HappyT = 30;
        }
        catch (Exception ex) { GuardLog("большая панель", ex); }
    }

    public void BigClose() { bigOpen = false; try { if (bigWin != null) bigWin.Hide(); } catch { } }

    void BigRender() { try { if (bigWin != null && bigOpen) bigWin.Render(); } catch (Exception ex) { GuardLog("большая панель: кадр", ex); } }

    int BigHitAt(Point p) { for (int i = 0; i < bigHits.Count; i++) if (bigHits[i].Key.Contains(p)) return i; return -1; }
    void BigClick(Point p) { int i = BigHitAt(p); if (i >= 0) { Action a = bigHits[i].Value; try { a(); } catch (Exception ex) { GuardLog("панель: клик", ex); } } }

    void BigTick()
    {
        BigFirstShow();
        if (!bigOpen) return;
        if (tick % 30 == 0)
        {
            netHist[netHistN % netHist.Length] = netRx; netHistN++;
            bool over = bigWin != null && bigWin.Bounds.Contains(Cursor.Position);
            if (over) bigIdleT = 0; else if (++bigIdleT > 90) { BigClose(); return; }   // 1,5 минуты без внимания — сворачиваем
            if (over != bigHot) { bigHot = over; BigRender(); }
            else if (netHistN % 2 == 0) { GrabBehind(); BigRender(); }
            if (bigWin != null) SetWindowPos(bigWin.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }
    }

    // ---------- данные: прогноз, курсы ----------
    void ParseDaily(string json)
    {
        try
        {
            int di = json.IndexOf("\"daily\":{"); if (di < 0) return;
            string d = json.Substring(di);
            Match mc = Regex.Match(d, "\"weather_code\"\\s*:\\s*\\[([^\\]]*)\\]"), mt = Regex.Match(d, "\"temperature_2m_max\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (!mc.Success || !mt.Success) return;
            string[] c = mc.Groups[1].Value.Split(','), t = mt.Groups[1].Value.Split(',');
            for (int i = 0; i < 4 && i < c.Length && i < t.Length; i++)
            {
                float v; if (float.TryParse(c[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) wxDayCode[i] = (int)v;
                if (float.TryParse(t[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) wxDayMax[i] = v;
            }
        }
        catch { }
    }

    void FxFetch()
    {
        if (fxBusy) return; fxBusy = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string d1 = DateTime.Now.AddDays(-40).ToString("dd'/'MM'/'yyyy"), d2 = DateTime.Now.AddDays(1).ToString("dd'/'MM'/'yyyy");
                float[] u = CbrDyn("R01235", d1, d2), e = CbrDyn("R01239", d1, d2);
                if (u.Length >= 2) { usdHist = u; usdNow = u[u.Length - 1]; usdPrev = u[u.Length - 2]; }
                if (e.Length >= 2) { eurNow = e[e.Length - 1]; eurPrev = e[e.Length - 2]; }
                fxAt = DateTime.Now;
            }
            catch { fxAt = DateTime.Now.AddHours(-1.8); }
            try
            {
                string b = HttpT("https://api.binance.com/api/v3/ticker/24hr?symbol=BTCUSDT", 10000, Encoding.UTF8);
                Match lp = Regex.Match(b, "\"lastPrice\"\\s*:\\s*\"([0-9.]+)\""), pc = Regex.Match(b, "\"priceChangePercent\"\\s*:\\s*\"(-?[0-9.]+)\"");
                float v;
                if (lp.Success && float.TryParse(lp.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) btcNow = v;
                if (pc.Success && float.TryParse(pc.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) btcPct = v;
            }
            catch { }
            fxBusy = false;
            try { BeginInvoke((Action)delegate { BigRender(); }); } catch { }
        });
    }

    static float[] CbrDyn(string code, string d1, string d2)
    {
        string x = HttpT("https://www.cbr.ru/scripts/XML_dynamic.asp?date_req1=" + d1 + "&date_req2=" + d2 + "&VAL_NM_RQ=" + code, 15000, Encoding.GetEncoding(1251));
        List<float> r = new List<float>();
        foreach (Match m in Regex.Matches(x, "<Nominal>([0-9]+)</Nominal>\\s*<Value>([0-9]+,[0-9]+)</Value>"))
        {
            float n = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), v = float.Parse(m.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            r.Add(v / Math.Max(1f, n));
        }
        return r.ToArray();
    }

    // ---------- рисование ----------
    static readonly Color Txt = Color.FromArgb(250, 252, 255), TxtDim = Color.FromArgb(215, 225, 235, 250);

    void Glass(Graphics g, RectangleF r, float rad, bool hover)
    {
        using (GraphicsPath p = RoundRect(r.X, r.Y, r.Width, r.Height, rad))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(hover ? 70 : 38, 255, 255, 255), Color.FromArgb(hover ? 34 : 12, 255, 255, 255), 90f)) g.FillPath(b, p);
            using (LinearGradientBrush eb = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), Color.FromArgb(hover ? 230 : 170, 255, 255, 255), Color.FromArgb(40, 255, 255, 255), 90f))
            using (Pen pen = new Pen(eb, B(1))) g.DrawPath(pen, p);
        }
        // блик-изгиб сверху, как у жидкого стекла
        RectangleF hl = new RectangleF(r.X + rad * 0.6f, r.Y + B(1.2f), r.Width - rad * 1.2f, Math.Min(r.Height * 0.38f, B(16)));
        using (GraphicsPath hp = RoundRect(hl.X, hl.Y, hl.Width, hl.Height, hl.Height / 2))
        using (LinearGradientBrush hb = new LinearGradientBrush(new RectangleF(hl.X, hl.Y - 1, hl.Width, hl.Height + 2), Color.FromArgb(38, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(hb, hp);
    }

    void Pill(Graphics g, RectangleF r, Color c1, Color c2, string text, Font f, bool hover, string glyph)
    {
        using (GraphicsPath p = RoundRect(r.X, r.Y, r.Width, r.Height, r.Height / 2))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(hover ? 230 : 175, hover ? ControlPaint.Light(c1, 0.25f) : c1), Color.FromArgb(hover ? 230 : 175, hover ? ControlPaint.Light(c2, 0.25f) : c2), 90f)) g.FillPath(b, p);
            using (LinearGradientBrush eb = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), Color.FromArgb(200, 255, 255, 255), Color.FromArgb(40, 255, 255, 255), 90f))
            using (Pen pen = new Pen(eb, B(0.9f))) g.DrawPath(pen, p);
        }
        using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; STr(g, (glyph != null ? glyph + " " : "") + text, f, Brushes.White, r, sf); }
    }

    static readonly SolidBrush ShadowB = new SolidBrush(Color.FromArgb(110, 0, 10, 30));
    void STr(Graphics g, string s, Font f, Brush b, float x, float y) { g.DrawString(s, f, ShadowB, x + 0.8f, y + 1f); g.DrawString(s, f, b, x, y); }
    void STr(Graphics g, string s, Font f, Brush b, float x, float y, StringFormat sf) { g.DrawString(s, f, ShadowB, x + 0.8f, y + 1f, sf); g.DrawString(s, f, b, x, y, sf); }
    void STr(Graphics g, string s, Font f, Brush b, RectangleF r, StringFormat sf) { RectangleF r2 = r; r2.Offset(0.8f, 1f); g.DrawString(s, f, ShadowB, r2, sf); g.DrawString(s, f, b, r, sf); }
    void STr(Graphics g, string s, Font f, Brush b, PointF p) { STr(g, s, f, b, p.X, p.Y); }

    void Hit(RectangleF r, Action a) { bigHits.Add(new KeyValuePair<RectangleF, Action>(r, a)); }
    bool Hov(RectangleF r) { int i = BigHitAt(Point.Round(new PointF(r.X + r.Width / 2, r.Y + r.Height / 2))); return i >= 0 && i == bigHover; }

    void DrawBig(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        bigHits.Clear();
        Size s = BigSize(); float W0 = s.Width, H0 = s.Height;
        RectangleF all = new RectangleF(B(3), B(3), W0 - B(6), H0 - B(6));
        using (GraphicsPath p = RoundRect(all.X, all.Y, all.Width, all.Height, B(20)))
        {
            // матовое стекло: размытое то, что под панелью, + тонировка + белая дымка сверху
            bool blur = bigBlur != null;
            if (blur) { GraphicsState st = g.Save(); g.SetClip(p); g.DrawImageUnscaled(bigBlur, 0, 0); g.Restore(st); }
            using (LinearGradientBrush b = new LinearGradientBrush(all, Color.FromArgb(blur ? 150 : 215, 24, 34, 58), Color.FromArgb(blur ? 172 : 228, 14, 20, 38), 90f)) g.FillPath(b, p);
            using (LinearGradientBrush w = new LinearGradientBrush(all, Color.FromArgb(46, 255, 255, 255), Color.FromArgb(6, 255, 255, 255), 90f)) g.FillPath(w, p);
            using (LinearGradientBrush eb = new LinearGradientBrush(RectangleF.Inflate(all, 1, 1), Color.FromArgb(235, 255, 255, 255), Color.FromArgb(60, 255, 255, 255), 90f))
            using (Pen pen = new Pen(eb, B(1.3f))) g.DrawPath(pen, p);
            using (GraphicsPath ip = RoundRect(all.X + B(1.5f), all.Y + B(1.5f), all.Width - B(3), all.Height - B(3), B(18.5f)))
            using (Pen inner = new Pen(Color.FromArgb(45, 255, 255, 255), 1f)) g.DrawPath(inner, ip);
        }
        using (Font fTitle = new Font("Segoe UI Semibold", B(15), FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font fBig = new Font("Segoe UI Semibold", B(13), FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font fHuge = new Font("Segoe UI Semibold", B(21), FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font fN = new Font("Segoe UI", B(10.5f), FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font fS = new Font("Segoe UI", B(9), FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font fB = new Font("Segoe UI Semibold", B(10), FontStyle.Bold, GraphicsUnit.Pixel))
        using (SolidBrush tx = new SolidBrush(Txt)) using (SolidBrush dim = new SolidBrush(TxtDim))
        {
            float x0 = all.X + B(8), cw = all.Width - B(16), y = all.Y + B(8);

            // --- шапка
            RectangleF hdr = new RectangleF(x0, y, cw, B(42)); Glass(g, hdr, B(12), false);
            RectangleF logo = new RectangleF(hdr.X + B(7), hdr.Y + B(6), B(30), B(30));
            using (GraphicsPath lp = RoundRect(logo.X, logo.Y, logo.Width, logo.Height, B(8)))
            using (LinearGradientBrush lb = new LinearGradientBrush(logo, Color.FromArgb(120, 190, 255), Color.FromArgb(50, 110, 230), 90f)) g.FillPath(lb, lp);
            Paw(g, logo.X + logo.Width / 2, logo.Y + logo.Height / 2 + B(1), B(11), Color.White);
            STr(g, "VapePet", fTitle, tx, hdr.X + B(44), hdr.Y + B(4));
            STr(g, "Твой цифровой друг ♥", fS, dim, hdr.X + B(45), hdr.Y + B(23));
            string[] hb = { "—", "☰", "✕" };
            Action[] ha = { delegate { BigClose(); }, delegate { OpenMenuPath(); }, delegate { BigClose(); } };
            for (int i = 0; i < 3; i++)
            {
                RectangleF br = new RectangleF(hdr.Right - B(32) * (3 - i) - B(4), hdr.Y + B(8), B(26), B(26)); Hit(br, ha[i]);
                bool hv = Hov(br);
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(hv ? 120 : 60, 255, 255, 255))) g.FillEllipse(cb, br);
                using (Pen cp = new Pen(Color.FromArgb(160, 255, 255, 255), B(1))) g.DrawEllipse(cp, br);
                using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; STr(g, hb[i], fB, Brushes.White, br, sf); }
            }
            y = hdr.Bottom + B(6);

            // --- погода
            RectangleF wc = new RectangleF(x0, y, cw, B(66)); Hit(wc, delegate { FetchWeather(); string wr = WeatherRemark(); if (wr != null) Say(wr, me.X, CatTop(me) - U(1), 'U'); });
            Glass(g, wc, B(12), Hov(wc));
            if (wxOk)
            {
                DrawWxIcon(g, wc.X + B(30), wc.Y + B(33), B(21));
                int t = (int)Math.Round(wxTemp);
                STr(g, (t > 0 ? "+" : "") + t + "°C", fHuge, tx, wc.X + B(56), wc.Y + B(8));
                string city = wxCity != null && wxCity.Length > 0 ? wxCity : "";
                if (city.Length > 12) city = city.Substring(0, 11) + "…";
                SizeF ts = g.MeasureString((t > 0 ? "+" : "") + t + "°C", fHuge);
                STr(g, "⌖ " + city, fS, dim, wc.X + B(58) + ts.Width, wc.Y + B(16));
                STr(g, WxText(wxCode), fS, dim, wc.X + B(58), wc.Y + B(40));
                // разделитель и 3 дня
                float dx = wc.Right - B(132);
                using (Pen sp = new Pen(Color.FromArgb(70, 255, 255, 255), 1)) g.DrawLine(sp, dx, wc.Y + B(10), dx, wc.Bottom - B(10));
                string[] dn = { "Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };
                using (StringFormat sc = new StringFormat()) { sc.Alignment = StringAlignment.Center;
                    for (int i = 1; i <= 3; i++)
                    {
                        float cx = dx + B(22) + (i - 1) * B(42);
                        STr(g, dn[(int)DateTime.Now.AddDays(i).DayOfWeek], fB, tx, cx, wc.Y + B(6), sc);
                        if (wxDayCode[i] >= 0)
                        {
                            int sv = wxCode; wxCode = wxDayCode[i]; wxIconDay = true;
                            try { DrawWxIcon(g, cx, wc.Y + B(33), B(10)); } finally { wxCode = sv; wxIconDay = false; }
                            int tm = (int)Math.Round(wxDayMax[i]);
                            STr(g, (tm > 0 ? "+" : "") + tm + "°", fB, tx, cx, wc.Y + B(46), sc);
                        }
                    } }
            }
            else STr(g, wxErr != null ? "Погода: нет связи" : "Погода: загружаю…", fN, dim, wc.X + B(14), wc.Y + B(24));
            y = wc.Bottom + B(6);

            // --- VPN
            RectangleF vc = new RectangleF(x0, y, cw, B(56)); Glass(g, vc, B(12), false);
            bool on = vpnName != null;
            Shield(g, vc.X + B(26), vc.Y + B(28), B(17), on ? Color.FromArgb(70, 220, 150) : (vpnApp != null ? Color.FromArgb(240, 190, 60) : Color.FromArgb(150, 160, 180)), on);
            STr(g, "VPN", fBig, tx, vc.X + B(52), vc.Y + B(9));
            Color stc = on ? Color.FromArgb(90, 225, 130) : vpnApp != null ? Color.FromArgb(245, 195, 70) : Color.FromArgb(170, 180, 200);
            using (SolidBrush sb = new SolidBrush(stc)) { g.FillEllipse(sb, vc.X + B(53), vc.Y + B(34), B(6), B(6)); STr(g, on ? "Подключено" : vpnApp != null ? "Не подключён" : "Выключен", fS, sb, vc.X + B(61), vc.Y + B(29)); }
            float vx = vc.X + B(140);
            using (Pen sp = new Pen(Color.FromArgb(70, 255, 255, 255), 1)) g.DrawLine(sp, vx - B(8), vc.Y + B(10), vx - B(8), vc.Bottom - B(10));
            string ctry = geoLine ?? "—", vcity = "";
            int comma = ctry.IndexOf(','); if (comma > 0) { vcity = ctry.Substring(comma + 1).Trim(); ctry = ctry.Substring(0, comma).Trim(); }
            Flag(g, new RectangleF(vx, vc.Y + B(13), B(17), B(12)), ctry);
            string ct = ctry.Length > 11 ? ctry.Substring(0, 10) + "…" : ctry; string cc = vcity.Length > 12 ? vcity.Substring(0, 11) + "…" : vcity;
            STr(g, ct, fB, tx, vx + B(21), vc.Y + B(10)); STr(g, cc, fS, dim, vx + B(21), vc.Y + B(29));
            RectangleF vb = new RectangleF(vc.Right - B(76), vc.Y + B(14), B(68), B(28)); Hit(vb, delegate { OpenVpnApp(); });
            Pill(g, vb, Color.FromArgb(60, 190, 130), Color.FromArgb(25, 120, 85), "Открыть", fB, Hov(vb), "⚡");
            y = vc.Bottom + B(6);

            // --- интернет
            RectangleF ic = new RectangleF(x0, y, cw, B(56)); Hit(ic, delegate { SpeedTest(); }); Glass(g, ic, B(12), Hov(ic));
            Wifi(g, ic.X + B(26), ic.Y + B(31), B(17), netDown ? Color.FromArgb(240, 90, 90) : Color.FromArgb(80, 190, 255));
            STr(g, "Интернет", fBig, tx, ic.X + B(52), ic.Y + B(9));
            Color nc = netDown ? Color.FromArgb(240, 90, 90) : Color.FromArgb(90, 225, 130);
            string ns = netDown ? "Нет связи · " + (int)(DateTime.Now - netDownSince).TotalMinutes + " мин" : netSpeedBps > 0 ? "Скорость " + Mbit(netSpeedBps) : "Подключение активно";
            using (SolidBrush sb = new SolidBrush(nc)) { g.FillEllipse(sb, ic.X + B(53), ic.Y + B(34), B(6), B(6)); STr(g, ns, fS, sb, ic.X + B(61), ic.Y + B(29)); }
            float sx = ic.X + B(168);
            using (SolidBrush db = new SolidBrush(Color.FromArgb(90, 180, 255))) STr(g, "↓", fB, db, sx, ic.Y + B(9));
            using (SolidBrush ub = new SolidBrush(Color.FromArgb(255, 120, 150))) STr(g, "↑", fB, ub, sx, ic.Y + B(29));
            STr(g, Mbit(netRx), fS, tx, sx + B(11), ic.Y + B(10)); STr(g, Mbit(netTx), fS, tx, sx + B(11), ic.Y + B(30));
            Spark(g, new RectangleF(ic.Right - B(74), ic.Y + B(10), B(64), B(36)), NetSeries(), Color.FromArgb(80, 170, 255));
            y = ic.Bottom + B(6);

            // --- курсы
            RectangleF rc = new RectangleF(x0, y, cw, B(76)); Hit(rc, delegate { FxFetch(); ShowRates(); }); Glass(g, rc, B(12), Hov(rc));
            Coins(g, rc.X + B(26), rc.Y + B(40), B(16));
            STr(g, "Курсы валют", fBig, tx, rc.X + B(52), rc.Y + B(7));
            RateLine(g, rc.X + B(54), rc.Y + B(27), "$", usdNow, usdPrev, false, fS, fB, tx);
            RateLine(g, rc.X + B(54), rc.Y + B(42), "€", eurNow, eurPrev, false, fS, fB, tx);
            RateLine(g, rc.X + B(54), rc.Y + B(57), "₿", btcNow, btcPct, true, fS, fB, tx);
            if (usdHist != null && usdHist.Length > 2) Spark(g, new RectangleF(rc.Right - B(98), rc.Y + B(14), B(88), B(50)), usdHist, Color.FromArgb(250, 200, 70));
            else STr(g, fxBusy ? "загружаю…" : "", fS, dim, rc.Right - B(80), rc.Y + B(32));
            y = rc.Bottom + B(6);

            // --- ИИ-помощник
            RectangleF ac = new RectangleF(x0, y, cw, B(52)); Glass(g, ac, B(12), false);
            Robot(g, ac.X + B(26), ac.Y + B(26), B(15));
            STr(g, "ИИ-помощник", fBig, tx, ac.X + B(52), ac.Y + B(8));
            STr(g, aiKey.Length > 0 ? "Задай вопрос…" : "Нужен ключ в настройках", fS, dim, ac.X + B(53), ac.Y + B(28));
            RectangleF ab = new RectangleF(ac.Right - B(104), ac.Y + B(12), B(96), B(28)); Hit(ab, delegate { BigClose(); OpenChat(); });
            Pill(g, ab, Color.FromArgb(110, 90, 255), Color.FromArgb(70, 50, 210), "Открыть чат", fB, Hov(ab), "💬");
            y = ac.Bottom + B(6);

            // --- нижняя панель кнопок
            RectangleF tb = new RectangleF(x0, y, cw, all.Bottom - B(8) - y); Glass(g, tb, B(12), false);
            string[] tl = { "Буфер\nобмена", "Таймер", "Напоми-\nнания", "Настройки", "Больше" };
            Action[] ta = {
                delegate { OpenMenuPath("Буфер обмена"); },
                delegate { OpenMenuPath("Инструменты", "Таймер"); },
                delegate { AskReminder(); },
                delegate { OpenMenuPath("Настройки"); },
                delegate { OpenMenuPath(); } };
            float bw = tb.Width / 5;
            for (int i = 0; i < 5; i++)
            {
                RectangleF br = new RectangleF(tb.X + i * bw + B(2), tb.Y + B(3), bw - B(4), tb.Height - B(6)); Hit(br, ta[i]);
                if (Hov(br)) using (GraphicsPath hp = RoundRect(br.X, br.Y, br.Width, br.Height, B(9))) using (SolidBrush hbsh = new SolidBrush(Color.FromArgb(55, 255, 255, 255))) g.FillPath(hbsh, hp);
                if (i > 0) using (Pen sp = new Pen(Color.FromArgb(50, 255, 255, 255), 1)) g.DrawLine(sp, br.X - B(2), br.Y + B(8), br.X - B(2), br.Bottom - B(8));
                ToolIcon(g, i, br.X + br.Width / 2, br.Y + B(17), B(11));
                using (StringFormat sc = new StringFormat()) { sc.Alignment = StringAlignment.Center; STr(g, tl[i], fS, tx, new RectangleF(br.X - B(4), br.Y + B(32), br.Width + B(8), B(26)), sc); }
            }
        }
    }

    float[] NetSeries()
    {
        int n = Math.Min(netHistN, netHist.Length); float[] r = new float[Math.Max(n, 2)];
        for (int i = 0; i < n; i++) r[i] = netHist[(netHistN - n + i) % netHist.Length];
        return r;
    }

    void RateLine(Graphics g, float x, float y, string cur, float now, float prevOrPct, bool pct, Font fS, Font fB, Brush tx)
    {
        STr(g, cur, fB, tx, x, y);
        if (now <= 0) { STr(g, "—", fS, tx, x + B(13), y + B(1)); return; }
        string v = pct ? now.ToString("#,0", new CultureInfo("ru-RU")) : now.ToString("0.00", new CultureInfo("ru-RU"));
        STr(g, v, fS, tx, x + B(13), y + B(1));
        float d = pct ? prevOrPct : now - prevOrPct;
        if (!pct && prevOrPct <= 0) return;
        bool up = d >= 0;
        string ds = (up ? "▲ +" : "▼ ") + (pct ? d.ToString("0.00", CultureInfo.InvariantCulture) + "%" : d.ToString("0.00", CultureInfo.InvariantCulture));
        SizeF vs = g.MeasureString(v, fS);
        using (SolidBrush b = new SolidBrush(up ? Color.FromArgb(110, 230, 120) : Color.FromArgb(255, 110, 110))) STr(g, ds, fS, b, x + B(15) + vs.Width, y + B(1));
    }

    void Spark(Graphics g, RectangleF r, float[] v, Color c)
    {
        if (v == null || v.Length < 2) return;
        float mn = float.MaxValue, mx = float.MinValue; foreach (float f in v) { mn = Math.Min(mn, f); mx = Math.Max(mx, f); }
        if (mx - mn < 1e-6f) { mx = mn + 1; }
        PointF[] pts = new PointF[v.Length];
        for (int i = 0; i < v.Length; i++) pts[i] = new PointF(r.X + r.Width * i / (v.Length - 1), r.Bottom - (v[i] - mn) / (mx - mn) * r.Height * 0.85f - r.Height * 0.05f);
        using (GraphicsPath area = new GraphicsPath())
        {
            area.AddLines(pts); area.AddLine(pts[pts.Length - 1], new PointF(r.Right, r.Bottom)); area.AddLine(new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom)); area.CloseFigure();
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Color.FromArgb(150, c), Color.FromArgb(10, c), 90f)) g.FillPath(b, area);
        }
        using (Pen p = new Pen(c, B(1.6f))) { p.LineJoin = LineJoin.Round; g.DrawLines(p, pts); }
        if (v.Length <= 40) using (SolidBrush b = new SolidBrush(c)) for (int i = 0; i < pts.Length; i += Math.Max(1, pts.Length / 6)) g.FillEllipse(b, pts[i].X - B(2), pts[i].Y - B(2), B(4), B(4));
    }

    // ---------- значки ----------
    void Paw(Graphics g, float cx, float cy, float s, Color c)
    {
        using (SolidBrush b = new SolidBrush(c))
        {
            g.FillEllipse(b, cx - s * 0.55f, cy - s * 0.05f, s * 1.1f, s * 0.85f);
            float[][] t = { new float[] { -0.75f, -0.45f }, new float[] { -0.28f, -0.85f }, new float[] { 0.28f, -0.85f }, new float[] { 0.75f, -0.45f } };
            foreach (float[] p in t) g.FillEllipse(b, cx + p[0] * s - s * 0.2f, cy + p[1] * s - s * 0.25f, s * 0.4f, s * 0.5f);
        }
    }
    void Shield(Graphics g, float cx, float cy, float s, Color c, bool ok)
    {
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddBezier(cx, cy - s, cx + s * 0.5f, cy - s * 0.75f, cx + s * 0.9f, cy - s * 0.8f, cx + s * 0.9f, cy - s * 0.8f);
            p.AddBezier(cx + s * 0.9f, cy - s * 0.8f, cx + s * 0.95f, cy + s * 0.2f, cx + s * 0.5f, cy + s * 0.7f, cx, cy + s);
            p.AddBezier(cx, cy + s, cx - s * 0.5f, cy + s * 0.7f, cx - s * 0.95f, cy + s * 0.2f, cx - s * 0.9f, cy - s * 0.8f);
            p.AddBezier(cx - s * 0.9f, cy - s * 0.8f, cx - s * 0.9f, cy - s * 0.8f, cx - s * 0.5f, cy - s * 0.75f, cx, cy - s);
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(50, c))) g.FillEllipse(glow, cx - s * 1.3f, cy - s * 1.3f, s * 2.6f, s * 2.6f);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(cx - s, cy - s, s * 2, s * 2), ControlPaint.Light(c, 0.3f), ControlPaint.Dark(c, 0.15f), 90f)) g.FillPath(b, p);
            using (Pen pen = new Pen(Color.FromArgb(200, 255, 255, 255), B(1.2f))) g.DrawPath(pen, p);
        }
        using (Pen m = new Pen(Color.White, B(2.4f))) { m.StartCap = m.EndCap = LineCap.Round; if (ok) g.DrawLines(m, new PointF[] { new PointF(cx - s * 0.4f, cy), new PointF(cx - s * 0.08f, cy + s * 0.32f), new PointF(cx + s * 0.45f, cy - s * 0.3f) }); else { g.DrawLine(m, cx - s * 0.3f, cy - s * 0.3f, cx + s * 0.3f, cy + s * 0.3f); g.DrawLine(m, cx + s * 0.3f, cy - s * 0.3f, cx - s * 0.3f, cy + s * 0.3f); } }
    }
    void Wifi(Graphics g, float cx, float cy, float s, Color c)
    {
        using (Pen p = new Pen(c, s * 0.2f)) { p.StartCap = p.EndCap = LineCap.Round;
            for (int i = 1; i <= 3; i++) { float r = s * 0.38f * i; g.DrawArc(p, cx - r, cy - r, r * 2, r * 2, 225, 90); } }
        using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, cx - s * 0.16f, cy - s * 0.16f, s * 0.32f, s * 0.32f);
    }
    void Coins(Graphics g, float cx, float cy, float s)
    {
        Color g1 = Color.FromArgb(255, 214, 90), g2 = Color.FromArgb(214, 150, 30);
        float[][] st = { new float[] { -0.55f, 5 }, new float[] { 0.5f, 3 } };
        foreach (float[] col in st)
            for (int i = 0; i < (int)col[1]; i++)
            {
                float x = cx + col[0] * s - s * 0.55f, y = cy + s * 0.55f - i * s * 0.28f;
                using (SolidBrush side = new SolidBrush(g2)) g.FillRectangle(side, x, y, s * 1.1f, s * 0.22f);
                using (SolidBrush side2 = new SolidBrush(g2)) g.FillEllipse(side2, x, y + s * 0.1f, s * 1.1f, s * 0.3f);
                using (LinearGradientBrush top = new LinearGradientBrush(new RectangleF(x, y - s * 0.15f, s * 1.1f, s * 0.3f), g1, ControlPaint.Light(g1, 0.4f), 0f)) g.FillEllipse(top, x, y - s * 0.15f, s * 1.1f, s * 0.3f);
            }
    }
    void Robot(Graphics g, float cx, float cy, float s)
    {
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(50, 120, 160, 255))) g.FillEllipse(glow, cx - s * 1.3f, cy - s * 1.3f, s * 2.6f, s * 2.6f);
        RectangleF head = new RectangleF(cx - s * 0.85f, cy - s * 0.6f, s * 1.7f, s * 1.25f);
        using (GraphicsPath p = RoundRect(head.X, head.Y, head.Width, head.Height, s * 0.45f))
        using (LinearGradientBrush b = new LinearGradientBrush(head, Color.White, Color.FromArgb(190, 205, 235), 90f)) g.FillPath(b, p);
        RectangleF face = RectangleF.Inflate(head, -s * 0.2f, -s * 0.22f);
        using (GraphicsPath p = RoundRect(face.X, face.Y, face.Width, face.Height, s * 0.3f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 40, 80))) g.FillPath(b, p);
        using (SolidBrush e = new SolidBrush(Color.FromArgb(90, 210, 255))) { g.FillEllipse(e, cx - s * 0.42f, cy - s * 0.15f, s * 0.25f, s * 0.3f); g.FillEllipse(e, cx + s * 0.17f, cy - s * 0.15f, s * 0.25f, s * 0.3f); }
        using (Pen a = new Pen(Color.FromArgb(200, 210, 235), s * 0.12f)) g.DrawLine(a, cx, head.Y, cx, head.Y - s * 0.3f);
        using (SolidBrush t = new SolidBrush(Color.FromArgb(90, 210, 255))) g.FillEllipse(t, cx - s * 0.13f, head.Y - s * 0.45f, s * 0.26f, s * 0.26f);
    }
    void ToolIcon(Graphics g, int kind, float cx, float cy, float s)
    {
        Color c = Color.FromArgb(120, 190, 255);
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(40, c))) g.FillEllipse(glow, cx - s * 1.3f, cy - s * 1.3f, s * 2.6f, s * 2.6f);
        using (Pen p = new Pen(Color.White, B(1.6f))) using (SolidBrush b = new SolidBrush(c))
        {
            p.StartCap = p.EndCap = LineCap.Round;
            if (kind == 0) { using (GraphicsPath r = RoundRect(cx - s * 0.7f, cy - s * 0.85f, s * 1.4f, s * 1.75f, s * 0.2f)) { g.FillPath(b, r); g.DrawPath(p, r); } g.FillRectangle(Brushes.White, cx - s * 0.35f, cy - s, s * 0.7f, s * 0.3f); for (int i = 0; i < 3; i++) g.DrawLine(p, cx - s * 0.4f, cy - s * 0.2f + i * s * 0.35f, cx + s * 0.4f, cy - s * 0.2f + i * s * 0.35f); }
            else if (kind == 1) { g.FillEllipse(b, cx - s * 0.8f, cy - s * 0.7f, s * 1.6f, s * 1.6f); g.DrawEllipse(p, cx - s * 0.8f, cy - s * 0.7f, s * 1.6f, s * 1.6f); g.DrawLine(p, cx, cy + s * 0.1f, cx, cy - s * 0.4f); g.DrawLine(p, cx, cy + s * 0.1f, cx + s * 0.35f, cy + s * 0.25f); g.DrawLine(p, cx - s * 0.25f, cy - s * 0.95f, cx + s * 0.25f, cy - s * 0.95f); }
            else if (kind == 2) { using (GraphicsPath r = RoundRect(cx - s * 0.75f, cy - s * 0.85f, s * 1.5f, s * 1.7f, s * 0.25f)) { g.FillPath(b, r); g.DrawPath(p, r); } for (int i = 0; i < 3; i++) { float yy = cy - s * 0.4f + i * s * 0.4f; g.FillEllipse(Brushes.White, cx - s * 0.48f, yy - s * 0.08f, s * 0.16f, s * 0.16f); g.DrawLine(p, cx - s * 0.15f, yy, cx + s * 0.45f, yy); } }
            else if (kind == 3) { for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.FillEllipse(b, cx + (float)Math.Cos(a) * s * 0.75f - s * 0.22f, cy + (float)Math.Sin(a) * s * 0.75f - s * 0.22f, s * 0.44f, s * 0.44f); } g.FillEllipse(b, cx - s * 0.7f, cy - s * 0.7f, s * 1.4f, s * 1.4f); g.FillEllipse(Brushes.White, cx - s * 0.28f, cy - s * 0.28f, s * 0.56f, s * 0.56f); }
            else { for (int i = 0; i < 4; i++) { float xx = cx - s * 0.8f + (i % 2) * s * 0.9f, yy = cy - s * 0.8f + (i / 2) * s * 0.9f; using (GraphicsPath r = RoundRect(xx, yy, s * 0.7f, s * 0.7f, s * 0.15f)) { g.FillPath(b, r); g.DrawPath(p, r); } } }
        }
    }
    void Flag(Graphics g, RectangleF r, string country)
    {
        Color[] h = null; bool vert = false; Color crossBg = Color.Empty, cross = Color.Empty;
        switch (country)
        {
            case "Нидерланды": h = new Color[] { Color.FromArgb(174, 28, 40), Color.White, Color.FromArgb(33, 70, 139) }; break;
            case "Россия": h = new Color[] { Color.White, Color.FromArgb(0, 57, 166), Color.FromArgb(213, 43, 30) }; break;
            case "Германия": h = new Color[] { Color.Black, Color.FromArgb(221, 0, 0), Color.FromArgb(255, 206, 0) }; break;
            case "Эстония": h = new Color[] { Color.FromArgb(0, 114, 206), Color.Black, Color.White }; break;
            case "Литва": h = new Color[] { Color.FromArgb(253, 185, 19), Color.FromArgb(0, 106, 68), Color.FromArgb(193, 39, 45) }; break;
            case "Латвия": h = new Color[] { Color.FromArgb(158, 48, 57), Color.White, Color.FromArgb(158, 48, 57) }; break;
            case "Польша": h = new Color[] { Color.White, Color.FromArgb(220, 20, 60) }; break;
            case "Украина": h = new Color[] { Color.FromArgb(0, 87, 183), Color.FromArgb(255, 215, 0) }; break;
            case "Армения": h = new Color[] { Color.FromArgb(217, 0, 18), Color.FromArgb(0, 51, 160), Color.FromArgb(242, 168, 0) }; break;
            case "Франция": h = new Color[] { Color.FromArgb(0, 35, 149), Color.White, Color.FromArgb(237, 41, 57) }; vert = true; break;
            case "Италия": h = new Color[] { Color.FromArgb(0, 146, 70), Color.White, Color.FromArgb(206, 43, 55) }; vert = true; break;
            case "Финляндия": crossBg = Color.White; cross = Color.FromArgb(0, 47, 108); break;
            case "Швеция": crossBg = Color.FromArgb(0, 106, 167); cross = Color.FromArgb(254, 204, 0); break;
            case "Норвегия": crossBg = Color.FromArgb(186, 12, 47); cross = Color.FromArgb(0, 32, 91); break;
            case "Дания": crossBg = Color.FromArgb(200, 16, 46); cross = Color.White; break;
            case "Турция": h = new Color[] { Color.FromArgb(227, 10, 23) }; break;
            case "Казахстан": h = new Color[] { Color.FromArgb(0, 175, 202) }; break;
            case "США": h = new Color[] { Color.FromArgb(178, 34, 52), Color.White, Color.FromArgb(178, 34, 52), Color.White, Color.FromArgb(178, 34, 52) }; break;
            case "Великобритания": h = new Color[] { Color.FromArgb(1, 33, 105) }; break;
        }
        using (GraphicsPath clip = RoundRect(r.X, r.Y, r.Width, r.Height, B(2)))
        {
            GraphicsState st = g.Save(); g.SetClip(clip);
            if (h != null)
                for (int i = 0; i < h.Length; i++)
                    using (SolidBrush b = new SolidBrush(h[i]))
                        if (vert) g.FillRectangle(b, r.X + r.Width * i / h.Length, r.Y, r.Width / h.Length + 1, r.Height);
                        else g.FillRectangle(b, r.X, r.Y + r.Height * i / h.Length, r.Width, r.Height / h.Length + 1);
            else if (crossBg != Color.Empty)
            {
                using (SolidBrush b = new SolidBrush(crossBg)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(cross)) { g.FillRectangle(b, r.X + r.Width * 0.3f, r.Y, r.Width * 0.16f, r.Height); g.FillRectangle(b, r.X, r.Y + r.Height * 0.42f, r.Width, r.Height * 0.18f); }
            }
            else using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 150, 190))) { g.FillRectangle(b, r); using (Pen p = new Pen(Color.White, 1)) { g.DrawEllipse(p, r.X + r.Width * 0.3f, r.Y + r.Height * 0.1f, r.Height * 0.8f, r.Height * 0.8f); } }
            if (country == "США") using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 59, 110))) g.FillRectangle(b, r.X, r.Y, r.Width * 0.45f, r.Height * 0.55f);
            if (country == "Великобритания") using (Pen p = new Pen(Color.White, r.Height * 0.25f)) { g.DrawLine(p, r.X, r.Y, r.Right, r.Bottom); g.DrawLine(p, r.Right, r.Y, r.X, r.Bottom); using (Pen p2 = new Pen(Color.FromArgb(200, 16, 46), r.Height * 0.22f)) { g.DrawLine(p2, r.X + r.Width / 2, r.Y, r.X + r.Width / 2, r.Bottom); g.DrawLine(p2, r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2); } }
            g.Restore(st);
        }
        using (GraphicsPath bp = RoundRect(r.X, r.Y, r.Width, r.Height, B(2))) using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1)) g.DrawPath(p, bp);
    }

    static void CrashLog(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_crash.log"), DateTime.Now + "\r\n" + ex + "\r\n\r\n", Encoding.UTF8); } catch { }
    }

    public static void Start()
    {
        bool created;
        using (Mutex m = new Mutex(true, "VapePet_SingleInstance", out created))
        {
            if (!created) return;
            try { SetProcessDPIAware(); } catch { }
            float dpi = 96;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX;
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object o, ThreadExceptionEventArgs e) { CrashLog(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object o, UnhandledExceptionEventArgs e) { CrashLog(e.ExceptionObject as Exception); };
            Application.Run(new VapePet(dpi));
        }
    }


    // ================= обновления с GitHub =================
    public const string AppVersion = "3.0.1";
    const string UpdateRepo = "capvmorge1legal-ship-it/vapepet";
    bool autoUpdate = true, updBusy, updSaid; string savedVer = "";
    const int UpdEvery = 30 * 60 * 60 * 6;      // раз в 6 часов (30 кадров в секунду)
    int updTick = UpdEvery - 30 * 120, updStart;   // первая проверка через 2 минуты после запуска

    void UpdateTick()
    {
        if (!updSaid && ++updStart > 30 * 8)
        {
            updSaid = true;
            if (savedVer.Length > 0 && savedVer != AppVersion) { LifeSay("Я обновился до версии " + AppVersion + "!"); SaveSettings(); }
            else if (savedVer.Length == 0) SaveSettings();
        }
        if (++updTick < UpdEvery) return;
        updTick = 0;
        if (autoUpdate) CheckUpdate(false);
    }

    static bool RunningAsExe { get { return !Path.GetFileName(Application.ExecutablePath).StartsWith("powershell", StringComparison.OrdinalIgnoreCase); } }

    void CheckUpdate(bool manual)
    {
        if (updBusy) return;
        if (!RunningAsExe) { if (manual) LifeSay("Обновляюсь только из VapePet.exe"); return; }
        updBusy = true;
        if (manual) LifeSay("Проверяю обновления…");
        Thread t = new Thread(delegate ()
        {
            string err = null, newVer = null, file = null;
            try
            {
                string j = HttpT("https://api.github.com/repos/" + UpdateRepo + "/releases/latest", 15000, Encoding.UTF8);
                Match mv = Regex.Match(j, "\"tag_name\"\\s*:\\s*\"v?([0-9][0-9.]*)\"");
                Match mu = Regex.Match(j, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/VapePet\\.exe)\"");
                if (mv.Success && mu.Success && new Version(mv.Groups[1].Value) > new Version(AppVersion))
                {
                    newVer = mv.Groups[1].Value;
                    file = Path.Combine(Path.GetTempPath(), "VapePet-" + newVer + ".exe");
                    using (WebClient wc = new WebClient()) { wc.Headers["User-Agent"] = "VapePet/" + AppVersion; wc.DownloadFile(mu.Groups[1].Value, file); }
                    byte[] head = new byte[2];
                    using (FileStream fs = File.OpenRead(file)) { if (fs.Length < 100000 || fs.Read(head, 0, 2) != 2 || head[0] != (byte)'M' || head[1] != (byte)'Z') throw new Exception("скачанный файл повреждён"); }
                }
            }
            catch (Exception ex) { err = ex.Message; }
            try
            {
                BeginInvoke((Action)delegate
                {
                    updBusy = false;
                    if (err != null) { GuardLog("обновление: " + err, null); if (manual) LifeSay("Не получилось проверить обновления"); return; }
                    if (newVer == null) { if (manual) LifeSay("У меня последняя версия " + AppVersion); return; }
                    ApplyUpdate(file, newVer);
                });
            }
            catch { }
        });
        t.IsBackground = true; t.Start();
    }

    void ApplyUpdate(string file, string ver)
    {
        string exe = Application.ExecutablePath;
        string dir = Path.GetDirectoryName(exe);
        string ps = "Wait-Process -Id " + System.Diagnostics.Process.GetCurrentProcess().Id + " -Timeout 20 -ErrorAction SilentlyContinue; " +
                    "Copy-Item -LiteralPath '" + exe.Replace("'", "''") + "' -Destination '" + Path.Combine(dir, "VapePet-prev.exe").Replace("'", "''") + "' -Force; " +
                    "Move-Item -LiteralPath '" + file.Replace("'", "''") + "' -Destination '" + exe.Replace("'", "''") + "' -Force; " +
                    "Unblock-File -LiteralPath '" + exe.Replace("'", "''") + "'; Start-Process -FilePath '" + exe.Replace("'", "''") + "' -WorkingDirectory '" + dir.Replace("'", "''") + "'";
        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" + ps.Replace("\"", "\\\"") + "\"");
        psi.CreateNoWindow = true; psi.UseShellExecute = false; psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
        LifeSay("Нашёл версию " + ver + ", обновляюсь…");
        SaveSettings();
        System.Diagnostics.Process.Start(psi);
        restarting = true;
        try { timer.Stop(); FreeDib(); } catch { }
        Application.Exit();
    }

    // ================= своё меню (вместо стандартного меню Windows) =================
    // Пункты по-прежнему собираются в ContextMenuStrip «menu» (так их строит весь остальной код),
    // но показывает их собственное окно CatMenu: без задержек Windows, с подсветкой, поиском и тумблерами.
    CatMenu catMenu;
    public bool MenuOpen { get { return catMenu != null && !catMenu.IsDisposed && catMenu.Visible; } }
    DateTime menuAwaySince = DateTime.MinValue;

    public void ShowMenu(Point at)
    {
        CloseMenu();
        catMenu = new CatMenu(this, menu.Items, null);
        catMenu.ShowAt(new Rectangle(at.X, at.Y, 0, 0), false);
        menuAwaySince = DateTime.MinValue;
    }

    public void CloseMenu()
    {
        CatMenu m = catMenu; catMenu = null;
        if (m != null) { try { m.CloseTree(); } catch { } }
    }

    // клик мимо меню — закрыть; меню долго без внимания — тоже закрыть
    void MenuWatch()
    {
        if (!MenuOpen) { lmbWasDown = (GetAsyncKeyState(0x01) & 0x8000) != 0; return; }
        bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
        Point p = Cursor.Position;
        bool inside = catMenu.TreeContains(p);
        if (down && !lmbWasDown && !inside) { CloseMenu(); lmbWasDown = down; return; }
        lmbWasDown = down;
        if (inside || catMenu.ContainsFocus) menuAwaySince = DateTime.MinValue;
        else if (menuAwaySince == DateTime.MinValue) menuAwaySince = DateTime.Now;
        else if ((DateTime.Now - menuAwaySince).TotalSeconds > 25) CloseMenu();
    }

    static void RaiseDropDownOpening(ToolStripMenuItem mi)
    {
        try
        {
            System.Reflection.MethodInfo m = typeof(ToolStripDropDownItem).GetMethod("OnDropDownShow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (m != null) m.Invoke(mi, new object[] { EventArgs.Empty });
        }
        catch { }
    }

    static void RaiseHostKeyDown(ToolStripControlHost h, KeyEventArgs e)
    {
        try
        {
            System.Reflection.MethodInfo m = typeof(ToolStripControlHost).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new Type[] { typeof(KeyEventArgs) }, null);
            if (m != null) m.Invoke(h, new object[] { e });
        }
        catch { }
    }

    static bool HasSub(ToolStripItem it)
    {
        ToolStripMenuItem mi = it as ToolStripMenuItem;
        return mi != null && mi.DropDownItems.Count > 0;
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    class CatMenu : Form
    {
        class Row
        {
            public ToolStripItem Item; public int Kind;   // 0 пункт, 1 разделитель, 2 заголовок, 3 надпись, 4 поле ввода
            public Rectangle R; public TextBox Box; public string Hint; public string[] Path;
        }

        static readonly Color Bg1 = Color.FromArgb(36, 41, 56), Bg2 = Color.FromArgb(25, 28, 40), Edge = Color.FromArgb(78, 84, 108);
        static readonly Color TextC = Color.FromArgb(234, 237, 244), DimC = Color.FromArgb(140, 147, 168), HiText = Color.FromArgb(255, 216, 176);
        static readonly Color HiBg = Color.FromArgb(70, 54, 40), FieldBg = Color.FromArgb(48, 53, 70);

        readonly VapePet p;
        readonly ToolStripItemCollection items;
        readonly CatMenu parentMenu;
        public CatMenu Child; ToolStripMenuItem childOf;
        List<Row> rows = new List<Row>();
        int hover = -1;
        readonly float s;
        readonly Font fItem, fHead, fLabel, fBox;
        readonly System.Windows.Forms.Timer hoverT = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer fadeT = new System.Windows.Forms.Timer();
        TextBox searchBox; ToolStripTextBox searchHost; string searchText = "";
        Dictionary<ToolStripItem, TextBox> boxes = new Dictionary<ToolStripItem, TextBox>();
        bool roundedByDwm;
        Rectangle anchor; bool anchorIsRow;

        public CatMenu(VapePet owner, ToolStripItemCollection list, CatMenu parent)
        {
            p = owner; items = list; parentMenu = parent;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            KeyPreview = true; DoubleBuffered = true; BackColor = Bg2; AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) s = g.DpiX / 96f;
            fItem = new Font("Segoe UI", 10.5f); fHead = new Font("Segoe UI", 7.6f, FontStyle.Bold);
            fLabel = new Font("Segoe UI", 9f, FontStyle.Bold); fBox = new Font("Segoe UI", 10.5f);
            Font = fItem;
            hoverT.Interval = 130; hoverT.Tick += delegate { hoverT.Stop(); SyncChild(); };
            fadeT.Interval = 15; fadeT.Tick += delegate { Opacity = Math.Min(1.0, Opacity + 0.25); if (Opacity >= 1.0) fadeT.Stop(); };
            BuildRows();
        }

        bool IsRoot { get { return parentMenu == null; } }
        int S(float v) { return (int)Math.Round(v * s); }

        protected override bool ShowWithoutActivation { get { return !IsRoot; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x8;                  // без Alt+Tab, поверх всех
                if (parentMenu != null) cp.ExStyle |= 0x08000000;   // подменю не забирают фокус у поля ввода
                cp.ClassStyle |= 0x20000;                 // тень
                return cp;
            }
        }

        // ---------- строки ----------
        void BuildRows()
        {
            rows.Clear();
            bool resPut = false;
            List<Row> res = new List<Row>();
            if (IsRoot && searchText.Length >= 2) res = SearchRows(searchText);
            foreach (ToolStripItem it in items)
            {
                if (!it.Available) continue;
                Row r = new Row(); r.Item = it;
                if (it is ToolStripSeparator) r.Kind = 1;
                else if (it is ToolStripTextBox) r.Kind = 4;
                else if (it is ToolStripLabel) r.Kind = (it.Tag is string && (string)it.Tag == "header") ? 2 : 3;
                else r.Kind = 0;
                if (r.Kind == 1 && (rows.Count == 0 || rows[rows.Count - 1].Kind == 1)) continue;
                rows.Add(r);
                // результаты поиска — сразу под первым полем ввода
                if (r.Kind == 4 && res.Count > 0 && !resPut)
                {
                    resPut = true;
                    Row h = new Row(); h.Kind = 2; h.Hint = "#found"; rows.Add(h);
                    rows.AddRange(res);
                    Row sp = new Row(); sp.Kind = 1; rows.Add(sp);
                }
            }
            while (rows.Count > 0 && rows[rows.Count - 1].Kind == 1) rows.RemoveAt(rows.Count - 1);
            Layout2();
        }

        List<Row> SearchRows(string q)
        {
            List<Row> found = new List<Row>();
            Collect(p.menu.Items, q.ToLowerInvariant(), new List<string>(), found);
            if (found.Count > 7) found.RemoveRange(7, found.Count - 7);
            return found;
        }

        void Collect(ToolStripItemCollection list, string q, List<string> path, List<Row> found)
        {
            foreach (ToolStripItem it in list)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null || !mi.Available || !mi.Enabled) continue;
                string name = Clean(mi.Text);
                bool sub = mi.DropDownItems.Count > 0;
                if (name.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) >= 0)
                {
                    Row r = new Row(); r.Kind = 0; r.Item = mi; r.Hint = path.Count > 0 ? path[path.Count - 1] : "Меню";
                    if (sub) { List<string> full = new List<string>(path); full.Add(name); r.Path = full.ToArray(); }
                    found.Add(r);
                }
                if (sub) { path.Add(name); Collect(mi.DropDownItems, q, path, found); path.RemoveAt(path.Count - 1); }
            }
        }

        static string Clean(string t) { return (t ?? "").Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&"); }

        int RowH(Row r)
        {
            switch (r.Kind)
            {
                case 1: return S(11);
                case 2: return S(r.Hint == "#found" ? 26 : 27);
                case 3: return S(25);
                case 4: return S(38);
                default: return S(36);
            }
        }

        void Layout2()
        {
            int w = S(250);
            foreach (Row r in rows)
            {
                if (r.Kind == 0)
                {
                    ToolStripMenuItem mi = r.Item as ToolStripMenuItem;
                    int tw = TextRenderer.MeasureText(Clean(r.Item.Text), fItem).Width;
                    int extra = 0;
                    if (mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString)) extra += TextRenderer.MeasureText(mi.ShortcutKeyDisplayString, fHead).Width + S(16);
                    if (r.Hint != null) extra += TextRenderer.MeasureText(r.Hint, fHead).Width + S(14);
                    w = Math.Max(w, S(12 + 30 + 10) + tw + extra + S(46));
                }
                else if (r.Kind == 3) w = Math.Max(w, S(42) + TextRenderer.MeasureText(Clean(r.Item.Text), fLabel).Width + S(16));
                else if (r.Kind == 4) w = Math.Max(w, S(320));
                else if (r.Kind == 2) w = Math.Max(w, S(30) + TextRenderer.MeasureText(HeadText(r), fHead).Width);
            }
            w = Math.Min(w, S(560));
            int y = S(7);
            foreach (Row r in rows) { int h = RowH(r); r.R = new Rectangle(S(5), y, w - S(10), h); y += h; }
            Size = new Size(w, y + S(7));
            foreach (Row r in rows) if (r.Kind == 4) PlaceBox(r);
            ApplyShape();
        }

        string HeadText(Row r) { return r.Hint == "#found" ? "НАЙДЕНО В МЕНЮ" : Clean(r.Item.Text).ToUpperInvariant(); }

        void PlaceBox(Row r)
        {
            ToolStripTextBox host = (ToolStripTextBox)r.Item;
            TextBox had;
            if (boxes.TryGetValue(host, out had)) r.Box = had;
            if (r.Box == null)
            {
                TextBox b = new TextBox();
                b.BorderStyle = BorderStyle.None; b.BackColor = FieldBg; b.ForeColor = Color.White; b.Font = fBox;
                b.Text = host.Text;
                b.KeyDown += delegate (object o, KeyEventArgs e) { BoxKey(r, e); };
                if (searchHost == null || searchHost == host)
                {
                    searchHost = host; searchBox = b;
                    b.TextChanged += delegate { OnSearch(b.Text); };
                }
                r.Box = b; boxes[host] = b; Controls.Add(b);
            }
            int fh = r.Box.PreferredHeight;
            r.Box.SetBounds(r.R.X + S(14), r.R.Y + (r.R.Height - fh) / 2, r.R.Width - S(28), fh);
        }

        void OnSearch(string t)
        {
            string q = t.Trim();
            string was = searchText;
            searchText = q.Length >= 2 ? q : "";
            if (searchText == was) return;
            if (Child != null) CloseChild();
            int caret = searchBox.SelectionStart;
            hover = -1;
            BuildRows();
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            Location = new Point(Left, Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height)));
            if (searchBox != null) { searchBox.Focus(); searchBox.SelectionStart = Math.Min(caret, searchBox.TextLength); }
            Invalidate();
        }

        void BoxKey(Row r, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; p.CloseMenu(); return; }
            if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; Step(1, rows.IndexOf(r)); ActiveControl = null; Focus(); return; }
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            string text = r.Box.Text;
            ToolStripTextBox host = (ToolStripTextBox)r.Item;
            VapePet pp = p;
            pp.CloseMenu();
            host.Text = text;
            pp.BeginInvoke((Action)delegate { RaiseHostKeyDown(host, new KeyEventArgs(Keys.Enter)); });
        }

        // ---------- форма ----------
        void ApplyShape()
        {
            if (!IsHandleCreated) return;
            int pref = 2;   // Windows 11: настоящие скруглённые углы и тень
            roundedByDwm = DwmSetWindowAttribute(Handle, 33, ref pref, 4) == 0;
            if (!roundedByDwm)
            {
                using (GraphicsPath gp = RoundRect(0, 0, Width, Height, S(9))) Region = new Region(gp);
            }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyShape(); }

        public void ShowAt(Rectangle a, bool fromRow)
        {
            anchor = a; anchorIsRow = fromRow;
            Reposition();
            Opacity = 0;
            Show();
            if (IsRoot)
            {
                Activate();
                try { SetForegroundWindow(Handle); } catch { }
                if (searchBox != null) { searchBox.Focus(); searchBox.SelectAll(); }
            }
            fadeT.Start();
        }

        void Reposition()
        {
            Rectangle wa = Screen.FromPoint(new Point(anchor.X, anchor.Y)).WorkingArea;
            int x, y;
            if (anchorIsRow)
            {
                x = anchor.Right - S(3);
                if (x + Width > wa.Right) x = anchor.Left - Width + S(3);
                y = anchor.Top - S(7);
            }
            else
            {
                x = anchor.X; y = anchor.Y;
                if (x + Width > wa.Right) x = anchor.X - Width;
                if (y + Height > wa.Bottom) y = anchor.Y - Height;
            }
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height));
            Location = new Point(x, y);
        }

        public bool TreeContains(Point pt)
        {
            for (CatMenu m = this; m != null; m = m.Child) if (!m.IsDisposed && m.Visible && m.Bounds.Contains(pt)) return true;
            return false;
        }

        public void CloseTree()
        {
            if (Child != null) Child.CloseTree();
            Child = null;
            hoverT.Stop(); fadeT.Stop();
            Close(); Dispose();
        }

        void CloseChild()
        {
            if (Child == null) return;
            Child.CloseTree(); Child = null; childOf = null;
            if (IsRoot && searchBox != null && !searchBox.Focused && ActiveControl == null) { }
        }

        void OpenChild(int idx, bool viaKeys)
        {
            ToolStripMenuItem mi = rows[idx].Item as ToolStripMenuItem;
            if (mi == null) return;
            if (Child != null && childOf == mi) return;
            CloseChild();
            RaiseDropDownOpening(mi);
            if (mi.DropDownItems.Count == 0) return;
            Child = new CatMenu(p, mi.DropDownItems, this); childOf = mi;
            Rectangle rr = rows[idx].R; rr.Offset(Location);
            Child.ShowAt(rr, true);
            if (viaKeys) { Child.Step(1, -1); }
            Invalidate();
        }

        void SyncChild()
        {
            if (hover < 0 || hover >= rows.Count) return;
            Row r = rows[hover];
            if (r.Kind == 0 && r.Hint == null && HasSub(r.Item) && r.Item.Enabled) OpenChild(hover, false);
            else CloseChild();
        }

        bool Selectable(Row r) { return r.Kind == 0 && r.Item.Enabled; }

        int Hit(Point pt)
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i].R.Contains(pt) && Selectable(rows[i])) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Hit(e.Location);
            if (h == hover) return;
            hover = h; Invalidate();
            hoverT.Stop();
            if (h >= 0) hoverT.Start();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverT.Stop();
            if (Child == null && hover >= 0) { hover = -1; Invalidate(); }
            else if (Child != null && childOf != null) { int i = rows.FindIndex(x => x.Item == childOf); if (i != hover) { hover = i; Invalidate(); } }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int h = Hit(e.Location);
            if (h >= 0) ActivateRow(h);
        }

        void ActivateRow(int idx)
        {
            Row r = rows[idx];
            if (r.Hint == null && HasSub(r.Item)) { hoverT.Stop(); OpenChild(idx, false); return; }
            if (r.Path != null) { VapePet q = p; string[] path = r.Path; q.CloseMenu(); q.BeginInvoke((Action)delegate { q.OpenMenuPath(path); }); return; }
            ToolStripItem it = r.Item;
            bool keep = it.Tag is string && (string)it.Tag == "toggle";
            if (keep) { it.PerformClick(); Invalidate(); p.HudChanged(); return; }
            VapePet pp = p;
            pp.CloseMenu();
            pp.BeginInvoke((Action)delegate { try { it.PerformClick(); } catch (Exception ex) { pp.GuardLog("пункт меню", ex); } pp.HudChanged(); });
        }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); }

        public bool OpenByName(string name)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Kind == 0 && rows[i].Hint == null && Clean(rows[i].Item.Text).StartsWith(name) && HasSub(rows[i].Item))
                { hover = i; OpenChild(i, false); return Child != null; }
            return false;
        }

        // ---------- клавиатура ----------
        CatMenu Deepest() { CatMenu m = this; while (m.Child != null && !m.Child.IsDisposed) m = m.Child; return m; }

        void Step(int dir, int from)
        {
            int n = rows.Count, i = from;
            for (int k = 0; k < n; k++)
            {
                i += dir; if (i < 0) i = n - 1; if (i >= n) i = 0;
                if (Selectable(rows[i])) { hover = i; Invalidate(); return; }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            if (ActiveControl is TextBox && e.KeyCode != Keys.Escape) return;
            CatMenu m = Deepest();
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    e.Handled = true; e.SuppressKeyPress = true;
                    if (m == this) p.CloseMenu(); else { m.parentMenu.CloseChild(); }
                    return;
                case Keys.Down: e.Handled = true; m.Step(1, m.hover); return;
                case Keys.Up: e.Handled = true; m.Step(-1, m.hover < 0 ? m.rows.Count : m.hover); return;
                case Keys.Right: e.Handled = true; if (m.hover >= 0 && m.rows[m.hover].Hint == null && HasSub(m.rows[m.hover].Item)) m.OpenChild(m.hover, true); return;
                case Keys.Left: e.Handled = true; if (m != this) m.parentMenu.CloseChild(); return;
                case Keys.Enter: e.Handled = true; e.SuppressKeyPress = true; if (m.hover >= 0) { if (m.rows[m.hover].Hint == null && HasSub(m.rows[m.hover].Item)) m.OpenChild(m.hover, true); else m.ActivateRow(m.hover); } return;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            // начали печатать, когда курсор не в поле — переводим в поле «Спроси котика»
            if (IsRoot && searchBox != null && !searchBox.Focused && !char.IsControl(e.KeyChar))
            {
                CloseChild();
                searchBox.Focus(); searchBox.AppendText(e.KeyChar.ToString()); e.Handled = true;
            }
        }

        // ---------- рисование ----------
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Bg1, Bg2, 90f)) g.FillRectangle(b, ClientRectangle);
            using (Pen sh = new Pen(Color.FromArgb(22, 255, 255, 255), 1)) g.DrawLine(sh, S(12), 1, Width - S(12), 1);
            if (!roundedByDwm) using (GraphicsPath gp = RoundRect(0.5f, 0.5f, Width - 1.5f, Height - 1.5f, S(9))) using (Pen pen = new Pen(Edge, 1)) g.DrawPath(pen, gp);
            for (int i = 0; i < rows.Count; i++) DrawRow(g, rows[i], i == hover || (Child != null && childOf != null && rows[i].Item == childOf && rows[i].Hint == null));
        }

        void DrawRow(Graphics g, Row r, bool hot)
        {
            Rectangle R = r.R;
            switch (r.Kind)
            {
                case 1:
                    using (Pen pen = new Pen(Color.FromArgb(54, 59, 78))) g.DrawLine(pen, R.X + S(10), R.Y + R.Height / 2, R.Right - S(10), R.Y + R.Height / 2);
                    return;
                case 2:
                    TextRenderer.DrawText(g, HeadText(r), fHead, new Rectangle(R.X + S(12), R.Y + S(8), R.Width - S(16), R.Height - S(8)), r.Hint == "#found" ? MenuAc : DimC, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
                    return;
                case 3:
                    {
                        int ix = R.X + S(10);
                        if (r.Item.Image != null) { DrawIcon(g, r.Item.Image, new Rectangle(ix, R.Y + (R.Height - S(18)) / 2, S(18), S(18))); }
                        Color c = r.Item.ForeColor.A > 0 ? r.Item.ForeColor : TextC;
                        TextRenderer.DrawText(g, Clean(r.Item.Text), fLabel, new Rectangle(ix + S(24), R.Y, R.Width - S(40), R.Height), c, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                        return;
                    }
                case 4:
                    {
                        Rectangle f = new Rectangle(R.X + S(6), R.Y + S(4), R.Width - S(12), R.Height - S(8));
                        bool focus = r.Box != null && r.Box.Focused;
                        using (GraphicsPath gp = RoundRect(f.X, f.Y, f.Width, f.Height, S(7)))
                        {
                            using (SolidBrush b = new SolidBrush(FieldBg)) g.FillPath(b, gp);
                            using (Pen pen = new Pen(focus ? MenuAc : Color.FromArgb(78, 86, 110), focus ? 1.6f : 1f)) g.DrawPath(pen, gp);
                        }
                        return;
                    }
            }
            ToolStripMenuItem mi = r.Item as ToolStripMenuItem;
            bool en = r.Item.Enabled;
            if (hot && en)
            {
                using (GraphicsPath gp = RoundRect(R.X, R.Y + 1, R.Width, R.Height - 2, S(7)))
                using (LinearGradientBrush b = new LinearGradientBrush(R, Color.FromArgb(84, 62, 42), HiBg, 0f)) g.FillPath(b, gp);
                using (GraphicsPath ap = RoundRect(R.X + S(2), R.Y + S(9), S(3), R.Height - S(18), S(1.5f))) using (SolidBrush b = new SolidBrush(MenuAc)) g.FillPath(b, ap);
            }
            bool toggle = r.Item.Tag is string && (string)r.Item.Tag == "toggle";
            int x = R.X + S(12);
            Rectangle ico = new Rectangle(x, R.Y + (R.Height - S(22)) / 2, S(22), S(22));
            if (r.Item.Image != null) DrawIcon(g, r.Item.Image, toggle ? new Rectangle(ico.X - S(3), ico.Y - S(3), S(28), S(28)) : ico);
            else if (mi != null && mi.Checked) DrawCheck(g, ico);
            x += S(32);
            Color tc = !en ? Color.FromArgb(110, 116, 136) : hot ? HiText : TextC;
            int right = R.Right - S(12);
            if (r.Hint == null && HasSub(r.Item))
            {
                int cx = R.Right - S(16), cy = R.Y + R.Height / 2;
                using (Pen pen = new Pen(hot ? MenuAc : Color.FromArgb(130, 136, 158), 1.8f * s)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLines(pen, new Point[] { new Point(cx - S(3), cy - S(5)), new Point(cx + S(2), cy), new Point(cx - S(3), cy + S(5)) }); }
                right -= S(18);
            }
            else if (mi != null && mi.Checked && r.Item.Image != null && !toggle)
            {
                DrawCheck(g, new Rectangle(R.Right - S(30), R.Y + (R.Height - S(18)) / 2, S(18), S(18)));
                right -= S(24);
            }
            string tail = r.Hint ?? (mi != null ? mi.ShortcutKeyDisplayString : null);
            if (!string.IsNullOrEmpty(tail))
            {
                Size ts = TextRenderer.MeasureText(tail, fHead);
                TextRenderer.DrawText(g, tail, fHead, new Rectangle(right - ts.Width, R.Y, ts.Width, R.Height), DimC, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                right -= ts.Width + S(10);
            }
            TextRenderer.DrawText(g, Clean(r.Item.Text), fItem, new Rectangle(x, R.Y, right - x, R.Height), tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        void DrawIcon(Graphics g, Image img, Rectangle r)
        {
            InterpolationMode im = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(img, r);
            g.InterpolationMode = im;
        }

        void DrawCheck(Graphics g, Rectangle r)
        {
            using (Pen pen = new Pen(MenuAc, 2.4f * s)) { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new PointF[] { new PointF(r.X + r.Width * 0.2f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.74f), new PointF(r.X + r.Width * 0.82f, r.Y + r.Height * 0.28f) }); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { hoverT.Dispose(); fadeT.Dispose(); fItem.Dispose(); fHead.Dispose(); fLabel.Dispose(); fBox.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // открыть меню сразу на нужном подменю: OpenMenuPath("Инструменты", "Таймер")
    void OpenMenuPath(params string[] path)
    {
        Point at = Cursor.Position;
        ShowMenu(new Point(at.X - 20, Math.Max(0, at.Y - 40)));
        CatMenu m = catMenu;
        foreach (string name in path)
        {
            if (m == null) return;
            if (!m.OpenByName(name)) return;
            m = m.Child;
        }
    }
}

// точка входа для VapePet.exe (при запуске из PowerShell не используется)
static class VapePetProgram
{
    [STAThread]
    static void Main() { VapePet.Start(); }
}
