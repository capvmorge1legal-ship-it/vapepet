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

    Rectangle ScreenWork { get { return RoamFake ? new Rectangle(0, 0, 1920, 1040) : PetScreen.WorkingArea; } }
    public int FakeShift { get { return RoamFake ? PetScreen.WorkingArea.Bottom - 1040 : 0; } }
    Rectangle ScreenFull { get { return PetScreen.Bounds; } }
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
}
