using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pintinho
{
    partial class MainForm
    {
        // Baldes de cor (2 a 4 anos): arrastar cada bolinha para o balde da mesma cor (ou tocar na
        // bolinha e depois no balde). Errou: ela volta sozinha para o lugar. Nunca perde.
        // Fases: mais cores e mais bolinhas; da 7 em diante vêm formas (estrela, coração...) em vez
        // de bolinhas e a cor continua sendo o que vale; da 14 em diante entram cores parecidas.
        class BucketGame : Mini
        {
            public override string Key { get { return "baldes"; } }
            public override string Title { get { return "Baldes de cor"; } }
            public override Color Accent { get { return Shapes.Hex(0xfb8c00); } }
            public override int Levels { get { return 20; } }
            public override string Info { get { return done + "/" + items.Count; } }

            static readonly Color[] Easy = {
                Shapes.Hex(0xe53935), Shapes.Hex(0x1e88e5), Shapes.Hex(0xfdd835), Shapes.Hex(0x43a047),
                Shapes.Hex(0x8e24aa), Shapes.Hex(0xfb8c00)
            };
            static readonly Color[] Hard = {
                Shapes.Hex(0xe53935), Shapes.Hex(0x1e88e5), Shapes.Hex(0xfdd835), Shapes.Hex(0x43a047),
                Shapes.Hex(0xec407a), Shapes.Hex(0xfb8c00), Shapes.Hex(0x00acc1), Shapes.Hex(0x8e24aa)
            };

            class Item
            {
                public float U, V;      // casa (0..1 dentro da área de cima)
                public float X, Y;      // posição atual na tela
                public int Col, Kind;
                public bool Done;
                public int Bucket = -1; // em que balde caiu
                public float Wobble;
            }

            readonly List<Item> items = new List<Item>();
            Color[] colors;
            int nColors, done;
            Rectangle top, bottom;
            Rectangle[] buckets = new Rectangle[0];
            float itemR;
            Item dragging, selected;
            int dragId;
            Random rnd;
            Bitmap bg;
            readonly Sparks sparks = new Sparks();
            readonly float[] bucketPulse = new float[8];

            public override void Start(Random r)
            {
                rnd = r;
                int L = Level;
                int[] cs = { 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 6, 6, 6, 6 };
                nColors = cs[L - 1];
                Color[] pal = L >= 14 ? Hard : Easy;
                // escolhe as cores da fase
                List<Color> pool = new List<Color>(pal);
                colors = new Color[nColors];
                for (int i = 0; i < nColors; i++)
                {
                    int k = L >= 14 ? 0 : rnd.Next(pool.Count); // difícil: cores vizinhas (parecidas)
                    colors[i] = pool[k];
                    pool.RemoveAt(k);
                }
                if (L >= 14) Shuffle(colors);
                int n = Math.Min(14, 3 + L / 2 + nColors / 2);
                items.Clear();
                // posições numa grade com um pouco de bagunça
                int gc = (int)Math.Ceiling(Math.Sqrt(n * 2.2)), gr = (int)Math.Ceiling(n / (float)gc);
                List<int> cells = new List<int>();
                for (int i = 0; i < gc * gr; i++) cells.Add(i);
                for (int i = 0; i < n; i++)
                {
                    Item it = new Item();
                    it.Col = i < nColors ? i : rnd.Next(nColors); // pelo menos uma de cada cor
                    it.Kind = L >= 7 ? rnd.Next(5) : 0;
                    int c = cells[rnd.Next(cells.Count)];
                    cells.Remove(c);
                    it.U = ((c % gc) + 0.5f + ((float)rnd.NextDouble() - 0.5f) * 0.35f) / gc;
                    it.V = ((c / gc) + 0.5f + ((float)rnd.NextDouble() - 0.5f) * 0.3f) / gr;
                    items.Add(it);
                }
                done = 0;
                dragging = selected = null;
                sparks.Clear();
                if (bg != null) { bg.Dispose(); bg = null; }
            }

            void Shuffle<T>(T[] a)
            {
                for (int i = a.Length - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    T t = a[i]; a[i] = a[j]; a[j] = t;
                }
            }

            public override void Layout()
            {
                int gap = R(20);
                int bw = Math.Min(R(220), (Area.Width - R(40) - (nColors - 1) * gap) / nColors);
                int bh = Math.Min((int)(Area.Height * 0.38f), bw + R(80));
                top = new Rectangle(Area.X + R(20), Area.Y + R(20), Area.Width - R(40), Area.Height - bh - R(30));
                bottom = new Rectangle(Area.X, Area.Bottom - bh, Area.Width, bh);
                buckets = new Rectangle[nColors];
                int bhh = Math.Min(bh - R(70), bw);
                int x0 = bottom.X + (bottom.Width - nColors * bw - (nColors - 1) * gap) / 2;
                for (int i = 0; i < nColors; i++) buckets[i] = new Rectangle(x0 + i * (bw + gap), bottom.Bottom - bhh - R(16), bw, bhh);
                int gc = (int)Math.Ceiling(Math.Sqrt(items.Count * 2.2)), gr = (int)Math.Ceiling(items.Count / (float)gc);
                itemR = Math.Min(R(70), Math.Min(top.Width / (float)gc, top.Height / (float)gr) * 0.36f);
                foreach (Item it in items) Place(it);

                if (bg != null) bg.Dispose();
                bg = new Bitmap(Math.Max(1, Area.Width), Math.Max(1, Area.Height), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bg))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(M.theme.Surface);
                    using (GraphicsPath p = Shapes.RoundRect(new RectangleF(0, 0, Area.Width, Area.Height), 28 * Sc))
                    {
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xfff3e0))) g.FillPath(b, p);
                        g.SetClip(p);
                        // bolinhas de fundo
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffe0b2)))
                            for (int y = 0; y < Area.Height; y += R(60))
                                for (int x = ((y / R(60)) % 2) * R(30); x < Area.Width; x += R(60))
                                    g.FillEllipse(b, x - R(5), y - R(5), R(10), R(10));
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xc8e6c9))) g.FillRectangle(b, 0, bottom.Y - Area.Y + R(30), Area.Width, Area.Height);
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xa5d6a7))) g.FillRectangle(b, 0, bottom.Y - Area.Y + R(30), Area.Width, R(8));
                        g.ResetClip();
                    }
                }
            }

            void Place(Item it)
            {
                if (it.Done)
                {
                    PointF p = InBucket(it);
                    it.X = p.X; it.Y = p.Y;
                }
                else
                {
                    it.X = top.X + itemR + it.U * (top.Width - 2 * itemR);
                    it.Y = top.Y + itemR + it.V * (top.Height - 2 * itemR);
                }
            }

            PointF Home(Item it)
            {
                return new PointF(top.X + itemR + it.U * (top.Width - 2 * itemR), top.Y + itemR + it.V * (top.Height - 2 * itemR));
            }

            // lugar da bolinha dentro do balde (empilhadas na boca do balde)
            PointF InBucket(Item it)
            {
                Rectangle b = buckets[it.Bucket];
                int idx = 0;
                foreach (Item o in items)
                {
                    if (o == it) break;
                    if (o.Done && o.Bucket == it.Bucket) idx++;
                }
                float r = SmallR();
                int per = Math.Max(1, (int)((b.Width * 0.7f) / (2 * r)));
                float x = b.X + b.Width / 2f + ((idx % per) - (per - 1) / 2f) * 2 * r;
                float y = b.Y + b.Height * 0.06f - r * 0.35f - (idx / per) * r * 1.3f;
                return new PointF(x, y);
            }

            float SmallR() { return Math.Min(itemR * 0.7f, buckets.Length > 0 ? buckets[0].Width * 0.16f : itemR); }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (bg == null) return;
                g.DrawImageUnscaled(bg, Area.X, Area.Y);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // baldes (parte de trás), bolinhas guardadas, frente do balde
                for (int i = 0; i < buckets.Length; i++) DrawBucketBack(g, buckets[i], colors[i]);
                foreach (Item it in items) if (it.Done) DrawItem(g, it, SmallR());
                for (int i = 0; i < buckets.Length; i++) DrawBucketFront(g, buckets[i], colors[i], bucketPulse[i]);
                foreach (Item it in items)
                    if (!it.Done && it != dragging)
                    {
                        if (it == selected)
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
                            using (Pen p = new Pen(M.theme.Ink, 4 * Sc))
                            {
                                g.FillEllipse(b, it.X - itemR * 1.25f, it.Y - itemR * 1.25f, itemR * 2.5f, itemR * 2.5f);
                                g.DrawEllipse(p, it.X - itemR * 1.25f, it.Y - itemR * 1.25f, itemR * 2.5f, itemR * 2.5f);
                            }
                        DrawItem(g, it, itemR);
                    }
                if (dragging != null) DrawItem(g, dragging, itemR * 1.15f);
                sparks.Paint(g);
            }

            void DrawItem(Graphics g, Item it, float r)
            {
                float wob = (float)Math.Sin(it.Wobble * 30) * it.Wobble * r * 0.6f;
                Color c = colors[it.Col];
                if (it.Kind == 0)
                {
                    float x = it.X + wob, y = it.Y;
                    using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
                    using (Pen p = new Pen(FInk, Math.Max(1.5f, r * 0.08f))) g.DrawEllipse(p, x - r, y - r, 2 * r, 2 * r);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 255, 255, 255))) g.FillEllipse(b, x - r * 0.55f, y - r * 0.6f, r * 0.5f, r * 0.38f);
                }
                else
                {
                    DrawShape(g, it.Kind, it.X + wob, it.Y, r * 1.1f, c, Math.Max(1.5f, r * 0.08f));
                }
            }

            void DrawBucketBack(Graphics g, Rectangle b, Color c)
            {
                // alça
                using (Pen p = Shapes.RoundPen(Shapes.Hex(0x78909c), Math.Max(2, b.Width * 0.035f)))
                    g.DrawArc(p, b.X + b.Width * 0.12f, b.Y - b.Height * 0.32f, b.Width * 0.76f, b.Height * 0.7f, 200, 140);
                RectangleF rim = new RectangleF(b.X, b.Y, b.Width, b.Height * 0.24f);
                using (SolidBrush br = new SolidBrush(Darker(c, 0.55f))) g.FillEllipse(br, rim);
            }

            void DrawBucketFront(Graphics g, Rectangle b, Color c, float pulse)
            {
                float s = 1 + pulse * 0.08f;
                float cx = b.X + b.Width / 2f;
                float topY = b.Y + b.Height * 0.12f, w = b.Width * s, bw = w * 0.74f, bot = b.Bottom;
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddLine(cx - w / 2, topY, cx + w / 2, topY);
                    p.AddLine(cx + w / 2, topY, cx + bw / 2, bot - b.Height * 0.06f);
                    p.AddArc(cx - bw / 2, bot - b.Height * 0.12f, bw, b.Height * 0.12f, 0, 180);
                    p.CloseFigure();
                    using (SolidBrush br = new SolidBrush(c)) g.FillPath(br, p);
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillRectangle(br, cx - w * 0.3f, topY + b.Height * 0.12f, w * 0.1f, b.Height * 0.55f);
                    using (Pen pen = new Pen(FInk, Math.Max(2, 3.5f * Sc))) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
                }
                using (Pen pen = new Pen(FInk, Math.Max(2, 3.5f * Sc)))
                    g.DrawArc(pen, cx - w / 2, b.Y, w, b.Height * 0.24f, 0, 180);
            }

            static Color Darker(Color c, float f) { return Color.FromArgb((int)(c.R * f), (int)(c.G * f), (int)(c.B * f)); }

            Item ItemAt(Point p)
            {
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    Item it = items[i];
                    if (it.Done) continue;
                    float dx = p.X - it.X, dy = p.Y - it.Y, rr = itemR * 1.3f;
                    if (dx * dx + dy * dy <= rr * rr) return it;
                }
                return null;
            }

            int BucketAt(PointF p)
            {
                for (int i = 0; i < buckets.Length; i++)
                    if (RectangleF.Inflate(buckets[i], R(20), R(40)).Contains(p)) return i;
                return -1;
            }

            public override void Down(Contact c, Point p)
            {
                if (dragging != null) return;
                Item it = ItemAt(p);
                if (it == null)
                {
                    int b = BucketAt(p);
                    if (b >= 0 && selected != null) { Drop(selected, b); selected = null; }
                    else selected = null;
                    RepaintAll();
                    return;
                }
                dragging = it;
                dragId = c.Id;
                float ox = it.X - p.X, oy = it.Y - p.Y;
                moved = false;
                Point start = p;
                c.Role = Role.Arrastar;
                c.Drag = delegate(Point q)
                {
                    if (dragging != it) return;
                    it.X = q.X + ox; it.Y = q.Y + oy;
                    if (Math.Abs(q.X - start.X) + Math.Abs(q.Y - start.Y) > R(20)) moved = true;
                    Repaint(Area);
                };
                RepaintAll();
            }

            bool moved;

            public override void Up(int id)
            {
                if (dragging == null || id != dragId) return;
                Item it = dragging;
                dragging = null;
                int b = BucketAt(new PointF(it.X, it.Y));
                if (moved && b >= 0) { Drop(it, b); selected = null; }
                else if (!moved) selected = selected == it ? null : it; // tocou: fica escolhida
                else selected = null;
                RepaintAll();
            }

            void Drop(Item it, int b)
            {
                if (it.Col == b)
                {
                    it.Done = true;
                    it.Bucket = b;
                    done++;
                    bucketPulse[b] = 1;
                    Place(it);
                    sparks.Burst(buckets[b].X + buckets[b].Width / 2f, buckets[b].Y, 14, R(9), colors[b]);
                    if (done == items.Count)
                    {
                        Won = true;
                        foreach (Rectangle r in buckets) sparks.Burst(r.X + r.Width / 2f, r.Y, 10, R(10), Color.White);
                    }
                }
                else
                {
                    it.Wobble = 0.5f; // volta para casa balançando
                }
            }

            public override void Tick(float dt)
            {
                bool any = sparks.Tick(dt);
                for (int i = 0; i < bucketPulse.Length; i++)
                    if (bucketPulse[i] > 0) { bucketPulse[i] = Math.Max(0, bucketPulse[i] - dt * 3); any = true; }
                foreach (Item it in items)
                {
                    if (it.Done || it == dragging) continue;
                    PointF h = Home(it);
                    float dx = h.X - it.X, dy = h.Y - it.Y;
                    if (Math.Abs(dx) + Math.Abs(dy) > 0.5f)
                    {
                        float k = Math.Min(1, dt * 10);
                        it.X += dx * k; it.Y += dy * k;
                        any = true;
                    }
                    if (it.Wobble > 0) { it.Wobble = Math.Max(0, it.Wobble - dt); any = true; }
                }
                if (any) Repaint(Area);
            }

            public override void Demo(Random r)
            {
                // guarda metade no balde certo e deixa uma escolhida
                int n = items.Count / 2;
                foreach (Item it in items)
                {
                    if (n-- <= 0) break;
                    Drop(it, it.Col);
                }
                foreach (Item it in items) if (!it.Done) { selected = it; break; }
                Won = false;
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xfff3e0))) g.FillRectangle(b, img);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xc8e6c9))) g.FillRectangle(b, img.X, img.Bottom - img.Height * 0.25f, img.Width, img.Height * 0.25f);
                Color[] cs = { Easy[0], Easy[1], Easy[2] };
                float bw = img.Width * 0.26f;
                for (int i = 0; i < 3; i++)
                {
                    Rectangle b = new Rectangle((int)(img.X + img.Width * 0.06f + i * img.Width * 0.31f), (int)(img.Bottom - bw * 0.95f - img.Height * 0.05f), (int)bw, (int)(bw * 0.9f));
                    DrawBucketBack(g, b, cs[i]);
                    DrawBucketFront(g, b, cs[i], 0);
                }
                float r = img.Height * 0.11f;
                float[][] pos = { new float[] { 0.2f, 0.25f, 1 }, new float[] { 0.45f, 0.18f, 0 }, new float[] { 0.7f, 0.28f, 2 }, new float[] { 0.88f, 0.15f, 0 } };
                foreach (float[] q in pos)
                {
                    float x = img.X + img.Width * q[0], y = img.Y + img.Height * q[1];
                    Color c = cs[(int)q[2]];
                    using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
                    using (Pen p = new Pen(FInk, Math.Max(1.5f, 2 * Sc))) g.DrawEllipse(p, x - r, y - r, 2 * r, 2 * r);
                }
                g.Restore(st);
            }
        }
    }
}
