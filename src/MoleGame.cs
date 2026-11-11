using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pintinho
{
    partial class MainForm
    {
        // Toupeira: bate nas toupeiras que saem dos buracos antes do tempo acabar.
        // Fases: mais buracos, toupeira mais rápida, várias ao mesmo tempo e (da 5 em diante) o
        // coelhinho, que não pode levar martelada (tira 3 segundos).
        class MoleGame : Mini
        {
            public override string Key { get { return "toupeira"; } }
            public override string Title { get { return "Toupeira"; } }
            public override Color Accent { get { return Shapes.Hex(0x8d6e63); } }
            public override string LostText { get { return "Acabou o tempo!"; } }
            public override string Info { get { return hits + "/" + goal + "  ·  " + (int)Math.Ceiling(timeLeft) + "s"; } }

            class Hole
            {
                public RectangleF Cell;
                public float Cx, Cy, Rx, Ry;
                public int State; // 0 escondida, 1 subindo, 2 em cima, 3 descendo, 4 levou martelada
                public float T, Up;
                public bool Bunny;
            }

            class Pop { public float X, Y, Life; public string Text; public Color C; }

            Hole[] holes = new Hole[0];
            readonly List<Pop> pops = new List<Pop>();
            int goal, hits, maxUp;
            float timeLeft, upTime, gap, spawnIn, bunnyChance, infoT;
            Random rnd;
            Bitmap bg;
            Font popFont;

            public override void Start(Random r)
            {
                rnd = r;
                int L = Level;
                int[] counts = { 3, 4, 5, 6, 6, 6, 9, 9, 9, 9, 9, 9 };
                holes = new Hole[counts[L - 1]];
                for (int i = 0; i < holes.Length; i++) holes[i] = new Hole();
                upTime = 2.1f - (L - 1) * 0.13f;
                gap = 1.15f - (L - 1) * 0.055f;
                maxUp = 1 + L / 4;
                bunnyChance = L >= 5 ? 0.15f + (L - 5) * 0.02f : 0;
                goal = 8 + L;
                timeLeft = 40;
                hits = 0;
                spawnIn = 0.8f;
                pops.Clear();
                if (bg != null) { bg.Dispose(); bg = null; }
            }

            int[] Rows()
            {
                switch (holes.Length)
                {
                    case 3: return M.portrait ? new int[] { 1, 1, 1 } : new int[] { 3 };
                    case 4: return new int[] { 2, 2 };
                    case 5: return M.portrait ? new int[] { 2, 1, 2 } : new int[] { 2, 3 };
                    case 6: return M.portrait ? new int[] { 2, 2, 2 } : new int[] { 3, 3 };
                    default: return new int[] { 3, 3, 3 };
                }
            }

            public override void Layout()
            {
                if (popFont != null) popFont.Dispose();
                popFont = M.F(34, FontStyle.Bold);
                int[] rows = Rows();
                float ch = Area.Height / (float)rows.Length;
                int k = 0;
                for (int r = 0; r < rows.Length; r++)
                {
                    float cw = Area.Width / (float)rows[r];
                    for (int c = 0; c < rows[r] && k < holes.Length; c++, k++)
                    {
                        Hole h = holes[k];
                        h.Cell = new RectangleF(Area.X + c * cw, Area.Y + r * ch, cw, ch);
                        h.Rx = Math.Min(cw * 0.34f, ch * 0.4f);
                        h.Ry = h.Rx * 0.3f;
                        h.Cx = h.Cell.X + cw / 2;
                        h.Cy = h.Cell.Y + ch * 0.74f;
                    }
                }
                if (bg != null) bg.Dispose();
                bg = new Bitmap(Math.Max(1, Area.Width), Math.Max(1, Area.Height), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bg))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(M.theme.Surface);
                    using (GraphicsPath p = Shapes.RoundRect(new RectangleF(0, 0, Area.Width, Area.Height), 28 * Sc))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, Area.Width, Area.Height), Shapes.Hex(0x9ccc65), Shapes.Hex(0x7cb342), 90f)) g.FillPath(b, p);
                        g.SetClip(p);
                        using (SolidBrush s = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                            for (int x = -Area.Height; x < Area.Width; x += R(90)) g.FillPolygon(s, new PointF[] { new PointF(x, 0), new PointF(x + R(45), 0), new PointF(x + R(45) + Area.Height, Area.Height), new PointF(x + Area.Height, Area.Height) });
                        Random fr = new Random(5);
                        using (Pen p2 = Shapes.RoundPen(Shapes.Hex(0x558b2f), 3 * Sc))
                            for (int i = 0; i < 40; i++)
                            {
                                float x = (float)fr.NextDouble() * Area.Width, y = (float)fr.NextDouble() * Area.Height;
                                g.DrawLine(p2, x, y, x - R(4), y - R(10));
                                g.DrawLine(p2, x + R(6), y, x + R(8), y - R(12));
                            }
                        g.ResetClip();
                    }
                    foreach (Hole h in holes)
                    {
                        float cx = h.Cx - Area.X, cy = h.Cy - Area.Y;
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x8d6e63))) g.FillEllipse(b, cx - h.Rx * 1.28f, cy - h.Ry * 1.9f, h.Rx * 2.56f, h.Ry * 3.8f);
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x3e2723))) g.FillEllipse(b, cx - h.Rx, cy - h.Ry, h.Rx * 2, h.Ry * 2);
                    }
                }
            }

            float Frac(Hole h)
            {
                switch (h.State)
                {
                    case 1: return Math.Min(1, h.T / 0.18f);
                    case 3: return Math.Max(0, 1 - h.T / 0.2f);
                    case 0: return 0;
                    default: return 1;
                }
            }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (bg == null) return;
                g.DrawImageUnscaled(bg, Area.X, Area.Y);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (Hole h in holes)
                {
                    if (!clip.IntersectsWith(Rectangle.Ceiling(h.Cell))) continue;
                    float f = Frac(h);
                    if (f > 0)
                    {
                        GraphicsState st = g.Save();
                        g.SetClip(new RectangleF(h.Cell.X, Area.Y, h.Cell.Width, h.Cy - Area.Y), CombineMode.Intersect);
                        float mh = h.Rx * 1.55f;
                        DrawMole(g, h.Cx, h.Cy + (1 - f) * mh * 1.05f, h.Rx * 1.25f, mh, h.State == 4, h.Bunny);
                        g.Restore(st);
                    }
                    using (Pen p = Shapes.RoundPen(Shapes.Hex(0x6d4c41), h.Ry * 0.5f))
                        g.DrawArc(p, h.Cx - h.Rx, h.Cy - h.Ry, h.Rx * 2, h.Ry * 2, 5, 170);
                }
                foreach (Pop p in pops)
                {
                    int a = (int)(255 * Math.Min(1, p.Life * 2.5f));
                    RectangleF r = new RectangleF(p.X - R(150), p.Y - R(30), R(300), R(60));
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, Color.White))) { r.Offset(R(2), R(2)); g.DrawString(p.Text, popFont, b, r, CenterFmt); r.Offset(-R(2), -R(2)); }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, p.C))) g.DrawString(p.Text, popFont, b, r, CenterFmt);
                }
            }

            // Toupeira (ou coelhinho) com a base do corpo em baseY; o buraco esconde o que fica abaixo.
            public static void DrawMole(Graphics g, float cx, float baseY, float w, float h, bool hit, bool bunny)
            {
                float lw = Math.Max(1.5f, w * 0.035f);
                Color body = bunny ? Shapes.Hex(0xeceff1) : Shapes.Hex(0x8d6e63), belly = bunny ? Color.White : Shapes.Hex(0xd7ccc8);
                float top = baseY - h;
                using (Pen ink = new Pen(FInk, lw))
                {
                    if (bunny)
                        for (int s = -1; s <= 1; s += 2)
                        {
                            RectangleF ear = new RectangleF(cx + s * w * 0.2f - w * 0.12f, top - h * 0.55f, w * 0.24f, h * 0.75f);
                            using (SolidBrush b = new SolidBrush(body)) g.FillEllipse(b, ear);
                            g.DrawEllipse(ink, ear);
                            using (SolidBrush b = new SolidBrush(Shapes.Hex(0xf8bbd0))) g.FillEllipse(b, RectangleF.Inflate(ear, -w * 0.05f, -h * 0.1f));
                        }
                    RectangleF bodyR = new RectangleF(cx - w / 2, top, w, h * 1.5f);
                    using (SolidBrush b = new SolidBrush(body)) g.FillEllipse(b, bodyR);
                    g.DrawEllipse(ink, bodyR);
                    using (SolidBrush b = new SolidBrush(belly)) g.FillEllipse(b, cx - w * 0.3f, top + h * 0.5f, w * 0.6f, h * 0.9f);

                    float ey = top + h * 0.3f, ex = w * 0.17f, er = w * 0.075f;
                    if (hit)
                        using (Pen p = Shapes.RoundPen(FInk, lw * 1.2f))
                            for (int s = -1; s <= 1; s += 2)
                            {
                                g.DrawLine(p, cx + s * ex - er, ey - er, cx + s * ex + er, ey + er);
                                g.DrawLine(p, cx + s * ex - er, ey + er, cx + s * ex + er, ey - er);
                            }
                    else
                        for (int s = -1; s <= 1; s += 2)
                        {
                            using (SolidBrush b = new SolidBrush(FInk)) g.FillEllipse(b, cx + s * ex - er, ey - er, er * 2, er * 2);
                            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, cx + s * ex - er * 0.2f, ey - er * 0.7f, er * 0.7f, er * 0.7f);
                        }
                    using (SolidBrush b = new SolidBrush(Shapes.Hex(0xf48fb1))) g.FillEllipse(b, cx - w * 0.12f, top + h * 0.42f, w * 0.24f, w * 0.15f);
                    g.DrawEllipse(ink, cx - w * 0.12f, top + h * 0.42f, w * 0.24f, w * 0.15f);
                    RectangleF teeth = new RectangleF(cx - w * 0.08f, top + h * 0.42f + w * 0.15f, w * 0.16f, h * 0.12f);
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, teeth);
                    g.DrawRectangle(ink, teeth.X, teeth.Y, teeth.Width, teeth.Height);
                    g.DrawLine(ink, cx, teeth.Y, cx, teeth.Bottom);
                    using (Pen p = Shapes.RoundPen(Color.FromArgb(150, FInk), lw * 0.6f))
                        for (int s = -1; s <= 1; s += 2)
                        {
                            g.DrawLine(p, cx + s * w * 0.14f, top + h * 0.5f, cx + s * w * 0.42f, top + h * 0.44f);
                            g.DrawLine(p, cx + s * w * 0.14f, top + h * 0.54f, cx + s * w * 0.42f, top + h * 0.58f);
                        }
                }
                if (hit)
                    for (int i = 0; i < 3; i++)
                    {
                        float sx = cx + (i - 1) * w * 0.32f, sy = top - h * 0.12f - (i == 1 ? h * 0.1f : 0);
                        using (GraphicsPath st = Shapes.Star(sx, sy, w * 0.11f, w * 0.05f, 5))
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffd54f)))
                        using (Pen p = new Pen(FInk, lw * 0.7f)) { g.FillPath(b, st); g.DrawPath(p, st); }
                    }
            }

            public override void Tick(float dt)
            {
                timeLeft -= dt;
                if (timeLeft <= 0)
                {
                    timeLeft = 0;
                    Lost = true;
                    M.InvalidateMiniHeader();
                    return;
                }
                infoT += dt;
                if (infoT > 0.25f) { infoT = 0; M.InvalidateMiniHeader(); }

                int up = 0;
                foreach (Hole h in holes) if (h.State != 0) up++;
                spawnIn -= dt;
                if (spawnIn <= 0 && up < maxUp)
                {
                    for (int tries = 0; tries < 10; tries++)
                    {
                        Hole h = holes[rnd.Next(holes.Length)];
                        if (h.State != 0) continue;
                        h.State = 1;
                        h.T = 0;
                        h.Bunny = rnd.NextDouble() < bunnyChance;
                        h.Up = upTime * (0.8f + (float)rnd.NextDouble() * 0.4f);
                        break;
                    }
                    spawnIn = gap * (0.7f + (float)rnd.NextDouble() * 0.6f);
                }
                foreach (Hole h in holes)
                {
                    if (h.State == 0) continue;
                    h.T += dt;
                    if (h.State == 1 && h.T >= 0.18f) { h.State = 2; h.T = 0; }
                    else if (h.State == 2 && h.T >= h.Up) { h.State = 3; h.T = 0; }
                    else if (h.State == 4 && h.T >= 0.5f) { h.State = 3; h.T = 0; }
                    else if (h.State == 3 && h.T >= 0.2f) h.State = 0;
                    Repaint(Rectangle.Ceiling(h.Cell));
                }
                for (int i = pops.Count - 1; i >= 0; i--)
                {
                    Pop p = pops[i];
                    Repaint(new Rectangle((int)p.X - R(160), (int)p.Y - R(40), R(320), R(80)));
                    p.Life -= dt;
                    p.Y -= R(40) * dt;
                    if (p.Life <= 0) pops.RemoveAt(i);
                }
            }

            public override void Down(Contact c, Point p)
            {
                foreach (Hole h in holes)
                {
                    if ((h.State != 1 && h.State != 2) || !h.Cell.Contains(p)) continue;
                    Pop pop = new Pop();
                    pop.X = h.Cx;
                    pop.Y = h.Cy - h.Rx * 2;
                    pop.Life = 0.7f;
                    if (h.Bunny)
                    {
                        timeLeft = Math.Max(0, timeLeft - 3);
                        h.State = 3;
                        h.T = 0;
                        pop.Text = "Ops! -3s";
                        pop.C = M.theme.Bombeiro;
                    }
                    else
                    {
                        hits++;
                        h.State = 4;
                        h.T = 0;
                        pop.Text = hits % 5 == 0 ? "Muito bem!" : "Pou!";
                        pop.C = M.theme.Cone;
                        if (hits >= goal) Won = true;
                    }
                    pops.Add(pop);
                    Repaint(Rectangle.Ceiling(h.Cell));
                    M.InvalidateMiniHeader();
                    return;
                }
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x8bc34a))) g.FillRectangle(b, img);
                float cx = img.X + img.Width / 2f, cy = img.Y + img.Height * 0.78f, rx = Math.Min(img.Width * 0.3f, img.Height * 0.42f), ry = rx * 0.3f;
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x8d6e63))) g.FillEllipse(b, cx - rx * 1.28f, cy - ry * 1.9f, rx * 2.56f, ry * 3.8f);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x3e2723))) g.FillEllipse(b, cx - rx, cy - ry, rx * 2, ry * 2);
                GraphicsState s2 = g.Save();
                g.SetClip(new RectangleF(img.X, img.Y, img.Width, cy - img.Y), CombineMode.Intersect);
                DrawMole(g, cx, cy + rx * 0.1f, rx * 1.25f, rx * 1.55f, true, false);
                g.Restore(s2);
                using (Pen p = Shapes.RoundPen(Shapes.Hex(0x6d4c41), ry * 0.5f)) g.DrawArc(p, cx - rx, cy - ry, rx * 2, ry * 2, 5, 170);
                // martelo
                g.TranslateTransform(cx + rx * 1.15f, cy - rx * 1.5f);
                g.RotateTransform(-35);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffc727))) g.FillRectangle(b, -rx * 0.07f, 0, rx * 0.14f, rx * 0.9f);
                using (GraphicsPath head = Shapes.RoundRect(new RectangleF(-rx * 0.38f, -rx * 0.2f, rx * 0.76f, rx * 0.36f), rx * 0.08f))
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xe53935)))
                using (Pen p = new Pen(FInk, Math.Max(1.5f, rx * 0.04f))) { g.FillPath(b, head); g.DrawPath(p, head); }
                g.Restore(st);
            }
        }
    }
}
