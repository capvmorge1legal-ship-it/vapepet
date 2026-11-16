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
    // ================= не мешать: полноэкранные игры/видео и экономия кадров =================
    bool hideInFullscreen = true, fsHidden; int fsCheckT, calmT;
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();

    // котик спокоен: ничего не летит, не идёт, не говорит — достаточно 10 кадров в секунду
    bool CalmNow()
    {
        if (mode != 0 || pendingMode >= 0 || roaming || chasing || dragging) return false;
        if (parts.Count > 0 || flyers.Count > 0 || texts.Count > 0 || guests.Count > 0) return false;
        if (sleeping) return true;
        return !me.Walking && !me.Jumping && me.PuffT == 0 && me.Shake == 0 && me.SweatT == 0 && typingT == 0 && shakeT == 0;
    }

    bool ForegroundFullscreen()
    {
        try
        {
            int st;
            if (SHQueryUserNotificationState(out st) == 0 && (st == 3 || st == 4)) return true;   // игра D3D на весь экран / режим презентации
            IntPtr fw = GetForegroundWindow();
            if (fw == IntPtr.Zero || fw == GetShellWindow() || fw == GetDesktopWindow()) return false;
            uint pid; GetWindowThreadProcessId(fw, out pid);
            if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return false;
            StringBuilder cls = new StringBuilder(64); GetClassName(fw, cls, 64);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Windows.UI.Core.CoreWindow") return false;
            RECT r; if (!GetWindowRect(fw, out r)) return false;
            Rectangle sb = Screen.FromHandle(Handle).Bounds;
            return r.L <= sb.Left && r.T <= sb.Top && r.R >= sb.Right && r.B >= sb.Bottom;
        }
        catch { return false; }
    }

    void FullscreenCheck()
    {
        bool fs = hideInFullscreen && !MenuOpen && ForegroundFullscreen();
        if (fs == fsHidden) return;
        fsHidden = fs;
        if (fs)
        {
            try { if (roaming) EndRoam(); } catch { }
            try { if (chasing) EndChase(); } catch { }
            ShowWindow(Handle, 0);
        }
        else { ShowWindow(Handle, 4); hudDirty = true; RenderFrame(); }
    }

    // ================= обновления с GitHub =================
    public const string AppVersion = "3.2.0";
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
            else if (savedVer.Length == 0 || keysPlain) SaveSettings();   // заодно зашифровать ключи, сохранённые старой версией
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
}
