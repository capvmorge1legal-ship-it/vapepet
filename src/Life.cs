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
using Graphics = Gdi.Graphics;
using Bitmap = Gdi.Bitmap;
using Brush = Gdi.Brush;
using SolidBrush = Gdi.SolidBrush;
using LinearGradientBrush = Gdi.LinearGradientBrush;
using PathGradientBrush = Gdi.PathGradientBrush;
using Brushes = Gdi.Brushes;
using Pen = Gdi.Pen;
using Pens = Gdi.Pens;
using GraphicsPath = Gdi.GraphicsPath;
using Font = Gdi.Font;
using StringFormat = Gdi.StringFormat;
using GraphicsState = Gdi.GraphicsState;
using ImageAttributes = Gdi.ImageAttributes;
using ColorBlend = Gdi.ColorBlend;

partial class VapePet
{
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
            bmp.CopyTo(bits);
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
}
