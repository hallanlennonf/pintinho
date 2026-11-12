using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Pintinho
{
    partial class MainForm
    {
        // Estacionamento (2 a 4 anos): os carrinhos esperam na rua; arrastar cada um para a vaga
        // certa (ou tocar no carro e depois na vaga). Errou: volta para a rua. Nunca perde.
        // Fases 1-7: vaga da mesma cor. 8-14: o mesmo desenho (estrela, coração...) no teto e na
        // vaga. 15-20: número no carro e bolinhas (quantidade) na vaga. Mais vagas a cada fase.
        class ParkGame : Mini
        {
            public override string Key { get { return "estacionamento"; } }
            public override string Title { get { return "Estacionamento"; } }
            public override Color Accent { get { return Shapes.Hex(0x546e7a); } }
            public override int Levels { get { return 20; } }
            public override string Info { get { return parked + "/" + cars.Length; } }

            static readonly Color[] CarColors = {
                Shapes.Hex(0xe53935), Shapes.Hex(0x1e88e5), Shapes.Hex(0xfdd835), Shapes.Hex(0x43a047),
                Shapes.Hex(0x8e24aa), Shapes.Hex(0xfb8c00), Shapes.Hex(0xec407a)
            };

            class Car
            {
                public int Key;          // cor / forma / número que tem que bater com a vaga
                public Color C;
                public float X, Y, HomeX, HomeY;
                public int Spot = -1;
                public float Wobble;
            }

            int mode, parked; // 0 cor, 1 forma, 2 número
            Car[] cars = new Car[0];
            int[] spotKey = new int[0];
            RectangleF[] spots = new RectangleF[0];
            RectangleF road;
            float carW, carH;
            Car dragging, selected;
            int dragId;
            bool moved;
            Random rnd;
            Bitmap bg;
            Font numFont;
            readonly Sparks sparks = new Sparks();

            public override void Start(Random r)
            {
                rnd = r;
                int L = Level;
                mode = (L - 1) / 7; // 0..2
                int i = (L - 1) % 7;
                int n = Math.Min(7, 2 + (i + 1) / 2 + mode); // 2..7 vagas
                spotKey = new int[n];
                for (int k = 0; k < n; k++) spotKey[k] = k;
                Shuffle(spotKey);
                cars = new Car[n];
                List<int> order = new List<int>();
                for (int k = 0; k < n; k++) order.Add(k);
                for (int k = 0; k < n; k++)
                {
                    Car c = new Car();
                    int j = rnd.Next(order.Count);
                    c.Key = order[j];
                    order.RemoveAt(j);
                    c.C = mode == 0 ? CarColors[c.Key] : CarColors[rnd.Next(CarColors.Length)];
                    cars[k] = c;
                }
                parked = 0;
                dragging = selected = null;
                sparks.Clear();
                if (bg != null) { bg.Dispose(); bg = null; }
            }

            void Shuffle(int[] a)
            {
                for (int i = a.Length - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    int t = a[i]; a[i] = a[j]; a[j] = t;
                }
            }

            int spotRows, spotPer;
            float lotBottom;

            public override void Layout()
            {
                int n = spotKey.Length;
                float pad = R(24), lgap = R(18);
                // em pé, com muitas vagas: duas fileiras
                spotRows = M.portrait && n > 3 ? 2 : 1;
                spotPer = (n + spotRows - 1) / spotRows;
                float maxLot = Area.Height * (M.portrait ? 0.56f : 0.52f);
                float sw = Math.Min(R(190), (Area.Width - 2 * pad) / spotPer);
                float sh = Math.Min((maxLot - R(30) - (spotRows - 1) * lgap) / spotRows, sw * 1.7f);
                spots = new RectangleF[n];
                for (int i = 0; i < n; i++)
                {
                    int r = i / spotPer;
                    int inRow = Math.Min(spotPer, n - r * spotPer);
                    float x0 = Area.X + (Area.Width - inRow * sw) / 2;
                    spots[i] = new RectangleF(x0 + (i % spotPer) * sw, Area.Y + R(24) + r * (sh + lgap), sw, sh);
                }
                lotBottom = spots[n - 1].Bottom + R(14);
                road = new RectangleF(Area.X, lotBottom + R(20), Area.Width, Area.Bottom - lotBottom - R(20));
                carW = sw * 0.6f;
                carH = Math.Min(sh * 0.78f, carW * 1.75f);
                ArrangeHomes(true);
                if (numFont != null) numFont.Dispose();
                numFont = new Font("Segoe UI", Math.Max(8, carW * 0.42f), FontStyle.Bold, GraphicsUnit.Pixel);

                if (bg != null) bg.Dispose();
                bg = new Bitmap(Math.Max(1, Area.Width), Math.Max(1, Area.Height), PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bg))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(M.theme.Surface);
                    g.TranslateTransform(-Area.X, -Area.Y);
                    using (GraphicsPath p = Shapes.RoundRect(new RectangleF(Area.X, Area.Y, Area.Width, Area.Height), 28 * Sc))
                    {
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x9ccc65))) g.FillPath(b, p);
                        g.SetClip(p);
                        // pátio do estacionamento
                        float lx = Area.X + (Area.Width - spotPer * sw) / 2;
                        RectangleF lot = new RectangleF(lx - R(16), Area.Y + R(10), spotPer * sw + R(32), lotBottom - Area.Y - R(10));
                        using (GraphicsPath lp = Shapes.RoundRect(lot, 18 * Sc))
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x78909c))) g.FillPath(b, lp);
                        // rua
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0x607d8b))) g.FillRectangle(b, road);
                        using (SolidBrush b = new SolidBrush(Shapes.Hex(0xb0bec5))) g.FillRectangle(b, road.X, road.Y - R(10), road.Width, R(10));
                        using (Pen dash = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(2, 5 * Sc)))
                        {
                            float midY = road.Bottom - R(14);
                            for (float x = road.X + R(20); x < road.Right; x += R(80)) g.DrawLine(dash, x, midY, x + R(40), midY);
                        }
                        g.ResetClip();
                    }
                    // vagas: linhas brancas + marca da vaga
                    using (Pen line = new Pen(Color.White, Math.Max(2, 6 * Sc)))
                        for (int i = 0; i < n; i++)
                        {
                            g.DrawLine(line, spots[i].X, spots[i].Y, spots[i].X, spots[i].Bottom);
                            g.DrawLine(line, spots[i].Right, spots[i].Y, spots[i].Right, spots[i].Bottom);
                        }
                    for (int i = 0; i < n; i++) DrawSpotMark(g, i);
                }
            }

            // Os carros que ainda estão na rua ficam centralizados (uma ou duas fileiras).
            void ArrangeHomes(bool snap)
            {
                List<Car> wait = new List<Car>();
                foreach (Car c in cars) if (c.Spot < 0) wait.Add(c);
                int n = Math.Max(1, wait.Count);
                float pad = R(24);
                int perRow = n;
                float cw = (Area.Width - 2 * pad) / perRow;
                float baseW = spots.Length > 0 ? spots[0].Width * 0.6f : carW;
                if (cw < baseW * 1.15f) { perRow = (n + 1) / 2; cw = (Area.Width - 2 * pad) / perRow; }
                int rowsN = (n + perRow - 1) / perRow;
                float rowH = road.Height / rowsN;
                if (snap && carH > rowH * 0.85f) { float k = rowH * 0.85f / carH; carH *= k; carW *= k; }
                cw = Math.Min(cw, carW * 1.6f);
                for (int i = 0; i < wait.Count; i++)
                {
                    Car c = wait[i];
                    int inRow = Math.Min(perRow, wait.Count - (i / perRow) * perRow);
                    float rx0 = Area.X + (Area.Width - inRow * cw) / 2;
                    c.HomeX = rx0 + (i % perRow + 0.5f) * cw;
                    c.HomeY = road.Y + (i / perRow + 0.5f) * rowH;
                    if (snap) { c.X = c.HomeX; c.Y = c.HomeY; }
                }
                if (snap)
                    foreach (Car c in cars)
                        if (c.Spot >= 0) { PointF sc = SpotCenter(c.Spot); c.X = sc.X; c.Y = sc.Y; }
            }

            PointF SpotCenter(int i) { return new PointF(spots[i].X + spots[i].Width / 2, spots[i].Y + spots[i].Height / 2); }

            void DrawSpotMark(Graphics g, int i)
            {
                RectangleF s = spots[i];
                int key = spotKey[i];
                float cx = s.X + s.Width / 2, cy = s.Y + s.Height / 2, r = Math.Min(s.Width, s.Height) * 0.3f;
                if (mode == 0)
                {
                    RectangleF inner = RectangleF.Inflate(s, -s.Width * 0.12f, -s.Height * 0.06f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, CarColors[key]))) g.FillRectangle(b, inner);
                    using (Pen p = new Pen(CarColors[key], Math.Max(2, 6 * Sc))) g.DrawRectangle(p, inner.X, inner.Y, inner.Width, inner.Height);
                }
                else if (mode == 1)
                {
                    DrawShape(g, key % 5, cx, cy, r, Color.FromArgb(230, 255, 255, 255), 0);
                    if (key >= 5) DrawShape(g, key % 5, cx, cy, r * 0.45f, Shapes.Hex(0x78909c), 0);
                }
                else
                {
                    // bolinhas = quantidade (1 a 7)
                    int q = key + 1;
                    float dr = Math.Min(s.Width * 0.13f, R(16));
                    int cols = q <= 3 ? 1 : 2;
                    int rows = (q + cols - 1) / cols;
                    using (SolidBrush b = new SolidBrush(Color.White))
                        for (int k = 0; k < q; k++)
                        {
                            int c = k % cols, rr = k / cols;
                            int inRow = Math.Min(cols, q - rr * cols);
                            float x = cx + (c - (inRow - 1) / 2f) * dr * 2.6f, y = cy + (rr - (rows - 1) / 2f) * dr * 2.6f;
                            g.FillEllipse(b, x - dr, y - dr, 2 * dr, 2 * dr);
                        }
                }
            }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (bg == null) return;
                g.DrawImageUnscaled(bg, Area.X, Area.Y);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (Car c in cars) if (c != dragging) DrawCar(g, c, c == selected);
                if (dragging != null) DrawCar(g, dragging, true);
                sparks.Paint(g);
            }

            void DrawCar(Graphics g, Car c, bool lifted)
            {
                float s = lifted ? 1.1f : 1;
                float w = carW * s, h = carH * s;
                float wob = (float)Math.Sin(c.Wobble * 30) * c.Wobble * w * 0.4f;
                float x = c.X - w / 2 + wob, y = c.Y - h / 2;
                if (lifted)
                    using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sh, x - w * 0.1f, y + h * 0.1f, w * 1.2f, h * 1.0f);
                float lw = Math.Max(1.5f, w * 0.045f);
                // rodas
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x263238)))
                    foreach (float fy in new float[] { 0.2f, 0.72f })
                    {
                        g.FillRectangle(b, x - w * 0.08f, y + h * fy, w * 0.16f, h * 0.17f);
                        g.FillRectangle(b, x + w * 0.92f, y + h * fy, w * 0.16f, h * 0.17f);
                    }
                using (GraphicsPath p = Shapes.RoundRect(new RectangleF(x, y, w, h), w * 0.32f))
                {
                    using (SolidBrush b = new SolidBrush(c.C)) g.FillPath(b, p);
                    using (Pen pen = new Pen(FInk, lw)) g.DrawPath(pen, p);
                }
                // para-brisa (frente para cima) e vidro de trás
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xb3e5fc)))
                using (Pen pen = new Pen(FInk, lw * 0.8f))
                {
                    using (GraphicsPath p = Shapes.RoundRect(new RectangleF(x + w * 0.14f, y + h * 0.17f, w * 0.72f, h * 0.17f), w * 0.1f)) { g.FillPath(b, p); g.DrawPath(pen, p); }
                    using (GraphicsPath p = Shapes.RoundRect(new RectangleF(x + w * 0.18f, y + h * 0.74f, w * 0.64f, h * 0.12f), w * 0.08f)) { g.FillPath(b, p); g.DrawPath(pen, p); }
                }
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xfff59d)))
                {
                    g.FillEllipse(b, x + w * 0.1f, y + h * 0.02f, w * 0.2f, h * 0.07f);
                    g.FillEllipse(b, x + w * 0.7f, y + h * 0.02f, w * 0.2f, h * 0.07f);
                }
                // teto com a marca
                float cx = x + w / 2, cy = y + h * 0.54f, r = w * 0.3f;
                if (mode == 1)
                {
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - r * 1.15f, cy - r * 1.15f, r * 2.3f, r * 2.3f);
                    DrawShape(g, c.Key % 5, cx, cy, r, Shapes.Hex(0x37474f), 0);
                    if (c.Key >= 5) DrawShape(g, c.Key % 5, cx, cy, r * 0.45f, Color.White, 0);
                }
                else if (mode == 2)
                {
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - r * 1.15f, cy - r * 1.15f, r * 2.3f, r * 2.3f);
                    DrawText(g, (c.Key + 1).ToString(), numFont, FInk, new RectangleF(cx - r * 1.3f, cy - r * 1.2f, r * 2.6f, r * 2.5f), CenterFmt);
                }
            }

            Car CarAt(Point p)
            {
                for (int i = cars.Length - 1; i >= 0; i--)
                {
                    Car c = cars[i];
                    if (c.Spot >= 0) continue;
                    if (Math.Abs(p.X - c.X) < carW * 0.7f && Math.Abs(p.Y - c.Y) < carH * 0.6f) return c;
                }
                return null;
            }

            int SpotAt(PointF p)
            {
                for (int i = 0; i < spots.Length; i++)
                    if (RectangleF.Inflate(spots[i], 0, R(30)).Contains(p)) return i;
                return -1;
            }

            public override void Down(Contact c, Point p)
            {
                if (dragging != null) return;
                Car car = CarAt(p);
                if (car == null)
                {
                    int s = SpotAt(p);
                    if (s >= 0 && selected != null) Park(selected, s);
                    selected = null;
                    RepaintAll();
                    return;
                }
                dragging = car;
                dragId = c.Id;
                moved = false;
                float ox = car.X - p.X, oy = car.Y - p.Y;
                Point start = p;
                c.Role = Role.Arrastar;
                c.Drag = delegate(Point q)
                {
                    if (dragging != car) return;
                    car.X = q.X + ox; car.Y = q.Y + oy;
                    if (Math.Abs(q.X - start.X) + Math.Abs(q.Y - start.Y) > R(20)) moved = true;
                    Repaint(Area);
                };
                RepaintAll();
            }

            public override void Up(int id)
            {
                if (dragging == null || id != dragId) return;
                Car car = dragging;
                dragging = null;
                int s = SpotAt(new PointF(car.X, car.Y));
                if (moved && s >= 0) { Park(car, s); selected = null; }
                else if (!moved) selected = selected == car ? null : car;
                else selected = null;
                RepaintAll();
            }

            void Park(Car car, int s)
            {
                bool free = true;
                foreach (Car o in cars) if (o.Spot == s) free = false;
                if (free && spotKey[s] == car.Key)
                {
                    car.Spot = s;
                    PointF c = SpotCenter(s);
                    car.X = c.X; car.Y = c.Y;
                    parked++;
                    sparks.Burst(c.X, c.Y, 16, R(9), car.C);
                    ArrangeHomes(false);
                    if (parked == cars.Length) Won = true;
                }
                else car.Wobble = 0.5f;
            }

            public override void Tick(float dt)
            {
                bool any = sparks.Tick(dt);
                foreach (Car c in cars)
                {
                    if (c.Spot >= 0 || c == dragging) continue;
                    float dx = c.HomeX - c.X, dy = c.HomeY - c.Y;
                    if (Math.Abs(dx) + Math.Abs(dy) > 0.5f)
                    {
                        float k = Math.Min(1, dt * 8);
                        c.X += dx * k; c.Y += dy * k;
                        any = true;
                    }
                    if (c.Wobble > 0) { c.Wobble = Math.Max(0, c.Wobble - dt); any = true; }
                }
                if (any) Repaint(Area);
            }

            public override void Demo(Random r)
            {
                for (int i = 0; i < cars.Length / 2; i++)
                    for (int s = 0; s < spotKey.Length; s++)
                        if (spotKey[s] == cars[i].Key) Park(cars[i], s);
                foreach (Car c in cars) if (c.Spot < 0) { selected = c; break; }
                Won = false;
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x78909c))) g.FillRectangle(b, img);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x607d8b))) g.FillRectangle(b, img.X, img.Y + img.Height * 0.62f, img.Width, img.Height);
                float sw = img.Width / 4f;
                using (Pen p = new Pen(Color.White, Math.Max(2, 4 * Sc)))
                    for (int i = 1; i < 4; i++) g.DrawLine(p, img.X + i * sw, img.Y, img.X + i * sw, img.Y + img.Height * 0.55f);
                Color[] cs = { CarColors[0], CarColors[1], CarColors[2] };
                for (int i = 0; i < 3; i++)
                    using (Pen p = new Pen(cs[i], Math.Max(2, 4 * Sc))) g.DrawRectangle(p, img.X + i * sw + sw * 0.15f + sw * 0.5f, img.Y + img.Height * 0.06f, sw * 0.7f, img.Height * 0.45f);
                float oldW = carW, oldH = carH;
                int oldMode = mode;
                mode = 0;
                carW = sw * 0.45f; carH = carW * 1.7f;
                Car a = new Car(); a.C = cs[0]; a.X = img.X + sw * 1.5f; a.Y = img.Y + img.Height * 0.3f;
                Car b2 = new Car(); b2.C = cs[1]; b2.X = img.X + sw * 2.4f; b2.Y = img.Y + img.Height * 0.8f;
                DrawCar(g, a, false);
                DrawCar(g, b2, true);
                carW = oldW; carH = oldH; mode = oldMode;
                g.Restore(st);
            }
        }
    }
}
