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
    // ================= окно настроек =================
    // Строится из тех же пунктов, что раньше были в подменю «Настройки» (settingsRoot):
    // заголовки → вкладки, тумблеры → переключатели, подменю-выбор (размер, пугливость, монитор) → кнопки-сегменты,
    // остальные пункты → строки-действия. Обработчики пунктов те же, поэтому логика не дублируется.
    ToolStripMenuItem settingsRoot;
    SettingsWin settingsWin;

    public void OpenSettings(string tab)
    {
        if (settingsWin == null || settingsWin.IsDisposed) settingsWin = new SettingsWin(this);
        settingsWin.ShowTab(tab);
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();

    class SettingsWin : Form
    {
        class SRow
        {
            public int Kind;   // 0 заголовок, 1 тумблер, 2 выбор, 3 действие, 4 пояснение
            public string Text; public ToolStripMenuItem Item; public List<ToolStripMenuItem> Choices = new List<ToolStripMenuItem>();
            public Rectangle R; public List<Rectangle> ChoiceR = new List<Rectangle>();
        }
        class STab { public string Name; public Image Icon; public List<SRow> Rows = new List<SRow>(); public Rectangle R; }

        static readonly Color Bg = Color.FromArgb(27, 30, 42), Side = Color.FromArgb(22, 25, 35), Card = Color.FromArgb(36, 40, 55);
        static readonly Color TextC = Color.FromArgb(234, 237, 244), DimC = Color.FromArgb(140, 147, 168), Line = Color.FromArgb(52, 57, 76);

        readonly VapePet p;
        readonly float s;
        readonly Font fText, fHead, fTitle, fSmall, fTab;
        List<STab> tabs = new List<STab>();
        int cur, scroll, hoverRow = -1, hoverChoice = -1, hoverTab = -1, contentH;
        readonly System.Windows.Forms.Timer refreshT = new System.Windows.Forms.Timer();

        public SettingsWin(VapePet owner)
        {
            p = owner;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) s = g.DpiX / 96f;
            Text = "Настройки VapePet";
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = true; ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen; TopMost = true; DoubleBuffered = true; AutoScaleMode = AutoScaleMode.None;
            BackColor = Bg; KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            fText = new Font("Segoe UI", 10.5f); fHead = new Font("Segoe UI", 8f, FontStyle.Bold); fTitle = new Font("Segoe UI Semibold", 15f);
            fSmall = new Font("Segoe UI", 9f); fTab = new Font("Segoe UI", 10.5f);
            ClientSize = new Size(S(760), S(540));
            refreshT.Interval = 800; refreshT.Tick += delegate { Rebuild(); };
            KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            Rebuild();
        }

        int S(float v) { return (int)Math.Round(v * s); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = 1; try { DwmSetWindowAttribute(Handle, 20, ref dark, 4); } catch { }   // тёмный заголовок окна
        }
        protected override void OnShown(EventArgs e) { base.OnShown(e); refreshT.Start(); }
        protected override void OnFormClosed(FormClosedEventArgs e) { refreshT.Stop(); base.OnFormClosed(e); }

        public void ShowTab(string name)
        {
            Rebuild();
            if (name != null) for (int i = 0; i < tabs.Count; i++) if (tabs[i].Name.StartsWith(name)) { cur = i; scroll = 0; }
            Layout2(); Invalidate();
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        // ---------- модель ----------
        static bool IsToggle(ToolStripItem it) { return it.Tag is string && (string)it.Tag == "toggle"; }
        static bool IsHeader(ToolStripItem it) { return it is ToolStripLabel && it.Tag is string && (string)it.Tag == "header"; }

        static bool IsChoiceMenu(ToolStripMenuItem mi)
        {
            int n = 0, chk = 0;
            foreach (ToolStripItem c in mi.DropDownItems)
            {
                if (!c.Available || c is ToolStripSeparator) continue;
                ToolStripMenuItem cm = c as ToolStripMenuItem;
                if (cm == null || IsToggle(cm) || cm.DropDownItems.Count > 0) return false;
                n++; if (cm.Checked) chk++;
            }
            return n >= 1 && chk >= 1;
        }

        void Rebuild()
        {
            string was = cur < tabs.Count ? tabs[cur].Name : null;
            tabs = new List<STab>();
            if (p.settingsRoot == null) return;
            STab t = null;
            foreach (ToolStripItem it in p.settingsRoot.DropDownItems)
            {
                if (!it.Available || it is ToolStripSeparator) continue;
                if (IsHeader(it))
                {
                    string name = Clean(it.Text);
                    if (name.Length > 1) name = name.Substring(0, 1).ToUpperInvariant() + name.Substring(1).ToLowerInvariant();
                    t = new STab(); t.Name = name.StartsWith("Версия") ? "Обновления" : name; tabs.Add(t);
                    if (name.StartsWith("Версия")) { SRow v = new SRow(); v.Kind = 4; v.Text = "Установлена версия " + AppVersion + ". Новые версии ставятся сами из GitHub (не чаще раза в 6 часов)."; t.Rows.Add(v); }
                    continue;
                }
                if (t == null) { t = new STab(); t.Name = "Основное"; tabs.Add(t); }
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null) continue;
                RaiseDropDownOpening(mi);
                if (mi.DropDownItems.Count > 0)
                {
                    if (IsChoiceMenu(mi)) { t.Rows.Add(ChoiceRow(mi)); continue; }
                    // подменю с переключателями и действиями — отдельная вкладка
                    STab sub = new STab(); sub.Name = Clean(mi.Text); sub.Icon = mi.Image;
                    AddItems(sub, mi.DropDownItems, null);
                    tabs.Add(sub);
                    continue;
                }
                t.Rows.Add(LeafRow(mi));
            }
            // вкладки в удобном порядке: Вид, Котик сам, остальное, Обновления в конце
            tabs.Sort(delegate (STab a, STab b) { return Rank(a).CompareTo(Rank(b)); });
            cur = 0;
            if (was != null) for (int i = 0; i < tabs.Count; i++) if (tabs[i].Name == was) cur = i;
            if (IsHandleCreated) { Layout2(); Invalidate(); }
        }

        static int Rank(STab t) { return t.Name == "Вид" ? 0 : t.Name == "Котик сам" ? 1 : t.Name == "Обновления" ? 9 : 5; }

        void AddItems(STab tab, ToolStripItemCollection list, string head)
        {
            if (head != null) { SRow h = new SRow(); h.Kind = 0; h.Text = head; tab.Rows.Add(h); }
            foreach (ToolStripItem it in list)
            {
                if (!it.Available || it is ToolStripSeparator) continue;
                if (IsHeader(it)) { SRow h = new SRow(); h.Kind = 0; h.Text = Clean(it.Text); tab.Rows.Add(h); continue; }
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null) continue;
                if (mi.DropDownItems.Count > 0)
                {
                    RaiseDropDownOpening(mi);
                    if (IsChoiceMenu(mi)) tab.Rows.Add(ChoiceRow(mi));
                    else AddItems(tab, mi.DropDownItems, Clean(mi.Text).ToUpperInvariant());
                    continue;
                }
                tab.Rows.Add(LeafRow(mi));
            }
        }

        SRow ChoiceRow(ToolStripMenuItem mi)
        {
            SRow r = new SRow(); r.Kind = 2; r.Text = Clean(mi.Text); r.Item = mi;
            foreach (ToolStripItem c in mi.DropDownItems) { ToolStripMenuItem cm = c as ToolStripMenuItem; if (cm != null && cm.Available) r.Choices.Add(cm); }
            return r;
        }

        static SRow LeafRow(ToolStripMenuItem mi)
        {
            SRow r = new SRow(); r.Item = mi; r.Text = Clean(mi.Text);
            r.Kind = IsToggle(mi) ? 1 : 3;
            return r;
        }

        static string Clean(string t) { return (t ?? "").Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&"); }

        // ---------- раскладка ----------
        int SideW { get { return S(200); } }

        void Layout2()
        {
            int y = S(70);
            for (int i = 0; i < tabs.Count; i++) { tabs[i].R = new Rectangle(S(10), y, SideW - S(20), S(40)); y += S(44); }
            if (cur >= tabs.Count) return;
            int x0 = SideW + S(28), w = ClientSize.Width - x0 - S(28);
            y = S(76);
            foreach (SRow r in tabs[cur].Rows)
            {
                int h;
                switch (r.Kind)
                {
                    case 0: h = S(34); break;
                    case 4: h = TextRenderer.MeasureText(r.Text, fSmall, new Size(w, 1000), TextFormatFlags.WordBreak).Height + S(14); break;
                    case 2:
                        {
                            r.ChoiceR.Clear();
                            int cx = x0 + S(16), cy = y + S(38);
                            foreach (ToolStripMenuItem c in r.Choices)
                            {
                                int cw = TextRenderer.MeasureText(Clean(c.Text), fText).Width + S(28);
                                if (cx + cw > x0 + w - S(12)) { cx = x0 + S(16); cy += S(40); }
                                r.ChoiceR.Add(new Rectangle(cx, cy, cw, S(32)));
                                cx += cw + S(8);
                            }
                            h = cy + S(32) + S(14) - y;
                            break;
                        }
                    default: h = S(50); break;
                }
                r.R = new Rectangle(x0, y, w, h);
                y += h + (r.Kind == 0 ? 0 : S(8));
            }
            contentH = y + S(20);
            int maxScroll = Math.Max(0, contentH - ClientSize.Height);
            if (scroll > maxScroll) scroll = maxScroll;
        }

        // ---------- мышь ----------
        Point Content(Point pt) { return pt.X >= SideW ? new Point(pt.X, pt.Y + scroll) : pt; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int ht = -1, hr = -1, hc = -1;
            for (int i = 0; i < tabs.Count; i++) if (tabs[i].R.Contains(e.Location)) ht = i;
            if (cur < tabs.Count && e.X >= SideW)
            {
                Point c = Content(e.Location);
                List<SRow> rows = tabs[cur].Rows;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!rows[i].R.Contains(c)) continue;
                    if (rows[i].Kind == 1 || rows[i].Kind == 3) hr = i;
                    if (rows[i].Kind == 2) for (int k = 0; k < rows[i].ChoiceR.Count; k++) if (rows[i].ChoiceR[k].Contains(c)) { hr = i; hc = k; }
                }
            }
            if (ht != hoverTab || hr != hoverRow || hc != hoverChoice) { hoverTab = ht; hoverRow = hr; hoverChoice = hc; Cursor = (ht >= 0 || hr >= 0) ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverTab = hoverRow = hoverChoice = -1; Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int maxScroll = Math.Max(0, contentH - ClientSize.Height);
            scroll = Math.Max(0, Math.Min(maxScroll, scroll - e.Delta / 2));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (hoverTab >= 0) { cur = hoverTab; scroll = 0; Layout2(); Invalidate(); return; }
            if (cur >= tabs.Count || hoverRow < 0 || hoverRow >= tabs[cur].Rows.Count) return;
            SRow r = tabs[cur].Rows[hoverRow];
            ToolStripMenuItem target = r.Kind == 2 ? (hoverChoice >= 0 && hoverChoice < r.Choices.Count ? r.Choices[hoverChoice] : null) : r.Item;
            if (target == null) return;
            try { target.PerformClick(); } catch (Exception ex) { p.GuardLog("настройки", ex); }
            p.HudChanged();
            BeginInvoke((Action)delegate { if (!IsDisposed) Rebuild(); });
        }

        // ---------- рисование ----------
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Bg);
            // боковая панель
            using (SolidBrush b = new SolidBrush(Side)) g.FillRectangle(b, 0, 0, SideW, ClientSize.Height);
            using (Pen pen = new Pen(Line)) g.DrawLine(pen, SideW, 0, SideW, ClientSize.Height);
            Paw(g, S(30), S(34), S(9), MenuAc);
            TextRenderer.DrawText(g, "VapePet", fTitle, new Point(S(46), S(18)), TextC, TextFormatFlags.NoPrefix);
            for (int i = 0; i < tabs.Count; i++)
            {
                STab t = tabs[i]; bool on = i == cur, hot = i == hoverTab;
                if (on || hot)
                    using (GraphicsPath gp = RoundRect(t.R.X, t.R.Y, t.R.Width, t.R.Height, S(8)))
                    using (SolidBrush b = new SolidBrush(on ? Color.FromArgb(70, 54, 40) : Color.FromArgb(34, 38, 52))) g.FillPath(b, gp);
                if (on) using (GraphicsPath ap = RoundRect(t.R.X + S(2), t.R.Y + S(10), S(3), t.R.Height - S(20), S(1.5f))) using (SolidBrush b = new SolidBrush(MenuAc)) g.FillPath(b, ap);
                TextRenderer.DrawText(g, t.Name, fTab, new Rectangle(t.R.X + S(16), t.R.Y, t.R.Width - S(20), t.R.Height), on ? Color.FromArgb(255, 216, 176) : TextC, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            TextRenderer.DrawText(g, "версия " + AppVersion, fSmall, new Rectangle(S(20), ClientSize.Height - S(34), SideW - S(30), S(24)), DimC, TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            if (cur >= tabs.Count) return;

            // заголовок вкладки
            int x0 = SideW + S(28);
            g.SetClip(new Rectangle(SideW + 1, 0, ClientSize.Width - SideW - 1, ClientSize.Height));
            g.TranslateTransform(0, -scroll);
            TextRenderer.DrawText(g, tabs[cur].Name, fTitle, new Point(x0 - S(2), S(20)), TextC, TextFormatFlags.NoPrefix);
            List<SRow> rows = tabs[cur].Rows;
            for (int i = 0; i < rows.Count; i++) DrawRow(g, rows[i], i == hoverRow);
            g.ResetTransform(); g.ResetClip();
        }

        void DrawRow(Graphics g, SRow r, bool hot)
        {
            Rectangle R = r.R;
            if (r.Kind == 0)
            {
                TextRenderer.DrawText(g, r.Text.ToUpperInvariant(), fHead, new Rectangle(R.X + S(4), R.Y + S(10), R.Width, R.Height - S(10)), DimC, TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                return;
            }
            if (r.Kind == 4)
            {
                TextRenderer.DrawText(g, r.Text, fSmall, new Rectangle(R.X + S(4), R.Y, R.Width - S(8), R.Height), DimC, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                return;
            }
            using (GraphicsPath gp = RoundRect(R.X, R.Y, R.Width, R.Height, S(10)))
            {
                using (SolidBrush b = new SolidBrush(hot && r.Kind != 2 ? Color.FromArgb(44, 48, 66) : Card)) g.FillPath(b, gp);
                using (Pen pen = new Pen(Color.FromArgb(46, 51, 70))) g.DrawPath(pen, gp);
            }
            int ix = R.X + S(16);
            if (r.Item != null && r.Item.Image != null && r.Kind != 1)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(r.Item.Image, new Rectangle(ix, R.Y + (r.Kind == 2 ? S(9) : (R.Height - S(22)) / 2), S(22), S(22)));
                ix += S(32);
            }
            if (r.Kind == 2)
            {
                TextRenderer.DrawText(g, r.Text, fText, new Rectangle(ix, R.Y + S(6), R.Width - S(40), S(28)), TextC, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                for (int k = 0; k < r.Choices.Count; k++)
                {
                    Rectangle c = r.ChoiceR[k]; bool on = r.Choices[k].Checked, h = hot && k == hoverChoice;
                    using (GraphicsPath gp = RoundRect(c.X, c.Y, c.Width, c.Height, c.Height / 2f))
                    {
                        using (SolidBrush b = new SolidBrush(on ? MenuAc : h ? Color.FromArgb(62, 68, 90) : Color.FromArgb(48, 53, 70))) g.FillPath(b, gp);
                    }
                    TextRenderer.DrawText(g, Clean(r.Choices[k].Text), fText, c, on ? Color.FromArgb(40, 24, 10) : TextC, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                return;
            }
            Color tc = r.Item != null && !r.Item.Enabled ? DimC : TextC;
            TextRenderer.DrawText(g, r.Text, fText, new Rectangle(ix, R.Y, R.Right - ix - S(80), R.Height), tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (r.Kind == 1)
            {
                bool on = r.Item.Checked;
                Rectangle sw = new Rectangle(R.Right - S(62), R.Y + (R.Height - S(26)) / 2, S(46), S(26));
                using (GraphicsPath gp = RoundRect(sw.X, sw.Y, sw.Width, sw.Height, sw.Height / 2f))
                using (SolidBrush b = new SolidBrush(on ? MenuAc : Color.FromArgb(70, 76, 98))) g.FillPath(b, gp);
                int d = S(20), kx = on ? sw.Right - d - S(3) : sw.X + S(3);
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, sw.Y + S(3), d, d);
            }
            else
            {
                int cx = R.Right - S(24), cy = R.Y + R.Height / 2;
                using (Pen pen = new Pen(hot ? MenuAc : Color.FromArgb(130, 136, 158), 1.8f * s)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLines(pen, new Point[] { new Point(cx - S(3), cy - S(5)), new Point(cx + S(2), cy), new Point(cx - S(3), cy + S(5)) }); }
            }
        }

        static void Paw(Graphics g, float cx, float cy, float r, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillEllipse(b, cx - r * 0.75f, cy - r * 0.2f, r * 1.5f, r * 1.25f);
                g.FillEllipse(b, cx - r * 1.25f, cy - r * 0.95f, r * 0.62f, r * 0.75f);
                g.FillEllipse(b, cx - r * 0.55f, cy - r * 1.45f, r * 0.62f, r * 0.75f);
                g.FillEllipse(b, cx + r * 0.0f, cy - r * 1.45f, r * 0.62f, r * 0.75f);
                g.FillEllipse(b, cx + r * 0.65f, cy - r * 0.95f, r * 0.62f, r * 0.75f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { refreshT.Dispose(); fText.Dispose(); fHead.Dispose(); fTitle.Dispose(); fSmall.Dispose(); fTab.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
