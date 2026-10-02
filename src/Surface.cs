using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Pintinho
{
    // A folha de pintura: camada de tinta + camada de contorno (sempre por cima).
    class Surface : IDisposable
    {
        const int FillTolerance = 100;

        public int W { get; private set; }
        public int H { get; private set; }
        public Bitmap Paint { get; private set; }
        public Bitmap Overlay { get; private set; }

        readonly int maxUndo;
        byte[] mask; // 1 = pixel de linha do contorno (barreira do balde)
        readonly List<Bitmap> undo = new List<Bitmap>();
        readonly List<Bitmap> redo = new List<Bitmap>();

        // estado temporário do balde
        int[] px;
        bool[] done;
        int target;

        public Surface(int w, int h, int maxUndo)
        {
            W = w;
            H = h;
            this.maxUndo = maxUndo;
            Paint = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            Clear();
        }

        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        public void Clear()
        {
            using (Graphics g = Graphics.FromImage(Paint)) g.Clear(Color.White);
        }

        static void Copy(Bitmap src, Bitmap dst, bool smooth)
        {
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = smooth ? InterpolationMode.HighQualityBilinear : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, dst.Width, dst.Height));
            }
        }

        Bitmap Snapshot()
        {
            Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            Copy(Paint, b, false);
            return b;
        }

        // Muda o tamanho da folha (tela girou). A tinta é reescalada; o histórico é esvaziado.
        public void Resize(int w, int h)
        {
            if (w == W && h == H) return;
            Bitmap np = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            Copy(Paint, np, true);
            Paint.Dispose();
            Paint = np;
            W = w;
            H = h;
            ClearUndo();
            SetOverlay(null);
        }

        public void SaveUndo()
        {
            undo.Add(Snapshot());
            if (undo.Count > maxUndo)
            {
                undo[0].Dispose();
                undo.RemoveAt(0);
            }
            Dispose(redo);
        }

        public void DropLastUndo()
        {
            if (undo.Count == 0) return;
            undo[undo.Count - 1].Dispose();
            undo.RemoveAt(undo.Count - 1);
        }

        // Desfaz o traço em andamento sem mandar nada para o "refazer" (vira pinça).
        public void CancelStroke()
        {
            if (undo.Count == 0) return;
            Copy(undo[undo.Count - 1], Paint, false);
            DropLastUndo();
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            redo.Add(Snapshot());
            Bitmap b = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Copy(b, Paint, false);
            b.Dispose();
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            undo.Add(Snapshot());
            Bitmap b = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Copy(b, Paint, false);
            b.Dispose();
            return true;
        }

        static void Dispose(List<Bitmap> list)
        {
            foreach (Bitmap b in list) b.Dispose();
            list.Clear();
        }

        public void ClearUndo()
        {
            Dispose(undo);
            Dispose(redo);
        }

        public void SetOverlay(Bitmap ov)
        {
            if (Overlay != null) Overlay.Dispose();
            Overlay = ov;
            mask = null;
            if (ov == null) return;

            BitmapData d = ov.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int[] a = new int[W * H];
            Marshal.Copy(d.Scan0, a, 0, a.Length);
            ov.UnlockBits(d);
            mask = new byte[W * H];
            for (int i = 0; i < a.Length; i++)
                if (((a[i] >> 24) & 255) >= 100) mask[i] = 1;
        }

        static Rectangle Around(PointF c, float r)
        {
            int k = (int)Math.Ceiling(r) + 2;
            return new Rectangle((int)c.X - k, (int)c.Y - k, 2 * k, 2 * k);
        }

        static Rectangle Bounds(PointF a, PointF b, float w)
        {
            Rectangle r = Rectangle.FromLTRB((int)Math.Floor(Math.Min(a.X, b.X)), (int)Math.Floor(Math.Min(a.Y, b.Y)),
                (int)Math.Ceiling(Math.Max(a.X, b.X)), (int)Math.Ceiling(Math.Max(a.Y, b.Y)));
            r.Inflate((int)w + 3, (int)w + 3);
            return r;
        }

        // Traço opaco, desenhado pedaço a pedaço (rápido).
        public Rectangle Line(PointF a, PointF b, Color c, float w)
        {
            using (Graphics g = Graphics.FromImage(Paint))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (a == b)
                {
                    using (SolidBrush br = new SolidBrush(c)) g.FillEllipse(br, a.X - w / 2f, a.Y - w / 2f, w, w);
                }
                else
                {
                    using (Pen p = Shapes.RoundPen(c, w)) g.DrawLine(p, a, b);
                }
            }
            return Bounds(a, b, w);
        }

        // Traço transparente (marcador, lápis, opacidade < 100%): redesenha o traço inteiro sobre a
        // foto tirada no início dele, só na área do último pedaço. Assim a tinta não "acumula" onde o
        // traço passa por cima de si mesmo. Precisa de SaveUndo() no começo do traço.
        public Rectangle StrokePath(List<PointF> pts, Color c, float w, bool flat)
        {
            if (undo.Count == 0 || pts.Count == 0) return Rectangle.Empty;
            PointF a = pts[Math.Max(0, pts.Count - 2)], b = pts[pts.Count - 1];
            Rectangle r = Bounds(a, b, w);
            r.Intersect(new Rectangle(0, 0, W, H));
            if (r.IsEmpty) return r;
            Bitmap snap = undo[undo.Count - 1];
            using (Graphics g = Graphics.FromImage(Paint))
            {
                g.SetClip(r);
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(snap, r, r, GraphicsUnit.Pixel);
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (pts.Count == 1)
                {
                    using (SolidBrush br = new SolidBrush(c)) g.FillEllipse(br, a.X - w / 2f, a.Y - w / 2f, w, w);
                }
                else
                {
                    using (Pen p = new Pen(c, w))
                    {
                        p.LineJoin = LineJoin.Round;
                        p.StartCap = flat ? LineCap.Flat : LineCap.Round;
                        p.EndCap = flat ? LineCap.Flat : LineCap.Round;
                        g.DrawLines(p, pts.ToArray());
                    }
                }
            }
            return r;
        }

        public Rectangle Spray(PointF c, Color col, float radius, Random rnd)
        {
            using (Graphics g = Graphics.FromImage(Paint))
            using (SolidBrush b = new SolidBrush(col))
            {
                int n = (int)(radius * 0.6f) + 6;
                float dot = Math.Max(1.5f, radius / 12f);
                for (int i = 0; i < n; i++)
                {
                    double ang = rnd.NextDouble() * 2 * Math.PI;
                    double dist = Math.Sqrt(rnd.NextDouble()) * radius;
                    g.FillEllipse(b, (float)(c.X + Math.Cos(ang) * dist - dot / 2), (float)(c.Y + Math.Sin(ang) * dist - dot / 2), dot, dot);
                }
            }
            return Around(c, radius + 2);
        }

        public Rectangle Stamp(PointF c, int kind, Color col, float size)
        {
            using (Graphics g = Graphics.FromImage(Paint))
                Stamps.Draw(g, kind, c.X, c.Y, size, col, Color.FromArgb(Math.Min(col.A, (byte)110), 0, 0, 0), Math.Max(2f, size / 14f));
            return Around(c, size * 1.3f);
        }

        public Color PickColor(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return Color.Empty;
            Color c = Paint.GetPixel(x, y);
            return Color.FromArgb(255, c.R, c.G, c.B);
        }

        static int Blend(int dst, int r, int g, int b, int a)
        {
            int ia = 255 - a;
            int dr = (dst >> 16) & 255, dg = (dst >> 8) & 255, db = dst & 255;
            return unchecked((int)0xFF000000) | (((r * a + dr * ia) / 255) << 16) | (((g * a + dg * ia) / 255) << 8) | ((b * a + db * ia) / 255);
        }

        // Balde de tinta: preenche a região parecida com o ponto tocado, parando nas linhas.
        // Com cor transparente (opacidade < 100%), mistura com a tinta que já estava ali.
        public Rectangle Fill(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return Rectangle.Empty;
            int seed = y * W + x;
            if (mask != null && mask[seed] != 0) return Rectangle.Empty;

            BitmapData d = Paint.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                int n = W * H;
                px = new int[n];
                Marshal.Copy(d.Scan0, px, 0, n);
                target = px[seed];
                bool opaque = c.A == 255;
                int fill = Color.FromArgb(255, c.R, c.G, c.B).ToArgb();
                if (opaque && target == fill) return Rectangle.Empty;

                done = new bool[n];
                int minX = x, maxX = x, minY = y, maxY = y;
                Stack<int> st = new Stack<int>();
                st.Push(seed);
                while (st.Count > 0)
                {
                    int p = st.Pop();
                    if (!Ok(p)) continue;
                    int row = p / W, rs = row * W, re = rs + W;
                    int i = p;
                    while (i > rs && Ok(i - 1)) i--;
                    int sx = i - rs;
                    bool upOpen = false, downOpen = false;
                    while (i < re && Ok(i))
                    {
                        done[i] = true;
                        if (row > 0)
                        {
                            if (Ok(i - W)) { if (!upOpen) { st.Push(i - W); upOpen = true; } }
                            else upOpen = false;
                        }
                        if (row < H - 1)
                        {
                            if (Ok(i + W)) { if (!downOpen) { st.Push(i + W); downOpen = true; } }
                            else downOpen = false;
                        }
                        i++;
                    }
                    int ex = i - 1 - rs;
                    if (sx < minX) minX = sx;
                    if (ex > maxX) maxX = ex;
                    if (row < minY) minY = row;
                    if (row > maxY) maxY = row;
                }

                // Cobre as bordas suavizadas: 1 pixel em volta e mais 1 por baixo das linhas.
                Dilate(minX - 1, minY - 1, maxX + 1, maxY + 1, false);
                Dilate(minX - 2, minY - 2, maxX + 2, maxY + 2, true);

                // pinta tudo que foi marcado
                int x0 = Math.Max(0, minX - 2), x1 = Math.Min(W - 1, maxX + 2), y0 = Math.Max(0, minY - 2), y1 = Math.Min(H - 1, maxY + 2);
                for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0, i = yy * W + x0; xx <= x1; xx++, i++)
                        if (done[i]) px[i] = opaque ? fill : Blend(px[i], c.R, c.G, c.B, c.A);

                Marshal.Copy(px, 0, d.Scan0, n);
                return Rectangle.FromLTRB(minX - 3, minY - 3, maxX + 4, maxY + 4);
            }
            finally
            {
                Paint.UnlockBits(d);
                px = null;
                done = null;
            }
        }

        bool Ok(int i)
        {
            return !done[i] && (mask == null || mask[i] == 0) && Near(px[i], target);
        }

        static bool Near(int a, int b)
        {
            int dr = ((a >> 16) & 255) - ((b >> 16) & 255);
            int dg = ((a >> 8) & 255) - ((b >> 8) & 255);
            int db = (a & 255) - (b & 255);
            return (dr < 0 ? -dr : dr) + (dg < 0 ? -dg : dg) + (db < 0 ? -db : db) <= FillTolerance;
        }

        void Dilate(int x0, int y0, int x1, int y1, bool onlyMask)
        {
            x0 = Math.Max(0, x0); y0 = Math.Max(0, y0);
            x1 = Math.Min(W - 1, x1); y1 = Math.Min(H - 1, y1);
            List<int> add = new List<int>();
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * W + x;
                    if (done[i]) continue;
                    if (onlyMask && (mask == null || mask[i] == 0)) continue;
                    if ((x > 0 && done[i - 1]) || (x < W - 1 && done[i + 1]) || (y > 0 && done[i - W]) || (y < H - 1 && done[i + W]))
                        add.Add(i);
                }
            }
            foreach (int i in add) done[i] = true;
        }

        public Bitmap Compose()
        {
            Bitmap b = new Bitmap(W, H, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                Rectangle r = new Rectangle(0, 0, W, H);
                g.DrawImage(Paint, r);
                if (Overlay != null) g.DrawImage(Overlay, r);
            }
            return b;
        }

        public void Dispose()
        {
            ClearUndo();
            if (Overlay != null) Overlay.Dispose();
            Paint.Dispose();
        }
    }
}
