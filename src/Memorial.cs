using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// ================= сценка: бар «Помним CLAUDE» (mode 7) =================
// Все наши котики сидят в мрачном баре, пьют, кто-то плачет, вспоминают большого
// брутального кота CLAUDE (IT, мотоцикл, всем помогал). Воспоминания — в облачке над
// главным котиком. В конце все затягиваются, и в паре проступает надпись.
partial class VapePet
{
    Cat barBoss, barJan, barWhite, barGinger;
    int memKind, memT, memDur;            // 0 нет, 1 мотоцикл, 2 IT, 3 помогает
    string memCaption;
    int vaporT;                           // финальная надпись в паре
    const int VaporDur = 520;
    int barRuns, toastT;

    float StoolX(int i) { return A + U(18 + 36 * i); }
    float SeatH { get { return U(10); } }

    static Cat MakeClaude()
    {
        Cat c = new Cat();
        c.Id = 4242;
        c.MainC = Color.FromArgb(92, 96, 110); c.BellyC = Color.FromArgb(205, 206, 216); c.Stripes = true;
        c.IrisC = Color.FromArgb(90, 180, 245);
        c.Outfit = 9; c.C1 = Color.FromArgb(28, 26, 32); c.C2 = Color.FromArgb(40, 44, 56);   // кожанка
        c.Glasses = true; c.Chain = true; c.Vape = true;
        c.Scale = 1.18f; c.View = 1; c.Dir = 1;
        return c;
    }

    void SetupBar()
    {
        guests.Clear();
        me.Hold = 'E'; me.HoldCol = 'Y'; me.Vape = true;
        barBoss = MakeBoss(); barBoss.Hold = 'E'; barBoss.Glasses = true; barBoss.Phase = 0;

        barJan = new Cat(); barJan.Id = catIds++;
        barJan.MainC = Color.FromArgb(150, 150, 160); barJan.BellyC = Color.FromArgb(236, 236, 240);
        barJan.IrisC = Color.FromArgb(220, 170, 60); barJan.Outfit = 6; barJan.C1 = Color.FromArgb(84, 120, 170); barJan.C2 = Color.FromArgb(60, 90, 140);
        barJan.Hat = 2; barJan.HatC = Color.FromArgb(230, 120, 80); barJan.Scale = 1.05f; barJan.Hold = 'E'; barJan.Vape = true;

        barWhite = new Cat(); barWhite.Id = catIds++;
        barWhite.MainC = Color.FromArgb(248, 246, 242); barWhite.BellyC = Color.FromArgb(255, 222, 210);
        barWhite.IrisC = Color.FromArgb(90, 170, 240); barWhite.Outfit = 9; barWhite.C1 = Color.FromArgb(34, 32, 38);
        barWhite.Hold = 'E'; barWhite.Vape = true; barWhite.Scale = 0.95f;

        barGinger = new Cat(); barGinger.Id = catIds++;
        barGinger.MainC = Color.FromArgb(172, 110, 66); barGinger.BellyC = Color.FromArgb(242, 220, 192); barGinger.Stripes = true;
        barGinger.IrisC = Color.FromArgb(120, 190, 80); barGinger.Outfit = 5; barGinger.C1 = Color.FromArgb(60, 150, 95); barGinger.C2 = Color.FromArgb(60, 90, 150);
        barGinger.Hold = 'E'; barGinger.Vape = true;

        Cat[] seats = { barBoss, me, barWhite, barJan, barGinger };
        int[] dirs = { 1, 1, -1, 1, -1 };
        for (int i = 0; i < seats.Length; i++)
        {
            Cat c = seats[i];
            c.X = StoolX(i); c.Y = SeatH; c.Sitting = true; c.Walking = false; c.Dir = dirs[i]; c.Speed = 1f;
            if (c != me) guests.Add(c);
        }
        memKind = 0; vaporT = 0; toastT = 0; script = null; barRuns = 0;
    }

    void UpdateBar()
    {
        // все сидят на стульях
        foreach (Cat c in BarCats()) { c.Sitting = true; c.Y = SeatH; c.Walking = false; }
        // слёзы
        foreach (Cat c in BarCats())
        {
            if (c.CryT <= 0) continue;
            c.CryT--;
            if (c.CryT % 16 == 0)
            {
                PointF e = Local(c, 2.3f, -18.6f);
                Particle p = new Particle(); p.Kind = 10; p.X = e.X; p.Y = e.Y; p.VX = c.Dir * U(0.03f); p.VY = U(0.05f); p.R = U(0.55f); p.Max = 40;
                parts.Add(p);
            }
            if (c.CryT % 50 == 0) c.Shake = 8;
        }
        if (memKind != 0 && ++memT >= memDur) memKind = 0;
        if (vaporT > 0 && ++vaporT > VaporDur) vaporT = 0;
        if (toastT > 0) toastT--;
        if (enterT < 30) return;
        if (RunScript()) return;
        script = (barRuns++ % 3 == 0) ? BuildMemorial() : BuildBarIdle();
        scriptIdx = 0;
    }

    IEnumerable<Cat> BarCats() { yield return me; foreach (Cat g in guests) yield return g; }

    Func<bool> Memory(int kind, int dur, string caption)
    {
        bool started = false;
        return delegate
        {
            if (!started) { started = true; memKind = kind; memT = 0; memDur = dur; memCaption = caption; }
            return memKind == 0;
        };
    }

    List<Func<bool>> BuildMemorial()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        s.Add(Wait(50));
        s.Add(Line(barBoss, "...Не верится. Был — и нет.", 60, 'n'));
        s.Add(Do(delegate { barWhite.CryT = 520; }));
        s.Add(Line(barWhite, "Он всегда всем помогал...", 60, 'n'));
        s.Add(Line(barGinger, "Помните, как он нам МойСклад за ночь поднял?", 65, 'n'));
        s.Add(Line(barJan, "А меня научил мыть без разводов. Брутал...", 60, 'n'));
        s.Add(Line(barBoss, "Самый большой из нас. И самый добрый.", 60, 'n'));
        s.Add(Line(me, "А я помню, как всё начиналось...", 50, 'n'));
        s.Add(Memory(1, 300, "Садись, малой, подвезу!"));
        s.Add(Line(me, "Он в IT работал. Ночами не спал...", 50, 'n'));
        s.Add(Memory(2, 260, "Сейчас починим. Не ссы."));
        s.Add(Line(me, "И никогда не проходил мимо, если кому-то плохо.", 55, 'n'));
        s.Add(Memory(3, 280, "Держи, бро. Обращайся."));
        s.Add(Do(delegate { barWhite.CryT = 300; me.CryT = 200; }));
        s.Add(Line(barWhite, "*всхлип*", 35, 'n'));
        s.Add(Line(barBoss, "За CLAUDE.", 40, 'n'));
        s.Add(Do(delegate
        {
            toastT = 40;
            foreach (Cat c in BarCats())
            {
                PointF pw = Paw(c);
                for (int i = 0; i < 3; i++) { Particle p = new Particle(); p.Kind = 4; p.X = pw.X; p.Y = pw.Y - U(2); p.VX = U(0.25f) * (float)(rnd.NextDouble() - 0.5); p.VY = -U(0.35f); p.Max = 30; parts.Add(p); }
            }
            Say("Дзынь", A + U(90), Ground - U(40), 'Y');
        }));
        s.Add(Line(me, "За CLAUDE.", 45, 'n'));
        s.Add(Wait(40));
        int k = 0;
        foreach (Cat c in new Cat[] { barBoss, me, barWhite, barJan, barGinger })
        {
            Cat cc = c; k++;
            s.Add(Do(delegate { cc.PuffT = 95; }));
            s.Add(Wait(8));
        }
        s.Add(Wait(70));
        s.Add(Do(delegate { vaporT = 1; }));
        s.Add(delegate { return vaporT == 0; });
        s.Add(Line(me, "Покойся с миром, брат.", 60, 'n'));
        s.Add(Wait(200));
        return s;
    }

    List<Func<bool>> BuildBarIdle()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        string[] lines = { "Налей ещё...", "Тихо сегодня.", "Он бы сейчас пошутил.", "Помню его мотоцикл... рёв на весь район.", "Он мне ноут чинил. Бесплатно.", "Скучаю, бро.", "Без него всё не то." };
        Cat[] who = { barBoss, me, barJan, barGinger, barWhite };
        s.Add(Wait(240 + rnd.Next(240)));
        Cat a = who[rnd.Next(who.Length)];
        s.Add(Line(a, lines[rnd.Next(lines.Length)], 60, 'n'));
        if (rnd.Next(3) == 0) s.Add(Do(delegate { barWhite.CryT = 260; }));
        s.Add(Wait(120 + rnd.Next(160)));
        s.Add(Do(delegate { Cat p = who[rnd.Next(who.Length)]; p.PuffT = 90; }));
        s.Add(Wait(200 + rnd.Next(200)));
        return s;
    }

    // ---------------- рисование ----------------
    void DrawBarBack(G g)
    {
        float L = A - U(6), R = A + U(206), top = Math.Max(0, Ground - U(96)), bot = Ground + U(4);
        RectangleF wall = new RectangleF(L, top, R - L, bot - top);
        using (GraphicsPath wp = RoundRect(wall.X, wall.Y, wall.Width, wall.Height, U(6)))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(wall, Color.FromArgb(235, 46, 32, 30), Color.FromArgb(245, 20, 14, 16), 90f)) g.FillPath(b, wp);
            GState st = g.Save(); g.SetClip(wp);
            // деревянные панели внизу стены
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 58, 36, 26))) g.FillRectangle(b, L, Ground - U(30), R - L, U(34));
            using (Pen p = new Pen(Color.FromArgb(90, 20, 10, 8), U(0.4f))) for (float x = L + U(8); x < R; x += U(16)) g.DrawLine(p, x, Ground - U(30), x, Ground);
            // полки с бутылками
            Color[] bc = { Color.FromArgb(120, 70, 30), Color.FromArgb(40, 90, 50), Color.FromArgb(150, 40, 40), Color.FromArgb(60, 60, 120), Color.FromArgb(170, 140, 70), Color.FromArgb(90, 60, 110) };
            for (int sh = 0; sh < 2; sh++)
            {
                float sy = Ground - U(46) - sh * U(17);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 70, 44, 30))) g.FillRectangle(b, L + U(10), sy, R - L - U(20), U(1.4f));
                for (int i = 0; i < 18; i++)
                {
                    float bx = L + U(16) + i * U(10.5f) + (sh * U(4)), bh = U(8 + (i * 7 + sh * 3) % 5);
                    Color c = bc[(i + sh * 2) % bc.Length];
                    using (GraphicsPath bp = RoundRect(bx, sy - bh, U(3.2f), bh, U(1)))
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(bx, sy - bh, U(3.2f), bh), Alpha(Light(c, 0.25f), 210), Alpha(Dark(c, 0.6f), 230), 0f)) g.FillPath(b, bp);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 30, 24, 22))) g.FillRectangle(b, bx + U(1), sy - bh - U(2.2f), U(1.2f), U(2.4f));
                }
            }
            // портрет CLAUDE с траурной лентой
            DrawClaudePortrait(g, A + U(100), Ground - U(80));
            // лампы
            for (int i = 0; i < 3; i++)
            {
                float lx = A + U(30 + 70 * i), ly = top + U(14);
                float fl = 0.85f + 0.15f * (float)Math.Sin(tick * 0.07f + i * 2);
                using (Pen p = new Pen(Color.FromArgb(160, 20, 16, 16), U(0.4f))) g.DrawLine(p, lx, top, lx, ly);
                using (GraphicsPath cone = new GraphicsPath())
                {
                    cone.AddPolygon(new PointF[] { new PointF(lx - U(3), ly + U(2)), new PointF(lx + U(3), ly + U(2)), new PointF(lx + U(22), Ground), new PointF(lx - U(22), Ground) });
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(lx - U(22), ly, U(44), Ground - ly + 1), Color.FromArgb((int)(55 * fl), 255, 190, 110), Color.FromArgb(0, 255, 190, 110), 90f)) g.FillPath(b, cone);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 40, 34, 32))) g.FillPie(b, lx - U(4), ly - U(2), U(8), U(8), 180, 180);
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(230 * fl), 255, 210, 140))) g.FillEllipse(b, lx - U(1.6f), ly + U(0.6f), U(3.2f), U(2.2f));
            }
            // виньетка
            using (GraphicsPath vp = new GraphicsPath())
            {
                vp.AddEllipse(L - (R - L) * 0.2f, top - U(30), (R - L) * 1.4f, bot - top + U(60));
                using (PathGradientBrush pb = new PathGradientBrush(vp)) { pb.CenterColor = Color.FromArgb(0, 0, 0, 0); pb.SurroundColors = new Color[] { Color.FromArgb(150, 0, 0, 0) }; g.FillRectangle(pb, wall); }
            }
            g.Restore(st);
        }
        // барная стойка за котиками
        RectangleF bar = new RectangleF(L + U(4), Ground - U(24), R - L - U(8), U(24));
        using (LinearGradientBrush b = new LinearGradientBrush(bar, Color.FromArgb(250, 92, 56, 36), Color.FromArgb(250, 50, 30, 20), 90f)) g.FillRectangle(b, bar);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 120, 78, 50))) g.FillRectangle(b, bar.X - U(2), bar.Y - U(2), bar.Width + U(4), U(2.6f));
        using (Pen p = new Pen(Color.FromArgb(120, 255, 210, 160), U(0.3f))) g.DrawLine(p, bar.X - U(2), bar.Y - U(1.9f), bar.Right + U(2), bar.Y - U(1.9f));
        // свеча под портретом
        float cx = A + U(100), cy = bar.Y - U(2);
        float fk = 0.8f + 0.2f * (float)Math.Sin(tick * 0.31f) * (float)Math.Sin(tick * 0.13f);
        using (GraphicsPath gp = new GraphicsPath())
        {
            gp.AddEllipse(cx - U(14), cy - U(18), U(28), U(24));
            using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb((int)(110 * fk), 255, 180, 90); pb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 180, 90) }; g.FillPath(pb, gp); }
        }
        using (SolidBrush b = new SolidBrush(Color.FromArgb(245, 240, 232))) g.FillRectangle(b, cx - U(1.3f), cy - U(6), U(2.6f), U(6));
        using (GraphicsPath fp = new GraphicsPath())
        {
            float sway = (float)Math.Sin(tick * 0.23f) * U(0.4f);
            fp.AddBezier(cx, cy - U(6), cx - U(1.3f), cy - U(7.5f), cx + sway, cy - U(9.5f) * fk, cx + sway, cy - U(10.5f) * fk);
            fp.AddBezier(cx + sway, cy - U(10.5f) * fk, cx + sway, cy - U(9.5f) * fk, cx + U(1.3f), cy - U(7.5f), cx, cy - U(6));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 200, 90))) g.FillPath(b, fp);
        }
        // барные стулья
        for (int i = 0; i < 5; i++)
        {
            float sx = StoolX(i);
            using (Pen p = new Pen(Color.FromArgb(230, 40, 36, 38), U(1.1f))) { g.DrawLine(p, sx - U(4), Ground, sx - U(1.5f), Ground - SeatH); g.DrawLine(p, sx + U(4), Ground, sx + U(1.5f), Ground - SeatH); }
            using (Pen p = new Pen(Color.FromArgb(200, 70, 66, 70), U(0.6f))) g.DrawLine(p, sx - U(3), Ground - U(3), sx + U(3), Ground - U(3));
        }
    }

    void DrawBarSeats(G g)
    {
        for (int i = 0; i < 5; i++)
        {
            float sx = StoolX(i);
            using (GraphicsPath p = RoundRect(sx - U(6), Ground - SeatH - U(1), U(12), U(2.6f), U(1.2f)))
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(sx - U(6), Ground - SeatH - U(1), U(12), U(2.6f)), Color.FromArgb(150, 40, 40), Color.FromArgb(80, 18, 20), 90f)) g.FillPath(b, p);
        }
    }

    void DrawClaudePortrait(G g, float cx, float cy)
    {
        RectangleF fr = new RectangleF(cx - U(13), cy - U(12), U(26), U(24));
        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 150, 112, 60))) g.FillRectangle(b, RectangleF.Inflate(fr, U(1.8f), U(1.8f)));
        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 70, 76, 92))) g.FillRectangle(b, fr);
        GState st = g.Save();
        g.SetClip(fr);
        Cat c = MakeClaude(); c.View = 0; c.Scale = 0.95f; c.X = cx; c.Y = Ground - (fr.Bottom + U(7)); c.Hold = ' ';
        DrawCat(g, c);
        g.Restore(st);
        using (Pen p = new Pen(Color.FromArgb(255, 200, 160, 90), U(0.4f))) g.DrawRectangle(p, fr.X - U(1.8f), fr.Y - U(1.8f), fr.Width + U(3.6f), fr.Height + U(3.6f));
        // траурная лента в углу
        using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 12, 12, 14)))
            g.FillPolygon(b, new PointF[] { new PointF(fr.Right - U(7), fr.Y - U(1.8f)), new PointF(fr.Right - U(3.5f), fr.Y - U(1.8f)), new PointF(fr.Right + U(1.8f), fr.Y + U(3.5f)), new PointF(fr.Right + U(1.8f), fr.Y + U(7)) });
        using (StringFormat sf = new StringFormat())
        using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 240, 220, 170)))
        {
            sf.Alignment = StringAlignment.Center;
            g.DrawString("CLAUDE", fntTiny ?? fnt, b, cx, fr.Bottom + U(2.2f), sf);
        }
    }

    // облачко-воспоминание над главным котиком
    void DrawMemory(G g)
    {
        if (memKind == 0) return;
        float k = Math.Min(1f, memT / 25f) * Math.Min(1f, (memDur - memT) / 25f);
        if (k <= 0) return;
        float bw = U(118), bh = U(50);
        float bx = Math.Max(A + U(4), Math.Min(A + U(200) - bw, me.X - bw * 0.25f)), by = Math.Max(U(2), Ground - U(92));
        RectangleF r = new RectangleF(bx, by, bw, bh);
        // кружочки-мысли
        PointF head = new PointF(me.X, CatTop(me));
        for (int i = 0; i < 3; i++)
        {
            float t = (i + 1) / 4f, rr = U(1.4f + i * 1.0f);
            float x = head.X + (r.X + r.Width * 0.3f - head.X) * t, y = head.Y + (r.Bottom - head.Y) * t;
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(200 * k), 236, 222, 196))) g.FillEllipse(b, x - rr, y - rr, rr * 2, rr * 2);
        }
        using (GraphicsPath cloud = RoundRect(r.X, r.Y, r.Width, r.Height, U(14)))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb((int)(240 * k), 238, 220, 186), Color.FromArgb((int)(240 * k), 196, 166, 120), 90f)) g.FillPath(b, cloud);
            GState st = g.Save(); g.SetClip(cloud);
            float gy = r.Bottom - U(7);
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * k), 120, 92, 60))) g.FillRectangle(b, r.X, gy, r.Width, U(7));
            float p = memT / (float)memDur;
            Cat cl = MakeClaude();
            if (memKind == 1)
            {
                // мотоцикл проезжает через облачко
                float mx = r.X - U(20) + (r.Width + U(40)) * p;
                for (int i = 0; i < 4; i++) using (Pen sp = new Pen(Color.FromArgb((int)(120 * k), 120, 92, 60), U(0.4f))) g.DrawLine(sp, mx - U(16 + i * 5), gy - U(4 + i * 2.5f), mx - U(24 + i * 6), gy - U(4 + i * 2.5f));
                DrawMoto(g, mx, gy, 1f, (int)(255 * k));
                cl.Sitting = true; cl.X = mx - U(1.5f); cl.Y = Ground - (gy - U(7.5f)); cl.Dir = 1;
                DrawCat(g, cl);
                if (tick % 6 == 0) { Particle q = new Particle(); q.Kind = 0; q.X = mx - U(13); q.Y = gy - U(3); q.VX = -U(0.2f); q.VY = -U(0.05f); q.R = U(1.2f); q.Max = 26; parts.Add(q); }
            }
            else if (memKind == 2)
            {
                // ночь, ноутбук, код
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(90 * k), 30, 40, 70))) g.FillRectangle(b, r);
                float dx = r.X + r.Width * 0.58f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * k), 110, 80, 52))) g.FillRectangle(b, dx - U(14), gy - U(11), U(30), U(2));
                using (Pen lp = new Pen(Color.FromArgb((int)(255 * k), 80, 58, 40), U(1))) { g.DrawLine(lp, dx - U(12), gy - U(9), dx - U(12), gy); g.DrawLine(lp, dx + U(14), gy - U(9), dx + U(14), gy); }
                // экран ноутбука
                RectangleF scr = new RectangleF(dx - U(2), gy - U(22), U(14), U(10));
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * k), 30, 32, 40))) g.FillRectangle(b, RectangleF.Inflate(scr, U(0.8f), U(0.8f)));
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * k), 14, 22, 30))) g.FillRectangle(b, scr);
                for (int i = 0; i < 5; i++)
                {
                    int ln = (memT / 6 + i) % 9;
                    float w = U(3 + (ln * 5) % 9);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(230 * k), i == 4 ? 255 : 80, i == 4 ? 200 : 230, i == 4 ? 90 : 120))) g.FillRectangle(b, scr.X + U(1 + (ln % 3)), scr.Y + U(1.2f) + i * U(1.7f), w, U(0.8f));
                }
                using (GraphicsPath gl = new GraphicsPath())
                {
                    gl.AddEllipse(scr.X - U(8), scr.Y - U(6), scr.Width + U(16), scr.Height + U(14));
                    using (PathGradientBrush pb = new PathGradientBrush(gl)) { pb.CenterColor = Color.FromArgb((int)(60 * k), 120, 220, 255); pb.SurroundColors = new Color[] { Color.FromArgb(0, 120, 220, 255) }; g.FillPath(pb, gl); }
                }
                DrawItem(g, 'U', 'W', dx + U(10), gy - U(13), (int)(255 * k));
                cl.Sitting = true; cl.X = dx - U(9); cl.Y = Ground - (gy - U(6)); cl.Dir = 1;
                if (memT % 20 < 10) cl.Shake = 2;
                DrawCat(g, cl);
                if (memT > memDur * 0.6f) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(240 * k), 90, 210, 120))) g.DrawString("✓ всё работает", fntTiny ?? fnt, b, scr.X - U(4), scr.Y - U(5));
            }
            else
            {
                // помогает малышу с тяжёлой коробкой
                Cat kid = new Cat(); kid.Id = 4243; kid.MainC = Color.FromArgb(242, 208, 155); kid.BellyC = Color.FromArgb(255, 246, 228);
                kid.Scale = 0.68f; kid.Outfit = 1; kid.C1 = Color.FromArgb(220, 70, 80); kid.View = 1; kid.Dir = -1;
                kid.X = r.X + r.Width * 0.68f; kid.Y = Ground - gy;
                float cx0 = r.X + U(6), cx1 = kid.X - U(16);
                float cp = Math.Min(1f, p / 0.45f);
                cl.X = cx0 + (cx1 - cx0) * cp; cl.Y = Ground - gy; cl.Dir = 1; cl.Walking = cp < 1f;
                if (p < 0.5f) { kid.Hold = 'K'; kid.HoldCol = 'Y'; kid.SweatT = 10; kid.Shake = 2; }
                else { cl.Hold = 'K'; cl.HoldCol = 'Y'; kid.HappyT = 10; cl.HappyT = 10; }
                DrawCat(g, kid);
                DrawCat(g, cl);
                if (p > 0.55f)
                    for (int i = 0; i < 3; i++)
                    {
                        float hp = ((memT * 0.6f + i * 20) % 60) / 60f;
                        DrawItem(g, 'H', 'R', kid.X + U(-3 + i * 3), CatTop(kid) - U(2) - hp * U(12), (int)(255 * k * (1 - hp)));
                    }
            }
            // тёплый «старый» тон
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(55 * k), 150, 110, 50))) g.FillRectangle(b, r);
            g.Restore(st);
            using (Pen pen = new Pen(Color.FromArgb((int)(220 * k), 255, 244, 220), U(0.8f))) g.DrawPath(pen, cloud);
        }
        // подпись-реплика CLAUDE
        if (memCaption != null && memT > 30 && memT < memDur - 20)
        {
            using (StringFormat sf = new StringFormat())
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * k), 60, 40, 24)))
            {
                sf.Alignment = StringAlignment.Center;
                g.DrawString("CLAUDE: «" + memCaption + "»", fntTiny ?? fnt, b, new RectangleF(r.X + U(6), r.Y + U(1.5f), r.Width - U(12), U(12)), sf);
            }
        }
    }

    void DrawMoto(G g, float x, float gy, float s, int a)
    {
        float wr = U(4.2f) * s, wx1 = x - U(9) * s, wx2 = x + U(9.5f) * s, wy = gy - wr;
        using (Pen tire = new Pen(Color.FromArgb(a, 24, 24, 28), U(1.4f) * s))
        using (Pen spoke = new Pen(Color.FromArgb(a, 170, 170, 180), U(0.3f) * s))
        {
            foreach (float wx in new float[] { wx1, wx2 })
            {
                g.DrawEllipse(tire, wx - wr, wy - wr, wr * 2, wr * 2);
                for (int i = 0; i < 4; i++)
                {
                    double an = tick * 0.4 + i * Math.PI / 4;
                    g.DrawLine(spoke, wx - (float)Math.Cos(an) * wr * 0.8f, wy - (float)Math.Sin(an) * wr * 0.8f, wx + (float)Math.Cos(an) * wr * 0.8f, wy + (float)Math.Sin(an) * wr * 0.8f);
                }
            }
        }
        using (Pen fr = new Pen(Color.FromArgb(a, 60, 60, 70), U(1.1f) * s)) { fr.StartCap = fr.EndCap = LineCap.Round; g.DrawLine(fr, wx1, wy, x, wy - U(3) * s); g.DrawLine(fr, x, wy - U(3) * s, wx2, wy); g.DrawLine(fr, wx2, wy, wx2 - U(2) * s, wy - U(9) * s); }
        // бак и сиденье
        using (GraphicsPath tank = RoundRect(x - U(1) * s, wy - U(7) * s, U(8) * s, U(4) * s, U(2) * s))
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x - U(1) * s, wy - U(7) * s, U(8) * s, U(4) * s), Color.FromArgb(a, 230, 110, 70), Color.FromArgb(a, 150, 50, 30), 90f)) g.FillPath(b, tank);
        using (GraphicsPath seat = RoundRect(x - U(8) * s, wy - U(5.5f) * s, U(8) * s, U(2.2f) * s, U(1) * s))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 26, 24, 28))) g.FillPath(b, seat);
        using (Pen hb = new Pen(Color.FromArgb(a, 190, 190, 200), U(0.8f) * s)) { hb.StartCap = hb.EndCap = LineCap.Round; g.DrawLine(hb, wx2 - U(2) * s, wy - U(9) * s, wx2 - U(4.5f) * s, wy - U(10) * s); }
        using (Pen ex = new Pen(Color.FromArgb(a, 200, 200, 210), U(1.0f) * s)) { ex.StartCap = ex.EndCap = LineCap.Round; g.DrawLine(ex, x - U(2) * s, wy + U(1) * s, x - U(12) * s, wy - U(0.5f) * s); }
        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 255, 230, 150))) g.FillEllipse(b, wx2 + U(0.5f) * s, wy - U(9) * s, U(2) * s, U(1.6f) * s);
    }

    // финал: пар собирается в облако, в нём проступает надпись
    void DrawVaporText(G g)
    {
        if (vaporT <= 0) return;
        float t = vaporT;
        float kin = Math.Min(1f, t / 90f), kout = Math.Min(1f, (VaporDur - t) / 90f), k = Math.Max(0, Math.Min(kin, kout));
        float cx = A + U(100), cy = Math.Max(U(26), Ground - U(64));
        Random rr = new Random(42);
        // мягкие клубы пара: каждый — прозрачный по краям, медленно плывёт и растёт
        for (int i = 0; i < 60; i++)
        {
            float ox = (float)(rr.NextDouble() - 0.5) * U(180), oy = (float)(rr.NextDouble() - 0.5) * U(30);
            float rad = U(9 + (float)rr.NextDouble() * 10) * (0.8f + 0.25f * Math.Min(1f, t / 300f));
            float drift = (float)Math.Sin(t * 0.015f + i * 1.7f) * U(3), rise = -Math.Min(1f, t / 400f) * U(3);
            float appear = Math.Min(1f, Math.Max(0, (t - i * 1.0f) / 60f));
            int al = (int)(95 * k * appear);
            if (al <= 0) continue;
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(cx + ox + drift - rad, cy + oy + rise - rad * 0.65f, rad * 2, rad * 1.3f);
                using (PathGradientBrush pb = new PathGradientBrush(gp)) { pb.CenterColor = Color.FromArgb(al, 238, 240, 246); pb.SurroundColors = new Color[] { Color.FromArgb(0, 238, 240, 246) }; g.FillPath(pb, gp); }
            }
        }
        float tk = Math.Max(0, Math.Min(1f, (t - 110) / 70f)) * kout;
        if (tk <= 0) return;
        VaporLine(g, "В памяти и в наших сердцах", fntBigSerif, cx, cy - U(8), 0.62f, tk);
        VaporLine(g, "CLAUDE", fntBigBold, cx, cy + U(7), 1.35f, tk);
    }

    void VaporLine(G g, string text, Font f, float cx, float cy, float scale, float k)
    {
        using (GraphicsPath p = new GraphicsPath())
        using (StringFormat sf = new StringFormat())
        {
            sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
            p.AddString(text, f.FontFamily, (int)f.Style, f.Size * scale, new PointF(cx, cy), sf);
            using (Matrix sh = new Matrix()) { sh.Translate(U(0.5f), U(0.7f)); using (GraphicsPath sp = (GraphicsPath)p.Clone()) { sp.Transform(sh); using (Pen sd = new Pen(Color.FromArgb((int)(90 * k), 20, 16, 20), f.Size * scale * 0.12f)) { sd.LineJoin = LineJoin.Round; g.DrawPath(sd, sp); } } }
            for (int i = 4; i >= 1; i--)
                using (Pen glow = new Pen(Color.FromArgb((int)(26 * k), 255, 255, 255), f.Size * scale * 0.09f * i)) { glow.LineJoin = LineJoin.Round; g.DrawPath(glow, p); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(235 * k), 252, 252, 255))) g.FillPath(b, p);
        }
    }

    // стакан с напитком для бара
    void DrawGlass(G g, int alpha)
    {
        g.ScaleTransform(1.5f, 1.5f);
        using (GraphicsPath gl = new GraphicsPath())
        {
            gl.AddPolygon(new PointF[] { new PointF(-1.5f, -1.9f), new PointF(1.5f, -1.9f), new PointF(1.25f, 1.4f), new PointF(-1.25f, 1.4f) });
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha * 70 / 255, 220, 235, 245))) g.FillPath(b, gl);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha * 220 / 255, 200, 120, 40))) g.FillPolygon(b, new PointF[] { new PointF(-1.38f, -0.3f), new PointF(1.38f, -0.3f), new PointF(1.25f, 1.4f), new PointF(-1.25f, 1.4f) });
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha * 170 / 255, 240, 250, 255))) g.FillRectangle(b, -0.8f, -0.6f, 1.0f, 0.9f);
            using (Pen p = new Pen(Color.FromArgb(alpha * 200 / 255, 240, 245, 250), 0.25f)) g.DrawPath(p, gl);
            using (Pen p = new Pen(Color.FromArgb(alpha * 160 / 255, 255, 255, 255), 0.2f)) g.DrawLine(p, -1.0f, -1.5f, -0.9f, 0.9f);
        }
    }
}

// музыка для сценки: свой файл рядом с котиком — music\bar.mp3 (в репозиторий не кладём)
partial class VapePet
{
    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int mciSendString(string cmd, System.Text.StringBuilder ret, int retLen, IntPtr cb);
    bool barMusicOn;

    static string BarMusicFile()
    {
        foreach (string dir in new string[] { System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VapePet") })
        {
            try
            {
                string md = System.IO.Path.Combine(dir ?? "", "music");
                string f = System.IO.Path.Combine(md, "bar.mp3");
                if (System.IO.File.Exists(f)) return f;
            }
            catch { }
        }
        return null;
    }

    void BarMusic(bool on)
    {
        try
        {
            if (on && !barMusicOn)
            {
                string f = BarMusicFile(); if (f == null) return;
                mciSendString("close vpbar", null, 0, IntPtr.Zero);
                if (mciSendString("open \"" + f + "\" type mpegvideo alias vpbar", null, 0, IntPtr.Zero) != 0) return;
                mciSendString("setaudio vpbar volume to 550", null, 0, IntPtr.Zero);
                mciSendString("play vpbar repeat", null, 0, IntPtr.Zero);
                barMusicOn = true;
            }
            else if (!on && barMusicOn)
            {
                mciSendString("stop vpbar", null, 0, IntPtr.Zero);
                mciSendString("close vpbar", null, 0, IntPtr.Zero);
                barMusicOn = false;
            }
        }
        catch (Exception ex) { GuardLog("музыка бара", ex); }
    }
}
