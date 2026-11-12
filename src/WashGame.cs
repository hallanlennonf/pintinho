using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pintinho
{
    partial class MainForm
    {
        // Lava-jato (2 a 4 anos): o veículo chega sujo de lama. 1) esfregar com a esponja (a lama vira
        // espuma), 2) enxaguar com a mangueira, 3) tocar numa cor para pintar e no ✓ para ele sair
        // brilhando. Sem texto para ler, sem tempo e sem perder: cada etapa termina sozinha quando
        // quase tudo foi limpo. Fases trocam o veículo (6) e da 7 em diante vem mais sujo.
        class WashGame : Mini
        {
            public override string Key { get { return "lavajato"; } }
            public override string Title { get { return "Lava-jato"; } }
            public override Color Accent { get { return Shapes.Hex(0x039be5); } }
            public override string Info { get { return stage == 0 ? "Esfregue!" : stage == 1 ? "Enxágue!" : stage == 2 ? "Pinte!" : "Brilhando!"; } }

            const float UW = 400, UH = 240, Brush = 36, Cell = 16;
            static readonly string[] Names = { "Carro", "Caminhão", "Ônibus", "Bombeiro", "Trator", "Corrida" };
            static readonly int[] DefaultColor = { 0xe53935, 0x1e88e5, 0xfdd835, 0xe53935, 0x43a047, 0x8e24aa };
            static readonly Color[] PaintColors = {
                Shapes.Hex(0xe53935), Shapes.Hex(0xfb8c00), Shapes.Hex(0xfdd835), Shapes.Hex(0x43a047),
                Shapes.Hex(0x1e88e5), Shapes.Hex(0x8e24aa), Shapes.Hex(0xec407a), Shapes.Hex(0x212121)
            };

            class Part { public GraphicsPath Path; public bool Body; public Color C; }
            class Fx { public float X, Y, VX, VY, Life, Size; public int Kind; public Color C; } // 0 gota, 1 bolha, 2 brilho

            int kind, stage; // 0 esfregar, 1 enxaguar, 2 pintar, 3 indo embora
            bool heavy, painted;
            Color body;
            float stageWait, driveT, finishT;
            Random rnd;
            List<Part> parts;
            GraphicsPath silhouette;
            bool[] inside, dirty, foamy, scrubbed, rinsed;
            int cols, rows, dirtyCount, foamCount;
            Bitmap bg, vehBmp, mud, foam;
            Rectangle vRect, bar, checkBtn;
            readonly Rectangle[] colorBtns = new Rectangle[8];
            readonly Rectangle[] stepIcons = new Rectangle[3];
            readonly Dictionary<int, PointF> fingers = new Dictionary<int, PointF>();
            readonly List<Fx> fx = new List<Fx>();

            float K { get { return vRect.Width / UW; } }

            // ---------- veículos (espaço 400x240, chão em y≈225) ----------

            static GraphicsPath RR(float x, float y, float w, float h, float r) { return Shapes.RoundRect(new RectangleF(x, y, w, h), r); }
            static GraphicsPath Circ(float cx, float cy, float r)
            {
                GraphicsPath p = new GraphicsPath();
                p.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
                return p;
            }

            static void Add(List<Part> l, GraphicsPath p, bool bodyPart, int color)
            {
                Part q = new Part();
                q.Path = p;
                q.Body = bodyPart;
                q.C = Shapes.Hex(color);
                l.Add(q);
            }

            static void Wheel(List<Part> l, float cx, float cy, float r, int hub)
            {
                Add(l, Circ(cx, cy, r), false, 0x37474f);
                Add(l, Circ(cx, cy, r * 0.45f), false, hub);
            }

            static List<Part> Build(int kind)
            {
                List<Part> l = new List<Part>();
                const int Glass = 0xb3e5fc;
                switch (kind)
                {
                    case 0: // carro
                        Add(l, RR(105, 55, 190, 80, 34), true, 0);
                        Add(l, RR(40, 108, 320, 78, 24), true, 0);
                        Add(l, RR(122, 70, 72, 42, 12), false, Glass);
                        Add(l, RR(206, 70, 72, 42, 12), false, Glass);
                        Add(l, Circ(346, 132, 10), false, 0xfff59d);
                        Add(l, RR(40, 126, 12, 20, 4), false, 0xff8a80);
                        Wheel(l, 115, 188, 34, 0xcfd8dc);
                        Wheel(l, 285, 188, 34, 0xcfd8dc);
                        break;
                    case 1: // caminhão
                        Add(l, RR(30, 40, 230, 138, 14), true, 0);
                        Add(l, RR(265, 72, 105, 108, 20), true, 0);
                        Add(l, RR(290, 88, 62, 44, 10), false, Glass);
                        Add(l, RR(255, 168, 122, 16, 6), false, 0x90a4ae);
                        Add(l, Circ(362, 150, 8), false, 0xfff59d);
                        Wheel(l, 85, 194, 32, 0xcfd8dc);
                        Wheel(l, 200, 194, 32, 0xcfd8dc);
                        Wheel(l, 318, 194, 32, 0xcfd8dc);
                        break;
                    case 2: // ônibus
                        Add(l, RR(25, 42, 350, 145, 28), true, 0);
                        for (int i = 0; i < 4; i++) Add(l, RR(48 + i * 68, 62, 56, 46, 10), false, Glass);
                        Add(l, RR(326, 66, 34, 100, 8), false, Glass);
                        Add(l, RR(25, 124, 300, 12, 0), false, 0xfafafa);
                        Add(l, Circ(362, 168, 7), false, 0xfff59d);
                        Wheel(l, 95, 194, 32, 0xcfd8dc);
                        Wheel(l, 300, 194, 32, 0xcfd8dc);
                        break;
                    case 3: // bombeiro
                        Add(l, RR(30, 86, 252, 96, 14), true, 0);
                        Add(l, RR(278, 58, 94, 124, 20), true, 0);
                        Add(l, RR(300, 74, 54, 44, 10), false, Glass);
                        Add(l, RR(300, 42, 42, 16, 7), false, 0x42a5f5);
                        Add(l, RR(40, 62, 224, 9, 4), false, 0xb0bec5);
                        Add(l, RR(40, 76, 224, 9, 4), false, 0xb0bec5);
                        for (int i = 0; i < 8; i++) Add(l, RR(50 + i * 28, 62, 6, 23, 2), false, 0xb0bec5);
                        Add(l, Circ(150, 132, 24), false, 0xfdd835);
                        Add(l, Circ(150, 132, 9), false, 0x9e9e9e);
                        Wheel(l, 90, 194, 32, 0xcfd8dc);
                        Wheel(l, 215, 194, 32, 0xcfd8dc);
                        Wheel(l, 326, 194, 32, 0xcfd8dc);
                        break;
                    case 4: // trator
                        Add(l, RR(178, 104, 178, 72, 16), true, 0);
                        Add(l, RR(84, 38, 122, 128, 14), true, 0);
                        Add(l, RR(102, 56, 86, 62, 10), false, Glass);
                        Add(l, RR(300, 56, 13, 52, 4), false, 0x616161);
                        Wheel(l, 130, 168, 62, 0xfdd835);
                        Wheel(l, 316, 190, 36, 0xfdd835);
                        break;
                    default: // carro de corrida
                        Add(l, RR(22, 88, 54, 14, 5), true, 0);
                        Add(l, RR(44, 98, 12, 30, 3), true, 0);
                        Add(l, RR(150, 90, 112, 44, 20), true, 0);
                        Add(l, RR(25, 118, 352, 58, 26), true, 0);
                        Add(l, RR(170, 97, 72, 28, 10), false, Glass);
                        Add(l, Circ(118, 147, 21), false, 0xffffff);
                        Add(l, RR(350, 136, 18, 12, 4), false, 0xfff59d);
                        Wheel(l, 95, 184, 32, 0xcfd8dc);
                        Wheel(l, 305, 184, 32, 0xcfd8dc);
                        break;
                }
                return l;
            }

            static void DrawVehicle(Graphics g, List<Part> parts, Color body, float k)
            {
                float lw = Math.Max(1.5f, 4 * k);
                GraphicsState st = g.Save();
                g.ScaleTransform(k, k);
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(sh, 30, 212, 340, 26);
                using (Pen ink = new Pen(FInk, lw / k))
                {
                    ink.LineJoin = LineJoin.Round;
                    foreach (Part p in parts)
                    {
                        using (SolidBrush b = new SolidBrush(p.Body ? body : p.C)) g.FillPath(b, p.Path);
                        g.DrawPath(ink, p.Path);
                    }
                }
                // brilho na lataria
                using (Pen hl = Shapes.RoundPen(Color.FromArgb(90, 255, 255, 255), 7))
                    foreach (Part p in parts)
                        if (p.Body)
                        {
                            RectangleF b = p.Path.GetBounds();
                            if (b.Width > 60 && b.Height > 30) g.DrawLine(hl, b.X + 18, b.Y + 12, b.X + Math.Min(90, b.Width * 0.4f), b.Y + 12);
                        }
                g.Restore(st);
            }

            // ---------- fase ----------

            public override void Start(Random r)
            {
                rnd = r;
                kind = (Level - 1) % 6;
                heavy = Level > 6;
                body = Shapes.Hex(DefaultColor[kind]);
                parts = Build(kind);
                silhouette = new GraphicsPath(FillMode.Winding);
                foreach (Part p in parts) silhouette.AddPath(p.Path, false);
                cols = (int)Math.Ceiling(UW / Cell);
                rows = (int)Math.Ceiling(UH / Cell);
                inside = new bool[cols * rows];
                for (int i = 0; i < inside.Length; i++) inside[i] = silhouette.IsVisible((i % cols + 0.5f) * Cell, (i / cols + 0.5f) * Cell);
                dirty = new bool[inside.Length];
                foamy = new bool[inside.Length];
                scrubbed = new bool[inside.Length];
                rinsed = new bool[inside.Length];
                stage = 0;
                painted = false;
                stageWait = driveT = finishT = 0;
                fx.Clear();
                fingers.Clear();
                foreach (Bitmap b in new Bitmap[] { mud, foam, vehBmp }) if (b != null) b.Dispose();
                mud = foam = vehBmp = null;
            }

            void MakeMud()
            {
                int w = vRect.Width, h = vRect.Height;
                mud = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                float k = K;
                using (Graphics g = Graphics.FromImage(mud))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (Matrix m = new Matrix())
                    using (GraphicsPath clip = (GraphicsPath)silhouette.Clone())
                    {
                        m.Scale(k, k);
                        clip.Transform(m);
                        g.SetClip(clip);
                    }
                    Color[] mc = { Shapes.Hex(0x6d4c41), Shapes.Hex(0x795548), Shapes.Hex(0x5d4037), Shapes.Hex(0x8d6e63) };
                    int blobs = heavy ? 70 : 32;
                    for (int i = 0; i < blobs; i++)
                    {
                        float x = (float)rnd.NextDouble() * UW, y = 40 + (float)rnd.NextDouble() * 190;
                        float rr = (heavy ? 26 : 20) + (float)rnd.NextDouble() * 26;
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(240, mc[rnd.Next(mc.Length)])))
                        {
                            g.FillEllipse(b, (x - rr) * k, (y - rr * 0.8f) * k, 2 * rr * k, 1.6f * rr * k);
                            for (int j = 0; j < 4; j++)
                            {
                                double a = rnd.NextDouble() * Math.PI * 2;
                                float d = rr * 1.1f, sr = rr * 0.25f;
                                g.FillEllipse(b, (x + (float)Math.Cos(a) * d - sr) * k, (y + (float)Math.Sin(a) * d - sr) * k, 2 * sr * k, 2 * sr * k);
                            }
                        }
                    }
                }
                foam = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                dirtyCount = 0;
                for (int i = 0; i < inside.Length; i++)
                {
                    int px = (int)((i % cols + 0.5f) * Cell * k), py = (int)((i / cols + 0.5f) * Cell * k);
                    dirty[i] = inside[i] && px < w && py < h && mud.GetPixel(px, py).A > 100;
                    if (dirty[i]) dirtyCount++;
                }
            }

            // ---------- layout ----------

            public override void Layout()
            {
                if (parts == null) return;
                int barH = M.portrait ? R(240) : R(130);
                bar = new Rectangle(Area.X, Area.Bottom - barH, Area.Width, barH);
                Rectangle top = new Rectangle(Area.X, Area.Y, Area.Width, Area.Height - barH - R(16));
                float k = Math.Min((top.Width - R(40)) / UW, (top.Height - R(20)) / UH);
                Size vs = new Size((int)(UW * k), (int)(UH * k));
                Rectangle old = vRect;
                vRect = new Rectangle(top.X + (top.Width - vs.Width) / 2, top.Y + (top.Height - vs.Height) / 2 + R(10), vs.Width, vs.Height);

                if (mud == null) MakeMud();
                else if (old.Size != vRect.Size)
                {
                    mud = Rescale(mud, vRect.Size);
                    foam = Rescale(foam, vRect.Size);
                }
                RenderVehicle();

                // barra: etapas (1-2) ou cores + ✓ (3)
                int s = Math.Min(R(104), barH - R(20));
                for (int i = 0; i < 3; i++) stepIcons[i] = new Rectangle(bar.X + bar.Width / 2 - (3 * s + 2 * R(40)) / 2 + i * (s + R(40)), bar.Y + (barH - s) / 2, s, s);
                // cores grandes (dedo de criança): 1 fileira deitado, 2 fileiras em pé
                int perRow = M.portrait ? 4 : 8, nRows = 8 / perRow;
                int cs = Math.Min(R(100), Math.Min((bar.Width - R(170) - perRow * R(16)) / perRow, (barH - R(30) - (nRows - 1) * R(14)) / nRows));
                int total = perRow * cs + (perRow - 1) * R(16) + R(36) + R(130);
                int x0 = bar.X + (bar.Width - total) / 2, y0 = bar.Y + (barH - nRows * cs - (nRows - 1) * R(14)) / 2;
                for (int i = 0; i < 8; i++) colorBtns[i] = new Rectangle(x0 + (i % perRow) * (cs + R(16)), y0 + (i / perRow) * (cs + R(14)), cs, cs);
                int ch = Math.Min(R(150), barH - R(30));
                checkBtn = new Rectangle(colorBtns[perRow - 1].Right + R(36), bar.Y + (barH - ch) / 2, R(130), ch);

                if (bg != null) bg.Dispose();
                bg = new Bitmap(Math.Max(1, Area.Width), Math.Max(1, Area.Height), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bg))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(M.theme.Surface);
                    using (GraphicsPath clip = Shapes.RoundRect(new RectangleF(0, 0, Area.Width, Area.Height), 28 * Sc))
                    {
                        g.SetClip(clip);
                        float floorY = vRect.Bottom - Area.Y - 18 * k;
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xe1f5fe))) g.FillRectangle(b, 0, 0, Area.Width, floorY);
                        int t = Math.Max(10, R(56));
                        using (Pen p = new Pen(Shapes.Hex(0xb3e5fc), Math.Max(1, 2 * Sc)))
                        {
                            for (int x = 0; x < Area.Width; x += t) g.DrawLine(p, x, 0, x, floorY);
                            for (int y = 0; y < floorY; y += t) g.DrawLine(p, 0, y, Area.Width, y);
                        }
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x4fc3f7))) g.FillRectangle(b, 0, R(36), Area.Width, R(14));
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xb0bec5))) g.FillRectangle(b, 0, floorY, Area.Width, Area.Height - floorY);
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x90a4ae))) g.FillRectangle(b, 0, floorY, Area.Width, R(10));
                        // bolhas enfeitando a parede
                        Random fr = new Random(3);
                        using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255), Math.Max(1, 3 * Sc)))
                            for (int i = 0; i < 14; i++)
                            {
                                float bx = (float)fr.NextDouble() * Area.Width, by = R(60) + (float)fr.NextDouble() * (floorY - R(120)), br = R(10) + (float)fr.NextDouble() * R(26);
                                g.DrawEllipse(p, bx - br, by - br, 2 * br, 2 * br);
                            }
                        g.ResetClip();
                    }
                }
            }

            static Bitmap Rescale(Bitmap b, Size s)
            {
                Bitmap n = new Bitmap(Math.Max(1, s.Width), Math.Max(1, s.Height), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(n)) g.DrawImage(b, new Rectangle(Point.Empty, n.Size));
                b.Dispose();
                return n;
            }

            void RenderVehicle()
            {
                if (vehBmp != null) vehBmp.Dispose();
                vehBmp = new Bitmap(Math.Max(1, vRect.Width), Math.Max(1, vRect.Height), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(vehBmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    DrawVehicle(g, parts, body, K);
                }
            }

            // ---------- desenho ----------

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (bg == null) return;
                g.DrawImageUnscaled(bg, Area.X, Area.Y);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int dx = (int)(driveT * driveT * Area.Width);
                GraphicsState st = g.Save();
                g.SetClip(Area, CombineMode.Intersect);
                g.DrawImageUnscaled(vehBmp, vRect.X + dx, vRect.Y);
                if (stage <= 1) g.DrawImageUnscaled(mud, vRect.X, vRect.Y);
                if (stage <= 1) g.DrawImageUnscaled(foam, vRect.X, vRect.Y);
                foreach (Fx f in fx)
                {
                    int a = (int)(255 * Math.Min(1, f.Life * 3));
                    float x = f.X + (f.Kind == 2 ? dx : 0), r = f.Size;
                    if (f.Kind == 0) using (SolidBrush b = new SolidBrush(Color.FromArgb(a, f.C))) g.FillEllipse(b, x - r, f.Y - r * 1.4f, 2 * r, 2.8f * r);
                    else if (f.Kind == 1) using (Pen p = new Pen(Color.FromArgb(a, 255, 255, 255), Math.Max(1, 2.5f * Sc))) g.DrawEllipse(p, x - r, f.Y - r, 2 * r, 2 * r);
                    else
                        using (GraphicsPath s = Shapes.Star(x, f.Y, r, r * 0.4f, 4))
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, f.C))) g.FillPath(b, s);
                }
                // ferramenta embaixo do dedo
                foreach (PointF p in fingers.Values)
                {
                    if (stage == 0) DrawSponge(g, p.X, p.Y, Brush * K * 1.1f);
                    else if (stage == 1) DrawHose(g, p.X, p.Y, Brush * K);
                }
                g.Restore(st);

                // barra de baixo
                M.RoundBox(g, bar, 28 * Sc, Color.White, M.theme.Borda, 2 * Sc);
                if (stage <= 1)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Rectangle r = stepIcons[i];
                        bool cur = i == stage, done = i < stage;
                        M.RoundBox(g, r, 24 * Sc, cur ? Shapes.Hex(0xe1f5fe) : Color.White, cur ? Accent : M.theme.Borda, (cur ? 4 : 2) * Sc);
                        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = r.Width * 0.3f;
                        if (i == 0) DrawSponge(g, cx, cy, s);
                        else if (i == 1) DrawHose(g, cx, cy, s);
                        else DrawPaintIcon(g, cx, cy, s);
                        if (done) DrawCheck(g, new RectangleF(r.Right - R(30), r.Y - R(8), R(38), R(38)));
                        if (i < 2)
                            using (Pen p = Shapes.RoundPen(M.theme.Borda, 4 * Sc))
                            {
                                float ax = r.Right + R(12), ay = cy;
                                g.DrawLine(p, ax, ay, ax + R(16), ay);
                                g.DrawLine(p, ax + R(10), ay - R(6), ax + R(16), ay);
                                g.DrawLine(p, ax + R(10), ay + R(6), ax + R(16), ay);
                            }
                    }
                }
                else
                {
                    for (int i = 0; i < 8; i++)
                    {
                        Rectangle r = colorBtns[i];
                        bool sel = painted && PaintColors[i].ToArgb() == body.ToArgb();
                        using (SolidBrush b = new SolidBrush(PaintColors[i])) g.FillEllipse(b, r);
                        using (Pen p = new Pen(sel ? FInk : Color.FromArgb(60, FInk), (sel ? 5 : 2) * Sc)) g.DrawEllipse(p, r);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(b, r.X + r.Width * 0.2f, r.Y + r.Height * 0.15f, r.Width * 0.25f, r.Height * 0.18f);
                    }
                    if (painted && stage == 2)
                    {
                        float pulse = 1 + (float)Math.Sin(finishT * 6) * 0.06f;
                        RectangleF cr = new RectangleF(checkBtn.X + checkBtn.Width * (1 - pulse) / 2, checkBtn.Y + checkBtn.Height * (1 - pulse) / 2, checkBtn.Width * pulse, checkBtn.Height * pulse);
                        M.RoundBox(g, Rectangle.Round(cr), 26 * Sc, M.theme.Trator, Shapes.Hex(0x2f7d3d), 3 * Sc);
                        using (Pen p = Shapes.RoundPen(Color.White, 12 * Sc))
                            g.DrawLines(p, new PointF[] { new PointF(cr.X + cr.Width * 0.27f, cr.Y + cr.Height * 0.52f), new PointF(cr.X + cr.Width * 0.44f, cr.Y + cr.Height * 0.68f), new PointF(cr.X + cr.Width * 0.74f, cr.Y + cr.Height * 0.33f) });
                    }
                }
            }

            void DrawCheck(Graphics g, RectangleF r)
            {
                using (SolidBrush b = new SolidBrush(M.theme.Trator)) g.FillEllipse(b, r);
                using (Pen p = Shapes.RoundPen(Color.White, r.Width * 0.12f))
                    g.DrawLines(p, new PointF[] { new PointF(r.X + r.Width * 0.28f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.44f, r.Y + r.Height * 0.68f), new PointF(r.X + r.Width * 0.72f, r.Y + r.Height * 0.34f) });
            }

            static void DrawSponge(Graphics g, float cx, float cy, float s)
            {
                RectangleF r = new RectangleF(cx - s, cy - s * 0.65f, 2 * s, 1.3f * s);
                using (GraphicsPath p = Shapes.RoundRect(r, s * 0.3f))
                {
                    using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffd54f))) g.FillPath(b, p);
                    using (Pen pen = new Pen(FInk, Math.Max(1.5f, s * 0.08f))) g.DrawPath(pen, p);
                }
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xf9a825)))
                {
                    float[] holes = { -0.5f, -0.2f, 0.1f, 0.25f, 0.5f, -0.1f, -0.6f, 0.3f, 0.2f, -0.35f };
                    for (int i = 0; i < holes.Length; i += 2)
                        g.FillEllipse(b, cx + holes[i] * s - s * 0.1f, cy + holes[i + 1] * s - s * 0.1f, s * 0.2f, s * 0.2f);
                }
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x66bb6a))) g.FillRectangle(b, r.X + s * 0.1f, r.Y + s * 0.02f, r.Width - s * 0.2f, s * 0.22f);
            }

            static void DrawHose(Graphics g, float cx, float cy, float s)
            {
                // bico da mangueira apontando para baixo/esquerda e o jato
                using (SolidBrush w = new SolidBrush(Color.FromArgb(170, 79, 195, 247)))
                    g.FillPolygon(w, new PointF[] { new PointF(cx - s * 0.15f, cy - s * 0.1f), new PointF(cx - s * 0.9f, cy + s * 0.9f), new PointF(cx - s * 0.2f, cy + s * 1.0f) });
                GraphicsState st = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(-35);
                using (GraphicsPath p = Shapes.RoundRect(new RectangleF(-s * 0.22f, -s * 0.15f, s * 0.44f, s * 1.1f), s * 0.12f))
                {
                    using (SolidBrush b = new SolidBrush(Shapes.Hex(0x43a047))) g.FillPath(b, p);
                    using (Pen pen = new Pen(FInk, Math.Max(1.5f, s * 0.08f))) g.DrawPath(pen, p);
                }
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffc727))) g.FillRectangle(b, -s * 0.16f, -s * 0.3f, s * 0.32f, s * 0.2f);
                g.Restore(st);
            }

            static void DrawPaintIcon(Graphics g, float cx, float cy, float s)
            {
                for (int i = 0; i < 4; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 2;
                    float x = cx + (float)Math.Cos(a) * s * 0.55f, y = cy + (float)Math.Sin(a) * s * 0.55f;
                    using (SolidBrush b = new SolidBrush(PaintColors[i * 2])) g.FillEllipse(b, x - s * 0.38f, y - s * 0.38f, s * 0.76f, s * 0.76f);
                }
            }

            // ---------- toque ----------

            public override void Down(Contact c, Point p)
            {
                if (stage == 2)
                {
                    for (int i = 0; i < 8; i++)
                        if (colorBtns[i].Contains(p))
                        {
                            body = PaintColors[i];
                            painted = true;
                            RenderVehicle();
                            Sparkle(18);
                            RepaintAll();
                            return;
                        }
                    if (painted && checkBtn.Contains(p))
                    {
                        stage = 3;
                        Sparkle(30);
                        RepaintAll();
                    }
                    return;
                }
                if (stage > 1 || stageWait > 0) return;
                int id = c.Id;
                PointF last = p;
                fingers[id] = p;
                Rub(p, p);
                c.Role = Role.Arrastar;
                c.Drag = delegate(Point q)
                {
                    if (stage > 1) return;
                    Rub(last, q);
                    last = q;
                    fingers[id] = q;
                };
            }

            public override void Up(int id)
            {
                if (fingers.Remove(id)) Repaint(Rectangle.Inflate(vRect, R(80), R(80)));
            }

            // Esfrega (ou enxágua) ao longo do traço de a até b, em pixels da tela.
            void Rub(PointF a, PointF b)
            {
                if (mud == null) return;
                float k = K, rad = Brush * k;
                float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                int steps = Math.Max(1, (int)(len / (rad * 0.35f)));
                using (Graphics gm = Graphics.FromImage(stage == 0 ? mud : foam))
                using (Graphics gf = Graphics.FromImage(foam))
                using (SolidBrush clear = new SolidBrush(Color.Transparent))
                using (Matrix m = new Matrix())
                using (GraphicsPath clip = (GraphicsPath)silhouette.Clone())
                {
                    m.Scale(k, k);
                    clip.Transform(m);
                    gf.SetClip(clip);
                    gm.CompositingMode = CompositingMode.SourceCopy;
                    gm.SmoothingMode = SmoothingMode.AntiAlias;
                    gf.SmoothingMode = SmoothingMode.AntiAlias;
                    for (int i = 0; i <= steps; i++)
                    {
                        float x = a.X + dx * i / steps - vRect.X, y = a.Y + dy * i / steps - vRect.Y;
                        gm.FillEllipse(clear, x - rad, y - rad, 2 * rad, 2 * rad);
                        if (stage == 0 && rnd.NextDouble() < 0.7)
                            using (SolidBrush fb = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                            using (Pen fp = new Pen(Shapes.Hex(0xb3e5fc), Math.Max(1, 2 * Sc)))
                                for (int j = 0; j < 3; j++)
                                {
                                    float br = rad * (0.25f + (float)rnd.NextDouble() * 0.35f);
                                    float bx = x + ((float)rnd.NextDouble() - 0.5f) * rad * 1.4f, by = y + ((float)rnd.NextDouble() - 0.5f) * rad * 1.4f;
                                    gf.FillEllipse(fb, bx - br, by - br, 2 * br, 2 * br);
                                    gf.DrawEllipse(fp, bx - br, by - br, 2 * br, 2 * br);
                                }
                        MarkCells(x / k, y / k);
                    }
                }
                if (stage == 1)
                    for (int i = 0; i < 3; i++)
                    {
                        Fx f = new Fx();
                        f.Kind = 0;
                        f.X = b.X + ((float)rnd.NextDouble() - 0.5f) * rad;
                        f.Y = b.Y + rad * 0.3f;
                        f.VX = ((float)rnd.NextDouble() - 0.5f) * R(120);
                        f.VY = R(250) + (float)rnd.NextDouble() * R(200);
                        f.Life = 0.5f;
                        f.Size = R(5) + (float)rnd.NextDouble() * R(4);
                        f.C = Shapes.Hex(0x4fc3f7);
                        fx.Add(f);
                    }
                else if (rnd.NextDouble() < 0.4) Bubble(b.X, b.Y);
                CheckStage();
                Repaint(Rectangle.Inflate(vRect, R(80), R(80)));
            }

            void MarkCells(float ux, float uy)
            {
                int c0 = Math.Max(0, (int)((ux - Brush) / Cell)), c1 = Math.Min(cols - 1, (int)((ux + Brush) / Cell));
                int r0 = Math.Max(0, (int)((uy - Brush) / Cell)), r1 = Math.Min(rows - 1, (int)((uy + Brush) / Cell));
                for (int r = r0; r <= r1; r++)
                    for (int c = c0; c <= c1; c++)
                    {
                        float cx = (c + 0.5f) * Cell - ux, cy = (r + 0.5f) * Cell - uy;
                        if (cx * cx + cy * cy > Brush * Brush) continue;
                        int i = r * cols + c;
                        if (!inside[i]) continue;
                        if (stage == 0)
                        {
                            scrubbed[i] = true;
                            if (!foamy[i]) { foamy[i] = true; foamCount++; }
                        }
                        else rinsed[i] = true;
                    }
            }

            void CheckStage()
            {
                if (stage == 0)
                {
                    int done = 0;
                    for (int i = 0; i < dirty.Length; i++) if (dirty[i] && scrubbed[i]) done++;
                    if (done >= Math.Max(1, dirtyCount) * 0.88f) NextStage();
                }
                else if (stage == 1)
                {
                    int done = 0;
                    for (int i = 0; i < foamy.Length; i++) if (foamy[i] && rinsed[i]) done++;
                    if (done >= Math.Max(1, foamCount) * 0.88f) NextStage();
                }
            }

            void NextStage()
            {
                // termina sozinho: some o resto da sujeira/espuma e comemora
                using (Graphics g = Graphics.FromImage(stage == 0 ? mud : foam)) g.Clear(Color.Transparent);
                stage++;
                stageWait = 0.5f;
                fingers.Clear();
                Sparkle(16);
                RepaintAll();
            }

            void Sparkle(int n)
            {
                for (int i = 0; i < n; i++)
                {
                    Fx f = new Fx();
                    f.Kind = 2;
                    f.X = vRect.X + (float)rnd.NextDouble() * vRect.Width;
                    f.Y = vRect.Y + vRect.Height * 0.15f + (float)rnd.NextDouble() * vRect.Height * 0.7f;
                    f.VY = -R(30);
                    f.Life = 0.6f + (float)rnd.NextDouble() * 0.6f;
                    f.Size = R(10) + (float)rnd.NextDouble() * R(14);
                    f.C = rnd.Next(3) == 0 ? Shapes.Hex(0xffd54f) : Color.White;
                    fx.Add(f);
                }
            }

            void Bubble(float x, float y)
            {
                Fx f = new Fx();
                f.Kind = 1;
                f.X = x;
                f.Y = y;
                f.VX = ((float)rnd.NextDouble() - 0.5f) * R(60);
                f.VY = -R(90) - (float)rnd.NextDouble() * R(80);
                f.Life = 0.9f;
                f.Size = R(8) + (float)rnd.NextDouble() * R(12);
                fx.Add(f);
            }

            public override void Tick(float dt)
            {
                bool any = fx.Count > 0;
                for (int i = fx.Count - 1; i >= 0; i--)
                {
                    Fx f = fx[i];
                    f.X += f.VX * dt;
                    f.Y += f.VY * dt;
                    f.Life -= dt;
                    if (f.Life <= 0) fx.RemoveAt(i);
                }
                if (stageWait > 0) stageWait -= dt;
                if (stage == 2 && painted) { finishT += dt; Repaint(Rectangle.Inflate(checkBtn, R(12), R(12))); }
                if (stage == 3)
                {
                    driveT += dt / 1.3f;
                    if (rnd.NextDouble() < 0.5) Bubble(vRect.X + driveT * driveT * Area.Width + (float)rnd.NextDouble() * vRect.Width, vRect.Bottom - R(30));
                    if (driveT >= 1) { driveT = 1; Won = true; }
                    any = true;
                }
                if (any) Repaint(Rectangle.Inflate(new Rectangle(Area.X, vRect.Y, Area.Width, vRect.Height), R(60), R(60)));
            }

            // Só para o modo --captura: pula direto para uma etapa.
            public void DemoStage(int s)
            {
                while (stage < s && stage < 2) NextStage();
                fingers.Clear();
                if (s >= 2) { body = PaintColors[4]; painted = true; RenderVehicle(); }
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xe1f5fe))) g.FillRectangle(b, img);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xb0bec5))) g.FillRectangle(b, img.X, img.Bottom - img.Height * 0.18f, img.Width, img.Height * 0.18f);
                float k = Math.Min(img.Width * 0.86f / UW, img.Height * 0.8f / UH);
                g.TranslateTransform(img.X + (img.Width - UW * k) / 2, img.Bottom - img.Height * 0.06f - UH * k);
                List<Part> car = Build(0);
                DrawVehicle(g, car, Shapes.Hex(0xe53935), k);
                // metade suja
                GraphicsPath sil = new GraphicsPath(FillMode.Winding);
                foreach (Part p in car) sil.AddPath(p.Path, false);
                using (Matrix m = new Matrix()) { m.Scale(k, k); sil.Transform(m); }
                g.SetClip(sil, CombineMode.Intersect);
                g.SetClip(new RectangleF(UW * k * 0.5f, 0, UW * k, UH * k), CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x6d4c41))) g.FillRectangle(b, 0, 0, UW * k, UH * k);
                using (SolidBrush b = new SolidBrush(Color.White))
                    for (int i = 0; i < 6; i++) g.FillEllipse(b, UW * k * (0.48f + (i % 3) * 0.05f), UH * k * (0.3f + i * 0.09f), 20 * k * (1 + i % 2), 20 * k * (1 + i % 2));
                sil.Dispose();
                g.Restore(st);
                DrawSponge(g, img.X + img.Width * 0.5f, img.Y + img.Height * 0.3f, Math.Min(img.Width, img.Height) * 0.12f);
            }
        }
    }
}
