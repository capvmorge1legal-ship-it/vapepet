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
}
