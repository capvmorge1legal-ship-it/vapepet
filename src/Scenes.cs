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
    // ================= сценарий: облава ОМОН =================
    List<Cat> omons = new List<Cat>(), inmates = new List<Cat>();
    Cat raidSeller, raidCustomer, guard;
    int omonPhase, jailEnterT, sirenT, vanState, tally, barsShake;
    float vanX, vanSpeed;
    float VanStop { get { return A + U(128); } }
    float VanDoor { get { return vanX + U(20); } }
    float JailX { get { return A + U(30); } }

    Cat NewOmon()
    {
        Cat o = NewGuest();
        o.Outfit = 11; o.C1 = Color.FromArgb(50, 56, 68); o.C2 = Color.FromArgb(34, 37, 46);
        o.Hat = 6; o.HatC = Color.FromArgb(42, 48, 62); o.Glasses = false; o.Vape = false; o.Speed = 3.6f;
        return o;
    }

    void SetupRaid()
    {
        omonPhase = 0; sirenT = 0; vanState = 0; barsShake = 0; tally = 1;
        omons.Clear(); inmates.Clear(); guard = null;
        raidSeller = NewGuest();
        raidSeller.Outfit = 5; raidSeller.C1 = Color.FromArgb(70, 120, 210); raidSeller.Hat = 0; raidSeller.Glasses = false; raidSeller.Vape = true;
        raidSeller.X = ShelfX(0) + U(8); raidSeller.Dir = 1;
        raidCustomer = NewGuest();
        raidCustomer.X = CustFront; raidCustomer.Dir = -1;
        guests.Add(raidSeller); guests.Add(raidCustomer);
        Poof(me.X, CatTop(me) + U(8));
        me.X = CashX; me.Dir = 1;
        script = BuildRaid(); scriptIdx = 0;
    }

    void StartRaid()
    {
        sirenT = 700; shakeT = 10;
        Cat[] targets = { raidCustomer, me, raidSeller };
        for (int i = 0; i < 3; i++)
        {
            Cat o = NewOmon();
            o.X = Width + U(8 + i * 12); o.Dir = -1;
            guests.Add(o); omons.Add(o);
            Go(o, targets[i].X + U(10));
        }
        foreach (Cat t in targets) { t.Shake = 25; t.SweatT = 200; t.Walking = false; t.Hold = ' '; t.PuffT = 0; }
    }

    bool AllStopped(List<Cat> list) { foreach (Cat c in list) if (c.Walking || c.Jumping) return false; return true; }

    List<Func<bool>> BuildRaid()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        Cat col = raidSeller, cust = raidCustomer;
        List<Cat> targets = new List<Cat> { cust, me, col };
        s.Add(Wait(45));
        s.Add(SayStep(cust, "Мне что-нибудь на арбузе", 40));
        s.Add(SayF(delegate { return col; }, "Ща подберём!", 25, 'U'));
        s.Add(Do(delegate { Go(col, ShelfX(1)); }));
        s.Add(Wait(20));
        s.Add(Do(delegate { StartRaid(); }));
        s.Add(SayF(delegate { return omons[0]; }, "ОМОН! ВСЕМ ЛЕЖАТЬ!!!", 40, 'R'));
        s.Add(delegate { return AllStopped(omons); });
        s.Add(Do(delegate
        {
            foreach (Cat t in targets) { t.Lying = true; t.Cuffed = true; t.Walking = false; t.Hold = ' '; Poof(t.X + t.Dir * U(12), Ground - U(2)); }
            Hurt(25);
            foreach (Cat o in omons) o.Dir = -1;
            shakeT = 10;
        }));
        if (rnd.Next(100) < 45) s.Add(Do(delegate { BigText("ВЫ ЗАДЕРЖАНЫ", 0, 110); }));
        s.Add(SayF(delegate { return omons[1]; }, "Лицом в пол! Руки за спину!", 45, 'R'));
        s.Add(Line(me, "Это не моё! Я просто на кассе стою!", 55, 'w'));
        s.Add(SayF(delegate { return omons[1]; }, "Разберёмся.", 30, 'K'));
        s.Add(Line(col, "У нас все документы есть! Честно-честно!", 55, 'w'));
        s.Add(Line(cust, "Я вообще за кефиром зашёл!!!", 50, 'w'));
        s.Add(SayF(delegate { return omons[0]; }, "Ага. В вейп-шоп. За кефиром.", 50, 'K'));
        s.Add(Line(me, "Начальник, это жижа со вкусом печенья...", 55, 'w'));
        s.Add(Line(col, "Можно я маме позвоню?", 45, 'w'));
        s.Add(Line(cust, "Я вообще не с ними!", 40, 'w'));
        s.Add(SayF(delegate { return omons[2]; }, "Все вопросы — в отделении. В автозак!", 50, 'R'));
        s.Add(Do(delegate { vanState = 1; vanX = Width + U(4); sirenT = 600; }));
        s.Add(delegate { return vanState == 2; });
        s.Add(Do(delegate
        {
            for (int i = 0; i < targets.Count; i++) { Cat t = targets[i]; t.Lying = false; t.Speed = 1.0f; Go(t, VanDoor + U(i * 2)); }
            for (int i = 0; i < omons.Count; i++) { omons[i].Speed = 1.0f; Go(omons[i], VanDoor + U(10 + i * 4)); }
        }));
        s.Add(Line(cust, "Я буду жаловаться!", 35, 'a'));
        s.Add(delegate { return AllStopped(targets); });
        s.Add(Do(delegate { foreach (Cat t in targets) { t.Hidden = true; } Poof(VanDoor, Ground - U(8)); }));
        s.Add(delegate { return AllStopped(omons); });
        s.Add(Do(delegate { foreach (Cat o in omons) o.Hidden = true; }));
        s.Add(Wait(20));
        s.Add(Do(delegate { vanState = 3; vanSpeed = 0; }));
        s.Add(delegate { return vanState == 0; });
        s.Add(Do(delegate { GoToJail(targets); }));
        s.Add(Wait(35));
        s.Add(Line(me, "Ну... приехали.", 45, 'w'));
        return s;
    }

    void GoToJail(List<Cat> targets)
    {
        omonPhase = 1; jailEnterT = 0; sirenT = 0;
        for (int k = 0; k < 6; k++) Poof(A + U(10 + k * 18), Ground - U(10));
        guests.Clear(); omons.Clear(); inmates.Clear();
        int i = 0;
        foreach (Cat c in new Cat[] { me, raidSeller, raidCustomer })
        {
            c.Hidden = false; c.Lying = false; c.Cuffed = false; c.Walking = false; c.Hold = ' ';
            c.Outfit = 10; c.C1 = Color.FromArgb(245, 245, 245); c.C2 = Color.FromArgb(70, 72, 80);
            c.Hat = 0; c.Glasses = false; c.Vape = false; c.Speed = 1.0f;
            c.X = JailX + U(20 + i * 24); c.Y = U(7); c.Sitting = true; c.Dir = (i % 2 == 0) ? 1 : -1;
            inmates.Add(c);
            if (c != me) guests.Add(c);
            i++;
        }
        guard = NewOmon(); guard.X = JailX + U(134); guard.Dir = -1; guard.Speed = 0.8f;
        guests.Add(guard);
        script = null;
    }

    static readonly string[] JailQuips = {
        "Ну и что теперь?", "Зато обед бесплатный", "Я же говорил: открываем шаурмичную!", "Мам, забери меня...",
        "Интересно, касса закрылась?", "Я вообще за кефиром заходил!!!", "Кто-нибудь умеет копать ложкой?",
        "Давайте в города? Москва.", "Анапа.", "...Апатиты", "Даже вейп отобрали...", "Скучаю по жиже с печеньем",
        "А тут вай-фай есть?", "Адвоката мне! Кошачьего!", "Сидим ровно, пацаны", "Это всё из-за арбузной..." };

    const string JailActs = "wwawnswnhnnwnaha";

    List<Func<bool>> NextJail()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        if (inmates.Count == 0) { s.Add(Wait(50)); return s; }
        Cat c = inmates[rnd.Next(inmates.Count)];
        int r = rnd.Next(100);
        if (r < 22)
        {
            s.Add(Do(delegate { Jump(c, c.X, 0, U(3), 10, delegate { c.Sitting = false; }); c.Sitting = false; }));
            s.Add(delegate { return !c.Jumping; });
            s.Add(Do(delegate { c.Dir = 1; barsShake = 35; c.Shake = 35; c.AngryT = 70; }));
            s.Add(SayStep(c, rnd.Next(2) == 0 ? "Выпустите! Я больше не буду!" : "Я требую звонок!", 50));
            if (guard != null)
            {
                Cat gd = guard;
                s.Add(Do(delegate { gd.AngryT = 50; gd.Dir = -1; }));
                s.Add(SayF(delegate { return gd; }, rnd.Next(2) == 0 ? "Тихо там!" : "Отбой был!", 40, 'R'));
            }
            s.Add(Do(delegate { Jump(c, c.X, U(7), U(4), 12, delegate { c.Sitting = true; }); }));
            s.Add(delegate { return !c.Jumping; });
        }
        else if (r < 32)
        {
            s.Add(Do(delegate { tally++; }));
            s.Add(Line(c, "День " + (tally + 1) + "...", 45, 'w'));
        }
        else if (r < 42 && guard != null)
        {
            Cat gd = guard;
            float gx = JailX + U(126 + (float)rnd.NextDouble() * 12);
            s.Add(GoStep(gd, delegate { return gx; }));
            s.Add(Do(delegate { gd.Dir = -1; }));
        }
        else
        {
            int qi = rnd.Next(JailQuips.Length);
            string q = JailQuips[qi];
            char act = JailActs[qi];
            s.Add(Line(c, q, 50, act));
            if (rnd.Next(100) < 40)
            {
                Cat c2 = inmates[rnd.Next(inmates.Count)];
                if (c2 != c)
                {
                    string[] answers = { "Ага...", "Не начинай", "Ха-ха", "Сам такой", "Согласен", "Тише ты" };
                    s.Add(Line(c2, answers[rnd.Next(answers.Length)], 35, 'n'));
                }
            }
        }
        s.Add(Wait(50 + rnd.Next(100)));
        return s;
    }

    void UpdateOmon()
    {
        if (sirenT > 0) sirenT--;
        if (barsShake > 0) barsShake--;
        if (vanState == 1)
        {
            float d = vanX - VanStop;
            vanX -= Math.Max(U(0.3f), d * 0.05f);
            if (tick % 3 == 0) Exhaust(vanX + U(54), Ground - U(3), 1);
            if (vanX - VanStop < U(0.3f)) { vanX = VanStop; vanState = 2; }
        }
        else if (vanState == 3)
        {
            vanSpeed += 0.04f * u; vanX += vanSpeed;
            if (tick % 2 == 0) Exhaust(vanX + U(54), Ground - U(3), 1);
            if (vanX > Width + U(4)) vanState = 0;
        }
        if (omonPhase == 1) jailEnterT++;
        if (enterT < 5) return;
        if (RunScript()) return;
        if (omonPhase == 1) { script = NextJail(); scriptIdx = 0; }
    }

    void Exhaust(float x, float y, int dir)
    {
        Particle p = new Particle();
        p.Kind = 5; p.X = x; p.Y = y;
        p.VX = dir * (0.2f + (float)rnd.NextDouble() * 0.3f) * u; p.VY = -(0.05f + (float)rnd.NextDouble() * 0.1f) * u;
        p.R = U(0.8f + (float)rnd.NextDouble()); p.Max = 30;
        parts.Add(p);
    }

    // ---- рисование: автозак, камера, мигалки ----
    PointF V(float x0, float x, float y) { return new PointF(x0 + U(x), Ground - U(y)); }

    void DrawWheel(Graphics g, float cx, float cy, float r, float rot)
    {
        using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 60, 62))) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
        using (Pen p = new Pen(InkC, Math.Max(1.2f, r * 0.16f))) g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2);
        using (Pen p = new Pen(Color.FromArgb(80, 255, 250, 240), Math.Max(1f, r * 0.1f))) g.DrawArc(p, cx - r * 0.8f, cy - r * 0.8f, r * 1.6f, r * 1.6f, 205, 70);
        float rr = r * 0.58f;
        using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 224, 210))) g.FillEllipse(b, cx - rr, cy - rr, rr * 2, rr * 2);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 172, 162))) g.FillPie(b, cx - rr, cy - rr, rr * 2, rr * 2, 10, 160);
        using (Pen p = new Pen(InkC, Math.Max(1f, r * 0.1f)))
        {
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            g.DrawEllipse(p, cx - rr, cy - rr, rr * 2, rr * 2);
            for (int i = 0; i < 5; i++)
            {
                double a = rot + i * Math.PI * 2 / 5;
                g.DrawLine(p, cx + (float)Math.Cos(a) * rr * 0.3f, cy + (float)Math.Sin(a) * rr * 0.3f, cx + (float)Math.Cos(a) * rr * 0.92f, cy + (float)Math.Sin(a) * rr * 0.92f);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(128, 116, 112))) g.FillEllipse(b, cx - rr * 0.32f, cy - rr * 0.32f, rr * 0.64f, rr * 0.64f);
            g.DrawEllipse(p, cx - rr * 0.32f, cy - rr * 0.32f, rr * 0.64f, rr * 0.64f);
        }
    }

    // стекло с бликами и контуром
    void CarGlass(Graphics g, PointF[] pts, Pen ink)
    {
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddPolygon(pts);
            RectangleF r = p.GetBounds();
            using (SolidBrush b = new SolidBrush(Color.FromArgb(176, 212, 224))) g.FillPath(b, p);
            GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 168, 190))) g.FillRectangle(b, r.X, r.Y + r.Height * 0.62f, r.Width, r.Height * 0.4f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
            {
                g.FillPolygon(b, new PointF[] { new PointF(r.X + r.Width * 0.14f, r.Bottom), new PointF(r.X + r.Width * 0.36f, r.Y), new PointF(r.X + r.Width * 0.5f, r.Y), new PointF(r.X + r.Width * 0.28f, r.Bottom) });
                g.FillPolygon(b, new PointF[] { new PointF(r.X + r.Width * 0.4f, r.Bottom), new PointF(r.X + r.Width * 0.62f, r.Y), new PointF(r.X + r.Width * 0.67f, r.Y), new PointF(r.X + r.Width * 0.45f, r.Bottom) });
            }
            g.Restore(cs);
            g.DrawPath(ink, p);
        }
    }

    void CarArch(Graphics g, float cx, float r)
    {
        using (SolidBrush b = new SolidBrush(Color.FromArgb(74, 56, 54))) g.FillPie(b, cx - r, Ground - U(3.4f) - r, r * 2, r * 2, 180, 180);
    }

    void DrawPoliceVan(Graphics g)
    {
        float x0 = vanX, lw = Math.Max(1.4f, U(0.55f));
        Shadow(g, x0 + U(27), U(56));
        Color wc = Color.FromArgb(248, 244, 234), wd = Color.FromArgb(214, 210, 204);
        using (Pen ink = new Pen(InkC, lw))
        using (Pen thin = new Pen(Color.FromArgb(210, InkC), Math.Max(1f, U(0.32f))))
        {
            ink.LineJoin = LineJoin.Round; ink.StartCap = LineCap.Round; ink.EndCap = LineCap.Round;
            PointF[] body = { V(x0, 0, 3.4f), V(x0, 0, 9.5f), V(x0, 2, 10.8f), V(x0, 7.6f, 20.8f), V(x0, 9.4f, 22.4f), V(x0, 52.8f, 22.4f), V(x0, 54.4f, 20.8f), V(x0, 54.4f, 3.4f) };
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(body);
                using (SolidBrush b = new SolidBrush(wc)) g.FillPath(b, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(wd)) g.FillRectangle(b, x0 - U(1), Ground - U(7.2f), U(57), U(5));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(78, 130, 204))) g.FillRectangle(b, x0, Ground - U(11.6f), U(56), U(3.4f));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(56, 104, 176))) g.FillRectangle(b, x0, Ground - U(9.2f), U(56), U(1f));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 84, 84))) g.FillRectangle(b, x0, Ground - U(8.1f), U(56), U(0.9f));
                CarArch(g, x0 + U(10), U(4.7f)); CarArch(g, x0 + U(44), U(4.7f));
                g.Restore(cs);
                g.DrawPath(ink, p);
            }
        using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString("ПОЛИЦИЯ", fntSign, b, new RectangleF(x0 + U(26), Ground - U(11.6f), U(26), U(3.6f)), sf); }
            CarGlass(g, new PointF[] { V(x0, 3.0f, 11.6f), V(x0, 8.0f, 19.8f), V(x0, 13.4f, 19.8f), V(x0, 13.4f, 11.6f) }, thin);
            // дверь и окошко с решёткой
            using (GraphicsPath p = RoundRect(x0 + U(15), Ground - U(20.6f), U(10), U(16.6f), U(0.8f))) g.DrawPath(thin, p);
            RectangleF w2 = new RectangleF(x0 + U(16.2f), Ground - U(19.6f), U(7.6f), U(6.4f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(84, 100, 120))) g.FillRectangle(b, w2);
            using (Pen pp = new Pen(Color.FromArgb(206, 200, 190), Math.Max(1, U(0.4f)))) for (int i = 1; i < 4; i++) g.DrawLine(pp, w2.X + w2.Width * i / 4, w2.Y, w2.X + w2.Width * i / 4, w2.Bottom);
            g.DrawRectangle(thin, w2.X, w2.Y, w2.Width, w2.Height);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 110, 108))) g.FillRectangle(b, x0 + U(22.6f), Ground - U(11.0f) - U(2.6f), U(1.6f), U(0.6f));
            // боковая дверь кузова, ручки, зеркало
            g.DrawLine(thin, x0 + U(34), Ground - U(21.4f), x0 + U(34), Ground - U(12.2f)); g.DrawLine(thin, x0 + U(34), Ground - U(7.6f), x0 + U(34), Ground - U(4.4f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 110, 108))) g.FillRectangle(b, x0 + U(35), Ground - U(14.4f), U(1.8f), U(0.6f));
            using (GraphicsPath p = RoundRect(x0 + U(5.0f), Ground - U(13.6f), U(1.6f), U(2.4f), U(0.6f))) { using (SolidBrush b = new SolidBrush(wd)) g.FillPath(b, p); g.DrawPath(thin, p); }
            // фара, бампер, подножка
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 240, 180))) g.FillEllipse(b, x0 + U(0.3f), Ground - U(8.8f), U(1.9f), U(1.9f));
            g.DrawEllipse(thin, x0 + U(0.3f), Ground - U(8.8f), U(1.9f), U(1.9f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 84, 84))) g.FillRectangle(b, x0 + U(53.4f), Ground - U(9.6f), U(1f), U(2.6f));
            using (GraphicsPath p = RoundRect(x0 - U(0.6f), Ground - U(4.6f), U(55.6f), U(2f), U(0.9f))) { using (SolidBrush b = new SolidBrush(Color.FromArgb(104, 96, 96))) g.FillPath(b, p); g.DrawPath(ink, p); }
            // мигалка
            bool f = (tick / 6) % 2 == 0;
            Color red = Color.FromArgb(f ? 255 : 150, 230, 70, 70), blue = Color.FromArgb(f ? 150 : 255, 70, 120, 240);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 236, 226))) g.FillRectangle(b, x0 + U(20), Ground - U(24.2f), U(12), U(1.9f));
            using (SolidBrush b = new SolidBrush(red)) g.FillRectangle(b, x0 + U(20), Ground - U(24.2f), U(6), U(1.9f));
            using (SolidBrush b = new SolidBrush(blue)) g.FillRectangle(b, x0 + U(26), Ground - U(24.2f), U(6), U(1.9f));
            using (GraphicsPath p = RoundRect(x0 + U(20), Ground - U(24.2f), U(12), U(1.9f), U(0.6f))) g.DrawPath(thin, p);
            if (sirenT > 0)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(f ? 90 : 30, 255, 70, 70))) g.FillEllipse(b, x0 + U(12), Ground - U(31), U(16), U(14));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(f ? 30 : 90, 80, 130, 255))) g.FillEllipse(b, x0 + U(24), Ground - U(31), U(16), U(14));
            }
        }
        float rot = vanX / U(3.6f);
        DrawWheel(g, x0 + U(10), Ground - U(3.6f), U(3.6f), rot);
        DrawWheel(g, x0 + U(44), Ground - U(3.6f), U(3.6f), rot);
    }

    void DrawSiren(Graphics g)
    {
        bool f = (tick / 6) % 2 == 0;
        RectangleF r = new RectangleF(A - U(10), Ground - U(60), U(220), U(70));
        using (SolidBrush b = new SolidBrush(f ? Color.FromArgb(28, 255, 30, 40) : Color.FromArgb(28, 40, 90, 255))) g.FillEllipse(b, r);
    }

    float JailA() { float a = Math.Min(1f, jailEnterT / 20f); return 1 - (1 - a) * (1 - a); }

    void DrawJailBack(Graphics g)
    {
        float by = Ground + (1 - JailA()) * U(62);
        float ox = JailX;
        Shadow(g, ox + U(60), U(124));
        Panel(g, Color.FromArgb(128, 132, 142), ox, by, 0, 0, 120, 54, 0.6f);
        RectangleF wr = R(ox, by, 0, 0, 120, 54);
        GraphicsState cs = g.Save();
        g.SetClip(wr);
        using (Pen p = new Pen(Color.FromArgb(40, 30, 30, 40), Math.Max(1, U(0.25f))))
            for (int row = 0; row < 14; row++)
            {
                float y = by - U(3 + row * 3.8f);
                g.DrawLine(p, ox, y, ox + U(120), y);
                for (int k = 0; k < 16; k++) { float x = ox + U(k * 8 + (row % 2) * 4); g.DrawLine(p, x, y, x, y - U(3.8f)); }
            }
        g.Restore(cs);
        Flat(g, Color.FromArgb(96, 100, 110), ox, by, 0, 0, 120, 3, 0);
        Panel(g, Color.FromArgb(170, 215, 255), ox, by, 52, 38, 16, 10, 0.4f);
        using (Pen p = new Pen(Color.FromArgb(60, 64, 72), Math.Max(1, U(0.6f)))) for (int i = 1; i < 4; i++) g.DrawLine(p, ox + U(52 + i * 4), by - U(38), ox + U(52 + i * 4), by - U(48));
        using (Pen p = new Pen(Color.FromArgb(150, 245, 245, 245), Math.Max(1, U(0.35f))))
            for (int i = 0; i < tally; i++)
            {
                int grp = i / 5, k = i % 5;
                float x = ox + U(92 + grp * 6 + k * 1.1f), y = by - U(30);
                if (k < 4) g.DrawLine(p, x, y, x, y - U(4));
                else g.DrawLine(p, ox + U(91.5f + grp * 6), y - U(0.6f), ox + U(96.5f + grp * 6), y - U(3.4f));
            }
        Flat(g, Color.FromArgb(60, 44, 34), ox, by, 12, 0, 1.6f, 5, 0.3f);
        Flat(g, Color.FromArgb(60, 44, 34), ox, by, 76, 0, 1.6f, 5, 0.3f);
        Wood(g, pal['B'], ox, by, 8, 4.6f, 74, 2.6f, 0.6f);
        Panel(g, Color.FromArgb(150, 155, 165), ox, by, 102, 0, 7, 6, 0.6f);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 240, 240, 240))) g.DrawString("Здесь был Стрелок", fntTiny, b, ox + U(6), by - U(32));
        Panel(g, Color.FromArgb(235, 235, 240), ox, by, 90, 0, 3, 3, 0.4f);
    }

    void DrawJailFront(Graphics g)
    {
        float by = Ground + (1 - JailA()) * U(62);
        float ox = JailX + (barsShake > 0 ? (float)Math.Sin(tick * 2.5) * U(0.35f) : 0);
        for (int i = 0; i <= 20; i++)
        {
            RectangleF r = R(ox, by, i * 6 - 0.5f, 0, 1.1f, 54);
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, r.Width + 2, r.Height), Color.FromArgb(110, 115, 128), Color.FromArgb(48, 50, 58), 0f)) g.FillRectangle(b, r);
        }
        foreach (float yy in new float[] { 1, 27, 52.6f }) Panel(g, Color.FromArgb(80, 84, 96), ox, by, -0.5f, yy, 121, 1.4f, 0.4f);
        Panel(g, Color.FromArgb(200, 170, 60), ox, by, 63, 22, 4.4f, 5, 0.6f);
        Panel(g, Color.FromArgb(170, 40, 46), ox, by, 40, 55, 40, 5.6f, 1.2f);
        RectangleF sr = R(ox, by, 40, 55, 40, 5.6f);
        using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.White)) g.DrawString("ИЗОЛЯТОР", fntSign, b, sr, sf); }
    }

    // ================= поставка на машине (склад) =================
    List<float[]> boxes = new List<float[]>();
    int carState; float carX, carSpeed; bool trunkOpen, deliveryPending, driverInCar;
    float CarStop { get { return A + U(2); } }

    Cat NewBald()
    {
        Cat d = NewGuest();
        d.MainC = Color.FromArgb(236, 194, 184); d.BellyC = Color.FromArgb(248, 222, 214); d.Stripes = false; d.Bald = true;
        d.IrisC = Color.FromArgb(90, 160, 230); d.Outfit = 4; d.C1 = Color.FromArgb(32, 32, 38); d.C2 = Color.White;
        d.Hat = 0; d.Glasses = false; d.Chain = true; d.Vape = true; d.Speed = 1.3f;
        return d;
    }

    void UpdateCar()
    {
        if (carState == 1)
        {
            float d = CarStop - carX;
            carX += Math.Max(U(0.3f), d * 0.05f);
            if (tick % 3 == 0) Exhaust(carX - U(0.5f), Ground - U(3), -1);
            if (CarStop - carX < U(0.3f)) { carX = CarStop; carState = 2; }
        }
        else if (carState == 3)
        {
            carSpeed += 0.04f * u; carX -= carSpeed;
            if (tick % 2 == 0) Exhaust(carX + U(46.5f), Ground - U(3), 1);
            if (carX < -U(60)) carState = 0;
        }
    }

    void AddDelivery(List<Func<bool>> s)
    {
        Cat drv = null;
        Func<Cat> D = delegate { return drv; };
        s.Add(Do(delegate { carState = 1; carX = -U(52); carSpeed = 0; trunkOpen = false; driverInCar = true; }));
        s.Add(delegate { return carState == 2; });
        s.Add(Wait(15));
        s.Add(Do(delegate
        {
            driverInCar = false;
            drv = NewBald(); drv.X = carX + U(30); drv.Dir = -1; guests.Add(drv);
        }));
        s.Add(SayF(D, "Здорово, братан! Поставка!", 45, 'K'));
        s.Add(Line(me, "О, привет! Заноси!", 30, 'h'));
        for (int k = 0; k < 3; k++)
        {
            float dx = A + U(16 + k * 13);
            s.Add(GoF(D, delegate { return carX + U(5); }));
            s.Add(Do(delegate { drv.Dir = -1; trunkOpen = true; }));
            s.Add(Wait(12));
            s.Add(Do(delegate { drv.Hold = 'K'; drv.HoldCol = 'B'; }));
            s.Add(GoF(D, delegate { return dx - U(4.8f); }));
            s.Add(Do(delegate { drv.Dir = 1; drv.Hold = ' '; boxes.Add(new float[] { dx, 2 }); Poof(dx, Ground - U(1)); }));
            if (k == 1) s.Add(SayF(D, "Тяжёлая, зараза...", 25, 'K'));
        }
        s.Add(SayF(D, "Распишись тут, братан", 40, 'K'));
        s.Add(GoF(delegate { return me; }, delegate { return Math.Min(Width - U(10), drv.X + U(10)); }));
        s.Add(Do(delegate { me.Dir = -1; drv.Dir = 1; }));
        s.Add(FlyStep('I', 'w', delegate { return Paw(drv); }, delegate { return Paw(me); }, 12));
        s.Add(Wait(15));
        s.Add(FlyStep('I', 'w', delegate { return Paw(me); }, delegate { return Paw(drv); }, 12));
        s.Add(Do(delegate { AddMoney(-(1500 + rnd.Next(25) * 100), true); }));
        s.Add(Line(me, "Готово! Спасибо!", 30, 'h'));
        s.Add(SayF(D, rnd.Next(100) < 45 ? "Я вернусь." : "Давай, на связи!", 30, 'K'));
        s.Add(GoF(D, delegate { return carX + U(30); }));
        s.Add(Do(delegate { guests.Remove(drv); driverInCar = true; trunkOpen = false; carState = 3; }));
        s.Add(delegate { return carState == 0; });
        s.Add(Do(delegate { driverInCar = false; if (rnd.Next(100) < 40) BigText("МИССИЯ ВЫПОЛНЕНА|+ уважение", 2, 110); }));
    }

    void DrawCar(Graphics g)
    {
        float x0 = carX, lw = Math.Max(1.4f, U(0.55f));
        Shadow(g, x0 + U(23), U(48));
        Color cc = Color.FromArgb(202, 80, 76), cd = Color.FromArgb(168, 60, 62), cl = Color.FromArgb(230, 122, 108);
        using (Pen ink = new Pen(InkC, lw))
        using (Pen thin = new Pen(Color.FromArgb(210, InkC), Math.Max(1f, U(0.32f))))
        {
            ink.LineJoin = LineJoin.Round; ink.StartCap = LineCap.Round; ink.EndCap = LineCap.Round; thin.StartCap = LineCap.Round; thin.EndCap = LineCap.Round;
            if (trunkOpen)
            {
                PointF[] lid = { V(x0, 0.6f, 16), V(x0, -1.6f, 24.6f), V(x0, 1.0f, 25.2f), V(x0, 2.6f, 16) };
                using (SolidBrush b = new SolidBrush(cd)) g.FillPolygon(b, lid);
                g.DrawPolygon(ink, lid);
            }
            PointF[] body = { V(x0, 0, 3.6f), V(x0, 0, 13.8f), V(x0, 1.6f, 16.2f), V(x0, 33.8f, 16.2f), V(x0, 40.4f, 10.0f), V(x0, 45.2f, 8.8f), V(x0, 46.4f, 7.0f), V(x0, 46.4f, 3.6f) };
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(body);
                using (SolidBrush b = new SolidBrush(cc)) g.FillPath(b, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(cd)) g.FillRectangle(b, x0 - U(1), Ground - U(6.2f), U(49), U(4));
                using (SolidBrush b = new SolidBrush(cl)) { g.FillRectangle(b, x0, Ground - U(10.0f), U(42), U(0.8f)); g.FillRectangle(b, x0 + U(2), Ground - U(16.2f), U(31), U(0.7f)); }
                CarArch(g, x0 + U(9), U(4.4f)); CarArch(g, x0 + U(37), U(4.4f));
                g.Restore(cs);
                g.DrawPath(ink, p);
            }
            CarGlass(g, new PointF[] { V(x0, 2.4f, 10.4f), V(x0, 2.4f, 15), V(x0, 13, 15), V(x0, 13, 10.4f) }, thin);
            CarGlass(g, new PointF[] { V(x0, 14.6f, 10.4f), V(x0, 14.6f, 15), V(x0, 25.4f, 15), V(x0, 25.4f, 10.4f) }, thin);
            CarGlass(g, new PointF[] { V(x0, 27, 10.4f), V(x0, 27, 15), V(x0, 33.4f, 15), V(x0, 38.0f, 10.4f) }, thin);
            if (driverInCar)
            {
                PointF h = V(x0, 31, 12.8f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 194, 184)))
                {
                    g.FillEllipse(b, h.X - U(2.2f), h.Y - U(2), U(4.4f), U(4));
                    g.FillPolygon(b, new PointF[] { new PointF(h.X - U(1.8f), h.Y - U(1.2f)), new PointF(h.X - U(1.4f), h.Y - U(3.6f)), new PointF(h.X - U(0.2f), h.Y - U(1.8f)) });
                    g.FillPolygon(b, new PointF[] { new PointF(h.X + U(0.4f), h.Y - U(1.8f)), new PointF(h.X + U(1.6f), h.Y - U(3.6f)), new PointF(h.X + U(2.0f), h.Y - U(1.0f)) });
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 26, 36))) g.FillEllipse(b, h.X + U(0.8f), h.Y - U(0.8f), U(0.8f), U(1.0f));
            }
            if (trunkOpen) using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 44, 42))) g.FillRectangle(b, new RectangleF(x0 + U(0.3f), Ground - U(15), U(1.6f), U(11)));
            // швы дверей, молдинг, ручки, рейлинги, зеркало
            g.DrawLine(thin, x0 + U(13.8f), Ground - U(4.6f), x0 + U(13.8f), Ground - U(10)); g.DrawLine(thin, x0 + U(26.2f), Ground - U(4.6f), x0 + U(26.2f), Ground - U(10));
            g.DrawLine(thin, x0 + U(0.6f), Ground - U(6.2f), x0 + U(45.6f), Ground - U(6.2f));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 226, 210)))
            {
                using (GraphicsPath p = RoundRect(x0 + U(10.4f), Ground - U(9.3f), U(2.2f), U(0.7f), U(0.3f))) { g.FillPath(b, p); g.DrawPath(thin, p); }
                using (GraphicsPath p = RoundRect(x0 + U(22.8f), Ground - U(9.3f), U(2.2f), U(0.7f), U(0.3f))) { g.FillPath(b, p); g.DrawPath(thin, p); }
            }
            using (Pen pp = new Pen(Color.FromArgb(96, 86, 86), Math.Max(1.2f, U(0.55f)))) { pp.StartCap = LineCap.Round; pp.EndCap = LineCap.Round; g.DrawLine(pp, x0 + U(4), Ground - U(17.0f), x0 + U(31), Ground - U(17.0f)); g.DrawLine(pp, x0 + U(6), Ground - U(17.0f), x0 + U(6), Ground - U(16.3f)); g.DrawLine(pp, x0 + U(29), Ground - U(17.0f), x0 + U(29), Ground - U(16.3f)); }
            using (GraphicsPath p = RoundRect(x0 + U(37.2f), Ground - U(11.6f), U(2f), U(1.5f), U(0.6f))) { using (SolidBrush b = new SolidBrush(cd)) g.FillPath(b, p); g.DrawPath(thin, p); }
            // фара, задний фонарь, бампер
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 240, 180))) g.FillEllipse(b, x0 + U(43.2f), Ground - U(8.5f), U(2.6f), U(1.6f));
            g.DrawEllipse(thin, x0 + U(43.2f), Ground - U(8.5f), U(2.6f), U(1.6f));
            using (GraphicsPath p = RoundRect(x0 + U(0.1f), Ground - U(11.8f), U(1.1f), U(3.2f), U(0.4f))) { using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 96, 90))) g.FillPath(b, p); g.DrawPath(thin, p); }
            using (GraphicsPath p = RoundRect(x0 - U(0.6f), Ground - U(4.6f), U(47.6f), U(2f), U(0.9f))) { using (SolidBrush b = new SolidBrush(Color.FromArgb(104, 96, 96))) g.FillPath(b, p); g.DrawPath(ink, p); }
        }
        float rot = carX / U(3.3f);
        DrawWheel(g, x0 + U(9), Ground - U(3.3f), U(3.3f), rot);
        DrawWheel(g, x0 + U(37), Ground - U(3.3f), U(3.3f), rot);
    }

    // ================= тряска шкафа мышкой =================
    bool grabRack, grabMoved; int grabX0, reactCooldown; float shakeEnergy, wobbleAmp = 3;
    static readonly string[] ShakeLines = { "Эй, не тряси!", "Аккуратнее!!!", "Там же товар!", "Ну зачем...", "Пыль же!", "Я только убрался!" };

    bool RackGrabbable(Point pt)
    {
        return mode == 3 && pendingMode < 0 && !rackFallen && !rackFalling && !rackLifting && R(RackX, Ground, -2, 0, 38, 47).Contains(pt);
    }

    void RackShakeEvent(bool soft)
    {
        for (int k = 0; k < (soft ? 4 : 7); k++)
        {
            Particle p = new Particle();
            p.Kind = 5; p.X = RackX + U((float)rnd.NextDouble() * 34); p.Y = Ground - U(26 + (float)rnd.NextDouble() * 18);
            p.VX = (float)(rnd.NextDouble() - 0.5) * 0.6f * u; p.VY = (0.05f + (float)rnd.NextDouble() * 0.15f) * u;
            p.R = U(0.8f + (float)rnd.NextDouble() * 1.2f); p.Max = 40;
            parts.Add(p);
        }
        if (dust.Count < 7 && rnd.Next(100) < (soft ? 25 : 45))
            dust.Add(new float[] { Math.Max(U(6), Math.Min(Width - U(6), RackX + U(-8 + (float)rnd.NextDouble() * 50))), 1.2f + (float)rnd.NextDouble() * 1.2f });
        if (rnd.Next(100) < (soft ? 30 : 50))
        {
            List<int> filled = new List<int>();
            for (int i = 0; i < 15; i++) if (slots[i] != ' ') filled.Add(i);
            if (filled.Count > 0)
            {
                int k = filled[rnd.Next(filled.Count)];
                char cc = slots[k]; slots[k] = ' ';
                PointF sp = SlotPos(k);
                float tx = RackX + U(-10 + (float)rnd.NextDouble() * 54);
                Fly('X', cc, sp.X, sp.Y, tx, Ground - U(1.75f), 14, delegate { AddPileAt(tx, cc); });
                celebrated = false;
            }
        }
        if (reactCooldown <= 0)
        {
            Say(ShakeLines[rnd.Next(ShakeLines.Length)], me.X, CatTop(me) - U(1), 'R');
            me.SweatT = 60; me.Shake = 15; reactCooldown = 70;
        }
    }

    // ================= музыка и кино =================
    int musicT, movieT;

    void OpenUrl(string url)
    {
        try { ShellOpen(url); }
        catch (Exception ex) { Say("Не открылось: " + ex.Message, me.X, CatTop(me) - U(1), 'R'); }
    }

    void UpdateMedia()
    {
        if (musicT > 0)
        {
            musicT--;
            if (tick % 20 == 0)
            {
                PointF h = Local(me, 1, -24);
                Particle p = new Particle(); p.Kind = 2; p.X = h.X + U((float)rnd.NextDouble() * 6 - 3); p.Y = h.Y;
                p.VY = -0.3f * u; p.Max = 70; p.Col = ItemColors[rnd.Next(ItemColors.Length)];
                parts.Add(p);
            }
            if (musicT == 0 && mode == 0) me.Y = 0;
        }
        if (movieT > 0)
        {
            movieT--;
            if (movieT % 160 == 80) Say(rnd.Next(2) == 0 ? "Ням-ням" : (rnd.Next(2) == 0 ? "О, поворот!" : "Ахаха"), me.X, CatTop(me) - U(1), 'U');
            if (movieT == 0 && me.Hold == 'Q') me.Hold = ' ';
        }
    }

    // ================= сценарий: ревизия =================
    int countShelf = -1, countIdx, lastRev = -1; bool revPending;

    Cat MakeBoss()
    {
        Cat boss = new Cat();
        boss.Id = catIds++; boss.Boss = true; boss.Scale = 1.35f; boss.Vape = true; boss.Outfit = 3;
        boss.C1 = Color.FromArgb(38, 38, 48); boss.C2 = Color.FromArgb(210, 40, 50); boss.Glasses = true;
        boss.IrisC = Color.FromArgb(230, 170, 50);
        boss.MainC = Color.FromArgb(52, 50, 60); boss.BellyC = Color.FromArgb(125, 120, 135);
        boss.Phase = 9; boss.Speed = 1.0f;
        return boss;
    }

    Func<bool> CountStep()
    {
        int t = 0;
        return delegate
        {
            t++;
            if (t % 6 == 0)
            {
                countIdx++;
                if (countIdx % 6 == 0 && countIdx < 18) Say("*пик* " + countIdx, ShelfX(Math.Max(0, countShelf)) + U(14), Ground - U(50), 'K');
            }
            return countIdx >= 18;
        };
    }

    void SetupRevision()
    {
        countShelf = -1; digging = false; revPending = true;
        Poof(me.X, CatTop(me) + U(8));
        me.X = CashX; me.Dir = 1;
        script = null;
    }

    List<Func<bool>> BuildRevision()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        Cat boss = null;
        Func<Cat> B = delegate { return boss; };
        int variant; do { variant = rnd.Next(3); } while (variant == lastRev);
        lastRev = variant;
        int baseN = 30 + rnd.Next(25), miss = 2 + rnd.Next(3);

        s.Add(Wait(30));
        string[] idle = { "Ля-ля-ля... тихий денёк", "Сейчас бы чайку...", "Касса сошлась, красота" };
        s.Add(Line(me, idle[rnd.Next(idle.Length)], 40, 'h'));
        s.Add(Do(delegate
        {
            boss = MakeBoss(); boss.X = Width + U(16); boss.Dir = -1; boss.Hold = 'T'; boss.HoldCol = 'G';
            boss.Speed = 1.8f; guests.Add(boss); Go(boss, CounterX + U(34));
        }));
        s.Add(delegate { return !boss.Walking; });
        s.Add(Do(delegate { boss.Speed = 1.0f; }));
        if (rnd.Next(100) < 40) s.Add(SayF(B, "Продавец... я — твой начальник.", 50, 'K'));
        s.Add(SayF(B, "Так-так-так. Внеплановая ревизия!", 50, 'K'));
        s.Add(Do(delegate { me.SweatT = 150; me.Shake = 25; }));
        s.Add(Line(me, "О... з-здрасьте, начальник!", 40, 'w'));
        foreach (int sh in new int[] { 1, 0 })
        {
            int shelf = sh;
            s.Add(GoF(B, delegate { return ShelfX(shelf) + U(16); }));
            s.Add(Do(delegate { boss.Dir = -1; countShelf = shelf; countIdx = 0; }));
            s.Add(CountStep());
            s.Add(Do(delegate { countShelf = -1; }));
        }
        s.Add(GoF(B, delegate { return CounterX + U(34); }));
        s.Add(Do(delegate { boss.Dir = -1; }));
        s.Add(SayF(B, "По базе " + baseN + " флаконов. На полках — " + (baseN - miss) + ".", 55, 'K'));
        s.Add(Do(delegate { boss.AngryT = 70; boss.Shake = 20; }));
        s.Add(SayF(B, "Где ещё " + miss + "?!", 40, 'R'));

        string[,] pairs = {
            { "Это... усушка!", "Жижа не усыхает!" },
            { "Испарилась! Это же пар!", "Очень смешно." },
            { "Тестеры! Покупатели пробовали!", miss + " флакона?!" },
            { "Мышь утащила!", "Мышь. С вейпом." },
            { "Пересорт! В МойСклад не туда провели!", "Хм... проверю." },
            { "Это... инфляция?", "Это не так работает." },
            { "Их забрал лысый на Ларгусе!", "Лысый... опять он." } };
        List<int> idx = new List<int>();
        while (idx.Count < 3) { int k = rnd.Next(pairs.GetLength(0)); if (!idx.Contains(k)) idx.Add(k); }
        foreach (int k in idx)
        {
            string ex = pairs[k, 0], rep = pairs[k, 1];
            s.Add(Line(me, ex, 45, 'w'));
            s.Add(Do(delegate { boss.AngryT = 50; }));
            s.Add(SayF(B, rep, 40, 'K'));
        }

        if (variant == 0) // нашлись под прилавком
        {
            s.Add(Line(me, "Подождите! Щас...", 30, 'w'));
            s.Add(Do(delegate { me.Sitting = true; digging = true; }));
            s.Add(Wait(75));
            s.Add(Do(delegate
            {
                me.Sitting = false; digging = false;
                for (int i = 0; i < miss; i++)
                {
                    char cc = ItemColors[rnd.Next(ItemColors.Length)];
                    Fly('B', cc, CounterX + U(4), Ground - U(8), ShelfX(i % 2), Ground - U(30), 16 + i * 4, null);
                }
            }));
            s.Add(Line(me, "Нашёл! Закатились под прилавок!", 50, 'h'));
            s.Add(SayF(B, "Хм. Повезло тебе. Работай.", 45, 'K'));
        }
        else if (variant == 1) // в карманах продавца
        {
            s.Add(SayF(B, "А ну-ка, выверни карманы.", 40, 'K'));
            s.Add(Do(delegate
            {
                me.Shake = 40; me.SweatT = 100;
                for (int i = 0; i < miss; i++)
                {
                    PointF pw = Paw(me);
                    Fly('B', ItemColors[rnd.Next(ItemColors.Length)], pw.X, pw.Y, me.X + U(-6 + i * 4), Ground - U(1.5f), 14 + i * 5, null);
                }
            }));
            s.Add(Wait(30));
            s.Add(Line(me, "Это... не моё... оно само...", 45, 'w'));
            s.Add(Do(delegate { boss.AngryT = 90; boss.StompT = 42; }));
            s.Add(SayF(B, "Из зарплаты вычту!", 45, 'R'));
            s.Add(Do(delegate { AddMoney(-miss * 450, true); }));
            s.Add(Line(me, "Эх...", 30, 'w'));
        }
        else // у самого начальника
        {
            s.Add(Do(delegate
            {
                boss.Shake = 20;
                for (int i = 0; i < miss; i++)
                {
                    PointF bp = Local(boss, 0, -9);
                    Fly('B', ItemColors[rnd.Next(ItemColors.Length)], bp.X, bp.Y, boss.X + U(-5 + i * 4), Ground - U(1.5f), 14 + i * 5, null);
                }
            }));
            s.Add(Wait(30));
            s.Add(Line(me, "Начальник... а это не ваши?", 45, 'n'));
            s.Add(Do(delegate { boss.SweatT = 100; boss.Shake = 30; }));
            s.Add(SayF(B, "...Ревизия окончена.", 40, 'K'));
            if (rnd.Next(2) == 0) s.Add(SayF(B, "Я вернусь.", 35, 'K'));
            s.Add(Do(delegate { boss.Speed = 2.8f; }));
        }
        s.Add(GoF(B, delegate { return Width + U(18); }));
        s.Add(Do(delegate { guests.Remove(boss); }));
        string[] after = variant == 0 ? new string[] { "Фух... пронесло" } : (variant == 1 ? new string[] { "Ну и денёк..." } : new string[] { "Ха! Вот это поворот", "Ревизор, называется" });
        s.Add(Line(me, after[rnd.Next(after.Length)], 45, variant == 1 ? 'w' : 'h'));
        return s;
    }

    void UpdateRevision()
    {
        if (enterT < 40) return;
        if (digging && tick % 7 == 0)
        {
            float dx = me.X - U(4 + (float)rnd.NextDouble() * 14);
            Fly('X', ItemColors[rnd.Next(ItemColors.Length)], CounterX + U(4), Ground - U(12), dx, Ground - U(2), 18, null);
        }
        if (RunScript()) return;
        if (revPending) { revPending = false; script = BuildRevision(); }
        else
        {
            revPending = true;
            script = new List<Func<bool>>();
            script.Add(Do(delegate { if (me.Walking == false && Math.Abs(me.X - CashX) > U(1)) Go(me, CashX); }));
            script.Add(delegate { return !me.Walking; });
            script.Add(Do(delegate { me.Dir = 1; if (me.PuffT == 0) me.PuffT = 90; }));
            script.Add(Wait(300 + rnd.Next(300)));
        }
        scriptIdx = 0;
    }

    // ================= пасхалки =================
    List<Func<bool>> egg; int eggIdx, eggCooldown = 1800;
    string bigText; int bigT, bigMax, bigStyle, matrixT;
    Font fntBigSerif, fntBigBold, fntMatrix;

    void BigText(string t, int style, int dur) { bigText = t; bigStyle = style; bigT = 0; bigMax = dur; }

    void RunEgg()
    {
        if (egg == null) return;
        int guard = 0;
        while (egg != null && eggIdx < egg.Count && guard++ < 8) { if (egg[eggIdx]()) eggIdx++; else break; }
        if (egg != null && eggIdx >= egg.Count) egg = null;
    }

    void UpdateEggs()
    {
        if (bigText != null && ++bigT > bigMax) bigText = null;
        if (matrixT > 0)
        {
            matrixT--;
            if (tick % 2 == 0)
                for (int i = 0; i < 2; i++)
                {
                    Particle p = new Particle();
                    p.Kind = 8; p.X = (float)Math.Floor(rnd.NextDouble() * (Width / U(3))) * U(3); p.Y = -U(3);
                    p.VY = (0.6f + (float)rnd.NextDouble() * 0.6f) * u; p.Max = (int)(Height / p.VY) + 4;
                    p.Col = "01ЖИЖАVAPE0110КОТ"[rnd.Next(17)];
                    parts.Add(p);
                }
        }
        RunEgg();
        if (--eggCooldown <= 0)
        {
            eggCooldown = 2400 + rnd.Next(2400);
            if (egg == null && pendingMode < 0) TryRandomEgg();
        }
    }

    Cat AnyGuest()
    {
        List<Cat> l = new List<Cat>();
        foreach (Cat c in guests) if (!c.Hidden && c.Outfit != 11 && !c.Boss) l.Add(c);
        return l.Count == 0 ? null : l[rnd.Next(l.Count)];
    }

    void TryRandomEgg()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        int r = rnd.Next(100);
        switch (mode)
        {
            case 0:
                if ((IsSnow || IsCold) && r < 30) s.Add(Line(me, "Зима близко...", 45, 'n'));
                else if (r < 45) s.Add(Line(me, "Раньше я тоже гулял... пока не словил стрелу в колено", 60, 'w'));
                else if (r < 70) s.Add(Line(me, "Жизнь как коробка конфет: не знаешь, какая жижа попадётся", 60, 'h'));
                else
                {
                    s.Add(Do(delegate { me.SweatT = 120; matrixT = 170; }));
                    s.Add(Line(me, "Кажется... я вижу код", 50, 'w'));
                    s.Add(Wait(60));
                    s.Add(Line(me, "Ложки нет. Есть только жижа.", 50, 'h'));
                }
                break;
            case 1:
            {
                Cat f = null;
                foreach (Cat g in guests) if (g.Phase <= 2 && !g.Walking) { f = g; break; }
                if (f == null) { s.Add(Line(me, "Касса — мой путь. Таков путь.", 45, 'h')); break; }
                Cat ff = f;
                s.Add(SayStep(me, "Скажи моё имя.", 40));
                s.Add(SayF(delegate { return ff; }, "...Кассир?", 35, 'K'));
                s.Add(Line(me, "Ты чертовски прав.", 40, 'h'));
                break;
            }
            case 2:
                if (tvState == 4 && r < 45)
                {
                    s.Add(Do(delegate { me.SweatT = 60; }));
                    s.Add(Do(delegate { BigText("ВЫ ПОГИБЛИ", 0, 110); }));
                    s.Add(Wait(40));
                    s.Add(Line(me, "Опять с первого костра...", 45, 'w'));
                }
                else if (r < 70) s.Add(Line(me, "Когда уже Half-Life 3?", 45, 'w'));
                else s.Add(Line(me, "Ещё одну катку — и спать. Честно.", 45, 'h'));
                break;
            case 3:
                if (celebrated && r < 50) s.Add(Do(delegate { BigText("МИССИЯ ВЫПОЛНЕНА|+ уважение", 2, 110); me.HappyT = 60; }));
                else if (r < 75) s.Add(Line(me, "Это не склад, это квест какой-то", 45, 'w'));
                else s.Add(Line(me, "Работа не волк... но и не жижа", 45, 'n'));
                break;
            case 4:
            {
                if (r < 35)
                {
                    s.Add(SayF(delegate { return AnyGuest() ?? me; }, "Танцуют все!", 40, 'K'));
                    s.Add(Do(delegate { foreach (Cat c in guests) if (!c.Swimming && !c.Sitting && !c.Jumping) c.HappyT = 90; me.HappyT = 90; }));
                }
                else if (r < 65) s.Add(SayF(delegate { return AnyGuest() ?? me; }, "Акуна матата!", 40, 'K'));
                else { s.Add(Do(delegate { me.HappyT = 80; })); s.Add(Line(me, "Я король мира!", 45, 'h')); }
                break;
            }
            case 5:
                if (omonPhase != 1 || inmates.Count < 2) return;
                {
                    Cat a = inmates[rnd.Next(inmates.Count)], b = inmates[rnd.Next(inmates.Count)];
                    if (r < 25) s.Add(Line(a, "Пасть порву, моргалы выколю!", 50, 'a'));
                    else if (r < 45) s.Add(Line(a, "Граждане! Храните деньги в сберегательной кассе!", 60, 'h'));
                    else if (r < 65) s.Add(Line(a, "Пацаны не извиняются.", 45, 'n'));
                    else { s.Add(Line(a, "В чём сила, брат?", 40, 'n')); if (b != a) s.Add(Line(b, "Сила в правде.", 40, 'n')); }
                }
                break;
            case 6:
                s.Add(Line(me, r < 50 ? "Ревизор... как у Гоголя" : "Главное — не паниковать", 45, 'w'));
                break;
        }
        if (s.Count > 0) { egg = s; eggIdx = 0; }
    }

    void DrawBigText(Graphics g)
    {
        if (bigText == null) return;
        float k = bigT < 12 ? bigT / 12f : (bigT > bigMax - 18 ? Math.Max(0, (bigMax - bigT) / 18f) : 1f);
        int a = (int)(255 * k);
        float cy = Height * 0.45f;
        string[] lines = bigText.Split('|');
        if (bigStyle == 0)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, cy - U(10), Width, U(20)), Color.FromArgb((int)(170 * k), 0, 0, 0), Color.FromArgb((int)(170 * k), 0, 0, 0), 90f))
            {
                ColorBlend cb = new ColorBlend();
                cb.Colors = new Color[] { Color.FromArgb(0, 0, 0, 0), Color.FromArgb((int)(180 * k), 0, 0, 0), Color.FromArgb(0, 0, 0, 0) };
                cb.Positions = new float[] { 0, 0.5f, 1 };
                b.InterpolationColors = cb;
                g.FillRectangle(b, 0, cy - U(10), Width, U(20));
            }
            OutlinedText(g, lines[0], fntBigSerif, Color.FromArgb(a, 175, 25, 25), Color.FromArgb(a / 2, 0, 0, 0), Width / 2f, cy, 1.0f);
        }
        else if (bigStyle == 1)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, cy - U(12), Width, U(24)), Color.Black, Color.Black, 90f))
            {
                ColorBlend cb = new ColorBlend();
                cb.Colors = new Color[] { Color.FromArgb(0, 60, 60, 60), Color.FromArgb((int)(150 * k), 60, 60, 60), Color.FromArgb(0, 60, 60, 60) };
                cb.Positions = new float[] { 0, 0.5f, 1 };
                b.InterpolationColors = cb;
                g.FillRectangle(b, 0, cy - U(12), Width, U(24));
            }
            OutlinedText(g, lines[0], fntBigBold, Color.FromArgb(a, 235, 235, 235), Color.FromArgb(a, 0, 0, 0), Width / 2f, cy, 1.0f);
        }
        else
        {
            OutlinedText(g, lines[0], fntBigBold, Color.FromArgb(a, 255, 210, 60), Color.FromArgb(a, 0, 0, 0), Width / 2f, cy - (lines.Length > 1 ? U(4) : 0), 0.85f);
            if (lines.Length > 1) OutlinedText(g, lines[1], fnt, Color.FromArgb(a, 255, 255, 255), Color.FromArgb(a, 0, 0, 0), Width / 2f, cy + U(5), 1.0f);
        }
    }

    void OutlinedText(Graphics g, string t, Font f, Color fill, Color outline, float cx, float cy, float scale)
    {
        using (GraphicsPath p = new GraphicsPath())
        using (StringFormat sf = new StringFormat())
        {
            sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
            p.AddString(t, f.FontFamily, (int)f.Style, f.Size * scale, new PointF(cx, cy), sf);
            using (Pen pen = new Pen(outline, Math.Max(1.5f, f.Size * scale * 0.14f))) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
            using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
        }
    }

    // ================= GTA: деньги и здоровье =================
    List<float[]> pickups = new List<float[]>(); // x, сумма
    long money; float health = 100; int hudFlashT, hurtFlashT;
    Font fntHud;

    void AddMoney(long d, bool bubble)
    {
        money += d; if (money < 0) money = 0;
        hudFlashT = 25;
        if (bubble) Say((d >= 0 ? "+$" : "-$") + Math.Abs(d), me.X, CatTop(me) - U(1), d >= 0 ? 'n' : 'R');
        SaveSettings();
    }

    void Hurt(float dmg)
    {
        health -= dmg; hurtFlashT = 20;
        if (health <= 0)
        {
            health = 100;
            long fee = money / 10;
            BigText("ПОТРАЧЕНО", 1, 120);
            List<Func<bool>> e = new List<Func<bool>>();
            e.Add(Wait(70));
            e.Add(Do(delegate { if (fee > 0) AddMoney(-fee, false); }));
            e.Add(Line(me, fee > 0 ? "Больница: -$" + fee + "..." : "Больница... хорошо, что бесплатно", 55, 'w'));
            egg = e; eggIdx = 0;
        }
    }

    void UpdateGta()
    {
        if (health < 100) health = Math.Min(100, health + 0.03f);
        if (hudFlashT > 0) hudFlashT--;
        if (hurtFlashT > 0) hurtFlashT--;
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            float[] p = pickups[i];
            if (!me.Hidden && !me.Lying && Math.Abs(me.X - p[0]) < U(3.2f) && me.Y < U(2))
            {
                pickups.RemoveAt(i);
                for (int k = 0; k < 5; k++)
                {
                    Particle sp = new Particle(); sp.Kind = 4; sp.X = p[0]; sp.Y = Ground - U(3);
                    double a = k * Math.PI * 2 / 5; sp.VX = (float)Math.Cos(a) * 0.3f * u; sp.VY = (float)Math.Sin(a) * 0.3f * u - 0.15f * u; sp.Max = 25;
                    parts.Add(sp);
                }
                AddMoney((long)p[1], true);
            }
        }
    }

    void DrawPickups(Graphics g)
    {
        foreach (float[] p in pickups)
        {
            float cx = p[0];
            float bob = (float)Math.Sin((tick + cx) * 0.1) * U(0.5f);
            float cy = Ground - U(4.2f) + bob;
            Glow(g, cx, Ground - U(0.6f), U(4.5f), U(1.4f), Color.FromArgb(90, 255, 110), 150);
            Glow(g, cx, cy, U(4), U(4), Color.FromArgb(140, 255, 150), 70);
            float sc = (float)Math.Cos(tick * 0.12 + cx * 0.05);
            float w = U(6.2f) * Math.Max(0.12f, Math.Abs(sc));
            for (int i = 2; i >= 0; i--)
            {
                RectangleF r = new RectangleF(cx - w / 2, cy - U(1.5f) - i * U(0.6f), w, U(2.9f));
                using (GraphicsPath gp = RoundRect(r.X, r.Y, r.Width, r.Height, U(0.3f)))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width + 0.1f, r.Height + 2), Color.FromArgb(120, 200, 110), Color.FromArgb(40, 110, 50), 90f)) g.FillPath(b, gp);
                    using (Pen pp = new Pen(Color.FromArgb(25, 70, 30), Math.Max(1, U(0.2f)))) g.DrawPath(pp, gp);
                }
            }
            if (Math.Abs(sc) > 0.45f)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 230, 255, 220))) g.FillEllipse(b, cx - w * 0.22f, cy - U(1.1f) - U(0.1f), w * 0.44f, U(1.5f));
                using (StringFormat sf = new StringFormat()) { sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 90, 35))) g.DrawString("$", fntTiny, b, cx, cy - U(0.35f), sf); }
            }
            if (tick % 18 == (int)cx % 18)
            {
                Particle s = new Particle(); s.Kind = 4; s.X = cx + U((float)rnd.NextDouble() * 4 - 2); s.Y = cy - U(2); s.VY = -0.1f * u; s.Max = 20;
                parts.Add(s);
            }
        }
    }

    void DrawHud(Graphics g)
    {
        try { DrawPanel(g); } catch (Exception ex) { GuardLog("панель", ex); }
    }


    // ================= костюмированные гости =================
    int lastCostume = -1;

    void ApplyCostume(Cat c, int id)
    {
        c.Hat = 0; c.Glasses = false; c.SmallShades = false; c.Speed = 1.0f; c.HatBack = false;
        switch (id)
        {
            case 0: c.Outfit = 12; c.C1 = Color.FromArgb(24, 24, 28); c.C2 = Color.FromArgb(20, 20, 24); c.SmallShades = true; c.MainC = Color.FromArgb(70, 66, 74); c.BellyC = Color.FromArgb(160, 154, 166); break;
            case 1: c.Outfit = 13; c.C1 = Color.FromArgb(196, 182, 150); c.C2 = Color.FromArgb(60, 86, 140); break;
            case 2: c.Outfit = 4; c.C1 = Color.FromArgb(28, 30, 40); c.C2 = Color.White; c.Hat = 1; c.HatC = Color.FromArgb(24, 24, 30); c.MainC = Color.FromArgb(120, 112, 104); c.BellyC = Color.FromArgb(200, 192, 182); break;
            case 3: c.Outfit = 9; c.C1 = Color.FromArgb(28, 26, 30); c.C2 = Color.FromArgb(40, 44, 60); c.Glasses = true; c.MainC = Color.FromArgb(150, 146, 140); c.BellyC = Color.FromArgb(220, 214, 206); c.Vape = false; break;
            case 4: c.Outfit = 12; c.C1 = Color.FromArgb(124, 86, 54); c.C2 = Color.FromArgb(96, 66, 42); c.Hat = 9; c.HatC = Color.FromArgb(110, 76, 48); c.MainC = Color.FromArgb(242, 208, 155); c.BellyC = Color.FromArgb(255, 246, 228); c.Vape = false; break;
            case 5: c.Outfit = 14; c.C1 = Color.FromArgb(176, 150, 110); c.C2 = Color.FromArgb(90, 74, 60); c.Hat = 7; c.HatC = Color.FromArgb(176, 150, 110); break;
            case 6: c.Outfit = 15; c.C1 = Color.FromArgb(96, 104, 70); c.C2 = Color.FromArgb(60, 64, 50); c.Hat = 8; c.HatC = Color.FromArgb(96, 104, 70); c.Vape = false; break;
            case 7: c.Outfit = 16; c.C1 = Color.FromArgb(245, 245, 248); c.C2 = Color.FromArgb(50, 46, 60); c.Hat = 10; c.HatC = Color.FromArgb(30, 28, 34); break;
            case 8: c.Outfit = 17; c.C1 = Color.FromArgb(236, 226, 205); c.C2 = Color.FromArgb(70, 90, 140); c.Hat = 11; c.HatC = Color.FromArgb(150, 100, 60); break;
            case 9: c.Outfit = 18; c.C1 = Color.FromArgb(248, 248, 250); c.C2 = Color.FromArgb(80, 120, 170); c.Hat = 16; c.HatC = Color.FromArgb(200, 205, 215); c.Vape = false; break;
            case 10: c.Outfit = 19; c.C1 = Color.FromArgb(240, 240, 244); c.C2 = Color.FromArgb(232, 232, 238); c.Hat = 12; c.Vape = false; break;
            case 11: c.Outfit = 20; c.C1 = Color.FromArgb(22, 22, 26); c.C2 = Color.FromArgb(22, 22, 26); c.MainC = Color.FromArgb(246, 246, 246); c.BellyC = Color.FromArgb(255, 220, 210); break;
            case 12: c.Outfit = 21; c.C1 = Color.FromArgb(40, 52, 96); c.C2 = Color.FromArgb(40, 40, 50); c.Hat = 13; c.HatC = Color.FromArgb(220, 40, 50); c.Vape = false; break;
            case 13: c.Outfit = 22; c.C1 = Color.FromArgb(50, 90, 210); c.C2 = Color.FromArgb(50, 90, 210); c.Hat = 14; c.HatC = Color.FromArgb(200, 30, 50); c.Vape = false; break;
            case 14: c.Outfit = 23; c.C1 = Color.FromArgb(90, 60, 160); c.C2 = Color.FromArgb(70, 46, 130); c.Hat = 15; c.HatC = Color.FromArgb(90, 60, 160); c.Vape = false; break;
        }
    }

    void AddCostumeDialog(List<Func<bool>> s, Cat f)
    {
        char col = ItemColors[rnd.Next(ItemColors.Length)];
        switch (f.DialogV)
        {
            case 0: f.DialogV = 6; AddDialog(s, f); return;
            case 1: f.DialogV = 7; AddDialog(s, f); return;
            case 2:
                s.Add(Line(f, "Ты с какого района, кассир?", 45, 'a'));
                s.Add(Line(me, "С вейп-района :)", 35, 'h'));
                s.Add(Line(f, "Ладно... дай жижу. Пацаны не извиняются.", 50, 'n'));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, false);
                s.Add(Line(f, "Бывай.", 25, 'n'));
                break;
            case 3:
                s.Add(Line(f, "Мне нужна твоя жижа, твой вейп и твой испаритель.", 60, 'n'));
                s.Add(Line(me, "Э-э... может, просто купите?", 40, 'w'));
                s.Add(Line(f, "...Хорошо.", 30, 'n'));
                AddFetch(s, 'K'); AddScan(s, 'K'); AddHandOver(s, f, 'K', false);
                s.Add(Line(f, "Я вернусь.", 35, 'n'));
                break;
            case 4:
                s.Add(Line(f, "Да пребудет с тобой Сила.", 45, 'n'));
                s.Add(Line(me, "И с вами... Вам что-нибудь подсказать?", 45, 'h'));
                s.Add(Do(delegate { f.HappyT = 40; }));
                s.Add(Line(f, "Это не та жижа, что ты ищешь. Дай мятную.", 55, 'n'));
                s.Add(Do(delegate { me.BlinkT = 40; }));
                s.Add(Line(me, "...Это не та жижа. Дам мятную.", 45, 'n'));
                AddFetch(s, 'N'); AddScan(s, 'N'); AddHandOver(s, f, 'N', false);
                break;
            case 5:
                s.Add(Line(f, "Элементарно! Вы курите клубнику и не завтракали.", 55, 'n'));
                s.Add(Line(me, "...Как вы узнали?!", 35, 'w'));
                s.Add(Line(f, "Дедукция. И крошки на фартуке.", 45, 'h'));
                AddFetch(s, 'R'); AddScan(s, 'R'); AddHandOver(s, f, 'R', false);
                break;
            case 6:
                s.Add(Line(f, "Здарова, кассир. Тут аномалия рядом — цены видал?", 55, 'n'));
                s.Add(Line(me, "Цены как цены :)", 30, 'h'));
                s.Add(Line(f, "Ладно. Дай что-нибудь от радиации. Мятное.", 50, 'n'));
                AddFetch(s, 'N'); AddScan(s, 'N'); AddHandOver(s, f, 'N', false);
                s.Add(Line(f, "Удачной охоты, кассир.", 35, 'n'));
                break;
            case 7:
                s.Add(Line(f, "Йо-хо-хо! Где тут ром?", 45, 'h'));
                s.Add(Line(me, "Рома нет. Есть жижа со вкусом рома!", 45, 'h'));
                s.Add(Line(f, "Тысяча чертей! Беру!", 35, 'h'));
                AddFetch(s, 'B'); AddScan(s, 'B'); AddHandOver(s, f, 'B', false);
                break;
            case 8:
                s.Add(Line(f, "Этот город слишком мал для нас двоих, кассир.", 55, 'a'));
                s.Add(Line(me, "Так я и не уезжаю...", 35, 'w'));
                s.Add(Line(f, "...Тогда дай жижу. Со вкусом виски.", 45, 'n'));
                AddFetch(s, 'Y'); AddScan(s, 'Y'); AddHandOver(s, f, 'Y', false);
                s.Add(Line(f, "Йи-ха!", 25, 'h'));
                break;
            case 9:
                s.Add(Line(f, "Минздрав предупреждает!", 45, 'a'));
                s.Add(Line(me, "Ой... а вы за чем?", 35, 'w'));
                s.Add(Line(f, "...Мне мятную. Только никому ни слова.", 50, 'n'));
                AddFetch(s, 'N'); AddScan(s, 'N'); AddHandOver(s, f, 'N', false);
                break;
            case 10:
                s.Add(Line(f, "Поехали!", 35, 'h'));
                s.Add(Line(me, "Куда?", 25, 'w'));
                s.Add(Line(f, "За жижей! На орбите закончилась.", 45, 'n'));
                AddFetch(s, 'U'); AddScan(s, 'U'); AddHandOver(s, f, 'U', false);
                s.Add(Line(f, "Земля, приём! Задание выполнено.", 40, 'h'));
                break;
            case 11:
                s.Add(Line(f, "Жижу. Взболтать, но не смешивать.", 50, 'n'));
                s.Add(Line(me, "Сделаем, агент.", 30, 'h'));
                AddFetch(s, col);
                s.Add(Do(delegate { me.Shake = 35; }));
                s.Add(Wait(35));
                AddScan(s, col); AddHandOver(s, f, col, false);
                s.Add(Line(f, "Превосходно.", 30, 'n'));
                break;
            case 12:
                s.Add(Line(f, "У самурая нет цели, только путь.", 50, 'n'));
                s.Add(Line(me, "Путь к кассе — вот здесь :)", 35, 'h'));
                s.Add(Line(f, "Мудро.", 25, 'n'));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, false);
                break;
            case 13:
                s.Add(Line(f, "Не бойтесь, граждане! Я спасу этот магазин!", 50, 'h'));
                s.Add(Line(me, "От чего?", 25, 'w'));
                s.Add(Line(f, "От лишней жижи! Беру три!", 40, 'h'));
                AddFetch(s, col); AddScan(s, col); AddHandOver(s, f, col, false);
                s.Add(Do(delegate { f.Speed = 3.6f; }));
                s.Add(Line(f, "Вперёд, навстречу подвигам!", 25, 'h'));
                break;
            default:
                s.Add(Line(f, "Ты волшебник, кассир!", 40, 'h'));
                s.Add(Line(me, "Я просто пробиваю товар...", 40, 'w'));
                s.Add(Line(f, "Тогда наколдуй мне арбузную.", 40, 'n'));
                s.Add(Do(delegate { Stars(Local(me, 1, -24)); Stars(Local(me, 3, -14)); me.HappyT = 40; }));
                AddFetch(s, 'R'); AddScan(s, 'R'); AddHandOver(s, f, 'R', false);
                s.Add(Line(f, "Магия!", 25, 'h'));
                break;
        }
        s.Add(LeaveShop(f));
    }

    void DrawCape(Graphics g, Cat c)
    {
        float sw = (float)Math.Sin((tick + c.Id * 13) * 0.15) * 0.8f + (c.Walking ? 1.2f : 0);
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddBezier(-1.8f, -12.8f, -4f, -9f, -6.5f - sw, -5f, -7.5f - sw, -1.2f);
            p.AddLine(-7.5f - sw, -1.2f, -1.2f - sw * 0.4f, -0.8f);
            p.AddBezier(-1.2f - sw * 0.4f, -0.8f, -0.6f, -5f, 1.2f, -9f, 2.0f, -12.8f);
            p.CloseFigure();
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-8.5f, -13f, 11f, 12.5f), Color.FromArgb(230, 40, 50), Color.FromArgb(140, 20, 30), 0f)) g.FillPath(b, p);
            using (Pen pp = new Pen(Color.FromArgb(90, 10, 20), 0.35f)) g.DrawPath(pp, p);
        }
    }

    void DrawCostumeTorso(Graphics g, Cat c, int o, Color c1, Color c2, Pen op)
    {
        Func<float, float, float, float, GraphicsPath> body = delegate (float x, float y, float w, float h) { return RoundRect(x, y, w, h, 3.0f); };
        if (o == 14) // клетчатая накидка сыщика
        {
            using (GraphicsPath p = body(-4.1f, -12.9f, 8.2f, 8.4f)) { using (SolidBrush b = new SolidBrush(c1)) g.FillPath(b, p); g.DrawPath(op, p); }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { P(-4.4f, -13.4f), P(4.6f, -13.4f), P(5.6f, -8.4f), P(-5.4f, -8.4f) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-5.5f, -13.5f, 11.2f, 5.2f), Light(c1, 0.1f), Dark(c1, 0.82f), 90f)) g.FillPath(b, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (Pen cp = new Pen(Color.FromArgb(90, 80, 50, 30), 0.25f))
                {
                    for (int i = 0; i < 9; i++) g.DrawLine(cp, -5.4f + i * 1.3f, -13.4f, -5.4f + i * 1.3f, -8.4f);
                    for (int i = 0; i < 5; i++) g.DrawLine(cp, -5.6f, -13f + i * 1.2f, 5.8f, -13f + i * 1.2f);
                }
                g.Restore(cs);
                g.DrawPath(op, p);
            }
        }
        else if (o == 15) // куртка сталкера
        {
            using (GraphicsPath p = body(-4.2f, -13.0f, 8.4f, 8.8f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.3f, -13.1f, 8.6f, 9f), Dark(c1, 0.8f), Light(c1, 0.1f), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
            }
            using (SolidBrush b = new SolidBrush(Dark(c1, 0.8f))) { g.FillRectangle(b, -3.2f, -9.6f, 2.4f, 2.0f); g.FillRectangle(b, 1.0f, -9.6f, 2.4f, 2.0f); }
            using (Pen zp = new Pen(Color.FromArgb(170, 170, 160), 0.22f)) g.DrawLine(zp, 0.2f, -12.8f, 0.2f, -4.6f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 60, 50))) g.FillEllipse(b, 2.2f, -11.8f, 0.9f, 0.9f);
        }
        else if (o == 16) // тельняшка пирата
        {
            using (GraphicsPath p = body(-4.1f, -12.9f, 8.2f, 8.3f))
            {
                using (SolidBrush b = new SolidBrush(c1)) g.FillPath(b, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 60, 140))) for (int i = 0; i < 6; i++) g.FillRectangle(b, -4.2f, -12.4f + i * 1.45f, 8.4f, 0.65f);
                g.Restore(cs);
                g.DrawPath(op, p);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 40, 40))) g.FillRectangle(b, -4.0f, -6.0f, 8.0f, 1.0f);
        }
        else if (o == 17) // жилетка ковбоя
        {
            using (GraphicsPath p = body(-4.1f, -12.9f, 8.2f, 8.3f)) { using (SolidBrush b = new SolidBrush(c1)) g.FillPath(b, p); g.DrawPath(op, p); }
            Color vest = Color.FromArgb(130, 82, 48);
            using (SolidBrush b = new SolidBrush(vest))
            {
                g.FillPolygon(b, new PointF[] { P(-4.1f, -12.4f), P(0.6f, -12.8f), P(0.6f, -5.0f), P(-4.0f, -5.0f) });
                g.FillPolygon(b, new PointF[] { P(2.6f, -12.8f), P(4.1f, -12.4f), P(4.0f, -5.0f), P(2.0f, -5.0f) });
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 200, 50)))
            {
                PointF ctr = P(-1.6f, -10.0f);
                PointF[] star = new PointF[10];
                for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; float r = i % 2 == 0 ? 0.9f : 0.4f; star[i] = P(ctr.X + (float)Math.Cos(a) * r, ctr.Y + (float)Math.Sin(a) * r); }
                g.FillPolygon(b, star);
            }
        }
        else if (o == 18) // халат врача
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { P(-4.0f, -13.0f), P(4.0f, -13.0f), P(4.6f, -8f), P(5.0f, -2.0f), P(-4.6f, -2.0f), P(-4.4f, -8f) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.7f, -13.1f, 9.8f, 11.2f), Color.FromArgb(220, 224, 232), Color.White, 0f)) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(150, 155, 170), 0.3f)) g.DrawPath(pp, p);
            }
            using (SolidBrush b = new SolidBrush(c2)) g.FillPolygon(b, new PointF[] { P(0.8f, -13f), P(3.6f, -13f), P(2.2f, -9.6f) });
            using (Pen sp = new Pen(Color.FromArgb(60, 64, 72), 0.3f)) { g.DrawArc(sp, -0.6f, -13.8f, 5.0f, 5.4f, 20, 150); g.DrawLine(sp, 3.8f, -10.0f, 3.6f, -8.2f); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 175, 185))) g.FillEllipse(b, 3.0f, -8.4f, 1.3f, 1.3f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 90, 200))) g.FillRectangle(b, -2.8f, -10.6f, 0.4f, 1.8f);
        }
        else if (o == 19) // скафандр
        {
            using (GraphicsPath p = body(-4.6f, -13.4f, 9.2f, 9.6f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.7f, -13.5f, 9.4f, 9.8f), Color.FromArgb(205, 208, 216), Color.White, 0f)) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(140, 145, 160), 0.3f)) g.DrawPath(pp, p);
            }
            using (GraphicsPath p = RoundRect(-0.6f, -11.4f, 3.8f, 2.8f, 0.4f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 86, 100))) g.FillPath(b, p);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 60, 60))) g.FillEllipse(b, -0.2f, -11.0f, 0.8f, 0.8f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 200, 90))) g.FillEllipse(b, 0.9f, -11.0f, 0.8f, 0.8f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 140, 240))) g.FillEllipse(b, 2.0f, -11.0f, 0.8f, 0.8f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 50, 50))) g.FillRectangle(b, -4.4f, -7.2f, 8.8f, 0.8f);
        }
        else if (o == 20) // смокинг
        {
            using (GraphicsPath p = body(-4.1f, -12.9f, 8.2f, 8.4f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.2f, -13f, 8.4f, 8.6f), Color.FromArgb(10, 10, 12), Color.FromArgb(50, 50, 58), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
            }
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new PointF[] { P(0.4f, -13f), P(4.0f, -13f), P(2.4f, -8.2f) });
            using (SolidBrush b = new SolidBrush(Color.FromArgb(14, 14, 16)))
            {
                g.FillPolygon(b, new PointF[] { P(1.0f, -12.9f), P(2.2f, -12.4f), P(1.0f, -11.9f) });
                g.FillPolygon(b, new PointF[] { P(3.4f, -12.9f), P(2.2f, -12.4f), P(3.4f, -11.9f) });
                g.FillEllipse(b, 1.9f, -12.7f, 0.6f, 0.6f);
            }
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, new PointF[] { P(-1.6f, -11.2f), P(-0.8f, -11.2f), P(-1.2f, -10.4f) });
        }
        else if (o == 21) // кимоно
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { P(-4.0f, -13.0f), P(4.0f, -13.0f), P(4.6f, -8f), P(4.8f, -3.0f), P(-4.6f, -3.0f), P(-4.4f, -8f) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.7f, -13.1f, 9.6f, 10.2f), Dark(c1, 0.8f), Light(c1, 0.15f), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
            }
            using (Pen wp = new Pen(Color.FromArgb(240, 240, 235), 0.6f)) { g.DrawLine(wp, -0.6f, -13.0f, 2.6f, -8.8f); g.DrawLine(wp, 3.8f, -13.0f, 2.0f, -10.4f); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 40, 50))) g.FillRectangle(b, -4.4f, -8.4f, 9.0f, 1.6f);
        }
        else if (o == 22) // трико супергероя
        {
            using (GraphicsPath p = body(-4.0f, -12.9f, 8.0f, 8.4f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-4.1f, -13f, 8.2f, 8.6f), Dark(c1, 0.75f), Light(c1, 0.15f), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 210, 50))) { g.FillEllipse(b, -0.4f, -11.8f, 3.4f, 3.0f); g.FillRectangle(b, -4.0f, -5.8f, 8.0f, 0.9f); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 40, 50)))
            {
                PointF ctr = P(1.3f, -10.3f);
                PointF[] star = new PointF[10];
                for (int i = 0; i < 10; i++) { double a = -Math.PI / 2 + i * Math.PI / 5; float r = i % 2 == 0 ? 1.2f : 0.5f; star[i] = P(ctr.X + (float)Math.Cos(a) * r, ctr.Y + (float)Math.Sin(a) * r); }
                g.FillPolygon(b, star);
            }
        }
        else if (o == 23) // мантия волшебника
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(new PointF[] { P(-3.8f, -13.0f), P(3.9f, -13.0f), P(4.6f, -8.0f), P(5.6f, -0.8f), P(-5.2f, -0.8f), P(-4.5f, -8.0f) });
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-5.3f, -13.1f, 11f, 12.5f), Dark(c1, 0.75f), Light(c1, 0.15f), 0f)) g.FillPath(b, p);
                g.DrawPath(op, p);
                GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 220, 90)))
                    for (int i = 0; i < 8; i++) { float x = -4.2f + (i * 37 % 90) / 10f, y = -12.2f + (i * 53 % 105) / 10f; g.FillEllipse(b, x, y, 0.6f, 0.6f); }
                g.Restore(cs);
            }
        }
    }

    void DrawCostumeHat(Graphics g, Cat c)
    {
        Color hc = c.HatC;
        switch (c.Hat)
        {
            case 7: // кепка с двумя козырьками
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-4.0f, -24.6f, 10.0f, 7.0f, 180, 180); p.CloseFigure();
                    using (SolidBrush b = new SolidBrush(hc)) g.FillPath(b, p);
                    GraphicsState cs = g.Save(); g.SetClip(p, CombineMode.Intersect);
                    using (Pen cp = new Pen(Color.FromArgb(90, 70, 50, 30), 0.25f)) { for (int i = 0; i < 8; i++) g.DrawLine(cp, -4 + i * 1.4f, -25, -4 + i * 1.4f, -21); for (int i = 0; i < 4; i++) g.DrawLine(cp, -4, -24.4f + i * 1.0f, 6, -24.4f + i * 1.0f); }
                    g.Restore(cs);
                    using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Dark(hc, 0.85f))) { g.FillPolygon(b, new PointF[] { P(5.0f, -21.4f), P(7.6f, -21.0f), P(5.2f, -20.6f) }); g.FillPolygon(b, new PointF[] { P(-3.2f, -21.4f), P(-5.8f, -21.0f), P(-3.4f, -20.6f) }); }
                using (SolidBrush b = new SolidBrush(Dark(hc, 0.6f))) g.FillEllipse(b, 0.5f, -25.0f, 0.8f, 0.6f);
                break;
            case 8: // противогаз + капюшон
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-5.6f, -24.2f, 13.0f, 10.0f, 180, 180); p.AddLine(7.4f, -19.2f, 7.0f, -15.0f); p.AddLine(-5.2f, -15.0f, -5.6f, -19.2f); p.CloseFigure();
                    using (SolidBrush b = new SolidBrush(hc)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (GraphicsPath p = RoundRect(-1.4f, -21.2f, 8.2f, 7.6f, 3.0f))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-1.5f, -21.3f, 8.4f, 7.8f), Color.FromArgb(90, 94, 100), Color.FromArgb(40, 42, 48), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(20, 20, 24), 0.3f)) g.DrawPath(pp, p);
                }
                foreach (float ex in new float[] { 0.2f, 3.6f })
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 30, 34))) g.FillEllipse(b, ex - 0.2f, -20.4f, 2.8f, 2.8f);
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(ex, -20.2f, 2.4f, 2.4f), Color.FromArgb(160, 200, 220), Color.FromArgb(60, 80, 100), 45f)) g.FillEllipse(b, ex, -20.2f, 2.4f, 2.4f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.FillEllipse(b, ex + 0.4f, -19.9f, 0.7f, 0.5f);
                }
                using (GraphicsPath p = RoundRect(3.0f, -16.2f, 3.2f, 3.0f, 0.8f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 74, 64))) g.FillPath(b, p);
                using (Pen pp = new Pen(Color.FromArgb(40, 42, 36), 0.25f)) for (int i = 0; i < 3; i++) g.DrawLine(pp, 3.4f + i * 0.9f, -15.8f, 3.4f + i * 0.9f, -13.6f);
                break;
            case 9: // капюшон монаха
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddArc(-5.8f, -24.6f, 13.4f, 11.0f, 160, 220); p.AddLine(7.2f, -16.0f, 6.0f, -13.0f); p.AddLine(-4.6f, -13.0f, -5.6f, -17.0f); p.CloseFigure();
                    p.AddEllipse(-2.8f, -21.4f, 9.6f, 8.4f);
                    p.FillMode = FillMode.Alternate;
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-6f, -24.7f, 14f, 12f), Light(hc, 0.1f), Dark(hc, 0.75f), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(hc, 0.45f), 0.3f)) g.DrawPath(pp, p);
                }
                using (Pen sp = new Pen(Color.FromArgb(80, 0, 0, 0), 0.5f)) g.DrawArc(sp, -2.8f, -21.4f, 9.6f, 8.4f, 190, 160);
                break;
            case 10: // треуголка + повязка
                using (Pen pp = new Pen(Color.FromArgb(20, 18, 22), 0.3f)) g.DrawLine(pp, -4.6f, -20.8f, 6.6f, -18.6f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(20, 18, 22))) g.FillEllipse(b, 3.6f, -20.4f, 2.6f, 2.4f);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-6.0f, -21.0f), P(-2.0f, -26.6f), P(1.2f, -24.6f), P(4.4f, -26.6f), P(8.2f, -21.0f), P(1.2f, -22.4f) });
                    using (SolidBrush b = new SolidBrush(hc)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Color.FromArgb(230, 190, 70), 0.35f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, 0.6f, -24.8f, 1.2f, 1.0f);
                break;
            case 11: // ковбойская шляпа
                using (SolidBrush b = new SolidBrush(Dark(hc, 0.85f))) g.FillEllipse(b, -7.0f, -22.6f, 16.0f, 2.8f);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddBezier(-3.2f, -21.6f, -3.4f, -26.6f, 5.6f, -26.6f, 5.4f, -21.6f);
                    p.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.5f, -26.7f, 9f, 5.2f), Light(hc, 0.15f), Dark(hc, 0.8f), 90f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 40, 30))) g.FillRectangle(b, -3.2f, -23.0f, 8.6f, 0.9f);
                using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawEllipse(pp, -7.0f, -22.6f, 16.0f, 2.8f);
                break;
            case 12: // шлем-пузырь
                using (SolidBrush b = new SolidBrush(Color.FromArgb(55, 180, 220, 255))) g.FillEllipse(b, -6.6f, -26.4f, 15.2f, 14.6f);
                using (Pen pp = new Pen(Color.FromArgb(200, 210, 225, 240), 0.4f)) g.DrawEllipse(pp, -6.6f, -26.4f, 15.2f, 14.6f);
                using (Pen hp = new Pen(Color.FromArgb(200, 255, 255, 255), 0.6f)) { hp.StartCap = LineCap.Round; hp.EndCap = LineCap.Round; g.DrawArc(hp, -5.4f, -25.2f, 12.8f, 12.2f, 200, 50); }
                using (GraphicsPath p = RoundRect(-5.0f, -13.6f, 12.0f, 2.0f, 0.8f)) using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 156, 170))) g.FillPath(b, p);
                break;
            case 13: // пучок + повязка
                using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 24, 30))) { g.FillEllipse(b, -0.4f, -24.6f, 3.2f, 2.6f); g.FillRectangle(b, 0.6f, -22.6f, 1.2f, 0.9f); }
                using (GraphicsPath p = RoundRect(-4.6f, -21.4f, 11.6f, 1.3f, 0.6f)) using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
                using (SolidBrush b = new SolidBrush(hc)) g.FillEllipse(b, 1.6f, -21.4f, 1.3f, 1.3f);
                using (Pen pp = new Pen(Color.White, 0.5f)) { g.DrawLine(pp, -4.4f, -20.8f, -6.0f, -19.2f); g.DrawLine(pp, -4.4f, -20.8f, -6.4f, -20.4f); }
                break;
            case 14: // маска супергероя
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.FillMode = FillMode.Alternate;
                    p.AddClosedCurve(new PointF[] { P(-1.2f, -20.4f), P(3.0f, -20.8f), P(7.0f, -20.4f), P(6.8f, -17.6f), P(3.0f, -18.0f), P(-1.0f, -17.6f) }, 0.4f);
                    p.AddEllipse(0.2f, -20.0f, 2.0f, 2.0f);
                    p.AddEllipse(3.8f, -20.0f, 2.0f, 2.0f);
                    using (SolidBrush b = new SolidBrush(hc)) g.FillPath(b, p);
                }
                using (Pen pp = new Pen(hc, 0.4f)) { g.DrawLine(pp, -1.2f, -19.4f, -3.4f, -18.4f); g.DrawLine(pp, -1.2f, -19.0f, -3.0f, -17.4f); }
                break;
            case 15: // шляпа волшебника
                using (SolidBrush b = new SolidBrush(Dark(hc, 0.8f))) g.FillEllipse(b, -6.0f, -22.6f, 14.0f, 2.6f);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddPolygon(new PointF[] { P(-3.4f, -21.8f), P(5.6f, -21.8f), P(2.4f, -29.0f), P(-0.6f, -31.4f), P(0.8f, -28.6f) });
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-3.5f, -31.5f, 9.2f, 9.8f), Light(hc, 0.15f), Dark(hc, 0.8f), 0f)) g.FillPath(b, p);
                    using (Pen pp = new Pen(Dark(hc, 0.5f), 0.3f)) g.DrawPath(pp, p);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 220, 90))) { g.FillEllipse(b, 0.4f, -26.2f, 0.8f, 0.8f); g.FillEllipse(b, 2.6f, -24.0f, 0.6f, 0.6f); g.FillEllipse(b, -1.2f, -23.6f, 0.6f, 0.6f); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 220, 90))) g.FillRectangle(b, -3.2f, -22.8f, 8.6f, 0.7f);
                break;
            case 16: // зеркало врача
                using (Pen pp = new Pen(Color.FromArgb(70, 74, 84), 0.5f)) g.DrawArc(pp, -4.6f, -24.6f, 11.6f, 7.0f, 200, 140);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(1.2f, -24.0f, 3.2f, 3.2f), Color.White, Color.FromArgb(160, 170, 190), 45f)) g.FillEllipse(b, 1.2f, -24.0f, 3.2f, 3.2f);
                using (Pen pp = new Pen(Color.FromArgb(120, 125, 140), 0.25f)) g.DrawEllipse(pp, 1.2f, -24.0f, 3.2f, 3.2f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 46))) g.FillEllipse(b, 2.5f, -22.7f, 0.6f, 0.6f);
                break;
        }
    }

    // ================= живая очередь =================
    float counterHP = 100; bool registerDown, registerFlying; float registerX; int sellerReactT, rampageCD = 900;
    static readonly string[] IdleLines = { "♪ ля-ля-ля", "*зевает*", "Хм...", "Интересно, что новенького", "Эх...", "Сейчас бы на море" };
    static readonly string[] AnnoyLines = { "Ну давай уже...", "Касса, работаем!", "Я тут с утра стою!", "Можно побыстрее?", "Сколько можно?!", "Тут вообще очередь движется?" };
    static readonly string[] RageLines = { "ДА СКОЛЬКО МОЖНО?!", "Я СЕЙЧАС ВСЁ РАЗНЕСУ!", "ГДЕ ЖАЛОБНАЯ КНИГА?!", "ЭТО НЕ СЕРВИС, ЭТО ИЗДЕВАТЕЛЬСТВО!", "А-А-А-А!" };
    static readonly string[] SellerCalm = { "Спокойно, всех обслужу!", "Не толкаемся, пожалуйста!", "Тише, тише!", "Сейчас-сейчас!" };

    bool AnyRampage() { foreach (Cat g in guests) if (g.Phase == 4) return true; return false; }

    void SellerSay(string t)
    {
        if (sellerReactT > 0) return;
        sellerReactT = 90;
        me.SweatT = 70;
        Say(t, me.X, CatTop(me) - U(1), 'U');
    }

    void UpdateQueueMood()
    {
        if (rampageCD > 0) rampageCD--;
        List<Cat> queue = new List<Cat>();
        foreach (Cat g in guests) if (g.Phase <= 1) queue.Add(g);
        for (int qi = 0; qi < queue.Count; qi++)
        {
            Cat g = queue[qi];
            if (g.Walking && g.TX > Width) continue;
            g.Patience -= (0.03f + 0.024f * qi) * g.Temper;
            if (g.Patience < 0) g.Patience = 0;
            if (g.Patience < 40 && tick % 16 == g.Id % 16)
            {
                PointF h = Local(g, -2, -24);
                Particle p = new Particle(); p.Kind = 0; p.X = h.X; p.Y = h.Y; p.VX = -0.1f * u; p.VY = -0.25f * u; p.R = U(0.8f); p.Max = 25;
                parts.Add(p);
            }
            if (g.LookT > 0) { g.LookT--; g.Dir = 1; if (g.LookT == 0) g.Dir = -1; }
            if (--g.ActT > 0 || g.Walking) continue;
            float pt = g.Patience;
            if (pt > 65)
            {
                g.ActT = 110 + rnd.Next(150);
                int r = rnd.Next(5);
                if (r == 0) g.LookT = 30;
                else if (r == 1 && g.Hold == ' ') { g.Hold = 'F'; g.HoldCol = 'U'; g.ActT = 80; }
                else if (r == 2 && rnd.Next(2) == 0) Say(IdleLines[rnd.Next(IdleLines.Length)], g.X, CatTop(g) - U(1), 'K');
                else g.BlinkT = 8;
                if (r != 1 && g.Hold == 'F') g.Hold = ' ';
            }
            else if (pt > 40)
            {
                if (g.Hold == 'F') g.Hold = ' ';
                g.ActT = 90 + rnd.Next(110);
                if (rnd.Next(2) == 0) { g.StompT = 22; g.AngryT = 30; }
                else g.SweatT = 40;
                if (rnd.Next(100) < 35) Say(AnnoyLines[rnd.Next(AnnoyLines.Length)], g.X, CatTop(g) - U(1), 'K');
            }
            else if (pt > 15)
            {
                if (g.Hold == 'F') g.Hold = ' ';
                g.ActT = 70 + rnd.Next(80);
                g.AngryT = 70; g.StompT = 30;
                int r = rnd.Next(3);
                if (r == 0 && qi > 0 && queue[qi - 1].Phase == 0) Shove(g, queue[qi - 1]);
                else if (r == 1) ThrowAtRegister(g);
                else Say(AnnoyLines[rnd.Next(AnnoyLines.Length)].ToUpper(), g.X, CatTop(g) - U(1), 'R');
                SellerSay(SellerCalm[rnd.Next(SellerCalm.Length)]);
            }
            else if (!AnyRampage() && rampageCD <= 0)
            {
                rampageCD = 1800;
                g.Phase = 4; g.RageHits = 0; g.ActT = 10; g.Speed = 2.6f;
                Go(g, CounterX + U(30));
                Say(RageLines[rnd.Next(RageLines.Length)], g.X, CatTop(g) - U(1), 'R');
                g.AngryT = 200;
            }
        }
        foreach (Cat g in guests.ToArray()) if (g.Phase == 4) UpdateRampage(g);
    }

    void Shove(Cat pusher, Cat victim)
    {
        pusher.Shake = 10;
        Jump(victim, victim.X - U(4), 0, U(2.5f), 10, null);
        victim.Shake = 25; victim.AngryT = 80; victim.Patience = Math.Max(0, victim.Patience - 18);
        Say("Подвинься!", pusher.X, CatTop(pusher) - U(1), 'R');
        Say("Э! Не толкайся!", victim.X, CatTop(victim) - U(4), 'R');
        if (rnd.Next(2) == 0)
        {
            int a = guests.IndexOf(pusher), b = guests.IndexOf(victim);
            if (a >= 0 && b >= 0) { guests[a] = victim; guests[b] = pusher; pusher.Phase = 0; victim.Phase = 0; }
        }
    }

    void ThrowAtRegister(Cat g)
    {
        PointF pw = Local(g, 4.8f, -14f);
        char kind = rnd.Next(2) == 0 ? 'X' : 'B';
        char col = ItemColors[rnd.Next(ItemColors.Length)];
        g.HappyT = 0;
        Say(rnd.Next(2) == 0 ? "НА, ДЕРЖИ!" : "ЛОВИ!", g.X, CatTop(g) - U(1), 'R');
        if (registerDown) return;
        Fly(kind, col, pw.X, pw.Y, RegPt.X, RegPt.Y + U(2), 14, delegate
        {
            Stars(new PointF(RegPt.X, RegPt.Y + U(2)));
            regFlash = 14; counterHP = Math.Max(0, counterHP - 4); shakeT = 4;
            SellerSay(rnd.Next(2) == 0 ? "Эй! Не кидайтесь!" : "Касса-то тут при чём?!");
        });
    }

    void Chips(float x, float y)
    {
        for (int i = 0; i < 6; i++)
        {
            Particle p = new Particle();
            p.Kind = 1; p.X = x; p.Y = y;
            p.VX = (float)(rnd.NextDouble() - 0.5) * u; p.VY = -(0.4f + (float)rnd.NextDouble() * 0.6f) * u;
            p.R = U(0.4f + (float)rnd.NextDouble() * 0.4f); p.Max = 26; p.Col = rnd.Next(2) == 0 ? 'B' : 'b';
            parts.Add(p);
        }
    }

    void UpdateRampage(Cat g)
    {
        if (g.Walking || g.Jumping) return;
        g.Dir = -1;
        if (--g.ActT > 0) return;
        if (g.RageHits < 5)
        {
            g.ActT = 16;
            g.RageHits++;
            g.StompT = 12; g.Shake = 12; g.AngryT = 120;
            counterHP = Math.Max(0, counterHP - 9);
            shakeT = 5;
            Chips(CounterX + U(22), Ground - U(8 + (float)rnd.NextDouble() * 4));
            if (g.RageHits == 2) SellerSay("Не ломайте прилавок!!!");
            if (g.RageHits % 2 == 1) Say(new string[] { "БАХ!", "ХРЯСЬ!", "ТРЕСЬ!" }[rnd.Next(3)], CounterX + U(18), Ground - U(20), 'R');
            return;
        }
        if (g.RageHits == 5)
        {
            g.RageHits++;
            g.ActT = 40;
            if (!registerDown && !registerFlying && rnd.Next(100) < 40)
            {
                registerFlying = true;
                registerX = Math.Max(U(6), CounterX - U(6 + (float)rnd.NextDouble() * 10));
                Say("А КАССУ Я ЗАБЕРУ... НЕТ, ВЫБРОШУ!", g.X, CatTop(g) - U(1), 'R');
                Fly('G', 'G', RegPt.X, RegPt.Y + U(2), registerX, Ground - U(2.5f), 22, delegate
                {
                    registerFlying = false; registerDown = true; shakeT = 10; Poof(registerX, Ground - U(2));
                    me.SweatT = 120; me.Shake = 25;
                    sellerReactT = 0; SellerSay("Касса! Моя касса!");
                });
            }
            return;
        }
        // развязка
        if (rnd.Next(2) == 0)
        {
            Say("Ноги моей тут не будет!", g.X, CatTop(g) - U(1), 'R');
            g.Phase = 3; g.StompT = 30; g.Speed = 2.4f; Go(g, Width + U(16));
        }
        else
        {
            Say("...Ладно. Извините. Нервы.", g.X, CatTop(g) - U(1), 'K');
            g.AngryT = 0; g.SweatT = 60; g.Patience = 70; g.Temper *= 0.5f; g.Phase = 0; g.Speed = 1.6f;
            guests.Remove(g); guests.Insert(0, g);
        }
    }

    List<Func<bool>> FixRegister()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        float rx = registerX;
        s.Add(GoStep(me, delegate { return Math.Max(U(10), rx + U(6)); }));
        s.Add(Do(delegate { me.Dir = -1; registerDown = false; me.Hold = 'G'; me.HoldCol = 'G'; }));
        s.Add(Line(me, "Ох... ты как, касса?", 25, 'w'));
        s.Add(GoStep(me, delegate { return CashX; }));
        s.Add(Do(delegate { me.Dir = 1; me.Hold = ' '; regFlash = 30; }));
        s.Add(Line(me, "Работает! Вроде...", 25, 'h'));
        return s;
    }

    List<Func<bool>> FixCounter()
    {
        List<Func<bool>> s = new List<Func<bool>>();
        s.Add(GoStep(me, delegate { return CashX; }));
        s.Add(Do(delegate { me.Dir = 1; me.Hold = 'A'; me.HoldCol = 'g'; }));
        for (int i = 0; i < 4; i++)
        {
            s.Add(Do(delegate { me.ItemRot = -40; }));
            s.Add(Wait(5));
            s.Add(Do(delegate { me.ItemRot = 10; counterHP = Math.Min(100, counterHP + 15); Stars(new PointF(CounterX + U(2), Ground - U(10))); Say("Тук!", CounterX + U(4), Ground - U(18), 'K'); }));
            s.Add(Wait(8));
        }
        s.Add(Do(delegate { counterHP = 100; me.Hold = ' '; me.ItemRot = 0; me.HappyT = 40; }));
        s.Add(Line(me, "Как новенький!", 30, 'h'));
        return s;
    }
}
