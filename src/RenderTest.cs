using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

// Проверочный прогон без окна: VapePet.exe --render-test <папка>
// Рисует кадры всех сцен двумя движками (gdi_… и skia_…), чтобы сравнить их глазами.
partial class VapePet
{
    public static void RenderTest(string dir)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Directory.CreateDirectory(dir);
        StringBuilder log = new StringBuilder();
        foreach (bool skia in new bool[] { false, true })
        {
            G.UseSkia = skia;
            string tag = skia ? "skia" : "gdi";
            VapePet p = new VapePet(96);
            p.rnd = new Random(7);
            foreach (int m in new int[] { 0, 1, 2, 3, 4, 5, 6, 7 })
            {
                try
                {
                    p.SetMode(m);
                    int last = 0;
                    foreach (int f in (m == 7 ? new int[] { 300, 600, 950, 1250, 1500, 1780, 1950 } : new int[] { 300, 1200 }))
                    {
                        for (; last < f; last++) p.Step();
                        using (Bitmap b = new Bitmap(p.Width, p.Height + 30, PixelFormat.Format32bppPArgb))
                        {
                            using (Graphics bg = Graphics.FromImage(b))
                            using (LinearGradientBrush br = new LinearGradientBrush(new Point(0, 0), new Point(0, b.Height), Color.FromArgb(34, 52, 84), Color.FromArgb(90, 124, 148)))
                                bg.FillRectangle(br, 0, 0, b.Width, b.Height);
                            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                            using (G g = G.On(b)) { p.DrawAll(g); g.ResetTransform(); p.DrawHud(g); }
                            log.AppendLine(tag + " m" + m + " f" + f + ": " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + " мс");
                            b.Save(Path.Combine(dir, tag + "_m" + m + "_" + f.ToString("0000") + ".png"), ImageFormat.Png);
                        }
                    }
                }
                catch (Exception ex) { log.AppendLine(tag + " m" + m + " ОШИБКА: " + ex); }
            }
            // большая панель
            try
            {
                Size s = p.BigSize();
                using (Bitmap b = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics bg = Graphics.FromImage(b)) bg.Clear(Color.FromArgb(235, 238, 242));
                    using (G g = G.On(b)) p.DrawBig(g);
                    b.Save(Path.Combine(dir, tag + "_bigpanel.png"), ImageFormat.Png);
                }
            }
            catch (Exception ex) { log.AppendLine(tag + " bigpanel ОШИБКА: " + ex); }
            // текст: выравнивание, перенос, многоточие, значки
            try
            {
                using (Bitmap b = new Bitmap(520, 260, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics bg = Graphics.FromImage(b)) bg.Clear(Color.White);
                    using (G g = G.On(b))
                    using (Font f1 = new Font("Segoe UI", 11f)) using (Font f2 = new Font("Segoe UI Semibold", 14f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat c = new StringFormat()) using (StringFormat el = new StringFormat())
                    using (Pen pen = new Pen(Color.Red))
                    {
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        g.DrawString("Обычный текст: Привет, котик! 12:30 ₽ € ☂ ♥ 🐾", f1, Brushes.Black, 10, 10);
                        RectangleF r1 = new RectangleF(10, 50, 240, 60); g.DrawRectangle(pen, r1.X, r1.Y, r1.Width, r1.Height);
                        c.Alignment = StringAlignment.Center; c.LineAlignment = StringAlignment.Center;
                        g.DrawString("По центру, с переносом на несколько строк внутри рамки", f1, Brushes.Black, r1, c);
                        RectangleF r2 = new RectangleF(270, 50, 230, 30); g.DrawRectangle(pen, r2.X, r2.Y, r2.Width, r2.Height);
                        el.FormatFlags = StringFormatFlags.NoWrap; el.Trimming = StringTrimming.EllipsisCharacter; el.LineAlignment = StringAlignment.Center;
                        g.DrawString("Очень длинная строка, которая не влезает целиком", f2, Brushes.DarkBlue, r2, el);
                        SizeF ms = g.MeasureString("Измерение ширины", f1); g.DrawRectangle(pen, 10, 130, ms.Width, ms.Height); g.DrawString("Измерение ширины", f1, Brushes.Black, 10, 130);
                        using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(270, 120, 230, 60), Color.Orange, Color.Purple, 45f)) g.FillRectangle(lb, 270, 120, 230, 60);
                        using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(270, 190, 230, 60), Color.White, Color.Navy, 90f))
                        using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(270, 190, 230, 60); g.FillPath(lb, gp); }
                    }
                    b.Save(Path.Combine(dir, tag + "_text.png"), ImageFormat.Png);
                }
            }
            catch (Exception ex) { log.AppendLine(tag + " text ОШИБКА: " + ex); }
        }
        File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString(), Encoding.UTF8);
    }
}
