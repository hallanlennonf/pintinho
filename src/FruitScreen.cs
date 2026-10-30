using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;

namespace Pintinho
{
    enum FState { Menu, Jogando, Pausado, Fim }

    // Rastro de um dedo cortando (pontos em pixels do design, com o tempo de cada um).
    class Blade
    {
        public readonly List<PointF> Pts = new List<PointF>();
        public readonly List<float> Times = new List<float>();
        public bool Done;
        public int Combo;
        public float LastCut;
        public PointF ComboAt;
    }

    // Salada de Frutas: menu (Pequenos / Desafio), partida, pausa e fim.
    // Mesmo esquema do Comboio: laço próprio (Application.Idle), lógica em passos de 1/60 s e frutas
    // pré-desenhadas (inteira e as duas metades) que só giram na hora de desenhar.
    partial class MainForm
    {
        static readonly Color FInk = Shapes.Hex(0x2b2118), FCream = Shapes.Hex(0xfff8ec);
        static readonly Color FGreen = Shapes.Hex(0x5cbf6d), FRed = Shapes.Hex(0xe5533e);

        Fruits fruits;
        FState fstate = FState.Menu;
        bool fKids, fRecord;
        Rectangle fBack, fPause, fKidsBtn, fHardBtn, fPanel, fBtnA, fBtnB;
        Bitmap fBg;
        Bitmap[] fWhole, fTop, fBottom;
        float fSpriteR = -1;
        readonly Dictionary<int, Blade> blades = new Dictionary<int, Blade>();
        readonly List<Blade> oldBlades = new List<Blade>();
        Font fScoreFont, fPopFont, fPopBig;

        bool fLooping;
        readonly Stopwatch fsw = new Stopwatch();
        double fLast, fAcc, fLastRender;
        BufferedGraphicsContext fBufCtx;
        BufferedGraphics fBuf;
        Graphics fTarget;

        // ---------- layout ----------

        void LayoutFruits()
        {
            Font[] old = { fScoreFont, fPopFont, fPopBig };
            foreach (Font f in old) if (f != null) f.Dispose();
            fScoreFont = F(44, FontStyle.Bold);
            fPopFont = F(26, FontStyle.Bold);
            fPopBig = F(40, FontStyle.Bold);

            int W = view.Width, H = view.Height;
            fBack = new Rectangle(R(24), R(24), R(56), R(56));
            fPause = new Rectangle(W - R(24) - R(64), R(24), R(64), R(64));
            if (!portrait)
            {
                int w = R(440), h = R(400), gap = R(40), x0 = (W - 2 * w - gap) / 2, y = R(236);
                fKidsBtn = new Rectangle(x0, y, w, h);
                fHardBtn = new Rectangle(x0 + w + gap, y, w, h);
            }
            else
            {
                int w = W - 2 * R(64), h = R(420), y = R(236);
                fKidsBtn = new Rectangle(R(64), y, w, h);
                fHardBtn = new Rectangle(R(64), y + h + R(36), w, h);
            }
            fPanel = Center(new Rectangle(Point.Empty, view), Math.Min(W - R(40), R(520)), R(fstate == FState.Fim ? 400 : 300));
            fBtnB = new Rectangle(fPanel.Right - R(40) - R(140), fPanel.Bottom - R(36) - R(68), R(140), R(68));
            fBtnA = new Rectangle(fPanel.X + R(40), fBtnB.Y, fBtnB.X - R(12) - fPanel.X - R(40), R(68));

            if (fruits != null) { fruits.W = W / sc; fruits.H = H / sc; }
            if (fLooping) AllocFruitBuffer();
        }

        // ---------- partida ----------

        void EnterFruits()
        {
            StopFruitLoop();
            fstate = FState.Menu;
            fruits = null;
            blades.Clear();
            oldBlades.Clear();
            LayoutFruits();
        }

        void StartFruits(bool kids)
        {
            fKids = kids;
            fruits = new Fruits(kids, view.Width / sc, view.Height / sc, Environment.TickCount);
            blades.Clear();
            oldBlades.Clear();
            fstate = FState.Jogando;
            LayoutFruits();
            Invalidate();
            StartFruitLoop();
        }

        void StartFruitLoop()
        {
            if (fLooping || CaptureDir != null) return;
            fLooping = true;
            timeBeginPeriod(1);
            fsw.Reset();
            fsw.Start();
            fLast = fAcc = fLastRender = 0;
            AllocFruitBuffer();
            Application.Idle += OnFruitIdle;
        }

        void StopFruitLoop()
        {
            if (!fLooping) return;
            fLooping = false;
            Application.Idle -= OnFruitIdle;
            timeEndPeriod(1);
            if (fBuf != null) { fBuf.Dispose(); fBuf = null; }
            if (fBufCtx != null) { fBufCtx.Dispose(); fBufCtx = null; }
            if (fTarget != null) { fTarget.Dispose(); fTarget = null; }
        }

        void AllocFruitBuffer()
        {
            if (fBuf != null) { fBuf.Dispose(); fBuf = null; }
            if (fBufCtx != null) { fBufCtx.Dispose(); fBufCtx = null; }
            if (fTarget != null) { fTarget.Dispose(); fTarget = null; }
            if (!IsHandleCreated) return;
            fTarget = CreateGraphics();
            fBufCtx = new BufferedGraphicsContext();
            fBufCtx.MaximumBuffer = new Size(view.Width + 1, view.Height + 1);
            fBuf = fBufCtx.Allocate(fTarget, new Rectangle(Point.Empty, view));
        }

        void OnFruitIdle(object sender, EventArgs e)
        {
            NativeMessage m;
            while (fLooping && !PeekMessage(out m, IntPtr.Zero, 0, 0, 0)) FruitFrame();
        }

        void FruitFrame()
        {
            const double step = 1 / 60.0;
            double now = fsw.Elapsed.TotalSeconds;
            fAcc += Math.Min(0.1, now - fLast);
            fLast = now;
            int steps = 0;
            while (fAcc >= step && steps < 4)
            {
                fruits.Step((float)step);
                fAcc -= step;
                steps++;
            }
            if (steps == 4) fAcc = 0;
            if (steps > 0) CheckCombos();
            if (fruits.Over) { FruitsOver(); return; }

            if (now - fLastRender >= step - 0.001)
            {
                fLastRender = now;
                if (fBuf != null)
                {
                    RenderFruits(fBuf.Graphics);
                    fBuf.Render();
                }
            }
            else Thread.Sleep(1);
        }

        // Combo: 3 ou mais frutas no mesmo traço (até 0,25 s entre um corte e outro).
        void CheckCombos()
        {
            foreach (Blade b in blades.Values) FlushCombo(b, false);
            foreach (Blade b in oldBlades) FlushCombo(b, false);
            for (int i = oldBlades.Count - 1; i >= 0; i--)
            {
                Blade b = oldBlades[i];
                if (b.Combo == 0 && (b.Times.Count == 0 || fruits.Time - b.Times[b.Times.Count - 1] > 0.3f)) oldBlades.RemoveAt(i);
            }
        }

        void FlushCombo(Blade b, bool now)
        {
            if (b.Combo == 0 || (!now && fruits.Time - b.LastCut < 0.25f)) return;
            fruits.Combo(b.Combo, b.ComboAt);
            b.Combo = 0;
        }

        void FruitsOver()
        {
            StopFruitLoop();
            fstate = FState.Fim;
            fRecord = fruits.Score > Config.FruitRecord;
            if (fRecord)
            {
                Config.FruitRecord = fruits.Score;
                Config.Save();
            }
            LayoutFruits();
            Invalidate();
        }

        void PauseFruits()
        {
            if (fstate != FState.Jogando) return;
            StopFruitLoop();
            fstate = FState.Pausado;
            LayoutFruits();
            Invalidate();
        }

        void ResumeFruits()
        {
            fstate = FState.Jogando;
            LayoutFruits();
            Invalidate();
            StartFruitLoop();
        }

        // ---------- frutas desenhadas ----------

        static GraphicsPath Ell(float rx, float ry)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddEllipse(-rx, -ry, 2 * rx, 2 * ry);
            return p;
        }

        static GraphicsPath StarPath(float ro, float ri, int n)
        {
            PointF[] pts = new PointF[n * 2];
            for (int i = 0; i < n * 2; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / n;
                float rr = i % 2 == 0 ? ro : ri;
                pts[i] = new PointF((float)Math.Cos(a) * rr, (float)Math.Sin(a) * rr);
            }
            GraphicsPath p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        static readonly float[] AppleUnit = {
            0, -0.62f, 0.35f, -0.95f, 1f, -0.85f, 0.98f, -0.15f, 0.96f, 0.55f, 0.5f, 0.98f, 0.2f, 0.92f,
            0.1f, 0.9f, -0.1f, 0.9f, -0.2f, 0.92f, -0.5f, 0.98f, -0.96f, 0.55f, -0.98f, -0.15f,
            -1f, -0.85f, -0.35f, -0.95f, 0, -0.62f };

        static GraphicsPath FruitShape(int k, float r)
        {
            switch (k)
            {
                case Fruits.Melancia: return Ell(r * 1.08f, r * 0.86f);
                case Fruits.Maca:
                    {
                        PointF[] pts = new PointF[AppleUnit.Length / 2];
                        for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(AppleUnit[2 * i] * r, AppleUnit[2 * i + 1] * r);
                        GraphicsPath p = new GraphicsPath();
                        p.AddBeziers(pts);
                        p.CloseFigure();
                        return p;
                    }
                case Fruits.Limao: return Ell(r * 0.96f, r * 0.78f);
                case Fruits.Kiwi: return Ell(r, r * 0.8f);
                case Fruits.Carambola: return StarPath(r, r * 0.52f, 5);
                case Fruits.Ourico: return Ell(r * 0.66f, r * 0.66f);
                default: return Ell(r, r);
            }
        }

        static float FLine(float r) { return Math.Max(1.5f, r * 0.07f); }

        static void FFill(Graphics g, GraphicsPath p, Color c)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        static void FEllipse(Graphics g, Color c, float x, float y, float rx, float ry)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x - rx, y - ry, 2 * rx, 2 * ry);
        }

        static void FOutline(Graphics g, GraphicsPath p, float r)
        {
            using (Pen pen = Shapes.RoundPen(FInk, FLine(r))) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
        }

        static void FShine(Graphics g, float x, float y, float rx, float ry)
        {
            FEllipse(g, Color.FromArgb(110, 255, 255, 255), x, y, rx, ry);
        }

        static void Leaf(Graphics g, float x, float y, float r, float ang)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(ang);
            using (GraphicsPath p = Ell(r * 0.3f, r * 0.13f))
            {
                g.TranslateTransform(r * 0.28f, 0);
                FFill(g, p, Shapes.Hex(0x66bb6a));
                using (Pen pen = Shapes.RoundPen(FInk, FLine(r) * 0.8f)) g.DrawPath(pen, p);
            }
            g.Restore(st);
        }

        static void Stem(Graphics g, float x0, float y0, float x1, float y1, float r)
        {
            using (Pen p = Shapes.RoundPen(FInk, r * 0.13f)) g.DrawLine(p, x0, y0, x1, y1);
            using (Pen p = Shapes.RoundPen(Shapes.Hex(0x795548), r * 0.07f)) g.DrawLine(p, x0, y0, x1, y1);
        }

        // Fruta inteira, centrada em (0,0), raio r em pixels.
        static void DrawFruitWhole(Graphics g, int k, float r)
        {
            using (GraphicsPath shape = FruitShape(k, r))
            {
                switch (k)
                {
                    case Fruits.Melancia:
                        FFill(g, shape, Shapes.Hex(0x43a047));
                        {
                            GraphicsState st = g.Save();
                            g.SetClip(shape);
                            using (Pen p = Shapes.RoundPen(Shapes.Hex(0x1b5e20), r * 0.14f))
                                for (int i = -2; i <= 2; i++)
                                    g.DrawBezier(p, i * r * 0.3f, -r, i * r * 0.55f, -r * 0.3f, i * r * 0.55f, r * 0.3f, i * r * 0.3f, r);
                            g.Restore(st);
                        }
                        FShine(g, -r * 0.5f, -r * 0.5f, r * 0.24f, r * 0.11f);
                        break;
                    case Fruits.Laranja:
                        FFill(g, shape, Shapes.Hex(0xfb8c00));
                        {
                            float[] dots = { 0.3f, 0.2f, -0.4f, 0.35f, 0.1f, 0.6f, -0.2f, -0.1f, 0.55f, -0.3f, -0.55f, -0.25f, 0.45f, 0.55f };
                            for (int i = 0; i < dots.Length; i += 2) FEllipse(g, Shapes.Hex(0xef6c00), dots[i] * r, dots[i + 1] * r, r * 0.04f, r * 0.04f);
                        }
                        FShine(g, -r * 0.45f, -r * 0.45f, r * 0.22f, r * 0.12f);
                        FOutline(g, shape, r);
                        Stem(g, 0, -r * 0.92f, r * 0.05f, -r * 1.12f, r);
                        Leaf(g, r * 0.04f, -r * 1.02f, r, -25);
                        return;
                    case Fruits.Maca:
                        FFill(g, shape, Shapes.Hex(0xe53935));
                        FShine(g, -r * 0.5f, -r * 0.35f, r * 0.14f, r * 0.24f);
                        FOutline(g, shape, r);
                        Stem(g, 0, -r * 0.6f, r * 0.1f, -r * 1.02f, r);
                        Leaf(g, r * 0.08f, -r * 0.85f, r, -20);
                        return;
                    case Fruits.Limao:
                        using (GraphicsPath nub = new GraphicsPath())
                        {
                            nub.AddEllipse(r * 0.82f, -r * 0.14f, r * 0.26f, r * 0.28f);
                            nub.AddEllipse(-r * 1.08f, -r * 0.14f, r * 0.26f, r * 0.28f);
                            FFill(g, nub, Shapes.Hex(0x7cb342));
                            FOutline(g, nub, r);
                        }
                        FFill(g, shape, Shapes.Hex(0x7cb342));
                        {
                            float[] dots = { 0.3f, 0.2f, -0.4f, 0.3f, 0.1f, 0.5f, -0.2f, -0.1f, 0.5f, -0.25f, -0.5f, -0.2f };
                            for (int i = 0; i < dots.Length; i += 2) FEllipse(g, Shapes.Hex(0x689f38), dots[i] * r, dots[i + 1] * r, r * 0.04f, r * 0.04f);
                        }
                        FShine(g, -r * 0.4f, -r * 0.4f, r * 0.22f, r * 0.1f);
                        break;
                    case Fruits.Kiwi:
                        FFill(g, shape, Shapes.Hex(0x8d6e63));
                        {
                            Random fr = new Random(7);
                            using (Pen p = Shapes.RoundPen(Shapes.Hex(0x6d4c41), r * 0.035f))
                                for (int i = 0; i < 26; i++)
                                {
                                    double a = fr.NextDouble() * Math.PI * 2, d = Math.Sqrt(fr.NextDouble()) * 0.8;
                                    float x = (float)(Math.Cos(a) * d) * r, y = (float)(Math.Sin(a) * d) * r * 0.78f;
                                    g.DrawLine(p, x, y, x + r * 0.06f, y + r * 0.03f);
                                }
                        }
                        FShine(g, -r * 0.45f, -r * 0.4f, r * 0.2f, r * 0.09f);
                        break;
                    case Fruits.Coco:
                        FFill(g, shape, Shapes.Hex(0x6d4c41));
                        using (Pen p = Shapes.RoundPen(Shapes.Hex(0x5d4037), r * 0.05f))
                        {
                            g.DrawArc(p, -r * 0.8f, -r * 0.7f, r * 1.6f, r * 1.6f, 200, 60);
                            g.DrawArc(p, -r * 0.6f, -r * 0.2f, r * 1.4f, r * 1.1f, 20, 70);
                            g.DrawArc(p, -r * 0.9f, -r * 0.1f, r * 1.2f, r * 0.9f, 110, 50);
                        }
                        FEllipse(g, Shapes.Hex(0x3e2723), -r * 0.22f, -r * 0.45f, r * 0.11f, r * 0.13f);
                        FEllipse(g, Shapes.Hex(0x3e2723), r * 0.22f, -r * 0.45f, r * 0.11f, r * 0.13f);
                        FEllipse(g, Shapes.Hex(0x3e2723), 0, -r * 0.2f, r * 0.11f, r * 0.13f);
                        FShine(g, -r * 0.55f, -r * 0.2f, r * 0.1f, r * 0.2f);
                        break;
                    case Fruits.Carambola:
                        FFill(g, shape, Shapes.Hex(0xffca28));
                        using (Pen p = Shapes.RoundPen(Shapes.Hex(0xf9a825), r * 0.06f))
                            for (int i = 0; i < 5; i++)
                            {
                                double a = -Math.PI / 2 + i * Math.PI * 2 / 5;
                                g.DrawLine(p, 0, 0, (float)Math.Cos(a) * r * 0.8f, (float)Math.Sin(a) * r * 0.8f);
                            }
                        FShine(g, -r * 0.2f, -r * 0.45f, r * 0.08f, r * 0.16f);
                        break;
                    case Fruits.Ourico:
                        using (GraphicsPath sp = new GraphicsPath())
                        {
                            for (int i = 0; i < 16; i++)
                            {
                                double a = i * Math.PI * 2 / 16;
                                sp.AddPolygon(new PointF[] {
                                    new PointF((float)Math.Cos(a - 0.17) * r * 0.55f, (float)Math.Sin(a - 0.17) * r * 0.55f),
                                    new PointF((float)Math.Cos(a) * r * 1.02f, (float)Math.Sin(a) * r * 1.02f),
                                    new PointF((float)Math.Cos(a + 0.17) * r * 0.55f, (float)Math.Sin(a + 0.17) * r * 0.55f) });
                            }
                            FFill(g, sp, Shapes.Hex(0x311b92));
                            FOutline(g, sp, r * 0.7f);
                        }
                        FFill(g, shape, Shapes.Hex(0x7e57c2));
                        FOutline(g, shape, r);
                        FEllipse(g, Color.White, -r * 0.24f, -r * 0.04f, r * 0.17f, r * 0.17f);
                        FEllipse(g, Color.White, r * 0.24f, -r * 0.04f, r * 0.17f, r * 0.17f);
                        FEllipse(g, FInk, -r * 0.2f, 0, r * 0.08f, r * 0.08f);
                        FEllipse(g, FInk, r * 0.2f, 0, r * 0.08f, r * 0.08f);
                        using (Pen p = Shapes.RoundPen(FInk, r * 0.07f))
                        {
                            g.DrawLine(p, -r * 0.42f, -r * 0.3f, -r * 0.1f, -r * 0.18f);
                            g.DrawLine(p, r * 0.42f, -r * 0.3f, r * 0.1f, -r * 0.18f);
                            g.DrawArc(p, -r * 0.16f, r * 0.24f, r * 0.32f, r * 0.2f, 200, 140);
                        }
                        return;
                }
                FOutline(g, shape, r);
            }
        }

        // Fruta cortada: a "fatia" vista de frente (as metades usam metade disto).
        static void DrawFruitInside(Graphics g, int k, float r)
        {
            using (GraphicsPath shape = FruitShape(k, r))
            {
                switch (k)
                {
                    case Fruits.Melancia:
                        FFill(g, shape, Shapes.Hex(0x43a047));
                        using (GraphicsPath p = Ell(r * 0.96f, r * 0.74f)) FFill(g, p, Shapes.Hex(0xe6f5c8));
                        using (GraphicsPath p = Ell(r * 0.88f, r * 0.66f)) FFill(g, p, Shapes.Hex(0xff5252));
                        for (int i = 0; i < 10; i++)
                        {
                            double a = i * Math.PI * 2 / 10 + 0.3;
                            float x = (float)Math.Cos(a) * r * 0.55f, y = (float)Math.Sin(a) * r * 0.4f;
                            FEllipse(g, FInk, x, y, r * 0.045f, r * 0.075f);
                        }
                        break;
                    case Fruits.Laranja:
                    case Fruits.Limao:
                        {
                            bool orange = k == Fruits.Laranja;
                            FFill(g, shape, orange ? Shapes.Hex(0xfb8c00) : Shapes.Hex(0x7cb342));
                            float sx = orange ? 1 : 0.96f, sy = orange ? 1 : 0.78f;
                            using (GraphicsPath p = Ell(r * 0.9f * sx, r * 0.9f * sy)) FFill(g, p, orange ? Shapes.Hex(0xfff3e0) : Shapes.Hex(0xf1f8e9));
                            using (GraphicsPath p = Ell(r * 0.82f * sx, r * 0.82f * sy)) FFill(g, p, orange ? Shapes.Hex(0xffb74d) : Shapes.Hex(0xd4e157));
                            using (Pen p = Shapes.RoundPen(Color.FromArgb(220, 255, 255, 255), r * 0.05f))
                                for (int i = 0; i < 10; i++)
                                {
                                    double a = i * Math.PI * 2 / 10;
                                    g.DrawLine(p, 0, 0, (float)Math.Cos(a) * r * 0.8f * sx, (float)Math.Sin(a) * r * 0.8f * sy);
                                }
                            FEllipse(g, Color.White, 0, 0, r * 0.1f, r * 0.1f * sy);
                        }
                        break;
                    case Fruits.Maca:
                        FFill(g, shape, Shapes.Hex(0xe53935));
                        using (GraphicsPath p = FruitShape(k, r * 0.86f)) FFill(g, p, Shapes.Hex(0xfff3d6));
                        using (Pen p = Shapes.RoundPen(Shapes.Hex(0xe0c48c), r * 0.04f)) g.DrawEllipse(p, -r * 0.18f, -r * 0.3f, r * 0.36f, r * 0.62f);
                        FEllipse(g, Shapes.Hex(0x5d4037), -r * 0.07f, 0, r * 0.05f, r * 0.09f);
                        FEllipse(g, Shapes.Hex(0x5d4037), r * 0.07f, 0, r * 0.05f, r * 0.09f);
                        break;
                    case Fruits.Kiwi:
                        FFill(g, shape, Shapes.Hex(0x8d6e63));
                        using (GraphicsPath p = Ell(r * 0.9f, r * 0.7f)) FFill(g, p, Shapes.Hex(0x7cb342));
                        using (GraphicsPath p = Ell(r * 0.48f, r * 0.38f)) FFill(g, p, Shapes.Hex(0xc5e1a5));
                        FEllipse(g, Shapes.Hex(0xfffde7), 0, 0, r * 0.22f, r * 0.14f);
                        for (int i = 0; i < 14; i++)
                        {
                            double a = i * Math.PI * 2 / 14;
                            FEllipse(g, FInk, (float)Math.Cos(a) * r * 0.36f, (float)Math.Sin(a) * r * 0.27f, r * 0.035f, r * 0.035f);
                        }
                        break;
                    case Fruits.Coco:
                        FFill(g, shape, Shapes.Hex(0x6d4c41));
                        using (GraphicsPath p = Ell(r * 0.86f, r * 0.86f)) FFill(g, p, Shapes.Hex(0xfafafa));
                        using (GraphicsPath p = Ell(r * 0.6f, r * 0.6f)) FFill(g, p, Shapes.Hex(0xd6ecfa));
                        FShine(g, -r * 0.2f, -r * 0.2f, r * 0.16f, r * 0.08f);
                        break;
                    case Fruits.Carambola:
                        FFill(g, shape, Shapes.Hex(0xffd54f));
                        using (GraphicsPath p = StarPath(r * 0.82f, r * 0.42f, 5)) FFill(g, p, Shapes.Hex(0xfff59d));
                        for (int i = 0; i < 5; i++)
                        {
                            double a = -Math.PI / 2 + i * Math.PI * 2 / 5;
                            FEllipse(g, Shapes.Hex(0x8d6e63), (float)Math.Cos(a) * r * 0.32f, (float)Math.Sin(a) * r * 0.32f, r * 0.04f, r * 0.04f);
                        }
                        break;
                    default:
                        DrawFruitWhole(g, k, r);
                        return;
                }
                FOutline(g, shape, r);
            }
        }

        Bitmap FruitSprite(float r, Action<Graphics> draw)
        {
            int s = (int)Math.Ceiling(r * 2.6f);
            Bitmap b = new Bitmap(s, s, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(s / 2f, s / 2f);
                draw(g);
            }
            return b;
        }

        void BuildFruitSprites(float r)
        {
            if (fSpriteR == r && fWhole != null) return;
            if (fWhole != null)
                for (int i = 0; i < Fruits.Count; i++) { fWhole[i].Dispose(); fTop[i].Dispose(); fBottom[i].Dispose(); }
            fSpriteR = r;
            fWhole = new Bitmap[Fruits.Count];
            fTop = new Bitmap[Fruits.Count];
            fBottom = new Bitmap[Fruits.Count];
            for (int i = 0; i < Fruits.Count; i++)
            {
                int k = i;
                fWhole[i] = FruitSprite(r, delegate(Graphics g) { DrawFruitWhole(g, k, r); });
                fTop[i] = FruitSprite(r, delegate(Graphics g) { g.SetClip(new RectangleF(-2 * r, -2 * r, 4 * r, 2 * r)); DrawFruitInside(g, k, r); });
                fBottom[i] = FruitSprite(r, delegate(Graphics g) { g.SetClip(new RectangleF(-2 * r, 0, 4 * r, 2 * r)); DrawFruitInside(g, k, r); });
            }
        }

        // Toalha de piquenique xadrez (pré-desenhada no tamanho da tela).
        void EnsureFruitBg()
        {
            if (fBg != null && fBg.Width == view.Width && fBg.Height == view.Height) return;
            if (fBg != null) fBg.Dispose();
            fBg = new Bitmap(Math.Max(1, view.Width), Math.Max(1, view.Height), PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(fBg))
            {
                g.Clear(FCream);
                int t = Math.Max(8, R(44));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 0xe5, 0x53, 0x3e)))
                {
                    for (int x = 0; x < view.Width; x += 2 * t) g.FillRectangle(b, x, 0, t, view.Height);
                    for (int y = 0; y < view.Height; y += 2 * t) g.FillRectangle(b, 0, y, view.Width, t);
                }
                using (Pen p = new Pen(Color.FromArgb(18, 0x2b, 0x21, 0x18), 1))
                    for (int x = t / 2; x < view.Width; x += t) g.DrawLine(p, x, 0, x, view.Height);
            }
        }

        // ---------- desenho ----------

        void PaintFruits(Graphics g, Rectangle clip)
        {
            if (fstate == FState.Menu) { DrawFruitMenu(g); return; }
            RenderFruits(g);
            if (fstate == FState.Pausado || fstate == FState.Fim) DrawFruitCard(g);
        }

        void RenderFruits(Graphics g)
        {
            EnsureFruitBg();
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.SmoothingMode = SmoothingMode.None;
            g.DrawImageUnscaled(fBg, 0, 0);
            if (fruits == null) return;
            float k = sc;
            float ox = 0, oy = 0;
            if (fruits.Shake > 0)
            {
                ox = (float)(rnd.NextDouble() - 0.5) * 24 * k * fruits.Shake;
                oy = (float)(rnd.NextDouble() - 0.5) * 24 * k * fruits.Shake;
            }

            // manchas de suco na toalha
            foreach (Splat s in fruits.Splats)
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(120 * Math.Min(1, s.Life * 1.5f)), s.C)))
                {
                    float r = s.Size * k / 2, x = s.X * k + ox, y = s.Y * k + oy;
                    g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
                    for (int i = 0; i < 5; i++)
                    {
                        double a = s.Seed + i * 1.3;
                        float d = r * (1.1f + (i % 3) * 0.25f), rr = r * (0.18f + (i % 2) * 0.12f);
                        g.FillEllipse(b, x + (float)Math.Cos(a) * d - rr, y + (float)Math.Sin(a) * d - rr, 2 * rr, 2 * rr);
                    }
                }

            BuildFruitSprites(fruits.R * k);
            foreach (FruitPiece p in fruits.Pieces)
            {
                if (p.Dead) continue;
                Bitmap b = p.Half == 0 ? fWhole[p.Kind] : p.Half == 1 ? fTop[p.Kind] : fBottom[p.Kind];
                g.TranslateTransform(p.X * k + ox, p.Y * k + oy);
                g.RotateTransform(p.Rot);
                g.DrawImage(b, new RectangleF(-b.Width / 2f, -b.Height / 2f, b.Width, b.Height));
                g.ResetTransform();
            }

            foreach (Drop d in fruits.Drops)
                using (SolidBrush b = new SolidBrush(d.C))
                {
                    float r = d.Size * k * Math.Min(1, d.Life * 2) / 2;
                    g.FillEllipse(b, d.X * k + ox - r, d.Y * k + oy - r, 2 * r, 2 * r);
                }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (Blade b in blades.Values) DrawBlade(g, b);
            foreach (Blade b in oldBlades) DrawBlade(g, b);

            foreach (Popup p in fruits.Popups)
            {
                Font f = p.Big ? fPopBig : fPopFont;
                int a = (int)(255 * Math.Min(1, p.Life * 3));
                RectangleF r = new RectangleF(p.X * k - R(300), p.Y * k - R(40), R(600), R(80));
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(a, Color.White)))
                    for (int i = 0; i < 4; i++)
                    {
                        RectangleF rs = r;
                        rs.Offset(i < 2 ? (i == 0 ? -R(3) : R(3)) : 0, i >= 2 ? (i == 2 ? -R(3) : R(3)) : 0);
                        g.DrawString(p.Text, f, sh, rs, CenterFmt);
                    }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a, p.C))) g.DrawString(p.Text, f, b, r, CenterFmt);
            }

            if (fruits.Flash > 0)
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(110 * fruits.Flash), 0xe5, 0x53, 0x3e)))
                    g.FillRectangle(b, 0, 0, view.Width, view.Height);

            DrawFruitHud(g);
        }

        void DrawBlade(Graphics g, Blade b)
        {
            int n = b.Pts.Count;
            if (n < 2) return;
            float now = fruits.Time, life = 0.16f;
            float k = sc;
            // primeiro o brilho de todos os pedaços, depois o miolo branco (cores opacas: sem emendas)
            for (int pass = 0; pass < 2; pass++)
                for (int i = 1; i < n; i++)
                {
                    float age = now - b.Times[i];
                    if (age > life) continue;
                    float t = 1 - age / life; // 1 = ponta do dedo
                    float w = (fKids ? 16 : 11) * k * t;
                    PointF a = new PointF(b.Pts[i - 1].X * k, b.Pts[i - 1].Y * k), z = new PointF(b.Pts[i].X * k, b.Pts[i].Y * k);
                    if (pass == 0)
                    {
                        Color c = fKids ? Shapes.FromHsv((b.Times[i] * 600) % 360, 0.75f, 1f) : Shapes.Hex(0xb3e5fc);
                        using (Pen p = Shapes.RoundPen(c, w + 6 * k)) g.DrawLine(p, a, z);
                    }
                    else using (Pen p = Shapes.RoundPen(Color.White, Math.Max(1, w * 0.55f))) g.DrawLine(p, a, z);
                }
        }

        void DrawFruitHud(Graphics g)
        {
            DrawBackButton(g, fBack, null);
            float x = fBack.Right + R(20);
            DrawText(g, fKids ? "FRUTAS" : "PONTOS", sectionFont, Shapes.Hex(0x7a6a5c), new RectangleF(x, R(16), R(200), R(18)), LeftFmt);
            DrawText(g, fruits.Score.ToString(), fScoreFont, FInk, new RectangleF(x - R(2), R(32), R(260), R(56)), LeftFmt);
            if (fKids) return;

            RoundBox(g, fPause, 18 * sc, Color.White, Shapes.Hex(0xe8dccb), 2 * sc);
            using (SolidBrush b = new SolidBrush(FInk))
            {
                g.FillRectangle(b, fPause.X + fPause.Width / 2 - R(12), fPause.Y + R(19), R(8), R(26));
                g.FillRectangle(b, fPause.X + fPause.Width / 2 + R(4), fPause.Y + R(19), R(8), R(26));
            }
            for (int i = 0; i < 3; i++)
            {
                float hx = fPause.X - R(24) - (3 - i) * R(52), hy = R(34);
                DrawHeart(g, hx, hy, R(44), i < fruits.Lives);
            }
            if (Config.FruitRecord > 0)
                DrawText(g, "Recorde " + Config.FruitRecord, labelFont, Shapes.Hex(0x7a6a5c), new RectangleF(fPause.X - R(24) - R(300), R(84), R(300), R(24)), RightFmt);
        }

        void DrawHeart(Graphics g, float x, float y, float s, bool full)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(x + s / 2, y + s * 0.3f, x + s / 2, y, x, y, x, y + s * 0.3f);
                p.AddBezier(x, y + s * 0.3f, x, y + s * 0.6f, x + s / 2, y + s * 0.75f, x + s / 2, y + s * 0.95f);
                p.AddBezier(x + s / 2, y + s * 0.95f, x + s / 2, y + s * 0.75f, x + s, y + s * 0.6f, x + s, y + s * 0.3f);
                p.AddBezier(x + s, y + s * 0.3f, x + s, y, x + s / 2, y, x + s / 2, y + s * 0.3f);
                using (SolidBrush b = new SolidBrush(full ? FRed : Color.FromArgb(60, FInk))) g.FillPath(b, p);
                if (full) using (Pen pen = new Pen(FInk, 2.5f * sc)) g.DrawPath(pen, p);
            }
        }

        void DrawFruitMenu(Graphics g)
        {
            EnsureFruitBg();
            g.DrawImageUnscaled(fBg, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle back = new Rectangle(R(40), R(36), R(170), R(52));
            RoundBox(g, back, 14 * sc, Color.White, Shapes.Hex(0xe8dccb), 2 * sc);
            using (Pen p = Shapes.RoundPen(FInk, 2.4f * sc))
                g.DrawLines(p, new PointF[] { new PointF(back.X + R(26), back.Y + back.Height / 2f - R(8)), new PointF(back.X + R(19), back.Y + back.Height / 2f), new PointF(back.X + R(26), back.Y + back.Height / 2f + R(8)) });
            DrawText(g, "Joguinhos", labelFont, FInk, new RectangleF(back.X + R(40), back.Y, back.Width - R(48), back.Height), LeftFmt);

            DrawText(g, "Salada de Frutas", bigFont, FInk, new RectangleF(0, R(60), view.Width, R(84)), CenterFmt);
            DrawText(g, "Passe o dedo para cortar as frutas", subtitleFont, Shapes.Hex(0x7a6a5c), new RectangleF(0, R(146), view.Width, R(40)), CenterFmt);

            DrawModeCard(g, fKidsBtn, FGreen, "Pequenos", "Nunca perde", "Frutas grandes e devagar, sem ouriço. Um toque já corta!", true);
            DrawModeCard(g, fHardBtn, FRed, "Desafio", Config.FruitRecord > 0 ? "Recorde: " + Config.FruitRecord : "3 vidas",
                "Não deixe a fruta cair e fuja do ouriço. Cortou 3 de uma vez? Combo!", false);
            DrawLockButton(g, homeLock, 18 * sc);
        }

        void DrawModeCard(Graphics g, Rectangle r, Color accent, string title, string chip, string desc, bool kids)
        {
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(30, FInk)))
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(r.X, r.Y + R(6), r.Width, r.Height), 28 * sc)) g.FillPath(sh, p);
            RoundBox(g, r, 28 * sc, Color.White, accent, 4 * sc);
            int pad = R(24);
            Rectangle img = new Rectangle(r.X + pad, r.Y + pad, r.Width - 2 * pad, R(190));
            RoundBox(g, img, 18 * sc, Color.FromArgb(40, accent), Color.Empty, 0);
            GraphicsState st = g.Save();
            g.SetClip(img);
            float cx = img.X + img.Width / 2f, cy = img.Y + img.Height / 2f, fr = R(kids ? 52 : 40);
            if (kids)
            {
                DrawFruitAt(g, Fruits.Melancia, cx - R(110), cy + R(10), fr, -12, 0);
                DrawFruitAt(g, Fruits.Maca, cx + R(10), cy - R(10), fr, 8, 0);
                DrawFruitAt(g, Fruits.Laranja, cx + R(130), cy + R(18), fr * 0.9f, 0, 1);
                DrawFruitAt(g, Fruits.Laranja, cx + R(124), cy + R(34), fr * 0.9f, 0, 2);
            }
            else
            {
                DrawFruitAt(g, Fruits.Kiwi, cx - R(130), cy, fr, 20, 1);
                DrawFruitAt(g, Fruits.Kiwi, cx - R(118), cy + R(22), fr, 20, 2);
                DrawFruitAt(g, Fruits.Ourico, cx - R(10), cy - R(14), fr * 1.15f, 0, 0);
                DrawFruitAt(g, Fruits.Carambola, cx + R(120), cy + R(10), fr, 10, 0);
                using (Pen p = Shapes.RoundPen(Color.White, R(6))) g.DrawLine(p, cx - R(190), cy + R(60), cx - R(60), cy - R(50));
            }
            g.Restore(st);
            RectangleF titleR = new RectangleF(r.X + pad, img.Bottom + R(16), r.Width - 2 * pad, R(44));
            DrawText(g, title, cardTitleFont, FInk, titleR, LeftFmt);
            Size cs = TextRenderer.MeasureText(chip, labelFont);
            Rectangle chipR = new Rectangle((int)titleR.Right - cs.Width - R(24), (int)(titleR.Y + (titleR.Height - R(30)) / 2), cs.Width + R(24), R(30));
            RoundBox(g, chipR, chipR.Height / 2f, accent, accent, 0);
            DrawText(g, chip, labelFont, Color.White, chipR, CenterFmt);
            DrawText(g, desc, bodyFont, Shapes.Hex(0x5a4a3c), new RectangleF(r.X + pad, titleR.Bottom + R(10), r.Width - 2 * pad, r.Bottom - titleR.Bottom - R(10) - pad), WrapFmt);
        }

        // Fruta vetorial (cartões); half: 0 inteira, 1/2 metades.
        static void DrawFruitAt(Graphics g, int kind, float x, float y, float r, float rot, int half)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(rot);
            if (half == 0) DrawFruitWhole(g, kind, r);
            else
            {
                g.SetClip(new RectangleF(-2 * r, half == 1 ? -2 * r : 0, 4 * r, 2 * r), CombineMode.Intersect);
                DrawFruitInside(g, kind, r);
            }
            g.Restore(st);
        }

        // Cartão da escolha de jogo.
        void DrawFruitArt(Graphics g, Rectangle img)
        {
            GraphicsState st = g.Save();
            using (GraphicsPath clip = Shapes.RoundRect(img, 18 * sc)) g.SetClip(clip, CombineMode.Intersect);
            using (SolidBrush b = new SolidBrush(FCream)) g.FillRectangle(b, img);
            int t = R(28);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 0xe5, 0x53, 0x3e)))
            {
                for (int x = img.X; x < img.Right; x += 2 * t) g.FillRectangle(b, x, img.Y, t, img.Height);
                for (int y = img.Y; y < img.Bottom; y += 2 * t) g.FillRectangle(b, img.X, y, img.Width, t);
            }
            float cx = img.X + img.Width / 2f, cy = img.Y + img.Height / 2f, r = Math.Min(img.Height * 0.2f, img.Width * 0.12f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(110, Fruits.Juice[0]))) g.FillEllipse(b, cx - r * 2.6f, cy - r * 0.2f, r * 1.8f, r * 1.4f);
            DrawFruitAt(g, Fruits.Melancia, cx - r * 1.9f, cy - r * 0.55f, r, -20, 1);
            DrawFruitAt(g, Fruits.Melancia, cx - r * 1.6f, cy + r * 0.35f, r, -20, 2);
            DrawFruitAt(g, Fruits.Laranja, cx + r * 0.2f, cy - r * 0.3f, r * 0.9f, 10, 0);
            DrawFruitAt(g, Fruits.Maca, cx + r * 2.1f, cy + r * 0.25f, r * 0.85f, -8, 0);
            using (Pen p = Shapes.RoundPen(Color.FromArgb(200, Color.White), r * 0.14f)) g.DrawLine(p, cx - r * 3, cy + r * 1.3f, cx - r * 0.4f, cy - r * 1.4f);
            g.Restore(st);
        }

        void DrawFruitCard(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(150, 43, 33, 24))) g.FillRectangle(dim, new Rectangle(Point.Empty, view));
            RoundBox(g, fPanel, 28 * sc, Color.White, Shapes.Hex(0xe8dccb), 2 * sc);
            float y = fPanel.Y + R(32);
            if (fstate == FState.Pausado)
            {
                DrawText(g, "Pausado", titleFont, FInk, new RectangleF(fPanel.X, y + R(20), fPanel.Width, R(52)), CenterFmt);
                RoundBox(g, fBtnA, 18 * sc, FGreen, Shapes.Hex(0x3f9a50), 3 * sc);
                DrawText(g, "Continuar", buttonFont, Color.White, fBtnA, CenterFmt);
            }
            else
            {
                if (fRecord)
                {
                    Size ts = TextRenderer.MeasureText("Novo recorde!", labelFont);
                    Rectangle badge = new Rectangle(fPanel.X + (fPanel.Width - ts.Width - R(32)) / 2, (int)y, ts.Width + R(32), R(34));
                    RoundBox(g, badge, badge.Height / 2f, theme.Taxi, theme.Taxi, 0);
                    DrawText(g, "Novo recorde!", labelFont, theme.OnTaxi, badge, CenterFmt);
                }
                y += R(44);
                DrawText(g, fruits.OverText ?? "Fim de jogo", titleFont, FInk, new RectangleF(fPanel.X, y, fPanel.Width, R(52)), CenterFmt);
                y += R(66);
                int bw = (fPanel.Width - R(80) - R(12)) / 2;
                string[] labels = { "PONTOS", "RECORDE" };
                string[] vals = { fruits.Score.ToString(), Config.FruitRecord.ToString() };
                for (int i = 0; i < 2; i++)
                {
                    Rectangle r = new Rectangle(fPanel.X + R(40) + i * (bw + R(12)), (int)y, bw, R(84));
                    RoundBox(g, r, 18 * sc, FCream, FCream, 0);
                    DrawText(g, labels[i], sectionFont, Shapes.Hex(0x7a6a5c), new RectangleF(r.X + R(18), r.Y + R(10), r.Width - R(30), R(20)), LeftFmt);
                    DrawText(g, vals[i], gScore, i == 0 ? FRed : FInk, new RectangleF(r.X + R(16), r.Y + R(34), r.Width - R(30), R(40)), LeftFmt);
                }
                RoundBox(g, fBtnA, 18 * sc, FGreen, Shapes.Hex(0x3f9a50), 3 * sc);
                DrawText(g, "Jogar de novo", buttonFont, Color.White, fBtnA, CenterFmt);
            }
            RoundBox(g, fBtnB, 18 * sc, FCream, Shapes.Hex(0xe8dccb), 2 * sc);
            DrawText(g, "Sair", buttonFont, FInk, fBtnB, CenterFmt);
        }

        // ---------- toque ----------

        PointF FWorld(Point p) { return new PointF(p.X / sc, p.Y / sc); }

        void FruitDown(Contact c, Point p)
        {
            if (fstate == FState.Menu)
            {
                if (new Rectangle(R(40), R(36), R(170), R(52)).Contains(p)) GoTo(Screen.Jogos);
                else if (fKidsBtn.Contains(p)) StartFruits(true);
                else if (fHardBtn.Contains(p)) StartFruits(false);
                return;
            }
            if (fstate == FState.Pausado || fstate == FState.Fim)
            {
                if (fBtnA.Contains(p))
                {
                    if (fstate == FState.Pausado) ResumeFruits();
                    else StartFruits(fKids);
                }
                else if (fBtnB.Contains(p)) EnterFruitMenu();
                return;
            }
            if (fBack.Contains(p)) { EnterFruitMenu(); return; }
            if (!fKids && fPause.Contains(p)) { PauseFruits(); return; }

            Blade b = new Blade();
            PointF w = FWorld(p);
            b.Pts.Add(w);
            b.Times.Add(fruits.Time);
            blades[c.Id] = b;
            if (fKids && fruits.Tap(w) > 0) { b.Combo++; b.LastCut = fruits.Time; b.ComboAt = w; }
            c.Role = Role.Arrastar;
            c.Drag = delegate(Point q)
            {
                if (fruits == null || fstate != FState.Jogando) return;
                PointF z = FWorld(q), a = b.Pts[b.Pts.Count - 1];
                int n = fruits.Slice(a, z);
                if (n > 0) { b.Combo += n; b.LastCut = fruits.Time; b.ComboAt = z; }
                b.Pts.Add(z);
                b.Times.Add(fruits.Time);
                if (b.Pts.Count > 40) { b.Pts.RemoveAt(0); b.Times.RemoveAt(0); }
            };
        }

        void FruitUp(int id)
        {
            Blade b;
            if (!blades.TryGetValue(id, out b)) return;
            blades.Remove(id);
            b.Done = true;
            if (fruits != null) FlushCombo(b, true);
            oldBlades.Add(b);
        }

        void EnterFruitMenu()
        {
            StopFruitLoop();
            fstate = FState.Menu;
            fruits = null;
            blades.Clear();
            oldBlades.Clear();
            LayoutFruits();
            Invalidate();
        }

        bool FruitKey(Keys key)
        {
            if (screen != Screen.Frutas) return false;
            if (key == Keys.P || key == Keys.Escape)
            {
                if (fstate == FState.Jogando && !fKids) PauseFruits();
                else if (fstate == FState.Pausado) ResumeFruits();
                else return false;
                return true;
            }
            return false;
        }
    }
}
