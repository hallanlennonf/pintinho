using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pintinho
{
    partial class MainForm
    {
        // Memória (2 a 4 anos para começar, depois fica boa para adulto também): cartas viradas;
        // vira duas, se forem iguais ficam abertas. Nunca perde. Fases: de 2 pares (4 cartas) até
        // 10 pares (20 cartas); da 12 em diante as figuras ficam parecidas (mesma forma com outra
        // cor, ou mesma cor com outra forma).
        class MemoryGame : Mini
        {
            public override string Key { get { return "memoria"; } }
            public override string Title { get { return "Memória"; } }
            public override Color Accent { get { return Shapes.Hex(0x7e57c2); } }
            public override int Levels { get { return 20; } }
            public override string Info { get { return found + "/" + pairs; } }

            static readonly Color[] Cols = {
                Shapes.Hex(0xe53935), Shapes.Hex(0x1e88e5), Shapes.Hex(0xfdd835), Shapes.Hex(0x43a047),
                Shapes.Hex(0x8e24aa), Shapes.Hex(0xfb8c00), Shapes.Hex(0xec407a), Shapes.Hex(0x00acc1)
            };

            class Card
            {
                public int Face;          // índice da figura (dois cartões com o mesmo)
                public int Kind, Col;
                public RectangleF R;
                public float Flip;        // 0 = virada para baixo, 1 = aberta
                public bool Open, Done;
                public float Pop;
            }

            Card[] cards = new Card[0];
            int pairs, found, cols, rows;
            readonly List<Card> open = new List<Card>();
            float closeIn;
            Random rnd;
            Bitmap bg;
            readonly Sparks sparks = new Sparks();

            public override void Start(Random r)
            {
                rnd = r;
                int L = Level;
                pairs = Math.Min(10, 2 + L * 8 / 20);
                // figuras: (forma, cor). Fácil: formas e cores todas diferentes. Difícil: repete forma ou cor.
                List<int[]> faces = new List<int[]>();
                if (L < 12)
                {
                    int[] ks = { 0, 1, 2, 3, 4 };
                    List<int> colsLeft = new List<int>();
                    for (int i = 0; i < Cols.Length; i++) colsLeft.Add(i);
                    for (int i = 0; i < pairs; i++)
                    {
                        int ci = rnd.Next(colsLeft.Count);
                        faces.Add(new int[] { ks[i % 5], colsLeft[ci] });
                        colsLeft.RemoveAt(ci);
                        if (colsLeft.Count == 0) for (int k = 0; k < Cols.Length; k++) colsLeft.Add(k);
                    }
                }
                else
                {
                    // poucas formas e poucas cores misturadas: precisa olhar com atenção
                    int nk = 3, nc = (pairs + nk - 1) / nk + 1;
                    List<int[]> all = new List<int[]>();
                    int k0 = rnd.Next(5), c0 = rnd.Next(Cols.Length);
                    for (int a = 0; a < nk; a++)
                        for (int b = 0; b < nc; b++) all.Add(new int[] { (k0 + a) % 5, (c0 + b) % Cols.Length });
                    for (int i = 0; i < pairs; i++)
                    {
                        int j = rnd.Next(all.Count);
                        faces.Add(all[j]);
                        all.RemoveAt(j);
                    }
                }
                List<Card> list = new List<Card>();
                for (int i = 0; i < pairs; i++)
                    for (int t = 0; t < 2; t++)
                    {
                        Card c = new Card();
                        c.Face = i; c.Kind = faces[i][0]; c.Col = faces[i][1];
                        list.Add(c);
                    }
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    Card t = list[i]; list[i] = list[j]; list[j] = t;
                }
                cards = list.ToArray();
                found = 0;
                open.Clear();
                closeIn = 0;
                sparks.Clear();
                if (bg != null) { bg.Dispose(); bg = null; }
            }

            public override void Layout()
            {
                int n = cards.Length;
                float pad = R(20), gap = R(16);
                // escolhe colunas para as cartas ficarem o maior possível (proporção 3:4)
                float best = 0;
                cols = 1; rows = n;
                for (int c = 1; c <= n; c++)
                {
                    int r = (n + c - 1) / c;
                    if (r * c - n >= c) continue;
                    float w = (Area.Width - 2 * pad - (c - 1) * gap) / c, h = (Area.Height - 2 * pad - (r - 1) * gap) / r;
                    float cw = Math.Min(w, h * 0.75f);
                    if (cw > best) { best = cw; cols = c; rows = r; }
                }
                float cardW = Math.Min(best, R(220)), cardH = cardW / 0.75f;
                float totalW = cols * cardW + (cols - 1) * gap, totalH = rows * cardH + (rows - 1) * gap;
                float x0 = Area.X + (Area.Width - totalW) / 2, y0 = Area.Y + (Area.Height - totalH) / 2;
                for (int i = 0; i < n; i++)
                {
                    int r = i / cols, c = i % cols;
                    int inRow = Math.Min(cols, n - r * cols);
                    float rx = x0 + (cols - inRow) * (cardW + gap) / 2;
                    cards[i].R = new RectangleF(rx + c * (cardW + gap), y0 + r * (cardH + gap), cardW, cardH);
                }
                if (bg != null) bg.Dispose();
                bg = new Bitmap(Math.Max(1, Area.Width), Math.Max(1, Area.Height), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bg))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(M.theme.Surface);
                    using (GraphicsPath p = Shapes.RoundRect(new RectangleF(0, 0, Area.Width, Area.Height), 28 * Sc))
                    {
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xede7f6))) g.FillPath(b, p);
                        g.SetClip(p);
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xe1d5f5)))
                            for (int y = 0; y < Area.Height; y += R(70))
                                for (int x = ((y / R(70)) % 2) * R(35); x < Area.Width; x += R(70))
                                    using (GraphicsPath s = Shapes.Star(x, y, R(10), R(4), 5)) g.FillPath(b, s);
                        g.ResetClip();
                    }
                }
            }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (bg == null) return;
                g.DrawImageUnscaled(bg, Area.X, Area.Y);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (Card c in cards) if (clip.IntersectsWith(Rectangle.Ceiling(RectangleF.Inflate(c.R, R(12), R(12))))) DrawCard(g, c);
                sparks.Paint(g);
            }

            void DrawCard(Graphics g, Card c)
            {
                // virar = encolher na horizontal até a metade e abrir do outro lado
                float f = c.Flip;
                bool face = f >= 0.5f;
                float sx = Math.Abs(f - 0.5f) * 2;
                float pop = 1 + c.Pop * 0.08f;
                float w = c.R.Width * sx * pop, h = c.R.Height * pop;
                RectangleF r = new RectangleF(c.R.X + c.R.Width / 2 - w / 2, c.R.Y + c.R.Height / 2 - h / 2, Math.Max(1, w), h);
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(35, 0, 0, 0)))
                using (GraphicsPath p = Shapes.RoundRect(new RectangleF(r.X, r.Y + R(5), r.Width, r.Height), c.R.Width * 0.12f)) g.FillPath(sh, p);
                using (GraphicsPath p = Shapes.RoundRect(r, Math.Min(r.Width / 2, c.R.Width * 0.12f)))
                {
                    if (face)
                    {
                        using (SolidBrush b = new SolidBrush(c.Done ? Shapes.Hex(0xf1f8e9) : Color.White)) g.FillPath(b, p);
                        using (Pen pen = new Pen(c.Done ? M.theme.Trator : Accent, Math.Max(2, 4 * Sc))) g.DrawPath(pen, p);
                        if (sx > 0.15f)
                        {
                            GraphicsState st = g.Save();
                            g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
                            g.ScaleTransform(sx, 1);
                            DrawShape(g, c.Kind, 0, 0, c.R.Width * 0.3f, Cols[c.Col], Math.Max(1.5f, c.R.Width * 0.025f));
                            g.Restore(st);
                        }
                    }
                    else
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X, r.Y, Math.Max(1, r.Width), r.Height), Shapes.Hex(0x9575cd), Shapes.Hex(0x7e57c2), 90f)) g.FillPath(b, p);
                        using (Pen pen = new Pen(Color.White, Math.Max(2, 4 * Sc))) g.DrawPath(pen, p);
                        if (sx > 0.15f)
                        {
                            GraphicsState st = g.Save();
                            g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
                            g.ScaleTransform(sx, 1);
                            using (GraphicsPath s = Shapes.Star(0, 0, c.R.Width * 0.22f, c.R.Width * 0.1f, 5))
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillPath(b, s);
                            g.Restore(st);
                        }
                    }
                }
            }

            public override void Down(Contact c, Point p)
            {
                if (open.Count >= 2) return;
                foreach (Card k in cards)
                {
                    if (k.Done || k.Open || !k.R.Contains(p)) continue;
                    k.Open = true;
                    open.Add(k);
                    if (open.Count == 2)
                    {
                        if (open[0].Face == open[1].Face)
                        {
                            foreach (Card o in open)
                            {
                                o.Done = true;
                                o.Pop = 1;
                                sparks.Burst(o.R.X + o.R.Width / 2, o.R.Y + o.R.Height / 2, 12, R(9), Cols[o.Col]);
                            }
                            open.Clear();
                            found++;
                            M.InvalidateMiniHeader();
                            if (found == pairs) Won = true;
                        }
                        else closeIn = 1.0f;
                    }
                    Repaint(Rectangle.Ceiling(RectangleF.Inflate(k.R, R(12), R(12))));
                    return;
                }
            }

            public override void Tick(float dt)
            {
                bool any = sparks.Tick(dt);
                if (closeIn > 0)
                {
                    closeIn -= dt;
                    if (closeIn <= 0)
                    {
                        foreach (Card k in open) k.Open = false;
                        open.Clear();
                    }
                }
                foreach (Card k in cards)
                {
                    float want = k.Open || k.Done ? 1 : 0;
                    bool moving = false;
                    if (k.Flip != want)
                    {
                        float step = dt * 4.5f;
                        k.Flip = k.Flip < want ? Math.Min(want, k.Flip + step) : Math.Max(want, k.Flip - step);
                        moving = true;
                    }
                    if (k.Pop > 0) { k.Pop = Math.Max(0, k.Pop - dt * 2.5f); moving = true; }
                    if (moving) Repaint(Rectangle.Ceiling(RectangleF.Inflate(k.R, R(14), R(14))));
                }
                if (any) Repaint(Area);
            }

            public override void Demo(Random r)
            {
                // acha um par e deixa uma carta aberta
                for (int i = 0; i < cards.Length; i++)
                    for (int j = i + 1; j < cards.Length; j++)
                        if (cards[i].Face == cards[j].Face && found == 0)
                        {
                            Down(new Contact(), Point.Round(new PointF(cards[i].R.X + 5, cards[i].R.Y + 5)));
                            Down(new Contact(), Point.Round(new PointF(cards[j].R.X + 5, cards[j].R.Y + 5)));
                        }
                foreach (Card k in cards) if (!k.Done) { k.Open = true; open.Add(k); break; }
                for (int i = 0; i < 30; i++) Tick(1 / 30f);
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xede7f6))) g.FillRectangle(b, img);
                float cw = img.Height * 0.36f, ch = cw / 0.75f, gap = cw * 0.15f;
                float x0 = img.X + (img.Width - 3 * cw - 2 * gap) / 2, y0 = img.Y + (img.Height - ch) / 2;
                Card[] demo = new Card[3];
                for (int i = 0; i < 3; i++)
                {
                    demo[i] = new Card();
                    demo[i].R = new RectangleF(x0 + i * (cw + gap), y0, cw, ch);
                    demo[i].Kind = 4; demo[i].Col = 6;
                    demo[i].Flip = i == 1 ? 0 : 1;
                }
                foreach (Card c in demo) DrawCard(g, c);
                g.Restore(st);
            }
        }
    }
}
