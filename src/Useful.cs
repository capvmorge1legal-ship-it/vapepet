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
        float x = HudX(w), y = (WxVisible ? WidgetRect().Bottom : 0) + W(2);
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
}
