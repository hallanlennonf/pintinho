using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pintinho
{
    partial class MainForm
    {
        // 7 Erros: duas cenas quase iguais montadas na hora (frutas, formas, balões, flores, carimbos
        // num campo). Toque na diferença em qualquer uma das duas. Fases: mais objetos, menores, e
        // diferenças mais sutis (girar, tamanho, cor parecida). Fase 1 tem 3 erros, 2-3 têm 5.
        class DiffGame : Mini
        {
            public override string Key { get { return "erros"; } }
            public override string Title { get { return "7 Erros"; } }
            public override Color Accent { get { return Shapes.Hex(0xd81b60); } }
            public override string Info { get { return diffs == null ? null : "Achou " + Found() + "/" + diffs.Count; } }
            public override bool HasHint { get { return true; } }

            const float SW = 400, SH = 300;
            const int OFruit = 0, OShape = 1, OBalloon = 2, OFlower = 3, OStamp = 4;
            static readonly int[] SceneStamps = { 5, 6, 7, 8, 9, 10, 11 };

            class SObj
            {
                public int Type, Sub;
                public Color C;
                public float X, Y, S, Rot;
                public bool Hidden;
                public SObj Copy() { return (SObj)MemberwiseClone(); }
            }

            class Diff { public float X, Y, Rad; public bool Found; }
            class Mark { public float X, Y, Life; }

            List<SObj> sa, sb;
            List<Diff> diffs;
            readonly List<Mark> marks = new List<Mark>();
            Diff hint;
            float hintT;
            Rectangle picA, picB;
            Bitmap bmpA, bmpB;

            int Found()
            {
                int n = 0;
                foreach (Diff d in diffs) if (d.Found) n++;
                return n;
            }

            static SObj RandomObj(Random rnd)
            {
                SObj o = new SObj();
                o.Type = rnd.Next(5);
                switch (o.Type)
                {
                    case OFruit: o.Sub = rnd.Next(7); break;
                    case OShape: o.Sub = rnd.Next(5); break;
                    case OStamp: o.Sub = SceneStamps[rnd.Next(SceneStamps.Length)]; break;
                }
                o.C = Bright[rnd.Next(Bright.Length)];
                o.Rot = o.Type == OFruit ? rnd.Next(-20, 20) : 0;
                return o;
            }

            static bool HasColor(SObj o) { return o.Type != OFruit; }

            static bool CanRotate(SObj o)
            {
                if (o.Type == OBalloon || o.Type == OFlower) return false;
                if (o.Type == OShape) return o.Sub != 0;
                if (o.Type == OStamp) return o.Sub != 7; // sol é redondo
                return o.Sub != Fruits.Coco && o.Sub != Fruits.Kiwi;
            }

            public override void Start(Random rnd)
            {
                int L = Level;
                int count = 8 + (int)(L * 1.2f);
                float size = 34 - L * 1.25f;
                int ndiff = L == 1 ? 3 : L <= 3 ? 5 : 7;
                sa = new List<SObj>();
                for (int i = 0; i < count; i++)
                {
                    for (int t = 0; t < 300; t++)
                    {
                        SObj o = RandomObj(rnd);
                        o.S = size * (0.85f + (float)rnd.NextDouble() * 0.3f);
                        o.X = o.S + 6 + (float)rnd.NextDouble() * (SW - 2 * o.S - 12);
                        o.Y = o.S + 8 + (float)rnd.NextDouble() * (SH - 2 * o.S - 14);
                        if (o.Type == OBalloon) o.Y = Math.Min(o.Y, SH - o.S * 2.2f);
                        bool ok = true;
                        foreach (SObj q in sa)
                        {
                            float dx = q.X - o.X, dy = q.Y - o.Y, min = (q.S + o.S) * 1.08f;
                            if (dx * dx + dy * dy < min * min) { ok = false; break; }
                        }
                        if (ok) { sa.Add(o); break; }
                    }
                }
                sb = new List<SObj>();
                foreach (SObj o in sa) sb.Add(o.Copy());

                List<int> kinds = new List<int> { 0, 1, 4 }; // sumiu, cor, trocou
                if (L >= 4) { kinds.Add(2); kinds.Add(3); } // girou, tamanho
                bool subtle = L >= 9;
                diffs = new List<Diff>();
                List<int> idx = new List<int>();
                for (int i = 0; i < sa.Count; i++) idx.Add(i);
                while (diffs.Count < ndiff && idx.Count > 0)
                {
                    int pick = idx[rnd.Next(idx.Count)];
                    idx.Remove(pick);
                    List<SObj> side = rnd.Next(2) == 0 ? sa : sb;
                    SObj o = side[pick];
                    int kind = kinds[rnd.Next(kinds.Count)];
                    if (kind == 1 && !HasColor(o)) kind = 4;
                    if (kind == 2 && !CanRotate(o)) kind = 3;
                    switch (kind)
                    {
                        case 0: o.Hidden = true; break;
                        case 1:
                            {
                                int ci = Array.IndexOf(Bright, o.C);
                                int step = subtle ? (rnd.Next(2) == 0 ? 1 : -1) : 2 + rnd.Next(Bright.Length - 3);
                                o.C = Bright[((ci < 0 ? 0 : ci) + step + Bright.Length) % Bright.Length];
                            }
                            break;
                        case 2: o.Rot += (subtle ? 35 : 90) * (rnd.Next(2) == 0 ? 1 : -1); break;
                        case 3: o.S *= subtle ? 0.75f : 0.6f; break;
                        default:
                            {
                                SObj n = RandomObj(rnd);
                                while (n.Type == o.Type && n.Sub == o.Sub) n = RandomObj(rnd);
                                n.X = o.X; n.Y = o.Y; n.S = o.S;
                                side[pick] = n;
                            }
                            break;
                    }
                    Diff d = new Diff();
                    d.X = sa[pick].X;
                    d.Y = sa[pick].Y;
                    d.Rad = Math.Max(sa[pick].S * 1.25f, 18);
                    diffs.Add(d);
                }
                marks.Clear();
                hint = null;
                if (bmpA != null) { bmpA.Dispose(); bmpB.Dispose(); bmpA = bmpB = null; }
            }

            public override void Layout()
            {
                if (diffs == null) return;
                int gap = R(24), w, h;
                if (!M.portrait)
                {
                    w = Math.Min((Area.Width - gap) / 2, Area.Height * 4 / 3);
                    h = w * 3 / 4;
                    int x0 = Area.X + (Area.Width - 2 * w - gap) / 2, y0 = Area.Y + (Area.Height - h) / 2;
                    picA = new Rectangle(x0, y0, w, h);
                    picB = new Rectangle(x0 + w + gap, y0, w, h);
                }
                else
                {
                    h = Math.Min((Area.Height - gap) / 2, Area.Width * 3 / 4);
                    w = h * 4 / 3;
                    int x0 = Area.X + (Area.Width - w) / 2, y0 = Area.Y + (Area.Height - 2 * h - gap) / 2;
                    picA = new Rectangle(x0, y0, w, h);
                    picB = new Rectangle(x0, y0 + h + gap, w, h);
                }
                if (bmpA == null || bmpA.Width != w)
                {
                    if (bmpA != null) { bmpA.Dispose(); bmpB.Dispose(); }
                    bmpA = RenderScene(sa, w, h);
                    bmpB = RenderScene(sb, w, h);
                }
            }

            static Bitmap RenderScene(List<SObj> objs, int w, int h)
            {
                Bitmap b = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    float k = w / SW;
                    DrawSceneBg(g, w, h, k);
                    List<SObj> order = new List<SObj>(objs);
                    order.Sort(delegate(SObj p, SObj q) { return p.Y.CompareTo(q.Y); });
                    foreach (SObj o in order)
                    {
                        if (o.Hidden) continue;
                        GraphicsState st = g.Save();
                        g.TranslateTransform(o.X * k, o.Y * k);
                        g.RotateTransform(o.Rot);
                        DrawObj(g, o, o.S * k, Math.Max(1.2f, 2.2f * k));
                        g.Restore(st);
                    }
                }
                return b;
            }

            static void DrawSceneBg(Graphics g, float w, float h, float k)
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, 0, w, h * 0.6f + 1), Shapes.Hex(0x81d4fa), Shapes.Hex(0xe1f5fe), 90f)) g.FillRectangle(b, 0, 0, w, h * 0.6f + 1);
                using (GraphicsPath hill = new GraphicsPath())
                {
                    hill.AddBezier(0, h * 0.58f, w * 0.3f, h * 0.48f, w * 0.65f, h * 0.66f, w, h * 0.54f);
                    hill.AddLine(w, h * 0.54f, w, h);
                    hill.AddLine(w, h, 0, h);
                    hill.CloseFigure();
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, h * 0.45f, w, h * 0.55f + 1), Shapes.Hex(0xaed581), Shapes.Hex(0x7cb342), 90f)) g.FillPath(b, hill);
                }
            }

            static void DrawObj(Graphics g, SObj o, float r, float lw)
            {
                switch (o.Type)
                {
                    case OFruit: DrawFruitWhole(g, o.Sub, r * 0.85f); break;
                    case OShape: DrawShape(g, o.Sub, 0, 0, r * 0.9f, o.C, lw); break;
                    case OBalloon: DrawBalloon(g, 0, -r * 0.35f, r * 0.62f, o.C, lw); break;
                    case OFlower: DrawFlower(g, 0, 0, r * 0.85f, o.C, lw); break;
                    default: Stamps.Draw(g, o.Sub, 0, 0, r * 0.95f, o.C, FInk, lw); break;
                }
            }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (bmpA == null) return;
                Rectangle[] pics = { picA, picB };
                Bitmap[] bmps = { bmpA, bmpB };
                for (int i = 0; i < 2; i++)
                {
                    Rectangle r = pics[i];
                    using (GraphicsPath sh = Shapes.RoundRect(new RectangleF(r.X - R(8), r.Y - R(8) + R(5), r.Width + R(16), r.Height + R(16)), 22 * Sc))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(35, FInk))) g.FillPath(b, sh);
                    M.RoundBox(g, Rectangle.Inflate(r, R(8), R(8)), 22 * Sc, Color.White, Color.White, 0);
                    g.DrawImageUnscaled(bmps[i], r.X, r.Y);
                    float k = r.Width / SW;
                    foreach (Diff d in diffs)
                    {
                        if (d.Found)
                            using (Pen p = new Pen(M.theme.Bombeiro, 4 * Sc))
                                g.DrawEllipse(p, r.X + (d.X - d.Rad) * k, r.Y + (d.Y - d.Rad) * k, d.Rad * 2 * k, d.Rad * 2 * k);
                    }
                    if (hint != null && hintT > 0)
                    {
                        float pulse = 1 + (float)Math.Sin(hintT * 12) * 0.15f, rr = hint.Rad * pulse * 1.2f;
                        using (Pen p = new Pen(M.theme.Taxi, 5 * Sc)) g.DrawEllipse(p, r.X + (hint.X - rr) * k, r.Y + (hint.Y - rr) * k, rr * 2 * k, rr * 2 * k);
                    }
                    foreach (Mark m in marks)
                        using (Pen p = Shapes.RoundPen(Color.FromArgb((int)(255 * Math.Min(1, m.Life * 2)), M.theme.Muted), 5 * Sc))
                        {
                            float x = r.X + m.X * k, y = r.Y + m.Y * k, s = R(14);
                            g.DrawLine(p, x - s, y - s, x + s, y + s);
                            g.DrawLine(p, x - s, y + s, x + s, y - s);
                        }
                }
            }

            public override void Hint()
            {
                foreach (Diff d in diffs)
                    if (!d.Found) { hint = d; hintT = 1.8f; Repaint(Area); return; }
            }

            public override void Tick(float dt)
            {
                bool any = false;
                if (hintT > 0) { hintT -= dt; any = true; }
                for (int i = marks.Count - 1; i >= 0; i--)
                {
                    marks[i].Life -= dt;
                    any = true;
                    if (marks[i].Life <= 0) marks.RemoveAt(i);
                }
                if (any) { Repaint(Rectangle.Inflate(picA, R(10), R(10))); Repaint(Rectangle.Inflate(picB, R(10), R(10))); }
            }

            public override void Down(Contact c, Point p)
            {
                Rectangle r = picA.Contains(p) ? picA : picB.Contains(p) ? picB : Rectangle.Empty;
                if (r.IsEmpty) return;
                float k = r.Width / SW, sx = (p.X - r.X) / k, sy = (p.Y - r.Y) / k;
                Diff best = null;
                float bestD = float.MaxValue;
                foreach (Diff d in diffs)
                {
                    if (d.Found) continue;
                    float dx = d.X - sx, dy = d.Y - sy, dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist <= d.Rad + 8 && dist < bestD) { best = d; bestD = dist; }
                }
                if (best != null)
                {
                    best.Found = true;
                    if (hint == best) hintT = 0;
                    if (Found() == diffs.Count) Won = true;
                    RepaintAll();
                }
                else
                {
                    Mark m = new Mark();
                    m.X = sx;
                    m.Y = sy;
                    m.Life = 0.6f;
                    marks.Add(m);
                }
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                g.FillRectangle(Brushes.White, img);
                int gap = R(8), w = (img.Width - 3 * gap) / 2, h = Math.Min(w * 3 / 4, img.Height - 2 * gap);
                for (int i = 0; i < 2; i++)
                {
                    float x = img.X + gap + i * (w + gap), y = img.Y + (img.Height - h) / 2f, k = w / SW;
                    GraphicsState s2 = g.Save();
                    g.TranslateTransform(x, y);
                    g.SetClip(new RectangleF(0, 0, w, h), CombineMode.Intersect);
                    DrawSceneBg(g, w, h, k);
                    DrawFruitAtScene(g, Fruits.Maca, 110 * k, 190 * k, 50 * k);
                    DrawBalloon(g, 300 * k, 90 * k, 40 * k, i == 0 ? Bright[4] : Bright[0], 1.5f);
                    if (i == 0) DrawFlower(g, 230 * k, 230 * k, 40 * k, Bright[6], 1.5f);
                    if (i == 1)
                        using (Pen p = new Pen(M.theme.Bombeiro, 3 * Sc)) g.DrawEllipse(p, 300 * k - 55 * k, 90 * k - 60 * k, 110 * k, 120 * k);
                    g.Restore(s2);
                }
                g.Restore(st);
            }

            static void DrawFruitAtScene(Graphics g, int kind, float x, float y, float r)
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(x, y);
                DrawFruitWhole(g, kind, r);
                g.Restore(st);
            }
        }
    }
}
