using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pintinho
{
    partial class MainForm
    {
        // Bolhas (2 a 4 anos): bolhas de sabão sobem e é só tocar para estourar (vários dedos
        // juntos). Sem tempo e sem perder. Fases 1-5: qualquer bolha vale. 6-12: só as da cor
        // pedida (mostrada no canto); as outras só balançam. 13-20: só as que têm o desenho pedido
        // (estrela, coração...). A cada fase mais bolhas para estourar, menores e mais rápidas.
        class BubbleGame : Mini
        {
            public override string Key { get { return "bolhas"; } }
            public override string Title { get { return "Bolhas"; } }
            public override Color Accent { get { return Shapes.Hex(0x29b6f6); } }
            public override int Levels { get { return 20; } }
            public override string Info { get { return popped + "/" + goal; } }

            static readonly Color[] Cols = {
                Shapes.Hex(0xe53935), Shapes.Hex(0x1e88e5), Shapes.Hex(0xfdd835), Shapes.Hex(0x43a047),
                Shapes.Hex(0x8e24aa), Shapes.Hex(0xfb8c00)
            };

            class Bubble
            {
                public float X, Y, R, Speed, Phase, Wobble;
                public int Col, Kind; // Kind: -1 sem desenho, 0..4 forma
            }

            readonly List<Bubble> bubbles = new List<Bubble>();
            int mode, goal, popped, nCols, target, maxOn;
            float speed, size, spawnIn, time;
            Random rnd;
            Bitmap bg;
            Rectangle badge, progress;
            readonly Sparks sparks = new Sparks();

            public override void Start(Random r)
            {
                rnd = r;
                int L = Level;
                mode = L <= 5 ? 0 : L <= 12 ? 1 : 2;
                goal = 8 + L;
                popped = 0;
                nCols = mode == 1 ? Math.Min(5, 2 + (L - 6) / 2) : 4;
                target = rnd.Next(mode == 2 ? 5 : nCols);
                speed = 70 + L * 6;          // px de design por segundo
                size = 95 - L * 2.2f;        // raio em px de design
                maxOn = 4 + L / 3;
                bubbles.Clear();
                sparks.Clear();
                spawnIn = 0;
                time = 0;
                if (bg != null) { bg.Dispose(); bg = null; }
            }

            public override void Layout()
            {
                int bs = R(110);
                badge = new Rectangle(Area.X + R(18), Area.Y + R(18), bs, bs);
                progress = new Rectangle(Area.X + R(150), Area.Y + R(28), Area.Width - R(170), R(26));
                if (mode == 0) progress = new Rectangle(Area.X + R(24), Area.Y + R(28), Area.Width - R(48), R(26));
                if (bg != null) bg.Dispose();
                bg = new Bitmap(Math.Max(1, Area.Width), Math.Max(1, Area.Height), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bg))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(M.theme.Surface);
                    using (GraphicsPath p = Shapes.RoundRect(new RectangleF(0, 0, Area.Width, Area.Height), 28 * Sc))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, Area.Width, Area.Height), Shapes.Hex(0x81d4fa), Shapes.Hex(0xe1f5fe), 90f)) g.FillPath(b, p);
                        g.SetClip(p);
                        // nuvens e grama
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                        {
                            Random fr = new Random(9);
                            for (int i = 0; i < 5; i++)
                            {
                                float x = (float)fr.NextDouble() * Area.Width, y = R(90) + (float)fr.NextDouble() * Area.Height * 0.4f, s = R(40) + (float)fr.NextDouble() * R(30);
                                g.FillEllipse(b, x - s, y - s * 0.5f, s * 2, s);
                                g.FillEllipse(b, x - s * 0.4f, y - s * 0.9f, s * 1.2f, s * 1.1f);
                                g.FillEllipse(b, x + s * 0.2f, y - s * 0.6f, s * 1.2f, s * 0.9f);
                            }
                        }
                        float gy = Area.Height - R(50);
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x8bc34a))) g.FillRectangle(b, 0, gy, Area.Width, Area.Height - gy);
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x7cb342)))
                            for (float x = 0; x < Area.Width; x += R(40)) g.FillEllipse(b, x, gy - R(14), R(48), R(30));
                        g.ResetClip();
                    }
                }
                foreach (Bubble b in bubbles) b.R = size * Sc;
            }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (bg == null) return;
                g.DrawImageUnscaled(bg, Area.X, Area.Y);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                GraphicsState st = g.Save();
                g.SetClip(Area, CombineMode.Intersect);
                foreach (Bubble b in bubbles) DrawBubble(g, b);
                sparks.Paint(g);
                g.Restore(st);
                // barra de progresso
                using (GraphicsPath p = Shapes.RoundRect(progress, progress.Height / 2f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(170, 255, 255, 255))) g.FillPath(b, p);
                    float f = Math.Min(1, popped / (float)goal);
                    if (f > 0)
                        using (GraphicsPath q = Shapes.RoundRect(new RectangleF(progress.X, progress.Y, Math.Max(progress.Height, progress.Width * f), progress.Height), progress.Height / 2f))
                        using (SolidBrush b = new SolidBrush(Accent)) g.FillPath(b, q);
                    using (Pen pen = new Pen(Color.White, 3 * Sc)) g.DrawPath(pen, p);
                }
                // qual bolha estourar
                if (mode > 0)
                {
                    M.RoundBox(g, badge, 24 * Sc, Color.White, Accent, 4 * Sc);
                    Bubble demo = new Bubble();
                    demo.X = badge.X + badge.Width / 2f; demo.Y = badge.Y + badge.Height / 2f; demo.R = badge.Width * 0.38f;
                    demo.Col = mode == 1 ? target : 1; demo.Kind = mode == 2 ? target : -1;
                    DrawBubble(g, demo);
                }
            }

            Color BubbleColor(Bubble b) { return mode == 2 ? Shapes.Hex(0x4fc3f7) : Cols[b.Col]; }

            void DrawBubble(Graphics g, Bubble b)
            {
                float wob = (float)Math.Sin(b.Wobble * 30) * b.Wobble * b.R * 0.4f;
                float x = b.X + wob, y = b.Y, r = b.R;
                float sq = 1 + (float)Math.Sin(time * 3 + b.Phase) * 0.04f;
                Color c = mode == 0 ? Cols[b.Col] : BubbleColor(b);
                RectangleF e = new RectangleF(x - r * sq, y - r / sq, 2 * r * sq, 2 * r / sq);
                Color fill = Color.FromArgb(mode == 2 ? 110 : 185, (c.R + 255) / 2, (c.G + 255) / 2, (c.B + 255) / 2);
                using (SolidBrush br = new SolidBrush(fill)) g.FillEllipse(br, e);
                if (b.Kind >= 0) DrawShape(g, b.Kind, x, y, r * 0.5f, Cols[b.Col], Math.Max(1.5f, r * 0.05f));
                using (Pen p = new Pen(Color.FromArgb(220, c), Math.Max(2, r * 0.07f))) g.DrawEllipse(p, e);
                using (Pen p = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(2, r * 0.09f)))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    g.DrawArc(p, x - r * 0.68f, y - r * 0.68f, r * 1.36f, r * 1.36f, 200, 50);
                }
                using (SolidBrush br = new SolidBrush(Color.FromArgb(230, 255, 255, 255))) g.FillEllipse(br, x + r * 0.3f, y - r * 0.55f, r * 0.16f, r * 0.16f);
            }

            bool IsTarget(Bubble b)
            {
                if (mode == 0) return true;
                if (mode == 1) return b.Col == target;
                return b.Kind == target;
            }

            void Spawn()
            {
                Bubble b = new Bubble();
                b.R = size * Sc * (0.85f + (float)rnd.NextDouble() * 0.3f);
                b.X = Area.X + b.R + (float)rnd.NextDouble() * (Area.Width - 2 * b.R);
                b.Y = Area.Bottom + b.R;
                b.Speed = speed * Sc * (0.8f + (float)rnd.NextDouble() * 0.4f);
                b.Phase = (float)rnd.NextDouble() * 6.28f;
                // pelo menos ~45% das bolhas são as certas
                int tgt = 0;
                foreach (Bubble o in bubbles) if (IsTarget(o)) tgt++;
                bool want = mode == 0 || tgt * 2 < bubbles.Count + 1 || rnd.NextDouble() < 0.45;
                if (mode == 1)
                {
                    b.Col = want ? target : (target + 1 + rnd.Next(nCols - 1)) % nCols;
                    b.Kind = -1;
                }
                else if (mode == 2)
                {
                    b.Kind = want ? target : (target + 1 + rnd.Next(4)) % 5;
                    b.Col = rnd.Next(Cols.Length);
                }
                else { b.Col = rnd.Next(Cols.Length); b.Kind = -1; }
                bubbles.Add(b);
            }

            public override void Down(Contact c, Point p)
            {
                for (int i = bubbles.Count - 1; i >= 0; i--)
                {
                    Bubble b = bubbles[i];
                    float dx = p.X - b.X, dy = p.Y - b.Y, rr = b.R * 1.15f;
                    if (dx * dx + dy * dy > rr * rr) continue;
                    if (IsTarget(b))
                    {
                        bubbles.RemoveAt(i);
                        popped++;
                        sparks.Burst(b.X, b.Y, 12, b.R * 0.12f, mode == 2 ? Cols[b.Col] : Cols[b.Col]);
                        if (popped >= goal) Won = true;
                        M.InvalidateMiniHeader();
                    }
                    else b.Wobble = 0.5f;
                    Repaint(Area);
                    return;
                }
            }

            public override void Tick(float dt)
            {
                time += dt;
                sparks.Tick(dt);
                if (!Won)
                {
                    spawnIn -= dt;
                    if (spawnIn <= 0 && bubbles.Count < maxOn)
                    {
                        Spawn();
                        spawnIn = 0.55f + (float)rnd.NextDouble() * 0.6f;
                    }
                }
                for (int i = bubbles.Count - 1; i >= 0; i--)
                {
                    Bubble b = bubbles[i];
                    b.Y -= b.Speed * dt;
                    b.X += (float)Math.Sin(time * 1.6f + b.Phase) * b.R * 0.6f * dt;
                    if (b.Wobble > 0) b.Wobble = Math.Max(0, b.Wobble - dt);
                    if (b.Y < Area.Y - b.R) bubbles.RemoveAt(i);
                }
                Repaint(Area);
            }

            public override void Demo(Random r)
            {
                for (int i = 0; i < 400 && bubbles.Count < maxOn; i++) Tick(1 / 30f);
                for (int i = 0; i < 40; i++) Tick(1 / 30f);
                popped = goal / 3;
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                using (LinearGradientBrush b = new LinearGradientBrush(img, Shapes.Hex(0x81d4fa), Shapes.Hex(0xe1f5fe), 90f)) g.FillRectangle(b, img);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x8bc34a))) g.FillRectangle(b, img.X, img.Bottom - img.Height * 0.15f, img.Width, img.Height * 0.15f);
                int oldMode = mode;
                mode = 0;
                float[][] pos = { new float[] { 0.25f, 0.55f, 0.2f, 0 }, new float[] { 0.55f, 0.35f, 0.26f, 1 }, new float[] { 0.8f, 0.62f, 0.15f, 3 }, new float[] { 0.42f, 0.8f, 0.1f, 2 } };
                foreach (float[] q in pos)
                {
                    Bubble b = new Bubble();
                    b.X = img.X + img.Width * q[0]; b.Y = img.Y + img.Height * q[1]; b.R = img.Height * q[2]; b.Col = (int)q[3]; b.Kind = -1;
                    DrawBubble(g, b);
                }
                mode = oldMode;
                g.Restore(st);
            }
        }
    }
}
