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
    // ================= своё меню (вместо стандартного меню Windows) =================
    // Пункты по-прежнему собираются в ContextMenuStrip «menu» (так их строит весь остальной код),
    // но показывает их собственное окно CatMenu: без задержек Windows, с подсветкой, поиском и тумблерами.
    CatMenu catMenu;
    public bool MenuOpen { get { return catMenu != null && !catMenu.IsDisposed && catMenu.Visible; } }
    DateTime menuAwaySince = DateTime.MinValue;

    public void ShowMenu(Point at)
    {
        CloseMenu();
        catMenu = new CatMenu(this, menu.Items, null);
        catMenu.ShowAt(new Rectangle(at.X, at.Y, 0, 0), false);
        menuAwaySince = DateTime.MinValue;
    }

    public void CloseMenu()
    {
        CatMenu m = catMenu; catMenu = null;
        if (m != null) { try { m.CloseTree(); } catch { } }
    }

    // клик мимо меню — закрыть; меню долго без внимания — тоже закрыть
    void MenuWatch()
    {
        if (!MenuOpen) { lmbWasDown = (GetAsyncKeyState(0x01) & 0x8000) != 0; return; }
        bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;
        Point p = Cursor.Position;
        bool inside = catMenu.TreeContains(p);
        if (down && !lmbWasDown && !inside) { CloseMenu(); lmbWasDown = down; return; }
        lmbWasDown = down;
        if (inside || catMenu.ContainsFocus) menuAwaySince = DateTime.MinValue;
        else if (menuAwaySince == DateTime.MinValue) menuAwaySince = DateTime.Now;
        else if ((DateTime.Now - menuAwaySince).TotalSeconds > 25) CloseMenu();
    }

    static void RaiseDropDownOpening(ToolStripMenuItem mi)
    {
        try
        {
            System.Reflection.MethodInfo m = typeof(ToolStripDropDownItem).GetMethod("OnDropDownShow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (m != null) m.Invoke(mi, new object[] { EventArgs.Empty });
        }
        catch { }
    }

    static void RaiseHostKeyDown(ToolStripControlHost h, KeyEventArgs e)
    {
        try
        {
            System.Reflection.MethodInfo m = typeof(ToolStripControlHost).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new Type[] { typeof(KeyEventArgs) }, null);
            if (m != null) m.Invoke(h, new object[] { e });
        }
        catch { }
    }

    static bool HasSub(ToolStripItem it)
    {
        ToolStripMenuItem mi = it as ToolStripMenuItem;
        return mi != null && mi.DropDownItems.Count > 0;
    }

    // скруглённый прямоугольник для окон WinForms (меню, настройки) — обычный System.Drawing
    static System.Drawing.Drawing2D.GraphicsPath RoundRectW(float x, float y, float w, float h, float r)
    {
        System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath();
        float d = Math.Max(0.1f, Math.Min(r * 2, Math.Min(w, h)));
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    class CatMenu : Form
    {
        class Row
        {
            public ToolStripItem Item; public int Kind;   // 0 пункт, 1 разделитель, 2 заголовок, 3 надпись, 4 поле ввода
            public Rectangle R; public TextBox Box; public string Hint; public string[] Path;
        }

        static readonly Color Bg1 = Color.FromArgb(36, 41, 56), Bg2 = Color.FromArgb(25, 28, 40), Edge = Color.FromArgb(78, 84, 108);
        static readonly Color TextC = Color.FromArgb(234, 237, 244), DimC = Color.FromArgb(140, 147, 168), HiText = Color.FromArgb(255, 216, 176);
        static readonly Color HiBg = Color.FromArgb(70, 54, 40), FieldBg = Color.FromArgb(48, 53, 70);

        readonly VapePet p;
        readonly ToolStripItemCollection items;
        readonly CatMenu parentMenu;
        public CatMenu Child; ToolStripMenuItem childOf;
        List<Row> rows = new List<Row>();
        int hover = -1;
        readonly float s;
        readonly Font fItem, fHead, fLabel, fBox;
        readonly System.Windows.Forms.Timer hoverT = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer fadeT = new System.Windows.Forms.Timer();
        TextBox searchBox; ToolStripTextBox searchHost; string searchText = "";
        Dictionary<ToolStripItem, TextBox> boxes = new Dictionary<ToolStripItem, TextBox>();
        bool roundedByDwm;
        Rectangle anchor; bool anchorIsRow;

        public CatMenu(VapePet owner, ToolStripItemCollection list, CatMenu parent)
        {
            p = owner; items = list; parentMenu = parent;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            KeyPreview = true; DoubleBuffered = true; BackColor = Bg2; AutoScaleMode = AutoScaleMode.None;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) s = g.DpiX / 96f;
            fItem = new Font("Segoe UI", 10.5f); fHead = new Font("Segoe UI", 7.6f, FontStyle.Bold);
            fLabel = new Font("Segoe UI", 9f, FontStyle.Bold); fBox = new Font("Segoe UI", 10.5f);
            Font = fItem;
            hoverT.Interval = 130; hoverT.Tick += delegate { hoverT.Stop(); SyncChild(); };
            fadeT.Interval = 15; fadeT.Tick += delegate { Opacity = Math.Min(1.0, Opacity + 0.25); if (Opacity >= 1.0) fadeT.Stop(); };
            BuildRows();
        }

        bool IsRoot { get { return parentMenu == null; } }
        int S(float v) { return (int)Math.Round(v * s); }

        protected override bool ShowWithoutActivation { get { return !IsRoot; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x8;                  // без Alt+Tab, поверх всех
                if (parentMenu != null) cp.ExStyle |= 0x08000000;   // подменю не забирают фокус у поля ввода
                cp.ClassStyle |= 0x20000;                 // тень
                return cp;
            }
        }

        // ---------- строки ----------
        void BuildRows()
        {
            rows.Clear();
            bool resPut = false;
            List<Row> res = new List<Row>();
            if (IsRoot && searchText.Length >= 2) res = SearchRows(searchText);
            foreach (ToolStripItem it in items)
            {
                if (!it.Available) continue;
                Row r = new Row(); r.Item = it;
                if (it is ToolStripSeparator) r.Kind = 1;
                else if (it is ToolStripTextBox) r.Kind = 4;
                else if (it is ToolStripLabel) r.Kind = (it.Tag is string && (string)it.Tag == "header") ? 2 : 3;
                else r.Kind = 0;
                if (r.Kind == 1 && (rows.Count == 0 || rows[rows.Count - 1].Kind == 1)) continue;
                rows.Add(r);
                // результаты поиска — сразу под первым полем ввода
                if (r.Kind == 4 && res.Count > 0 && !resPut)
                {
                    resPut = true;
                    Row h = new Row(); h.Kind = 2; h.Hint = "#found"; rows.Add(h);
                    rows.AddRange(res);
                    Row sp = new Row(); sp.Kind = 1; rows.Add(sp);
                }
            }
            while (rows.Count > 0 && rows[rows.Count - 1].Kind == 1) rows.RemoveAt(rows.Count - 1);
            Layout2();
        }

        List<Row> SearchRows(string q)
        {
            List<Row> found = new List<Row>();
            Collect(p.menu.Items, q.ToLowerInvariant(), new List<string>(), found, true);
            if (p.settingsRoot != null) { List<string> sp = new List<string>(); sp.Add("Настройки"); Collect(p.settingsRoot.DropDownItems, q.ToLowerInvariant(), sp, found, false); }
            if (found.Count > 7) found.RemoveRange(7, found.Count - 7);
            return found;
        }

        void Collect(ToolStripItemCollection list, string q, List<string> path, List<Row> found, bool subs)
        {
            foreach (ToolStripItem it in list)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null || !mi.Available || !mi.Enabled) continue;
                string name = Clean(mi.Text);
                bool sub = mi.DropDownItems.Count > 0;
                if (name.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) >= 0 && (subs || !sub))
                {
                    Row r = new Row(); r.Kind = 0; r.Item = mi; r.Hint = path.Count > 0 ? path[path.Count - 1] : "Меню";
                    if (sub) { List<string> full = new List<string>(path); full.Add(name); r.Path = full.ToArray(); }
                    found.Add(r);
                }
                if (sub) { path.Add(name); Collect(mi.DropDownItems, q, path, found, subs); path.RemoveAt(path.Count - 1); }
            }
        }

        static string Clean(string t) { return (t ?? "").Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&"); }

        int RowH(Row r)
        {
            switch (r.Kind)
            {
                case 1: return S(11);
                case 2: return S(r.Hint == "#found" ? 26 : 27);
                case 3: return S(25);
                case 4: return S(38);
                default: return S(36);
            }
        }

        void Layout2()
        {
            int w = S(250);
            foreach (Row r in rows)
            {
                if (r.Kind == 0)
                {
                    ToolStripMenuItem mi = r.Item as ToolStripMenuItem;
                    int tw = TextRenderer.MeasureText(Clean(r.Item.Text), fItem).Width;
                    int extra = 0;
                    if (mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString)) extra += TextRenderer.MeasureText(mi.ShortcutKeyDisplayString, fHead).Width + S(16);
                    if (r.Hint != null) extra += TextRenderer.MeasureText(r.Hint, fHead).Width + S(14);
                    w = Math.Max(w, S(12 + 30 + 10) + tw + extra + S(46));
                }
                else if (r.Kind == 3) w = Math.Max(w, S(42) + TextRenderer.MeasureText(Clean(r.Item.Text), fLabel).Width + S(16));
                else if (r.Kind == 4) w = Math.Max(w, S(320));
                else if (r.Kind == 2) w = Math.Max(w, S(30) + TextRenderer.MeasureText(HeadText(r), fHead).Width);
            }
            w = Math.Min(w, S(560));
            int y = S(7);
            foreach (Row r in rows) { int h = RowH(r); r.R = new Rectangle(S(5), y, w - S(10), h); y += h; }
            Size = new Size(w, y + S(7));
            foreach (Row r in rows) if (r.Kind == 4) PlaceBox(r);
            ApplyShape();
        }

        string HeadText(Row r) { return r.Hint == "#found" ? "НАЙДЕНО В МЕНЮ" : Clean(r.Item.Text).ToUpperInvariant(); }

        void PlaceBox(Row r)
        {
            ToolStripTextBox host = (ToolStripTextBox)r.Item;
            TextBox had;
            if (boxes.TryGetValue(host, out had)) r.Box = had;
            if (r.Box == null)
            {
                TextBox b = new TextBox();
                b.BorderStyle = BorderStyle.None; b.BackColor = FieldBg; b.ForeColor = Color.White; b.Font = fBox;
                b.Text = host.Text;
                b.KeyDown += delegate (object o, KeyEventArgs e) { BoxKey(r, e); };
                if (searchHost == null || searchHost == host)
                {
                    searchHost = host; searchBox = b;
                    b.TextChanged += delegate { OnSearch(b.Text); };
                }
                r.Box = b; boxes[host] = b; Controls.Add(b);
            }
            int fh = r.Box.PreferredHeight;
            r.Box.SetBounds(r.R.X + S(14), r.R.Y + (r.R.Height - fh) / 2, r.R.Width - S(28), fh);
        }

        void OnSearch(string t)
        {
            string q = t.Trim();
            string was = searchText;
            searchText = q.Length >= 2 ? q : "";
            if (searchText == was) return;
            if (Child != null) CloseChild();
            int caret = searchBox.SelectionStart;
            hover = -1;
            BuildRows();
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            Location = new Point(Left, Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height)));
            if (searchBox != null) { searchBox.Focus(); searchBox.SelectionStart = Math.Min(caret, searchBox.TextLength); }
            Invalidate();
        }

        void BoxKey(Row r, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; p.CloseMenu(); return; }
            if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; Step(1, rows.IndexOf(r)); ActiveControl = null; Focus(); return; }
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            string text = r.Box.Text;
            ToolStripTextBox host = (ToolStripTextBox)r.Item;
            VapePet pp = p;
            pp.CloseMenu();
            host.Text = text;
            pp.BeginInvoke((Action)delegate { RaiseHostKeyDown(host, new KeyEventArgs(Keys.Enter)); });
        }

        // ---------- форма ----------
        void ApplyShape()
        {
            if (!IsHandleCreated) return;
            int pref = 2;   // Windows 11: настоящие скруглённые углы и тень
            roundedByDwm = DwmSetWindowAttribute(Handle, 33, ref pref, 4) == 0;
            if (!roundedByDwm)
            {
                using (GraphicsPath gp = RoundRectW(0, 0, Width, Height, S(9))) Region = new Region(gp);
            }
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyShape(); }

        public void ShowAt(Rectangle a, bool fromRow)
        {
            anchor = a; anchorIsRow = fromRow;
            Reposition();
            Opacity = 0;
            Show();
            if (IsRoot)
            {
                Activate();
                try { SetForegroundWindow(Handle); } catch { }
                if (searchBox != null) { searchBox.Focus(); searchBox.SelectAll(); }
            }
            fadeT.Start();
        }

        void Reposition()
        {
            Rectangle wa = Screen.FromPoint(new Point(anchor.X, anchor.Y)).WorkingArea;
            int x, y;
            if (anchorIsRow)
            {
                x = anchor.Right - S(3);
                if (x + Width > wa.Right) x = anchor.Left - Width + S(3);
                y = anchor.Top - S(7);
            }
            else
            {
                x = anchor.X; y = anchor.Y;
                if (x + Width > wa.Right) x = anchor.X - Width;
                if (y + Height > wa.Bottom) y = anchor.Y - Height;
            }
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height));
            Location = new Point(x, y);
        }

        public bool TreeContains(Point pt)
        {
            for (CatMenu m = this; m != null; m = m.Child) if (!m.IsDisposed && m.Visible && m.Bounds.Contains(pt)) return true;
            return false;
        }

        public void CloseTree()
        {
            if (Child != null) Child.CloseTree();
            Child = null;
            hoverT.Stop(); fadeT.Stop();
            Close(); Dispose();
        }

        void CloseChild()
        {
            if (Child == null) return;
            Child.CloseTree(); Child = null; childOf = null;
            if (IsRoot && searchBox != null && !searchBox.Focused && ActiveControl == null) { }
        }

        void OpenChild(int idx, bool viaKeys)
        {
            ToolStripMenuItem mi = rows[idx].Item as ToolStripMenuItem;
            if (mi == null) return;
            if (Child != null && childOf == mi) return;
            CloseChild();
            RaiseDropDownOpening(mi);
            if (mi.DropDownItems.Count == 0) return;
            Child = new CatMenu(p, mi.DropDownItems, this); childOf = mi;
            Rectangle rr = rows[idx].R; rr.Offset(Location);
            Child.ShowAt(rr, true);
            if (viaKeys) { Child.Step(1, -1); }
            Invalidate();
        }

        void SyncChild()
        {
            if (hover < 0 || hover >= rows.Count) return;
            Row r = rows[hover];
            if (r.Kind == 0 && r.Hint == null && HasSub(r.Item) && r.Item.Enabled) OpenChild(hover, false);
            else CloseChild();
        }

        bool Selectable(Row r) { return r.Kind == 0 && r.Item.Enabled; }

        int Hit(Point pt)
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i].R.Contains(pt) && Selectable(rows[i])) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Hit(e.Location);
            if (h == hover) return;
            hover = h; Invalidate();
            hoverT.Stop();
            if (h >= 0) hoverT.Start();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverT.Stop();
            if (Child == null && hover >= 0) { hover = -1; Invalidate(); }
            else if (Child != null && childOf != null) { int i = rows.FindIndex(x => x.Item == childOf); if (i != hover) { hover = i; Invalidate(); } }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int h = Hit(e.Location);
            if (h >= 0) ActivateRow(h);
        }

        void ActivateRow(int idx)
        {
            Row r = rows[idx];
            if (r.Hint == null && HasSub(r.Item)) { hoverT.Stop(); OpenChild(idx, false); return; }
            if (r.Path != null) { VapePet q = p; string[] path = r.Path; q.CloseMenu(); q.BeginInvoke((Action)delegate { q.OpenMenuPath(path); }); return; }
            ToolStripItem it = r.Item;
            bool keep = it.Tag is string && (string)it.Tag == "toggle";
            if (keep) { it.PerformClick(); Invalidate(); p.HudChanged(); return; }
            VapePet pp = p;
            pp.CloseMenu();
            pp.BeginInvoke((Action)delegate { try { it.PerformClick(); } catch (Exception ex) { pp.GuardLog("пункт меню", ex); } pp.HudChanged(); });
        }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); }

        public bool OpenByName(string name)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Kind == 0 && rows[i].Hint == null && Clean(rows[i].Item.Text).StartsWith(name) && HasSub(rows[i].Item))
                { hover = i; OpenChild(i, false); return Child != null; }
            return false;
        }

        // ---------- клавиатура ----------
        CatMenu Deepest() { CatMenu m = this; while (m.Child != null && !m.Child.IsDisposed) m = m.Child; return m; }

        void Step(int dir, int from)
        {
            int n = rows.Count, i = from;
            for (int k = 0; k < n; k++)
            {
                i += dir; if (i < 0) i = n - 1; if (i >= n) i = 0;
                if (Selectable(rows[i])) { hover = i; Invalidate(); return; }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            if (ActiveControl is TextBox && e.KeyCode != Keys.Escape) return;
            CatMenu m = Deepest();
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    e.Handled = true; e.SuppressKeyPress = true;
                    if (m == this) p.CloseMenu(); else { m.parentMenu.CloseChild(); }
                    return;
                case Keys.Down: e.Handled = true; m.Step(1, m.hover); return;
                case Keys.Up: e.Handled = true; m.Step(-1, m.hover < 0 ? m.rows.Count : m.hover); return;
                case Keys.Right: e.Handled = true; if (m.hover >= 0 && m.rows[m.hover].Hint == null && HasSub(m.rows[m.hover].Item)) m.OpenChild(m.hover, true); return;
                case Keys.Left: e.Handled = true; if (m != this) m.parentMenu.CloseChild(); return;
                case Keys.Enter: e.Handled = true; e.SuppressKeyPress = true; if (m.hover >= 0) { if (m.rows[m.hover].Hint == null && HasSub(m.rows[m.hover].Item)) m.OpenChild(m.hover, true); else m.ActivateRow(m.hover); } return;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            // начали печатать, когда курсор не в поле — переводим в поле «Спроси котика»
            if (IsRoot && searchBox != null && !searchBox.Focused && !char.IsControl(e.KeyChar))
            {
                CloseChild();
                searchBox.Focus(); searchBox.AppendText(e.KeyChar.ToString()); e.Handled = true;
            }
        }

        // ---------- рисование ----------
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Bg1, Bg2, 90f)) g.FillRectangle(b, ClientRectangle);
            using (Pen sh = new Pen(Color.FromArgb(22, 255, 255, 255), 1)) g.DrawLine(sh, S(12), 1, Width - S(12), 1);
            if (!roundedByDwm) using (GraphicsPath gp = RoundRectW(0.5f, 0.5f, Width - 1.5f, Height - 1.5f, S(9))) using (Pen pen = new Pen(Edge, 1)) g.DrawPath(pen, gp);
            for (int i = 0; i < rows.Count; i++) DrawRow(g, rows[i], i == hover || (Child != null && childOf != null && rows[i].Item == childOf && rows[i].Hint == null));
        }

        void DrawRow(Graphics g, Row r, bool hot)
        {
            Rectangle R = r.R;
            switch (r.Kind)
            {
                case 1:
                    using (Pen pen = new Pen(Color.FromArgb(54, 59, 78))) g.DrawLine(pen, R.X + S(10), R.Y + R.Height / 2, R.Right - S(10), R.Y + R.Height / 2);
                    return;
                case 2:
                    TextRenderer.DrawText(g, HeadText(r), fHead, new Rectangle(R.X + S(12), R.Y + S(8), R.Width - S(16), R.Height - S(8)), r.Hint == "#found" ? MenuAc : DimC, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
                    return;
                case 3:
                    {
                        int ix = R.X + S(10);
                        if (r.Item.Image != null) { DrawIcon(g, r.Item.Image, new Rectangle(ix, R.Y + (R.Height - S(18)) / 2, S(18), S(18))); }
                        Color c = r.Item.ForeColor.A > 0 ? r.Item.ForeColor : TextC;
                        TextRenderer.DrawText(g, Clean(r.Item.Text), fLabel, new Rectangle(ix + S(24), R.Y, R.Width - S(40), R.Height), c, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                        return;
                    }
                case 4:
                    {
                        Rectangle f = new Rectangle(R.X + S(6), R.Y + S(4), R.Width - S(12), R.Height - S(8));
                        bool focus = r.Box != null && r.Box.Focused;
                        using (GraphicsPath gp = RoundRectW(f.X, f.Y, f.Width, f.Height, S(7)))
                        {
                            using (SolidBrush b = new SolidBrush(FieldBg)) g.FillPath(b, gp);
                            using (Pen pen = new Pen(focus ? MenuAc : Color.FromArgb(78, 86, 110), focus ? 1.6f : 1f)) g.DrawPath(pen, gp);
                        }
                        return;
                    }
            }
            ToolStripMenuItem mi = r.Item as ToolStripMenuItem;
            bool en = r.Item.Enabled;
            if (hot && en)
            {
                using (GraphicsPath gp = RoundRectW(R.X, R.Y + 1, R.Width, R.Height - 2, S(7)))
                using (LinearGradientBrush b = new LinearGradientBrush(R, Color.FromArgb(84, 62, 42), HiBg, 0f)) g.FillPath(b, gp);
                using (GraphicsPath ap = RoundRectW(R.X + S(2), R.Y + S(9), S(3), R.Height - S(18), S(1.5f))) using (SolidBrush b = new SolidBrush(MenuAc)) g.FillPath(b, ap);
            }
            bool toggle = r.Item.Tag is string && (string)r.Item.Tag == "toggle";
            int x = R.X + S(12);
            Rectangle ico = new Rectangle(x, R.Y + (R.Height - S(22)) / 2, S(22), S(22));
            if (r.Item.Image != null) DrawIcon(g, r.Item.Image, toggle ? new Rectangle(ico.X - S(3), ico.Y - S(3), S(28), S(28)) : ico);
            else if (mi != null && mi.Checked) DrawCheck(g, ico);
            x += S(32);
            Color tc = !en ? Color.FromArgb(110, 116, 136) : hot ? HiText : TextC;
            int right = R.Right - S(12);
            if (r.Hint == null && HasSub(r.Item))
            {
                int cx = R.Right - S(16), cy = R.Y + R.Height / 2;
                using (Pen pen = new Pen(hot ? MenuAc : Color.FromArgb(130, 136, 158), 1.8f * s)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLines(pen, new Point[] { new Point(cx - S(3), cy - S(5)), new Point(cx + S(2), cy), new Point(cx - S(3), cy + S(5)) }); }
                right -= S(18);
            }
            else if (mi != null && mi.Checked && r.Item.Image != null && !toggle)
            {
                DrawCheck(g, new Rectangle(R.Right - S(30), R.Y + (R.Height - S(18)) / 2, S(18), S(18)));
                right -= S(24);
            }
            string tail = r.Hint ?? (mi != null ? mi.ShortcutKeyDisplayString : null);
            if (!string.IsNullOrEmpty(tail))
            {
                Size ts = TextRenderer.MeasureText(tail, fHead);
                TextRenderer.DrawText(g, tail, fHead, new Rectangle(right - ts.Width, R.Y, ts.Width, R.Height), DimC, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                right -= ts.Width + S(10);
            }
            TextRenderer.DrawText(g, Clean(r.Item.Text), fItem, new Rectangle(x, R.Y, right - x, R.Height), tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        void DrawIcon(Graphics g, Image img, Rectangle r)
        {
            InterpolationMode im = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(img, r);
            g.InterpolationMode = im;
        }

        void DrawCheck(Graphics g, Rectangle r)
        {
            using (Pen pen = new Pen(MenuAc, 2.4f * s)) { pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new PointF[] { new PointF(r.X + r.Width * 0.2f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.74f), new PointF(r.X + r.Width * 0.82f, r.Y + r.Height * 0.28f) }); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { hoverT.Dispose(); fadeT.Dispose(); fItem.Dispose(); fHead.Dispose(); fLabel.Dispose(); fBox.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // открыть меню сразу на нужном подменю: OpenMenuPath("Инструменты", "Таймер")
    void OpenMenuPath(params string[] path)
    {
        Point at = Cursor.Position;
        ShowMenu(new Point(at.X - 20, Math.Max(0, at.Y - 40)));
        CatMenu m = catMenu;
        foreach (string name in path)
        {
            if (m == null) return;
            if (!m.OpenByName(name)) return;
            m = m.Child;
        }
    }
}
