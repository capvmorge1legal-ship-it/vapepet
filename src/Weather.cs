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
    // ================= погода =================
    string wxCity = "Егорьевск"; double wxLat = 55.383, wxLon = 39.036; bool wxShow = true;
    volatile bool wxOk, wxBusy; float wxTemp, wxFeels, wxWind; int wxCode = -1, wxCloud; bool wxIsDay = true; string wxErr;
    DateTime wxSunrise = DateTime.MinValue, wxSunset = DateTime.MinValue;
    int lastWxFetchTick = -100000, thunderT, nextThunder = 300;
    float forceNight = -1; // для отладки
    List<float[]> clouds = new List<float[]>(), fogBanks = new List<float[]>();

    static float PetStat(string value, float fallback)
    {
        float n;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) return fallback;
        if (n < 0f) return 0f;
        if (n > 100f) return 100f;
        return n;
    }

    void LoadSettings()
    {
        try
        {
            string[] lines = File.ReadAllLines(SettingsPath);
            int v;
            if (lines.Length == 1 && int.TryParse(lines[0].Trim(), out v)) { if (v >= 0 && v < SizeSteps.Length) sizeIdx = v; return; }
            foreach (string ln in lines)
            {
                int eq = ln.IndexOf('='); if (eq < 0) continue;
                string k = ln.Substring(0, eq).Trim(), val = ln.Substring(eq + 1).Trim();
                if (k == "size" && int.TryParse(val, out v) && v >= 0 && v < SizeSteps.Length) sizeIdx = v;
                if (k == "pet_curiosity") petBrain.Curiosity = PetStat(val, 65f);
                if (k == "pet_playfulness") petBrain.Playfulness = PetStat(val, 60f);
                if (k == "pet_sociability") petBrain.Sociability = PetStat(val, 50f);
                if (k == "pet_laziness") petBrain.Laziness = PetStat(val, 45f);
                if (k == "pet_timidity") petBrain.Timidity = PetStat(val, 25f);
                if (k == "pet_affection") petBrain.Affection = PetStat(val, 20f);
                if (k == "pet_boredom") petBrain.Boredom = PetStat(val, 10f);
                if (k == "pet_energy") petBrain.Energy = PetStat(val, 85f);
                if (k == "city" && val.Length > 0) wxCity = val;
                if (k == "lat") double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out wxLat);
                if (k == "lon") double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out wxLon);
                if (k == "weather") wxShow = val != "0";
                if (k == "roam") interfacePlay = val != "0";
                if (k == "party") partyDay = val;
                if (k == "fright") { int fl; if (int.TryParse(val, out fl) && fl >= 0 && fl <= 3) frightLevel = fl; }
                if (k == "dl") dlWatch = val != "0";
                if (k == "notes") dodgeNotes = val != "0";
                if (k == "clip") clipOn = val != "0";
                if (k == "smartclip") smartClip = val != "0";
                if (k == "bigseen") bigSeen = val == "1";
                if (k == "ms_token") { msToken = Unseal(val.Trim()); if (val.Length > 0 && !val.StartsWith("dpapi:")) keysPlain = true; }
                if (k == "panel") panelShow = val != "0";
                if (k == "autoupd") autoUpdate = val != "0";
                if (k == "aiact") aiActions = val != "0";
                if (k == "hidefs") hideInFullscreen = val != "0";
                if (k == "monitor") petMonitor = val;
                if (k == "corner") petLeft = val == "left";
                if (k == "ver") savedVer = val;
                if (k == "net") netWatch = val != "0";
                if (k == "netspeed") netShowSpeed = val == "1";
                if (k == "links") linksSaved = true;
                if (k == "link") { int bar = val.IndexOf('|'); if (bar > 0) { links.Add(new string[] { val.Substring(0, bar), val.Substring(bar + 1) }); linksSaved = true; } }
                if (k == "money") long.TryParse(val, out money);
                if (k == "ai_format") aiFormat = val;
                if (k == "ai_url" && val.Length > 0) aiUrl = val;
                if (k == "ai_model" && val.Length > 0) aiModel = val;
                if (k == "ai_model" && val == "gemini-2.5-flash") aiModel = "gemini-3.8-flash";
                if (k == "ai_key") { aiKey = Unseal(val); if (val.Length > 0 && !val.StartsWith("dpapi:")) keysPlain = true; }
                if (k == "ai_voice") aiVoice = val != "0";
                if (k == "voice_clone") cloneVoice = val != "0";
                if (k == "voice_dir" && val.Length > 0) voiceDir = val;
            }
            if (aiKey.Length == 0 && aiFormat == "anthropic") { aiFormat = "openai"; aiUrl = GeminiUrl; aiModel = "gemini-3.8-flash"; }
        }
        catch { }
    }

    void SaveSettings()
    {
        try
        {
            List<string> sl = new List<string>(new string[] {
                "size=" + sizeIdx, "city=" + wxCity,
                "pet_curiosity=" + petBrain.Curiosity.ToString(CultureInfo.InvariantCulture), "pet_playfulness=" + petBrain.Playfulness.ToString(CultureInfo.InvariantCulture), "pet_sociability=" + petBrain.Sociability.ToString(CultureInfo.InvariantCulture), "pet_laziness=" + petBrain.Laziness.ToString(CultureInfo.InvariantCulture), "pet_timidity=" + petBrain.Timidity.ToString(CultureInfo.InvariantCulture), "pet_affection=" + petBrain.Affection.ToString(CultureInfo.InvariantCulture), "pet_boredom=" + petBrain.Boredom.ToString(CultureInfo.InvariantCulture), "pet_energy=" + petBrain.Energy.ToString(CultureInfo.InvariantCulture),
                "lat=" + wxLat.ToString(CultureInfo.InvariantCulture), "lon=" + wxLon.ToString(CultureInfo.InvariantCulture),
                "weather=" + (wxShow ? "1" : "0"), "roam=" + (interfacePlay ? "1" : "0"), "party=" + partyDay, "fright=" + frightLevel, "dl=" + (dlWatch ? "1" : "0"), "notes=" + (dodgeNotes ? "1" : "0"), "money=" + money, "ai_format=" + aiFormat, "ai_url=" + aiUrl, "ai_model=" + aiModel, "ai_key=" + Seal(aiKey), "ai_voice=" + (aiVoice ? "1" : "0"), "voice_clone=" + (cloneVoice ? "1" : "0"), "voice_dir=" + voiceDir
            });
            sl.Add("panel=" + (panelShow ? "1" : "0")); sl.Add("clip=" + (clipOn ? "1" : "0")); sl.Add("smartclip=" + (smartClip ? "1" : "0")); sl.Add("bigseen=" + (bigSeen ? "1" : "0")); sl.Add("ms_token=" + Seal(msToken)); sl.Add("net=" + (netWatch ? "1" : "0")); sl.Add("netspeed=" + (netShowSpeed ? "1" : "0"));
            sl.Add("autoupd=" + (autoUpdate ? "1" : "0")); sl.Add("aiact=" + (aiActions ? "1" : "0")); sl.Add("hidefs=" + (hideInFullscreen ? "1" : "0")); sl.Add("monitor=" + petMonitor); sl.Add("corner=" + (petLeft ? "left" : "right")); sl.Add("ver=" + AppVersion);
            sl.Add("links=1");
            foreach (string[] l in links) sl.Add("link=" + l[0].Replace("|", "/") + "|" + l[1]);
            File.WriteAllLines(SettingsPath, sl.ToArray(), Encoding.UTF8);
        }
        catch { }
    }

    static string Http(string url)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        ServicePointManager.Expect100Continue = false;
        using (WebClient wc = new WebClient())
        {
            wc.Encoding = Encoding.UTF8;
            wc.Headers.Add("User-Agent", "VapePet/1.0");
            return wc.DownloadString(url);
        }
    }

    static float JNum(string json, string key, float def)
    {
        Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?[0-9]+(\\.[0-9]+)?)");
        if (!m.Success) return def;
        return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    void FetchWeather()
    {
        if (wxBusy) return;
        wxBusy = true; lastWxFetchTick = tick;
        double lat = wxLat, lon = wxLon;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string url = "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString(CultureInfo.InvariantCulture) +
                    "&longitude=" + lon.ToString(CultureInfo.InvariantCulture) +
                    "&current=temperature_2m,apparent_temperature,weather_code,cloud_cover,wind_speed_10m,is_day&daily=sunrise,sunset,weather_code,temperature_2m_max&timezone=auto&forecast_days=4&wind_speed_unit=ms";
                ParseWeather(Http(url));
                wxErr = null;
            }
            catch (Exception ex) { bool first = wxErr == null; wxErr = ex.Message; if (first && mode == 0 && wxShow) houstonPending = true; }
            wxBusy = false;
        });
    }

    void ParseWeather(string json)
    {
        int ci = json.IndexOf("\"current\":{");
        if (ci < 0) throw new Exception("нет данных");
        int ce = json.IndexOf('}', ci);
        string cur = json.Substring(ci, ce - ci);
        wxTemp = JNum(cur, "temperature_2m", 0);
        wxFeels = JNum(cur, "apparent_temperature", wxTemp);
        wxCode = (int)JNum(cur, "weather_code", 0);
        wxCloud = (int)JNum(cur, "cloud_cover", 0);
        wxWind = JNum(cur, "wind_speed_10m", 0);
        wxIsDay = JNum(cur, "is_day", 1) > 0.5f;
        Match sr = Regex.Match(json, "\"sunrise\"\\s*:\\s*\\[\\s*\"([^\"]+)\"");
        Match ss = Regex.Match(json, "\"sunset\"\\s*:\\s*\\[\\s*\"([^\"]+)\"");
        DateTime d;
        if (sr.Success && DateTime.TryParseExact(sr.Groups[1].Value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) wxSunrise = d;
        if (ss.Success && DateTime.TryParseExact(ss.Groups[1].Value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) wxSunset = d;
        ParseDaily(json);
        wxOk = true;
    }

    void AskCity()
    {
        using (Form f = new Form())
        {
            f.Text = "Город для погоды"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.StartPosition = FormStartPosition.CenterScreen;
            f.MinimizeBox = false; f.MaximizeBox = false; f.TopMost = true; f.ClientSize = new Size(320, 110); f.ShowInTaskbar = false;
            Label l = new Label(); l.Text = "Введите город (например: Егорьевск):"; l.SetBounds(12, 12, 296, 20);
            TextBox tb = new TextBox(); tb.Text = wxCity; tb.SetBounds(12, 36, 296, 24);
            Button ok = new Button(); ok.Text = "OK"; ok.DialogResult = DialogResult.OK; ok.SetBounds(152, 72, 75, 26);
            Button cn = new Button(); cn.Text = "Отмена"; cn.DialogResult = DialogResult.Cancel; cn.SetBounds(233, 72, 75, 26);
            f.Controls.AddRange(new Control[] { l, tb, ok, cn });
            f.AcceptButton = ok; f.CancelButton = cn;
            if (f.ShowDialog() != DialogResult.OK) return;
            string name = tb.Text.Trim();
            if (name.Length == 0) return;
            try
            {
                string json = Http("https://geocoding-api.open-meteo.com/v1/search?count=1&language=ru&format=json&name=" + Uri.EscapeDataString(name));
                int ri = json.IndexOf("\"results\"");
                if (ri < 0) { MessageBox.Show("Город «" + name + "» не найден", "Погода"); return; }
                string r = json.Substring(ri);
                wxLat = JNum(r, "latitude", (float)wxLat); wxLon = JNum(r, "longitude", (float)wxLon);
                Match nm = Regex.Match(r, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                wxCity = nm.Success ? nm.Groups[1].Value : name;
                SaveSettings(); UpdateWeatherMenu();
                wxOk = false; FetchWeather();
            }
            catch (Exception ex) { MessageBox.Show("Не удалось найти город: " + ex.Message, "Погода"); }
        }
    }

    ToolStripMenuItem wxCityItem, wxShowItem;
    void UpdateWeatherMenu()
    {
        if (wxCityItem != null) wxCityItem.Text = "Город: " + wxCity + "...";
        if (wxShowItem != null) wxShowItem.Checked = wxShow;
    }

    int wxPeekT; volatile bool houstonPending;
    bool WxVisible { get { return wxShow && ((mode == 0 && pendingMode < 0) || (pendingMode == 0) || wxPeekT > 0); } }
    bool WxActive { get { return WxVisible && wxOk; } }
    bool IsSnow { get { int c = wxCode; return WxActive && ((c >= 71 && c <= 77) || c == 85 || c == 86); } }
    bool IsRain { get { int c = wxCode; return WxActive && ((c >= 51 && c <= 67) || (c >= 80 && c <= 82) || c >= 95); } }
    bool IsFog { get { return WxActive && (wxCode == 45 || wxCode == 48); } }
    bool IsStorm { get { return WxActive && wxCode >= 95; } }
    bool IsCold { get { return WxActive && wxTemp < 5; } }
    bool IsFreezing { get { return WxActive && wxTemp < -5; } }
    int PrecipLevel
    {
        get
        {
            int c = wxCode;
            if (c == 51 || c == 56 || c == 61 || c == 71 || c == 80 || c == 85 || c == 77) return 1;
            if (c == 53 || c == 63 || c == 73 || c == 81 || c == 66) return 2;
            return 3;
        }
    }

    static string WxText(int c)
    {
        if (c == 0) return "Ясно";
        if (c == 1) return "Почти ясно";
        if (c == 2) return "Переменная облачность";
        if (c == 3) return "Пасмурно";
        if (c == 45 || c == 48) return "Туман";
        if (c >= 51 && c <= 55) return "Морось";
        if (c == 56 || c == 57) return "Ледяная морось";
        if (c >= 61 && c <= 65) return c == 65 ? "Сильный дождь" : "Дождь";
        if (c == 66 || c == 67) return "Ледяной дождь";
        if (c >= 71 && c <= 75) return c == 75 ? "Сильный снег" : "Снег";
        if (c == 77) return "Снежная крупа";
        if (c >= 80 && c <= 82) return "Ливень";
        if (c == 85 || c == 86) return "Снегопад";
        if (c >= 95) return "Гроза";
        return "—";
    }

    // 0 - день, 1 - ночь
    float NightK()
    {
        if (forceNight >= 0) return forceNight;
        if (!wxShow) return 0;
        if (wxOk && wxSunrise == DateTime.MinValue) return wxIsDay ? 0 : 1;
        DateTime now = DateTime.Now;
        DateTime sr = wxSunrise != DateTime.MinValue ? now.Date + wxSunrise.TimeOfDay : now.Date.AddHours(7.5);
        DateTime ss = wxSunset != DateTime.MinValue ? now.Date + wxSunset.TimeOfDay : now.Date.AddHours(17);
        double m = (now - now.Date).TotalMinutes, a = (sr - sr.Date).TotalMinutes, b = (ss - ss.Date).TotalMinutes;
        if (m < a - 40 || m > b + 40) return 1;
        if (m < a + 20) return (float)Math.Min(1, Math.Max(0, (a + 20 - m) / 60.0));
        if (m > b - 20) return (float)Math.Min(1, Math.Max(0, (m - (b - 20)) / 60.0));
        return 0;
    }

    void UpdateWeather()
    {
        int wxInterval = (wxOk && wxErr == null) ? 30 * 60 * 15 : 30 * 60;
        if (tick - lastWxFetchTick > wxInterval && wxShow) FetchWeather();
        if (!WxActive) { clouds.Clear(); return; }
        float wind = Math.Max(-4, Math.Min(4, wxWind)) * 0.12f * u;
        // осадки
        if (IsRain || IsSnow)
        {
            int lvl = PrecipLevel;
            int n = IsSnow ? (rnd.Next(100) < 55 ? lvl : 0) : lvl * 2;
            int count = 0; foreach (Particle p in parts) if (p.Kind == 6 || p.Kind == 7) count++;
            for (int i = 0; i < n && count < (IsSnow ? 200 : 260); i++, count++)
            {
                Particle p = new Particle();
                p.X = (float)rnd.NextDouble() * (Width + U(20)) - U(10); p.Y = -U(2);
                if (IsSnow) { p.Kind = 7; p.VY = (0.25f + (float)rnd.NextDouble() * 0.2f) * u; p.VX = wind * 0.6f; p.R = U(0.35f + (float)rnd.NextDouble() * 0.45f); }
                else { p.Kind = 6; p.VY = (2.0f + (float)rnd.NextDouble() * 0.6f) * u; p.VX = wind * 1.5f; p.R = U(1.4f + lvl * 0.4f); }
                p.Max = (int)(Ground / p.VY) + 2;
                parts.Add(p);
            }
        }
        // облака
        int want = wxCloud >= 85 ? 5 : wxCloud / 22;
        if (IsFog) want = 0;
        while (clouds.Count < want) clouds.Add(new float[] { (float)rnd.NextDouble() * Width, U(3 + (float)rnd.NextDouble() * 14), 0.7f + (float)rnd.NextDouble() * 0.6f });
        while (clouds.Count > want) clouds.RemoveAt(clouds.Count - 1);
        foreach (float[] c in clouds)
        {
            c[0] += (0.08f + Math.Abs(wind) * 0.2f) * u * (wxWind >= 0 ? 1 : -1) * 0.5f + 0.05f * u;
            if (c[0] > Width + U(30)) c[0] = -U(30);
            if (c[0] < -U(30)) c[0] = Width + U(30);
        }
        // туман: мягкие клубы у земли
        if (IsFog)
        {
            int fogN = Math.Max(10, (int)(Width / U(18)));
            while (fogBanks.Count < fogN)
                fogBanks.Add(new float[] { (float)rnd.NextDouble() * Width, 1 + (float)rnd.NextDouble() * 16, 20 + (float)rnd.NextDouble() * 22, 4 + (float)rnd.NextDouble() * 5, (0.03f + (float)rnd.NextDouble() * 0.06f) * (rnd.Next(2) == 0 ? 1 : -1) });
            foreach (float[] f in fogBanks)
            {
                f[0] += f[4] * u;
                if (f[0] > Width + U(f[2])) f[0] = -U(f[2]);
                if (f[0] < -U(f[2])) f[0] = Width + U(f[2]);
            }
        }
        else fogBanks.Clear();
        // гроза
        if (IsStorm)
        {
            if (thunderT > 0) thunderT--;
            if (--nextThunder <= 0)
            {
                thunderT = 8; nextThunder = 250 + rnd.Next(400); shakeT = 6;
                me.SweatT = 60; me.Shake = 15;
                if (rnd.Next(2) == 0) Say("Ой! Гром!", me.X, CatTop(me) - U(1), 'U');
            }
        }
        // пар изо рта на морозе
        if (IsCold && tick % 4 == 0)
        {
            List<Cat> all = new List<Cat>(guests); all.Add(me);
            foreach (Cat c in all)
            {
                if (c.Hidden || c.Lying || c.Vape || rnd.Next(60) != 0) continue;
                PointF m = Local(c, 4.2f, -15.6f);
                Particle p = new Particle(); p.Kind = 0; p.X = m.X; p.Y = m.Y;
                p.VX = c.Dir * 0.15f * u; p.VY = -0.08f * u; p.R = U(0.6f); p.Max = 30;
                parts.Add(p);
            }
        }
    }

    string WeatherRemark()
    {
        if (!WxActive) return null;
        int t = (int)Math.Round(wxTemp);
        string ts = (t < 0 ? "−" + (-t) : t.ToString()) + "°";
        List<string> l = new List<string>();
        if (IsStorm) l.Add("Гроза... страшно");
        if (IsSnow) { l.Add("Снежок!"); l.Add("Зима близко..."); }
        if (IsRain) { l.Add("Опять дождь..."); l.Add("Хорошо, что зонтик взял"); }
        if (IsFog) l.Add("Туман, ничего не видно");
        if (wxTemp < -10) l.Add("Брр, " + ts + "! Лапы мёрзнут");
        else if (wxTemp < 3) l.Add("Холодно, " + ts + "...");
        if (wxTemp > 25) l.Add("Жара, " + ts + "!");
        if (wxCode <= 1 && NightK() < 0.5f) l.Add("Солнышко!");
        if (NightK() > 0.7f) l.Add("Уже ночь... спать пора");
        if (wxWind > 8) l.Add("Ну и ветрище!");
        if (l.Count == 0) l.Add("На улице " + ts + ", " + WxText(wxCode).ToLower());
        return l[rnd.Next(l.Count)];
    }

    // ---- рисование погоды ----
    void DrawCloud(Graphics g, float x, float y, float s, int alpha, bool dark)
    {
        Color c = dark ? Color.FromArgb(alpha, 150, 156, 170) : Color.FromArgb(alpha, 250, 252, 255);
        using (SolidBrush b = new SolidBrush(c))
        {
            g.FillEllipse(b, x - U(9) * s, y - U(2.5f) * s, U(18) * s, U(6) * s);
            g.FillEllipse(b, x - U(5) * s, y - U(6) * s, U(9) * s, U(8) * s);
            g.FillEllipse(b, x + U(0.5f) * s, y - U(4.6f) * s, U(7) * s, U(6) * s);
        }
    }

    void DrawWeatherBack(Graphics g)
    {
        if (!WxActive) return;
        bool dark = wxCode >= 3 && (wxCode == 3 || IsRain || IsSnow);
        int alpha = dark ? 150 : 120;
        foreach (float[] c in clouds) DrawCloud(g, c[0], c[1], c[2], alpha, dark);
    }

    void DrawWeatherGround(Graphics g)
    {
        if (!WxActive) return;
        int month = DateTime.Now.Month;
        bool snowGround = IsSnow || (wxTemp <= 1 && (month >= 11 || month <= 3));
        if (snowGround)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                List<PointF> pts = new List<PointF>();
                for (int i = 0; i <= 40; i++)
                {
                    float x = Width * i / 40f;
                    pts.Add(new PointF(x, Ground - U(1.2f) - (float)Math.Abs(Math.Sin(i * 1.7)) * U(0.7f)));
                }
                pts.Add(new PointF(Width, Ground)); pts.Add(new PointF(0, Ground));
                p.AddClosedCurve(pts.ToArray(), 0.3f);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, Ground - U(2.2f), Width, U(2.3f)), Color.FromArgb(245, 255, 255, 255), Color.FromArgb(235, 215, 228, 245), 90f)) g.FillPath(b, p);
            }
        }
        else if (IsRain)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 120, 160, 210)))
                for (int i = 0; i < 6; i++)
                {
                    float x = Width * ((i * 37 % 100) / 100f);
                    g.FillEllipse(b, x, Ground - U(0.8f), U(8 + (i % 3) * 3), U(1.2f));
                }
        }
    }

    void DrawWeatherFront(Graphics g)
    {
        if (!WxActive) return;
        foreach (Particle p in parts)
        {
            if (p.Kind == 6)
                using (Pen pen = new Pen(Color.FromArgb(150, 170, 200, 240), Math.Max(1f, U(0.22f))))
                    g.DrawLine(pen, p.X, p.Y, p.X - p.VX * 1.5f, p.Y - p.R);
            else if (p.Kind == 7)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
        }

        if (thunderT > 0 && thunderT % 3 != 0)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillRectangle(b, 0, 0, Width, Height);
    }

    // туман: сцена становится бледной и дымчатой (не трогая экран вокруг)
    Bitmap buf3;
    Bitmap ApplyFog(Bitmap src)
    {
        if (buf3 == null || buf3.Width != src.Width || buf3.Height != src.Height)
        {
            if (buf3 != null) buf3.Dispose();
            buf3 = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
        }
        float k = 0.38f;
        ColorMatrix cm = new ColorMatrix(new float[][] {
            new float[] { 1 - k, 0, 0, 0, 0 },
            new float[] { 0, 1 - k, 0, 0, 0 },
            new float[] { 0, 0, 1 - k, 0, 0 },
            new float[] { 0, 0, 0, 0.9f, 0 },
            new float[] { k * 0.86f, k * 0.88f, k * 0.92f, 0, 1 } });
        using (ImageAttributes ia = new ImageAttributes())
        using (Graphics g = Graphics.FromImage(buf3))
        {
            ia.SetColorMatrix(cm);
            g.Clear(Color.Transparent);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
        }
        return buf3;
    }

    // затемнение сцены ночью
    Bitmap buf2;
    Bitmap ApplyNight(Bitmap src, float n)
    {
        if (buf2 == null || buf2.Width != src.Width || buf2.Height != src.Height)
        {
            if (buf2 != null) buf2.Dispose();
            buf2 = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
        }
        ColorMatrix cm = new ColorMatrix(new float[][] {
            new float[] { 1 - 0.45f * n, 0, 0, 0, 0 },
            new float[] { 0, 1 - 0.38f * n, 0, 0, 0 },
            new float[] { 0, 0, 1 - 0.16f * n, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 0.0f, 0.0f, 0.03f * n, 0, 1 } });
        using (ImageAttributes ia = new ImageAttributes())
        using (Graphics g = Graphics.FromImage(buf2))
        {
            ia.SetColorMatrix(cm);
            g.Clear(Color.Transparent);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
        }
        return buf2;
    }

    void Glow(Graphics g, float cx, float cy, float rx, float ry, Color c, int alpha)
    {
        if (alpha <= 0) return;
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
            using (PathGradientBrush b = new PathGradientBrush(p))
            {
                b.CenterColor = Color.FromArgb(Math.Min(255, alpha), c);
                b.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                g.FillPath(b, p);
            }
        }
    }

    void DrawStreetLamp(Graphics g, float x, float n)
    {
        using (Pen p = new Pen(Color.FromArgb(60, 64, 74), Math.Max(1, U(0.9f)))) { g.DrawLine(p, x, Ground, x, Ground - U(38)); g.DrawArc(p, x, Ground - U(40), U(6), U(4), 180, 180); }
        using (GraphicsPath p = RoundRect(x + U(4.4f), Ground - U(38.8f), U(3.4f), U(1.6f), U(0.5f))) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 74, 84))) g.FillPath(b, p);
        int a = (int)(255 * n);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Max(60, a), 255, 235, 170))) g.FillEllipse(b, x + U(4.8f), Ground - U(37.6f), U(2.6f), U(1.2f));
        if (n > 0.05f)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { new PointF(x + U(5), Ground - U(37)), new PointF(x + U(7.4f), Ground - U(37)), new PointF(x + U(17), Ground), new PointF(x - U(5), Ground) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x - U(6), Ground - U(38), U(24), U(39)), Color.FromArgb((int)(90 * n), 255, 230, 150), Color.FromArgb((int)(25 * n), 255, 230, 150), 90f)) g.FillPath(b, p);
            }
            Glow(g, x + U(6), Ground - U(37), U(5), U(4), Color.FromArgb(255, 235, 170), (int)(200 * n));
            Glow(g, x + U(6), Ground, U(12), U(2.2f), Color.FromArgb(255, 230, 150), (int)(120 * n));
        }
    }

    void DrawNightLights(Graphics g, float n)
    {
        int a = (int)(170 * n);
        Color warm = Color.FromArgb(255, 220, 140);
        if (mode == 0) { DrawStreetLamp(g, U(6), n); DrawStreetLamp(g, Width - U(14), n); }
        if (mode == 1 || mode == 6 || (mode == 5 && omonPhase == 0))
        {
            Glow(g, A + U(13), Ground - U(25), U(18), U(24), warm, a / 2);
            Glow(g, A + U(43), Ground - U(25), U(18), U(24), warm, a / 2);
            Glow(g, A + U(13), Ground - U(43), U(10), U(4), Color.FromArgb(255, 220, 80), a);
            Glow(g, A + U(43), Ground - U(43), U(10), U(4), Color.FromArgb(255, 220, 80), a);
            Glow(g, CounterX + U(8), Ground - U(19), U(7), U(5), Color.FromArgb(120, 255, 160), a);
        }
        if (mode == 2)
        {
            if (consoleOn) Glow(g, StandX + U(23), Ground - U(20), U(26), U(18), tvState >= 2 ? GameA[tvState == 2 ? menuSel : gameIdx] : Color.FromArgb(120, 170, 255), a);
            Glow(g, HookahX + U(4), Ground - U(23), U(6), U(5), Color.FromArgb(255, 140, 60), me.PuffT > 55 ? 220 : a / 2);
        }
        if (mode == 3) { Glow(g, RackX + U(17), Ground - U(44), U(16), U(8), Color.FromArgb(255, 200, 200), a / 2); if (carState == 1 || carState == 2 || carState == 3) Glow(g, carX + U(48), Ground - U(8), U(14), U(4), Color.FromArgb(255, 245, 200), a); }
        if (mode == 4)
        {
            float[][] wins = { new float[] { 10, 8 }, new float[] { 78, 8 }, new float[] { 10, 28 }, new float[] { 44, 28 }, new float[] { 78, 28 } };
            Color[] disco = { pal['L'], pal['Y'], pal['u'], pal['P'], pal['N'] };
            for (int i = 0; i < wins.Length; i++) Glow(g, A + U(wins[i][0] + 10), Ground - U(wins[i][1] + 6), U(16), U(12), disco[(tick / 15 + i * 2) % disco.Length], a);
            Glow(g, PoolX + U(30), Ground - U(7), U(34), U(8), Color.FromArgb(80, 220, 255), a);
        }
        if (mode == 5 && omonPhase == 1) Glow(g, JailX + U(60), Ground - U(43), U(14), U(10), Color.FromArgb(170, 200, 255), a / 2);
        if (mode == 5 && vanState != 0) Glow(g, vanX + U(26), Ground - U(23), U(14), U(8), (tick / 6) % 2 == 0 ? Color.Red : Color.FromArgb(60, 120, 255), a + 40);
        List<Cat> all = new List<Cat>(guests); all.Add(me);
        foreach (Cat c in all)
            if (!c.Hidden && c.Vape && c.PuffT > 55) { PointF t = Local(c, 7.6f, -14.9f); Glow(g, t.X, t.Y, U(3), U(3), Color.FromArgb(120, 255, 255), 200); }
    }

    void DrawWxIcon(Graphics g, float cx, float cy, float s)
    {
        bool night = !wxIconDay && NightK() > 0.5f;
        int c = wxCode;
        bool cloudy = c >= 2;
        if (c <= 2)
        {
            float ox = cloudy ? -s * 0.3f : 0, oy = cloudy ? -s * 0.25f : 0;
            if (night)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 240, 190))) g.FillEllipse(b, cx + ox - s * 0.5f, cy + oy - s * 0.5f, s, s);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(45, 52, 70))) g.FillEllipse(b, cx + ox - s * 0.2f, cy + oy - s * 0.65f, s, s);
            }
            else
            {
                using (Pen p = new Pen(Color.FromArgb(255, 200, 50), Math.Max(1, s * 0.09f)))
                    for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(p, cx + ox + (float)Math.Cos(a) * s * 0.5f, cy + oy + (float)Math.Sin(a) * s * 0.5f, cx + ox + (float)Math.Cos(a) * s * 0.7f, cy + oy + (float)Math.Sin(a) * s * 0.7f); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 210, 60))) g.FillEllipse(b, cx + ox - s * 0.4f, cy + oy - s * 0.4f, s * 0.8f, s * 0.8f);
            }
        }
        if (cloudy)
        {
            Color cc = (c == 2) ? Color.FromArgb(245, 248, 255) : Color.FromArgb(205, 212, 225);
            using (SolidBrush b = new SolidBrush(cc))
            {
                g.FillEllipse(b, cx - s * 0.6f, cy - s * 0.05f, s * 1.2f, s * 0.5f);
                g.FillEllipse(b, cx - s * 0.35f, cy - s * 0.35f, s * 0.6f, s * 0.6f);
                g.FillEllipse(b, cx + s * 0.0f, cy - s * 0.25f, s * 0.5f, s * 0.5f);
            }
        }
        if (IsRain)
            using (Pen p = new Pen(Color.FromArgb(120, 180, 255), Math.Max(1, s * 0.08f)))
                for (int i = 0; i < 3; i++) g.DrawLine(p, cx - s * 0.3f + i * s * 0.3f, cy + s * 0.5f, cx - s * 0.4f + i * s * 0.3f, cy + s * 0.75f);
        if (IsSnow)
            using (SolidBrush b = new SolidBrush(Color.White))
                for (int i = 0; i < 3; i++) g.FillEllipse(b, cx - s * 0.35f + i * s * 0.3f, cy + s * 0.5f + (i % 2) * s * 0.12f, s * 0.15f, s * 0.15f);
        if (IsStorm)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 60)))
                g.FillPolygon(b, new PointF[] { new PointF(cx, cy + s * 0.3f), new PointF(cx - s * 0.15f, cy + s * 0.6f), new PointF(cx, cy + s * 0.6f), new PointF(cx - s * 0.1f, cy + s * 0.9f), new PointF(cx + s * 0.2f, cy + s * 0.5f), new PointF(cx + s * 0.05f, cy + s * 0.5f) });
        if (IsFog)
            using (Pen p = new Pen(Color.FromArgb(220, 225, 235), Math.Max(1, s * 0.09f)))
                for (int i = 0; i < 3; i++) g.DrawLine(p, cx - s * 0.5f + (i % 2) * s * 0.1f, cy + s * 0.35f + i * s * 0.18f, cx + s * 0.5f, cy + s * 0.35f + i * s * 0.18f);
    }

    RectangleF WidgetRect()
    {
        float w = W(72), h = W(22);
        return new RectangleF(HudX(w), W(1.5f), w, h);
    }

    void DrawWeatherWidget(Graphics g)
    {
        if (!WxVisible) return;
        RectangleF r = WidgetRect();
        using (GraphicsPath p = RoundRect(r.X, r.Y, r.Width, r.Height, W(3.5f)))
        {
            float n = NightK();
            Color top = n > 0.5f ? Color.FromArgb(215, 40, 46, 70) : Color.FromArgb(215, 70, 130, 210);
            Color bot = n > 0.5f ? Color.FromArgb(215, 25, 28, 45) : Color.FromArgb(215, 40, 90, 170);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), top, bot, 90f)) g.FillPath(b, p);
            using (Pen pp = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(1, W(0.25f)))) g.DrawPath(pp, p);
        }
        TextRenderingHint th = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using (SolidBrush w = new SolidBrush(Color.White))
        using (SolidBrush w2 = new SolidBrush(Color.FromArgb(210, 230, 240, 255)))
        {
            if (!wxOk)
            {
                g.DrawString(wxErr != null ? "Погода: нет связи" : "Загружаю погоду...", fntWxD, w, r.X + W(3), r.Y + W(6));
                g.DrawString(wxErr != null ? "повторю через минуту" : wxCity, fntWxS, w2, r.X + W(3), r.Y + W(12));
            }
            else
            {
                DrawWxIcon(g, r.X + W(10.5f), r.Y + W(10), W(11));
                int t = (int)Math.Round(wxTemp);
                string ts = (t < 0 ? "−" + (-t) : (t > 0 ? "+" + t : "0")) + "°";
                g.DrawString(ts, fntWxT, w, r.X + W(20), r.Y + W(0.4f));
                SizeF tsz = g.MeasureString(ts, fntWxT);
                int fl = (int)Math.Round(wxFeels);
                g.DrawString("ощущ. " + (fl < 0 ? "−" + (-fl) : fl.ToString()) + "°", fntWxS, w2, r.X + W(20.6f) + tsz.Width, r.Y + W(4.2f));
                g.DrawString(WxText(wxCode), fntWxD, w, r.X + W(20.6f), r.Y + W(10.4f));
                g.DrawString(wxCity + " · ветер " + wxWind.ToString("0", CultureInfo.InvariantCulture) + " м/с", fntWxS, w2, r.X + W(20.8f), r.Y + W(15.6f));
            }
        }
        g.TextRenderingHint = th;
    }

    // ---- одежда по погоде ----
    void DrawScarf(Graphics g, Cat c)
    {
        Color[] sc = { Color.FromArgb(220, 60, 70), Color.FromArgb(60, 120, 220), Color.FromArgb(250, 190, 60), Color.FromArgb(70, 170, 110), Color.FromArgb(170, 100, 220) };
        Color col = sc[Math.Abs(c.Id) % sc.Length];
        using (GraphicsPath p = RoundRect(-3.9f, -14.4f, 8.2f, 2.2f, 1.0f))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4, -14.5f, 8.4f, 2.4f), Light(col, 0.15f), Dark(col, 0.8f), 90f)) g.FillPath(b, p);
            using (Pen pp = new Pen(Dark(col, 0.55f), 0.25f)) g.DrawPath(pp, p);
        }
        float sw = (float)Math.Sin((tick + c.Id * 9) * 0.15) * 0.4f;
        PointF[] tail = { P(2.0f, -12.6f), P(3.4f, -12.6f), P(3.8f + sw, -9.0f), P(2.6f + sw, -9.2f) };
        using (SolidBrush b = new SolidBrush(Dark(col, 0.9f))) g.FillPolygon(b, tail);
        using (Pen pp = new Pen(Color.FromArgb(160, 255, 255, 255), 0.3f)) { g.DrawLine(pp, 2.4f + sw * 0.7f, -10.6f, 3.6f + sw * 0.8f, -10.6f); g.DrawLine(pp, -2.6f, -13.3f, -1.6f, -13.3f); g.DrawLine(pp, 0.2f, -13.3f, 1.2f, -13.3f); }
    }

    void DrawUmbrella(Graphics g, Cat c)
    {
        Color[] uc = { Color.FromArgb(230, 60, 80), Color.FromArgb(60, 130, 230), Color.FromArgb(250, 200, 60), Color.FromArgb(40, 40, 50), Color.FromArgb(150, 90, 220) };
        Color col = uc[Math.Abs(c.Id + 2) % uc.Length];
        using (Pen p = new Pen(Color.FromArgb(70, 60, 50), 0.35f)) { g.DrawLine(p, 2.8f, -15.2f, 2.6f, -30.0f); g.DrawArc(p, 2.2f, -15.6f, 1.2f, 1.6f, 0, 180); }
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddArc(-6.4f, -34.4f, 18f, 9f, 180, 180);
            float y = -29.9f;
            p.AddBezier(11.6f, y, 10.4f, y - 1.2f, 8.8f, y - 1.2f, 7.6f, y);
            p.AddBezier(7.6f, y, 6.4f, y - 1.2f, 4.0f, y - 1.2f, 2.6f, y);
            p.AddBezier(2.6f, y, 1.2f, y - 1.2f, -1.2f, y - 1.2f, -2.4f, y);
            p.AddBezier(-2.4f, y, -3.6f, y - 1.2f, -5.2f, y - 1.2f, -6.4f, y);
            p.CloseFigure();
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-6.5f, -34.5f, 18.2f, 5f), Light(col, 0.2f), Dark(col, 0.8f), 90f)) g.FillPath(b, p);
            using (Pen pp = new Pen(Dark(col, 0.5f), 0.25f)) g.DrawPath(pp, p);
        }
        using (Pen pp = new Pen(Color.FromArgb(70, 0, 0, 0), 0.2f)) { g.DrawLine(pp, 2.6f, -34.2f, -2.4f, -30f); g.DrawLine(pp, 2.6f, -34.2f, 7.6f, -30f); }
    }

    bool WantsUmbrella(Cat c)
    {
        if (!IsRain || c.Hold != ' ' || c.Sitting || c.Swimming || c.Lying || c.Hidden || c.Cuffed || c.Outfit == 11) return false;
        if (mode == 5 && omonPhase == 1) return false;
        return mode == 0 || mode == 4 || (mode == 1 && c != me && c.Walking);
    }
}
