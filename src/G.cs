using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using SkiaSharp;

// Рисование для слоёв котика: тот же набор команд, что у Graphics (GDI+), но с двумя движками.
// G.UseSkia = true  — рисует SkiaSharp прямо в память Bitmap (быстрее, ровнее сглаживание);
// G.UseSkia = false — обычный GDI+ (запасной путь, включается в настройках).
// Bitmap из GDI+ и холст Skia делят одни и те же пиксели (32bpp PArgb = BGRA premul), так что
// остальная обвязка (ночь, туман, склейка с панелью, вывод в окно) не меняется.
public sealed class GState { internal GraphicsState Gdi; internal int SkCount; internal SKMatrix M; internal List<int> Clips; }

public sealed class G : IDisposable
{
    public static bool UseSkia = true;

    readonly Graphics gd;          // GDI+ движок
    readonly SKCanvas c;            // Skia движок
    readonly SKBitmap skb; readonly Bitmap lockedBmp; readonly BitmapData lockedData; readonly bool ownsGdi;
    List<int> clipSaves = new List<int>();   // уровни Save() под SetClip — чтобы ResetClip мог их снять

    SmoothingMode smooth = SmoothingMode.Default; TextRenderingHint trh = TextRenderingHint.SystemDefault;
    InterpolationMode interp = InterpolationMode.Default; PixelOffsetMode pom = PixelOffsetMode.Default;
    CompositingMode comp = CompositingMode.SourceOver; CompositingQuality compQ = CompositingQuality.Default;

    public G(Graphics g) { gd = g; }
    public static implicit operator G(Graphics g) { return g == null ? null : new G(g); }

    G(Bitmap b)
    {
        lockedBmp = b;
        lockedData = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        skb = new SKBitmap();
        skb.InstallPixels(new SKImageInfo(b.Width, b.Height, SKColorType.Bgra8888, SKAlphaType.Premul), lockedData.Scan0, lockedData.Stride);
        c = new SKCanvas(skb);
    }

    /// Холст поверх картинки: Skia или GDI+ — смотря что выбрано.
    public static G On(Bitmap b)
    {
        if (UseSkia && b.PixelFormat == PixelFormat.Format32bppPArgb)
        {
            try { return new G(b); } catch { }
        }
        return new G(Graphics.FromImage(b), true);
    }
    G(Graphics g, bool own) { gd = g; ownsGdi = own; }

    public bool IsSkia { get { return c != null; } }

    public void Dispose()
    {
        if (c != null)
        {
            try { c.Flush(); c.Dispose(); skb.Dispose(); } finally { lockedBmp.UnlockBits(lockedData); }
        }
        else if (ownsGdi) gd.Dispose();
    }

    // ---------------- режимы ----------------
    public SmoothingMode SmoothingMode { get { return gd != null ? gd.SmoothingMode : smooth; } set { if (gd != null) gd.SmoothingMode = value; else smooth = value; } }
    public TextRenderingHint TextRenderingHint { get { return gd != null ? gd.TextRenderingHint : trh; } set { if (gd != null) gd.TextRenderingHint = value; else trh = value; } }
    public InterpolationMode InterpolationMode { get { return gd != null ? gd.InterpolationMode : interp; } set { if (gd != null) gd.InterpolationMode = value; else interp = value; } }
    public PixelOffsetMode PixelOffsetMode { get { return gd != null ? gd.PixelOffsetMode : pom; } set { if (gd != null) gd.PixelOffsetMode = value; else pom = value; } }
    public CompositingMode CompositingMode { get { return gd != null ? gd.CompositingMode : comp; } set { if (gd != null) gd.CompositingMode = value; else comp = value; } }
    public CompositingQuality CompositingQuality { get { return gd != null ? gd.CompositingQuality : compQ; } set { if (gd != null) gd.CompositingQuality = value; else compQ = value; } }
    public float DpiX { get { return gd != null ? gd.DpiX : 96f; } }
    public float DpiY { get { return gd != null ? gd.DpiY : 96f; } }
    bool AA { get { return smooth == SmoothingMode.AntiAlias || smooth == SmoothingMode.HighQuality; } }

    // ---------------- состояние, преобразования, обрезка ----------------
    public GState Save()
    {
        GState s = new GState();
        if (gd != null) { s.Gdi = gd.Save(); return s; }
        s.SkCount = c.Save(); s.M = c.TotalMatrix; s.Clips = clipSaves; clipSaves = new List<int>();
        return s;
    }
    public void Restore(GState s)
    {
        if (s == null) return;
        if (gd != null) { gd.Restore(s.Gdi); return; }
        c.RestoreToCount(s.SkCount); clipSaves = s.Clips ?? new List<int>();
    }
    public void TranslateTransform(float dx, float dy) { if (gd != null) gd.TranslateTransform(dx, dy); else c.Translate(dx, dy); }
    public void TranslateTransform(float dx, float dy, MatrixOrder o) { if (gd != null) gd.TranslateTransform(dx, dy, o); else if (o == MatrixOrder.Prepend) c.Translate(dx, dy); else c.SetMatrix(SKMatrix.Concat(SKMatrix.CreateTranslation(dx, dy), c.TotalMatrix)); }
    public void ScaleTransform(float sx, float sy) { if (gd != null) gd.ScaleTransform(sx, sy); else c.Scale(sx, sy); }
    public void RotateTransform(float a) { if (gd != null) gd.RotateTransform(a); else c.RotateDegrees(a); }
    public void ResetTransform() { if (gd != null) gd.ResetTransform(); else c.ResetMatrix(); }
    public Matrix Transform
    {
        get { if (gd != null) return gd.Transform; SKMatrix m = c.TotalMatrix; return new Matrix(m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY); }
        set { if (gd != null) { gd.Transform = value; return; } float[] e = value.Elements; c.SetMatrix(new SKMatrix(e[0], e[2], e[4], e[1], e[3], e[5], 0, 0, 1)); }
    }

    void SkClipSave() { SKMatrix m = c.TotalMatrix; clipSaves.Add(c.Save()); c.SetMatrix(m); }
    public void SetClip(GraphicsPath p) { SetClip(p, CombineMode.Replace); }
    public void SetClip(GraphicsPath p, CombineMode m)
    {
        if (gd != null) { gd.SetClip(p, m); return; }
        if (m == CombineMode.Replace) ResetClip();
        SkClipSave();
        using (SKPath sp = Path(p)) c.ClipPath(sp, m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, true);
    }
    public void SetClip(RectangleF r) { SetClip(r, CombineMode.Replace); }
    public void SetClip(Rectangle r) { SetClip((RectangleF)r, CombineMode.Replace); }
    public void SetClip(Rectangle r, CombineMode m) { SetClip((RectangleF)r, m); }
    public void SetClip(RectangleF r, CombineMode m)
    {
        if (gd != null) { gd.SetClip(r, m); return; }
        if (m == CombineMode.Replace) ResetClip();
        SkClipSave();
        c.ClipRect(R(r), m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, false);
    }
    public void ExcludeClip(Rectangle r) { if (gd != null) gd.ExcludeClip(r); else { SkClipSave(); c.ClipRect(R(r), SKClipOperation.Difference, false); } }
    public void ResetClip()
    {
        if (gd != null) { gd.ResetClip(); return; }
        if (clipSaves.Count == 0) return;
        SKMatrix m = c.TotalMatrix; c.RestoreToCount(clipSaves[0]); clipSaves.Clear(); c.SetMatrix(m);
    }

    public void Clear(Color col) { if (gd != null) gd.Clear(col); else c.Clear(Col(col)); }

    // ---------------- заливки ----------------
    public void FillPath(Brush b, GraphicsPath p) { if (gd != null) { gd.FillPath(b, p); return; } using (SKPath sp = Path(p)) using (SKPaint pt = Fill(b)) c.DrawPath(sp, pt); }
    public void FillEllipse(Brush b, float x, float y, float w, float h) { if (gd != null) { gd.FillEllipse(b, x, y, w, h); return; } using (SKPaint pt = Fill(b)) c.DrawOval(new SKRect(x, y, x + w, y + h), pt); }
    public void FillEllipse(Brush b, RectangleF r) { FillEllipse(b, r.X, r.Y, r.Width, r.Height); }
    public void FillEllipse(Brush b, Rectangle r) { FillEllipse(b, r.X, r.Y, r.Width, r.Height); }
    public void FillEllipse(Brush b, int x, int y, int w, int h) { FillEllipse(b, (float)x, y, w, h); }
    public void FillRectangle(Brush b, float x, float y, float w, float h) { if (gd != null) { gd.FillRectangle(b, x, y, w, h); return; } using (SKPaint pt = Fill(b)) { pt.IsAntialias = false; c.DrawRect(new SKRect(x, y, x + w, y + h), pt); } }
    public void FillRectangle(Brush b, RectangleF r) { FillRectangle(b, r.X, r.Y, r.Width, r.Height); }
    public void FillRectangle(Brush b, Rectangle r) { FillRectangle(b, r.X, r.Y, r.Width, r.Height); }
    public void FillRectangle(Brush b, int x, int y, int w, int h) { FillRectangle(b, (float)x, y, w, h); }
    public void FillPolygon(Brush b, PointF[] pts) { if (gd != null) { gd.FillPolygon(b, pts); return; } using (SKPath sp = Poly(pts, true)) using (SKPaint pt = Fill(b)) c.DrawPath(sp, pt); }
    public void FillPolygon(Brush b, Point[] pts) { if (gd != null) { gd.FillPolygon(b, pts); return; } FillPolygon(b, Array.ConvertAll(pts, q => (PointF)q)); }
    public void FillPie(Brush b, float x, float y, float w, float h, float start, float sweep) { if (gd != null) { gd.FillPie(b, x, y, w, h, start, sweep); return; } using (SKPaint pt = Fill(b)) c.DrawArc(new SKRect(x, y, x + w, y + h), start, sweep, true, pt); }
    public void FillPie(Brush b, Rectangle r, float start, float sweep) { FillPie(b, r.X, r.Y, r.Width, r.Height, start, sweep); }
    public void FillRegion(Brush b, Region r) { if (gd != null) { gd.FillRegion(b, r); return; } foreach (RectangleF rf in r.GetRegionScans(new Matrix())) FillRectangle(b, rf); }

    // ---------------- линии ----------------
    public void DrawPath(Pen p, GraphicsPath gp) { if (gd != null) { gd.DrawPath(p, gp); return; } using (SKPath sp = Path(gp)) using (SKPaint pt = Stroke(p)) c.DrawPath(sp, pt); }
    public void DrawLine(Pen p, float x1, float y1, float x2, float y2) { if (gd != null) { gd.DrawLine(p, x1, y1, x2, y2); return; } using (SKPaint pt = Stroke(p)) c.DrawLine(x1, y1, x2, y2, pt); }
    public void DrawLine(Pen p, PointF a, PointF b) { DrawLine(p, a.X, a.Y, b.X, b.Y); }
    public void DrawLine(Pen p, Point a, Point b) { DrawLine(p, a.X, a.Y, b.X, b.Y); }
    public void DrawLine(Pen p, int x1, int y1, int x2, int y2) { DrawLine(p, (float)x1, y1, x2, y2); }
    public void DrawLines(Pen p, PointF[] pts) { if (gd != null) { gd.DrawLines(p, pts); return; } using (SKPath sp = Poly(pts, false)) using (SKPaint pt = Stroke(p)) c.DrawPath(sp, pt); }
    public void DrawLines(Pen p, Point[] pts) { if (gd != null) { gd.DrawLines(p, pts); return; } DrawLines(p, Array.ConvertAll(pts, q => (PointF)q)); }
    public void DrawPolygon(Pen p, PointF[] pts) { if (gd != null) { gd.DrawPolygon(p, pts); return; } using (SKPath sp = Poly(pts, true)) using (SKPaint pt = Stroke(p)) c.DrawPath(sp, pt); }
    public void DrawPolygon(Pen p, Point[] pts) { if (gd != null) { gd.DrawPolygon(p, pts); return; } DrawPolygon(p, Array.ConvertAll(pts, q => (PointF)q)); }
    public void DrawEllipse(Pen p, float x, float y, float w, float h) { if (gd != null) { gd.DrawEllipse(p, x, y, w, h); return; } using (SKPaint pt = Stroke(p)) c.DrawOval(new SKRect(x, y, x + w, y + h), pt); }
    public void DrawEllipse(Pen p, RectangleF r) { DrawEllipse(p, r.X, r.Y, r.Width, r.Height); }
    public void DrawEllipse(Pen p, Rectangle r) { DrawEllipse(p, r.X, r.Y, r.Width, r.Height); }
    public void DrawEllipse(Pen p, int x, int y, int w, int h) { DrawEllipse(p, (float)x, y, w, h); }
    public void DrawRectangle(Pen p, float x, float y, float w, float h) { if (gd != null) { gd.DrawRectangle(p, x, y, w, h); return; } using (SKPaint pt = Stroke(p)) c.DrawRect(new SKRect(x, y, x + w, y + h), pt); }
    public void DrawRectangle(Pen p, Rectangle r) { DrawRectangle(p, r.X, r.Y, r.Width, r.Height); }
    public void DrawRectangle(Pen p, int x, int y, int w, int h) { DrawRectangle(p, (float)x, y, w, h); }
    public void DrawArc(Pen p, float x, float y, float w, float h, float start, float sweep) { if (gd != null) { gd.DrawArc(p, x, y, w, h, start, sweep); return; } using (SKPaint pt = Stroke(p)) c.DrawArc(new SKRect(x, y, x + w, y + h), start, sweep, false, pt); }
    public void DrawArc(Pen p, RectangleF r, float start, float sweep) { DrawArc(p, r.X, r.Y, r.Width, r.Height, start, sweep); }
    public void DrawArc(Pen p, Rectangle r, float start, float sweep) { DrawArc(p, r.X, r.Y, r.Width, r.Height, start, sweep); }
    public void DrawArc(Pen p, int x, int y, int w, int h, int start, int sweep) { DrawArc(p, (float)x, y, w, h, start, sweep); }
    public void DrawBezier(Pen p, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
        if (gd != null) { gd.DrawBezier(p, x1, y1, x2, y2, x3, y3, x4, y4); return; }
        using (SKPath sp = new SKPath()) using (SKPaint pt = Stroke(p)) { sp.MoveTo(x1, y1); sp.CubicTo(x2, y2, x3, y3, x4, y4); c.DrawPath(sp, pt); }
    }
    public void DrawBezier(Pen p, PointF a, PointF b, PointF d, PointF e) { DrawBezier(p, a.X, a.Y, b.X, b.Y, d.X, d.Y, e.X, e.Y); }

    // ---------------- картинки ----------------
    public void DrawImage(Image img, float x, float y, float w, float h) { if (gd != null) { gd.DrawImage(img, x, y, w, h); return; } SkImg(img, new SKRect(0, 0, img.Width, img.Height), new SKRect(x, y, x + w, y + h), null); }
    public void DrawImage(Image img, RectangleF r) { DrawImage(img, r.X, r.Y, r.Width, r.Height); }
    public void DrawImage(Image img, Rectangle r) { DrawImage(img, r.X, r.Y, r.Width, r.Height); }
    public void DrawImage(Image img, int x, int y, int w, int h) { DrawImage(img, (float)x, y, w, h); }
    public void DrawImage(Image img, float x, float y) { if (gd != null) { gd.DrawImage(img, x, y); return; } DrawImage(img, x, y, img.Width, img.Height); }
    public void DrawImage(Image img, PointF p) { DrawImage(img, p.X, p.Y); }
    public void DrawImageUnscaled(Image img, int x, int y) { if (gd != null) { gd.DrawImageUnscaled(img, x, y); return; } DrawImage(img, x, y, img.Width, img.Height); }
    public void DrawImage(Image img, Rectangle dst, int sx, int sy, int sw, int sh, GraphicsUnit u) { DrawImage(img, dst, sx, sy, sw, sh, u, null); }
    public void DrawImage(Image img, Rectangle dst, int sx, int sy, int sw, int sh, GraphicsUnit u, ImageAttributes ia)
    {
        if (gd != null) { gd.DrawImage(img, dst, sx, sy, sw, sh, u, ia); return; }
        SkImg(img, new SKRect(sx, sy, sx + sw, sy + sh), R(dst), null);
    }
    public void DrawImage(Image img, Rectangle dst, float sx, float sy, float sw, float sh, GraphicsUnit u)
    {
        if (gd != null) { gd.DrawImage(img, dst, sx, sy, sw, sh, u); return; }
        SkImg(img, new SKRect(sx, sy, sx + sw, sy + sh), R(dst), null);
    }

    void SkImg(Image img, SKRect src, SKRect dst, SKPaint paint)
    {
        Bitmap b = img as Bitmap; if (b == null) return;
        BitmapData bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            using (SKImage si = SKImage.FromPixelCopy(new SKImageInfo(b.Width, b.Height, SKColorType.Bgra8888, SKAlphaType.Premul), bd.Scan0, bd.Stride))
            {
                SKSamplingOptions so = interp == InterpolationMode.NearestNeighbor ? new SKSamplingOptions(SKFilterMode.Nearest) :
                                        (interp == InterpolationMode.HighQualityBicubic || interp == InterpolationMode.HighQualityBilinear || interp == InterpolationMode.HighQuality) ? new SKSamplingOptions(SKCubicResampler.Mitchell) :
                                        new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
                using (SKPaint p = paint ?? new SKPaint()) { if (comp == CompositingMode.SourceCopy) p.BlendMode = SKBlendMode.Src; c.DrawImage(si, src, dst, so, p); }
            }
        }
        finally { b.UnlockBits(bd); }
    }

    // ---------------- текст ----------------
    public void DrawString(string s, Font f, Brush b, float x, float y) { if (gd != null) { gd.DrawString(s, f, b, x, y); return; } Text(s, f, b, new RectangleF(x, y, 0, 0), null); }
    public void DrawString(string s, Font f, Brush b, PointF p) { DrawString(s, f, b, p.X, p.Y); }
    public void DrawString(string s, Font f, Brush b, float x, float y, StringFormat sf) { if (gd != null) { gd.DrawString(s, f, b, x, y, sf); return; } Text(s, f, b, new RectangleF(x, y, 0, 0), sf); }
    public void DrawString(string s, Font f, Brush b, PointF p, StringFormat sf) { DrawString(s, f, b, p.X, p.Y, sf); }
    public void DrawString(string s, Font f, Brush b, RectangleF r) { if (gd != null) { gd.DrawString(s, f, b, r); return; } Text(s, f, b, r, null); }
    public void DrawString(string s, Font f, Brush b, RectangleF r, StringFormat sf) { if (gd != null) { gd.DrawString(s, f, b, r, sf); return; } Text(s, f, b, r, sf); }
    public SizeF MeasureString(string s, Font f) { if (gd != null) return gd.MeasureString(s, f); return Measure(s, f, 0, null); }
    public SizeF MeasureString(string s, Font f, int width) { if (gd != null) return gd.MeasureString(s, f, width); return Measure(s, f, width, null); }
    public SizeF MeasureString(string s, Font f, int width, StringFormat sf) { if (gd != null) return gd.MeasureString(s, f, width, sf); return Measure(s, f, width, sf); }
    public SizeF MeasureString(string s, Font f, SizeF area) { if (gd != null) return gd.MeasureString(s, f, area); return Measure(s, f, area.Width, null); }
    public SizeF MeasureString(string s, Font f, SizeF area, StringFormat sf) { if (gd != null) return gd.MeasureString(s, f, area, sf); return Measure(s, f, area.Width, sf); }
    public SizeF MeasureString(string s, Font f, PointF origin, StringFormat sf) { if (gd != null) return gd.MeasureString(s, f, origin, sf); return Measure(s, f, 0, sf); }

    // ---------------- перевод GDI+ → Skia ----------------
    static SKColor Col(Color c) { return new SKColor(c.R, c.G, c.B, c.A); }
    static SKRect R(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }
    static SKRect R(Rectangle r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }

    static SKPath Poly(PointF[] pts, bool close)
    {
        SKPath p = new SKPath();
        if (pts.Length == 0) return p;
        p.MoveTo(pts[0].X, pts[0].Y);
        for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i].X, pts[i].Y);
        if (close) p.Close();
        return p;
    }

    public static SKPath Path(GraphicsPath gp)
    {
        SKPath p = new SKPath();
        p.FillType = gp.FillMode == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd;
        int n = gp.PointCount; if (n == 0) return p;
        PointF[] pts = gp.PathPoints; byte[] types = gp.PathTypes;
        for (int i = 0; i < n; i++)
        {
            int t = types[i] & 0x07;
            if (t == 0) p.MoveTo(pts[i].X, pts[i].Y);
            else if (t == 1) p.LineTo(pts[i].X, pts[i].Y);
            else if (t == 3 && i + 2 < n) { p.CubicTo(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y, pts[i + 2].X, pts[i + 2].Y); i += 2; }
            if ((types[i] & 0x80) != 0) p.Close();
        }
        return p;
    }

    SKPaint Fill(Brush b)
    {
        SKPaint p = new SKPaint(); p.Style = SKPaintStyle.Fill; p.IsAntialias = AA;
        Shade(p, b);
        if (comp == CompositingMode.SourceCopy) p.BlendMode = SKBlendMode.Src;
        return p;
    }

    SKPaint Stroke(Pen pen)
    {
        SKPaint p = new SKPaint(); p.Style = SKPaintStyle.Stroke; p.IsAntialias = AA;
        p.StrokeWidth = pen.Width;
        LineCap cap = pen.StartCap;
        p.StrokeCap = cap == LineCap.Round || cap == LineCap.RoundAnchor ? SKStrokeCap.Round : cap == LineCap.Square || cap == LineCap.SquareAnchor ? SKStrokeCap.Square : SKStrokeCap.Butt;
        p.StrokeJoin = pen.LineJoin == LineJoin.Round ? SKStrokeJoin.Round : pen.LineJoin == LineJoin.Bevel ? SKStrokeJoin.Bevel : SKStrokeJoin.Miter;
        p.StrokeMiter = Math.Max(1f, pen.MiterLimit);
        if (pen.PenType == PenType.SolidColor) p.Color = Col(pen.Color);
        else { try { Shade(p, pen.Brush); } catch { p.Color = Col(pen.Color); } }
        if (pen.DashStyle != DashStyle.Solid)
        {
            float w = Math.Max(1f, pen.Width);
            float[] d = pen.DashStyle == DashStyle.Dot ? new float[] { w, w } : pen.DashStyle == DashStyle.Dash ? new float[] { 3 * w, w } : pen.DashStyle == DashStyle.Custom ? Array.ConvertAll(pen.DashPattern, v => v * w) : new float[] { 3 * w, w, w, w };
            p.PathEffect = SKPathEffect.CreateDash(d, 0);
        }
        return p;
    }

    static void Shade(SKPaint p, Brush b)
    {
        SolidBrush sb = b as SolidBrush;
        if (sb != null) { p.Color = Col(sb.Color); return; }
        LinearGradientBrush lb = b as LinearGradientBrush;
        if (lb != null)
        {
            RectangleF r = lb.Rectangle;
            Color[] cols; float[] pos;
            ColorBlend cb = null;
            try { cb = lb.InterpolationColors; } catch { }
            if (cb != null && cb.Colors != null && cb.Colors.Length >= 2 && cb.Positions != null && cb.Positions.Length == cb.Colors.Length) { cols = cb.Colors; pos = cb.Positions; }
            else
            {
                Color a = lb.LinearColors[0], z = lb.LinearColors[1];
                Blend bl = null; try { bl = lb.Blend; } catch { }
                if (bl != null && bl.Factors != null && bl.Factors.Length >= 2)
                {
                    cols = new Color[bl.Factors.Length]; pos = bl.Positions;
                    for (int i = 0; i < cols.Length; i++) cols[i] = Lerp(a, z, bl.Factors[i]);
                }
                else { cols = new Color[] { a, z }; pos = new float[] { 0, 1 }; }
            }
            // перпендикуляр градиента в своём пространстве — вертикаль; после матрицы направление может «перекоситься»,
            // поэтому строим шейдер в пространстве кисти и задаём ему матрицу кисти
            float[] e; using (Matrix m = lb.Transform) e = m.Elements;
            SKMatrix lm = new SKMatrix(e[0], e[2], e[4], e[1], e[3], e[5], 0, 0, 1);
            p.Shader = SKShader.CreateLinearGradient(new SKPoint(r.Left, r.Top), new SKPoint(r.Right, r.Top), Array.ConvertAll(cols, Col), pos, SKShaderTileMode.Clamp, lm);
            return;
        }
        PathGradientBrush pb = b as PathGradientBrush;
        if (pb != null)
        {
            RectangleF r = pb.Rectangle; PointF cp = pb.CenterPoint;
            Color surround = pb.SurroundColors != null && pb.SurroundColors.Length > 0 ? pb.SurroundColors[0] : Color.Transparent;
            float rx = Math.Max(1f, r.Width / 2), ry = Math.Max(1f, r.Height / 2);
            SKMatrix lm = SKMatrix.CreateScale(1, ry / rx, cp.X, cp.Y);
            p.Shader = SKShader.CreateRadialGradient(new SKPoint(cp.X, cp.Y), rx, new SKColor[] { Col(pb.CenterColor), Col(surround) }, new float[] { 0, 1 }, SKShaderTileMode.Clamp, lm);
            return;
        }
        p.Color = SKColors.Gray;
    }

    static Color Lerp(Color a, Color z, float t)
    {
        t = Math.Max(0, Math.Min(1, t));
        return Color.FromArgb((int)(a.A + (z.A - a.A) * t), (int)(a.R + (z.R - a.R) * t), (int)(a.G + (z.G - a.G) * t), (int)(a.B + (z.B - a.B) * t));
    }

    // ---------------- текст через Skia ----------------
    static readonly Dictionary<string, SKTypeface> faces = new Dictionary<string, SKTypeface>();
    static SKTypeface Face(Font f)
    {
        string fam = f.FontFamily.Name; int weight = 400;
        if (fam.EndsWith(" Semibold", StringComparison.OrdinalIgnoreCase)) { fam = fam.Substring(0, fam.Length - 9); weight = 600; }
        else if (fam.EndsWith(" Light", StringComparison.OrdinalIgnoreCase)) { fam = fam.Substring(0, fam.Length - 6); weight = 300; }
        else if (fam.EndsWith(" Black", StringComparison.OrdinalIgnoreCase)) { fam = fam.Substring(0, fam.Length - 6); weight = 900; }
        if (f.Bold) weight = Math.Max(weight, 700);
        string key = fam + "|" + weight + "|" + (f.Italic ? 1 : 0);
        lock (faces)
        {
            SKTypeface t;
            if (!faces.TryGetValue(key, out t))
            {
                t = SKTypeface.FromFamilyName(fam, new SKFontStyle(weight, 5, f.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)) ?? SKTypeface.Default;
                faces[key] = t;
            }
            return t;
        }
    }

    float PxSize(Font f)
    {
        if (f.Unit == GraphicsUnit.Pixel) return f.Size;
        return f.SizeInPoints * DpiY / 72f;
    }

    static readonly Dictionary<int, SKTypeface> fallback = new Dictionary<int, SKTypeface>();
    static SKTypeface Fallback(SKTypeface baseFace, int cp, SKFontStyle st)
    {
        lock (fallback)
        {
            SKTypeface t;
            if (!fallback.TryGetValue(cp, out t)) { t = SKFontManager.Default.MatchCharacter(baseFace.FamilyName, st, null, cp) ?? baseFace; fallback[cp] = t; }
            return t;
        }
    }

    // кусочки строки, каждый — своим шрифтом (для значков и эмодзи, которых нет в Segoe UI)
    struct Run { public string S; public SKFont F; }
    static readonly Dictionary<string, SKFont> fontCache = new Dictionary<string, SKFont>();
    static SKFont FontOf(SKTypeface tf, float size)
    {
        string k = tf.Handle.ToInt64() + "|" + Math.Round(size, 1);
        lock (fontCache)
        {
            SKFont f;
            if (!fontCache.TryGetValue(k, out f))
            {
                if (fontCache.Count > 400) { foreach (SKFont old in fontCache.Values) old.Dispose(); fontCache.Clear(); }
                f = new SKFont(tf, size); f.Edging = SKFontEdging.Antialias; f.Subpixel = true; f.Hinting = SKFontHinting.Slight;
                fontCache[k] = f;
            }
            return f;
        }
    }

    List<Run> Runs(string s, SKTypeface tf, float size)
    {
        List<Run> res = new List<Run>();
        SKFont main = FontOf(tf, size);
        int i = 0; System.Text.StringBuilder cur = new System.Text.StringBuilder(); SKTypeface curF = tf;
        while (i < s.Length)
        {
            int cp = char.ConvertToUtf32(s, i); int len = char.IsSurrogatePair(s, i) ? 2 : 1;
            SKTypeface want = tf;
            if (cp > 0x7F && !main.ContainsGlyph(cp) && cp != 0xFE0F && cp != 0x200D) want = Fallback(tf, cp, tf.FontStyle);
            if (cp == 0xFE0F || cp == 0x200D) want = curF;
            if (want != curF && cur.Length > 0) { res.Add(MakeRun(cur.ToString(), curF, size, main, tf)); cur.Clear(); }
            curF = want; cur.Append(s, i, len); i += len;
        }
        if (cur.Length > 0) res.Add(MakeRun(cur.ToString(), curF, size, main, tf));
        return res;
    }
    static Run MakeRun(string s, SKTypeface f, float size, SKFont main, SKTypeface tf)
    {
        Run r; r.S = s;
        r.F = f == tf ? main : FontOf(f, size);
        return r;
    }
    static float RunsWidth(List<Run> rs) { float w = 0; foreach (Run r in rs) w += r.F.MeasureText(r.S); return w; }

    static bool Typographic(StringFormat sf) { return sf != null && (sf.FormatFlags & StringFormatFlags.FitBlackBox) != 0 && (sf.FormatFlags & StringFormatFlags.NoClip) != 0; }

    List<string> Lines(string s, Font f, float width, StringFormat sf, SKTypeface tf, float size)
    {
        List<string> outL = new List<string>();
        bool wrap = width > 0 && (sf == null || (sf.FormatFlags & StringFormatFlags.NoWrap) == 0);
        foreach (string para in s.Replace("\r\n", "\n").Split('\n'))
        {
            if (!wrap) { outL.Add(para); continue; }
            string line = "";
            foreach (string w in para.Split(' '))
            {
                string cand = line.Length == 0 ? w : line + " " + w;
                if (line.Length > 0 && RunsWidth(Runs(cand, tf, size)) > width) { outL.Add(line); line = w; }
                else line = cand;
            }
            outL.Add(line);
        }
        return outL;
    }

    SizeF Measure(string s, Font f, float width, StringFormat sf)
    {
        if (string.IsNullOrEmpty(s)) return new SizeF(0, f.GetHeight(DpiY));
        SKTypeface tf = Face(f); float size = PxSize(f);
        float pad = Typographic(sf) ? 0 : size / 6f;
        List<string> ls = Lines(s, f, width > 0 ? width - pad * 2 : 0, sf, tf, size);
        float w = 0; foreach (string l in ls) w = Math.Max(w, RunsWidth(Runs(l, tf, size)));
        SKFontMetrics m = FontOf(tf, size).Metrics; float lh = -m.Ascent + m.Descent + m.Leading; return new SizeF(w + pad * 2, lh * ls.Count);
    }

    void Text(string s, Font f, Brush b, RectangleF r, StringFormat sf)
    {
        if (string.IsNullOrEmpty(s)) return;
        SKTypeface tf = Face(f); float size = PxSize(f);
        bool typo = Typographic(sf); float pad = typo ? 0 : size / 6f;
        bool hasBox = r.Width > 0;
        StringAlignment ha = sf != null ? sf.Alignment : StringAlignment.Near, va = sf != null ? sf.LineAlignment : StringAlignment.Near;
        bool ellipsis = sf != null && (sf.Trimming == StringTrimming.EllipsisCharacter || sf.Trimming == StringTrimming.EllipsisWord) && hasBox;
        float avail = hasBox ? r.Width - pad * 2 : 0;
        List<string> ls = Lines(s, f, hasBox ? avail : 0, sf, tf, size);
        SKFontMetrics m = FontOf(tf, size).Metrics;
        float lh = -m.Ascent + m.Descent + m.Leading, total = lh * ls.Count;
        float y0 = r.Y;
        if (r.Height > 0) { if (va == StringAlignment.Center) y0 = r.Y + (r.Height - total) / 2; else if (va == StringAlignment.Far) y0 = r.Bottom - total; }
        else if (!hasBox) { if (va == StringAlignment.Center) y0 = r.Y - total / 2; else if (va == StringAlignment.Far) y0 = r.Y - total; }
        using (SKPaint p = new SKPaint())
        {
            p.IsAntialias = true; Shade(p, b);
            for (int li = 0; li < ls.Count; li++)
            {
                string line = ls[li];
                List<Run> rs = Runs(line, tf, size); float w = RunsWidth(rs);
                if (ellipsis && avail > 0 && w > avail)
                {
                    string t = line;
                    while (t.Length > 0 && RunsWidth(Runs(t + "…", tf, size)) > avail) t = t.Substring(0, t.Length - 1);
                    rs = Runs(t + "…", tf, size); w = RunsWidth(rs);
                }
                float x;
                if (hasBox) x = ha == StringAlignment.Center ? r.X + (r.Width - w) / 2 : ha == StringAlignment.Far ? r.Right - pad - w : r.X + pad;
                else x = ha == StringAlignment.Center ? r.X - w / 2 : ha == StringAlignment.Far ? r.X - pad - w : r.X + pad;
                float baseY = y0 + li * lh - m.Ascent;
                foreach (Run run in rs) { c.DrawText(run.S, x, baseY, SKTextAlign.Left, run.F, p); x += run.F.MeasureText(run.S); }
            }
        }
    }
}
