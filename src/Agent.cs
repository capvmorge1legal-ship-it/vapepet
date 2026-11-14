using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// ================= котик делает дела: действия по просьбе и память =================
// ИИ отвечает обычным текстом, а когда нужно что-то сделать — добавляет строку «@@{"do":"timer","minutes":10}».
// Котик выполняет действие и, если у действия есть результат (поиск, файлы, диск…), возвращает его ИИ для ответа.
// Протокол текстовый, поэтому работает с любым подключённым ИИ (Groq, Gemini, OpenAI-совместимые, Anthropic).
partial class VapePet
{
    public bool aiActions = true;
    static string MemoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_memory.txt"); } }
    static string AlarmsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_alarms.txt"); } }
    List<string> memory = new List<string>();
    bool agentReady; int agentTick; string alarmsSaved = "";
    static readonly bool devMode = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_dev.flag"));
    void DevLog(string q, string ans, string err)
    {
        if (!devMode) return;
        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_answer.txt"), "Q: " + q + "\r\nA: " + (ans ?? ("ОШИБКА " + err)) + "\r\n\r\n", Encoding.UTF8); } catch { }
    }

    // ---------- память ----------
    void LoadMemory()
    {
        memory.Clear();
        try { if (File.Exists(MemoryPath)) foreach (string l in File.ReadAllLines(MemoryPath, Encoding.UTF8)) if (l.Trim().Length > 0) memory.Add(l.Trim()); } catch { }
    }
    void SaveMemory() { try { File.WriteAllLines(MemoryPath, memory.ToArray(), Encoding.UTF8); } catch (Exception ex) { GuardLog("память", ex); } }

    public void EditMemory()
    {
        LoadMemory();
        using (Form f = new Form())
        {
            f.Text = "Память котика"; f.StartPosition = FormStartPosition.CenterScreen; f.TopMost = true; f.ClientSize = new Size(520, 380);
            f.MinimizeBox = false; f.ShowInTaskbar = false; f.Font = new Font("Segoe UI", 10f);
            Label l = new Label(); l.Text = "Что котик помнит о вас — по одному факту в строке. Можно править и удалять.\r\nХранится только на этом компьютере."; l.SetBounds(12, 10, 496, 40); f.Controls.Add(l);
            TextBox t = new TextBox(); t.Multiline = true; t.ScrollBars = ScrollBars.Vertical; t.AcceptsReturn = true; t.SetBounds(12, 54, 496, 270);
            t.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; t.Text = string.Join("\r\n", memory.ToArray()); f.Controls.Add(t);
            Button ok = new Button(); ok.Text = "Сохранить"; ok.SetBounds(398, 336, 110, 32); ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; ok.DialogResult = DialogResult.OK; f.Controls.Add(ok);
            t.SelectionStart = t.TextLength;
            if (f.ShowDialog() != DialogResult.OK) return;
            memory.Clear();
            foreach (string s in t.Text.Replace("\r", "").Split('\n')) if (s.Trim().Length > 0) memory.Add(s.Trim());
            SaveMemory();
        }
    }

    // ---------- напоминания переживают перезапуск ----------
    string AlarmsText()
    {
        StringBuilder b = new StringBuilder();
        foreach (Alarm a in alarms) b.Append(a.At.Ticks).Append('|').Append(a.Timer ? 1 : 0).Append('|').Append(a.Minutes).Append('|').Append((a.Text ?? "").Replace("\r", " ").Replace("\n", " ")).Append('\n');
        return b.ToString();
    }
    void LoadAlarms()
    {
        try
        {
            if (!File.Exists(AlarmsPath)) return;
            foreach (string l in File.ReadAllLines(AlarmsPath, Encoding.UTF8))
            {
                string[] p = l.Split(new char[] { '|' }, 4); long ticks; int tm, mins;
                if (p.Length < 4 || !long.TryParse(p[0], out ticks) || !int.TryParse(p[1], out tm) || !int.TryParse(p[2], out mins)) continue;
                Alarm a = new Alarm(); a.At = new DateTime(ticks); a.Timer = tm == 1; a.Minutes = mins; a.Text = p[3];
                if (a.At < DateTime.Now.AddHours(-12)) continue;              // совсем старое не будим
                if (a.At < DateTime.Now) { if (a.Timer) continue; a.Text = "Пропущено в " + a.At.ToString("HH:mm") + ": " + a.Text; a.At = DateTime.Now.AddSeconds(20); }
                alarms.Add(a);
            }
            alarmsSaved = AlarmsText();
        }
        catch (Exception ex) { GuardLog("напоминания", ex); }
    }

    // вызывается из главного цикла 30 раз в секунду
    void AgentTick()
    {
        if (!agentReady) { agentReady = true; LoadMemory(); LoadAlarms(); }
        if (++agentTick % 90 != 0) return;
        if (devMode)
        {
            // проверка без рук: вопрос из файла VapePet_ask.txt, ответ дописывается в VapePet_answer.txt
            string ask = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_ask.txt");
            try { if (File.Exists(ask) && !aiBusy) { string q = File.ReadAllText(ask, Encoding.UTF8).Trim(); File.Delete(ask); if (q.Length > 0) AskAI(q, false); } } catch { }
        }
        string now = AlarmsText();
        if (now == alarmsSaved) return;
        alarmsSaved = now;
        try { File.WriteAllText(AlarmsPath, now, Encoding.UTF8); } catch { }
    }

    // ---------- что умеет котик (описание для ИИ) ----------
    string AgentSystem()
    {
        CultureInfo ru = new CultureInfo("ru-RU");
        StringBuilder b = new StringBuilder(AiSystem);
        b.Append("\n\nСейчас ").Append(DateTime.Now.ToString("dddd, d MMMM yyyy, HH:mm", ru)).Append(". Город: ").Append(wxCity).Append(".");
        b.Append("\n\nТЫ УМЕЕШЬ ВЫПОЛНЯТЬ ДЕЙСТВИЯ на компьютере владельца. Чтобы выполнить действие, напиши ОТДЕЛЬНОЙ строкой @@ и JSON, например:\n@@{\"do\":\"timer\",\"minutes\":10,\"text\":\"Чай\"}\n");
        b.Append("Действия, после которых результат тебе не нужен (перед строкой @@ напиши короткий ответ владельцу — второй раз тебя не спросят):\n");
        b.Append("timer {minutes, text} — таймер на N минут.\n");
        b.Append("remind {at, text} — напоминание; at в виде \"ЧЧ:ММ\" (сегодня, а если время прошло — завтра) или \"ГГГГ-ММ-ДД ЧЧ:ММ\".\n");
        b.Append("cancel_timers {} — отменить все таймеры и напоминания.\n");
        b.Append("open {target} — открыть сайт (https://…), файл, папку или сохранённую ссылку по названию");
        if (links.Count > 0) { b.Append(" (сохранены: "); for (int i = 0; i < links.Count && i < 12; i++) b.Append(i > 0 ? ", " : "").Append(links[i][0]); b.Append(")"); }
        b.Append(".\n");
        b.Append("stock {query} — показать остатки товара в МойСклад.\n");
        b.Append("scene {name} — сценка котика: walk, shop, sofa, warehouse, party, raid, audit.\n");
        b.Append("clipboard_set {text} — положить текст в буфер обмена.\n");
        b.Append("media {cmd} — play_pause, next, prev, volume_up, volume_down, mute.\n");
        b.Append("lock {} — заблокировать компьютер. shutdown {minutes} — выключить компьютер через N минут (спросит подтверждение). shutdown_cancel {} — отменить выключение.\n");
        b.Append("remember {text} — запомнить факт о владельце надолго (имя, привычки, важные дела, предпочтения). forget {text} — забыть факты, где встречается этот текст.\n");
        b.Append("Действия с результатом (напиши ТОЛЬКО строку @@, без другого текста; котик пришлёт результат, и тогда ты ответишь):\n");
        b.Append("web {query} — поиск в интернете. Используй для фактов, новостей, цен, дат, всего, что могло измениться.\n");
        b.Append("find_file {name} — найти файл или папку по части имени (Рабочий стол, Документы, Загрузки).\n");
        b.Append("disk {} — свободное место на дисках. weather {} — погода сейчас. rates {} — курсы валют ЦБ.\n");
        b.Append("list_timers {} — активные таймеры и напоминания. clipboard_get {} — что сейчас в буфере обмена.\n");
        b.Append("Правила: выполняй действие только когда владелец об этом просит или это явно нужно для ответа. Не выдумывай результат действия — дождись его. ");
        b.Append("Можно несколько строк @@ подряд. Если ничего делать не нужно — просто ответь текстом. Про программы, которые ты не можешь запустить, честно скажи.");
        if (memory.Count > 0)
        {
            b.Append("\n\nЧТО ТЫ ПОМНИШЬ О ВЛАДЕЛЬЦЕ (используй, когда это к месту, не пересказывай без повода):\n");
            int from = Math.Max(0, memory.Count - 60);
            for (int i = from; i < memory.Count; i++) b.Append("- ").Append(memory[i]).Append("\n");
        }
        return b.ToString();
    }

    // ---------- запрос к ИИ с произвольной ролью и историей; текст возвращается как есть ----------
    string CallAIMessages(string sys, List<string[]> msgs, int maxTokens)
    {
        string fmt = aiFormat, url = aiUrl, model = aiModel, key = aiKey;
        if (key.Length == 0) throw new Exception("нет ключа ИИ");
        bool groq = url.Contains("api.groq.com"), gem = url.Contains("generativelanguage.googleapis.com");
        StringBuilder b = new StringBuilder();
        if (fmt == "anthropic")
        {
            b.Append("{\"model\":\"").Append(JEsc(model)).Append("\",\"max_tokens\":").Append(maxTokens).Append(",\"system\":\"").Append(JEsc(sys)).Append("\",\"messages\":[");
            for (int i = 0; i < msgs.Count; i++) b.Append(i > 0 ? "," : "").Append("{\"role\":\"").Append(msgs[i][0]).Append("\",\"content\":\"").Append(JEsc(msgs[i][1])).Append("\"}");
            b.Append("]}");
        }
        else
        {
            b.Append("{\"model\":\"").Append(JEsc(model)).Append("\",\"max_tokens\":").Append(groq || gem ? Math.Max(maxTokens, 2000) : maxTokens).Append(",");
            if (gem && model.StartsWith("gemini-2.5")) b.Append("\"reasoning_effort\":\"none\",");
            if (groq && model.Contains("gpt-oss")) b.Append("\"reasoning_effort\":\"low\",");
            if (groq && model.Contains("qwen")) b.Append("\"reasoning_effort\":\"none\",");
            b.Append("\"messages\":[{\"role\":\"system\",\"content\":\"").Append(JEsc(sys)).Append("\"}");
            foreach (string[] m in msgs) b.Append(",{\"role\":\"").Append(m[0]).Append("\",\"content\":\"").Append(JEsc(m[1])).Append("\"}");
            b.Append("]}");
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
                return text;
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

    // ---------- цикл: ответ ИИ → действия → результаты → ответ ----------
    static readonly Regex ActionLine = new Regex(@"@@\s*(\{.*\})\s*`*\s*$", RegexOptions.Multiline);
    static readonly HashSet<string> InfoActions = new HashSet<string> { "web", "find_file", "disk", "weather", "rates", "list_timers", "clipboard_get" };

    // выполняется в фоновом потоке; возвращает готовый текст ответа
    string AgentRun(List<string[]> hist, string q, bool withCard)
    {
        string sys = null;
        Invoke((Action)delegate { if (!agentReady) { agentReady = true; LoadMemory(); LoadAlarms(); } sys = AgentSystem(); });
        List<string[]> msgs = new List<string[]>(hist);
        string said = "", lastResults = "";
        for (int step = 0; step < 4; step++)
        {
            string raw = CallAIMessages(sys, msgs, 900);
            DevLog("  [шаг " + step + "]", raw, null);
            MatchCollection acts = ActionLine.Matches(raw);
            string text = ActionLine.Replace(raw, "").Replace("```json", "").Replace("```", "").Trim();
            if (acts.Count == 0)
            {
                if (text.Length == 0 && lastResults.Length > 0) return CleanText(lastResults);   // ИИ промолчал — отдаём результаты как есть
                return CleanText(text.Length > 0 ? text : raw);
            }
            if (text.Length > 0) said = text;
            bool needAnswer = false; StringBuilder results = new StringBuilder();
            foreach (Match m in acts)
            {
                string name = "?", res;
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(m.Groups[1].Value))
                    {
                        JsonElement e = doc.RootElement;
                        name = Str(e, "do");
                        string label = ActionLabel(name, e);
                        if (label != null) try { BeginInvoke((Action)delegate { Say(label, me.X, CatTop(me) - U(1), 'U'); if (withCard && card != null && card.Visible) ShowCard(q, label, false); }); } catch { }
                        res = DoAction(name, e);
                    }
                }
                catch (JsonException) { res = "ошибка: непонятный JSON в действии"; }
                catch (Exception ex) { res = "ошибка: " + ex.Message; }
                if (InfoActions.Contains(name) || res.StartsWith("ошибка")) needAnswer = true;
                results.Append("[").Append(name).Append("] ").Append(res).Append("\n");
            }
            lastResults = results.ToString();
            DevLog("  [результаты]", lastResults, null);
            if (!needAnswer && said.Length > 0) return CleanText(said);
            msgs.Add(new string[] { "assistant", raw });
            msgs.Add(new string[] { "user", "[Результаты действий — это сообщение прислал котик, а не владелец]\n" + results + "\nТеперь ответь владельцу коротко, по-русски, опираясь на эти результаты. Если искал в интернете — в конце укажи сайт-источник." });
        }
        return CleanText(said.Length > 0 ? said : "Мяу, запутался в делах. Попробуй сказать проще.");
    }

    static string Str(JsonElement e, string key)
    {
        JsonElement v;
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out v)) return "";
        return v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : v.ValueKind == JsonValueKind.Null ? "" : v.ToString();
    }
    static double Num(JsonElement e, string key, double def)
    {
        JsonElement v; double d;
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out v)) return def;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out d)) return d;
        if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
        return def;
    }

    static string ActionLabel(string name, JsonElement e)
    {
        switch (name)
        {
            case "web": return "Ищу в интернете: " + Str(e, "query") + "…";
            case "find_file": return "Ищу файл…";
            case "open": return "Открываю…";
            default: return null;
        }
    }

    // действия выполняются в потоке окна (кроме долгих: поиск в сети и по файлам)
    string DoAction(string name, JsonElement e)
    {
        if (name == "web")
        {
            string query = Str(e, "query"); if (query.Length < 2) return "ошибка: пустой запрос";
            string r = WebSearch(query);
            return r ?? "ничего не нашлось";
        }
        if (name == "find_file") return FindFiles(Str(e, "name"));
        string result = "ошибка: неизвестное действие " + name;
        Invoke((Action)delegate { try { result = DoActionUi(name, e); } catch (Exception ex) { result = "ошибка: " + ex.Message; } });
        return result;
    }

    string DoActionUi(string name, JsonElement e)
    {
        switch (name)
        {
            case "timer":
                {
                    int mins = (int)Math.Round(Num(e, "minutes", 0)); string text = Str(e, "text");
                    if (mins < 1 || mins > 24 * 60) return "ошибка: минут должно быть от 1 до 1440";
                    AddTimer(mins);
                    if (text.Length > 0) alarms[alarms.Count - 1].Text = text + " (" + mins + " мин прошло)";
                    HudChanged(); return "таймер на " + mins + " мин поставлен";
                }
            case "remind":
                {
                    string at = Str(e, "at"), text = Str(e, "text"); DateTime when;
                    if (!ParseWhen(at, out when)) return "ошибка: не понял время «" + at + "», нужно ЧЧ:ММ или ГГГГ-ММ-ДД ЧЧ:ММ";
                    if (when < DateTime.Now) return "ошибка: это время уже прошло";
                    Alarm a = new Alarm(); a.At = when; a.Text = text.Length > 0 ? text : "Напоминание!"; alarms.Add(a);
                    HudChanged(); return "напомню " + when.ToString("d MMMM в HH:mm", new CultureInfo("ru-RU"));
                }
            case "cancel_timers": alarms.Clear(); alarmT = 0; alarmText = null; HudChanged(); return "все таймеры и напоминания отменены";
            case "list_timers":
                {
                    if (alarms.Count == 0) return "нет активных таймеров и напоминаний";
                    StringBuilder b = new StringBuilder();
                    foreach (Alarm a in alarms) b.Append(a.Timer ? "таймер до " : "напоминание на ").Append(a.At.ToString("dd.MM HH:mm")).Append(": ").Append(a.Text).Append("; ");
                    return b.ToString();
                }
            case "open": return OpenTarget(Str(e, "target"));
            case "stock":
                {
                    string query = Str(e, "query"); if (query.Length < 2) return "ошибка: пустой запрос";
                    if (MsToken().Length == 0) return "ошибка: не задан токен МойСклад (Настройки → Вид → Токен МойСклад)";
                    StockSearch(query); return "окно с остатками показано владельцу";
                }
            case "scene":
                {
                    string[] names = { "walk", "shop", "sofa", "warehouse", "party", "raid", "audit" };
                    int i = Array.IndexOf(names, Str(e, "name").ToLowerInvariant());
                    if (i < 0) return "ошибка: такой сценки нет";
                    SetMode(i); return "сценка запущена";
                }
            case "clipboard_set":
                {
                    string text = Str(e, "text"); if (text.Length == 0) return "ошибка: пустой текст";
                    Clipboard.SetText(text); return "текст в буфере обмена";
                }
            case "clipboard_get":
                {
                    string t = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                    if (t.Length == 0) return "в буфере обмена нет текста";
                    return "в буфере обмена: " + (t.Length > 3000 ? t.Substring(0, 3000) + "…" : t);
                }
            case "media":
                {
                    byte vk;
                    switch (Str(e, "cmd"))
                    {
                        case "play_pause": vk = 0xB3; break;
                        case "next": vk = 0xB0; break;
                        case "prev": vk = 0xB1; break;
                        case "volume_up": vk = 0xAF; break;
                        case "volume_down": vk = 0xAE; break;
                        case "mute": vk = 0xAD; break;
                        default: return "ошибка: неизвестная команда";
                    }
                    int times = vk == 0xAF || vk == 0xAE ? 5 : 1;   // громкость — сразу на 10%
                    for (int i = 0; i < times; i++) { keybd_event(vk, 0, 0, UIntPtr.Zero); keybd_event(vk, 0, 2, UIntPtr.Zero); }
                    return "сделано";
                }
            case "lock": LockWorkStation(); return "компьютер заблокирован";
            case "shutdown":
                {
                    int mins = (int)Math.Round(Num(e, "minutes", 1)); if (mins < 1) mins = 1; if (mins > 600) return "ошибка: слишком большой срок";
                    if (!Confirm("Выключить компьютер через " + mins + " мин?\n\nОтменить потом можно, сказав котику «отмени выключение».")) return "владелец отказался";
                    RunHidden("shutdown.exe", "/s /t " + (mins * 60)); return "компьютер выключится через " + mins + " мин";
                }
            case "shutdown_cancel": RunHidden("shutdown.exe", "/a"); return "выключение отменено";
            case "remember":
                {
                    string text = Str(e, "text").Trim(); if (text.Length < 3) return "ошибка: нечего запоминать";
                    if (text.Length > 300) text = text.Substring(0, 300);
                    foreach (string m in memory) if (string.Equals(m, text, StringComparison.OrdinalIgnoreCase)) return "это уже записано";
                    memory.Add(text); while (memory.Count > 200) memory.RemoveAt(0);
                    SaveMemory(); return "запомнил";
                }
            case "forget":
                {
                    string text = Str(e, "text").Trim(); if (text.Length < 2) return "ошибка: что забыть?";
                    int n = memory.RemoveAll(delegate (string m) { return m.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0; });
                    if (n > 0) SaveMemory();
                    return n > 0 ? "забыл записей: " + n : "такого в памяти не было";
                }
            case "disk":
                {
                    StringBuilder b = new StringBuilder();
                    foreach (DriveInfo d in DriveInfo.GetDrives())
                        try { if (d.IsReady && d.DriveType == DriveType.Fixed) b.Append(d.Name).Append(" свободно ").Append((d.AvailableFreeSpace / 1073741824.0).ToString("0.#")).Append(" ГБ из ").Append((d.TotalSize / 1073741824.0).ToString("0")).Append(" ГБ; "); } catch { }
                    return b.Length > 0 ? b.ToString() : "не удалось узнать";
                }
            case "weather":
                if (!wxOk) return "погода ещё не загрузилась";
                return wxCity + ": " + Math.Round(wxTemp) + "°C, ощущается как " + Math.Round(wxFeels) + "°C, " + WxText(wxCode).ToLowerInvariant() + ", ветер " + wxWind.ToString("0") + " м/с";
            case "rates": return rateLine ?? "курс ещё не загрузился";
        }
        return "ошибка: неизвестное действие " + name;
    }

    static bool ParseWhen(string s, out DateTime when)
    {
        s = (s ?? "").Trim(); when = DateTime.MinValue;
        Match m = Regex.Match(s, @"^(\d{1,2})[:.](\d{2})$");
        if (m.Success)
        {
            int h = int.Parse(m.Groups[1].Value), mi = int.Parse(m.Groups[2].Value);
            if (h > 23 || mi > 59) return false;
            when = DateTime.Today.AddHours(h).AddMinutes(mi);
            if (when < DateTime.Now) when = when.AddDays(1);
            return true;
        }
        string[] fmts = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy H:mm" };
        return DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out when);
    }

    bool Confirm(string text)
    {
        using (Form owner = new Form())
        {
            owner.TopMost = true; owner.ShowInTaskbar = false; owner.StartPosition = FormStartPosition.CenterScreen; owner.Opacity = 0; owner.Size = new Size(1, 1); owner.FormBorderStyle = FormBorderStyle.None;
            owner.Show();
            return MessageBox.Show(owner, text, "Котик спрашивает", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
    }

    static void RunHidden(string exe, string args)
    {
        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exe, args);
        psi.CreateNoWindow = true; psi.UseShellExecute = false;
        System.Diagnostics.Process.Start(psi);
    }

    static readonly string[] RunnableExt = { ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".com", ".scr", ".lnk", ".reg", ".hta", ".wsf", ".jar", ".py" };

    string OpenTarget(string target)
    {
        target = (target ?? "").Trim().Trim('"');
        if (target.Length == 0) return "ошибка: не сказано, что открыть";
        // сохранённая ссылка по названию
        foreach (string[] l in links)
            if (string.Equals(l[0], target, StringComparison.OrdinalIgnoreCase) || (target.Length >= 3 && l[0].IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)) { target = l[1]; break; }
        if (Regex.IsMatch(target, @"^https?://", RegexOptions.IgnoreCase)) { ShellOpen(target); return "открыл " + target; }
        if (Regex.IsMatch(target, @"^[\w-]+(\.[\w-]+)+(/\S*)?$") && !File.Exists(target)) { ShellOpen("https://" + target); return "открыл https://" + target; }
        target = Environment.ExpandEnvironmentVariables(target);
        if (Directory.Exists(target)) { ShellOpen(target); return "открыл папку"; }
        if (File.Exists(target))
        {
            string ext = Path.GetExtension(target).ToLowerInvariant();
            if (Array.IndexOf(RunnableExt, ext) >= 0 && !Confirm("Котик хочет запустить программу:\n\n" + target + "\n\nЗапустить?")) return "владелец не разрешил запуск";
            ShellOpen(target); return "открыл " + Path.GetFileName(target);
        }
        return "ошибка: не нашёл «" + target + "». Это не сайт, не файл и не сохранённая ссылка";
    }

    static string FindFiles(string name)
    {
        name = (name ?? "").Trim(); if (name.Length < 2) return "ошибка: слишком короткое имя";
        List<string> found = new List<string>();
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots = { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Path.Combine(home, "Downloads") };
        foreach (string root in roots) Walk(root, name, 0, found, sw);
        if (found.Count == 0) return "ничего не нашлось (искал на Рабочем столе, в Документах и Загрузках)";
        return "нашлось: " + string.Join("; ", found.ToArray());
    }

    static void Walk(string dir, string name, int depth, List<string> found, System.Diagnostics.Stopwatch sw)
    {
        if (found.Count >= 8 || depth > 4 || sw.ElapsedMilliseconds > 6000) return;
        try
        {
            foreach (string f in Directory.EnumerateFileSystemEntries(dir))
            {
                if (found.Count >= 8) return;
                string n = Path.GetFileName(f);
                if (n.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) found.Add(f);
            }
            foreach (string d in Directory.EnumerateDirectories(dir))
            {
                string n = Path.GetFileName(d);
                if (n.StartsWith(".") || n.StartsWith("_") || n == "node_modules") continue;
                Walk(d, name, depth + 1, found, sw);
            }
        }
        catch { }
    }

    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool LockWorkStation();
}
