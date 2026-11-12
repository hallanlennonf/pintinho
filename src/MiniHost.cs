using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Pintinho
{
    enum MState { Fases, Jogando, Fim }

    // Joguinhos de fases (Toupeira, Quebra-cabeça, Forca, Ligue os pares, 7 Erros). Cada um é uma
    // classe Mini; aqui fica o que é comum: escolha de fase, cabeçalho, fim de fase, progresso salvo
    // e o relógio das animações (cada jogo invalida só o pedaço que mexeu).
    partial class MainForm
    {
        abstract class Mini
        {
            public MainForm M;
            public int Level = 1;
            public bool Won, Lost;
            public Rectangle Area;

            public abstract string Key { get; }
            public abstract string Title { get; }
            public abstract Color Accent { get; }
            public virtual int Levels { get { return 12; } }
            public virtual string Info { get { return null; } }
            public virtual string LostText { get { return "Tente de novo!"; } }
            public virtual bool HasHint { get { return false; } }
            public virtual void Hint() { }

            public abstract void Start(Random rnd);   // monta a fase "Level"
            public abstract void Layout();             // recalcula posições dentro de Area
            public abstract void Paint(Graphics g, Rectangle clip);
            public abstract void Down(Contact c, Point p);
            public virtual void Up(int id) { }
            public virtual void Tick(float dt) { }
            public abstract void DrawArt(Graphics g, Rectangle img); // cartão da escolha de jogo

            protected int R(float v) { return M.R(v); }
            protected float Sc { get { return M.sc; } }
            protected void Repaint(Rectangle r) { M.Invalidate(r); }
            protected void RepaintAll() { M.Invalidate(Area); M.InvalidateMiniHeader(); }
        }

        Mini[] minis;
        Mini mini;
        MState mstate = MState.Fases;
        Rectangle mBack, mFasesBack, mHint, mPanel, mBtnA, mBtnB;
        Rectangle[] mLevelBtns = new Rectangle[0];
        RectangleF mTitleR, mInfoR;
        readonly Timer miniTimer = new Timer();
        readonly Stopwatch miniWatch = new Stopwatch();
        double miniLast, miniEndAt;
        Font mLevelFont;

        void InitMinis()
        {
            minis = new Mini[] { new WashGame(), new MoleGame(), new SlideGame(), new HangGame(), new MatchGame(), new DiffGame() };
            foreach (Mini m in minis) m.M = this;
            miniTimer.Interval = 15;
            miniTimer.Tick += OnMiniTick;
        }

        void OpenMini(int i)
        {
            mini = minis[i];
            mstate = MState.Fases;
            GoTo(Screen.Mini);
            LayoutMini();
        }

        void LayoutMini()
        {
            if (mLevelFont != null) mLevelFont.Dispose();
            mLevelFont = F(56, FontStyle.Bold);
            int W = view.Width, H = view.Height;
            mBack = new Rectangle(R(24), R(20), R(56), R(56));
            mFasesBack = new Rectangle(R(40), R(36), R(170), R(52));
            mHint = new Rectangle(W - R(24) - R(120), R(20), R(120), R(56));
            mTitleR = new RectangleF(mBack.Right + R(16), R(20), W * 0.55f, R(56));
            float infoRight = mini != null && mini.HasHint ? mHint.X - R(20) : W - R(28);
            mInfoR = new RectangleF(W * 0.4f, R(20), infoRight - W * 0.4f, R(56));

            int n = mini != null ? mini.Levels : 12;
            int cols = portrait ? 3 : 6, size = portrait ? R(170) : R(150), gap = R(24);
            int x0 = (W - (cols * size + (cols - 1) * gap)) / 2, y0 = R(236);
            mLevelBtns = new Rectangle[n];
            for (int i = 0; i < n; i++) mLevelBtns[i] = new Rectangle(x0 + (i % cols) * (size + gap), y0 + (i / cols) * (size + gap), size, size);

            mPanel = Center(new Rectangle(Point.Empty, view), Math.Min(W - R(40), R(560)), R(380));
            mBtnB = new Rectangle(mPanel.Right - R(40) - R(150), mPanel.Bottom - R(36) - R(68), R(150), R(68));
            mBtnA = new Rectangle(mPanel.X + R(40), mBtnB.Y, mBtnB.X - R(12) - mPanel.X - R(40), R(68));

            if (mini != null)
            {
                mini.Area = new Rectangle(R(24), R(92), W - R(48), H - R(92) - R(24));
                if (mstate != MState.Fases) mini.Layout();
            }
        }

        void InvalidateMiniHeader()
        {
            Invalidate(new Rectangle(0, 0, view.Width, R(92)));
        }

        // ---------- relógio ----------

        void StartMiniClock()
        {
            miniWatch.Reset();
            miniWatch.Start();
            miniLast = 0;
            miniEndAt = 0;
            if (CaptureDir == null) miniTimer.Start();
        }

        void StopMiniClock()
        {
            miniTimer.Stop();
            miniWatch.Stop();
        }

        void OnMiniTick(object sender, EventArgs e)
        {
            if (screen != Screen.Mini || mini == null || mstate != MState.Jogando) { StopMiniClock(); return; }
            double now = miniWatch.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, now - miniLast);
            miniLast = now;
            mini.Tick(dt);
            if (mini.Won || mini.Lost)
            {
                if (miniEndAt == 0)
                {
                    // deixa ver a fase pronta um pouquinho antes do cartão
                    miniEndAt = now + 1.1;
                    if (mini.Won && mini.Level > Config.MiniBest(mini.Key))
                    {
                        Config.SetMiniBest(mini.Key, mini.Level);
                        Config.Save();
                    }
                    InvalidateMiniHeader();
                }
                else if (now >= miniEndAt) ShowMiniEnd();
            }
        }

        void ShowMiniEnd()
        {
            StopMiniClock();
            mstate = MState.Fim;
            LayoutMini();
            Invalidate();
        }

        void StartMiniLevel(int level)
        {
            mini.Level = Math.Max(1, Math.Min(mini.Levels, level));
            mini.Won = mini.Lost = false;
            mini.Start(rnd);
            mstate = MState.Jogando;
            LayoutMini();
            Invalidate();
            StartMiniClock();
        }

        void BackToLevels()
        {
            StopMiniClock();
            mstate = MState.Fases;
            LayoutMini();
            Invalidate();
        }

        // ---------- desenho ----------

        void PaintMini(Graphics g, Rectangle clip)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (mini == null) return;
            if (mstate == MState.Fases) { DrawMiniLevels(g); return; }

            if (clip.IntersectsWith(mini.Area)) mini.Paint(g, clip);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawBackButton(g, mBack, null);
            DrawText(g, mini.Title + " · Fase " + mini.Level, toastFont, theme.Ink, mTitleR, LeftFmt);
            string info = mini.Info;
            if (info != null) DrawText(g, info, toastFont, mini.Accent, mInfoR, RightFmt);
            if (mini.HasHint && !mini.Won)
            {
                RoundBox(g, mHint, 16 * sc, theme.Taxi, theme.Cone, 2 * sc);
                DrawText(g, "Dica", buttonFont, theme.OnTaxi, mHint, CenterFmt);
            }
            if (mstate == MState.Fim) DrawMiniCard(g);
        }

        void DrawMiniLevels(Graphics g)
        {
            DrawBackButton(g, mFasesBack, "Joguinhos");
            DrawText(g, mini.Title, bigFont, theme.Ink, new RectangleF(0, R(60), view.Width, R(84)), CenterFmt);
            DrawText(g, "Escolha a fase", subtitleFont, theme.Muted, new RectangleF(0, R(146), view.Width, R(40)), CenterFmt);
            int best = Config.MiniBest(mini.Key);
            for (int i = 0; i < mLevelBtns.Length; i++)
            {
                Rectangle r = mLevelBtns[i];
                bool done = i < best, open = i <= best;
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(open ? 40 : 15, theme.Ink)))
                using (GraphicsPath p = Shapes.RoundRect(new RectangleF(r.X, r.Y + R(5), r.Width, r.Height), 26 * sc)) g.FillPath(sh, p);
                Color fill = done ? mini.Accent : open ? Color.White : Shapes.Hex(0xf1e4c8);
                RoundBox(g, r, 26 * sc, fill, open ? mini.Accent : theme.Borda, 3 * sc);
                if (open)
                    DrawText(g, (i + 1).ToString(), mLevelFont, done ? Color.White : mini.Accent, r, CenterFmt);
                else
                    DrawPadlock(g, r.X + r.Width / 2f, r.Y + r.Height / 2f, r.Width * 0.3f, theme.Borda);
                if (done)
                    using (GraphicsPath star = Shapes.Star(r.Right - R(26), r.Y + R(26), R(16), R(7), 5))
                    using (SolidBrush b = new SolidBrush(theme.Taxi))
                    using (Pen pen = new Pen(Color.White, 2 * sc))
                    {
                        g.FillPath(b, star);
                        g.DrawPath(pen, star);
                    }
            }
            DrawLockButton(g, homeLock, 18 * sc);
        }

        static void DrawPadlock(Graphics g, float cx, float cy, float s, Color c)
        {
            using (Pen p = Shapes.RoundPen(c, s * 0.16f)) g.DrawArc(p, cx - s * 0.32f, cy - s * 0.75f, s * 0.64f, s * 0.8f, 180, 180);
            using (GraphicsPath body = Shapes.RoundRect(new RectangleF(cx - s * 0.5f, cy - s * 0.3f, s, s * 0.8f), s * 0.14f))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, body);
        }

        void DrawMiniCard(Graphics g)
        {
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(150, 43, 33, 24))) g.FillRectangle(dim, new Rectangle(Point.Empty, view));
            RoundBox(g, mPanel, 28 * sc, Color.White, theme.Borda, 2 * sc);
            float y = mPanel.Y + R(36);
            if (mini.Won)
            {
                for (int i = 0; i < 3; i++)
                {
                    float cx = mPanel.X + mPanel.Width / 2f + (i - 1) * R(78), cy = y + R(36) - (i == 1 ? R(12) : 0);
                    using (GraphicsPath star = Shapes.Star(cx, cy, R(i == 1 ? 40 : 32), R(i == 1 ? 17 : 14), 5))
                    using (SolidBrush b = new SolidBrush(theme.Taxi))
                    using (Pen pen = Shapes.RoundPen(theme.Cone, 3 * sc))
                    {
                        g.FillPath(b, star);
                        g.DrawPath(pen, star);
                    }
                }
                y += R(96);
                bool last = mini.Level >= mini.Levels;
                DrawText(g, last ? "Zerou o jogo!" : "Fase " + mini.Level + " concluída!", titleFont, theme.Ink, new RectangleF(mPanel.X, y, mPanel.Width, R(56)), CenterFmt);
                RoundBox(g, mBtnA, 18 * sc, theme.Trator, Shapes.Hex(0x2f7d3d), 3 * sc);
                DrawText(g, last ? "Jogar de novo" : "Próxima fase", buttonFont, Color.White, mBtnA, CenterFmt);
            }
            else
            {
                y += R(40);
                DrawText(g, mini.LostText, titleFont, theme.Ink, new RectangleF(mPanel.X + R(20), y, mPanel.Width - R(40), R(56)), CenterFmt);
                DrawText(g, "Vamos de novo?", subtitleFont, theme.Muted, new RectangleF(mPanel.X, y + R(64), mPanel.Width, R(40)), CenterFmt);
                RoundBox(g, mBtnA, 18 * sc, theme.Taxi, theme.Cone, 3 * sc);
                DrawText(g, "Tentar de novo", buttonFont, theme.OnTaxi, mBtnA, CenterFmt);
            }
            RoundBox(g, mBtnB, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, "Fases", buttonFont, theme.Ink, mBtnB, CenterFmt);
        }

        // ---------- toque ----------

        void MiniDown(Contact c, Point p)
        {
            if (mini == null) return;
            if (mstate == MState.Fases)
            {
                if (mFasesBack.Contains(p)) { GoTo(Screen.Jogos); return; }
                int best = Config.MiniBest(mini.Key);
                for (int i = 0; i < mLevelBtns.Length; i++)
                    if (mLevelBtns[i].Contains(p) && i <= best) { StartMiniLevel(i + 1); return; }
                return;
            }
            if (mstate == MState.Fim)
            {
                if (mBtnA.Contains(p))
                {
                    if (mini.Won) StartMiniLevel(mini.Level >= mini.Levels ? 1 : mini.Level + 1);
                    else StartMiniLevel(mini.Level);
                }
                else if (mBtnB.Contains(p)) BackToLevels();
                return;
            }
            if (mBack.Contains(p)) { BackToLevels(); return; }
            if (mini.HasHint && !mini.Won && mHint.Contains(p)) { mini.Hint(); return; }
            if (mini.Won || mini.Lost) return;
            mini.Down(c, p);
        }

        void MiniUp(int id)
        {
            if (mini != null && mstate == MState.Jogando) mini.Up(id);
        }

        // ---------- ajudantes de desenho dos joguinhos ----------

        static readonly Color[] Bright = {
            Shapes.Hex(0xe53935), Shapes.Hex(0xfb8c00), Shapes.Hex(0xfdd835), Shapes.Hex(0x43a047),
            Shapes.Hex(0x1e88e5), Shapes.Hex(0x8e24aa), Shapes.Hex(0xec407a), Shapes.Hex(0x00acc1)
        };

        // 0 círculo, 1 quadrado, 2 triângulo, 3 estrela, 4 coração
        static void DrawShape(Graphics g, int kind, float cx, float cy, float r, Color fill, float line)
        {
            using (GraphicsPath p = ShapePath(kind, cx, cy, r))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (line > 0) using (Pen pen = Shapes.RoundPen(FInk, line)) g.DrawPath(pen, p);
            }
        }

        static GraphicsPath ShapePath(int kind, float cx, float cy, float r)
        {
            GraphicsPath p;
            switch (kind)
            {
                case 1: return Shapes.RoundRect(new RectangleF(cx - r * 0.82f, cy - r * 0.82f, r * 1.64f, r * 1.64f), r * 0.2f);
                case 2:
                    p = new GraphicsPath();
                    p.AddPolygon(new PointF[] { new PointF(cx, cy - r), new PointF(cx + r * 0.95f, cy + r * 0.72f), new PointF(cx - r * 0.95f, cy + r * 0.72f) });
                    return p;
                case 3: return Shapes.Star(cx, cy + r * 0.06f, r, r * 0.45f, 5);
                case 4:
                    p = new GraphicsPath();
                    float x = cx - r, y = cy - r * 0.85f, s = 2 * r;
                    p.AddBezier(x + s / 2, y + s * 0.3f, x + s / 2, y, x, y, x, y + s * 0.3f);
                    p.AddBezier(x, y + s * 0.3f, x, y + s * 0.6f, x + s / 2, y + s * 0.75f, x + s / 2, y + s * 0.92f);
                    p.AddBezier(x + s / 2, y + s * 0.92f, x + s / 2, y + s * 0.75f, x + s, y + s * 0.6f, x + s, y + s * 0.3f);
                    p.AddBezier(x + s, y + s * 0.3f, x + s, y, x + s / 2, y, x + s / 2, y + s * 0.3f);
                    p.CloseFigure();
                    return p;
                default:
                    p = new GraphicsPath();
                    p.AddEllipse(cx - r * 0.9f, cy - r * 0.9f, r * 1.8f, r * 1.8f);
                    return p;
            }
        }

        static void DrawBalloon(Graphics g, float cx, float cy, float r, Color c, float line)
        {
            using (Pen p = Shapes.RoundPen(Color.FromArgb(160, FInk), Math.Max(1, line * 0.6f)))
                g.DrawBezier(p, cx, cy + r, cx - r * 0.2f, cy + r * 1.3f, cx + r * 0.2f, cy + r * 1.5f, cx, cy + r * 1.8f);
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillPolygon(b, new PointF[] { new PointF(cx, cy + r * 0.9f), new PointF(cx - r * 0.13f, cy + r * 1.08f), new PointF(cx + r * 0.13f, cy + r * 1.08f) });
                g.FillEllipse(b, cx - r * 0.78f, cy - r, r * 1.56f, r * 1.95f);
            }
            if (line > 0) using (Pen p = new Pen(FInk, line)) g.DrawEllipse(p, cx - r * 0.78f, cy - r, r * 1.56f, r * 1.95f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, cx - r * 0.45f, cy - r * 0.7f, r * 0.3f, r * 0.45f);
        }

        static void DrawFlower(Graphics g, float cx, float cy, float r, Color c, float line)
        {
            using (SolidBrush b = new SolidBrush(c))
            using (Pen p = new Pen(FInk, line))
                for (int i = 0; i < 5; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI * 2 / 5;
                    float px = cx + (float)Math.Cos(a) * r * 0.55f, py = cy + (float)Math.Sin(a) * r * 0.55f;
                    g.FillEllipse(b, px - r * 0.42f, py - r * 0.42f, r * 0.84f, r * 0.84f);
                    if (line > 0) g.DrawEllipse(p, px - r * 0.42f, py - r * 0.42f, r * 0.84f, r * 0.84f);
                }
            using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffd54f))) g.FillEllipse(b, cx - r * 0.32f, cy - r * 0.32f, r * 0.64f, r * 0.64f);
            if (line > 0) using (Pen p = new Pen(FInk, line)) g.DrawEllipse(p, cx - r * 0.32f, cy - r * 0.32f, r * 0.64f, r * 0.64f);
        }

        // Pinta sozinho cada região fechada de um desenho (fundo claro, regiões coloridas).
        static Bitmap AutoColor(Drawings.Item item, int w, int h, int seed)
        {
            Color[] pal = {
                Shapes.Hex(0xef9a9a), Shapes.Hex(0xffcc80), Shapes.Hex(0xfff176), Shapes.Hex(0xa5d6a7), Shapes.Hex(0x90caf9),
                Shapes.Hex(0xce93d8), Shapes.Hex(0xf48fb1), Shapes.Hex(0x80deea), Shapes.Hex(0xbcaaa4), Shapes.Hex(0xc5e1a5),
                Shapes.Hex(0xffb74d), Shapes.Hex(0x9fa8da), Shapes.Hex(0xe57373), Shapes.Hex(0x4fc3f7), Shapes.Hex(0x81c784)
            };
            Random rnd = new Random(seed);
            Bitmap result = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Bitmap ov = Drawings.Render(item, w, h))
            {
                int[] px = new int[w * h];
                using (Bitmap tmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(tmp)) g.DrawImageUnscaled(ov, 0, 0);
                    System.Drawing.Imaging.BitmapData bd = tmp.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, px, 0, px.Length);
                    tmp.UnlockBits(bd);
                }
                int[] lab = new int[w * h];
                for (int i = 0; i < lab.Length; i++) lab[i] = ((px[i] >> 24) & 0xff) > 90 ? -2 : -1;
                int[] queue = new int[w * h];
                List<int> colors = new List<int>();
                Color sky = Shapes.Hex(0xfff8e1);
                for (int s = 0; s < lab.Length; s++)
                {
                    if (lab[s] != -1) continue;
                    int id = colors.Count, head = 0, tail = 0;
                    bool border = false;
                    lab[s] = id;
                    queue[tail++] = s;
                    while (head < tail)
                    {
                        int q = queue[head++], x = q % w, y = q / w;
                        if (x == 0 || y == 0 || x == w - 1 || y == h - 1) border = true;
                        if (x > 0 && lab[q - 1] == -1) { lab[q - 1] = id; queue[tail++] = q - 1; }
                        if (x < w - 1 && lab[q + 1] == -1) { lab[q + 1] = id; queue[tail++] = q + 1; }
                        if (y > 0 && lab[q - w] == -1) { lab[q - w] = id; queue[tail++] = q - w; }
                        if (y < h - 1 && lab[q + w] == -1) { lab[q + w] = id; queue[tail++] = q + w; }
                    }
                    colors.Add((border ? sky : pal[rnd.Next(pal.Length)]).ToArgb());
                }
                int white = Color.White.ToArgb();
                for (int i = 0; i < px.Length; i++) px[i] = lab[i] >= 0 ? colors[lab[i]] : white;
                System.Drawing.Imaging.BitmapData od = result.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Runtime.InteropServices.Marshal.Copy(px, 0, od.Scan0, px.Length);
                result.UnlockBits(od);
                using (Graphics g = Graphics.FromImage(result)) g.DrawImageUnscaled(ov, 0, 0);
            }
            return result;
        }
    }
}
