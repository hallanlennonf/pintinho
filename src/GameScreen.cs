using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Pintinho
{
    enum GState { Menu, Play, Pause, Over, Shop }

    // Joguinho "Comboio": menu, partida, pausa, fim de jogo e oficina.
    // A partida roda num laço próprio (Application.Idle) a até 60 quadros/s: a lógica anda em passos
    // fixos de 1/60 s e a estrada é desenhada com imagens prontas (sprites) num buffer duplo.
    partial class MainForm
    {
        static readonly CultureInfo Br = new CultureInfo("pt-BR");
        static readonly Color GRoad = Shapes.Hex(0x2b3036), GBlack = Shapes.Hex(0x0e1013), GWater = Shapes.Hex(0x5cb8ec);
        static readonly Color GSmall = Shapes.Hex(0x8bc34a), GBig = Shapes.Hex(0x9575cd), GBoss = Shapes.Hex(0xe5533e), GTruck = Shapes.Hex(0xe53935);
        static readonly Color[] GateColors = { Color.FromArgb(220, 0x5c, 0xbf, 0x6d), Color.FromArgb(220, 0xe5, 0x53, 0x3e), Color.FromArgb(220, 0x5c, 0xb8, 0xec) };
        const string GosmaPath = "M-20 10 Q-20 -18 0 -18 Q20 -18 20 10 Q14 16 8 10 Q0 18 -8 10 Q-14 16 -20 10 Z";

        static readonly string[] UpgNames = { "Frota inicial", "Dano do jato", "Cadência", "Jatos por caminhão", "Ímã de moedas", "Para-choque" };
        static readonly string[] UpgDesc = { "Caminhões no começo da partida", "Gosmas grandes caem mais rápido", "Mais jatos por segundo", "Atira em leque", "Mais moedas por gosma", "Aguenta batidas a cada onda" };
        static readonly int[] UpgMax = { 4, 4, 4, 2, 4, 3 };
        static readonly int[] UpgBase = { 150, 150, 140, 900, 120, 300 };

        [StructLayout(LayoutKind.Sequential)]
        struct NativeMessage { public IntPtr Hwnd; public uint Msg; public IntPtr WParam; public IntPtr LParam; public uint Time; public Point P; }
        [DllImport("user32.dll")]
        static extern bool PeekMessage(out NativeMessage m, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("winmm.dll")]
        static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")]
        static extern uint timeEndPeriod(uint ms);

        GState gstate = GState.Menu;
        Game game;
        float fs = 1f, gameH = 800, spScale = -1;
        Rectangle gField, gLeft, gRight, gTop, gBottom, gPause, gBack, gPlay, gShop, gCoins, gShopBack;
        readonly Rectangle[] gCards = new Rectangle[6], gBuy = new Rectangle[6];
        Rectangle gPanel, gBtnA, gBtnB, gBtnC;
        Font gHuge, gNum, gScore, gStat, gGate, gGateSmall;
        Bitmap spTruck, spSmall, spBig, spBoss, spBullet;
        bool newRecord;
        int lastCoins;

        bool looping;
        readonly Stopwatch gsw = new Stopwatch();
        double gLast, gAcc, gLastRender, gFpsT, gHudT;
        int gFrames, gFps;
        bool gHudDirty;
        BufferedGraphicsContext gBufCtx;
        BufferedGraphics gBuf;
        Graphics gTarget;

        static string Fmt(int n) { return n.ToString("N0", Br); }

        // ---------- layout ----------

        void LayoutGame()
        {
            Font[] old = { gHuge, gNum, gScore, gStat };
            foreach (Font f in old) if (f != null) f.Dispose();
            gHuge = F(96, FontStyle.Bold);
            gNum = F(56, FontStyle.Bold);
            gScore = F(30, FontStyle.Bold);
            gStat = F(15, FontStyle.Regular);

            int W = view.Width, H = view.Height;
            if (!portrait)
            {
                int fw = Math.Min(W, R(760));
                gField = new Rectangle((W - fw) / 2, 0, fw, H);
                gLeft = new Rectangle(0, 0, gField.X, H);
                gRight = new Rectangle(gField.Right, 0, W - gField.Right, H);
                gTop = gBottom = Rectangle.Empty;
                gPause = new Rectangle(gRight.Right - R(22) - R(64), R(24), R(64), R(64));
            }
            else
            {
                int top = R(150), bottom = R(100);
                gField = new Rectangle(0, top, W, H - top - bottom);
                gTop = new Rectangle(0, 0, W, top);
                gBottom = new Rectangle(0, H - bottom, W, bottom);
                gLeft = gRight = Rectangle.Empty;
                gPause = new Rectangle(W - R(20) - R(64), (top - R(64)) / 2, R(64), R(64));
            }
            fs = gField.Width / Game.W;
            gameH = gField.Height / fs;
            if (game != null) game.H = gameH;

            if (gGate != null) { gGate.Dispose(); gGateSmall.Dispose(); }
            gGate = new Font("Segoe UI", Math.Max(8f, 36 * fs), FontStyle.Bold, GraphicsUnit.Pixel);
            gGateSmall = new Font("Segoe UI", Math.Max(6f, 12 * fs), FontStyle.Bold, GraphicsUnit.Pixel);
            if (spScale != fs) BuildSprites();

            // menu
            int x0 = portrait ? R(56) : R(96), y0 = portrait ? R(130) : R(150);
            gBack = new Rectangle(R(40), R(36), R(130), R(52));
            gCoins = new Rectangle(portrait ? W - R(40) - R(160) : W - R(420) - R(160), R(36), R(160), R(52));
            gPlay = new Rectangle(x0, y0 + R(294), R(300), R(84));
            gShop = new Rectangle(gPlay.Right + R(16), gPlay.Y, R(240), R(84));

            // oficina
            gShopBack = new Rectangle(R(48), R(36), R(56), R(56));
            int gy = gShopBack.Bottom + R(28), gap = R(20);
            Rectangle area = new Rectangle(R(48), gy, W - 2 * R(48), H - gy - R(36));
            int cols = portrait ? 2 : 3, rows = portrait ? 3 : 2;
            int cw = (area.Width - (cols - 1) * gap) / cols, ch = (area.Height - (rows - 1) * gap) / rows;
            for (int i = 0; i < 6; i++)
            {
                gCards[i] = new Rectangle(area.X + (i % cols) * (cw + gap), area.Y + (i / cols) * (ch + gap), cw, ch);
                gBuy[i] = new Rectangle(gCards[i].X + R(22), gCards[i].Bottom - R(22) - R(56), cw - R(44), R(56));
            }
            if (looping) AllocBuffer();
        }

        void LayoutGameOverlay()
        {
            if (gstate == GState.Pause)
            {
                gPanel = Center(new Rectangle(Point.Empty, view), R(480), R(400));
                int bw = gPanel.Width - R(80), bx = gPanel.X + R(40), by = gPanel.Y + R(120);
                gBtnA = new Rectangle(bx, by, bw, R(68));
                gBtnB = new Rectangle(bx, by + R(84), bw, R(68));
                gBtnC = new Rectangle(bx, by + R(168), bw, R(68));
            }
            else
            {
                gPanel = Center(new Rectangle(Point.Empty, view), Math.Min(view.Width - R(40), R(600)), R(560));
                int by = gPanel.Bottom - R(36) - R(68), bx = gPanel.X + R(40);
                gBtnC = new Rectangle(gPanel.Right - R(40) - R(120), by, R(120), R(68));
                gBtnB = new Rectangle(gBtnC.X - R(12) - R(150), by, R(150), R(68));
                gBtnA = new Rectangle(bx, by, gBtnB.X - R(12) - bx, R(68));
            }
        }

        // ---------- sprites ----------

        Bitmap Sprite(float w, float h, Action<Graphics> draw)
        {
            Bitmap b = new Bitmap(Math.Max(1, (int)Math.Ceiling(w * fs)), Math.Max(1, (int)Math.Ceiling(h * fs)), PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(b.Width / 2f, b.Height / 2f);
                g.ScaleTransform(fs, fs);
                draw(g);
            }
            return b;
        }

        static void FillStrokePath(Graphics g, GraphicsPath p, Color fill, Color line, float w)
        {
            using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
            if (w > 0) using (Pen pen = Shapes.RoundPen(line, w)) g.DrawPath(pen, p);
        }

        static void DrawTruck(Graphics g)
        {
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(-14, -23, 28, 46), 6)) FillStrokePath(g, p, GTruck, GBlack, 2);
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(-11, -20, 22, 12), 3)) FillStrokePath(g, p, Shapes.Hex(0xb3e5fc), GBlack, 0);
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(-3, -4, 6, 24), 2)) FillStrokePath(g, p, Shapes.Hex(0xf5ecdc), GBlack, 0);
            using (Pen p = new Pen(GTruck, 1.5f)) { g.DrawLine(p, -3, 3, 3, 3); g.DrawLine(p, -3, 10, 3, 10); }
            using (SolidBrush b = new SolidBrush(GWater)) g.FillEllipse(b, -4, -30, 8, 8);
            using (Pen p = new Pen(GBlack, 1.5f)) g.DrawEllipse(p, -4, -30, 8, 8);
        }

        static void DrawGosma(Graphics g, Color fill, float k, int kind)
        {
            GraphicsState st = g.Save();
            g.ScaleTransform(k, k);
            if (kind == 2)
            {
                using (GraphicsPath horn = SvgPath.Parse("M-11 -14 L-15 -24 L-6 -17 Z M11 -14 L15 -24 L6 -17 Z"))
                    FillStrokePath(g, horn, Shapes.Hex(0xffc727), GBlack, 1.2f / k * 1.5f);
            }
            using (GraphicsPath p = SvgPath.Parse(GosmaPath)) FillStrokePath(g, p, fill, GBlack, 2f / k * (kind == 0 ? 1 : 1.4f));
            using (SolidBrush w = new SolidBrush(Color.White))
            {
                g.FillEllipse(w, -12, -9, 10, 10);
                g.FillEllipse(w, 2, -9, 10, 10);
            }
            using (SolidBrush b = new SolidBrush(GBlack))
            {
                g.FillEllipse(b, -8.3f, -4.3f, 4.6f, 4.6f);
                g.FillEllipse(b, 5.7f, -4.3f, 4.6f, 4.6f);
            }
            if (kind > 0)
                using (Pen p = Shapes.RoundPen(GBlack, 1.6f)) { g.DrawLine(p, -12, -11, -3, -8); g.DrawLine(p, 12, -11, 3, -8); }
            else
                using (SolidBrush hl = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(hl, -14, -13, 6, 4);
            g.Restore(st);
        }

        void BuildSprites()
        {
            Bitmap[] old = { spTruck, spSmall, spBig, spBoss, spBullet };
            foreach (Bitmap b in old) if (b != null) b.Dispose();
            spScale = fs;
            spTruck = Sprite(34, 66, delegate(Graphics g) { g.TranslateTransform(0, 2); DrawTruck(g); });
            spSmall = Sprite(46, 42, delegate(Graphics g) { DrawGosma(g, GSmall, 1f, 0); });
            spBig = Sprite(46 * 1.65f, 42 * 1.65f, delegate(Graphics g) { DrawGosma(g, GBig, 1.65f, 1); });
            spBoss = Sprite(46 * 3.5f, 56 * 3.5f, delegate(Graphics g) { g.TranslateTransform(0, 10); DrawGosma(g, GBoss, 3.5f, 2); });
            spBullet = Sprite(8, 30, delegate(Graphics g)
            {
                using (Pen p = Shapes.RoundPen(GWater, 5)) g.DrawLine(p, 0, -12, 0, 12);
                using (Pen p = Shapes.RoundPen(Color.FromArgb(200, 255, 255, 255), 1.6f)) g.DrawLine(p, -0.8f, -9, -0.8f, 2);
            });
        }

        // ---------- laço do jogo ----------

        void EnterGame()
        {
            gstate = GState.Menu;
            game = null;
        }

        void StartGame()
        {
            game = new Game(Config.GameUpg, Environment.TickCount);
            game.H = gameH;
            gstate = GState.Play;
            Invalidate();
            StartLoop();
        }

        void StartLoop()
        {
            if (looping || CaptureDir != null) return;
            looping = true;
            timeBeginPeriod(1);
            gsw.Reset();
            gsw.Start();
            gLast = gAcc = gLastRender = gFpsT = gHudT = 0;
            gFrames = 0;
            AllocBuffer();
            Application.Idle += OnGameIdle;
        }

        void StopLoop()
        {
            if (!looping) return;
            looping = false;
            Application.Idle -= OnGameIdle;
            timeEndPeriod(1);
            FreeBuffer();
        }

        void AllocBuffer()
        {
            FreeBuffer();
            if (!IsHandleCreated) return;
            gTarget = CreateGraphics();
            gBufCtx = new BufferedGraphicsContext();
            gBufCtx.MaximumBuffer = new Size(view.Width + 1, view.Height + 1);
            gBuf = gBufCtx.Allocate(gTarget, gField);
        }

        void FreeBuffer()
        {
            if (gBuf != null) { gBuf.Dispose(); gBuf = null; }
            if (gBufCtx != null) { gBufCtx.Dispose(); gBufCtx = null; }
            if (gTarget != null) { gTarget.Dispose(); gTarget = null; }
        }

        void OnGameIdle(object sender, EventArgs e)
        {
            NativeMessage m;
            while (looping && !PeekMessage(out m, IntPtr.Zero, 0, 0, 0)) GameFrame();
        }

        void GameFrame()
        {
            const double step = 1 / 60.0;
            double now = gsw.Elapsed.TotalSeconds, dt = Math.Min(0.1, now - gLast);
            gLast = now;
            gAcc += dt;
            int steps = 0;
            while (gAcc >= step && steps < 4)
            {
                game.Step((float)step);
                gAcc -= step;
                steps++;
            }
            if (steps == 4) gAcc = 0;
            if (game.Over) { OnGameOver(); return; }

            if (now - gLastRender >= step - 0.001)
            {
                gLastRender = now;
                if (gBuf != null)
                {
                    RenderField(gBuf.Graphics);
                    gBuf.Render();
                }
                gFrames++;
                if (now - gFpsT >= 1) { gFps = gFrames; gFrames = 0; gFpsT = now; }
            }
            else Thread.Sleep(1);

            if (steps > 0) gHudDirty = true;
            if (gHudDirty && now - gHudT > 0.2)
            {
                gHudT = now;
                gHudDirty = false;
                if (!portrait) { Invalidate(gLeft); Invalidate(gRight); }
                else { Invalidate(gTop); Invalidate(gBottom); }
            }
        }

        void OnGameOver()
        {
            StopLoop();
            gstate = GState.Over;
            lastCoins = game.Coins;
            Config.GameCoins += lastCoins;
            newRecord = game.Wave > Config.GameRecordWave || (game.Wave == Config.GameRecordWave && game.Score > Config.GameRecordScore);
            if (newRecord) { Config.GameRecordWave = game.Wave; Config.GameRecordScore = game.Score; }
            Config.Save();
            Invalidate();
        }

        // ---------- desenho ----------

        void PaintGame(Graphics g, Rectangle clip)
        {
            switch (gstate)
            {
                case GState.Menu: DrawGameMenu(g); break;
                case GState.Shop: DrawGameShop(g); break;
                default:
                    if (clip.IntersectsWith(gField)) RenderField(g);
                    DrawGameHud(g);
                    if (gstate == GState.Pause) DrawPauseCard(g);
                    else if (gstate == GState.Over) DrawOverCard(g);
                    break;
            }
        }

        void RenderField(Graphics g)
        {
            GraphicsState st = g.Save();
            g.SetClip(gField);
            g.SmoothingMode = SmoothingMode.None;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            float ox = gField.X, oy = gField.Y, t = game != null ? game.Time : 0;

            using (SolidBrush b = new SolidBrush(GRoad)) g.FillRectangle(b, gField);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(180, 0xff, 0xc7, 0x27)))
            {
                g.FillRectangle(b, ox + 14 * fs, oy, 6 * fs, gField.Height);
                g.FillRectangle(b, ox + 740 * fs, oy, 6 * fs, gField.Height);
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(46, 0xf5, 0xec, 0xdc)))
            {
                float off = (t * 140) % 120;
                for (float y = -120 + off; y < gameH; y += 120)
                {
                    g.FillRectangle(b, ox + 250 * fs, oy + y * fs, 8 * fs, 48 * fs);
                    g.FillRectangle(b, ox + 502 * fs, oy + y * fs, 8 * fs, 48 * fs);
                }
            }
            if (game == null) { g.Restore(st); return; }

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (SolidBrush post = new SolidBrush(Shapes.Hex(0x444b55)))
            using (SolidBrush ink = new SolidBrush(Shapes.Hex(0x1c1f24)))
                foreach (Gate gt in game.Gates)
                    for (int side = 0; side < 2; side++)
                    {
                        RectangleF r = new RectangleF(ox + Game.GateLeft[side] * fs, oy + (gt.Y - Game.GateHalf) * fs, Game.GateWidth * fs, 2 * Game.GateHalf * fs);
                        g.FillRectangle(post, r.X - 6 * fs, r.Y - 4 * fs, 12 * fs, r.Height + 8 * fs);
                        g.FillRectangle(post, r.Right - 6 * fs, r.Y - 4 * fs, 12 * fs, r.Height + 8 * fs);
                        using (SolidBrush fill = new SolidBrush(GateColors[Game.GateColor(gt.Kind[side], gt.Val[side])])) g.FillRectangle(fill, r);
                        string big, small;
                        Game.GateText(gt.Kind[side], gt.Val[side], out big, out small);
                        g.DrawString(big, gGate, ink, new RectangleF(r.X, r.Y, r.Width, r.Height * 0.72f), CenterFmt);
                        g.DrawString(small, gGateSmall, ink, new RectangleF(r.X, r.Y + r.Height * 0.62f, r.Width, r.Height * 0.36f), CenterFmt);
                    }

            foreach (Enemy e in game.Enemies)
            {
                Bitmap sp = e.Kind == 0 ? spSmall : e.Kind == 1 ? spBig : spBoss;
                float sx = ox + e.X * fs, sy = oy + e.Y * fs;
                g.DrawImageUnscaled(sp, (int)(sx - sp.Width / 2f), (int)(sy - sp.Height / 2f));
                if (e.Kind > 0 && e.Hp < e.MaxHp)
                {
                    float bw = e.R * 2 * fs, by = sy - (e.R + 14) * fs;
                    using (SolidBrush bg = new SolidBrush(Shapes.Hex(0x444b55))) g.FillRectangle(bg, sx - bw / 2, by, bw, 7 * fs);
                    using (SolidBrush hp = new SolidBrush(GBoss)) g.FillRectangle(hp, sx - bw / 2, by, bw * Math.Max(0, e.Hp / e.MaxHp), 7 * fs);
                }
            }

            int bwh = spBullet.Width / 2, bhh = spBullet.Height / 2;
            for (int b = 0; b < game.BulletCount; b++)
                g.DrawImageUnscaled(spBullet, (int)(ox + game.BX[b] * fs) - bwh, (int)(oy + game.BY[b] * fs) - bhh);

            int twh = spTruck.Width / 2, thh = spTruck.Height / 2;
            for (int i = 0; i < game.Display; i++)
            {
                float x, y;
                game.UnitPos(i, out x, out y);
                g.DrawImageUnscaled(spTruck, (int)(ox + x * fs) - twh, (int)(oy + y * fs) - thh);
            }

            // número da frota e para-choque
            float cx = ox + game.SquadX * fs, top = oy + (game.SquadTop - 30) * fs;
            string units = game.Units.ToString();
            float pw = (22 + 13 * units.Length) * fs, ph = 26 * fs;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 14, 16, 19))) g.FillRectangle(b, cx - pw / 2, top - ph / 2, pw, ph);
            using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffc727))) g.DrawString(units, sectionFont, b, new RectangleF(cx - pw / 2, top - ph / 2, pw, ph), CenterFmt);
            if (game.Shield > 0)
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x5cbf6d)))
                {
                    float sw = game.Columns() * 38 * fs;
                    g.FillRectangle(b, cx - sw / 2, oy + (game.SquadTop - 10) * fs, sw, 5 * fs);
                }

            foreach (Particle p in game.Particles)
            {
                int alpha = (int)(255 * Math.Max(0, p.Life / p.MaxLife));
                if (p.Text != null)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 0xf5, 0xec, 0xdc)))
                        g.DrawString(p.Text, labelFont, b, new RectangleF(ox + p.X * fs - 120 * fs, oy + p.Y * fs - 12 * fs, 240 * fs, 24 * fs), CenterFmt);
                else
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, p.Color)))
                        g.FillEllipse(b, ox + (p.X - p.Size / 2) * fs, oy + (p.Y - p.Size / 2) * fs, p.Size * fs, p.Size * fs);
            }

            if (game.Banner > 0)
            {
                Size ts = TextRenderer.MeasureText(game.BannerText, gScore);
                RectangleF r = new RectangleF(ox + (gField.Width - ts.Width - 48 * fs) / 2, oy + gameH * 0.36f * fs, ts.Width + 48 * fs, 64 * fs);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 14, 16, 19))) g.FillRectangle(b, r);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffc727))) g.DrawString(game.BannerText, gScore, b, r, CenterFmt);
            }

            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 0xb8, 0xab, 0x98)))
                g.DrawString(gFps + " fps", smallFont, b, ox + 28 * fs, oy + 8 * fs);
            g.Restore(st);
        }

        void Coin(Graphics g, float x, float y, float d)
        {
            using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffc727))) g.FillEllipse(b, x, y, d, d);
            using (Pen p = new Pen(Shapes.Hex(0xe8680c), Math.Max(1f, d / 13))) g.DrawEllipse(p, x, y, d, d);
            using (Pen p = new Pen(Shapes.Hex(0xe8680c), Math.Max(1f, d / 14))) g.DrawLine(p, x + d / 2, y + d * 0.27f, x + d / 2, y + d * 0.73f);
        }

        void Label(Graphics g, string text, float x, float y, float w)
        {
            DrawText(g, text, sectionFont, theme.Muted, new RectangleF(x, y, w, R(18)), LeftFmt);
        }

        void DrawGameHud(Graphics g)
        {
            if (game == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            string jets = game.Jets == 1 ? "Simples" : game.Jets == 2 ? "Duplo" : "Triplo";
            int toBoss = (5 - game.Wave % 5) % 5;
            string boss = game.BossAlive() ? "chegou!" : toBoss == 0 ? "nesta onda" : toBoss == 1 ? "na próxima onda" : "em " + toBoss + " ondas";

            if (!portrait)
            {
                using (SolidBrush b = new SolidBrush(theme.Panel)) { g.FillRectangle(b, gLeft); g.FillRectangle(b, gRight); }
                using (Pen p = new Pen(theme.Borda, 1)) { g.DrawLine(p, gLeft.Right - 1, 0, gLeft.Right - 1, view.Height); g.DrawLine(p, gRight.X, 0, gRight.X, view.Height); }

                float x = gLeft.X + R(22), w = gLeft.Width - R(44), y = R(24);
                Label(g, "ONDA", x, y, w); y += R(22);
                DrawText(g, game.Wave.ToString(), gNum, theme.Ink, new RectangleF(x - R(4), y, w, R(60)), LeftFmt); y += R(64);
                RoundBox(g, new Rectangle((int)x, (int)y, (int)w, R(8)), R(4), theme.Borda, theme.Borda, 0);
                float frac = game.WaveTotal > 0 ? Math.Min(1f, game.WaveDone / (float)game.WaveTotal) : 0;
                if (frac > 0) RoundBox(g, new Rectangle((int)x, (int)y, (int)(w * frac), R(8)), R(4), theme.Taxi, theme.Taxi, 0);
                y += R(14);
                DrawText(g, game.WaveDone + " de " + game.WaveTotal + " gosmas", sectionFont, theme.Muted, new RectangleF(x, y, w, R(18)), LeftFmt); y += R(38);
                Label(g, "PONTOS", x, y, w); y += R(20);
                DrawText(g, Fmt(game.Score), gScore, theme.Ink, new RectangleF(x - R(2), y, w, R(38)), LeftFmt); y += R(52);
                Label(g, "MOEDAS", x, y, w); y += R(22);
                Coin(g, x, y + R(5), R(26));
                DrawText(g, Fmt(game.Coins), gScore, theme.Ink, new RectangleF(x + R(34), y, w - R(34), R(38)), LeftFmt); y += R(54);
                using (Pen p = new Pen(theme.Borda, 1)) g.DrawLine(p, x, y, x + w, y);
                y += R(22);
                Label(g, "FROTA", x, y, w); y += R(24);
                g.DrawImage(spTruck, new RectangleF(x, y, R(20), R(38)));
                DrawText(g, game.Units + (game.Units == 1 ? " caminhão" : " caminhões"), buttonFont, theme.Ink, new RectangleF(x + R(30), y, w - R(30), R(38)), LeftFmt); y += R(58);
                Label(g, "JATO D'ÁGUA", x, y, w); y += R(26);
                string[,] rows = { { "Dano", game.EffectiveDamage.ToString("0.#", Br) }, { "Cadência", game.EffectiveRate.ToString("0.0", Br) + " por segundo" }, { "Jatos", jets } };
                for (int i = 0; i < 3; i++)
                {
                    DrawText(g, rows[i, 0], gStat, theme.Ink, new RectangleF(x, y, w, R(22)), LeftFmt);
                    DrawText(g, rows[i, 1], labelFont, theme.Ink, new RectangleF(x, y, w, R(22)), RightFmt);
                    y += R(28);
                }

                // painel direito
                RoundBox(g, gPause, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
                using (SolidBrush b = new SolidBrush(theme.Ink))
                {
                    g.FillRectangle(b, gPause.X + gPause.Width / 2 - R(12), gPause.Y + R(19), R(8), R(26));
                    g.FillRectangle(b, gPause.X + gPause.Width / 2 + R(4), gPause.Y + R(19), R(8), R(26));
                }
                x = gRight.X + R(22); w = gRight.Width - R(44); y = gPause.Bottom + R(22);
                Label(g, "RECORDE", x, y, w); y += R(22);
                DrawText(g, "Onda " + Config.GameRecordWave + " · " + Fmt(Config.GameRecordScore), buttonFont, theme.Ink, new RectangleF(x, y, w, R(28)), LeftFmt); y += R(48);
                Rectangle card = new Rectangle((int)x, (int)y, (int)w, R(84));
                RoundBox(g, card, 18 * sc, theme.Surface, GBoss, 2 * sc);
                g.DrawImage(spBoss, new RectangleF(card.X + R(14), card.Y + R(14), R(56), R(56)));
                DrawText(g, "Chefão", cozyTitleFont, theme.Ink, new RectangleF(card.X + R(82), card.Y + R(18), card.Width - R(90), R(24)), LeftFmt);
                DrawText(g, boss, sectionFont, theme.Muted, new RectangleF(card.X + R(82), card.Y + R(44), card.Width - R(90), R(20)), LeftFmt);
                y = card.Bottom + R(24);
                Label(g, "NESTA PARTIDA", x, y, w); y += R(26);
                float cxp = x;
                foreach (string s in game.Picked)
                {
                    Size ts = TextRenderer.MeasureText(s, sectionFont);
                    int chipW = ts.Width + R(20);
                    if (cxp + chipW > x + w) { cxp = x; y += R(38); }
                    Rectangle chip = new Rectangle((int)cxp, (int)y, chipW, R(30));
                    RoundBox(g, chip, chip.Height / 2f, theme.Surface, s.StartsWith("−") ? GBoss : theme.Trator, 1.5f * sc);
                    DrawText(g, s, sectionFont, theme.Ink, chip, CenterFmt);
                    cxp += chipW + R(8);
                }
                DrawText(g, "Atire nas plaquinhas para aumentar o número delas antes de passar.", sectionFont, theme.Muted,
                    new RectangleF(x, view.Height - R(84), w, R(60)), WrapFmt);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(theme.Panel)) { g.FillRectangle(b, gTop); g.FillRectangle(b, gBottom); }
                float colW = (gPause.X - R(20)) / 4f, y = R(24);
                string[] labels = { "ONDA", "PONTOS", "MOEDAS", "FROTA" };
                string[] vals = { game.Wave.ToString(), Fmt(game.Score), Fmt(game.Coins), game.Units.ToString() };
                for (int i = 0; i < 4; i++)
                {
                    float x = R(20) + i * colW;
                    Label(g, labels[i], x, y, colW);
                    DrawText(g, vals[i], gScore, theme.Ink, new RectangleF(x, y + R(24), colW, R(40)), LeftFmt);
                }
                RoundBox(g, new Rectangle(R(20), R(110), (int)(colW * 4 - R(20)), R(8)), R(4), theme.Borda, theme.Borda, 0);
                float frac = game.WaveTotal > 0 ? Math.Min(1f, game.WaveDone / (float)game.WaveTotal) : 0;
                if (frac > 0) RoundBox(g, new Rectangle(R(20), R(110), (int)((colW * 4 - R(20)) * frac), R(8)), R(4), theme.Taxi, theme.Taxi, 0);
                RoundBox(g, gPause, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
                using (SolidBrush b = new SolidBrush(theme.Ink))
                {
                    g.FillRectangle(b, gPause.X + gPause.Width / 2 - R(12), gPause.Y + R(19), R(8), R(26));
                    g.FillRectangle(b, gPause.X + gPause.Width / 2 + R(4), gPause.Y + R(19), R(8), R(26));
                }
                string line = "Recorde: onda " + Config.GameRecordWave + "   ·   Chefão " + boss + "   ·   Dano " + game.EffectiveDamage.ToString("0.#", Br) + " · " + jets;
                DrawText(g, line, labelFont, theme.Ink, new RectangleF(R(20), gBottom.Y, view.Width - R(40), gBottom.Height), CenterFmt);
            }
        }

        void DrawRoadDecor(Graphics g, Rectangle r)
        {
            using (SolidBrush b = new SolidBrush(GRoad)) g.FillRectangle(b, r);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 0xff, 0xc7, 0x27))) g.FillRectangle(b, r.X + R(8), r.Y, R(5), r.Height);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 0xf5, 0xec, 0xdc)))
                for (int y = r.Y + R(30); y < r.Bottom; y += R(120)) g.FillRectangle(b, r.X + r.Width / 2, y, R(8), R(48));
            float[] gx = { 0.23f, 0.43f, 0.67f, 0.85f, 0.31f, 0.79f };
            float[] gy = { 0.11f, 0.08f, 0.15f, 0.09f, 0.24f, 0.28f };
            for (int i = 0; i < gx.Length; i++)
            {
                Bitmap s = i == 2 ? spBig : spSmall;
                g.DrawImageUnscaled(s, (int)(r.X + r.Width * gx[i]) - s.Width / 2, (int)(r.Y + r.Height * gy[i]) - s.Height / 2);
            }
            for (int i = 0; i < 6; i++)
                g.DrawImageUnscaled(spBullet, (int)(r.X + r.Width * (0.38f + 0.1f * (i % 4))) - spBullet.Width / 2, (int)(r.Y + r.Height * (0.45f + 0.07f * (i % 3))));
            for (int i = 0; i < 7; i++)
            {
                int row = i < 4 ? 0 : 1, col = i < 4 ? i : i - 4;
                float x = r.X + r.Width * 0.5f + (col - (row == 0 ? 1.5f : 1f)) * 40 * fs;
                g.DrawImageUnscaled(spTruck, (int)x - spTruck.Width / 2, (int)(r.Y + r.Height * 0.78f + row * 56 * fs) - spTruck.Height / 2);
            }
        }

        void DrawGameMenu(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle road = portrait ? new Rectangle(0, view.Height - R(380), view.Width, R(380)) : new Rectangle(R(890), 0, view.Width - R(890), view.Height);
            DrawRoadDecor(g, road);

            RoundBox(g, gBack, 14 * sc, theme.Panel, theme.Borda, 2 * sc);
            using (Pen p = Shapes.RoundPen(theme.Ink, 2.4f * sc))
                g.DrawLines(p, new PointF[] { new PointF(gBack.X + R(26), gBack.Y + R(18)), new PointF(gBack.X + R(19), gBack.Y + R(26)), new PointF(gBack.X + R(26), gBack.Y + R(34)) });
            DrawText(g, "Voltar", labelFont, theme.Ink, new RectangleF(gBack.X + R(40), gBack.Y, gBack.Width - R(48), gBack.Height), LeftFmt);

            RoundBox(g, gCoins, gCoins.Height / 2f, theme.Panel, theme.Taxi, 2 * sc);
            Coin(g, gCoins.X + R(16), gCoins.Y + R(13), R(26));
            DrawText(g, Fmt(Config.GameCoins), buttonFont, theme.Ink, new RectangleF(gCoins.X + R(50), gCoins.Y, gCoins.Width - R(60), gCoins.Height), LeftFmt);

            float x0 = gPlay.X, y0 = gPlay.Y - R(294), tw = Math.Min(R(700), view.Width - x0 - R(40));
            DrawText(g, "JOGUINHO", sectionFont, theme.Cone, new RectangleF(x0, y0, tw, R(18)), LeftFmt);
            DrawText(g, "Comboio", gHuge, theme.Taxi, new RectangleF(x0 - R(8), y0 + R(22), tw, R(110)), LeftFmt);
            DrawText(g, "Segure a horda de gosmas com sua frota de bombeiros.",
                subtitleFont, theme.Ink, new RectangleF(x0, y0 + R(134), Math.Min(R(560), tw), R(80)), WrapFmt);
            string rec = Config.GameRecordWave > 0 ? "Recorde: onda " + Config.GameRecordWave + " · " + Fmt(Config.GameRecordScore) + " pontos" : "Ainda sem recorde: bora jogar!";
            Size rs = TextRenderer.MeasureText(rec, labelFont);
            Rectangle pill = new Rectangle((int)x0, (int)(y0 + R(226)), rs.Width + R(28), R(40));
            RoundBox(g, pill, pill.Height / 2f, theme.Panel, theme.Panel, 0);
            DrawText(g, rec, labelFont, theme.Ink, pill, CenterFmt);

            RoundBox(g, gPlay, 20 * sc, theme.Taxi, theme.Cone, 3 * sc);
            using (SolidBrush b = new SolidBrush(theme.OnTaxi))
                g.FillPolygon(b, new PointF[] { new PointF(gPlay.X + R(92), gPlay.Y + R(29)), new PointF(gPlay.X + R(114), gPlay.Y + R(42)), new PointF(gPlay.X + R(92), gPlay.Y + R(55)) });
            DrawText(g, "Jogar", gScore, theme.OnTaxi, new RectangleF(gPlay.X + R(126), gPlay.Y, gPlay.Width - R(130), gPlay.Height), LeftFmt);
            RoundBox(g, gShop, 20 * sc, theme.Panel, theme.Borda, 2 * sc);
            DrawText(g, "Oficina", toastFont, theme.Ink, gShop, CenterFmt);

            float hy = gPlay.Bottom + R(28);
            DrawText(g, "COMO JOGAR", sectionFont, theme.Muted, new RectangleF(x0, hy, tw, R(18)), LeftFmt);
            DrawText(g, "Arraste o dedo para os lados: a frota atira sozinha. Passe pelas plaquinhas verdes e azuis para ganhar caminhões e melhorar o jato. Atire nas vermelhas para virar o jogo. A cada 5 ondas vem um chefão.",
                gStat, theme.Ink, new RectangleF(x0, hy + R(24), Math.Min(R(560), tw), R(100)), WrapFmt);

            DrawLockButton(g, homeLock, 18 * sc);
        }

        int UpgCost(int i) { int l = Config.GameUpg[i]; return UpgBase[i] * (l + 1) * (l + 2) / 2; }

        static string UpgValue(int i, int l)
        {
            switch (i)
            {
                case 0: return (1 + l) + (l == 0 ? " caminhão" : " caminhões");
                case 1: return "dano ×" + (1 + 0.5f * l).ToString("0.0", Br);
                case 2: return (2f * (1 + 0.15f * l)).ToString("0.0", Br) + " por s";
                case 3: return (1 + l) + (l == 0 ? " jato" : " jatos");
                case 4: return "+" + (20 * l) + "%";
                default: return l == 0 ? "nenhum" : l + (l == 1 ? " batida" : " batidas");
            }
        }

        void DrawUpgIcon(Graphics g, int i, RectangleF r)
        {
            switch (i)
            {
                case 0:
                    g.DrawImage(spTruck, new RectangleF(r.X + r.Width * 0.3f, r.Y + r.Height * 0.12f, r.Width * 0.4f, r.Height * 0.76f));
                    return;
            }
            string[] d = {
                "", "M5 1 C6.8 3.4 8 5 8 6.4 A3 3 0 0 1 2 6.4 C2 5 3.2 3.4 5 1 Z", "M5.8 0.8 L2 5.6 H4.8 L4 9.2 L8 4.2 H5.2 Z", "",
                "M2 1.5 V5.5 A3 3 0 0 0 8 5.5 V1.5 H6.2 V5.5 A1.2 1.2 0 0 1 3.8 5.5 V1.5 Z", "M5 0.8 L8.6 2.2 V5 Q8.6 7.8 5 9.2 Q1.4 7.8 1.4 5 V2.2 Z"
            };
            Color[] c = { Color.Empty, GWater, Shapes.Hex(0xffc727), GWater, GBoss, Shapes.Hex(0x5cbf6d) };
            GraphicsState st = g.Save();
            g.TranslateTransform(r.X + r.Width * 0.2f, r.Y + r.Height * 0.2f);
            g.ScaleTransform(r.Width * 0.06f, r.Height * 0.06f);
            if (i == 3)
                using (Pen p = Shapes.RoundPen(GWater, 1.1f)) { g.DrawLine(p, 2, 8.5f, 1, 2); g.DrawLine(p, 5, 8.5f, 5, 1.5f); g.DrawLine(p, 8, 8.5f, 9, 2); }
            else
                using (GraphicsPath p = SvgPath.Parse(d[i])) FillStrokePath(g, p, c[i], GBlack, 0.4f);
            g.Restore(st);
        }

        void DrawGameShop(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RoundBox(g, gShopBack, 16 * sc, theme.Panel, theme.Borda, 2 * sc);
            using (Pen p = Shapes.RoundPen(theme.Ink, 2.6f * sc))
            {
                float cx = gShopBack.X + gShopBack.Width / 2f, cy = gShopBack.Y + gShopBack.Height / 2f;
                g.DrawLines(p, new PointF[] { new PointF(cx + R(4), cy - R(9)), new PointF(cx - R(5), cy), new PointF(cx + R(4), cy + R(9)) });
            }
            DrawText(g, "Oficina", titleFont, theme.Ink, new RectangleF(gShopBack.Right + R(20), gShopBack.Y - R(6), R(500), R(44)), LeftFmt);
            DrawText(g, "Melhorias que valem para todas as partidas", labelFont, theme.Muted, new RectangleF(gShopBack.Right + R(20), gShopBack.Y + R(36), R(600), R(22)), LeftFmt);
            Rectangle coins = new Rectangle(view.Width - R(48) - R(170), gShopBack.Y, R(170), R(56));
            RoundBox(g, coins, coins.Height / 2f, theme.Panel, theme.Taxi, 2 * sc);
            Coin(g, coins.X + R(18), coins.Y + R(14), R(28));
            DrawText(g, Fmt(Config.GameCoins), buttonFont, theme.Ink, new RectangleF(coins.X + R(54), coins.Y, coins.Width - R(60), coins.Height), LeftFmt);

            for (int i = 0; i < 6; i++)
            {
                Rectangle c = gCards[i];
                RoundBox(g, c, 22 * sc, theme.Panel, theme.Borda, 2 * sc);
                Rectangle icon = new Rectangle(c.X + R(22), c.Y + R(22), R(56), R(56));
                RoundBox(g, icon, 16 * sc, theme.Surface, theme.Surface, 0);
                DrawUpgIcon(g, i, icon);
                DrawText(g, UpgNames[i], buttonFont, theme.Ink, new RectangleF(icon.Right + R(14), icon.Y + R(2), c.Right - icon.Right - R(30), R(28)), LeftFmt);
                DrawText(g, UpgDesc[i], sectionFont, theme.Muted, new RectangleF(icon.Right + R(14), icon.Y + R(32), c.Right - icon.Right - R(30), R(22)), LeftFmt);

                int lvl = Config.GameUpg[i], max = UpgMax[i];
                float dy = icon.Bottom + R(14), dw = (c.Width - R(44) - (max - 1) * R(6)) / (float)max;
                for (int k = 0; k < max; k++)
                    RoundBox(g, new Rectangle((int)(c.X + R(22) + k * (dw + R(6))), (int)dy, (int)dw, R(8)), R(4), k < lvl ? theme.Taxi : theme.Borda, Color.Empty, 0);

                bool maxed = lvl >= max;
                string now = UpgValue(i, lvl), next = maxed ? "máximo" : UpgValue(i, lvl + 1);
                DrawText(g, "Agora " + now + "  ›  " + next, labelFont, theme.Ink, new RectangleF(c.X + R(22), dy + R(16), c.Width - R(44), R(24)), LeftFmt);

                int cost = UpgCost(i);
                bool can = !maxed && Config.GameCoins >= cost;
                Rectangle b = gBuy[i];
                if (can) RoundBox(g, b, 16 * sc, theme.Taxi, theme.Cone, 3 * sc);
                else RoundBox(g, b, 16 * sc, theme.Surface, theme.Borda, 2 * sc);
                string label = maxed ? "Máximo" : can ? "Melhorar · " + Fmt(cost) : "Faltam " + Fmt(cost - Config.GameCoins) + " moedas";
                DrawText(g, label, buttonFont, can ? theme.OnTaxi : theme.Muted, b, CenterFmt);
            }
        }

        void DrawPauseCard(Graphics g)
        {
            LayoutGameOverlay();
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(190, 14, 16, 19))) g.FillRectangle(dim, new Rectangle(Point.Empty, view));
            RoundBox(g, gPanel, 28 * sc, theme.Panel, theme.Borda, 2 * sc);
            DrawText(g, "Pausado", titleFont, theme.Ink, new RectangleF(gPanel.X, gPanel.Y + R(36), gPanel.Width, R(52)), CenterFmt);
            RoundBox(g, gBtnA, 18 * sc, theme.Taxi, theme.Cone, 3 * sc);
            DrawText(g, "Continuar", buttonFont, theme.OnTaxi, gBtnA, CenterFmt);
            RoundBox(g, gBtnB, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, "Recomeçar", buttonFont, theme.Ink, gBtnB, CenterFmt);
            RoundBox(g, gBtnC, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, "Sair para o menu", buttonFont, theme.Ink, gBtnC, CenterFmt);
        }

        void DrawOverCard(Graphics g)
        {
            LayoutGameOverlay();
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(200, 14, 16, 19))) g.FillRectangle(dim, new Rectangle(Point.Empty, view));
            RoundBox(g, gPanel, 28 * sc, theme.Panel, theme.Borda, 2 * sc);
            float y = gPanel.Y + R(36);
            if (newRecord)
            {
                Size ts = TextRenderer.MeasureText("Novo recorde!", labelFont);
                Rectangle badge = new Rectangle(gPanel.X + (gPanel.Width - ts.Width - R(32)) / 2, (int)y, ts.Width + R(32), R(34));
                RoundBox(g, badge, badge.Height / 2f, theme.Taxi, theme.Taxi, 0);
                DrawText(g, "Novo recorde!", labelFont, theme.OnTaxi, badge, CenterFmt);
            }
            y += R(44);
            DrawText(g, "Fim de jogo", titleFont, theme.Ink, new RectangleF(gPanel.X, y, gPanel.Width, R(52)), CenterFmt); y += R(54);
            DrawText(g, "A horda passou na onda " + game.Wave, gStat, theme.Muted, new RectangleF(gPanel.X, y, gPanel.Width, R(26)), CenterFmt); y += R(44);

            string[] labels = { "ONDA", "PONTOS", "GOSMAS", "MOEDAS GANHAS" };
            string[] vals = { game.Wave.ToString(), Fmt(game.Score), Fmt(game.Kills), "+" + Fmt(lastCoins) };
            int bw = (gPanel.Width - R(80) - R(12)) / 2, bh = R(84);
            for (int i = 0; i < 4; i++)
            {
                Rectangle r = new Rectangle(gPanel.X + R(40) + (i % 2) * (bw + R(12)), (int)y + (i / 2) * (bh + R(12)), bw, bh);
                RoundBox(g, r, 18 * sc, theme.Surface, i == 3 ? theme.Taxi : theme.Surface, i == 3 ? 2 * sc : 0);
                Label(g, labels[i], r.X + R(18), r.Y + R(14), r.Width - R(30));
                DrawText(g, vals[i], gScore, i == 3 ? theme.Taxi : theme.Ink, new RectangleF(r.X + R(16), r.Y + R(34), r.Width - R(30), R(40)), LeftFmt);
            }

            RoundBox(g, gBtnA, 18 * sc, theme.Taxi, theme.Cone, 3 * sc);
            DrawText(g, "Jogar de novo", buttonFont, theme.OnTaxi, gBtnA, CenterFmt);
            RoundBox(g, gBtnB, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, "Oficina", buttonFont, theme.Ink, gBtnB, CenterFmt);
            RoundBox(g, gBtnC, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, "Menu", buttonFont, theme.Ink, gBtnC, CenterFmt);
        }

        // ---------- toque ----------

        void GameDown(Contact c, Point p)
        {
            switch (gstate)
            {
                case GState.Menu:
                    if (gBack.Contains(p)) GoTo(Screen.Jogos);
                    else if (gPlay.Contains(p)) StartGame();
                    else if (gShop.Contains(p)) { gstate = GState.Shop; Invalidate(); }
                    break;
                case GState.Shop:
                    if (gShopBack.Contains(p)) { gstate = GState.Menu; Invalidate(); return; }
                    for (int i = 0; i < 6; i++)
                    {
                        if (!gBuy[i].Contains(p)) continue;
                        int cost = UpgCost(i);
                        if (Config.GameUpg[i] < UpgMax[i] && Config.GameCoins >= cost)
                        {
                            Config.GameCoins -= cost;
                            Config.GameUpg[i]++;
                            Config.Save();
                            Invalidate();
                        }
                        return;
                    }
                    break;
                case GState.Play:
                    if (gPause.Contains(p)) { PauseGame(); return; }
                    if (gField.Contains(p) && game != null)
                    {
                        float start = game.SquadTarget;
                        int downX = p.X;
                        c.Role = Role.Arrastar;
                        c.Drag = delegate(Point q) { if (game != null) game.SquadTarget = start + (q.X - downX) / fs; };
                    }
                    break;
                case GState.Pause:
                    LayoutGameOverlay();
                    if (gBtnA.Contains(p)) { gstate = GState.Play; Invalidate(); StartLoop(); }
                    else if (gBtnB.Contains(p)) StartGame();
                    else if (gBtnC.Contains(p)) { game = null; gstate = GState.Menu; Invalidate(); }
                    break;
                case GState.Over:
                    LayoutGameOverlay();
                    if (gBtnA.Contains(p)) StartGame();
                    else if (gBtnB.Contains(p)) { game = null; gstate = GState.Shop; Invalidate(); }
                    else if (gBtnC.Contains(p)) { game = null; gstate = GState.Menu; Invalidate(); }
                    break;
            }
        }

        void PauseGame()
        {
            if (gstate != GState.Play) return;
            StopLoop();
            gstate = GState.Pause;
            Invalidate();
        }

        bool GameKey(Keys key)
        {
            if (screen != Screen.Jogo || game == null) return false;
            if (gstate == GState.Play)
            {
                if (key == Keys.Left) { game.SquadTarget -= 70; return true; }
                if (key == Keys.Right) { game.SquadTarget += 70; return true; }
                if (key == Keys.P || key == Keys.Escape) { PauseGame(); return true; }
            }
            else if (gstate == GState.Pause && (key == Keys.P || key == Keys.Escape))
            {
                gstate = GState.Play;
                Invalidate();
                StartLoop();
                return true;
            }
            return false;
        }
    }
}
