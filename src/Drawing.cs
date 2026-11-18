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
    // ================= рисование =================
    static Color Light(Color c, float t)
    {
        return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t));
    }
    static Color Dark(Color c, float k)
    {
        return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
    }
    static readonly Color InkC = Color.FromArgb(78, 54, 42);
    static Color Mix(Color a, Color b, float t) { return Color.FromArgb(a.A, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
    static Color Ink(Color c) { return c.GetBrightness() < 0.4f ? Color.FromArgb(46, 34, 32) : Mix(Dark(c, 0.42f), InkC, 0.6f); }
    static Color Alpha(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B); }

    static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        GraphicsPath p = new GraphicsPath();
        float d = Math.Min(r * 2, Math.Min(w, h));
        if (d <= 0.01f) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    RectangleF R(float ox, float by, float px, float py, float pw, float ph)
    {
        return new RectangleF(ox + U(px), by - U(py + ph), U(pw), U(ph));
    }

    // объёмная панель: градиент + мягкий контур
    void Panel(G g, Color c, float ox, float by, float px, float py, float pw, float ph, float rad, bool outline = true)
    {
        c = Mix(c, Color.FromArgb(c.A, 236, 214, 170), 0.14f);
        RectangleF r = R(ox, by, px, py, pw, ph);
        if (r.Width < 0.5f || r.Height < 0.5f) return;
        using (GraphicsPath path = RoundRect(r.X, r.Y, r.Width, r.Height, U(rad)))
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, path);
            if (r.Height > U(2.2f) && r.Width > U(2.2f))
            {
                GState st = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (GraphicsPath sp = new GraphicsPath())
                {
                    sp.AddRectangle(RectangleF.Inflate(r, 2, 2));
                    sp.AddEllipse(r.X - r.Width * 0.3f, r.Y - r.Height * 0.45f, r.Width * 1.22f, r.Height * 1.36f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(c.A * 130 / 255, Dark(c, 0.8f)))) g.FillPath(b, sp);
                }
                g.Restore(st);
            }
            if (outline)
                using (Pen p = new Pen(Color.FromArgb(c.A, Mix(Dark(c, 0.5f), InkC, 0.65f)), Math.Max(1.2f, U(0.55f))))
                { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
        }
    }
    void Wood(G g, Color c, float ox, float by, float px, float py, float pw, float ph, float rad)
    {
        Panel(g, c, ox, by, px, py, pw, ph, rad);
        RectangleF r = R(ox, by, px, py, pw, ph);
        if (r.Width < 3 || r.Height < 3) return;
        GState st = g.Save();
        using (GraphicsPath path = RoundRect(r.X, r.Y, r.Width, r.Height, U(rad))) g.SetClip(path, CombineMode.Intersect);
        int seed = (int)(px * 31 + py * 17 + pw * 7);
        bool vertical = r.Height > r.Width * 1.5f;
        using (Pen p = new Pen(Color.FromArgb(26, 90, 50, 20), Math.Max(1f, U(0.22f))))
        {
            int n = Math.Max(2, (int)((vertical ? pw : ph) / 1.6f));
            for (int i = 0; i < n; i++)
            {
                float k = (i + 0.5f) / n;
                float amp = U(0.35f) * (float)Math.Sin(seed * 0.7 + i * 1.9);
                if (vertical)
                {
                    float x = r.X + k * r.Width;
                    g.DrawBezier(p, x, r.Y, x + amp, r.Y + r.Height * 0.33f, x - amp, r.Y + r.Height * 0.66f, x + amp * 0.5f, r.Bottom);
                }
                else
                {
                    float y = r.Y + k * r.Height;
                    g.DrawBezier(p, r.X, y, r.X + r.Width * 0.33f, y + amp, r.X + r.Width * 0.66f, y - amp, r.Right, y + amp * 0.5f);
                }
            }
            if (seed % 3 == 0 && r.Width > U(8) && r.Height > U(3))
                g.DrawEllipse(p, r.X + r.Width * 0.6f, r.Y + r.Height * 0.35f, U(1.4f), U(0.7f));
        }
        g.Restore(st);
    }

    void Flat(G g, Color c, float ox, float by, float px, float py, float pw, float ph, float rad)
    {
        RectangleF r = R(ox, by, px, py, pw, ph);
        if (r.Width < 0.3f || r.Height < 0.3f) return;
        using (GraphicsPath path = RoundRect(r.X, r.Y, r.Width, r.Height, U(rad)))
        using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, path);
    }
    void Shadow(G g, float cx, float w)
    {
        using (SolidBrush b = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
            g.FillEllipse(b, cx - w / 2, Ground - U(1.6f), w, U(2.4f));
    }

    Bitmap hudBuf;
    int menuThrottleT;   // пока открыто меню — рисуем реже, чтобы меню не тормозило
    void RenderFrame()
    {
        if (Width <= 0 || Height <= 0) return;
        if (buf == null || buf.Width != Width || buf.Height != Height)
        {
            if (buf != null) buf.Dispose();
            buf = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        }
        long pf = prof ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        using (G g = G.On(buf))
        {
            g.Clear(Color.Transparent);
            DrawAll(g);
        }
        if (prof) { ProfAdd(1, pf); pf = System.Diagnostics.Stopwatch.GetTimestamp(); }
        Bitmap outB = buf;
        float n = NightK();
        if (IsFog) outB = ApplyFog(outB);
        if (n > 0.02f) outB = ApplyNight(outB, n);
        if (n > 0.02f) using (G g = G.On(outB)) { g.SmoothingMode = SmoothingMode.AntiAlias; DrawNightLights(g, n); }
        // погода и панель — слоем ПОД котом. Сам слой перерисовывается редко (2 раза в секунду или при изменении),
        // а в каждом кадре только склеивается с котом в небольшом прямоугольнике панели.
        if (hudBuf == null || hudBuf.Width != Width || hudBuf.Height != Height)
        {
            if (hudBuf != null) hudBuf.Dispose();
            hudBuf = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            hudDirty = true;
        }
        bool hudOn = !HudUnderNote() && !bigOpen;
        if (hudOn != hudWasOn) { hudWasOn = hudOn; hudDirty = true; }
        if (hudDirty || ++hudAge >= 15) RebuildHud(hudOn);
        using (Graphics g = Graphics.FromImage(hudBuf))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(outB, 0, 0);
            if (hudRect.Width > 0 && hudRect.Height > 0)
            {
                g.DrawImage(hudCache, hudRect, hudRect.X, hudRect.Y, hudRect.Width, hudRect.Height, GraphicsUnit.Pixel);
                g.CompositingMode = CompositingMode.SourceOver;
                g.DrawImage(outB, hudRect, hudRect.X, hudRect.Y, hudRect.Width, hudRect.Height, GraphicsUnit.Pixel);
            }
        }
        if (prof) { ProfAdd(2, pf); pf = System.Diagnostics.Stopwatch.GetTimestamp(); }
        PushLayered(hudBuf);
        if (prof) ProfAdd(3, pf);
    }

    Bitmap hudCache; Rectangle hudRect; int hudAge; bool hudDirty = true, hudWasOn;
    RectangleF panelCardRect;
    public void HudChanged() { hudDirty = true; }
    void RebuildHud(bool on)
    {
        hudDirty = false; hudAge = 0;
        if (hudCache == null || hudCache.Width != Width || hudCache.Height != Height)
        {
            if (hudCache != null) hudCache.Dispose();
            hudCache = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        }
        panelCardRect = RectangleF.Empty;
        using (G g = G.On(hudCache))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (on) { DrawWeatherWidget(g); DrawHud(g); } else panelHits.Clear();
        }
        RectangleF r = RectangleF.Empty;
        if (on && WxVisible) r = WidgetRect();
        if (on && !panelCardRect.IsEmpty) r = r.IsEmpty ? panelCardRect : RectangleF.Union(r, panelCardRect);
        Rectangle ri = Rectangle.Round(RectangleF.Inflate(r, 3, 3));
        ri.Intersect(new Rectangle(0, 0, Width, Height));
        hudRect = ri;
    }

    // ПРОФИЛЬ: создайте файл %APPDATA%\VapePet_profile.flag — раз в 10 с пишет время по этапам в VapePet_profile.log
    static readonly bool prof = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_profile.flag"));
    long[] profT = new long[6]; int profN;
    void ProfAdd(int i, long from) { profT[i] += System.Diagnostics.Stopwatch.GetTimestamp() - from; }
    void ProfTick()
    {
        if (++profN < 300) return;
        double k = 1000.0 / System.Diagnostics.Stopwatch.Frequency / profN;
        string line = DateTime.Now.ToString("HH:mm:ss") + " мс/кадр: логика " + (profT[0] * k).ToString("0.00") + ", котик " + (profT[1] * k).ToString("0.00") + ", панель+слой " + (profT[2] * k).ToString("0.00") + ", вывод " + (profT[3] * k).ToString("0.00") + ", окно " + Width + "x" + Height + "\r\n";
        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapePet_profile.log"), line, Encoding.UTF8); } catch { }
        profN = 0; for (int i = 0; i < profT.Length; i++) profT[i] = 0;
    }

    void DrawAll(G g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if (shakeT > 0) g.TranslateTransform((float)(rnd.NextDouble() - 0.5) * U(1.4f), (float)(rnd.NextDouble() - 0.5) * U(0.8f));

        if (mode != 7) DrawWeatherBack(g);
        if (mode == 7) { DrawBarBack(g); DrawBarSeats(g); }
        if (mode == 1 || mode == 6) DrawShop(g);
        if (mode == 2) DrawChill(g);
        if (mode == 3) DrawTidy(g);
        if (mode == 5) { if (omonPhase == 0) DrawShop(g); else DrawJailBack(g); if (vanState != 0) DrawPoliceVan(g); }
        if (mode == 4)
        {
            DrawPartyBack(g);
            foreach (Cat c in guests) if (c.Swimming) DrawCat(g, c);
            if (me.Swimming) DrawCat(g, me);
            DrawPoolFront(g);
        }

        if (mode != 7) DrawWeatherGround(g);
        DrawPickups(g);
        List<Cat> zl = new List<Cat>();
        foreach (Cat c in guests) if (!c.Swimming) zl.Add(c);
        if (!me.Swimming) zl.Add(me);
        for (int i = 1; i < zl.Count; i++) { Cat t = zl[i]; int j = i - 1; while (j >= 0 && zl[j].Z > t.Z) { zl[j + 1] = zl[j]; j--; } zl[j + 1] = t; }
        foreach (Cat c in zl) DrawCat(g, c);

        if (mode == 2 && pendingMode < 0 && me.Sitting) DrawHose(g);
        if (mode == 5 && omonPhase == 1) DrawJailFront(g);
        if (mode == 5 && sirenT > 0) DrawSiren(g);

        foreach (Flyer f in flyers)
        {
            float t = f.T / (float)f.Dur;
            float x = f.X0 + (f.X1 - f.X0) * t;
            float y = f.Y0 + (f.Y1 - f.Y0) * t - (float)Math.Sin(Math.PI * t) * U(6);
            DrawItem(g, f.Kind, f.Col, x, y);
        }

        foreach (Particle p in parts)
        {
            float k = 1f, fade = p.Max * 0.55f;
            if (p.Life > fade) k = 1f - (p.Life - fade) / (p.Max - fade);
            if (p.Kind == 0)
            {
                float fin = Math.Min(1f, p.Life / 6f);
                int a = (int)(170 * k * fin);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a / 2, 225, 228, 236)))
                    g.FillEllipse(b, p.X - p.R * 1.3f, p.Y - p.R * 1.3f, p.R * 2.6f, p.R * 2.6f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 246, 247, 250)))
                    g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
            }
            else if (p.Kind == 1)
            {
                using (SolidBrush b = new SolidBrush(Alpha(pal[p.Col], (int)(230 * k))))
                    g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
            }
            else if (p.Kind == 2 && k > 0.05f) DrawItem(g, 'M', p.Col, p.X, p.Y, (int)(255 * k));
            else if (p.Kind == 3 && k > 0.05f) DrawItem(g, 'H', p.Col, p.X, p.Y, (int)(255 * k));
            else if (p.Kind == 4 && k > 0.05f) DrawItem(g, 'S', 'Y', p.X, p.Y, (int)(255 * k), p.Life * 12);
            else if (p.Kind == 10)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(220 * k), 140, 200, 255)))
                    g.FillEllipse(b, p.X - p.R * 0.7f, p.Y - p.R, p.R * 1.4f, p.R * 2f);
            }
            else if (p.Kind == 5)
            {
                int a5 = (int)(150 * k * Math.Min(1f, p.Life / 5f));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a5, 190, 180, 160)))
                    g.FillEllipse(b, p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
            }
        }

        foreach (Particle p in parts)
            if (p.Kind == 8)
            {
                int ma = (int)(220 * Math.Max(0, 1 - p.Life / (float)p.Max * 0.6f));
                using (SolidBrush b = new SolidBrush(p.Life % 6 < 2 ? Color.FromArgb(ma, 200, 255, 200) : Color.FromArgb(ma, 40, 230, 90))) g.DrawString(p.Col.ToString(), fntMatrix, b, p.X, p.Y);
            }
        foreach (Particle p in parts)
            if (p.Kind == 9)
            {
                int qa = (int)(255 * Math.Max(0, 1 - p.Life / (float)p.Max));
                OutlinedText(g, "?", fnt, Color.FromArgb(qa, 255, 200, 80), Color.FromArgb(qa, 40, 30, 20), p.X, p.Y, 1.1f);
            }
        if (mode != 7) DrawWeatherFront(g);
        if (mode == 7) { DrawMemory(g); DrawVaporText(g); }
        DrawBigText(g);
        foreach (FText f in texts)
        {
            float k = f.Life < 6 ? f.Life / 6f : (f.Life > f.Max - 12 ? (f.Max - f.Life) / 12f : 1f);
            SizeF sz = g.MeasureString(f.Text, fnt);
            float w = sz.Width + U(2.4f), h = sz.Height + U(0.8f);
            float x = f.X - w / 2, y = f.Y - h;
            if (x < 2) x = 2; if (x + w > Width - 2) x = Width - 2 - w;
            using (GraphicsPath path = RoundRect(x, y, w, h, h / 2))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(248 * k), 255, 251, 238))) g.FillPath(b, path);
                using (Pen p = new Pen(Color.FromArgb((int)(220 * k), 78, 54, 42), Math.Max(1f, U(0.4f)))) g.DrawPath(p, path);
            }
            Color tc = pal.ContainsKey(f.Col) ? pal[f.Col] : pal['K'];
            using (SolidBrush b = new SolidBrush(Alpha(tc, (int)(255 * k))))
                g.DrawString(f.Text, fnt, b, x + U(1.2f), y + U(0.4f));
        }
    }

    // ---------- котик (на двух лапах) ----------
    void Limb(G g, PointF a, PointF b, Color fill, Color line, float w)
    {
        using (Pen p = new Pen(line, w + 0.55f)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawLine(p, a, b); }
        using (Pen p = new Pen(fill, w)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawLine(p, a, b); }
    }

    static PointF P(float x, float y) { return new PointF(x, y); }

    bool HasSleeves(int o) { return o == 1 || o == 3 || o == 4 || o == 6 || o == 9 || o == 10 || o == 11 || o == 12 || o == 13 || (o >= 14 && o != 16 && o != 17); }
    bool ShortSleeves(int o) { return o == 2 || o == 5 || o == 7 || o == 16 || o == 17; }
    int PantsType(int o) { if (o == 2 || o == 3 || o == 4 || o == 6 || o >= 10) return 2; if (o == 7) return 1; return 0; }

    // ---------- мультяшные детали ----------
    void TaperLimb(G g, PointF a, PointF b, float wa, float wb, Color fill, Color line, bool outline)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f) len = 0.01f;
        float nx = -dy / len, ny = dx / len;
        using (GraphicsPath p = new GraphicsPath())
        {
            p.FillMode = FillMode.Winding;
            p.AddPolygon(new PointF[] { P(a.X + nx * wa, a.Y + ny * wa), P(b.X + nx * wb, b.Y + ny * wb), P(b.X - nx * wb, b.Y - ny * wb), P(a.X - nx * wa, a.Y - ny * wa) });
            p.AddEllipse(a.X - wa, a.Y - wa, wa * 2, wa * 2);
            p.AddEllipse(b.X - wb, b.Y - wb, wb * 2, wb * 2);
            if (outline) using (Pen pen = new Pen(line, 1.1f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
            using (SolidBrush br = new SolidBrush(fill)) g.FillPath(br, p);
        }
    }

    void Mitten(G g, PointF p, Color fill, Color line, float r)
    {
        using (SolidBrush b = new SolidBrush(line)) g.FillEllipse(b, p.X - r - 0.42f, p.Y - r - 0.42f, (r + 0.42f) * 2, (r + 0.42f) * 2);
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(p.X - r, p.Y - r - 0.1f, r * 2, r * 2 + 0.2f), Light(fill, 0.06f), Dark(fill, 0.95f), 90f)) g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
        using (Pen tp = new Pen(Color.FromArgb(150, line), 0.18f)) { g.DrawLine(tp, p.X + r * 0.15f, p.Y + r * 0.2f, p.X + r * 0.25f, p.Y + r * 0.9f); g.DrawLine(tp, p.X + r * 0.6f, p.Y, p.X + r * 0.75f, p.Y + r * 0.7f); }
    }

    void Foot(G g, PointF p, Color fill, Color line)
    {
        RectangleF fr = new RectangleF(p.X - 1.2f, p.Y - 0.95f, 3.3f, 1.9f);
        using (SolidBrush b = new SolidBrush(line)) g.FillEllipse(b, fr.X - 0.42f, fr.Y - 0.42f, fr.Width + 0.84f, fr.Height + 0.84f);
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(fr.X, fr.Y - 0.1f, fr.Width, fr.Height + 0.2f), Light(fill, 0.2f), Dark(fill, 0.88f), 90f)) g.FillEllipse(b, fr);
        using (Pen tp = new Pen(Color.FromArgb(150, line), 0.18f)) { g.DrawLine(tp, fr.Right - 0.9f, fr.Y + 0.4f, fr.Right - 0.8f, fr.Bottom - 0.2f); g.DrawLine(tp, fr.Right - 1.7f, fr.Y + 0.3f, fr.Right - 1.6f, fr.Bottom - 0.2f); }
    }

    static PointF Bez(PointF a, PointF b, PointF c, PointF d, float t)
    {
        float m = 1 - t;
        return new PointF(m * m * m * a.X + 3 * m * m * t * b.X + 3 * m * t * t * c.X + t * t * t * d.X, m * m * m * a.Y + 3 * m * m * t * b.Y + 3 * m * t * t * c.Y + t * t * t * d.Y);
    }

    void FluffyTail(G g, Cat c, Color main, Color tip, Color line, float ts)
    {
        PointF a = P(-2.6f, -6.6f), b = P(-7.4f, -6.0f), cc = P(-8.4f + ts, -12.6f), d = P(-5.6f + ts * 1.2f, -15.8f);
        int n = 16;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                PointF p = Bez(a, b, cc, d, t);
                float r = 1.2f - 0.5f * t + (t > 0.82f ? (t - 0.82f) * 1.6f : 0);
                if (t > 0.95f) r *= 0.75f;
                if (pass == 0) { using (SolidBrush br = new SolidBrush(line)) g.FillEllipse(br, p.X - r - 0.42f, p.Y - r - 0.42f, (r + 0.42f) * 2, (r + 0.42f) * 2); }
                else
                {
                    Color col = t > 0.8f ? tip : (c.Stripes && ((int)(t * 9) % 2 == 1) ? Dark(main, 0.78f) : main);
                    using (SolidBrush br = new SolidBrush(col)) g.FillEllipse(br, p.X - r, p.Y - r, r * 2, r * 2);
                }
            }
    }

    // глаз в рисованной манере: emo 0 обычный, 1 радость, 2 злость, 3 испуг
    void ToonEye(G g, Cat c, float cx, float cy, float w, float h, int emo, bool blink, bool near, Color line, Color fur)
    {
        Color dk = Color.FromArgb(52, 36, 30);
        if (blink)
        {
            using (Pen ep = new Pen(dk, 0.5f)) { ep.StartCap = LineCap.Round; ep.EndCap = LineCap.Round; g.DrawArc(ep, cx - w / 2, cy - h * 0.15f, w, h * 0.5f, 15, 150); }
            return;
        }
        if (emo == 1)
        {
            using (Pen ep = new Pen(dk, 0.55f)) { ep.StartCap = LineCap.Round; ep.EndCap = LineCap.Round; g.DrawArc(ep, cx - w / 2, cy - h * 0.2f, w, h * 0.65f, 200, 140); }
            return;
        }
        if (emo == 3)
        {
            RectangleF er = new RectangleF(cx - w / 2, cy - h / 2, w, h);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, er);
            using (Pen lp = new Pen(dk, 0.36f)) g.DrawEllipse(lp, er);
            using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, cx - w * 0.16f, cy - h * 0.16f, w * 0.32f, h * 0.32f);
            return;
        }
        float ew = w * 0.74f, eh = h * 0.98f;
        using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, cx - ew / 2, cy - eh / 2, ew, eh);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(150, c.IrisC))) g.FillPie(b, cx - ew * 0.36f, cy - eh * 0.2f, ew * 0.72f, eh * 0.62f, 20, 140);
        using (SolidBrush b = new SolidBrush(Color.White))
        {
            g.FillEllipse(b, cx - ew * 0.36f, cy - eh * 0.4f, ew * 0.5f, ew * 0.54f);
            g.FillEllipse(b, cx + ew * 0.1f, cy + eh * 0.16f, ew * 0.24f, ew * 0.24f);
        }
        if (emo == 2)
        {
            float sgn = near ? -1 : 1;
            PointF[] lid = new PointF[] { P(cx - w * 0.6f, cy - h * 0.7f), P(cx + w * 0.6f, cy - h * 0.7f), P(cx + w * 0.6f, cy - h * (0.2f + 0.16f * sgn)), P(cx - w * 0.6f, cy - h * (0.2f - 0.16f * sgn)) };
            using (SolidBrush b = new SolidBrush(fur)) g.FillPolygon(b, lid);
            using (Pen lp = new Pen(dk, 0.5f)) { lp.StartCap = LineCap.Round; lp.EndCap = LineCap.Round; g.DrawLine(lp, lid[3], lid[2]); }
        }
    }

    void DrawCat(G g, Cat c)
    {
        GState gs = g.Save();
        try { DrawCatZ(g, c); }
        catch (Exception ex) { GuardLog("рисование кота", ex); }
        finally { g.Restore(gs); }
    }

    void DrawCatZ(G g, Cat c)
    {
        if (c.Z == 0) { DrawCatBase(g, c); return; }
        int g0 = Ground; float s0 = c.Scale, z0 = c.Z;
        Ground = g0 + (int)Math.Round(ZY(z0)); c.Scale = s0 * ZK(z0); c.Z = 0;
        try { DrawCatBase(g, c); } finally { Ground = g0; c.Scale = s0; c.Z = z0; }
    }

    // ================= рисованный котик: вид спереди, сбоку и со спины =================
    const float NK = 0.24f;
    static GraphicsPath NEll(float x, float y, float w, float h) { GraphicsPath p = new GraphicsPath(); p.AddEllipse(x, y, w, h); return p; }
    static GraphicsPath NPoly(params float[] v)
    {
        GraphicsPath p = new GraphicsPath(); PointF[] pt = new PointF[v.Length / 2];
        for (int i = 0; i < pt.Length; i++) pt[i] = new PointF(v[i * 2], v[i * 2 + 1]);
        p.AddPolygon(pt); return p;
    }
    // заливка + мягкая тень справа-снизу + тёплый контур
    void NBlob(G g, GraphicsPath p, Color fill, Color shade, Pen pen)
    {
        using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
        if (shade.A > 0)
        {
            GState st = g.Save();
            g.SetClip(p, CombineMode.Intersect);
            RectangleF r = p.GetBounds();
            using (GraphicsPath sp = new GraphicsPath())
            {
                sp.AddRectangle(RectangleF.Inflate(r, 4, 4));
                sp.AddEllipse(r.X - r.Width * 0.22f, r.Y - r.Height * 0.3f, r.Width * 1.1f, r.Height * 1.14f);
                using (SolidBrush b = new SolidBrush(shade)) g.FillPath(b, sp);
            }
            g.Restore(st);
        }
        if (pen != null) g.DrawPath(pen, p);
        p.Dispose();
    }

    void NTail(G g, Cat c, Color ink, float x, float y, float dir, float tw)
    {
        float tk = c.AngryT > 0 ? 1.45f : 1f;
        PointF p0 = new PointF(x, y), p1 = new PointF(x + dir * 22, y - 2), p2 = new PointF(x + dir * 30 + tw * 4, y - 26), p3 = new PointF(x + dir * 20 + tw * 7, y - 40);
        using (GraphicsPath p = new GraphicsPath())
        using (Pen o = new Pen(ink, 15.5f * tk))
        using (Pen f = new Pen(c.MainC, 9.6f * tk))
        {
            o.StartCap = LineCap.Round; o.EndCap = LineCap.Round; f.StartCap = LineCap.Round; f.EndCap = LineCap.Round;
            p.AddBezier(p0, p1, p2, p3);
            g.DrawPath(o, p); g.DrawPath(f, p);
            if (c.Stripes && !c.Bald)
                using (Pen sp = new Pen(Dark(c.MainC, 0.78f), 3.4f * tk))
                {
                    sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round;
                    float[] ts = { 0.34f, 0.52f, 0.7f, 0.86f };
                    foreach (float t in ts)
                    {
                        float u1 = 1 - t;
                        float bx = u1 * u1 * u1 * p0.X + 3 * u1 * u1 * t * p1.X + 3 * u1 * t * t * p2.X + t * t * t * p3.X;
                        float by = u1 * u1 * u1 * p0.Y + 3 * u1 * u1 * t * p1.Y + 3 * u1 * t * t * p2.Y + t * t * t * p3.Y;
                        float dx = 3 * u1 * u1 * (p1.X - p0.X) + 6 * u1 * t * (p2.X - p1.X) + 3 * t * t * (p3.X - p2.X);
                        float dy = 3 * u1 * u1 * (p1.Y - p0.Y) + 6 * u1 * t * (p2.Y - p1.Y) + 3 * t * t * (p3.Y - p2.Y);
                        float len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 0.01f) continue;
                        float nx = -dy / len * 4.2f * tk, ny = dx / len * 4.2f * tk;
                        g.DrawLine(sp, bx - nx, by - ny, bx + nx, by + ny);
                    }
                }
        }
    }

    void NToes(G g, Cat c, Pen thin, float cx, float dy)
    {
        using (SolidBrush b = new SolidBrush(Mix(c.MainC, c.BellyC, 0.55f))) g.FillEllipse(b, cx - 5.2f, -5.6f + dy, 10.4f, 5f);
        g.DrawLine(thin, cx - 1.8f, -3.6f + dy, cx - 1.8f, -1.2f + dy); g.DrawLine(thin, cx + 1.8f, -3.6f + dy, cx + 1.8f, -1.2f + dy);
    }

    void NEars(G g, Cat c, Pen pen, float dx)
    {
        int ph = (tick + c.Id * 97) % 260;
        float tw = ph < 7 ? (float)Math.Sin(ph / 7f * Math.PI) : 0;
        float droop = c.SweatT > 0 || c.Shake > 0 ? 1f : 0f;
        for (int k = -1; k <= 1; k += 2)
        {
            float tx = k * (3 * (k > 0 ? tw : 0) + 6 * droop), ty = 5 * (k > 0 ? tw : 0) + 9 * droop;
            NBlob(g, NPoly(dx + k * 28, -78, dx + k * 25 + tx, -108 + ty, dx + k * 5, -92), c.MainC, Color.Empty, pen);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(246, 172, 160))) using (GraphicsPath p = NPoly(dx + k * 24, -85, dx + k * 23 + tx * 0.8f, -101 + ty * 0.8f, dx + k * 12, -92)) g.FillPath(b, p);
        }
    }

    void NStripes(G g, Cat c, float dx)
    {
        if (!c.Stripes || c.Bald) return;
        using (Pen sp = new Pen(Dark(c.MainC, 0.8f), 3.6f))
        {
            sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round;
            g.DrawLine(sp, dx - 8, -95, dx - 6.5f, -86.5f); g.DrawLine(sp, dx, -96.5f, dx, -85.5f); g.DrawLine(sp, dx + 8, -95, dx + 6.5f, -86.5f);
            // по бокам головы
            g.DrawLine(sp, dx - 30.5f, -80, dx - 24.5f, -79); g.DrawLine(sp, dx - 30, -73.5f, dx - 25, -73.5f);
            g.DrawLine(sp, dx + 30.5f, -80, dx + 24.5f, -79); g.DrawLine(sp, dx + 30, -73.5f, dx + 25, -73.5f);
        }
    }

    // мордочка: emo 0 обычная, 1 радость, 2 злость, 3 испуг
    void NFace(G g, Cat c, Pen thin, float dx, int emo, bool blink)
    {
        Color dk = Color.FromArgb(50, 36, 30);
        using (SolidBrush b = new SolidBrush(c.BellyC)) g.FillEllipse(b, dx - 18, -73, 36, 23);
        bool puffing = c.Vape && c.PuffT > 55;
        if (puffing)
            using (SolidBrush b = new SolidBrush(c.BellyC))
            using (Pen cp = new Pen(Color.FromArgb(150, InkC), 1.2f))
                for (int k = -1; k <= 1; k += 2) { RectangleF cr = new RectangleF(dx + k * 17 - 8, -73, 16, 14); g.FillEllipse(b, cr); g.DrawArc(cp, cr, k > 0 ? -60 : 120, 120); }
        using (SolidBrush b = new SolidBrush(Color.FromArgb(puffing ? 160 : 110, 250, 130, 130))) { g.FillEllipse(b, dx - 26, -70, 10, 6.5f); g.FillEllipse(b, dx + 16, -70, 10, 6.5f); }
        float look = (c.Walking || c.ZMove) ? 0 : (float)Math.Round(Math.Sin((tick + c.Id * 53) * 0.017) * 1.3) * 1.3f;
        using (Pen ep = new Pen(dk, 2f))
        {
            ep.StartCap = LineCap.Round; ep.EndCap = LineCap.Round;
            for (int k = -1; k <= 1; k += 2)
            {
                float ex = dx + k * 14, ew = (dx > 0 && k > 0) ? 8.4f : 10.6f;
                if (blink) g.DrawArc(ep, ex - 4, -79, 8, 6, 0, 180);
                else if (emo == 1) g.DrawArc(ep, ex - 4.5f, -80, 9, 9, 180, 180);
                else if (emo == 3)
                {
                    g.FillEllipse(Brushes.White, ex - 5.2f, -84, 10.4f, 12.5f); g.DrawEllipse(thin, ex - 5.2f, -84, 10.4f, 12.5f);
                    using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, ex - 1.7f, -79.4f, 3.4f, 3.6f);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 16, 18))) g.FillEllipse(b, ex - ew / 2 + look, -85, ew, 13.5f);
                    g.FillEllipse(Brushes.White, ex - ew / 2 + 1.4f + look, -83.2f, ew * 0.46f, ew * 0.46f); g.FillEllipse(Brushes.White, ex + ew * 0.12f + look, -76.6f, ew * 0.2f, ew * 0.2f);
                    if (emo == 2) g.DrawLine(ep, ex + k * 6.5f, -90.5f, ex - k * 4.5f, -85);
                }
                g.DrawLine(thin, dx + k * 19, -65.5f, dx + k * 34, -68f); g.DrawLine(thin, dx + k * 19, -62f, dx + k * 33, -60.5f);
            }
        }
        using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 120, 120))) using (GraphicsPath p = NPoly(dx - 3, -67.5f, dx + 3, -67.5f, dx, -64)) g.FillPath(b, p);
        if (c.Vape)
        {
            bool on = c.PuffT > 55;
            GState vs = g.Save();
            g.TranslateTransform(dx + 1, -63.5f); g.RotateTransform(14);
            NBlob(g, RoundRect(0, -3.6f, 25, 7.2f, 2.4f), Color.FromArgb(62, 70, 86), Color.FromArgb(70, 10, 12, 20), thin);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillRectangle(b, 3, -2.6f, 17, 1.3f);
            if (on) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 120, 255, 255))) g.FillEllipse(b, 14, -7, 12, 12);
            using (SolidBrush b = new SolidBrush(on ? Color.FromArgb(150, 255, 255) : Color.FromArgb(70, 200, 220))) g.FillEllipse(b, 17.5f, -1.6f, 3.4f, 3.2f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 32, 40))) g.FillRectangle(b, 23.4f, -2.2f, 1.2f, 4.4f);
            g.Restore(vs);
        }
        else if (emo == 2) { using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 50, 56))) g.FillEllipse(b, dx - 4, -63.6f, 8, 5.6f); g.DrawEllipse(thin, dx - 4, -63.6f, 8, 5.6f); }
        else if (emo == 1) { using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 60, 70))) g.FillPie(b, dx - 5, -68, 10, 9, 0, 180); g.DrawArc(thin, dx - 5, -68, 10, 9, 0, 180); g.DrawLine(thin, dx - 5, -63.5f, dx + 5, -63.5f); }
        else if (emo == 3) g.DrawLines(thin, new PointF[] { P(dx - 5, -61.5f), P(dx - 2.5f, -63.5f), P(dx, -61.5f), P(dx + 2.5f, -63.5f), P(dx + 5, -61.5f) });
        else { g.DrawArc(thin, dx - 5, -66, 5, 4.4f, 0, 180); g.DrawArc(thin, dx, -66, 5, 4.4f, 0, 180); }
        if (c.Glasses || c.SmallShades)
        {
            float gw = c.Glasses ? 17 : 13, gh = c.Glasses ? 13 : 9, gy = c.Glasses ? -85 : -82;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 36, 30, 34)))
                for (int k = -1; k <= 1; k += 2) using (GraphicsPath p = RoundRect(dx + k * 13 - gw / 2, gy, gw, gh, 4)) g.FillPath(b, p);
            using (Pen p = new Pen(Color.FromArgb(36, 30, 34), 2f)) { g.DrawLine(p, dx - 5, gy + 4, dx + 5, gy + 4); g.DrawLine(p, dx - 13 - gw / 2, gy + 4, dx - 30, gy + 6); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) { g.FillEllipse(b, dx - 18, gy + 2, 5, 2.4f); g.FillEllipse(b, dx + 8, gy + 2, 5, 2.4f); }
        }
    }

    // старые костюмы надеваются на новое тело: рисуем их в прежних координатах, обрезая по силуэту
    int curView;
    void NHoodie(G g, Cat c, Pen pen, float bx, float by, float bw, float bh)
    {
        Color hc = c.C1, hl = Light(c.C1, 0.18f), hd = Dark(c.C1, 0.8f);
        float cx = bx + bw / 2;
        NBlob(g, NEll(bx, by, bw, bh), hc, Color.FromArgb(120, hd), pen);
        using (Pen thin = new Pen(Color.FromArgb(34, 26, 28), 1.8f))
        using (Pen seam = new Pen(hl, 1.6f))
        {
            thin.StartCap = LineCap.Round; thin.EndCap = LineCap.Round; seam.StartCap = LineCap.Round; seam.EndCap = LineCap.Round;
            if (curView == 2)
            {
                // со спины: капюшон
                NBlob(g, NPoly(cx - 17, by - 2, cx + 17, by - 2, cx + 11, by + 15, cx, by + 19, cx - 11, by + 15), hl, Color.FromArgb(110, hd), pen);
                g.DrawLine(seam, cx, by + 1, cx, by + 15);
                return;
            }
            // капюшон лежит на плечах
            NBlob(g, RoundRect(bx - 1, by - 4, bw + 2, 11, 5.5f), hl, Color.FromArgb(90, hd), pen);
            if (curView == 1)
            {
                // сбоку: передняя половина кармана и один шнурок
                using (GraphicsPath p = NPoly(cx - 2, by + bh * 0.52f, cx + 13, by + bh * 0.52f, cx + 16, by + bh * 0.84f, cx - 2, by + bh * 0.84f))
                { using (SolidBrush b = new SolidBrush(Dark(hc, 0.9f))) g.FillPath(b, p); g.DrawPath(thin, p); }
                g.DrawLine(seam, cx + 9, by + 5, cx + 10, by + 16);
                using (SolidBrush b = new SolidBrush(hl)) g.FillEllipse(b, cx + 8.6f, by + 15, 2.8f, 3.6f);
                return;
            }
            // карман-кенгуру
            using (GraphicsPath p = NPoly(cx - 14, by + bh * 0.52f, cx + 14, by + bh * 0.52f, cx + 17, by + bh * 0.84f, cx - 17, by + bh * 0.84f))
            { using (SolidBrush b = new SolidBrush(Dark(hc, 0.9f))) g.FillPath(b, p); g.DrawPath(thin, p); }
            g.DrawLine(seam, cx - 13, by + bh * 0.56f, cx + 13, by + bh * 0.56f);
            // шнурки
            g.DrawLine(seam, cx - 5, by + 5, cx - 6, by + 17); g.DrawLine(seam, cx + 5, by + 5, cx + 6, by + 17);
            using (SolidBrush b = new SolidBrush(hl)) { g.FillEllipse(b, cx - 7.4f, by + 16, 2.8f, 3.6f); g.FillEllipse(b, cx + 4.6f, by + 16, 2.8f, 3.6f); }
            // нашивка
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 50, 56))) g.FillRectangle(b, cx + 9, by + 11, 6, 4.6f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 236, 230))) g.FillRectangle(b, cx + 9.8f, by + 11.8f, 4.4f, 1.1f);
        }
    }

    void NOutfit(G g, Cat c, Pen pen, float bx, float by, float bw, float bh)
    {
        if (c.Outfit == 1) NHoodie(g, c, pen, bx, by, bw, bh);
        else if (c.Outfit != 0)
        {
            GState s = g.Save();
            using (GraphicsPath p = NEll(bx, by, bw, bh)) g.SetClip(p, CombineMode.Intersect);
            g.ScaleTransform(1f / NK, 1f / NK);
            g.TranslateTransform((bx + bw / 2) * NK, -8.6f); g.ScaleTransform(1.42f, 1.22f); g.TranslateTransform(0, 8.6f);
            DrawOutfitTorso(g, c, new RectangleF(-3.9f, -13.0f, 7.8f, 8.8f));
            g.Restore(s);
            g.DrawEllipse(pen, bx, by, bw, bh);
        }
        if (c.Chain) using (Pen cp = new Pen(Color.FromArgb(246, 200, 70), 2.2f)) g.DrawArc(cp, bx + bw / 2 - 11, by - 8, 22, 22, 20, 140);
        if (IsCold && c.Outfit != 11 && c.Outfit != 10)
        {
            GState s = g.Save();
            g.ScaleTransform(1f / NK, 1f / NK);
            g.TranslateTransform((bx + bw / 2) * NK, -13f); g.ScaleTransform(1.3f, 1.15f); g.TranslateTransform(0, 13f);
            DrawScarf(g, c);
            g.Restore(s);
        }
    }

    void NHead(G g, Cat c, float hx)
    {
        GState s = g.Save();
        g.ScaleTransform(1f / NK, 1f / NK);
        g.TranslateTransform(hx * NK, -17f); g.ScaleTransform(1.16f, 1.12f); g.TranslateTransform(-1.1f, 17f);
        {
            int hatSave = c.Hat; Color hatCSave = c.HatC;
            if (IsFreezing && c.Hat == 0 && c.Outfit != 10 && c.Outfit != 11) { c.Hat = 2; Color[] hc = { Color.FromArgb(220, 60, 70), Color.FromArgb(60, 120, 220), Color.FromArgb(250, 190, 60), Color.FromArgb(70, 170, 110), Color.FromArgb(170, 100, 220) }; c.HatC = hc[Math.Abs(c.Id) % hc.Length]; }
            DrawHat(g, c);
            c.Hat = hatSave; c.HatC = hatCSave;
            if (c == me && musicT > 0)
            {
                using (Pen hp = new Pen(Color.FromArgb(40, 40, 48), 0.8f)) g.DrawArc(hp, -4.6f, -26.0f, 11.0f, 11.0f, 195, 150);
                using (GraphicsPath p = RoundRect(-5.6f, -20.2f, 2.2f, 3.8f, 0.9f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 60, 70))) g.FillPath(b, p);
                using (GraphicsPath p = RoundRect(5.4f, -20.4f, 2.2f, 3.8f, 0.9f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 60, 70))) g.FillPath(b, p);
            }

        }
        g.Restore(s);
    }

    void DrawCatBase(G g, Cat c)
    {
        if (c.Hidden) return;
        bool low = c.Sitting || c.Swimming;
        if (c.Lying) Shadow(g, c.X + c.Dir * U(11) * c.Scale, U(26) * c.Scale);
        else if (!low) Shadow(g, c.X, U(12) * c.Scale * Math.Max(0.4f, 1 - c.Y / U(40)));

        GState st = g.Save();
        float shx = c.Shake > 0 ? (float)Math.Sin(tick * 2.1) * U(0.6f) : 0;
        g.TranslateTransform(c.X + shx, Ground - c.Y);
        g.ScaleTransform(c.Dir * u * c.Scale, u * c.Scale);
        if (c.Lying) { g.TranslateTransform(0, -4.2f); g.RotateTransform(90); }
        if (low) g.TranslateTransform(0, 3.5f);

        int view = (c.Lying || low) ? 1 : c.View; curView = view;
        Color fur = c.MainC, belly = c.BellyC, shadeC = Dark(fur, 0.84f);
        Color ink = Color.FromArgb(34, 26, 28);
        int o = c.Outfit; Color c1 = c.C1, c2 = c.C2;
        int pants = PantsType(o);
        Color pantsC = (o == 3) ? c1 : (o == 6 ? c1 : c2);
        bool walking = (c.Walking || c.ZMove) && !c.Jumping && !c.Stiff && !low && !c.Lying;
        int emo = c.AngryT > 0 ? 2 : (c.SweatT > 0 || c.Shake > 0 ? 3 : (c.HappyT > 0 || (c == me && musicT > 0) ? 1 : 0));
        float ph = (tick + c.Id * 10) * 0.42f;
        float sw = walking ? (float)Math.Sin(ph) : 0;
        float bob = walking ? Math.Abs((float)Math.Sin(ph)) * 3f : (float)Math.Sin(tick * 0.07f + c.Id) * 0.8f + 0.8f;
        float tw = (float)Math.Sin((tick + c.Id * 37) * (walking ? 0.2f : (c.HappyT > 0 ? 0.32f : 0.075f))) * (c.AngryT > 0 ? 1.6f : 1f) + (walking ? 0.4f : 0);
        bool blink = c.PuffT > 55 || (c.BlinkT > 0 && (c.BlinkT <= 6 ? true : (c.BlinkT > 13 || (c.BlinkT > 6 && c.BlinkT <= 9))));
        bool umb = WantsUmbrella(c), hold = c.Hold != ' ';
        Color armC = HasSleeves(o) ? c1 : fur, armD = Dark(armC, 0.84f);
        Color legC = pants == 2 ? pantsC : fur, legD = Dark(legC, 0.84f);
        bool reach = view == 2 && c == me && !c.Walking && !c.ZMove && !hold && ShopScene;
        if (o == 22) DrawCape(g, c);

        GState ks = g.Save();
        float sx = 1, sy = 1;
        if (!c.Lying)
        {
            float br = (float)Math.Sin(tick * 0.085f + c.Id);
            if (!walking && !c.Jumping) { sy += 0.018f * br; sx -= 0.012f * br; }
            if (walking) { float contact = 1 - Math.Abs((float)Math.Sin(ph)); sy -= 0.05f * contact; sx += 0.04f * contact; }
            if (c.Jumping) { float jt = Math.Min(1f, c.JT / (float)Math.Max(1, c.JDur)); float st2 = Math.Abs((float)Math.Cos(Math.PI * jt)); sy += 0.11f * st2; sx -= 0.07f * st2; }
            if (c.LandT > 0) { float lk = c.LandT / 8f; lk = lk * lk; sy -= 0.17f * lk; sx += 0.13f * lk; }
            if (c.HappyT > 0) { float hk = (float)Math.Sin(c.HappyT * 0.6f); sy += 0.05f * hk; sx -= 0.035f * hk; }
            if (c.StompT > 0) { float hk = Math.Abs((float)Math.Sin(c.StompT * 0.42f)); sy -= 0.07f * (1 - hk); sx += 0.05f * (1 - hk); }
            if (c.TurnT > 0) sx *= 1 - 0.32f * (float)Math.Sin(Math.PI * (1 - c.TurnT / 6f));
        }
        g.ScaleTransform(sx, sy);
        float headY = walking ? (float)Math.Sin(ph * 2 - 1.3f) * 2.2f : 0;
        float headR = c.Lying || low ? 0 : (walking ? (float)Math.Sin(ph) * 2.6f
            : (c.HappyT > 0 ? (float)Math.Sin(tick * 0.28f) * 8f
            : (c.AngryT > 0 ? (float)Math.Sin(tick * 0.9f) * 3f
            : (c.Shake > 0 || c.SweatT > 0 ? (float)Math.Sin(tick * 1.7f) * 2.5f
            : (float)Math.Sin((tick + c.Id * 41) * 0.022f) * 3.5f))));
        if (view == 2) headR *= 0.5f;
        g.ScaleTransform(NK, NK);
        using (Pen pen = new Pen(ink, 3.3f))
        using (Pen thin = new Pen(ink, 1.8f))
        {
            pen.LineJoin = LineJoin.Round; pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; thin.StartCap = LineCap.Round; thin.EndCap = LineCap.Round;
            Color sh = Color.FromArgb(150, shadeC), ash = Color.FromArgb(150, armD), lsh = Color.FromArgb(150, legD);
            float ll = Math.Max(0, sw) * 5, lr = Math.Max(0, -sw) * 5;
            if (view == 0)
            {
                NTail(g, c, ink, 16, -20, 1, tw);
                NBlob(g, RoundRect(-16, -17 - ll, 13, 17, 5.5f), legC, lsh, pen); NBlob(g, RoundRect(3, -17 - lr, 13, 17, 5.5f), legC, lsh, pen); NToes(g, c, thin, -9.5f, -ll); NToes(g, c, thin, 9.5f, -lr);
                g.TranslateTransform(0, -bob);
                bool pocket = c.Outfit == 1 && !walking && !hold && !umb && c.HappyT == 0 && !c.Cuffed && c.AngryT == 0;
                if (pocket) { NBlob(g, NEll(-27, -48, 12, 22), armC, ash, pen); NBlob(g, NEll(15, -48, 12, 22), armC, ash, pen); }
                else if (!c.Cuffed) NBlob(g, NEll(-30, -47 + sw * 2, 12, 21), armC, ash, pen);
                NBlob(g, NEll(-22, -52, 44, 43), fur, sh, pen);
                using (SolidBrush b = new SolidBrush(belly)) g.FillEllipse(b, -13, -44, 26, 31);
                NOutfit(g, c, pen, -22, -52, 44, 43);
                GState hs = g.Save(); g.TranslateTransform(0, -50 + headY); g.RotateTransform(headR); g.ScaleTransform(1.1f, 1.1f); g.TranslateTransform(0, 50); NEars(g, c, pen, 0); NBlob(g, NEll(-31, -97, 62, 50), fur, sh, pen); NStripes(g, c, 0);
                NFace(g, c, thin, 0, emo, blink);
                NHead(g, c, 0); g.Restore(hs);
                if (c.Cuffed) { }
                else if (umb || c.HappyT > 0) NBlob(g, NEll(18, -72 - (c.HappyT > 0 ? Math.Abs((float)Math.Sin(tick * 0.5)) * 4 : 0), 12, 25), armC, ash, pen);
                else if (hold) NBlob(g, NEll(17, -47, 13, 18), armC, ash, pen);
                else if (!pocket) NBlob(g, NEll(18, -47 - sw * 2, 12, 21), armC, ash, pen);
            }
            else if (view == 2)
            {
                NBlob(g, RoundRect(-16, -17 - ll, 13, 17, 5.5f), legC, lsh, pen); NBlob(g, RoundRect(3, -17 - lr, 13, 17, 5.5f), legC, lsh, pen); NToes(g, c, thin, -9.5f, -ll); NToes(g, c, thin, 9.5f, -lr);
                g.TranslateTransform(0, -bob);
                NBlob(g, NEll(-30, -47 + sw * 2, 12, 21), armC, ash, pen);
                if (reach) NBlob(g, NEll(18, -76, 12, 27), armC, ash, pen); else NBlob(g, NEll(18, -47 - sw * 2, 12, 21), armC, ash, pen);
                NBlob(g, NEll(-22, -52, 44, 43), fur, sh, pen);
                if (c.Stripes && o == 0) using (Pen sp = new Pen(shadeC, 3.2f)) { sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round; for (int i = -1; i <= 1; i++) g.DrawLine(sp, -9, -36 + i * 8, 9, -36 + i * 8); }
                NOutfit(g, c, pen, -22, -52, 44, 43);
                GState hs = g.Save(); g.TranslateTransform(0, -50 + headY); g.RotateTransform(headR); g.ScaleTransform(1.1f, 1.1f); g.TranslateTransform(0, 50); NEars(g, c, pen, 0); NBlob(g, NEll(-31, -97, 62, 50), fur, sh, pen); NStripes(g, c, 0);
                if (c.Stripes && !c.Bald) using (Pen sp = new Pen(shadeC, 3.2f)) { sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round; g.DrawLine(sp, -8, -72, 8, -72); g.DrawLine(sp, -6, -64, 6, -64); }
                NHead(g, c, 0); g.Restore(hs);
                NTail(g, c, ink, 0, -16, tw * 0.35f, tw);
            }
            else
            {
                NTail(g, c, ink, -14, -20, -1, tw);
                if (!c.Cuffed) { GState a1 = g.Save(); g.TranslateTransform(10, -45); g.RotateTransform(sw * 22); g.TranslateTransform(-10, 45); NBlob(g, NEll(4, -47, 12, 21), armD, Color.Empty, pen); g.Restore(a1); }
                if (low)
                {
                    NBlob(g, RoundRect(0, -24, 27, 12, 5.5f), legD, Color.Empty, pen); NBlob(g, NEll(22, -27, 9, 15), Dark(belly, 0.9f), Color.Empty, pen);
                    NBlob(g, RoundRect(3, -19, 27, 12, 5.5f), legC, lsh, pen); NBlob(g, NEll(25, -22, 9, 15), belly, Color.Empty, pen);
                }
                else
                {
                    NBlob(g, RoundRect(-3 - sw * 8, -17 - lr * 1.3f, 13, 17, 5.5f), legD, Color.Empty, pen); NBlob(g, RoundRect(-9 + sw * 8, -17 - ll * 1.3f, 13, 17, 5.5f), legC, lsh, pen);
                }
                g.TranslateTransform(0, -bob);
                NBlob(g, NEll(-19, -52, 40, 43), fur, sh, pen);
                using (SolidBrush b = new SolidBrush(belly)) g.FillEllipse(b, -2, -44, 20, 31);
                NOutfit(g, c, pen, -19, -52, 40, 43);
                GState hs = g.Save(); g.TranslateTransform(0, -50 + headY); g.RotateTransform(headR); g.ScaleTransform(1.1f, 1.1f); g.TranslateTransform(0, 50); NEars(g, c, pen, 5); NBlob(g, NEll(-26, -97, 62, 50), fur, sh, pen); NStripes(g, c, 3);
                NFace(g, c, thin, 11, emo, blink);
                NHead(g, c, 5); g.Restore(hs);
                if (c.Cuffed) NBlob(g, NEll(-27, -45, 12, 18), armC, ash, pen);
                else if (umb) NBlob(g, NEll(6, -72, 12, 27), armC, ash, pen);
                else if (hold) NBlob(g, NEll(7, -45, 17, 11.5f), armC, ash, pen);
                else if (c.HappyT > 0) NBlob(g, NEll(13, -73 - Math.Abs((float)Math.Sin(tick * 0.5)) * 4, 12, 26), armC, ash, pen);
                else if (c.AngryT > 0) NBlob(g, NEll(9, -51 + (float)Math.Sin(tick * 0.8) * 3, 19, 11.5f), armC, ash, pen);
                else { GState a2 = g.Save(); g.TranslateTransform(2, -45); g.RotateTransform(-sw * 22); g.TranslateTransform(-2, 45); NBlob(g, NEll(-4, -47, 12, 21), armC, ash, pen); g.Restore(a2); }
            }
        }
        g.Restore(ks);
        if (umb) { g.TranslateTransform(0, -bob * NK); DrawUmbrella(g, c); }
        g.Restore(st);

        if (c.Outfit == 11 && !c.Lying && fntTiny != null)
        {
            PointF tp = Local(c, 0.3f, -9.6f);
            using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString("ОМОН", fntTiny, b, tp.X, tp.Y, sf); }
        }

        if (c.AngryT > 0)
        {
            PointF ap = Local(c, 5.0f, -24.5f);
            float r = U(1.3f) * c.Scale * (1 + 0.15f * (float)Math.Sin(tick * 0.6));
            using (Pen p = new Pen(Color.FromArgb(235, 40, 50), Math.Max(1.2f, U(0.55f))))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                for (int q = 0; q < 4; q++)
                {
                    float ang = q * 90 + 45;
                    float cx = ap.X + (float)Math.Cos(ang * Math.PI / 180) * r, cy = ap.Y + (float)Math.Sin(ang * Math.PI / 180) * r;
                    g.DrawArc(p, cx - r * 0.7f, cy - r * 0.7f, r * 1.4f, r * 1.4f, ang + 90, 90);
                }
            }
        }
        if (c.SweatT > 0)
        {
            PointF sp = Local(c, -3.4f, -21.5f);
            float r = U(0.9f) * c.Scale;
            float drop = (60 - (c.SweatT % 60)) * 0.04f * u;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(sp.X, sp.Y - r * 1.8f + drop, sp.X + r * 1.1f, sp.Y + drop, sp.X + r, sp.Y + r + drop, sp.X, sp.Y + r + drop);
                p.AddBezier(sp.X, sp.Y + r + drop, sp.X - r, sp.Y + r + drop, sp.X - r * 1.1f, sp.Y + drop, sp.X, sp.Y - r * 1.8f + drop);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 120, 190, 255))) g.FillPath(b, p);
            }
        }

        if (c.Hold != ' ')
        {
            PointF pw = Paw(c);
            float hb = (c.Hold == 'J' && tick % 12 < 6) ? U(0.4f) : 0;
            DrawItem(g, c.Hold, c.HoldCol, pw.X, pw.Y + hb, 255, (c.Hold == 'W' || c.Hold == 'A' || c.Hold == 'Y') ? c.ItemRot * c.Dir : 0);
        }
    }

    static PointF Lerp(PointF a, PointF b, float t) { return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }

    void DrawEye(G g, Cat c, float x, float y, float w, float h, bool blink)
    {
        Color dk = Color.FromArgb(25, 22, 30);
        if (blink)
        {
            using (Pen ep = new Pen(dk, 0.45f)) { ep.StartCap = LineCap.Round; ep.EndCap = LineCap.Round; g.DrawArc(ep, x, y + h * 0.35f, w, h * 0.5f, 20, 140); }
            return;
        }
        RectangleF er = new RectangleF(x, y, w, h);
        using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, er);
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y - 0.1f, w, h + 0.2f), Light(c.IrisC, 0.35f), Dark(c.IrisC, 0.5f), 90f))
            g.FillEllipse(b, x + w * 0.1f, y + h * 0.12f, w * 0.8f, h * 0.8f);
        using (SolidBrush b = new SolidBrush(dk)) g.FillEllipse(b, x + w * 0.25f, y + h * 0.2f, w * 0.5f, h * 0.62f);
        using (SolidBrush b = new SolidBrush(Color.White))
        {
            g.FillEllipse(b, x + w * 0.45f, y + h * 0.14f, w * 0.38f, w * 0.38f);
            g.FillEllipse(b, x + w * 0.2f, y + h * 0.66f, w * 0.18f, w * 0.18f);
        }
    }

    void DrawOutfitTorso(G g, Cat c, RectangleF br)
    {
        int o = c.Outfit;
        if (o == 0) return;
        if (o >= 14) { using (Pen op0 = new Pen(Dark(c.C1, 0.55f), 0.35f)) { op0.LineJoin = LineJoin.Round; DrawCostumeTorso(g, c, o, c.C1, c.C2, op0); } return; }
        Color c1 = c.C1, c2 = c.C2, ln = Dark(c1, 0.55f);
        using (Pen op = new Pen(ln, 0.35f))
        {
            op.LineJoin = LineJoin.Round;
            if (o == 1 || o == 9 || o == 4 || o == 3 || o == 2 || o == 7)
            {
                float bottom = (o == 2 || o == 7) ? -5.6f : -4.6f;
                using (GraphicsPath p = RoundRect(-4.1f, -12.9f, 8.2f, bottom + 12.9f, 3.2f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.2f, -13f, 8.4f, 9f), Dark(c1, 0.82f), Light(c1, 0.12f), 0f)) g.FillPath(b, p);
                    GState cs = g.Save();
                    g.SetClip(p, CombineMode.Intersect);
                    if (o == 7) // гавайка: цветочки
                        for (int i = 0; i < 9; i++)
                        {
                            float fx = -3.6f + (i * 37 % 70) / 10f, fy = -12.4f + (i * 53 % 66) / 10f;
                            using (SolidBrush b = new SolidBrush(i % 2 == 0 ? c2 : Color.FromArgb(255, 245, 230))) g.FillEllipse(b, fx, fy, 1.2f, 1.2f);
                        }
                    if (o == 4) // две полосы спортивки
                        using (SolidBrush b = new SolidBrush(c2)) { g.FillRectangle(b, 1.6f, -13f, 0.45f, 9f); g.FillRectangle(b, 2.4f, -13f, 0.45f, 9f); }
                    if (o == 1) // карман худи
                        using (GraphicsPath pk = RoundRect(-1.0f, -8.4f, 4.6f, 2.6f, 0.8f)) { using (SolidBrush b = new SolidBrush(Dark(c1, 0.88f))) g.FillPath(b, pk); g.DrawPath(op, pk); }
                    if (o == 9) // кожанка: молния
                        using (Pen zp = new Pen(Color.FromArgb(200, 200, 210), 0.25f)) g.DrawLine(zp, 2.2f, -12.6f, 2.0f, -4.6f);
                    if (o == 3) // пиджак: рубашка и галстук
                    {
                        using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new PointF[] { P(0.6f, -13f), P(4.2f, -13f), P(2.6f, -8.6f) });
                        using (SolidBrush b = new SolidBrush(c2)) g.FillPolygon(b, new PointF[] { P(2.1f, -12.6f), P(2.9f, -12.6f), P(2.8f, -9.6f), P(2.5f, -9.0f), P(2.2f, -9.6f) });
                    }
                    g.Restore(cs);
                    g.DrawPath(op, p);
                }
                if (o == 1) // капюшон и шнурки
                {
                    using (GraphicsPath hp = new GraphicsPath())
                    {
                        hp.AddArc(-4.4f, -14.6f, 6.4f, 3.6f, 180, 180);
                        using (SolidBrush b = new SolidBrush(Dark(c1, 0.75f))) g.FillPath(b, hp);
                    }
                    using (Pen sp = new Pen(Color.White, 0.22f)) { g.DrawLine(sp, 2.2f, -12.4f, 2.0f, -10.2f); g.DrawLine(sp, 3.0f, -12.4f, 3.1f, -10.4f); }
                }
                if (o == 2 || o == 7) // ремень/шорты верх
                    using (SolidBrush b = new SolidBrush(Dark(c2, 0.7f))) g.FillRectangle(b, -3.6f, -5.8f, 7.2f, 0.7f);
            }
            if (o == 10 || o == 11)
            {
                using (GraphicsPath p = RoundRect(-4.1f, -12.9f, 8.2f, 8.3f, 3.2f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.2f, -13f, 8.4f, 9f), Dark(c1, 0.85f), Light(c1, 0.1f), 0f)) g.FillPath(b, p);
                    GState cs = g.Save();
                    g.SetClip(p, CombineMode.Intersect);
                    if (o == 10)
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 46)))
                            for (int i = 0; i < 5; i++) g.FillRectangle(b, -4.2f, -12.6f + i * 1.7f, 8.4f, 0.8f);
                    else
                    {
                        using (SolidBrush b = new SolidBrush(Light(c1, 0.12f))) { g.FillRectangle(b, -3.0f, -8.4f, 2.6f, 2.2f); g.FillRectangle(b, 0.8f, -8.4f, 2.6f, 2.2f); }
                        using (SolidBrush b = new SolidBrush(Dark(c1, 0.7f))) g.FillRectangle(b, -4.2f, -6.0f, 8.4f, 0.8f);
                    }
                    g.Restore(cs);
                    g.DrawPath(op, p);
                }
                if (o == 10) using (SolidBrush b = new SolidBrush(Color.White)) { g.FillRectangle(b, 1.2f, -11.4f, 2.2f, 1.2f); }
            }
            if (o == 12) // длинный чёрный плащ
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-3.8f, -13.0f), P(3.9f, -13.0f), P(4.6f, -8.0f), P(5.6f, -0.8f), P(-5.2f, -0.8f), P(-4.5f, -8.0f) });
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-5.3f, -13.1f, 11f, 12.5f), Color.FromArgb(48, 48, 56), Color.FromArgb(14, 14, 18), 0f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(5, 5, 8), 0.35f)) g.DrawPath(pp, p);
                }
                using (Pen sp = new Pen(Color.FromArgb(90, 90, 104), 0.25f))
                {
                    g.DrawLine(sp, 2.2f, -12.6f, 1.2f, -0.9f);
                    g.DrawLine(sp, 1.0f, -13.0f, 2.4f, -10.4f); g.DrawLine(sp, 3.8f, -12.8f, 2.6f, -10.4f);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillRectangle(b, -3.2f, -12.4f, 0.6f, 10f);
            }
            if (o == 13) // толстый вязаный свитер с горлом
            {
                using (GraphicsPath p = RoundRect(-4.3f, -13.2f, 8.6f, 8.8f, 3.0f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.4f, -13.3f, 8.8f, 9f), Dark(c1, 0.82f), Light(c1, 0.12f), 0f)) g.FillPath(b, p);
                    GState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                    using (Pen rp = new Pen(Color.FromArgb(70, Dark(c1, 0.5f)), 0.22f))
                        for (int i = 0; i < 9; i++) { float x = -4.0f + i * 1.0f; g.DrawLine(rp, x, -12.6f, x + 0.2f, -4.6f); }
                    using (Pen zp = new Pen(Color.FromArgb(80, Dark(c1, 0.55f)), 0.3f))
                        for (int i = 0; i < 4; i++) { float y = -10.6f + i * 1.6f; g.DrawLines(zp, new PointF[] { P(-4f, y), P(-2.6f, y - 0.5f), P(-1.2f, y), P(0.2f, y - 0.5f), P(1.6f, y), P(3.0f, y - 0.5f), P(4.4f, y) }); }
                    g.Restore(cs);
                    using (Pen pp = new Pen(Dark(c1, 0.55f), 0.35f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath p = RoundRect(-2.4f, -14.2f, 5.6f, 2.0f, 0.9f))
                {
                    using (SolidBrush b = new SolidBrush(Dark(c1, 0.9f))) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(c1, 0.55f), 0.3f)) g.DrawPath(pp, p);
                }
            }
            if (o == 5) // футболка + фартук продавца
            {
                using (GraphicsPath p = RoundRect(-4.1f, -12.9f, 8.2f, 7.4f, 3.2f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(245, 245, 248))) { g.FillPath(b, p); }
                using (GraphicsPath p = RoundRect(-0.6f, -12.0f, 4.6f, 8.6f, 0.8f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-0.7f, -12.1f, 4.8f, 8.8f), Light(c1, 0.12f), Dark(c1, 0.8f), 90f)) g.FillPath(b, p);
                    g.DrawPath(op, p);
                }
                using (Pen sp = new Pen(Dark(c1, 0.7f), 0.3f)) { g.DrawLine(sp, -0.4f, -11.6f, -2.6f, -13.0f); g.DrawLine(sp, -0.6f, -8.6f, -3.6f, -8.4f); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 80))) g.FillRectangle(b, 0.4f, -11.2f, 2.2f, 1.2f);
                using (Pen sp = new Pen(Color.FromArgb(120, 0, 0, 0), 0.15f)) g.DrawLine(sp, 0.7f, -10.6f, 2.3f, -10.6f);
                using (SolidBrush b = new SolidBrush(Dark(c1, 0.85f))) g.FillRectangle(b, 0.2f, -7.6f, 3.4f, 1.6f);
            }
            if (o == 6) // рабочий комбинезон
            {
                using (GraphicsPath p = RoundRect(-3.0f, -10.4f, 6.8f, 6.2f, 1.4f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.1f, -10.5f, 7f, 6.4f), Light(c1, 0.1f), Dark(c1, 0.8f), 90f)) g.FillPath(b, p);
                    g.DrawPath(op, p);
                }
                using (Pen sp = new Pen(c1, 0.8f)) { g.DrawLine(sp, -1.6f, -10.2f, -1.2f, -13.0f); g.DrawLine(sp, 2.6f, -10.2f, 2.4f, -13.0f); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 210, 60))) { g.FillEllipse(b, -1.7f, -10.4f, 0.7f, 0.7f); g.FillEllipse(b, 2.3f, -10.4f, 0.7f, 0.7f); }
                using (SolidBrush b = new SolidBrush(Dark(c1, 0.85f))) g.FillRectangle(b, -0.2f, -9.2f, 2.6f, 1.8f);
            }
            if (o == 8) // сарафан
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-2.6f, -12.6f), P(3.4f, -12.6f), P(5.0f, -3.4f), P(-4.4f, -3.4f) });
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.5f, -12.7f, 9.6f, 9.4f), Light(c1, 0.15f), Dark(c1, 0.8f), 90f)) g.FillPath(b, p);
                    g.DrawPath(op, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                    for (int i = 0; i < 5; i++) g.FillEllipse(b, -3.2f + i * 1.8f, -5.2f - (i % 2) * 2.4f, 0.8f, 0.8f);
            }
        }
    }

    void DrawHat(G g, Cat c)
    {
        if (c.Hat >= 7 && c.Hat <= 16) { DrawCostumeHat(g, c); return; }
        switch (c.Hat)
        {
            case 1: // кепка (козырьком назад если Seed чётный)
            {
                bool back = c.HatBack;
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-3.8f, -24.0f, 9.4f, 6.2f, 180, 180);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.9f, -24.1f, 9.6f, 3.4f), Light(c.HatC, 0.2f), Dark(c.HatC, 0.8f), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(c.HatC, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath v = RoundRect(back ? -7.0f : 4.4f, -21.4f, 3.6f, 1.0f, 0.5f))
                using (SolidBrush b = new SolidBrush(Dark(c.HatC, 0.75f))) g.FillPath(b, v);
                using (SolidBrush b = new SolidBrush(Dark(c.HatC, 0.6f))) g.FillEllipse(b, 0.4f, -24.4f, 0.8f, 0.6f);
                break;
            }
            case 2: // шапка-бини с помпоном
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-3.9f, -25.0f, 9.6f, 7.4f, 180, 180);
                    p.CloseFigure();
                    using (SolidBrush b = new SolidBrush(c.HatC)) g.FillPath(b, p);
                }
                using (GraphicsPath p = RoundRect(-4.2f, -22.0f, 10.2f, 1.6f, 0.7f)) using (SolidBrush b = new SolidBrush(Dark(c.HatC, 0.8f))) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(60, 0, 0, 0), 0.2f)) for (int i = 0; i < 6; i++) g.DrawLine(pp, -3.4f + i * 1.7f, -22.0f, -3.4f + i * 1.7f, -20.4f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 250, 250))) g.FillEllipse(b, -0.1f, -26.4f, 2.2f, 2.2f);
                break;
            case 3: // праздничный колпак
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-1.6f, -21.6f), P(3.6f, -21.6f), P(1.4f, -28.4f) });
                    using (SolidBrush b = new SolidBrush(c.HatC)) g.FillPath(b, p);
                    GState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                    using (Pen sp = new Pen(Color.FromArgb(230, 255, 255, 255), 0.6f)) for (int i = 0; i < 4; i++) g.DrawLine(sp, -2f, -22f - i * 1.8f, 5f, -23.5f - i * 1.8f);
                    g.Restore(cs);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 60))) g.FillEllipse(b, 0.7f, -29.2f, 1.5f, 1.5f);
                break;
            case 4: // строительная каска
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-3.9f, -25.0f, 9.8f, 7.6f, 180, 180);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4f, -25.1f, 10f, 4f), Color.FromArgb(255, 230, 90), Color.FromArgb(230, 170, 20), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(160, 110, 10), 0.3f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath p = RoundRect(-4.8f, -21.6f, 11.6f, 1.0f, 0.5f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 180, 30))) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillRectangle(b, 0.6f, -24.8f, 0.9f, 3.0f);
                break;
            case 6: // шлем ОМОН с забралом
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-4.4f, -25.4f, 10.8f, 8.6f, 180, 180);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.5f, -25.5f, 11f, 4.6f), Light(c.HatC, 0.25f), Dark(c.HatC, 0.8f), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(c.HatC, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath p = RoundRect(-0.6f, -21.4f, 7.4f, 5.2f, 1.6f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 150, 185, 220))) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(160, 60, 70, 90), 0.25f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 255, 255, 255))) g.FillRectangle(b, 3.6f, -20.8f, 0.6f, 3.4f);
                using (Pen pp = new Pen(Color.FromArgb(30, 30, 36), 0.3f)) g.DrawLine(pp, -3.4f, -21.0f, -1.8f, -14.4f);
                break;
            case 5: // бантик на ухе
                using (SolidBrush b = new SolidBrush(c.HatC))
                {
                    g.FillPolygon(b, new PointF[] { P(4.2f, -22.6f), P(2.6f, -23.8f), P(2.6f, -21.4f) });
                    g.FillPolygon(b, new PointF[] { P(4.2f, -22.6f), P(5.8f, -23.8f), P(5.8f, -21.4f) });
                    g.FillEllipse(b, 3.7f, -23.1f, 1.0f, 1.0f);
                }
                break;
        }
    }

    // ---------- предметы ----------
    void DrawItem(G g, char kind, char col, float cx, float cy, int alpha = 255, float rot = 0)
    {
        Color c = pal.ContainsKey(col) ? pal[col] : pal['R'];
        GState st = g.Save();
        g.TranslateTransform(cx, cy);
        if (rot != 0) g.RotateTransform(rot);
        g.ScaleTransform(u, u);
        using (Pen op = new Pen(Alpha(Mix(Dark(c, 0.45f), InkC, 0.65f), alpha), 0.4f))
        using (Pen dk = new Pen(Alpha(InkC, alpha), 0.4f))
        {
            switch (kind)
            {
                case 'B': // флакон
                    using (GraphicsPath p = RoundRect(-0.72f, -3.2f, 1.44f, 1.9f, 0.3f))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 74, 62, 64))) g.FillPath(b, p);
                        g.DrawPath(dk, p);
                    }
                    using (GraphicsPath p = RoundRect(-1.25f, -1.6f, 2.5f, 4.8f, 0.7f))
                    {
                        using (SolidBrush b = new SolidBrush(Alpha(c, alpha))) g.FillPath(b, p);
                        GState bs = g.Save();
                        g.SetClip(p, CombineMode.Intersect);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha * 70 / 255, 70, 30, 20))) g.FillRectangle(b, 0.35f, -1.7f, 1f, 5f);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha * 240 / 255, 255, 251, 238))) g.FillRectangle(b, -1.3f, 0.2f, 2.6f, 1.6f);
                        g.Restore(bs);
                        g.DrawPath(dk, p);
                    }
                    break;
                case 'D': // диск
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-2f, -2f, 4f, 4f), Color.FromArgb(235, 240, 250), Color.FromArgb(150, 190, 255), 45f))
                        g.FillEllipse(b, -1.9f, -1.9f, 3.8f, 3.8f);
                    using (Pen p = new Pen(Color.FromArgb(120, 130, 150), 0.25f)) g.DrawEllipse(p, -1.9f, -1.9f, 3.8f, 3.8f);
                    using (Pen p = new Pen(Color.FromArgb(120, 255, 160, 220), 0.25f)) g.DrawArc(p, -1.3f, -1.3f, 2.6f, 2.6f, 200, 100);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 205, 215))) g.FillEllipse(b, -0.7f, -0.7f, 1.4f, 1.4f);
                    using (SolidBrush b = new SolidBrush(pal['T'])) g.FillEllipse(b, -0.3f, -0.3f, 0.6f, 0.6f);
                    break;
                case 'X': // пачка табака
                    using (GraphicsPath p = RoundRect(-1.45f, -1.75f, 2.9f, 3.5f, 0.4f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.5f, -1.8f, 3f, 3.6f), Light(c, 0.2f), Dark(c, 0.82f), 90f)) g.FillPath(b, p);
                        g.DrawPath(op, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 255, 255))) g.FillEllipse(b, -1.0f, -0.55f, 2.0f, 1.1f);
                    using (Pen p = new Pen(Color.FromArgb(120, 0, 0, 0), 0.2f)) g.DrawLine(p, -1.3f, -1.1f, 1.3f, -1.1f);
                    break;
                case 'J': // геймпад
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddEllipse(-2.7f, -1.0f, 2.4f, 2.4f);
                        p.AddEllipse(0.3f, -1.0f, 2.4f, 2.4f);
                        p.AddRectangle(new RectangleF(-1.6f, -1.1f, 3.2f, 1.8f));
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 64, 78))) g.FillPath(b, p);
                    }
                    using (SolidBrush b = new SolidBrush(pal['R'])) g.FillEllipse(b, 1.1f, -0.7f, 0.6f, 0.6f);
                    using (SolidBrush b = new SolidBrush(pal['U'])) g.FillEllipse(b, 1.6f, -0.2f, 0.6f, 0.6f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 185, 195))) { g.FillRectangle(b, -1.9f, -0.5f, 1.2f, 0.4f); g.FillRectangle(b, -1.5f, -0.9f, 0.4f, 1.2f); }
                    break;
                case 'C': // коробка с игрой
                    using (GraphicsPath p = RoundRect(-1.4f, -1.9f, 2.8f, 3.8f, 0.3f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.5f, -2f, 3f, 4f), Color.FromArgb(70, 130, 240), Color.FromArgb(30, 70, 170), 90f)) g.FillPath(b, p);
                        g.DrawPath(dk, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 250, 210, 80))) g.FillRectangle(b, -1.0f, -0.6f, 2.0f, 1.2f);
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, -1.4f, -1.9f, 2.8f, 0.5f);
                    break;
                case 'V': // под-система
                    using (GraphicsPath p = RoundRect(-1.0f, -2.2f, 2.0f, 4.4f, 0.7f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.1f, -2.3f, 2.2f, 4.6f), Color.FromArgb(150, 90, 220), Color.FromArgb(70, 40, 120), 0f)) g.FillPath(b, p);
                        g.DrawPath(dk, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 50))) g.FillRectangle(b, -0.5f, -2.9f, 1.0f, 0.8f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255))) g.FillEllipse(b, -0.3f, 1.1f, 0.6f, 0.6f);
                    break;
                case 'Z': // заглушка
                    using (GraphicsPath p = RoundRect(-0.8f, -0.6f, 1.6f, 1.2f, 0.4f))
                    using (SolidBrush b = new SolidBrush(c)) { g.FillPath(b, p); g.DrawPath(op, p); }
                    break;
                case 'I': // ID-карта
                    using (GraphicsPath p = RoundRect(-2.0f, -1.3f, 4.0f, 2.6f, 0.35f))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(225, 240, 255))) g.FillPath(b, p);
                        using (Pen pp = new Pen(Color.FromArgb(80, 110, 160), 0.2f)) g.DrawPath(pp, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 170, 90))) g.FillRectangle(b, -1.6f, -0.8f, 1.2f, 1.5f);
                    using (Pen pp = new Pen(Color.FromArgb(120, 130, 160), 0.2f)) { g.DrawLine(pp, 0, -0.5f, 1.6f, -0.5f); g.DrawLine(pp, 0, 0.1f, 1.6f, 0.1f); g.DrawLine(pp, 0, 0.7f, 1.1f, 0.7f); }
                    break;
                case 'O': // монетка
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.5f, -1.5f, 3f, 3f), Color.FromArgb(255, 230, 120), Color.FromArgb(220, 160, 30), 45f))
                        g.FillEllipse(b, -1.4f, -1.4f, 2.8f, 2.8f);
                    using (Pen pp = new Pen(Color.FromArgb(170, 110, 20), 0.25f)) { g.DrawEllipse(pp, -1.4f, -1.4f, 2.8f, 2.8f); g.DrawEllipse(pp, -0.9f, -0.9f, 1.8f, 1.8f); }
                    break;
                case 'T': // планшет
                    using (GraphicsPath p = RoundRect(-2.4f, -1.7f, 4.8f, 3.4f, 0.4f))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 42, 50))) g.FillPath(b, p);
                    using (SolidBrush b = new SolidBrush(countShelf >= 0 && tick % 10 < 6 ? Color.FromArgb(150, 230, 160) : Color.FromArgb(120, 190, 255))) g.FillRectangle(b, -2.0f, -1.3f, 4.0f, 2.6f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 255, 255, 255))) { g.FillRectangle(b, -1.6f, -0.9f, 2.4f, 0.4f); g.FillRectangle(b, -1.6f, -0.2f, 3.0f, 0.4f); g.FillRectangle(b, -1.6f, 0.5f, 1.8f, 0.4f); }
                    break;
                case 'Q': // ведро попкорна
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddPolygon(new PointF[] { new PointF(-2.2f, -1.6f), new PointF(2.2f, -1.6f), new PointF(1.6f, 2.6f), new PointF(-1.6f, 2.6f) });
                        using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
                        GState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 50, 60))) for (int i = 0; i < 3; i++) g.FillRectangle(b, -2.0f + i * 1.5f, -1.6f, 0.75f, 4.4f);
                        g.Restore(cs);
                        using (Pen pp = new Pen(Color.FromArgb(150, 40, 40), 0.2f)) g.DrawPath(pp, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 245, 210)))
                        for (int i = 0; i < 6; i++) g.FillEllipse(b, -2.2f + i * 0.75f, -2.6f - (i % 2) * 0.5f, 1.1f, 1.1f);
                    break;
                case 'G': // касса (переносная)
                    using (GraphicsPath p = RoundRect(-3.4f, -2.0f, 6.8f, 4.0f, 0.6f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.5f, -2.1f, 7f, 4.2f), Color.FromArgb(120, 126, 142), Color.FromArgb(70, 74, 88), 90f)) g.FillPath(b, p);
                        using (Pen pp = new Pen(Color.FromArgb(40, 42, 50), 0.25f)) g.DrawPath(pp, p);
                    }
                    using (SolidBrush b = new SolidBrush(registerDown ? Color.FromArgb(40, 60, 50) : Color.FromArgb(110, 230, 150))) g.FillRectangle(b, -2.6f, -1.4f, 3.0f, 1.4f);
                    if (registerDown) using (Pen cp = new Pen(Color.White, 0.15f)) { g.DrawLine(cp, -2.4f, -1.3f, -1.0f, -0.2f); g.DrawLine(cp, -1.0f, -0.2f, 0.2f, -1.2f); }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 205, 215))) for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) g.FillRectangle(b, 1.0f + i * 0.9f, -1.4f + j * 0.9f, 0.6f, 0.6f);
                    break;
                case 'F': // телефон
                    using (GraphicsPath p = RoundRect(-0.9f, -1.6f, 1.8f, 3.2f, 0.4f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 30, 36))) g.FillPath(b, p);
                    using (SolidBrush b = new SolidBrush(tick % 30 < 25 ? Color.FromArgb(150, 210, 255) : Color.FromArgb(200, 240, 255))) g.FillRectangle(b, -0.7f, -1.3f, 1.4f, 2.4f);
                    break;
                case 'A': // молоток
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 100, 60))) g.FillRectangle(b, -0.25f, -3.0f, 0.5f, 5.2f);
                    using (GraphicsPath p = RoundRect(-1.6f, -4.0f, 3.2f, 1.4f, 0.3f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 125, 140))) g.FillPath(b, p);
                    break;
                case 'K': // коробка
                    using (GraphicsPath p = RoundRect(-2.4f, -2.0f, 4.8f, 4.0f, 0.3f))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-2.5f, -2.1f, 5f, 4.2f), Color.FromArgb(220, 178, 120), Color.FromArgb(170, 125, 75), 90f)) g.FillPath(b, p);
                        using (Pen pp = new Pen(Color.FromArgb(120, 85, 45), 0.25f)) g.DrawPath(pp, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 235, 215, 170))) g.FillRectangle(b, -0.4f, -2.0f, 0.8f, 4.0f);
                    break;
                case 'W': // метла
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 100, 60))) g.FillRectangle(b, -0.25f, -5.5f, 0.5f, 10f);
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddPolygon(new PointF[] { new PointF(-1.1f, 4.2f), new PointF(1.1f, 4.2f), new PointF(1.9f, 6.4f), new PointF(-1.9f, 6.4f) });
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 196, 96))) g.FillPath(b, p);
                        using (Pen pp = new Pen(Color.FromArgb(160, 120, 40), 0.18f))
                        {
                            g.DrawPath(pp, p);
                            for (int i = -2; i <= 2; i++) g.DrawLine(pp, i * 0.4f, 4.6f, i * 0.65f, 6.3f);
                        }
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 50, 50))) g.FillRectangle(b, -1.1f, 4.0f, 2.2f, 0.5f);
                    break;
                case 'Y': // швабра
                    using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(176, 130, 86), alpha))) g.FillRectangle(b, -0.28f, -9.5f, 0.56f, 15.5f);
                    g.DrawRectangle(dk, -0.28f, -9.5f, 0.56f, 15.5f);
                    using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(120, 130, 150), alpha))) g.FillRectangle(b, -1.6f, 5.6f, 3.2f, 1.0f);
                    g.DrawRectangle(dk, -1.6f, 5.6f, 3.2f, 1.0f);
                    using (Pen mp = new Pen(Alpha(Color.FromArgb(214, 218, 226), alpha), 0.55f))
                    using (Pen mo = new Pen(Alpha(InkC, alpha), 0.95f))
                    {
                        mp.StartCap = LineCap.Round; mp.EndCap = LineCap.Round; mo.StartCap = LineCap.Round; mo.EndCap = LineCap.Round;
                        for (int i = -3; i <= 3; i++) { float sw = (float)Math.Sin(tick * 0.3f + i) * 0.35f; g.DrawLine(mo, i * 0.45f, 6.6f, i * 0.62f + sw, 9.4f); }
                        for (int i = -3; i <= 3; i++) { float sw = (float)Math.Sin(tick * 0.3f + i) * 0.35f; g.DrawLine(mp, i * 0.45f, 6.6f, i * 0.62f + sw, 9.4f); }
                    }
                    break;
                case 'E': // стакан с напитком (бар)
                    DrawGlass(g, alpha);
                    break;
                case 'U': // кружка кофе
                    g.ScaleTransform(1.6f, 1.6f);
                    using (GraphicsPath p = RoundRect(-1.5f, -1.3f, 3.0f, 2.9f, 0.6f))
                    {
                        using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(250, 246, 238), alpha))) g.FillPath(b, p);
                        using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(214, 120, 110), alpha))) g.FillRectangle(b, -1.5f, 0.1f, 3.0f, 0.6f);
                        g.DrawPath(dk, p);
                    }
                    g.DrawArc(dk, 1.0f, -0.7f, 1.4f, 1.5f, -90, 180);
                    using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(120, 76, 50), alpha))) g.FillEllipse(b, -1.3f, -1.55f, 2.6f, 0.55f);
                    using (Pen sp = new Pen(Color.FromArgb(alpha * 150 / 255, 255, 255, 255), 0.3f))
                    {
                        sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round;
                        float w = (float)Math.Sin(tick * 0.15f) * 0.3f;
                        g.DrawBezier(sp, -0.5f, -1.9f, -0.9f + w, -2.6f, -0.1f - w, -3.0f, -0.5f, -3.7f);
                        g.DrawBezier(sp, 0.6f, -1.9f, 0.2f - w, -2.5f, 1.0f + w, -2.9f, 0.6f, -3.5f);
                    }
                    break;
                case 'S': // звёздочка
                    using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(255, 220, 60), alpha)))
                        g.FillPolygon(b, new PointF[] { new PointF(0, -1.2f), new PointF(0.3f, -0.3f), new PointF(1.2f, 0), new PointF(0.3f, 0.3f), new PointF(0, 1.2f), new PointF(-0.3f, 0.3f), new PointF(-1.2f, 0), new PointF(-0.3f, -0.3f) });
                    break;
                case 'H': // сердечко
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddBezier(0f, 1.6f, -2.2f, 0f, -1.6f, -1.8f, 0f, -0.7f);
                        p.AddBezier(0f, -0.7f, 1.6f, -1.8f, 2.2f, 0f, 0f, 1.6f);
                        using (SolidBrush b = new SolidBrush(Alpha(Color.FromArgb(255, 110, 150), alpha))) g.FillPath(b, p);
                    }
                    break;
                case 'M': // нота
                    using (SolidBrush b = new SolidBrush(Alpha(c, alpha)))
                    {
                        g.FillEllipse(b, -1.6f, 0.6f, 1.9f, 1.4f);
                        g.FillRectangle(b, 0.0f, -2.6f, 0.35f, 3.6f);
                        using (GraphicsPath p = new GraphicsPath())
                        {
                            p.AddBezier(0.2f, -2.6f, 1.4f, -2.0f, 1.8f, -1.2f, 1.2f, -0.2f);
                            p.AddBezier(1.2f, -0.2f, 1.4f, -1.1f, 0.9f, -1.6f, 0.2f, -1.8f);
                            g.FillPath(b, p);
                        }
                    }
                    break;
            }
        }
        g.Restore(st);
    }

    // ---------- магазин ----------
    void DrawShop(G g)
    {
        for (int s = 0; s < 2; s++)
        {
            float by = Ground + (1 - Appear(s)) * U(48);
            float ox = A + U(30 * s);
            GState zs = g.Save();
            g.TranslateTransform(ox + U(13), Ground + ZY(ShelfZ)); g.ScaleTransform(ZK(ShelfZ), ZK(ShelfZ)); g.TranslateTransform(-(ox + U(13)), -Ground);
            Shadow(g, ox + U(13), U(30));
            Wood(g, pal['B'], ox, by, 0, 0, 26, 40, 1.2f);
            using (LinearGradientBrush b = new LinearGradientBrush(R(ox, by, 2, 2, 22, 36), Color.FromArgb(92, 60, 40), Color.FromArgb(70, 44, 30), 90f))
                g.FillRectangle(b, R(ox, by, 2, 2, 22, 36));
            float[] boards = new float[] { 1, 13, 25 };
            for (int lv = 0; lv < 3; lv++)
            {
                for (int k = 0; k < 6; k++)
                {
                    char col = ItemColors[(s * 5 + lv * 3 + k * 7) % ItemColors.Length];
                    DrawItem(g, 'B', col, ox + U(3.8f + k * 3.7f), by - U(boards[lv] + 1.6f + 3.2f));
                }
                Wood(g, pal['B'], ox, by, 1, boards[lv], 24, 1.6f, 0.4f);
            }
            if (mode == 6 && countShelf == s && countIdx < 18)
            {
                int lv2 = countIdx / 6, k2 = countIdx % 6;
                float cx = ox + U(3.8f + k2 * 3.7f), cy = by - U(boards[lv2] + 1.6f + 3.2f);
                Glow(g, cx, cy, U(3.2f), U(4.2f), Color.FromArgb(255, 240, 120), 200);
                using (Pen p = new Pen(Color.FromArgb(255, 230, 60), Math.Max(1, U(0.35f)))) g.DrawEllipse(p, cx - U(1.9f), cy - U(3.6f), U(3.8f), U(7.2f));
            }
            // подсветка полки
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 240, 200))) g.FillRectangle(b, R(ox, by, 2, 30, 22, 8));
            Panel(g, pal['Y'], ox, by, 4, 40.6f, 18, 5, 1.2f);
            RectangleF sr = R(ox, by, 4, 40.6f, 18, 5);
            using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(pal['K'])) g.DrawString("VAPE", fntSign, b, sr, sf); }
            g.Restore(zs);
        }
        {
            float by = Ground + (1 - Appear(2)) * U(28);
            float ox = CounterX;
            Shadow(g, ox + U(12), U(28));
            Wood(g, pal['B'], ox, by, 0, 0, 24, 12.5f, 1);
            Flat(g, Color.FromArgb(60, 0, 0, 0), ox, by, 3, 3, 18, 0.6f, 0.3f);
            Flat(g, Color.FromArgb(60, 0, 0, 0), ox, by, 3, 7, 18, 0.6f, 0.3f);
            Wood(g, pal['b'], ox, by, -1, 12, 26, 2.4f, 0.8f);
            // трещины прилавка
            if (counterHP < 100 && (mode == 1))
            {
                int n = (int)((100 - counterHP) / 12);
                using (Pen cp = new Pen(Color.FromArgb(200, 50, 30, 20), Math.Max(1, U(0.3f))))
                    for (int i = 0; i < n; i++)
                    {
                        float x0 = 3 + (i * 7.3f) % 18, y0 = 2 + (i * 3.7f) % 9;
                        g.DrawLines(cp, new PointF[] { new PointF(ox + U(x0), by - U(y0)), new PointF(ox + U(x0 + 1.5f), by - U(y0 + 1.8f)), new PointF(ox + U(x0 + 0.8f), by - U(y0 + 3.2f)), new PointF(ox + U(x0 + 2.4f), by - U(y0 + 4.4f)) });
                    }
                if (counterHP <= 0) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 30, 20, 15))) g.FillPolygon(b, new PointF[] { new PointF(ox + U(16), by), new PointF(ox + U(24), by), new PointF(ox + U(24), by - U(7)), new PointF(ox + U(20), by - U(4)) });
            }
            // касса
            if (!registerDown && !registerFlying || mode != 1)
            {
            Panel(g, Color.FromArgb(90, 96, 112), ox, by, 3, 14, 14, 7.5f, 1.4f);
            Color scr = regFlash > 0 && tick % 6 < 3 ? Color.FromArgb(255, 230, 120) : Color.FromArgb(110, 230, 150);
            if (regFlash > 0) using (SolidBrush b = new SolidBrush(Color.FromArgb(80, scr))) g.FillEllipse(b, R(ox, by, 3, 15, 10, 7));
            Panel(g, scr, ox, by, 5, 17.3f, 6, 2.8f, 0.5f);
            for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
                    Flat(g, Color.FromArgb(200, 205, 215), ox, by, 12.3f + i * 1.8f, 15.6f + j * 1.8f, 1.2f, 1.2f, 0.4f);
            }
            if (registerDown && mode == 1) DrawItem(g, 'G', 'G', registerX, Ground - U(2.5f), 255, 18);
            Panel(g, Color.FromArgb(60, 62, 72), ox, by, 17.5f, 14, 4.5f, 3.4f, 0.6f);
            Flat(g, Color.FromArgb(120, 230, 150), ox, by, 18.4f, 16, 2.6f, 0.8f, 0.2f);
            if (bottleOnReg) DrawItem(g, 'B', regCol, ox + U(9), by - U(25.2f));
            if (devOnReg) DrawItem(g, 'V', 'G', ox + U(6.5f), by - U(24.5f));
            if (cardOnReg) DrawItem(g, 'I', 'u', ox + U(11.5f), by - U(23.6f));
        }
    }

    // ---------- диван ----------
    void DrawChill(G g)
    {
        {
            float by = Ground + (1 - Appear(0)) * U(24);
            float ox = A;
            Shadow(g, ox + U(15), U(32));
            Color sofa = Color.FromArgb(150, 100, 210);
            Flat(g, pal['b'], ox, by, 2, 0, 2, 3, 0.5f); Flat(g, pal['b'], ox, by, 26, 0, 2, 3, 0.5f);
            Panel(g, sofa, ox, by, 0, 2, 6, 18.5f, 2.6f);
            Panel(g, sofa, ox, by, 0, 2, 30, 7, 1.6f);
            Panel(g, Light(sofa, 0.15f), ox, by, 5, 7.6f, 20.5f, 2.8f, 1.3f);
            using (Pen p = new Pen(Color.FromArgb(50, 60, 30, 90), Math.Max(1, U(0.25f))))
                for (int i = 1; i < 3; i++) g.DrawLine(p, ox + U(5 + i * 6.8f), by - U(7.8f), ox + U(5 + i * 6.8f), by - U(10.2f));
            Panel(g, pal['Y'], ox, by, 4.2f, 10, 3.6f, 6, 1.6f);
            Panel(g, sofa, ox, by, 24.5f, 2, 5.5f, 11, 2.6f);
        }
        {
            float by = Ground + (1 - Appear(1)) * U(30);
            float ox = HookahX;
            Shadow(g, ox + U(4), U(9));
            RectangleF vr = R(ox, by, 0.4f, 0, 7.2f, 7.6f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 70, 130, 230))) g.FillEllipse(b, vr);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 150, 210, 255))) g.FillEllipse(b, R(ox, by, 1.2f, 0.6f, 5.6f, 3.6f));
            using (Pen p = new Pen(Color.FromArgb(40, 70, 140), Math.Max(1, U(0.35f)))) g.DrawEllipse(p, vr);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(b, R(ox, by, 1.6f, 3.6f, 1.2f, 2.6f));
            if (me.PuffT > 55)
                for (int k = 0; k < 4; k++)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 230, 245, 255)))
                        g.FillEllipse(b, R(ox, by, 2.6f + (k % 2) * 1.6f, 1 + ((tick / 2 + k * 3) % 5), 0.9f, 0.9f));
            Panel(g, Color.FromArgb(70, 130, 230), ox, by, 2.6f, 6.8f, 2.8f, 3, 0.9f);
            Panel(g, Color.FromArgb(190, 195, 205), ox, by, 3.3f, 9, 1.4f, 11, 0.6f);
            Panel(g, Color.FromArgb(190, 195, 205), ox, by, 0.4f, 18.4f, 7.2f, 1.6f, 0.8f);
            using (GraphicsPath p = new GraphicsPath())
            {
                RectangleF b2 = R(ox, by, 2, 20, 4, 3.6f);
                p.AddPolygon(new PointF[] { new PointF(b2.Left + U(0.8f), b2.Bottom), new PointF(b2.Right - U(0.8f), b2.Bottom), new PointF(b2.Right, b2.Top), new PointF(b2.Left, b2.Top) });
                using (SolidBrush b = new SolidBrush(Color.FromArgb(190, 96, 64))) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(110, 50, 30), Math.Max(1, U(0.3f)))) g.DrawPath(pp, p);
            }
            bool hot = me.PuffT > 55;
            if (hot) using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 255, 160, 60))) g.FillEllipse(b, R(ox, by, 0.6f, 21.6f, 6.8f, 4.4f));
            Flat(g, hot && tick % 8 < 5 ? Color.FromArgb(255, 210, 90) : Color.FromArgb(220, 70, 50), ox, by, 2.4f, 23.4f, 3.2f, 1.3f, 0.6f);
        }
        {
            float by = Ground + (1 - Appear(2)) * U(40);
            float ox = StandX;
            Shadow(g, ox + U(18), U(38));
            Wood(g, pal['B'], ox, by, 0, 0, 36, 9, 1);
            Flat(g, Color.FromArgb(60, 0, 0, 0), ox, by, 2, 4.2f, 32, 0.5f, 0.2f);
            Flat(g, Color.FromArgb(200, 225, 200, 160), ox, by, 9, 1.8f, 3, 0.8f, 0.4f);
            Flat(g, Color.FromArgb(200, 225, 200, 160), ox, by, 24, 1.8f, 3, 0.8f, 0.4f);
            // приставка
            Panel(g, Color.FromArgb(242, 242, 246), ox, by, 2, 9, 6, 12, 1.6f);
            Flat(g, Color.FromArgb(30, 32, 40), ox, by, 4.6f, 9.6f, 0.8f, 10.8f, 0.3f);
            if (consoleOn) using (SolidBrush b = new SolidBrush(Color.FromArgb(tick % 40 < 25 ? 140 : 70, 80, 160, 255))) g.FillEllipse(b, R(ox, by, 2.2f, 8.8f, 5.6f, 2.4f));
            Flat(g, consoleOn ? Color.FromArgb(90, 170, 255) : Color.FromArgb(160, 165, 175), ox, by, 3.4f, 9.8f, 3.2f, 0.4f, 0.2f);
            // телевизор
            Panel(g, Color.FromArgb(40, 44, 56), ox, by, 21, 9, 4.4f, 1.6f, 0.5f);
            if (consoleOn && tvState >= 2)
            {
                Color glow = tvState == 2 ? Color.FromArgb(90, 140, 255) : GameA[gameIdx];
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, glow))) g.FillEllipse(b, R(ox, by, 5, 6, 36, 28));
            }
            Panel(g, Color.FromArgb(34, 37, 48), ox, by, 9, 10.4f, 28, 18.6f, 1.2f);
            RectangleF scr = R(ox, by, 10, 11.4f, 26, 16.6f);
            GState gs = g.Save();
            using (GraphicsPath clip = RoundRect(scr.X, scr.Y, scr.Width, scr.Height, U(0.6f))) g.SetClip(clip);
            DrawTV(g, scr);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
                g.FillPolygon(b, new PointF[] { new PointF(scr.X + U(2), scr.Y), new PointF(scr.X + U(7), scr.Y), new PointF(scr.X + U(1), scr.Bottom), new PointF(scr.X - U(4), scr.Bottom) });
            g.Restore(gs);
            if (consoleOn && tvState >= 2) DrawGamePlate(g, ox + U(23), by - U(31.5f));
            if (caseVisible) DrawItem(g, 'C', 'U', ox + U(41.5f), by - U(1.9f));
        }
    }

    void DrawTV(G g, RectangleF r)
    {
        if (!consoleOn)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(28, 34, 52), Color.FromArgb(12, 14, 22), 90f)) g.FillRectangle(b, r);
            return;
        }
        if (tvState == 1)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 12, 20))) g.FillRectangle(b, r);
            float k = Math.Min(1f, tvT / 30f);
            float cr = r.Height * 0.22f * k;
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(200 * k), 90, 160, 255))) g.FillEllipse(b, r.X + r.Width / 2 - cr, r.Y + r.Height * 0.42f - cr, cr * 2, cr * 2);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 70, 90))) g.FillRectangle(b, r.X + r.Width * 0.25f, r.Y + r.Height * 0.78f, r.Width * 0.5f, U(0.8f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 190, 255))) g.FillRectangle(b, r.X + r.Width * 0.25f, r.Y + r.Height * 0.78f, r.Width * 0.5f * Math.Min(1f, tvT / 60f), U(0.8f));
            return;
        }
        if (tvState == 2)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(30, 40, 80), Color.FromArgb(10, 12, 30), 90f)) g.FillRectangle(b, r);
            for (int k = -1; k <= 1; k++)
            {
                int gi = (menuSel + k + Games.Length) % Games.Length;
                float w = r.Width * (k == 0 ? 0.32f : 0.24f), h = r.Height * (k == 0 ? 0.62f : 0.48f);
                float cx = r.X + r.Width / 2 + k * r.Width * 0.32f, cy = r.Y + r.Height * 0.47f;
                RectangleF tr = new RectangleF(cx - w / 2, cy - h / 2, w, h);
                using (GraphicsPath p = RoundRect(tr.X, tr.Y, tr.Width, tr.Height, U(0.5f)))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(tr.X, tr.Y - 1, tr.Width, tr.Height + 2), GameBg[gi], GameB[gi], 90f)) g.FillPath(b, p);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(220, GameA[gi]))) g.FillRectangle(b, tr.X + w * 0.15f, tr.Y + h * 0.62f, w * 0.7f, h * 0.12f);
                    if (k == 0) using (Pen pp = new Pen(Color.White, Math.Max(1, U(0.35f)))) g.DrawPath(pp, p);
                }
            }
            return;
        }
        if (tvState == 3)
        {
            Color a = GameA[gameIdx], b2 = GameB[gameIdx];
            using (LinearGradientBrush b = new LinearGradientBrush(r, GameBg[gameIdx], Dark(b2, 0.6f), 90f)) g.FillRectangle(b, r);
            float k = (tvT % 40) / 40f;
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * (1 - k)), a))) g.FillEllipse(b, r.X + r.Width / 2 - r.Width * k, r.Y + r.Height / 2 - r.Width * k, r.Width * 2 * k, r.Width * 2 * k);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(230, a))) g.FillRectangle(b, r.X + r.Width * 0.15f, r.Y + r.Height * 0.45f, r.Width * 0.7f, r.Height * 0.1f);
            return;
        }
        DrawGame(g, gameIdx, r, tvT);
    }

    void DrawGamePlate(G g, float cx, float bottom)
    {
        int gi = tvState == 2 ? menuSel : gameIdx;
        string text = Games[gi];
        Font f = logoFonts[gi];
        SizeF sz = g.MeasureString(text, f);
        float w = sz.Width + U(3), h = sz.Height + U(0.8f);
        float x = cx - w / 2, y = bottom - h;
        if (x < 2) x = 2; if (x + w > Width - 2) x = Width - 2 - w;
        int alpha = tvState == 3 ? 255 : 235;
        using (GraphicsPath p = RoundRect(x, y, w, h, U(1)))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y - 1, w, h + 2), Color.FromArgb(alpha, Light(GameBg[gi], 0.12f)), Color.FromArgb(alpha, GameBg[gi]), 90f)) g.FillPath(b, p);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x - 1, y, w + 2, h), GameA[gi], GameB[gi], 0f))
            using (Pen pp = new Pen(b, Math.Max(1, U(0.35f)))) g.DrawPath(pp, p);
        }
        if (tvState == 3)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60 + (int)(50 * Math.Sin(tick * 0.3)), GameA[gi]))) g.FillRectangle(b, x + U(1), y + h - U(0.8f), w - U(2), U(0.4f));
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y, w, h), GameA[gi], GameB[gi], 90f))
            g.DrawString(text, f, b, x + U(1.5f), y + U(0.4f));
        if (tvState == 2)
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.DrawString("выбор игры...", fntSign, b, x + U(1), y - U(4.2f));
    }

    // мини-сценки по мотивам игр (без настоящих логотипов и персонажей)
    void DrawGame(G g, int gi, RectangleF r, int t)
    {
        float X = r.X, Y = r.Y, W = r.Width, H = r.Height;
        Func<float, float> fx = delegate (float v) { return X + v * W; };
        Func<float, float> fy = delegate (float v) { return Y + v * H; };
        switch (gi)
        {
            case 0: // закатный неоновый город у моря
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(255, 110, 170), Color.FromArgb(255, 195, 110), 90f)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 235, 160))) g.FillEllipse(b, fx(0.58f), fy(0.22f), W * 0.24f, W * 0.24f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 40, 110)))
                {
                    float[] hs = { 0.35f, 0.5f, 0.28f, 0.6f, 0.4f, 0.55f, 0.3f };
                    for (int i = 0; i < hs.Length; i++) g.FillRectangle(b, fx(0.02f + i * 0.14f), fy(0.78f - hs[i]), W * 0.11f, H * hs[i]);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 120)))
                    for (int i = 0; i < 12; i++) g.FillRectangle(b, fx(0.04f + (i % 7) * 0.14f), fy(0.55f + (i / 7) * 0.1f), W * 0.02f, H * 0.03f);
                using (Pen p = new Pen(Color.FromArgb(50, 20, 60), Math.Max(1, U(0.5f))))
                {
                    g.DrawBezier(p, fx(0.12f), fy(0.8f), fx(0.13f), fy(0.6f), fx(0.1f), fy(0.45f), fx(0.08f), fy(0.35f));
                    for (int i = 0; i < 4; i++) g.DrawArc(p, fx(0.0f + i * 0.04f), fy(0.3f), W * 0.1f, H * 0.12f, 180 + i * 20, 70);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 30, 60))) g.FillRectangle(b, X, fy(0.8f), W, H * 0.2f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 120)))
                    for (int i = 0; i < 4; i++) g.FillRectangle(b, X + ((i * W * 0.3f - t * U(0.4f)) % W + W) % W, fy(0.89f), W * 0.1f, H * 0.025f);
                float carX = X + ((t * U(0.5f)) % (W + U(8))) - U(6);
                using (GraphicsPath p = RoundRect(carX, fy(0.8f), U(6), H * 0.1f, U(0.6f)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 80, 160))) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(20, 20, 20))) { g.FillEllipse(b, carX + U(0.8f), fy(0.87f), U(1.3f), U(1.3f)); g.FillEllipse(b, carX + U(4), fy(0.87f), U(1.3f), U(1.3f)); }
                break;
            }
            case 1: // тёмный коридор и фонарик
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(30, 14, 14), Color.FromArgb(5, 5, 5), 90f)) g.FillRectangle(b, r);
                using (Pen p = new Pen(Color.FromArgb(70, 110, 80, 80), 1))
                {
                    g.DrawLine(p, X, Y, fx(0.38f), fy(0.35f)); g.DrawLine(p, X + W, Y, fx(0.62f), fy(0.35f));
                    g.DrawLine(p, X, Y + H, fx(0.38f), fy(0.65f)); g.DrawLine(p, X + W, Y + H, fx(0.62f), fy(0.65f));
                    g.DrawRectangle(p, fx(0.38f), fy(0.35f), W * 0.24f, H * 0.3f);
                }
                bool flick = (t / 3) % 11 != 0;
                if (flick)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 240, 200)))
                        g.FillPolygon(b, new PointF[] { new PointF(fx(0.45f), Y), new PointF(fx(0.55f), Y), new PointF(fx(0.8f), Y + H), new PointF(fx(0.2f), Y + H) });
                float zk = 0.4f + ((t % 300) / 300f) * 0.6f;
                float zx = fx(0.5f), zy = fy(0.5f + 0.15f * zk);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 15, 10, 10)))
                {
                    g.FillEllipse(b, zx - U(1.8f) * zk, zy - U(3) * zk, U(3.6f) * zk, U(6) * zk);
                    g.FillEllipse(b, zx - U(1.1f) * zk, zy - U(5) * zk, U(2.2f) * zk, U(2.2f) * zk);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 220, 30, 30))) { g.FillEllipse(b, zx - U(0.6f) * zk, zy - U(4.3f) * zk, U(0.4f), U(0.4f)); g.FillEllipse(b, zx + U(0.2f) * zk, zy - U(4.3f) * zk, U(0.4f), U(0.4f)); }
                if (t % 160 < 6) using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 0, 0))) g.FillRectangle(b, r);
                break;
            }
            case 2: // тушь, горы, взмахи клинков
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 230, 218))) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(215, 50, 45))) g.FillEllipse(b, fx(0.66f), fy(0.12f), W * 0.16f, W * 0.16f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 40, 40, 45)))
                    g.FillPolygon(b, new PointF[] { new PointF(X, fy(0.7f)), new PointF(fx(0.25f), fy(0.3f)), new PointF(fx(0.45f), fy(0.62f)), new PointF(fx(0.7f), fy(0.38f)), new PointF(X + W, fy(0.7f)), new PointF(X + W, Y + H), new PointF(X, Y + H) });
                using (SolidBrush b = new SolidBrush(Color.FromArgb(25, 25, 28)))
                {
                    g.FillRectangle(b, X, fy(0.86f), W, H * 0.14f);
                    float dx = (float)Math.Sin(t * 0.08) * W * 0.06f;
                    g.FillEllipse(b, fx(0.3f) + dx, fy(0.6f), U(2.2f), U(4.5f)); g.FillEllipse(b, fx(0.3f) + dx + U(0.3f), fy(0.53f), U(1.6f), U(1.6f));
                    g.FillEllipse(b, fx(0.64f) - dx, fy(0.6f), U(2.2f), U(4.5f)); g.FillEllipse(b, fx(0.64f) - dx + U(0.3f), fy(0.53f), U(1.6f), U(1.6f));
                }
                int st = t % 50;
                if (st < 12)
                    using (Pen p = new Pen(Color.FromArgb(255 - st * 20, 200, 20, 20), Math.Max(1, U(0.6f))))
                    using (Pen p2 = new Pen(Color.FromArgb(255 - st * 20, 255, 255, 255), Math.Max(1, U(0.25f))))
                    {
                        g.DrawArc(p, fx(0.35f), fy(0.4f), W * 0.3f, H * 0.4f, 200, 140);
                        g.DrawArc(p2, fx(0.35f), fy(0.4f), W * 0.3f, H * 0.4f, 200, 140);
                    }
                break;
            }
            case 3: // гонка у снежной горы, сакура
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(110, 180, 255), Color.FromArgb(225, 240, 255), 90f)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 150, 190))) g.FillPolygon(b, new PointF[] { new PointF(fx(0.15f), fy(0.62f)), new PointF(fx(0.5f), fy(0.15f)), new PointF(fx(0.85f), fy(0.62f)) });
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new PointF[] { new PointF(fx(0.4f), fy(0.29f)), new PointF(fx(0.5f), fy(0.15f)), new PointF(fx(0.6f), fy(0.29f)), new PointF(fx(0.55f), fy(0.26f)), new PointF(fx(0.5f), fy(0.31f)), new PointF(fx(0.45f), fy(0.26f)) });
                using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 180, 100))) g.FillRectangle(b, X, fy(0.6f), W, H * 0.4f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 84, 96))) g.FillPolygon(b, new PointF[] { new PointF(fx(0.46f), fy(0.6f)), new PointF(fx(0.54f), fy(0.6f)), new PointF(fx(0.9f), Y + H), new PointF(fx(0.1f), Y + H) });
                using (SolidBrush b = new SolidBrush(Color.White))
                    for (int i = 0; i < 4; i++)
                    {
                        float k = ((t * 0.02f + i * 0.25f) % 1f);
                        float yy = fy(0.6f + 0.4f * k), ww = U(0.3f + 0.8f * k);
                        g.FillRectangle(b, fx(0.5f) - ww / 2, yy, ww, U(0.3f + 1.2f * k));
                    }
                float sway = (float)Math.Sin(t * 0.07) * W * 0.12f;
                using (GraphicsPath p = RoundRect(fx(0.4f) + sway, fy(0.78f), W * 0.2f, H * 0.12f, U(0.8f)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 140, 30))) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 50))) g.FillRectangle(b, fx(0.43f) + sway, fy(0.8f), W * 0.14f, H * 0.03f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 255, 170, 200)))
                    for (int i = 0; i < 7; i++)
                    {
                        float px = X + ((i * W * 0.17f + (float)Math.Sin(t * 0.05 + i) * U(1.5f)) % W);
                        float py = Y + ((t * U(0.15f) + i * H * 0.23f) % H);
                        g.FillEllipse(b, px, py, U(0.7f), U(0.5f));
                    }
                break;
            }
            case 4: // космос и голограммы
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(6, 8, 20))) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.White))
                    for (int i = 0; i < 18; i++) { float sx = fx((i * 37 % 100) / 100f), sy = fy((i * 61 % 70) / 100f); float sz = ((t / 10 + i) % 5 == 0) ? U(0.5f) : U(0.3f); g.FillEllipse(b, sx, sy, sz, sz); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 120, 220))) g.FillEllipse(b, fx(0.7f), fy(0.08f), W * 0.2f, W * 0.2f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 150, 160))) g.FillEllipse(b, fx(-0.3f), fy(0.72f), W * 1.6f, H);
                using (Pen p = new Pen(Color.FromArgb(90, 80, 230, 255), 1))
                    for (int i = 0; i < 5; i++) g.DrawLine(p, fx(0.1f + i * 0.2f), fy(0.25f), fx(0.1f + i * 0.2f), fy(0.7f));
                float hy = (float)Math.Sin(t * 0.08) * H * 0.05f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 80, 230, 255))) g.FillEllipse(b, fx(0.42f), fy(0.42f) + hy, W * 0.16f, H * 0.3f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 245, 255)))
                {
                    g.FillEllipse(b, fx(0.46f), fy(0.45f) + hy, W * 0.08f, W * 0.08f);
                    using (GraphicsPath p = RoundRect(fx(0.455f), fy(0.55f) + hy, W * 0.09f, H * 0.14f, U(0.5f))) g.FillPath(b, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 230, 255))) g.FillRectangle(b, fx(0.475f), fy(0.475f) + hy, W * 0.04f, H * 0.03f);
                break;
            }
            case 5: // ночной снежный лес, следы когтей
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(20, 30, 70), Color.FromArgb(60, 80, 130), 90f)) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 240, 250))) g.FillRectangle(b, X, fy(0.82f), W, H * 0.18f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(15, 35, 40)))
                    for (int i = 0; i < 6; i++)
                    {
                        float tx = fx(0.05f + i * 0.17f), th = H * (0.4f + (i % 3) * 0.12f);
                        g.FillPolygon(b, new PointF[] { new PointF(tx, fy(0.84f) - th), new PointF(tx - W * 0.07f, fy(0.84f)), new PointF(tx + W * 0.07f, fy(0.84f)) });
                    }
                using (SolidBrush b = new SolidBrush(Color.White))
                    for (int i = 0; i < 14; i++)
                    {
                        float px = X + ((i * W * 0.13f + (float)Math.Sin(t * 0.04 + i) * U(1)) % W);
                        float py = Y + ((t * U(0.12f) + i * H * 0.31f) % H);
                        g.FillEllipse(b, px, py, U(0.45f), U(0.45f));
                    }
                int ct = t % 90;
                if (ct < 18)
                    using (Pen p = new Pen(Color.FromArgb(255 - ct * 13, 230, 235, 245), Math.Max(1, U(0.45f))))
                        for (int i = 0; i < 3; i++) g.DrawLine(p, fx(0.3f + i * 0.12f), fy(0.2f), fx(0.45f + i * 0.12f), fy(0.75f));
                break;
            }
            case 6: // тоннель метро и свет поезда
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(10, 12, 10))) g.FillRectangle(b, r);
                float cx = fx(0.5f), cy = fy(0.45f);
                using (Pen p = new Pen(Color.FromArgb(60, 70, 60), 1))
                    for (int i = 1; i <= 5; i++) { float rr = W * 0.11f * i; g.DrawEllipse(p, cx - rr, cy - rr * 0.8f, rr * 2, rr * 1.6f); }
                using (Pen p = new Pen(Color.FromArgb(150, 140, 120), Math.Max(1, U(0.3f))))
                {
                    g.DrawLine(p, cx - U(0.6f), cy + U(1), fx(0.2f), Y + H); g.DrawLine(p, cx + U(0.6f), cy + U(1), fx(0.8f), Y + H);
                }
                float k = ((t % 200) / 200f);
                float lr = W * (0.03f + 0.35f * k * k);
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(80 + 150 * k), 255, 240, 190))) g.FillEllipse(b, cx - lr, cy - lr * 0.8f, lr * 2, lr * 1.6f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 250, 220))) g.FillEllipse(b, cx - lr * 0.3f, cy - lr * 0.25f, lr * 0.6f, lr * 0.5f);
                break;
            }
            default: // красно-чёрная геометрия
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 10, 10))) g.FillRectangle(b, r);
                GState gs = g.Save();
                g.TranslateTransform(fx(0.5f), fy(0.5f));
                using (Pen p = new Pen(Color.FromArgb(230, 40, 40), Math.Max(1, U(0.4f))))
                    for (int i = 0; i < 3; i++)
                    {
                        g.RotateTransform(t * 0.6f + i * 30);
                        float s = H * (0.25f + i * 0.13f);
                        g.DrawRectangle(p, -s / 2, -s / 2, s, s);
                        g.RotateTransform(-(t * 0.6f + i * 30));
                    }
                g.Restore(gs);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 200, 205)))
                    for (int i = 0; i < 4; i++) g.FillRectangle(b, fx(0.12f + i * 0.22f), fy(0.75f) + (float)Math.Sin(t * 0.06 + i) * H * 0.08f, W * 0.07f, H * 0.08f);
                if ((t / 4) % 17 == 0) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillRectangle(b, X, fy(0.3f + ((t / 4) % 5) * 0.1f), W, H * 0.05f);
                break;
            }
        }
    }

    void DrawHose(G g)
    {
        float hx = HookahX + U(0.8f), hy = Ground - U(4.2f);
        PointF m = Local(me, 3.6f, -15.3f);
        PointF[] pts = { new PointF(hx, hy), new PointF(hx - U(6), hy + U(3)), new PointF(m.X + U(3), m.Y + U(10)), m };
        using (Pen p = new Pen(Color.FromArgb(30, 60, 70), U(1.0f))) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawBezier(p, pts[0], pts[1], pts[2], pts[3]); }
        using (Pen p = new Pen(Color.FromArgb(70, 150, 160), U(0.45f))) g.DrawBezier(p, pts[0], pts[1], pts[2], pts[3]);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 205, 215))) g.FillEllipse(b, m.X - U(0.7f), m.Y - U(0.7f), U(1.4f), U(1.4f));
    }

    void DrawBox(G g, float bx, bool open)
    {
        float byb = Ground;

            Shadow(g, bx, U(16));
            Color cb = Color.FromArgb(206, 160, 104);
            if (open)
            {
                using (SolidBrush b = new SolidBrush(Dark(cb, 0.75f)))
                {
                    g.FillPolygon(b, new PointF[] { new PointF(bx - U(7), byb - U(10)), new PointF(bx - U(12), byb - U(14)), new PointF(bx - U(9), byb - U(15)), new PointF(bx - U(2), byb - U(10)) });
                    g.FillPolygon(b, new PointF[] { new PointF(bx + U(7), byb - U(10)), new PointF(bx + U(12), byb - U(14)), new PointF(bx + U(9), byb - U(15)), new PointF(bx + U(2), byb - U(10)) });
                }
            }
            Panel(g, cb, bx, byb, -7, 0, 14, 10, 0.5f);
            Flat(g, Color.FromArgb(140, 230, 210, 160), bx, byb, -1.2f, 0, 2.4f, 10, 0);
            Flat(g, Color.FromArgb(230, 255, 255, 255), bx, byb, -5.5f, 3, 3.6f, 2.4f, 0.3f);
            using (Pen p = new Pen(Color.FromArgb(180, 220, 50, 50), Math.Max(1, U(0.25f)))) { g.DrawLine(p, bx + U(2.5f), byb - U(3), bx + U(5.5f), byb - U(6)); g.DrawLine(p, bx + U(5.5f), byb - U(3), bx + U(2.5f), byb - U(6)); }
            if (open) Flat(g, Color.FromArgb(90, 60, 30), bx, byb, -6.5f, 9, 13, 1.2f, 0.3f);
            }

    // ---------- табак ----------
    void DrawTidy(G g)
    {
        if (carState != 0) DrawCar(g);
        foreach (float[] bxx in boxes) DrawBox(g, bxx[0], bxx[1] == 3);

        foreach (float[] d in dust)
        {
            float rr = U(d[1]);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 175, 165, 150)))
            {
                g.FillEllipse(b, d[0] - rr * 1.6f, Ground - rr * 0.9f, rr * 3.2f, rr * 1.0f);
                g.FillEllipse(b, d[0] - rr * 0.9f, Ground - rr * 1.4f, rr * 1.6f, rr * 1.3f);
                g.FillEllipse(b, d[0] - rr * 0.1f, Ground - rr * 1.1f, rr * 1.4f, rr * 1.0f);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 120, 110, 95))) g.FillEllipse(b, d[0] - rr * 0.5f, Ground - rr * 0.6f, rr * 0.6f, rr * 0.4f);
        }

        {
            float by = Ground + (1 - Appear(0)) * U(50);
            float ox = RackX;
            if (rackAngle < 30) Shadow(g, ox + U(17), U(38));
            else Shadow(g, ox + U(54), U(48));
            GState st = g.Save();
            if (Math.Abs(rackAngle) > 0.01f)
            {
                float px = ox + U(rackAngle >= 0 ? 34 : 0);
                g.TranslateTransform(px, by); g.RotateTransform(rackAngle); g.TranslateTransform(-px, -by);
            }
            using (LinearGradientBrush b = new LinearGradientBrush(R(ox, by, 1, 1, 32, 38), Color.FromArgb(96, 64, 42), Color.FromArgb(72, 46, 30), 90f))
                g.FillRectangle(b, R(ox, by, 1, 1, 32, 38));
            Wood(g, pal['B'], ox, by, 0, 0, 3, 41, 0.8f);
            Wood(g, pal['B'], ox, by, 31, 0, 3, 41, 0.8f);
            foreach (float bd in new float[] { 0, 13, 26, 38 }) Wood(g, pal['B'], ox, by, 0, bd, 34, 3, 0.8f);
            for (int s = 0; s < 15; s++)
            {
                if (slots[s] == ' ') continue;
                PointF sp = SlotPos(s);
                DrawItem(g, 'X', slots[s], sp.X, sp.Y + (by - Ground));
            }
            Panel(g, pal['D'], ox, by, 6, 41.4f, 22, 5.2f, 1.4f);
            RectangleF sr = R(ox, by, 6, 41.4f, 22, 5.2f);
            using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString("ТАБАК", fntSign, b, sr, sf); }
            g.Restore(st);
        }
        foreach (float[] p in pile)
            DrawItem(g, 'X', ItemColors[(int)p[2]], PackX(p), PackY(p));
    }

    // ---------- вечеринка ----------
    void DrawPartyBack(G g)
    {
        {
            float by = Ground + (1 - Appear(0)) * U(80);
            float ox = A;
            Shadow(g, ox + U(55), U(112));
            Panel(g, Color.FromArgb(140, 90, 70), ox, by, 80, 56, 8, 18, 0.6f);
            Panel(g, pal['E'], ox, by, 2, 0, 106, 46, 0.6f);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { new PointF(ox - U(3), by - U(45)), new PointF(ox + U(113), by - U(45)), new PointF(ox + U(92), by - U(70)), new PointF(ox + U(18), by - U(70)) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(ox, by - U(71), U(110), U(27)), Color.FromArgb(222, 94, 92), Color.FromArgb(160, 50, 56), 90f)) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(110, 34, 40), Math.Max(1, U(0.45f)))) g.DrawPath(pp, p);
            }
            using (Pen sp = new Pen(Color.FromArgb(60, 80, 20, 20), Math.Max(1, U(0.3f))))
                for (int i = 1; i < 6; i++)
                {
                    float yy = 45 + i * 4.2f;
                    float inset = (yy - 45) * 21f / 25f;
                    g.DrawLine(sp, ox + U(-3 + inset), by - U(yy), ox + U(113 - inset), by - U(yy));
                }
            Color[] disco = { pal['L'], pal['Y'], pal['u'], pal['P'], pal['N'] };
            float[][] wins = new float[][] {
                new float[] { 10, 8 }, new float[] { 78, 8 }, new float[] { 10, 28 }, new float[] { 44, 28 }, new float[] { 78, 28 } };
            for (int i = 0; i < wins.Length; i++)
            {
                Color wc = (enterT > 40) ? disco[(tick / 15 + i * 2) % disco.Length] : Color.FromArgb(170, 215, 255);
                if (enterT > 40)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(60, wc))) g.FillEllipse(b, R(ox, by, wins[i][0] - 4, wins[i][1] - 4, 28, 21));
                Panel(g, Color.White, ox, by, wins[i][0] - 0.8f, wins[i][1] - 0.8f, 21.6f, 14.6f, 0.8f);
                using (LinearGradientBrush b = new LinearGradientBrush(R(ox, by, wins[i][0], wins[i][1], 20, 13), Light(wc, 0.35f), wc, 90f))
                    g.FillRectangle(b, R(ox, by, wins[i][0], wins[i][1], 20, 13));
                Flat(g, Color.White, ox, by, wins[i][0] + 9.4f, wins[i][1], 1.2f, 13, 0);
                Flat(g, Color.White, ox, by, wins[i][0], wins[i][1] + 5.9f, 20, 1.2f, 0);
            }
            if (doorOpen > 0) Panel(g, Color.FromArgb(40, 30, 30), ox, by, 48, 0, 14, 24, 1);
            else
            {
                Wood(g, Color.FromArgb(150, 92, 58), ox, by, 48, 0, 14, 24, 1);
                Flat(g, Color.FromArgb(50, 0, 0, 0), ox, by, 50, 13, 10, 8, 0.6f);
                Flat(g, Color.FromArgb(50, 0, 0, 0), ox, by, 50, 2.5f, 10, 8, 0.6f);
                Flat(g, pal['Y'], ox, by, 58.2f, 10.6f, 1.6f, 1.6f, 0.8f);
            }
            Panel(g, Color.FromArgb(200, 200, 210), ox, by, 45, 23.6f, 20, 1.8f, 0.6f);
            Panel(g, pal['N'], ox, by, 3, 0, 12, 6, 3);
            Panel(g, pal['N'], ox, by, 95, 0, 12, 6, 3);
        }
        {
            float by = Ground + (1 - Appear(1)) * U(24);
            float ox = A + U(114);
            Shadow(g, ox + U(5.5f), U(13));
            Panel(g, Color.FromArgb(58, 62, 76), ox, by, 0, 0, 11, 19, 1.4f);
            float pulse = (tick % 18 < 4) ? 0.5f : 0;
            RectangleF w1 = R(ox, by, 2 - pulse, 2.5f - pulse, 7 + pulse * 2, 7 + pulse * 2);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(25, 25, 32))) g.FillEllipse(b, w1);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 125, 140))) g.FillEllipse(b, R(ox, by, 4, 4.5f, 3, 3));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(25, 25, 32))) g.FillEllipse(b, R(ox, by, 3.5f, 12, 4, 4));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 125, 140))) g.FillEllipse(b, R(ox, by, 4.5f, 13, 2, 2));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(tick % 18 < 4 ? 255 : 120, 120, 255, 200))) g.FillEllipse(b, R(ox, by, 8.6f, 16.8f, 1, 1));
        }
        {
            float by = Ground + (1 - Appear(3)) * U(14);
            float ox = LoungerX;
            Shadow(g, ox + U(10), U(20));
            using (Pen p = new Pen(Color.FromArgb(150, 156, 170), U(0.6f))) { g.DrawLine(p, ox + U(1.5f), by, ox + U(3), by - U(3.5f)); g.DrawLine(p, ox + U(17), by, ox + U(15.5f), by - U(3.5f)); }
            Panel(g, Color.White, ox, by, 0, 3, 19, 2.4f, 1.1f);
            Panel(g, Color.White, ox, by, 15, 4, 4, 8.5f, 1.6f);
            for (int i = 0; i < 3; i++) Flat(g, Color.FromArgb(90, 160, 240), ox, by, 2.5f + i * 4.5f, 3.6f, 2, 1.2f, 0.3f);
            Flat(g, Color.FromArgb(90, 160, 240), ox, by, 15.8f, 7, 2.4f, 1.6f, 0.4f);
        }
        {
            float by = Ground + (1 - Appear(2)) * U(22);
            float ox = PoolX;
            Shadow(g, ox + U(30), U(66));
            using (Pen p = new Pen(Color.FromArgb(185, 190, 205), U(0.7f)))
            {
                g.DrawLine(p, ox - U(3.6f), by, ox - U(3.6f), by - U(17));
                g.DrawLine(p, ox - U(0.8f), by, ox - U(0.8f), by - U(17));
                for (int i = 0; i < 4; i++) g.DrawLine(p, ox - U(3.6f), by - U(2.5f + i * 4), ox - U(0.8f), by - U(2.5f + i * 4));
            }
            // задняя стенка — плитка
            using (LinearGradientBrush b = new LinearGradientBrush(R(ox, by, 2, 2, 56, 12), Color.FromArgb(215, 238, 250), Color.FromArgb(160, 210, 240), 90f))
                g.FillRectangle(b, R(ox, by, 2, 2, 56, 12));
            using (Pen p = new Pen(Color.FromArgb(50, 60, 120, 170), 1))
                for (int i = 1; i < 14; i++) g.DrawLine(p, ox + U(2 + i * 4), by - U(2), ox + U(2 + i * 4), by - U(14));
        }
    }

    void DrawPoolFront(G g)
    {
        float by = Ground + (1 - Appear(2)) * U(22);
        float ox = PoolX;
        RectangleF wr = R(ox, by, 2, 2, 56, 9.2f);
        using (GraphicsPath p = new GraphicsPath())
        {
            List<PointF> pts = new List<PointF>();
            for (int i = 0; i <= 28; i++)
            {
                float x = wr.X + wr.Width * i / 28f;
                float y = wr.Y + (float)Math.Sin(i * 0.9 + tick * 0.12) * U(0.35f);
                pts.Add(new PointF(x, y));
            }
            pts.Add(new PointF(wr.Right, wr.Bottom));
            pts.Add(new PointF(wr.X, wr.Bottom));
            p.AddPolygon(pts.ToArray());
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(wr.X, wr.Y - U(1), wr.Width, wr.Height + U(1)), Color.FromArgb(150, 110, 200, 255), Color.FromArgb(205, 40, 120, 220), 90f))
                g.FillPath(b, p);
            using (Pen wp = new Pen(Color.FromArgb(220, 235, 250, 255), Math.Max(1, U(0.45f))))
                g.DrawLines(wp, pts.GetRange(0, 29).ToArray());
        }
        for (int i = 0; i < 5; i++)
        {
            float gx = ox + U(6 + ((tick * 0.08f + i * 11) % 48));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(b, gx, by - U(6 + (i % 3) * 1.5f), U(3), U(0.5f));
        }
        Panel(g, Color.FromArgb(236, 240, 246), ox, by, 0, 0, 60, 2.6f, 0.8f);
        Panel(g, Color.FromArgb(236, 240, 246), ox, by, 0, 0, 2.8f, 14.2f, 1);
        Panel(g, Color.FromArgb(236, 240, 246), ox, by, 57.2f, 0, 2.8f, 14.2f, 1);
    }
}
