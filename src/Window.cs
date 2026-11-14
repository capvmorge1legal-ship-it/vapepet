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
                bmp.CopyTo(dibBits);
                ok = Present(dibDc, dibW, dibH);
            }
            catch (Exception ex) { GuardLog("dib", ex); dibBroken = true; FreeDib(); }
        }
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
            Rectangle wa = PetScreen.WorkingArea;
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

    bool FrameEmpty(Bitmap b) { try { return b.IsBlank(); } catch { return false; } }

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
}
