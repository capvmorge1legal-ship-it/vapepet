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
}
