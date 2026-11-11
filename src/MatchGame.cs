using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pintinho
{
    partial class MainForm
    {
        // Ligue os pares (atividade de escola): arraste do cartão da esquerda até o par da direita
        // (ou toque num e depois no outro). Errou: a linha some e os cartões balançam; nada de "perdeu".
        class MatchGame : Mini
        {
            public override string Key { get { return "ligue"; } }
            public override string Title { get { return "Ligue os pares"; } }
            public override Color Accent { get { return Shapes.Hex(0xef6c00); } }
            public override string Info { get { return left == null ? null : done + "/" + left.Length; } }

            // Tipos de cartão
            const int TFruit = 0, TShape = 1, TDots = 2, TText = 3, TFruitShadow = 4, TStamp = 5, TStampShadow = 6, TBlob = 7;

            class Card
            {
                public int Type, A, Pair;
                public Color C;
                public string S;
                public bool Left, Done;
                public RectangleF R;
                public PointF Dot;
                public float Shake;
            }

            Card[] left, right;
            int done;
            Card dragFrom, selected;
            PointF dragTo;
            int dragId = int.MinValue;
            bool moved;
            Font bigFont, midFont;
            readonly Dictionary<int, Bitmap> fruitBmp = new Dictionary<int, Bitmap>();
            static readonly Color[] LinkColors = { Shapes.Hex(0xe53935), Shapes.Hex(0x1e88e5), Shapes.Hex(0x43a047), Shapes.Hex(0x8e24aa), Shapes.Hex(0xfb8c00), Shapes.Hex(0x00acc1) };
            static readonly int[] StampPick = { 0, 1, 4, 5, 6, 7, 9, 10, 11 };

            static Card Make(int type, int a, Color c, string s)
            {
                Card k = new Card();
                k.Type = type;
                k.A = a;
                k.C = c;
                k.S = s;
                return k;
            }

            static List<int> Pick(Random rnd, int count, int max)
            {
                List<int> all = new List<int>();
                for (int i = 0; i < max; i++) all.Add(i);
                List<int> res = new List<int>();
                while (res.Count < count && all.Count > 0)
                {
                    int i = rnd.Next(all.Count);
                    res.Add(all[i]);
                    all.RemoveAt(i);
                }
                return res;
            }

            public override void Start(Random rnd)
            {
                int L = Level;
                int[] pairsBy = { 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6 };
                int n = pairsBy[L - 1];
                List<Card> a = new List<Card>(), b = new List<Card>();
                switch (L)
                {
                    case 1: // frutas iguais
                        foreach (int f in Pick(rnd, n, 7)) { a.Add(Make(TFruit, f, Color.Empty, null)); b.Add(Make(TFruit, f, Color.Empty, null)); }
                        break;
                    case 2: // formas iguais
                        {
                            List<int> cols = Pick(rnd, n, Bright.Length);
                            List<int> shapes = Pick(rnd, n, 5);
                            for (int i = 0; i < n; i++) { a.Add(Make(TShape, shapes[i], Bright[cols[i]], null)); b.Add(Make(TShape, shapes[i], Bright[cols[i]], null)); }
                        }
                        break;
                    case 3: // cor ↔ fruta daquela cor
                        {
                            int[] fr = { Fruits.Maca, Fruits.Laranja, Fruits.Carambola, Fruits.Limao, Fruits.Coco };
                            Color[] cs = { Shapes.Hex(0xe53935), Shapes.Hex(0xfb8c00), Shapes.Hex(0xfdd835), Shapes.Hex(0x7cb342), Shapes.Hex(0x6d4c41) };
                            foreach (int i in Pick(rnd, n, fr.Length)) { a.Add(Make(TBlob, 0, cs[i], null)); b.Add(Make(TFruit, fr[i], Color.Empty, null)); }
                        }
                        break;
                    case 4: // fruta ↔ sombra
                        foreach (int f in Pick(rnd, n, 7)) { a.Add(Make(TFruit, f, Color.Empty, null)); b.Add(Make(TFruitShadow, f, Color.Empty, null)); }
                        break;
                    case 5:
                    case 8:
                    case 11: // número ↔ quantidade
                        {
                            int max = L == 5 ? 4 : L == 8 ? 6 : 9;
                            int c = rnd.Next(Bright.Length);
                            foreach (int i in Pick(rnd, n, max)) { a.Add(Make(TText, i + 1, Color.Empty, (i + 1).ToString())); b.Add(Make(TDots, i + 1, Bright[c], null)); }
                        }
                        break;
                    case 6: // carimbo ↔ sombra
                        foreach (int i in Pick(rnd, n, StampPick.Length)) { Color c = Bright[rnd.Next(Bright.Length)]; a.Add(Make(TStamp, StampPick[i], c, null)); b.Add(Make(TStampShadow, StampPick[i], c, null)); }
                        break;
                    case 7: // mesma forma, cor diferente
                        foreach (int s in Pick(rnd, n, 5))
                        {
                            List<int> cs = Pick(rnd, 2, Bright.Length);
                            a.Add(Make(TShape, s, Bright[cs[0]], null));
                            b.Add(Make(TShape, s, Bright[cs[1]], null));
                        }
                        break;
                    case 9: // maiúscula ↔ minúscula
                        foreach (int i in Pick(rnd, n, 26))
                        {
                            char ch = (char)('A' + i);
                            a.Add(Make(TText, i, Color.Empty, ch.ToString()));
                            b.Add(Make(TText, i, Color.Empty, char.ToLowerInvariant(ch).ToString()));
                        }
                        break;
                    case 10: // fruta ou carimbo ↔ sombra
                        {
                            List<int> fr = Pick(rnd, 3, 7), st = Pick(rnd, n - 3, StampPick.Length);
                            foreach (int f in fr) { a.Add(Make(TFruit, f, Color.Empty, null)); b.Add(Make(TFruitShadow, f, Color.Empty, null)); }
                            foreach (int i in st) { Color c = Bright[rnd.Next(Bright.Length)]; a.Add(Make(TStamp, StampPick[i], c, null)); b.Add(Make(TStampShadow, StampPick[i], c, null)); }
                        }
                        break;
                    default: // continha ↔ resultado
                        {
                            List<int> res = Pick(rnd, n, 9);
                            foreach (int i in res)
                            {
                                int total = i + 2, x = 1 + rnd.Next(total - 1);
                                a.Add(Make(TText, total, Color.Empty, x + "+" + (total - x)));
                                b.Add(Make(TText, total, Color.Empty, total.ToString()));
                            }
                        }
                        break;
                }
                for (int i = 0; i < a.Count; i++) { a[i].Pair = b[i].Pair = i; a[i].Left = true; }
                // embaralha as duas colunas (sem deixar tudo alinhado)
                Shuffle(a, rnd);
                do Shuffle(b, rnd); while (Aligned(a, b));
                left = a.ToArray();
                right = b.ToArray();
                done = 0;
                dragFrom = selected = null;
                dragId = int.MinValue;
            }

            static void Shuffle(List<Card> l, Random rnd)
            {
                for (int i = l.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    Card t = l[i]; l[i] = l[j]; l[j] = t;
                }
            }

            static bool Aligned(List<Card> a, List<Card> b)
            {
                int same = 0;
                for (int i = 0; i < a.Count; i++) if (a[i].Pair == b[i].Pair) same++;
                return same > a.Count / 3;
            }

            public override void Layout()
            {
                if (left == null) return;
                int n = left.Length;
                float gap = R(18);
                float cs = Math.Min(R(160), (Area.Height - (n - 1) * gap) / n);
                float cw = Math.Min(cs * 1.25f, Area.Width * 0.3f);
                float y0 = Area.Y + (Area.Height - n * cs - (n - 1) * gap) / 2;
                float lx = Area.X + Area.Width * (M.portrait ? 0.04f : 0.14f), rx = Area.Right - Area.Width * (M.portrait ? 0.04f : 0.14f) - cw;
                for (int i = 0; i < n; i++)
                {
                    left[i].R = new RectangleF(lx, y0 + i * (cs + gap), cw, cs);
                    left[i].Dot = new PointF(left[i].R.Right + R(22), left[i].R.Y + cs / 2);
                    right[i].R = new RectangleF(rx, y0 + i * (cs + gap), cw, cs);
                    right[i].Dot = new PointF(right[i].R.X - R(22), right[i].R.Y + cs / 2);
                }
                if (bigFont != null) { bigFont.Dispose(); midFont.Dispose(); }
                bigFont = M.F(Math.Min(72, cs / Sc * 0.55f), FontStyle.Bold);
                midFont = M.F(Math.Min(56, cs / Sc * 0.4f), FontStyle.Bold);
                foreach (Bitmap b in fruitBmp.Values) b.Dispose();
                fruitBmp.Clear();
            }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (left == null) return;
                using (GraphicsPath p = Shapes.RoundRect(Area, 28 * Sc))
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
                // linhas de caderno
                using (Pen p = new Pen(Shapes.Hex(0xe3f2fd), Math.Max(1, 2 * Sc)))
                    for (float y = Area.Y + R(40); y < Area.Bottom - R(10); y += R(40)) g.DrawLine(p, Area.X + R(20), y, Area.Right - R(20), y);

                foreach (Card a in left)
                {
                    if (!a.Done) continue;
                    Card b = null;
                    foreach (Card r in right) if (r.Pair == a.Pair) b = r;
                    DrawLink(g, a.Dot, b.Dot, LinkColors[a.Pair % LinkColors.Length], 1);
                }
                if (dragFrom != null && moved) DrawLink(g, dragFrom.Dot, dragTo, M.theme.Muted, 0.7f);

                foreach (Card k in left) DrawCard(g, k);
                foreach (Card k in right) DrawCard(g, k);
            }

            void DrawLink(Graphics g, PointF a, PointF b, Color c, float k)
            {
                float mx = (a.X + b.X) / 2;
                using (Pen p = Shapes.RoundPen(Color.White, 14 * Sc * k)) g.DrawBezier(p, a, new PointF(mx, a.Y), new PointF(mx, b.Y), b);
                using (Pen p = Shapes.RoundPen(c, 8 * Sc * k)) g.DrawBezier(p, a, new PointF(mx, a.Y), new PointF(mx, b.Y), b);
            }

            void DrawCard(Graphics g, Card k)
            {
                RectangleF r = k.R;
                if (k.Shake > 0) r.Offset((float)Math.Sin(k.Shake * 40) * R(8) * k.Shake / 0.4f, 0);
                bool sel = k == selected || k == dragFrom;
                Color border = k.Done ? LinkColors[k.Pair % LinkColors.Length] : sel ? Accent : M.theme.Borda;
                using (GraphicsPath sh = Shapes.RoundRect(new RectangleF(r.X, r.Y + R(5), r.Width, r.Height), 22 * Sc))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(30, FInk))) g.FillPath(b, sh);
                M.RoundBox(g, Rectangle.Round(r), 22 * Sc, k.Done ? Shapes.Hex(0xf1f8e9) : Color.White, border, (sel || k.Done ? 4 : 2.5f) * Sc);
                DrawContent(g, k, r);
                // bolinha de ligar
                float dr = R(12);
                PointF d = k.Dot;
                if (k.Shake > 0) d.X += (float)Math.Sin(k.Shake * 40) * R(8) * k.Shake / 0.4f;
                using (SolidBrush b = new SolidBrush(k.Done ? border : Color.White)) g.FillEllipse(b, d.X - dr, d.Y - dr, dr * 2, dr * 2);
                using (Pen p = new Pen(k.Done ? border : Accent, 3 * Sc)) g.DrawEllipse(p, d.X - dr, d.Y - dr, dr * 2, dr * 2);
            }

            void DrawContent(Graphics g, Card k, RectangleF r)
            {
                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = Math.Min(r.Width, r.Height) * 0.36f, lw = Math.Max(1.5f, 3 * Sc);
                switch (k.Type)
                {
                    case TFruit:
                    case TFruitShadow:
                        {
                            Bitmap bmp = FruitBmp(k.A, s);
                            RectangleF dst = new RectangleF(cx - bmp.Width / 2f, cy - bmp.Height / 2f, bmp.Width, bmp.Height);
                            if (k.Type == TFruit) g.DrawImage(bmp, dst);
                            else
                                using (ImageAttributes ia = new ImageAttributes())
                                {
                                    float v = 0.3f;
                                    ia.SetColorMatrix(new ColorMatrix(new float[][] {
                                        new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                                        new float[] { 0, 0, 0, 1, 0 }, new float[] { v, v, v * 1.1f, 0, 1 } }));
                                    g.DrawImage(bmp, Rectangle.Round(dst), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
                                }
                        }
                        break;
                    case TShape:
                        DrawShape(g, k.A, cx, cy, s * 1.05f, k.C, lw);
                        break;
                    case TBlob:
                        using (GraphicsPath p = new GraphicsPath())
                        {
                            PointF[] pts = new PointF[10];
                            for (int i = 0; i < 10; i++)
                            {
                                double a = i * Math.PI * 2 / 10;
                                float rr = s * (i % 2 == 0 ? 1.05f : 0.85f);
                                pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr);
                            }
                            p.AddClosedCurve(pts, 0.6f);
                            using (SolidBrush b = new SolidBrush(k.C)) g.FillPath(b, p);
                            using (Pen pen = new Pen(FInk, lw)) g.DrawPath(pen, p);
                        }
                        break;
                    case TDots:
                        DrawDots(g, k.A, cx, cy, s, k.C, lw);
                        break;
                    case TText:
                        DrawText(g, k.S, k.S.Length > 2 ? midFont : bigFont, M.theme.Ink, r, CenterFmt);
                        break;
                    case TStamp:
                        Stamps.Draw(g, k.A, cx, cy, s * 1.1f, k.C, FInk, lw);
                        break;
                    case TStampShadow:
                        Stamps.Draw(g, k.A, cx, cy, s * 1.1f, Shapes.Hex(0x4d4d55), Shapes.Hex(0x4d4d55), lw);
                        break;
                }
            }

            static void DrawDots(Graphics g, int n, float cx, float cy, float s, Color c, float lw)
            {
                // posições de dado (3x3)
                int[][] layout = {
                    new int[] { 4 }, new int[] { 0, 8 }, new int[] { 0, 4, 8 }, new int[] { 0, 2, 6, 8 }, new int[] { 0, 2, 4, 6, 8 },
                    new int[] { 0, 2, 3, 5, 6, 8 }, new int[] { 0, 2, 3, 4, 5, 6, 8 }, new int[] { 0, 1, 2, 3, 5, 6, 7, 8 }, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 } };
                float step = s * 0.75f, dr = s * 0.28f;
                foreach (int i in layout[n - 1])
                {
                    float x = cx + (i % 3 - 1) * step, y = cy + (i / 3 - 1) * step;
                    using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x - dr, y - dr, dr * 2, dr * 2);
                    using (Pen p = new Pen(FInk, lw * 0.7f)) g.DrawEllipse(p, x - dr, y - dr, dr * 2, dr * 2);
                }
            }

            Bitmap FruitBmp(int kind, float r)
            {
                Bitmap b;
                if (fruitBmp.TryGetValue(kind, out b)) return b;
                b = M.FruitSprite(r, delegate(Graphics g) { DrawFruitWhole(g, kind, r); });
                fruitBmp[kind] = b;
                return b;
            }

            Card CardAt(PointF p, bool wantLeft)
            {
                Card[] list = wantLeft ? left : right;
                foreach (Card k in list)
                {
                    RectangleF hit = RectangleF.Inflate(k.R, R(10), R(10));
                    hit = RectangleF.Union(hit, new RectangleF(k.Dot.X - R(30), k.Dot.Y - R(30), R(60), R(60)));
                    if (!k.Done && hit.Contains(p)) return k;
                }
                return null;
            }

            public override void Down(Contact c, Point p)
            {
                Card k = CardAt(p, true) ?? CardAt(p, false);
                if (k == null) { if (selected != null) { selected = null; RepaintAll(); } return; }
                if (selected != null && selected.Left != k.Left) { TryMatch(selected, k); selected = null; RepaintAll(); return; }
                dragFrom = k;
                dragTo = p;
                dragId = c.Id;
                moved = false;
                c.Role = Role.Arrastar;
                c.Drag = delegate(Point q)
                {
                    if (dragFrom == null) return;
                    dragTo = q;
                    if (!dragFrom.R.Contains(q)) moved = true;
                    Repaint(Area);
                };
                Repaint(Area);
            }

            public override void Up(int id)
            {
                if (dragFrom == null || id != dragId) return;
                Card from = dragFrom;
                dragFrom = null;
                Card to = CardAt(dragTo, !from.Left);
                if (to != null) { TryMatch(from, to); selected = null; }
                else if (!moved) selected = selected == from ? null : from; // tocou: fica escolhido
                else selected = null;
                RepaintAll();
            }

            void TryMatch(Card a, Card b)
            {
                if (a.Pair == b.Pair)
                {
                    a.Done = b.Done = true;
                    done++;
                    if (done == left.Length) Won = true;
                }
                else
                {
                    a.Shake = b.Shake = 0.4f;
                }
            }

            public override void Tick(float dt)
            {
                bool any = false;
                foreach (Card k in left) if (k.Shake > 0) { k.Shake = Math.Max(0, k.Shake - dt); any = true; }
                foreach (Card k in right) if (k.Shake > 0) { k.Shake = Math.Max(0, k.Shake - dt); any = true; }
                if (any) Repaint(Area);
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                g.FillRectangle(Brushes.White, img);
                using (Pen p = new Pen(Shapes.Hex(0xe3f2fd), 2 * Sc))
                    for (float y = img.Y + R(24); y < img.Bottom; y += R(24)) g.DrawLine(p, img.X, y, img.Right, y);
                float cs = Math.Min(img.Height * 0.36f, img.Width * 0.24f), lw = Math.Max(1.5f, 2.5f * Sc);
                RectangleF[] ls = { new RectangleF(img.X + img.Width * 0.1f, img.Y + img.Height * 0.08f, cs, cs), new RectangleF(img.X + img.Width * 0.1f, img.Bottom - img.Height * 0.08f - cs, cs, cs) };
                RectangleF[] rs = { new RectangleF(img.Right - img.Width * 0.1f - cs, ls[0].Y, cs, cs), new RectangleF(img.Right - img.Width * 0.1f - cs, ls[1].Y, cs, cs) };
                DrawLink(g, new PointF(ls[0].Right, ls[0].Y + cs / 2), new PointF(rs[1].X, rs[1].Y + cs / 2), LinkColors[0], 0.8f);
                DrawLink(g, new PointF(ls[1].Right, ls[1].Y + cs / 2), new PointF(rs[0].X, rs[0].Y + cs / 2), LinkColors[1], 0.8f);
                for (int i = 0; i < 2; i++)
                {
                    M.RoundBox(g, Rectangle.Round(ls[i]), 12 * Sc, Color.White, M.theme.Borda, 2 * Sc);
                    M.RoundBox(g, Rectangle.Round(rs[i]), 12 * Sc, Color.White, M.theme.Borda, 2 * Sc);
                }
                DrawShape(g, 3, ls[0].X + cs / 2, ls[0].Y + cs / 2, cs * 0.34f, Bright[2], lw);
                DrawShape(g, 3, rs[1].X + cs / 2, rs[1].Y + cs / 2, cs * 0.34f, Bright[2], lw);
                DrawDots(g, 3, ls[1].X + cs / 2, ls[1].Y + cs / 2, cs * 0.3f, Bright[4], lw);
                DrawText(g, "3", M.toastFont, M.theme.Ink, rs[0], CenterFmt);
                g.Restore(st);
            }
        }
    }
}
