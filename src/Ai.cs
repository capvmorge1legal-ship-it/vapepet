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
            Rectangle wa = PetScreen.WorkingArea;
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
        Rectangle wa = PetScreen.WorkingArea;
        int cx = Left + (int)me.X;
        int x = Math.Max(wa.Left + 8, Math.Min(wa.Right - w - 8, cx - w / 2));
        int y = Math.Max(wa.Top + 8, Top - h - 6);
        card.Bounds = new Rectangle(x, y, w, h);
        if (!card.Visible) card.Show();
        card.Invalidate();
        cardTimer.Stop();
        if (done) { cardTimer.Interval = Math.Min(60000, 8000 + a.Length * 60); cardTimer.Start(); }
    }
}
