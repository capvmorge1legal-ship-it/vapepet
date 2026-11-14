// Слой совместимости: привычные классы рисования (Graphics, Pen, SolidBrush, GraphicsPath…) поверх SkiaSharp.
// Весь рисунок котика и сцен написан в стиле GDI+; этот слой позволяет рисовать тем же кодом, но движком Skia:
// он в несколько раз быстрее, со сглаживанием лучше и умеет размытие, тени и свечение.
// Файлы, которые рисуют котика, подключают эти классы псевдонимами (using Graphics = Gdi.Graphics; …).
// Окна WinForms (меню, настройки, диалоги) продолжают рисовать обычным System.Drawing.
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using SkiaSharp;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using Point = System.Drawing.Point;
using RectangleF = System.Drawing.RectangleF;
using Rectangle = System.Drawing.Rectangle;
using SizeF = System.Drawing.SizeF;
using Size = System.Drawing.Size;
using FontStyle = System.Drawing.FontStyle;
using GraphicsUnit = System.Drawing.GraphicsUnit;
using StringAlignment = System.Drawing.StringAlignment;
using StringTrimming = System.Drawing.StringTrimming;
using StringFormatFlags = System.Drawing.StringFormatFlags;

namespace Gdi
{
    static class Sk
    {
        public static SKColor C(Color c) { return new SKColor(c.R, c.G, c.B, c.A); }
        public static SKRect R(float x, float y, float w, float h) { return new SKRect(x, y, x + w, y + h); }
        public static SKRect R(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }
        public static SKPoint P(PointF p) { return new SKPoint(p.X, p.Y); }
        public static SKPoint[] Pts(PointF[] a) { SKPoint[] r = new SKPoint[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = new SKPoint(a[i].X, a[i].Y); return r; }
        public static SKPoint[] Pts(Point[] a) { SKPoint[] r = new SKPoint[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = new SKPoint(a[i].X, a[i].Y); return r; }
    }

    // ---------------- кисти ----------------
    public abstract class Brush : IDisposable
    {
        internal abstract void Apply(SKPaint p);
        public virtual void Dispose() { }
    }

    public sealed class SolidBrush : Brush
    {
        public Color Color;
        public SolidBrush(Color c) { Color = c; }
        internal override void Apply(SKPaint p) { p.Shader = null; p.Color = Sk.C(Color); }
    }

    public sealed class ColorBlend
    {
        public Color[] Colors; public float[] Positions;
        public ColorBlend() { }
        public ColorBlend(int n) { Colors = new Color[n]; Positions = new float[n]; }
    }

    public sealed class LinearGradientBrush : Brush
    {
        SKPoint a, b; Color c1, c2; ColorBlend blend; SKShader shader;
        public LinearGradientBrush(RectangleF r, Color c1, Color c2, float angle) { Init(r, c1, c2, angle); }
        public LinearGradientBrush(Rectangle r, Color c1, Color c2, float angle) { Init(r, c1, c2, angle); }
        public LinearGradientBrush(RectangleF r, Color c1, Color c2, LinearGradientMode m) { InitMode(r, c1, c2, m); }
        public LinearGradientBrush(Rectangle r, Color c1, Color c2, LinearGradientMode m) { InitMode(r, c1, c2, m); }
        public LinearGradientBrush(PointF p1, PointF p2, Color c1, Color c2) { a = Sk.P(p1); b = Sk.P(p2); this.c1 = c1; this.c2 = c2; }
        public LinearGradientBrush(Point p1, Point p2, Color c1, Color c2) { a = new SKPoint(p1.X, p1.Y); b = new SKPoint(p2.X, p2.Y); this.c1 = c1; this.c2 = c2; }
        void InitMode(RectangleF r, Color c1, Color c2, LinearGradientMode m)
        {
            this.c1 = c1; this.c2 = c2;
            switch (m)
            {
                case LinearGradientMode.Horizontal: a = new SKPoint(r.Left, r.Top); b = new SKPoint(r.Right, r.Top); break;
                case LinearGradientMode.Vertical: a = new SKPoint(r.Left, r.Top); b = new SKPoint(r.Left, r.Bottom); break;
                case LinearGradientMode.ForwardDiagonal: a = new SKPoint(r.Left, r.Top); b = new SKPoint(r.Right, r.Bottom); break;
                default: a = new SKPoint(r.Right, r.Top); b = new SKPoint(r.Left, r.Bottom); break;
            }
        }
        void Init(RectangleF r, Color c1, Color c2, float angle)
        {
            this.c1 = c1; this.c2 = c2;
            double t = angle * Math.PI / 180.0; float dx = (float)Math.Cos(t), dy = (float)Math.Sin(t);
            float half = (Math.Abs(r.Width * dx) + Math.Abs(r.Height * dy)) / 2f;
            float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
            a = new SKPoint(cx - dx * half, cy - dy * half); b = new SKPoint(cx + dx * half, cy + dy * half);
        }
        public ColorBlend InterpolationColors { get { return blend; } set { blend = value; if (shader != null) { shader.Dispose(); shader = null; } } }
        internal override void Apply(SKPaint p)
        {
            if (shader == null)
            {
                if (blend != null && blend.Colors != null && blend.Colors.Length >= 2)
                {
                    SKColor[] cs = new SKColor[blend.Colors.Length];
                    for (int i = 0; i < cs.Length; i++) cs[i] = Sk.C(blend.Colors[i]);
                    shader = SKShader.CreateLinearGradient(a, b, cs, blend.Positions, SKShaderTileMode.Clamp);
                }
                else shader = SKShader.CreateLinearGradient(a, b, new SKColor[] { Sk.C(c1), Sk.C(c2) }, null, SKShaderTileMode.Clamp);
            }
            p.Color = SKColors.White; p.Shader = shader;
        }
        public override void Dispose() { if (shader != null) { shader.Dispose(); shader = null; } }
    }

    // радиальная заливка по контуру (используется для свечения): цвет центра → цвет края
    public sealed class PathGradientBrush : Brush
    {
        SKRect bounds; SKShader shader; Color center = Color.White; Color[] surround = new Color[] { Color.Transparent };
        public PathGradientBrush(GraphicsPath path) { bounds = path.sk.Bounds; }
        public Color CenterColor { get { return center; } set { center = value; Drop(); } }
        public Color[] SurroundColors { get { return surround; } set { surround = value; Drop(); } }
        void Drop() { if (shader != null) { shader.Dispose(); shader = null; } }
        internal override void Apply(SKPaint p)
        {
            if (shader == null)
            {
                float rx = Math.Max(0.01f, bounds.Width / 2f), ry = Math.Max(0.01f, bounds.Height / 2f);
                SKMatrix m = SKMatrix.CreateScale(rx, ry);
                m = m.PostConcat(SKMatrix.CreateTranslation(bounds.MidX, bounds.MidY));
                shader = SKShader.CreateRadialGradient(new SKPoint(0, 0), 1f, new SKColor[] { Sk.C(center), Sk.C(surround != null && surround.Length > 0 ? surround[0] : Color.Transparent) }, null, SKShaderTileMode.Clamp, m);
            }
            p.Color = SKColors.White; p.Shader = shader;
        }
        public override void Dispose() { Drop(); }
    }

    public static class Brushes
    {
        public static readonly SolidBrush White = new SolidBrush(Color.White), Black = new SolidBrush(Color.Black), Transparent = new SolidBrush(Color.Transparent),
            Gray = new SolidBrush(Color.Gray), Red = new SolidBrush(Color.Red), Gold = new SolidBrush(Color.Gold), Yellow = new SolidBrush(Color.Yellow);
    }

    // ---------------- перо ----------------
    public sealed class Pen : IDisposable
    {
        public Color Color; public float Width; public Brush Brush;
        public LineCap StartCap = LineCap.Flat, EndCap = LineCap.Flat; public LineJoin LineJoin = LineJoin.Miter;
        public Pen(Color c) { Color = c; Width = 1f; }
        public Pen(Color c, float w) { Color = c; Width = w; }
        public Pen(Brush b, float w) { Brush = b; Width = w; }
        public Pen(Brush b) { Brush = b; Width = 1f; }
        public void Dispose() { }
    }

    public static class Pens
    {
        public static readonly Pen White = new Pen(Color.White), Black = new Pen(Color.Black), Gray = new Pen(Color.Gray);
    }

    // ---------------- контур ----------------
    public sealed class GraphicsPath : IDisposable
    {
        internal SKPath sk = new SKPath(); bool open; SKPoint last;
        public GraphicsPath() { sk.FillType = SKPathFillType.EvenOdd; }
        public GraphicsPath(FillMode m) { sk.FillType = m == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd; }
        public FillMode FillMode
        {
            get { return sk.FillType == SKPathFillType.Winding ? FillMode.Winding : FillMode.Alternate; }
            set { sk.FillType = value == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd; }
        }
        void To(float x, float y)
        {
            if (!open) { sk.MoveTo(x, y); open = true; }
            else if (Math.Abs(last.X - x) > 0.0001f || Math.Abs(last.Y - y) > 0.0001f) sk.LineTo(x, y);
            last = new SKPoint(x, y);
        }
        public void StartFigure() { open = false; }
        public void CloseFigure() { if (open) sk.Close(); open = false; }
        public void CloseAllFigures() { CloseFigure(); }
        public void Reset() { sk.Reset(); open = false; }
        public void AddLine(float x1, float y1, float x2, float y2) { To(x1, y1); sk.LineTo(x2, y2); last = new SKPoint(x2, y2); }
        public void AddLine(PointF p1, PointF p2) { AddLine(p1.X, p1.Y, p2.X, p2.Y); }
        public void AddLine(Point p1, Point p2) { AddLine(p1.X, p1.Y, p2.X, p2.Y); }
        public void AddLines(PointF[] pts) { for (int i = 0; i < pts.Length; i++) { if (i == 0) To(pts[0].X, pts[0].Y); else { sk.LineTo(pts[i].X, pts[i].Y); last = Sk.P(pts[i]); } } }
        public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) { To(x1, y1); sk.CubicTo(x2, y2, x3, y3, x4, y4); last = new SKPoint(x4, y4); }
        public void AddBezier(PointF p1, PointF p2, PointF p3, PointF p4) { AddBezier(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, p4.X, p4.Y); }
        public void AddArc(float x, float y, float w, float h, float start, float sweep)
        {
            if (w <= 0 || h <= 0) return;
            SKRect r = Sk.R(x, y, w, h);
            if (Math.Abs(sweep) >= 360f) { sk.AddOval(r); open = false; return; }
            double a0 = start * Math.PI / 180.0, a1 = (start + sweep) * Math.PI / 180.0;
            float sx = r.MidX + (float)Math.Cos(a0) * w / 2f, sy = r.MidY + (float)Math.Sin(a0) * h / 2f;
            To(sx, sy);
            sk.ArcTo(r, start, sweep, false);
            last = new SKPoint(r.MidX + (float)Math.Cos(a1) * w / 2f, r.MidY + (float)Math.Sin(a1) * h / 2f);
        }
        public void AddArc(RectangleF r, float start, float sweep) { AddArc(r.X, r.Y, r.Width, r.Height, start, sweep); }
        public void AddArc(Rectangle r, float start, float sweep) { AddArc(r.X, r.Y, r.Width, r.Height, start, sweep); }
        public void AddEllipse(float x, float y, float w, float h) { sk.AddOval(Sk.R(x, y, w, h)); open = false; }
        public void AddEllipse(RectangleF r) { AddEllipse(r.X, r.Y, r.Width, r.Height); }
        public void AddEllipse(Rectangle r) { AddEllipse(r.X, r.Y, r.Width, r.Height); }
        public void AddRectangle(RectangleF r) { sk.AddRect(Sk.R(r)); open = false; }
        public void AddRectangle(Rectangle r) { sk.AddRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom)); open = false; }
        public void AddPolygon(PointF[] pts) { if (pts.Length > 1) sk.AddPoly(Sk.Pts(pts), true); open = false; }
        public void AddPolygon(Point[] pts) { if (pts.Length > 1) sk.AddPoly(Sk.Pts(pts), true); open = false; }
        public void AddPie(float x, float y, float w, float h, float start, float sweep)
        {
            SKRect r = Sk.R(x, y, w, h); sk.MoveTo(r.MidX, r.MidY); sk.ArcTo(r, start, sweep, false); sk.Close(); open = false;
        }
        // замкнутая сглаженная кривая через точки (кардинальный сплайн, как в GDI+)
        public void AddClosedCurve(PointF[] p) { AddClosedCurve(p, 0.5f); }
        public void AddClosedCurve(PointF[] p, float tension)
        {
            int n = p.Length; if (n < 3) { AddPolygon(p); return; }
            float k = tension / 3f;
            sk.MoveTo(p[0].X, p[0].Y);
            for (int i = 0; i < n; i++)
            {
                PointF p0 = p[(i + n - 1) % n], p1 = p[i], p2 = p[(i + 1) % n], p3 = p[(i + 2) % n];
                sk.CubicTo(p1.X + (p2.X - p0.X) * k, p1.Y + (p2.Y - p0.Y) * k, p2.X - (p3.X - p1.X) * k, p2.Y - (p3.Y - p1.Y) * k, p2.X, p2.Y);
            }
            sk.Close(); open = false;
        }
        public void AddCurve(PointF[] p) { AddCurve(p, 0.5f); }
        public void AddCurve(PointF[] p, float tension)
        {
            int n = p.Length; if (n < 2) return;
            float k = tension / 3f;
            To(p[0].X, p[0].Y);
            for (int i = 0; i < n - 1; i++)
            {
                PointF p0 = p[Math.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(n - 1, i + 2)];
                sk.CubicTo(p1.X + (p2.X - p0.X) * k, p1.Y + (p2.Y - p0.Y) * k, p2.X - (p3.X - p1.X) * k, p2.Y - (p3.Y - p1.Y) * k, p2.X, p2.Y);
            }
            last = Sk.P(p[n - 1]);
        }
        public void AddPath(GraphicsPath other, bool connect) { sk.AddPath(other.sk); open = false; }
        public void AddString(string s, System.Drawing.FontFamily family, int style, float emSize, PointF origin, StringFormat format)
        {
            AddString(s, family, style, emSize, new RectangleF(origin.X, origin.Y, 0, 0), format);
        }
        public void AddString(string s, System.Drawing.FontFamily family, int style, float emSize, RectangleF layout, StringFormat format)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (Font f = new Font(family.Name, emSize, (FontStyle)style, GraphicsUnit.Pixel))
            {
                float w = f.sk.MeasureText(s), pad = format != null && format.Typographic ? 0 : emSize / 6f;
                float x = layout.X + pad, y = layout.Y;
                StringAlignment al = format != null ? format.Alignment : StringAlignment.Near, la = format != null ? format.LineAlignment : StringAlignment.Near;
                if (al == StringAlignment.Center) x = layout.Width > 0 ? layout.X + (layout.Width - w) / 2f : layout.X - w / 2f;
                else if (al == StringAlignment.Far) x = layout.Width > 0 ? layout.Right - pad - w : layout.X - w;
                if (layout.Height > 0 && la == StringAlignment.Center) y = layout.Y + (layout.Height - f.LineH) / 2f;
                else if (layout.Height > 0 && la == StringAlignment.Far) y = layout.Bottom - f.LineH;
                using (SKPath tp = f.sk.GetTextPath(s, new SKPoint(x, y + f.Ascent))) sk.AddPath(tp);
            }
            open = false;
        }
        public RectangleF GetBounds() { SKRect b = sk.Bounds; return new RectangleF(b.Left, b.Top, b.Width, b.Height); }
        public bool IsVisible(PointF p) { return sk.Contains(p.X, p.Y); }
        public bool IsVisible(float x, float y) { return sk.Contains(x, y); }
        public void Dispose() { if (sk != null) { sk.Dispose(); sk = null; } }
    }

    // ---------------- шрифт и текст ----------------
    public sealed class Font : IDisposable
    {
        public readonly string Name; public readonly float Size; public readonly FontStyle Style; public readonly GraphicsUnit Unit;
        internal SKFont sk; internal float Px, Ascent, LineH;
        static readonly Dictionary<string, SKTypeface> faces = new Dictionary<string, SKTypeface>();

        public Font(string family, float size) : this(family, size, FontStyle.Regular, GraphicsUnit.Point) { }
        public Font(string family, float size, FontStyle style) : this(family, size, style, GraphicsUnit.Point) { }
        public Font(string family, float size, GraphicsUnit unit) : this(family, size, FontStyle.Regular, unit) { }
        public Font(System.Drawing.FontFamily family, float size, FontStyle style, GraphicsUnit unit) : this(family.Name, size, style, unit) { }
        public Font(string family, float size, FontStyle style, GraphicsUnit unit)
        {
            Name = family; Size = size; Style = style; Unit = unit;
            Px = unit == GraphicsUnit.Pixel || unit == GraphicsUnit.World ? size : size * 96f / 72f;
            bool bold = (style & FontStyle.Bold) != 0, italic = (style & FontStyle.Italic) != 0;
            SKTypeface tf = Face(family, bold, italic);
            sk = new SKFont(tf, Math.Max(0.5f, Px));
            sk.Subpixel = true; sk.Edging = SKFontEdging.Antialias; sk.Hinting = SKFontHinting.Slight;
            if (bold && tf.FontWeight < 600) sk.Embolden = true;
            if (italic && tf.FontSlant == SKFontStyleSlant.Upright) sk.SkewX = -0.25f;
            SKFontMetrics m = sk.Metrics;
            Ascent = -m.Ascent; LineH = m.Descent - m.Ascent + m.Leading;
        }

        internal static SKTypeface Face(string family, bool bold, bool italic)
        {
            string key = family + "|" + (bold ? "b" : "") + (italic ? "i" : "");
            SKTypeface tf;
            lock (faces)
            {
                if (faces.TryGetValue(key, out tf)) return tf;
                SKFontStyleSlant sl = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
                int weight = bold ? 700 : 400;
                tf = SKTypeface.FromFamilyName(family, (SKFontStyleWeight)weight, SKFontStyleWidth.Normal, sl);
                if (tf == null || !string.Equals(tf.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                {
                    // «Segoe UI Semibold», «Arial Black»: в Windows это начертания семейства, а не отдельные семейства
                    string[] suf = { " Semibold", " Semilight", " Light", " Black", " Bold", " Medium" };
                    int[] w = { 600, 350, 300, 900, 700, 500 };
                    for (int i = 0; i < suf.Length; i++)
                        if (family.EndsWith(suf[i], StringComparison.OrdinalIgnoreCase))
                        {
                            string baseName = family.Substring(0, family.Length - suf[i].Length);
                            SKTypeface t2 = SKTypeface.FromFamilyName(baseName, (SKFontStyleWeight)Math.Max(w[i], weight), SKFontStyleWidth.Normal, sl);
                            if (t2 != null && string.Equals(t2.FamilyName, baseName, StringComparison.OrdinalIgnoreCase)) tf = t2;
                            break;
                        }
                }
                if (tf == null) tf = SKTypeface.Default;
                faces[key] = tf;
                return tf;
            }
        }

        public float Height { get { return (float)Math.Ceiling(LineH); } }
        public float GetHeight() { return LineH; }
        public float GetHeight(Graphics g) { return LineH; }
        public float SizeInPoints { get { return Px * 72f / 96f; } }
        public bool Bold { get { return (Style & FontStyle.Bold) != 0; } }
        public System.Drawing.FontFamily FontFamily { get { return new System.Drawing.FontFamily(Name); } }
        public void Dispose() { if (sk != null) { sk.Dispose(); sk = null; } }
        public static implicit operator System.Drawing.Font(Font f) { return f == null ? null : new System.Drawing.Font(f.Name, f.Size, f.Style, f.Unit); }
    }

    public sealed class StringFormat : IDisposable, ICloneable
    {
        public StringAlignment Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near;
        public StringTrimming Trimming = StringTrimming.None; public StringFormatFlags FormatFlags = 0;
        internal bool Typographic;
        public StringFormat() { }
        public StringFormat(StringFormat o) { Alignment = o.Alignment; LineAlignment = o.LineAlignment; Trimming = o.Trimming; FormatFlags = o.FormatFlags; Typographic = o.Typographic; }
        public StringFormat(StringFormatFlags f) { FormatFlags = f; }
        public static StringFormat GenericTypographic { get { StringFormat f = new StringFormat(); f.Typographic = true; return f; } }
        public static StringFormat GenericDefault { get { return new StringFormat(); } }
        public object Clone() { return new StringFormat(this); }
        public void Dispose() { }
    }

    // раскладка текста: переносы, выравнивание, запасные шрифты для символов, которых нет в основном
    static class TextLayout
    {
        public struct Line { public string Text; public float Width; }
        static readonly Dictionary<long, SKTypeface> fallback = new Dictionary<long, SKTypeface>();
        static readonly Dictionary<long, SKFont> fallbackFonts = new Dictionary<long, SKFont>();

        static SKFont FontFor(Font f, int cp)
        {
            if (cp < 0x250 || f.sk.GetGlyph(cp) != 0) return f.sk;
            long key = ((long)f.sk.Typeface.Handle.GetHashCode() << 24) ^ cp;
            SKTypeface tf;
            if (!fallback.TryGetValue(cp, out tf))
            {
                tf = SKFontManager.Default.MatchCharacter(f.Name, cp);
                fallback[cp] = tf;
            }
            if (tf == null) return f.sk;
            long fk = ((long)tf.Handle.GetHashCode() << 20) ^ (long)(f.Px * 16);
            SKFont ff;
            if (!fallbackFonts.TryGetValue(fk, out ff))
            {
                ff = new SKFont(tf, f.sk.Size); ff.Subpixel = true; ff.Edging = SKFontEdging.Antialias; ff.Hinting = SKFontHinting.Slight;
                fallbackFonts[fk] = ff;
            }
            return ff;
        }

        // разбить строку на куски одного шрифта и вызвать действие для каждого
        public static float Runs(string s, Font f, Action<string, SKFont, float> each)
        {
            float x = 0; int start = 0; SKFont cur = null;
            for (int i = 0; i < s.Length;)
            {
                int cp = char.IsHighSurrogate(s[i]) && i + 1 < s.Length ? char.ConvertToUtf32(s[i], s[i + 1]) : s[i];
                int len = cp > 0xFFFF ? 2 : 1;
                SKFont ft = (cp == 0xFE0F || cp == 0x200D) && cur != null ? cur : FontFor(f, cp);
                if (cur != null && ft != cur)
                {
                    string part = s.Substring(start, i - start); float w = cur.MeasureText(part);
                    if (each != null) each(part, cur, x);
                    x += w; start = i;
                }
                cur = ft; i += len;
            }
            if (cur != null && start < s.Length)
            {
                string part = s.Substring(start); float w = cur.MeasureText(part);
                if (each != null) each(part, cur, x);
                x += w;
            }
            return x;
        }

        public static float Width(string s, Font f)
        {
            if (s.Length == 0) return 0;
            bool simple = true;
            for (int i = 0; i < s.Length; i++) if (s[i] >= 0x250) { simple = false; break; }
            return simple ? f.sk.MeasureText(s) : Runs(s, f, null);
        }

        public static List<Line> Break(string text, Font f, float maxWidth, bool noWrap)
        {
            List<Line> lines = new List<Line>();
            if (text == null) text = "";
            foreach (string para0 in text.Replace("\r", "").Split('\n'))
            {
                string para = para0;
                if (maxWidth <= 0 || noWrap) { Add(lines, para, f); continue; }
                if (Width(para.TrimEnd(), f) <= maxWidth) { Add(lines, para, f); continue; }
                string cur = "";
                foreach (string word in SplitWords(para))
                {
                    string tryLine = cur + word;
                    if (cur.Length == 0 || Width(tryLine.TrimEnd(), f) <= maxWidth) { cur = tryLine; }
                    else { Add(lines, cur, f); cur = word.TrimStart(); }
                    // слово длиннее строки — режем по буквам
                    while (Width(cur.TrimEnd(), f) > maxWidth && cur.Length > 1)
                    {
                        int k = cur.Length - 1;
                        while (k > 1 && Width(cur.Substring(0, k), f) > maxWidth) k--;
                        Add(lines, cur.Substring(0, k), f); cur = cur.Substring(k);
                    }
                }
                Add(lines, cur, f);
            }
            return lines;
        }

        static void Add(List<Line> lines, string s, Font f)
        {
            Line l = new Line(); l.Text = s.TrimEnd(); l.Width = Width(l.Text, f); lines.Add(l);
        }

        static IEnumerable<string> SplitWords(string s)
        {
            int start = 0;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == ' ' || s[i] == '-') { yield return s.Substring(start, i - start + 1); start = i + 1; }
            if (start < s.Length) yield return s.Substring(start);
        }

        public static string Ellipsis(string s, Font f, float maxWidth)
        {
            if (Width(s, f) <= maxWidth) return s;
            int k = s.Length;
            while (k > 0 && Width(s.Substring(0, k) + "…", f) > maxWidth) k--;
            return s.Substring(0, k).TrimEnd() + "…";
        }
    }

    // ---------------- картинка в памяти ----------------
    public sealed class Bitmap : IDisposable
    {
        internal SKSurface surface; public readonly int Width, Height;
        System.Drawing.Bitmap gdiCopy;
        public Bitmap(int w, int h) : this(w, h, PixelFormat.Format32bppPArgb) { }
        public Bitmap(int w, int h, PixelFormat pf)
        {
            Width = Math.Max(1, w); Height = Math.Max(1, h);
            surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
        }
        public Size Size { get { return new Size(Width, Height); } }

        // скопировать пиксели (BGRA, с умноженной альфой) в готовый буфер такого же размера — так кадр попадает в окно
        public unsafe void CopyTo(IntPtr dest)
        {
            using (SKPixmap pm = surface.PeekPixels())
            {
                long n = (long)pm.RowBytes * Height;
                Buffer.MemoryCopy((void*)pm.GetPixels(), (void*)dest, n, n);
            }
        }

        // есть ли в кадре хоть один заметный пиксель (защита от «пустого котика»)
        public unsafe bool IsBlank()
        {
            using (SKPixmap pm = surface.PeekPixels())
            {
                byte* p = (byte*)pm.GetPixels(); long n = (long)pm.RowBytes * Height;
                for (long i = 3; i < n; i += 16) if (p[i] > 8) return false;
                return true;
            }
        }

        public static unsafe Bitmap FromGdi(System.Drawing.Bitmap src)
        {
            Bitmap b = new Bitmap(src.Width, src.Height);
            BitmapData d = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                using (SKPixmap pm = b.surface.PeekPixels())
                    for (int y = 0; y < src.Height; y++)
                        Buffer.MemoryCopy((byte*)d.Scan0 + (long)y * d.Stride, (byte*)pm.GetPixels() + (long)y * pm.RowBytes, pm.RowBytes, Math.Min(pm.RowBytes, Math.Abs(d.Stride)));
            }
            finally { src.UnlockBits(d); }
            return b;
        }

        public unsafe System.Drawing.Bitmap ToGdi()
        {
            System.Drawing.Bitmap g = new System.Drawing.Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            BitmapData d = g.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                using (SKPixmap pm = surface.PeekPixels())
                    for (int y = 0; y < Height; y++)
                        Buffer.MemoryCopy((byte*)pm.GetPixels() + (long)y * pm.RowBytes, (byte*)d.Scan0 + (long)y * d.Stride, Math.Abs(d.Stride), Math.Min(pm.RowBytes, Math.Abs(d.Stride)));
            }
            finally { g.UnlockBits(d); }
            return g;
        }
        public static implicit operator System.Drawing.Bitmap(Bitmap b) { if (b == null) return null; if (b.gdiCopy == null) b.gdiCopy = b.ToGdi(); return b.gdiCopy; }
        public static implicit operator System.Drawing.Image(Bitmap b) { return (System.Drawing.Bitmap)b; }

        public void Dispose() { if (surface != null) { surface.Dispose(); surface = null; } }
    }

    public sealed class ImageAttributes : IDisposable
    {
        internal SKColorFilter filter;
        public void SetColorMatrix(ColorMatrix cm)
        {
            float[] m = new float[20];
            for (int row = 0; row < 4; row++) { for (int col = 0; col < 4; col++) m[row * 5 + col] = cm[col, row]; m[row * 5 + 4] = cm[4, row]; }
            if (filter != null) filter.Dispose();
            filter = SKColorFilter.CreateColorMatrix(m);
        }
        public void Dispose() { if (filter != null) { filter.Dispose(); filter = null; } }
    }

    public sealed class GraphicsState
    {
        internal int Count; internal float Off, Scale; internal bool AA; internal SKBlendMode Blend; internal InterpolationMode Interp;
        internal PixelOffsetMode Pom; internal SmoothingMode Sm; internal TextRenderingHint Hint;
    }

    // ---------------- холст ----------------
    public sealed class Graphics : IDisposable
    {
        internal readonly SKCanvas c;
        readonly SKPaint fill = new SKPaint(), stroke = new SKPaint();
        readonly SKPath scratch = new SKPath();
        bool aa; float off = 0.5f, scale = 1f; SKBlendMode blend = SKBlendMode.SrcOver;
        SmoothingMode sm = SmoothingMode.None; PixelOffsetMode pom = PixelOffsetMode.None; InterpolationMode interp = InterpolationMode.Bilinear;
        TextRenderingHint hint = TextRenderingHint.SystemDefault; CompositingMode comp = CompositingMode.SourceOver;

        internal Graphics(SKCanvas canvas)
        {
            c = canvas;
            fill.Style = SKPaintStyle.Fill; stroke.Style = SKPaintStyle.Stroke;
            c.Save();
            c.Translate(off, off);
        }
        public static Graphics FromImage(Bitmap b) { b.surface.Canvas.ResetMatrix(); return new Graphics(b.surface.Canvas); }
        public void Dispose() { c.RestoreToCount(1); c.ResetMatrix(); fill.Dispose(); stroke.Dispose(); scratch.Dispose(); }

        public float DpiX { get { return 96f; } }
        public float DpiY { get { return 96f; } }
        public SmoothingMode SmoothingMode { get { return sm; } set { sm = value; aa = value == SmoothingMode.AntiAlias || value == SmoothingMode.HighQuality; } }
        public TextRenderingHint TextRenderingHint { get { return hint; } set { hint = value; } }
        public InterpolationMode InterpolationMode { get { return interp; } set { interp = value; } }
        public CompositingQuality CompositingQuality { get; set; }
        public CompositingMode CompositingMode { get { return comp; } set { comp = value; blend = value == CompositingMode.SourceCopy ? SKBlendMode.Src : SKBlendMode.SrcOver; } }
        public PixelOffsetMode PixelOffsetMode
        {
            get { return pom; }
            set { pom = value; SetOff(value == PixelOffsetMode.Half || value == PixelOffsetMode.HighQuality ? 0f : 0.5f); }
        }
        // GDI+ по умолчанию считает, что координата — центр пикселя; Skia — что угол. Сдвигаем на полпикселя, чтобы тонкие линии остались чёткими.
        void SetOff(float v)
        {
            if (v == off) return;
            SKMatrix m = c.TotalMatrix; m.TransX += v - off; m.TransY += v - off; c.SetMatrix(m); off = v;
        }

        // ---- состояние и преобразования ----
        public GraphicsState Save()
        {
            GraphicsState s = new GraphicsState();
            s.Count = c.Save(); s.Off = off; s.Scale = scale; s.AA = aa; s.Blend = blend; s.Interp = interp; s.Pom = pom; s.Sm = sm; s.Hint = hint;
            return s;
        }
        public void Restore(GraphicsState s)
        {
            c.RestoreToCount(s.Count);
            off = s.Off; scale = s.Scale; aa = s.AA; blend = s.Blend; interp = s.Interp; pom = s.Pom; sm = s.Sm; hint = s.Hint;
            comp = blend == SKBlendMode.Src ? CompositingMode.SourceCopy : CompositingMode.SourceOver;
        }
        public void TranslateTransform(float dx, float dy) { c.Translate(dx, dy); }
        public void ScaleTransform(float sx, float sy) { c.Scale(sx, sy); scale *= (float)Math.Sqrt(Math.Abs(sx * sy)); }
        public void RotateTransform(float deg) { c.RotateDegrees(deg); }
        public void ResetTransform() { c.ResetMatrix(); c.Translate(off, off); scale = 1f; }
        public void SetClip(GraphicsPath p) { c.ClipPath(p.sk, SKClipOperation.Intersect, true); }
        public void SetClip(GraphicsPath p, CombineMode m) { c.ClipPath(p.sk, m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, true); }
        public void SetClip(RectangleF r) { c.ClipRect(Sk.R(r), SKClipOperation.Intersect, aa); }
        public void SetClip(Rectangle r) { c.ClipRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom), SKClipOperation.Intersect, false); }
        public void SetClip(RectangleF r, CombineMode m) { c.ClipRect(Sk.R(r), m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, aa); }
        public void Clear(Color col) { c.Clear(Sk.C(col)); }

        // ---- краски ----
        SKPaint F(Brush b) { b.Apply(fill); fill.IsAntialias = aa; fill.BlendMode = blend; return fill; }
        SKPaint S(Pen p)
        {
            if (p.Brush != null) p.Brush.Apply(stroke); else { stroke.Shader = null; stroke.Color = Sk.C(p.Color); }
            float w = p.Width;
            if (w * scale < 1f) w = 1f / scale;   // как в GDI+: линия не бывает тоньше одного пикселя
            stroke.StrokeWidth = w; stroke.IsAntialias = aa; stroke.BlendMode = blend;
            stroke.StrokeCap = p.StartCap == LineCap.Round || p.EndCap == LineCap.Round ? SKStrokeCap.Round : (p.StartCap == LineCap.Square || p.EndCap == LineCap.Square ? SKStrokeCap.Square : SKStrokeCap.Butt);
            stroke.StrokeJoin = p.LineJoin == LineJoin.Round ? SKStrokeJoin.Round : (p.LineJoin == LineJoin.Bevel ? SKStrokeJoin.Bevel : SKStrokeJoin.Miter);
            stroke.StrokeMiter = 10f;
            return stroke;
        }

        // ---- фигуры ----
        public void FillRectangle(Brush b, float x, float y, float w, float h) { if (w > 0 && h > 0) c.DrawRect(Sk.R(x, y, w, h), F(b)); }
        public void FillRectangle(Brush b, RectangleF r) { FillRectangle(b, r.X, r.Y, r.Width, r.Height); }
        public void FillRectangle(Brush b, Rectangle r) { FillRectangle(b, r.X, r.Y, r.Width, r.Height); }
        public void DrawRectangle(Pen p, float x, float y, float w, float h) { c.DrawRect(Sk.R(x, y, w, h), S(p)); }
        public void DrawRectangle(Pen p, Rectangle r) { DrawRectangle(p, r.X, r.Y, r.Width, r.Height); }
        public void DrawRectangle(Pen p, RectangleF r) { DrawRectangle(p, r.X, r.Y, r.Width, r.Height); }
        public void FillEllipse(Brush b, float x, float y, float w, float h) { if (w > 0 && h > 0) c.DrawOval(Sk.R(x, y, w, h), F(b)); }
        public void FillEllipse(Brush b, RectangleF r) { FillEllipse(b, r.X, r.Y, r.Width, r.Height); }
        public void FillEllipse(Brush b, Rectangle r) { FillEllipse(b, r.X, r.Y, r.Width, r.Height); }
        public void DrawEllipse(Pen p, float x, float y, float w, float h) { if (w > 0 && h > 0) c.DrawOval(Sk.R(x, y, w, h), S(p)); }
        public void DrawEllipse(Pen p, RectangleF r) { DrawEllipse(p, r.X, r.Y, r.Width, r.Height); }
        public void DrawEllipse(Pen p, Rectangle r) { DrawEllipse(p, r.X, r.Y, r.Width, r.Height); }
        public void DrawLine(Pen p, float x1, float y1, float x2, float y2) { c.DrawLine(x1, y1, x2, y2, S(p)); }
        public void DrawLine(Pen p, PointF a, PointF b) { c.DrawLine(a.X, a.Y, b.X, b.Y, S(p)); }
        public void DrawLine(Pen p, Point a, Point b) { c.DrawLine(a.X, a.Y, b.X, b.Y, S(p)); }
        public void DrawLines(Pen p, PointF[] pts) { if (pts.Length < 2) return; scratch.Rewind(); scratch.AddPoly(Sk.Pts(pts), false); c.DrawPath(scratch, S(p)); }
        public void DrawLines(Pen p, Point[] pts) { if (pts.Length < 2) return; scratch.Rewind(); scratch.AddPoly(Sk.Pts(pts), false); c.DrawPath(scratch, S(p)); }
        public void DrawPolygon(Pen p, PointF[] pts) { if (pts.Length < 2) return; scratch.Rewind(); scratch.AddPoly(Sk.Pts(pts), true); c.DrawPath(scratch, S(p)); }
        public void DrawPolygon(Pen p, Point[] pts) { if (pts.Length < 2) return; scratch.Rewind(); scratch.AddPoly(Sk.Pts(pts), true); c.DrawPath(scratch, S(p)); }
        public void FillPolygon(Brush b, PointF[] pts) { if (pts.Length < 3) return; scratch.Rewind(); scratch.FillType = SKPathFillType.EvenOdd; scratch.AddPoly(Sk.Pts(pts), true); c.DrawPath(scratch, F(b)); }
        public void FillPolygon(Brush b, Point[] pts) { if (pts.Length < 3) return; scratch.Rewind(); scratch.FillType = SKPathFillType.EvenOdd; scratch.AddPoly(Sk.Pts(pts), true); c.DrawPath(scratch, F(b)); }
        public void DrawBezier(Pen p, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
        { scratch.Rewind(); scratch.MoveTo(x1, y1); scratch.CubicTo(x2, y2, x3, y3, x4, y4); c.DrawPath(scratch, S(p)); }
        public void DrawBezier(Pen p, PointF a, PointF b, PointF cc, PointF d) { DrawBezier(p, a.X, a.Y, b.X, b.Y, cc.X, cc.Y, d.X, d.Y); }
        public void DrawArc(Pen p, float x, float y, float w, float h, float start, float sweep)
        {
            if (w <= 0 || h <= 0) return;
            if (Math.Abs(sweep) >= 360f) c.DrawOval(Sk.R(x, y, w, h), S(p)); else c.DrawArc(Sk.R(x, y, w, h), start, sweep, false, S(p));
        }
        public void DrawArc(Pen p, RectangleF r, float start, float sweep) { DrawArc(p, r.X, r.Y, r.Width, r.Height, start, sweep); }
        public void DrawArc(Pen p, Rectangle r, float start, float sweep) { DrawArc(p, r.X, r.Y, r.Width, r.Height, start, sweep); }
        public void FillPie(Brush b, float x, float y, float w, float h, float start, float sweep)
        {
            if (w <= 0 || h <= 0) return;
            if (Math.Abs(sweep) >= 360f) c.DrawOval(Sk.R(x, y, w, h), F(b)); else c.DrawArc(Sk.R(x, y, w, h), start, sweep, true, F(b));
        }
        public void FillPie(Brush b, Rectangle r, float start, float sweep) { FillPie(b, r.X, r.Y, r.Width, r.Height, start, sweep); }
        public void FillPie(Brush b, RectangleF r, float start, float sweep) { FillPie(b, r.X, r.Y, r.Width, r.Height, start, sweep); }
        public void DrawPie(Pen p, float x, float y, float w, float h, float start, float sweep) { if (w > 0 && h > 0) c.DrawArc(Sk.R(x, y, w, h), start, sweep, true, S(p)); }
        public void FillPath(Brush b, GraphicsPath p) { c.DrawPath(p.sk, F(b)); }
        public void DrawPath(Pen pen, GraphicsPath p) { c.DrawPath(p.sk, S(pen)); }

        // ---- картинки ----
        SKSamplingOptions Sampling { get { return new SKSamplingOptions(interp == InterpolationMode.NearestNeighbor ? SKFilterMode.Nearest : SKFilterMode.Linear); } }
        void Blit(Bitmap b, SKRect src, SKRect dst, ImageAttributes ia)
        {
            fill.Shader = null; fill.Color = SKColors.White; fill.BlendMode = blend; fill.IsAntialias = false;
            fill.ColorFilter = ia != null ? ia.filter : null;
            int n = c.Save();
            if (off != 0) { SKMatrix m = c.TotalMatrix; m.TransX -= off; m.TransY -= off; c.SetMatrix(m); }   // картинки — пиксель в пиксель
            using (SKImage img = b.surface.Snapshot()) c.DrawImage(img, src, dst, Sampling, fill);
            c.RestoreToCount(n);
            fill.ColorFilter = null;
        }
        public void DrawImage(Bitmap b, float x, float y) { Blit(b, new SKRect(0, 0, b.Width, b.Height), Sk.R(x, y, b.Width, b.Height), null); }
        public void DrawImage(Bitmap b, Point p) { DrawImage(b, p.X, p.Y); }
        public void DrawImage(Bitmap b, PointF p) { DrawImage(b, p.X, p.Y); }
        public void DrawImageUnscaled(Bitmap b, int x, int y) { DrawImage(b, x, y); }
        public void DrawImage(Bitmap b, float x, float y, float w, float h) { Blit(b, new SKRect(0, 0, b.Width, b.Height), Sk.R(x, y, w, h), null); }
        public void DrawImage(Bitmap b, RectangleF r) { DrawImage(b, r.X, r.Y, r.Width, r.Height); }
        public void DrawImage(Bitmap b, Rectangle r) { DrawImage(b, r.X, r.Y, r.Width, r.Height); }
        public void DrawImage(Bitmap b, Rectangle dst, float sx, float sy, float sw, float sh, GraphicsUnit u) { Blit(b, Sk.R(sx, sy, sw, sh), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), null); }
        public void DrawImage(Bitmap b, Rectangle dst, float sx, float sy, float sw, float sh, GraphicsUnit u, ImageAttributes ia) { Blit(b, Sk.R(sx, sy, sw, sh), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), ia); }
        public void DrawImage(Bitmap b, RectangleF dst, RectangleF src, GraphicsUnit u) { Blit(b, Sk.R(src), Sk.R(dst), null); }
        public void DrawImage(Bitmap b, Rectangle dst, Rectangle src, GraphicsUnit u) { Blit(b, new SKRect(src.Left, src.Top, src.Right, src.Bottom), new SKRect(dst.Left, dst.Top, dst.Right, dst.Bottom), null); }

        // ---- текст ----
        public SizeF MeasureString(string s, Font f) { return Measure(s, f, 0, null); }
        public SizeF MeasureString(string s, Font f, int width) { return Measure(s, f, width, null); }
        public SizeF MeasureString(string s, Font f, int width, StringFormat sf) { return Measure(s, f, width, sf); }
        public SizeF MeasureString(string s, Font f, SizeF area) { return Measure(s, f, area.Width, null); }
        public SizeF MeasureString(string s, Font f, SizeF area, StringFormat sf) { return Measure(s, f, area.Width, sf); }
        public SizeF MeasureString(string s, Font f, PointF origin, StringFormat sf) { return Measure(s, f, 0, sf); }
        static float Pad(Font f, StringFormat sf) { return sf != null && sf.Typographic ? 0f : f.Px / 6f; }
        static SizeF Measure(string s, Font f, float width, StringFormat sf)
        {
            float pad = Pad(f, sf); bool noWrap = sf != null && (sf.FormatFlags & StringFormatFlags.NoWrap) != 0;
            List<TextLayout.Line> lines = TextLayout.Break(s, f, width > 0 ? width - pad * 2 : 0, noWrap);
            float w = 0; foreach (TextLayout.Line l in lines) w = Math.Max(w, l.Width);
            return new SizeF(w + pad * 2, lines.Count * f.LineH);
        }

        public void DrawString(string s, Font f, Brush b, float x, float y) { Text(s, f, b, new RectangleF(x, y, 0, 0), null); }
        public void DrawString(string s, Font f, Brush b, PointF p) { Text(s, f, b, new RectangleF(p.X, p.Y, 0, 0), null); }
        public void DrawString(string s, Font f, Brush b, float x, float y, StringFormat sf) { Text(s, f, b, new RectangleF(x, y, 0, 0), sf); }
        public void DrawString(string s, Font f, Brush b, PointF p, StringFormat sf) { Text(s, f, b, new RectangleF(p.X, p.Y, 0, 0), sf); }
        public void DrawString(string s, Font f, Brush b, RectangleF r) { Text(s, f, b, r, null); }
        public void DrawString(string s, Font f, Brush b, RectangleF r, StringFormat sf) { Text(s, f, b, r, sf); }

        void Text(string s, Font f, Brush b, RectangleF r, StringFormat sf)
        {
            if (string.IsNullOrEmpty(s)) return;
            float pad = Pad(f, sf); bool noWrap = sf != null && (sf.FormatFlags & StringFormatFlags.NoWrap) != 0;
            StringAlignment al = sf != null ? sf.Alignment : StringAlignment.Near, la = sf != null ? sf.LineAlignment : StringAlignment.Near;
            float avail = r.Width > 0 ? r.Width - pad * 2 : 0;
            List<TextLayout.Line> lines = TextLayout.Break(s, f, avail, noWrap);
            bool ell = sf != null && (sf.Trimming == StringTrimming.EllipsisCharacter || sf.Trimming == StringTrimming.EllipsisWord || sf.Trimming == StringTrimming.EllipsisPath);
            if (r.Height > 0)
            {
                // строки, которые не помещаются по высоте, не рисуем; последняя видимая — с многоточием
                int fit = Math.Max(1, (int)Math.Floor((r.Height + 0.5f) / f.LineH));
                if (lines.Count > fit) { lines.RemoveRange(fit, lines.Count - fit); if (ell) { TextLayout.Line l = lines[fit - 1]; l.Text = TextLayout.Ellipsis(l.Text + "…", f, avail); l.Width = TextLayout.Width(l.Text, f); lines[fit - 1] = l; } }
            }
            float totalH = lines.Count * f.LineH, y = r.Y;
            if (r.Height > 0 && la == StringAlignment.Center) y = r.Y + (r.Height - totalH) / 2f;
            else if (r.Height > 0 && la == StringAlignment.Far) y = r.Bottom - totalH;
            else if (r.Height <= 0 && la == StringAlignment.Center) y = r.Y - totalH / 2f;
            else if (r.Height <= 0 && la == StringAlignment.Far) y = r.Y - totalH;
            SKPaint p = F(b); p.IsAntialias = true;
            for (int i = 0; i < lines.Count; i++)
            {
                TextLayout.Line l = lines[i];
                if (l.Text.Length == 0) continue;
                if (ell && avail > 0 && l.Width > avail) { l.Text = TextLayout.Ellipsis(l.Text, f, avail); l.Width = TextLayout.Width(l.Text, f); }
                float x;
                if (al == StringAlignment.Center) x = r.Width > 0 ? r.X + (r.Width - l.Width) / 2f : r.X - l.Width / 2f;
                else if (al == StringAlignment.Far) x = r.Width > 0 ? r.Right - pad - l.Width : r.X - l.Width;
                else x = r.X + pad;
                float by = y + i * f.LineH + f.Ascent;
                TextLayout.Runs(l.Text, f, delegate (string part, SKFont ft, float dx) { c.DrawText(part, x + dx, by, SKTextAlign.Left, ft, p); });
            }
        }

        // ---- эффекты, которых не было в старом движке ----
        // размыть картинку (для «матового стекла» и мягких теней)
        public void DrawBlurred(Bitmap b, float x, float y, float sigma)
        {
            using (SKPaint p = new SKPaint())
            using (SKImageFilter f = SKImageFilter.CreateBlur(sigma, sigma))
            using (SKImage img = b.surface.Snapshot())
            {
                p.ImageFilter = f;
                c.DrawImage(img, x, y, new SKSamplingOptions(SKFilterMode.Linear), p);
            }
        }
    }
}
