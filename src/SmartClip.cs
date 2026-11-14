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
}
