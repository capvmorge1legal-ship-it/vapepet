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
    // ================= большая панель «VapePet — твой цифровой друг» =================
    LayerWin bigWin; public bool bigOpen; int bigHover = -1; int bigIdleT; float bk = 1f;
    List<KeyValuePair<RectangleF, Action>> bigHits = new List<KeyValuePair<RectangleF, Action>>();
    float[] netHist = new float[48]; int netHistN;
    // прогноз на 3 дня
    int[] wxDayCode = new int[] { -1, -1, -1, -1 }; float[] wxDayMax = new float[4];
    // курсы для панели
    float usdNow, usdPrev, eurNow, eurPrev, btcNow, btcPct; float[] usdHist; DateTime fxAt = DateTime.MinValue; volatile bool fxBusy;
    bool wxIconDay;

    float B(float v) { return v * bk * 0.9f; }

    // ---------- настоящее «матовое стекло»: размытый снимок того, что под панелью ----------
    [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr h, uint aff);
    bool bigBlurOk; Bitmap bigBlur; public Bitmap PreviewBehind;
    void GrabBehind()
    {
        try
        {
            Size sz = BigSize(); Point at = BigPos();
            Bitmap src = null;
            if (PreviewBehind != null) src = PreviewBehind.Clone(new Rectangle(0, 0, Math.Min(sz.Width, PreviewBehind.Width), Math.Min(sz.Height, PreviewBehind.Height)), PixelFormat.Format24bppRgb);
            else if (bigBlurOk) { src = new Bitmap(sz.Width, sz.Height, PixelFormat.Format24bppRgb); using (Graphics g = Graphics.FromImage(src)) g.CopyFromScreen(at.X, at.Y, 0, 0, sz); }
            if (src == null) return;
            int sw = Math.Max(4, sz.Width / 14), sh = Math.Max(4, sz.Height / 14);
            using (src)
            using (Bitmap small = new Bitmap(sw, sh, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(src, 0, 0, sw, sh); }
                // плавное увеличение в два шага — без «квадратиков»
                Bitmap big = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppPArgb);
                using (Bitmap mid = new Bitmap(sw * 4, sh * 4, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(mid)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(small, new Rectangle(-2, -2, mid.Width + 4, mid.Height + 4)); }
                    using (Bitmap mid2 = new Bitmap(sw, sh, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(mid2)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(mid, 0, 0, sw, sh); }
                        using (Graphics g = Graphics.FromImage(mid)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(mid2, new Rectangle(-2, -2, mid.Width + 4, mid.Height + 4)); }
                    }
                    using (Graphics g = Graphics.FromImage(big)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(mid, new Rectangle(-4, -4, sz.Width + 8, sz.Height + 8)); }
                }
                if (bigBlur != null) bigBlur.Dispose(); bigBlur = big;
            }
        }
        catch { }
    }
    Size BigSize() { return new Size((int)B(340), (int)B(468)); }
    public bool bigHot;   // мышь над панелью — полная яркость, иначе полупрозрачная
    Point BigPos()
    {
        Rectangle wa = PetScreen.WorkingArea; Size s = BigSize();
        return new Point(wa.Right - s.Width - (int)B(10), wa.Bottom - s.Height - (int)B(10));
    }

    public bool bigSeen;
    void BigFirstShow() { if (!bigSeen && tick >= 30 * 12 && !roaming && mode == 0) { bigSeen = true; SaveSettings(); BigOpen(); } }

    public void BigOpen()
    {
        try
        {
            using (Graphics g0 = Graphics.FromHwnd(IntPtr.Zero)) bk = g0.DpiX / 96f;
            if (bigWin == null || bigWin.IsDisposed)
            {
                bigWin = new LayerWin(this, BigSize, BigPos, DrawBig);
                bigWin.LeftClick = delegate (Point p) { BigClick(p); };
                try { bigWin.Show(); bigWin.Hide(); bigBlurOk = SetWindowDisplayAffinity(bigWin.Handle, 0x11); } catch { bigBlurOk = false; }
                bigWin.Hover = delegate (Point p)
                {
                    int h = BigHitAt(p); bigIdleT = 0; bool hot = p.X >= 0;
                    if (h != bigHover || hot != bigHot) { bigHover = h; bigHot = hot; bigWin.Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; BigRender(); }
                };
            }
            bigOpen = true; bigIdleT = 0; bigHover = -1;
            if ((DateTime.Now - fxAt).TotalHours >= 2) FxFetch();
            if (wxDayCode[0] < 0) FetchWeather();
            GrabBehind(); BigRender(); bigWin.Show();
            me.HappyT = 30;
        }
        catch (Exception ex) { GuardLog("большая панель", ex); }
    }

    public void BigClose() { bigOpen = false; try { if (bigWin != null) bigWin.Hide(); } catch { } }

    void BigRender() { try { if (bigWin != null && bigOpen) bigWin.Render(); } catch (Exception ex) { GuardLog("большая панель: кадр", ex); } }

    int BigHitAt(Point p) { for (int i = 0; i < bigHits.Count; i++) if (bigHits[i].Key.Contains(p)) return i; return -1; }
    void BigClick(Point p) { int i = BigHitAt(p); if (i >= 0) { Action a = bigHits[i].Value; try { a(); } catch (Exception ex) { GuardLog("панель: клик", ex); } } }

    void BigTick()
    {
        BigFirstShow();
        if (!bigOpen) return;
        if (tick % 30 == 0)
        {
            netHist[netHistN % netHist.Length] = netRx; netHistN++;
            bool over = bigWin != null && bigWin.Bounds.Contains(Cursor.Position);
            if (over) bigIdleT = 0; else if (++bigIdleT > 90) { BigClose(); return; }   // 1,5 минуты без внимания — сворачиваем
            if (over != bigHot) { bigHot = over; BigRender(); }
            else if (netHistN % 2 == 0) { GrabBehind(); BigRender(); }
            if (bigWin != null) SetWindowPos(bigWin.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }
    }

    // ---------- данные: прогноз, курсы ----------
    void ParseDaily(string json)
    {
        try
        {
            int di = json.IndexOf("\"daily\":{"); if (di < 0) return;
            string d = json.Substring(di);
            Match mc = Regex.Match(d, "\"weather_code\"\\s*:\\s*\\[([^\\]]*)\\]"), mt = Regex.Match(d, "\"temperature_2m_max\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (!mc.Success || !mt.Success) return;
            string[] c = mc.Groups[1].Value.Split(','), t = mt.Groups[1].Value.Split(',');
            for (int i = 0; i < 4 && i < c.Length && i < t.Length; i++)
            {
                float v; if (float.TryParse(c[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) wxDayCode[i] = (int)v;
                if (float.TryParse(t[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) wxDayMax[i] = v;
            }
        }
        catch { }
    }

    void FxFetch()
    {
        if (fxBusy) return; fxBusy = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string d1 = DateTime.Now.AddDays(-40).ToString("dd'/'MM'/'yyyy"), d2 = DateTime.Now.AddDays(1).ToString("dd'/'MM'/'yyyy");
                float[] u = CbrDyn("R01235", d1, d2), e = CbrDyn("R01239", d1, d2);
                if (u.Length >= 2) { usdHist = u; usdNow = u[u.Length - 1]; usdPrev = u[u.Length - 2]; }
                if (e.Length >= 2) { eurNow = e[e.Length - 1]; eurPrev = e[e.Length - 2]; }
                fxAt = DateTime.Now;
            }
            catch { fxAt = DateTime.Now.AddHours(-1.8); }
            try
            {
                string b = HttpT("https://api.binance.com/api/v3/ticker/24hr?symbol=BTCUSDT", 10000, Encoding.UTF8);
                Match lp = Regex.Match(b, "\"lastPrice\"\\s*:\\s*\"([0-9.]+)\""), pc = Regex.Match(b, "\"priceChangePercent\"\\s*:\\s*\"(-?[0-9.]+)\"");
                float v;
                if (lp.Success && float.TryParse(lp.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) btcNow = v;
                if (pc.Success && float.TryParse(pc.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) btcPct = v;
            }
            catch { }
            fxBusy = false;
            try { BeginInvoke((Action)delegate { BigRender(); }); } catch { }
        });
    }

    static float[] CbrDyn(string code, string d1, string d2)
    {
        string x = HttpT("https://www.cbr.ru/scripts/XML_dynamic.asp?date_req1=" + d1 + "&date_req2=" + d2 + "&VAL_NM_RQ=" + code, 15000, Encoding.GetEncoding(1251));
        List<float> r = new List<float>();
        foreach (Match m in Regex.Matches(x, "<Nominal>([0-9]+)</Nominal>\\s*<Value>([0-9]+,[0-9]+)</Value>"))
        {
            float n = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), v = float.Parse(m.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            r.Add(v / Math.Max(1f, n));
        }
        return r.ToArray();
    }

    // ---------- рисование ----------
    static readonly Color Txt = Color.FromArgb(250, 252, 255), TxtDim = Color.FromArgb(215, 225, 235, 250);

    void Glass(Graphics g, RectangleF r, float rad, bool hover)
    {
        using (GraphicsPath p = RoundRect(r.X, r.Y, r.Width, r.Height, rad))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(hover ? 70 : 38, 255, 255, 255), Color.FromArgb(hover ? 34 : 12, 255, 255, 255), 90f)) g.FillPath(b, p);
            using (LinearGradientBrush eb = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), Color.FromArgb(hover ? 230 : 170, 255, 255, 255), Color.FromArgb(40, 255, 255, 255), 90f))
            using (Pen pen = new Pen(eb, B(1))) g.DrawPath(pen, p);
        }
        // блик-изгиб сверху, как у жидкого стекла
        RectangleF hl = new RectangleF(r.X + rad * 0.6f, r.Y + B(1.2f), r.Width - rad * 1.2f, Math.Min(r.Height * 0.38f, B(16)));
        using (GraphicsPath hp = RoundRect(hl.X, hl.Y, hl.Width, hl.Height, hl.Height / 2))
        using (LinearGradientBrush hb = new LinearGradientBrush(new RectangleF(hl.X, hl.Y - 1, hl.Width, hl.Height + 2), Color.FromArgb(38, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(hb, hp);
    }

    void Pill(Graphics g, RectangleF r, Color c1, Color c2, string text, Font f, bool hover, string glyph)
    {
        using (GraphicsPath p = RoundRect(r.X, r.Y, r.Width, r.Height, r.Height / 2))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(hover ? 230 : 175, hover ? ControlPaint.Light(c1, 0.25f) : c1), Color.FromArgb(hover ? 230 : 175, hover ? ControlPaint.Light(c2, 0.25f) : c2), 90f)) g.FillPath(b, p);
            using (LinearGradientBrush eb = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), Color.FromArgb(200, 255, 255, 255), Color.FromArgb(40, 255, 255, 255), 90f))
            using (Pen pen = new Pen(eb, B(0.9f))) g.DrawPath(pen, p);
        }
        using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; STr(g, (glyph != null ? glyph + " " : "") + text, f, Brushes.White, r, sf); }
    }

    static readonly SolidBrush ShadowB = new SolidBrush(Color.FromArgb(110, 0, 10, 30));
    void STr(Graphics g, string s, Font f, Brush b, float x, float y) { g.DrawString(s, f, ShadowB, x + 0.8f, y + 1f); g.DrawString(s, f, b, x, y); }
    void STr(Graphics g, string s, Font f, Brush b, float x, float y, StringFormat sf) { g.DrawString(s, f, ShadowB, x + 0.8f, y + 1f, sf); g.DrawString(s, f, b, x, y, sf); }
    void STr(Graphics g, string s, Font f, Brush b, RectangleF r, StringFormat sf) { RectangleF r2 = r; r2.Offset(0.8f, 1f); g.DrawString(s, f, ShadowB, r2, sf); g.DrawString(s, f, b, r, sf); }
    void STr(Graphics g, string s, Font f, Brush b, PointF p) { STr(g, s, f, b, p.X, p.Y); }

    void Hit(RectangleF r, Action a) { bigHits.Add(new KeyValuePair<RectangleF, Action>(r, a)); }
    bool Hov(RectangleF r) { int i = BigHitAt(Point.Round(new PointF(r.X + r.Width / 2, r.Y + r.Height / 2))); return i >= 0 && i == bigHover; }

    void DrawBig(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        bigHits.Clear();
        Size s = BigSize(); float W0 = s.Width, H0 = s.Height;
        RectangleF all = new RectangleF(B(3), B(3), W0 - B(6), H0 - B(6));
        using (GraphicsPath p = RoundRect(all.X, all.Y, all.Width, all.Height, B(20)))
        {
            // матовое стекло: размытое то, что под панелью, + тонировка + белая дымка сверху
            bool blur = bigBlur != null;
            if (blur) { GraphicsState st = g.Save(); g.SetClip(p); g.DrawImageUnscaled(bigBlur, 0, 0); g.Restore(st); }
            using (LinearGradientBrush b = new LinearGradientBrush(all, Color.FromArgb(blur ? 150 : 215, 24, 34, 58), Color.FromArgb(blur ? 172 : 228, 14, 20, 38), 90f)) g.FillPath(b, p);
            using (LinearGradientBrush w = new LinearGradientBrush(all, Color.FromArgb(46, 255, 255, 255), Color.FromArgb(6, 255, 255, 255), 90f)) g.FillPath(w, p);
            using (LinearGradientBrush eb = new LinearGradientBrush(RectangleF.Inflate(all, 1, 1), Color.FromArgb(235, 255, 255, 255), Color.FromArgb(60, 255, 255, 255), 90f))
            using (Pen pen = new Pen(eb, B(1.3f))) g.DrawPath(pen, p);
            using (GraphicsPath ip = RoundRect(all.X + B(1.5f), all.Y + B(1.5f), all.Width - B(3), all.Height - B(3), B(18.5f)))
            using (Pen inner = new Pen(Color.FromArgb(45, 255, 255, 255), 1f)) g.DrawPath(inner, ip);
        }
        using (Font fTitle = new Font("Segoe UI Semibold", B(15), FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font fBig = new Font("Segoe UI Semibold", B(13), FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font fHuge = new Font("Segoe UI Semibold", B(21), FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font fN = new Font("Segoe UI", B(10.5f), FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font fS = new Font("Segoe UI", B(9), FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font fB = new Font("Segoe UI Semibold", B(10), FontStyle.Bold, GraphicsUnit.Pixel))
        using (SolidBrush tx = new SolidBrush(Txt)) using (SolidBrush dim = new SolidBrush(TxtDim))
        {
            float x0 = all.X + B(8), cw = all.Width - B(16), y = all.Y + B(8);

            // --- шапка
            RectangleF hdr = new RectangleF(x0, y, cw, B(42)); Glass(g, hdr, B(12), false);
            RectangleF logo = new RectangleF(hdr.X + B(7), hdr.Y + B(6), B(30), B(30));
            using (GraphicsPath lp = RoundRect(logo.X, logo.Y, logo.Width, logo.Height, B(8)))
            using (LinearGradientBrush lb = new LinearGradientBrush(logo, Color.FromArgb(120, 190, 255), Color.FromArgb(50, 110, 230), 90f)) g.FillPath(lb, lp);
            Paw(g, logo.X + logo.Width / 2, logo.Y + logo.Height / 2 + B(1), B(11), Color.White);
            STr(g, "VapePet", fTitle, tx, hdr.X + B(44), hdr.Y + B(4));
            STr(g, "Твой цифровой друг ♥", fS, dim, hdr.X + B(45), hdr.Y + B(23));
            string[] hb = { "—", "☰", "✕" };
            Action[] ha = { delegate { BigClose(); }, delegate { OpenMenuPath(); }, delegate { BigClose(); } };
            for (int i = 0; i < 3; i++)
            {
                RectangleF br = new RectangleF(hdr.Right - B(32) * (3 - i) - B(4), hdr.Y + B(8), B(26), B(26)); Hit(br, ha[i]);
                bool hv = Hov(br);
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(hv ? 120 : 60, 255, 255, 255))) g.FillEllipse(cb, br);
                using (Pen cp = new Pen(Color.FromArgb(160, 255, 255, 255), B(1))) g.DrawEllipse(cp, br);
                using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; STr(g, hb[i], fB, Brushes.White, br, sf); }
            }
            y = hdr.Bottom + B(6);

            // --- погода
            RectangleF wc = new RectangleF(x0, y, cw, B(66)); Hit(wc, delegate { FetchWeather(); string wr = WeatherRemark(); if (wr != null) Say(wr, me.X, CatTop(me) - U(1), 'U'); });
            Glass(g, wc, B(12), Hov(wc));
            if (wxOk)
            {
                DrawWxIcon(g, wc.X + B(30), wc.Y + B(33), B(21));
                int t = (int)Math.Round(wxTemp);
                STr(g, (t > 0 ? "+" : "") + t + "°C", fHuge, tx, wc.X + B(56), wc.Y + B(8));
                string city = wxCity != null && wxCity.Length > 0 ? wxCity : "";
                if (city.Length > 12) city = city.Substring(0, 11) + "…";
                SizeF ts = g.MeasureString((t > 0 ? "+" : "") + t + "°C", fHuge);
                STr(g, "⌖ " + city, fS, dim, wc.X + B(58) + ts.Width, wc.Y + B(16));
                STr(g, WxText(wxCode), fS, dim, wc.X + B(58), wc.Y + B(40));
                // разделитель и 3 дня
                float dx = wc.Right - B(132);
                using (Pen sp = new Pen(Color.FromArgb(70, 255, 255, 255), 1)) g.DrawLine(sp, dx, wc.Y + B(10), dx, wc.Bottom - B(10));
                string[] dn = { "Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };
                using (StringFormat sc = new StringFormat()) { sc.Alignment = StringAlignment.Center;
                    for (int i = 1; i <= 3; i++)
                    {
                        float cx = dx + B(22) + (i - 1) * B(42);
                        STr(g, dn[(int)DateTime.Now.AddDays(i).DayOfWeek], fB, tx, cx, wc.Y + B(6), sc);
                        if (wxDayCode[i] >= 0)
                        {
                            int sv = wxCode; wxCode = wxDayCode[i]; wxIconDay = true;
                            try { DrawWxIcon(g, cx, wc.Y + B(33), B(10)); } finally { wxCode = sv; wxIconDay = false; }
                            int tm = (int)Math.Round(wxDayMax[i]);
                            STr(g, (tm > 0 ? "+" : "") + tm + "°", fB, tx, cx, wc.Y + B(46), sc);
                        }
                    } }
            }
            else STr(g, wxErr != null ? "Погода: нет связи" : "Погода: загружаю…", fN, dim, wc.X + B(14), wc.Y + B(24));
            y = wc.Bottom + B(6);

            // --- VPN
            RectangleF vc = new RectangleF(x0, y, cw, B(56)); Glass(g, vc, B(12), false);
            bool on = vpnName != null;
            Shield(g, vc.X + B(26), vc.Y + B(28), B(17), on ? Color.FromArgb(70, 220, 150) : (vpnApp != null ? Color.FromArgb(240, 190, 60) : Color.FromArgb(150, 160, 180)), on);
            STr(g, "VPN", fBig, tx, vc.X + B(52), vc.Y + B(9));
            Color stc = on ? Color.FromArgb(90, 225, 130) : vpnApp != null ? Color.FromArgb(245, 195, 70) : Color.FromArgb(170, 180, 200);
            using (SolidBrush sb = new SolidBrush(stc)) { g.FillEllipse(sb, vc.X + B(53), vc.Y + B(34), B(6), B(6)); STr(g, on ? "Подключено" : vpnApp != null ? "Не подключён" : "Выключен", fS, sb, vc.X + B(61), vc.Y + B(29)); }
            float vx = vc.X + B(140);
            using (Pen sp = new Pen(Color.FromArgb(70, 255, 255, 255), 1)) g.DrawLine(sp, vx - B(8), vc.Y + B(10), vx - B(8), vc.Bottom - B(10));
            string ctry = geoLine ?? "—", vcity = "";
            int comma = ctry.IndexOf(','); if (comma > 0) { vcity = ctry.Substring(comma + 1).Trim(); ctry = ctry.Substring(0, comma).Trim(); }
            Flag(g, new RectangleF(vx, vc.Y + B(13), B(17), B(12)), ctry);
            string ct = ctry.Length > 11 ? ctry.Substring(0, 10) + "…" : ctry; string cc = vcity.Length > 12 ? vcity.Substring(0, 11) + "…" : vcity;
            STr(g, ct, fB, tx, vx + B(21), vc.Y + B(10)); STr(g, cc, fS, dim, vx + B(21), vc.Y + B(29));
            RectangleF vb = new RectangleF(vc.Right - B(76), vc.Y + B(14), B(68), B(28)); Hit(vb, delegate { OpenVpnApp(); });
            Pill(g, vb, Color.FromArgb(60, 190, 130), Color.FromArgb(25, 120, 85), "Открыть", fB, Hov(vb), "⚡");
            y = vc.Bottom + B(6);

            // --- интернет
            RectangleF ic = new RectangleF(x0, y, cw, B(56)); Hit(ic, delegate { SpeedTest(); }); Glass(g, ic, B(12), Hov(ic));
            Wifi(g, ic.X + B(26), ic.Y + B(31), B(17), netDown ? Color.FromArgb(240, 90, 90) : Color.FromArgb(80, 190, 255));
            STr(g, "Интернет", fBig, tx, ic.X + B(52), ic.Y + B(9));
            Color nc = netDown ? Color.FromArgb(240, 90, 90) : Color.FromArgb(90, 225, 130);
            string ns = netDown ? "Нет связи · " + (int)(DateTime.Now - netDownSince).TotalMinutes + " мин" : netSpeedBps > 0 ? "Скорость " + Mbit(netSpeedBps) : "Подключение активно";
            using (SolidBrush sb = new SolidBrush(nc)) { g.FillEllipse(sb, ic.X + B(53), ic.Y + B(34), B(6), B(6)); STr(g, ns, fS, sb, ic.X + B(61), ic.Y + B(29)); }
            float sx = ic.X + B(168);
            using (SolidBrush db = new SolidBrush(Color.FromArgb(90, 180, 255))) STr(g, "↓", fB, db, sx, ic.Y + B(9));
            using (SolidBrush ub = new SolidBrush(Color.FromArgb(255, 120, 150))) STr(g, "↑", fB, ub, sx, ic.Y + B(29));
            STr(g, Mbit(netRx), fS, tx, sx + B(11), ic.Y + B(10)); STr(g, Mbit(netTx), fS, tx, sx + B(11), ic.Y + B(30));
            Spark(g, new RectangleF(ic.Right - B(74), ic.Y + B(10), B(64), B(36)), NetSeries(), Color.FromArgb(80, 170, 255));
            y = ic.Bottom + B(6);

            // --- курсы
            RectangleF rc = new RectangleF(x0, y, cw, B(76)); Hit(rc, delegate { FxFetch(); ShowRates(); }); Glass(g, rc, B(12), Hov(rc));
            Coins(g, rc.X + B(26), rc.Y + B(40), B(16));
            STr(g, "Курсы валют", fBig, tx, rc.X + B(52), rc.Y + B(7));
            RateLine(g, rc.X + B(54), rc.Y + B(27), "$", usdNow, usdPrev, false, fS, fB, tx);
            RateLine(g, rc.X + B(54), rc.Y + B(42), "€", eurNow, eurPrev, false, fS, fB, tx);
            RateLine(g, rc.X + B(54), rc.Y + B(57), "₿", btcNow, btcPct, true, fS, fB, tx);
            if (usdHist != null && usdHist.Length > 2) Spark(g, new RectangleF(rc.Right - B(98), rc.Y + B(14), B(88), B(50)), usdHist, Color.FromArgb(250, 200, 70));
            else STr(g, fxBusy ? "загружаю…" : "", fS, dim, rc.Right - B(80), rc.Y + B(32));
            y = rc.Bottom + B(6);

            // --- ИИ-помощник
            RectangleF ac = new RectangleF(x0, y, cw, B(52)); Glass(g, ac, B(12), false);
            Robot(g, ac.X + B(26), ac.Y + B(26), B(15));
            STr(g, "ИИ-помощник", fBig, tx, ac.X + B(52), ac.Y + B(8));
            STr(g, aiKey.Length > 0 ? "Задай вопрос…" : "Нужен ключ в настройках", fS, dim, ac.X + B(53), ac.Y + B(28));
            RectangleF ab = new RectangleF(ac.Right - B(104), ac.Y + B(12), B(96), B(28)); Hit(ab, delegate { BigClose(); OpenChat(); });
            Pill(g, ab, Color.FromArgb(110, 90, 255), Color.FromArgb(70, 50, 210), "Открыть чат", fB, Hov(ab), "💬");
            y = ac.Bottom + B(6);

            // --- нижняя панель кнопок
            RectangleF tb = new RectangleF(x0, y, cw, all.Bottom - B(8) - y); Glass(g, tb, B(12), false);
            string[] tl = { "Буфер\nобмена", "Таймер", "Напоми-\nнания", "Настройки", "Больше" };
            Action[] ta = {
                delegate { OpenMenuPath("Буфер обмена"); },
                delegate { OpenMenuPath("Инструменты", "Таймер"); },
                delegate { AskReminder(); },
                delegate { OpenSettings(null); },
                delegate { OpenMenuPath(); } };
            float bw = tb.Width / 5;
            for (int i = 0; i < 5; i++)
            {
                RectangleF br = new RectangleF(tb.X + i * bw + B(2), tb.Y + B(3), bw - B(4), tb.Height - B(6)); Hit(br, ta[i]);
                if (Hov(br)) using (GraphicsPath hp = RoundRect(br.X, br.Y, br.Width, br.Height, B(9))) using (SolidBrush hbsh = new SolidBrush(Color.FromArgb(55, 255, 255, 255))) g.FillPath(hbsh, hp);
                if (i > 0) using (Pen sp = new Pen(Color.FromArgb(50, 255, 255, 255), 1)) g.DrawLine(sp, br.X - B(2), br.Y + B(8), br.X - B(2), br.Bottom - B(8));
                ToolIcon(g, i, br.X + br.Width / 2, br.Y + B(17), B(11));
                using (StringFormat sc = new StringFormat()) { sc.Alignment = StringAlignment.Center; STr(g, tl[i], fS, tx, new RectangleF(br.X - B(4), br.Y + B(32), br.Width + B(8), B(26)), sc); }
            }
        }
    }

    float[] NetSeries()
    {
        int n = Math.Min(netHistN, netHist.Length); float[] r = new float[Math.Max(n, 2)];
        for (int i = 0; i < n; i++) r[i] = netHist[(netHistN - n + i) % netHist.Length];
        return r;
    }

    void RateLine(Graphics g, float x, float y, string cur, float now, float prevOrPct, bool pct, Font fS, Font fB, Brush tx)
    {
        STr(g, cur, fB, tx, x, y);
        if (now <= 0) { STr(g, "—", fS, tx, x + B(13), y + B(1)); return; }
        string v = pct ? now.ToString("#,0", new CultureInfo("ru-RU")) : now.ToString("0.00", new CultureInfo("ru-RU"));
        STr(g, v, fS, tx, x + B(13), y + B(1));
        float d = pct ? prevOrPct : now - prevOrPct;
        if (!pct && prevOrPct <= 0) return;
        bool up = d >= 0;
        string ds = (up ? "▲ +" : "▼ ") + (pct ? d.ToString("0.00", CultureInfo.InvariantCulture) + "%" : d.ToString("0.00", CultureInfo.InvariantCulture));
        SizeF vs = g.MeasureString(v, fS);
        using (SolidBrush b = new SolidBrush(up ? Color.FromArgb(110, 230, 120) : Color.FromArgb(255, 110, 110))) STr(g, ds, fS, b, x + B(15) + vs.Width, y + B(1));
    }

    void Spark(Graphics g, RectangleF r, float[] v, Color c)
    {
        if (v == null || v.Length < 2) return;
        float mn = float.MaxValue, mx = float.MinValue; foreach (float f in v) { mn = Math.Min(mn, f); mx = Math.Max(mx, f); }
        if (mx - mn < 1e-6f) { mx = mn + 1; }
        PointF[] pts = new PointF[v.Length];
        for (int i = 0; i < v.Length; i++) pts[i] = new PointF(r.X + r.Width * i / (v.Length - 1), r.Bottom - (v[i] - mn) / (mx - mn) * r.Height * 0.85f - r.Height * 0.05f);
        using (GraphicsPath area = new GraphicsPath())
        {
            area.AddLines(pts); area.AddLine(pts[pts.Length - 1], new PointF(r.Right, r.Bottom)); area.AddLine(new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom)); area.CloseFigure();
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Color.FromArgb(150, c), Color.FromArgb(10, c), 90f)) g.FillPath(b, area);
        }
        using (Pen p = new Pen(c, B(1.6f))) { p.LineJoin = LineJoin.Round; g.DrawLines(p, pts); }
        if (v.Length <= 40) using (SolidBrush b = new SolidBrush(c)) for (int i = 0; i < pts.Length; i += Math.Max(1, pts.Length / 6)) g.FillEllipse(b, pts[i].X - B(2), pts[i].Y - B(2), B(4), B(4));
    }

    // ---------- значки ----------
    void Paw(Graphics g, float cx, float cy, float s, Color c)
    {
        using (SolidBrush b = new SolidBrush(c))
        {
            g.FillEllipse(b, cx - s * 0.55f, cy - s * 0.05f, s * 1.1f, s * 0.85f);
            float[][] t = { new float[] { -0.75f, -0.45f }, new float[] { -0.28f, -0.85f }, new float[] { 0.28f, -0.85f }, new float[] { 0.75f, -0.45f } };
            foreach (float[] p in t) g.FillEllipse(b, cx + p[0] * s - s * 0.2f, cy + p[1] * s - s * 0.25f, s * 0.4f, s * 0.5f);
        }
    }
    void Shield(Graphics g, float cx, float cy, float s, Color c, bool ok)
    {
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddBezier(cx, cy - s, cx + s * 0.5f, cy - s * 0.75f, cx + s * 0.9f, cy - s * 0.8f, cx + s * 0.9f, cy - s * 0.8f);
            p.AddBezier(cx + s * 0.9f, cy - s * 0.8f, cx + s * 0.95f, cy + s * 0.2f, cx + s * 0.5f, cy + s * 0.7f, cx, cy + s);
            p.AddBezier(cx, cy + s, cx - s * 0.5f, cy + s * 0.7f, cx - s * 0.95f, cy + s * 0.2f, cx - s * 0.9f, cy - s * 0.8f);
            p.AddBezier(cx - s * 0.9f, cy - s * 0.8f, cx - s * 0.9f, cy - s * 0.8f, cx - s * 0.5f, cy - s * 0.75f, cx, cy - s);
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(50, c))) g.FillEllipse(glow, cx - s * 1.3f, cy - s * 1.3f, s * 2.6f, s * 2.6f);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(cx - s, cy - s, s * 2, s * 2), ControlPaint.Light(c, 0.3f), ControlPaint.Dark(c, 0.15f), 90f)) g.FillPath(b, p);
            using (Pen pen = new Pen(Color.FromArgb(200, 255, 255, 255), B(1.2f))) g.DrawPath(pen, p);
        }
        using (Pen m = new Pen(Color.White, B(2.4f))) { m.StartCap = m.EndCap = LineCap.Round; if (ok) g.DrawLines(m, new PointF[] { new PointF(cx - s * 0.4f, cy), new PointF(cx - s * 0.08f, cy + s * 0.32f), new PointF(cx + s * 0.45f, cy - s * 0.3f) }); else { g.DrawLine(m, cx - s * 0.3f, cy - s * 0.3f, cx + s * 0.3f, cy + s * 0.3f); g.DrawLine(m, cx + s * 0.3f, cy - s * 0.3f, cx - s * 0.3f, cy + s * 0.3f); } }
    }
    void Wifi(Graphics g, float cx, float cy, float s, Color c)
    {
        using (Pen p = new Pen(c, s * 0.2f)) { p.StartCap = p.EndCap = LineCap.Round;
            for (int i = 1; i <= 3; i++) { float r = s * 0.38f * i; g.DrawArc(p, cx - r, cy - r, r * 2, r * 2, 225, 90); } }
        using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, cx - s * 0.16f, cy - s * 0.16f, s * 0.32f, s * 0.32f);
    }
    void Coins(Graphics g, float cx, float cy, float s)
    {
        Color g1 = Color.FromArgb(255, 214, 90), g2 = Color.FromArgb(214, 150, 30);
        float[][] st = { new float[] { -0.55f, 5 }, new float[] { 0.5f, 3 } };
        foreach (float[] col in st)
            for (int i = 0; i < (int)col[1]; i++)
            {
                float x = cx + col[0] * s - s * 0.55f, y = cy + s * 0.55f - i * s * 0.28f;
                using (SolidBrush side = new SolidBrush(g2)) g.FillRectangle(side, x, y, s * 1.1f, s * 0.22f);
                using (SolidBrush side2 = new SolidBrush(g2)) g.FillEllipse(side2, x, y + s * 0.1f, s * 1.1f, s * 0.3f);
                using (LinearGradientBrush top = new LinearGradientBrush(new RectangleF(x, y - s * 0.15f, s * 1.1f, s * 0.3f), g1, ControlPaint.Light(g1, 0.4f), 0f)) g.FillEllipse(top, x, y - s * 0.15f, s * 1.1f, s * 0.3f);
            }
    }
    void Robot(Graphics g, float cx, float cy, float s)
    {
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(50, 120, 160, 255))) g.FillEllipse(glow, cx - s * 1.3f, cy - s * 1.3f, s * 2.6f, s * 2.6f);
        RectangleF head = new RectangleF(cx - s * 0.85f, cy - s * 0.6f, s * 1.7f, s * 1.25f);
        using (GraphicsPath p = RoundRect(head.X, head.Y, head.Width, head.Height, s * 0.45f))
        using (LinearGradientBrush b = new LinearGradientBrush(head, Color.White, Color.FromArgb(190, 205, 235), 90f)) g.FillPath(b, p);
        RectangleF face = RectangleF.Inflate(head, -s * 0.2f, -s * 0.22f);
        using (GraphicsPath p = RoundRect(face.X, face.Y, face.Width, face.Height, s * 0.3f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 40, 80))) g.FillPath(b, p);
        using (SolidBrush e = new SolidBrush(Color.FromArgb(90, 210, 255))) { g.FillEllipse(e, cx - s * 0.42f, cy - s * 0.15f, s * 0.25f, s * 0.3f); g.FillEllipse(e, cx + s * 0.17f, cy - s * 0.15f, s * 0.25f, s * 0.3f); }
        using (Pen a = new Pen(Color.FromArgb(200, 210, 235), s * 0.12f)) g.DrawLine(a, cx, head.Y, cx, head.Y - s * 0.3f);
        using (SolidBrush t = new SolidBrush(Color.FromArgb(90, 210, 255))) g.FillEllipse(t, cx - s * 0.13f, head.Y - s * 0.45f, s * 0.26f, s * 0.26f);
    }
    void ToolIcon(Graphics g, int kind, float cx, float cy, float s)
    {
        Color c = Color.FromArgb(120, 190, 255);
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(40, c))) g.FillEllipse(glow, cx - s * 1.3f, cy - s * 1.3f, s * 2.6f, s * 2.6f);
        using (Pen p = new Pen(Color.White, B(1.6f))) using (SolidBrush b = new SolidBrush(c))
        {
            p.StartCap = p.EndCap = LineCap.Round;
            if (kind == 0) { using (GraphicsPath r = RoundRect(cx - s * 0.7f, cy - s * 0.85f, s * 1.4f, s * 1.75f, s * 0.2f)) { g.FillPath(b, r); g.DrawPath(p, r); } g.FillRectangle(Brushes.White, cx - s * 0.35f, cy - s, s * 0.7f, s * 0.3f); for (int i = 0; i < 3; i++) g.DrawLine(p, cx - s * 0.4f, cy - s * 0.2f + i * s * 0.35f, cx + s * 0.4f, cy - s * 0.2f + i * s * 0.35f); }
            else if (kind == 1) { g.FillEllipse(b, cx - s * 0.8f, cy - s * 0.7f, s * 1.6f, s * 1.6f); g.DrawEllipse(p, cx - s * 0.8f, cy - s * 0.7f, s * 1.6f, s * 1.6f); g.DrawLine(p, cx, cy + s * 0.1f, cx, cy - s * 0.4f); g.DrawLine(p, cx, cy + s * 0.1f, cx + s * 0.35f, cy + s * 0.25f); g.DrawLine(p, cx - s * 0.25f, cy - s * 0.95f, cx + s * 0.25f, cy - s * 0.95f); }
            else if (kind == 2) { using (GraphicsPath r = RoundRect(cx - s * 0.75f, cy - s * 0.85f, s * 1.5f, s * 1.7f, s * 0.25f)) { g.FillPath(b, r); g.DrawPath(p, r); } for (int i = 0; i < 3; i++) { float yy = cy - s * 0.4f + i * s * 0.4f; g.FillEllipse(Brushes.White, cx - s * 0.48f, yy - s * 0.08f, s * 0.16f, s * 0.16f); g.DrawLine(p, cx - s * 0.15f, yy, cx + s * 0.45f, yy); } }
            else if (kind == 3) { for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.FillEllipse(b, cx + (float)Math.Cos(a) * s * 0.75f - s * 0.22f, cy + (float)Math.Sin(a) * s * 0.75f - s * 0.22f, s * 0.44f, s * 0.44f); } g.FillEllipse(b, cx - s * 0.7f, cy - s * 0.7f, s * 1.4f, s * 1.4f); g.FillEllipse(Brushes.White, cx - s * 0.28f, cy - s * 0.28f, s * 0.56f, s * 0.56f); }
            else { for (int i = 0; i < 4; i++) { float xx = cx - s * 0.8f + (i % 2) * s * 0.9f, yy = cy - s * 0.8f + (i / 2) * s * 0.9f; using (GraphicsPath r = RoundRect(xx, yy, s * 0.7f, s * 0.7f, s * 0.15f)) { g.FillPath(b, r); g.DrawPath(p, r); } } }
        }
    }
    void Flag(Graphics g, RectangleF r, string country)
    {
        Color[] h = null; bool vert = false; Color crossBg = Color.Empty, cross = Color.Empty;
        switch (country)
        {
            case "Нидерланды": h = new Color[] { Color.FromArgb(174, 28, 40), Color.White, Color.FromArgb(33, 70, 139) }; break;
            case "Россия": h = new Color[] { Color.White, Color.FromArgb(0, 57, 166), Color.FromArgb(213, 43, 30) }; break;
            case "Германия": h = new Color[] { Color.Black, Color.FromArgb(221, 0, 0), Color.FromArgb(255, 206, 0) }; break;
            case "Эстония": h = new Color[] { Color.FromArgb(0, 114, 206), Color.Black, Color.White }; break;
            case "Литва": h = new Color[] { Color.FromArgb(253, 185, 19), Color.FromArgb(0, 106, 68), Color.FromArgb(193, 39, 45) }; break;
            case "Латвия": h = new Color[] { Color.FromArgb(158, 48, 57), Color.White, Color.FromArgb(158, 48, 57) }; break;
            case "Польша": h = new Color[] { Color.White, Color.FromArgb(220, 20, 60) }; break;
            case "Украина": h = new Color[] { Color.FromArgb(0, 87, 183), Color.FromArgb(255, 215, 0) }; break;
            case "Армения": h = new Color[] { Color.FromArgb(217, 0, 18), Color.FromArgb(0, 51, 160), Color.FromArgb(242, 168, 0) }; break;
            case "Франция": h = new Color[] { Color.FromArgb(0, 35, 149), Color.White, Color.FromArgb(237, 41, 57) }; vert = true; break;
            case "Италия": h = new Color[] { Color.FromArgb(0, 146, 70), Color.White, Color.FromArgb(206, 43, 55) }; vert = true; break;
            case "Финляндия": crossBg = Color.White; cross = Color.FromArgb(0, 47, 108); break;
            case "Швеция": crossBg = Color.FromArgb(0, 106, 167); cross = Color.FromArgb(254, 204, 0); break;
            case "Норвегия": crossBg = Color.FromArgb(186, 12, 47); cross = Color.FromArgb(0, 32, 91); break;
            case "Дания": crossBg = Color.FromArgb(200, 16, 46); cross = Color.White; break;
            case "Турция": h = new Color[] { Color.FromArgb(227, 10, 23) }; break;
            case "Казахстан": h = new Color[] { Color.FromArgb(0, 175, 202) }; break;
            case "США": h = new Color[] { Color.FromArgb(178, 34, 52), Color.White, Color.FromArgb(178, 34, 52), Color.White, Color.FromArgb(178, 34, 52) }; break;
            case "Великобритания": h = new Color[] { Color.FromArgb(1, 33, 105) }; break;
        }
        using (GraphicsPath clip = RoundRect(r.X, r.Y, r.Width, r.Height, B(2)))
        {
            GraphicsState st = g.Save(); g.SetClip(clip);
            if (h != null)
                for (int i = 0; i < h.Length; i++)
                    using (SolidBrush b = new SolidBrush(h[i]))
                        if (vert) g.FillRectangle(b, r.X + r.Width * i / h.Length, r.Y, r.Width / h.Length + 1, r.Height);
                        else g.FillRectangle(b, r.X, r.Y + r.Height * i / h.Length, r.Width, r.Height / h.Length + 1);
            else if (crossBg != Color.Empty)
            {
                using (SolidBrush b = new SolidBrush(crossBg)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(cross)) { g.FillRectangle(b, r.X + r.Width * 0.3f, r.Y, r.Width * 0.16f, r.Height); g.FillRectangle(b, r.X, r.Y + r.Height * 0.42f, r.Width, r.Height * 0.18f); }
            }
            else using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 150, 190))) { g.FillRectangle(b, r); using (Pen p = new Pen(Color.White, 1)) { g.DrawEllipse(p, r.X + r.Width * 0.3f, r.Y + r.Height * 0.1f, r.Height * 0.8f, r.Height * 0.8f); } }
            if (country == "США") using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 59, 110))) g.FillRectangle(b, r.X, r.Y, r.Width * 0.45f, r.Height * 0.55f);
            if (country == "Великобритания") using (Pen p = new Pen(Color.White, r.Height * 0.25f)) { g.DrawLine(p, r.X, r.Y, r.Right, r.Bottom); g.DrawLine(p, r.Right, r.Y, r.X, r.Bottom); using (Pen p2 = new Pen(Color.FromArgb(200, 16, 46), r.Height * 0.22f)) { g.DrawLine(p2, r.X + r.Width / 2, r.Y, r.X + r.Width / 2, r.Bottom); g.DrawLine(p2, r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2); } }
            g.Restore(st);
        }
        using (GraphicsPath bp = RoundRect(r.X, r.Y, r.Width, r.Height, B(2))) using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255), 1)) g.DrawPath(p, bp);
    }

    static void CrashLog(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_crash.log"), DateTime.Now + "\r\n" + ex + "\r\n\r\n", Encoding.UTF8); } catch { }
    }

    public static void Start()
    {
        bool created;
        using (Mutex m = new Mutex(true, "VapePet_SingleInstance", out created))
        {
            if (!created) return;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);      // windows-1251 для ЦБ
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetDefaultFont(new Font(new FontFamily("Microsoft Sans Serif"), 8.25f));   // как было в .NET Framework — окна не «поедут»
            float dpi = 96;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX;
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object o, ThreadExceptionEventArgs e) { CrashLog(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object o, UnhandledExceptionEventArgs e) { CrashLog(e.ExceptionObject as Exception); };
            Application.Run(new VapePet(dpi));
        }
    }
}
