using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Pintinho
{
    enum SState { Pronto, Jogando, Pausado, Fim }

    // Escolha de joguinho (Comboio / Cobrinha) e a tela da Cobrinha.
    // Botões pensados para segurar o tablet com as duas mãos: dedão esquerdo ↑ ↓, dedão direito ← →.
    partial class MainForm
    {
        static readonly Color LcdBg = Shapes.Hex(0x9bbc0f), LcdMid = Shapes.Hex(0x306230), LcdDark = Shapes.Hex(0x0f380f);

        // ---------- escolha de jogo ----------

        Rectangle hubBack;
        readonly Rectangle[] hubCards = new Rectangle[2];

        void LayoutHub()
        {
            int W = view.Width;
            hubBack = new Rectangle(R(40), R(36), R(130), R(52));
            int y0 = R(214);
            if (!portrait)
            {
                int w = R(440), h = R(470), gap = R(36), x0 = (W - 2 * w - gap) / 2;
                for (int i = 0; i < 2; i++) hubCards[i] = new Rectangle(x0 + i * (w + gap), y0, w, h);
            }
            else
            {
                int w = W - 2 * R(48), h = R(440), gap = R(32);
                for (int i = 0; i < 2; i++) hubCards[i] = new Rectangle(R(48), y0 + i * (h + gap), w, h);
            }
        }

        void DrawBackButton(Graphics g, Rectangle r, string label)
        {
            RoundBox(g, r, 14 * sc, theme.Panel, theme.Borda, 2 * sc);
            using (Pen p = Shapes.RoundPen(theme.Ink, 2.4f * sc))
                g.DrawLines(p, new PointF[] { new PointF(r.X + R(26), r.Y + r.Height / 2f - R(8)), new PointF(r.X + R(19), r.Y + r.Height / 2f), new PointF(r.X + R(26), r.Y + r.Height / 2f + R(8)) });
            if (label != null) DrawText(g, label, labelFont, theme.Ink, new RectangleF(r.X + R(40), r.Y, r.Width - R(48), r.Height), LeftFmt);
        }

        void PaintHub(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawBackButton(g, hubBack, "Início");
            DrawText(g, "Joguinhos", bigFont, theme.Taxi, new RectangleF(0, R(52), view.Width, R(80)), CenterFmt);
            DrawText(g, "Escolha um jogo", subtitleFont, theme.Muted, new RectangleF(0, R(134), view.Width, R(36)), CenterFmt);

            string rec0 = Config.GameRecordWave > 0 ? "Recorde: onda " + Config.GameRecordWave : "Novo";
            string rec1 = Config.SnakeRecord > 0 ? "Recorde: " + Config.SnakeRecord : "Novo";
            DrawHubCard(g, hubCards[0], theme.Taxi, "Comboio", rec0, "Segure a horda de gosmas com sua frota de bombeiros.", false);
            DrawHubCard(g, hubCards[1], LcdBg, "Cobrinha", rec1, "A clássica: coma, cresça e não morda o próprio rabo.", true);
            DrawLockButton(g, homeLock, 18 * sc);
        }

        void DrawHubCard(Graphics g, Rectangle r, Color border, string title, string chip, string desc, bool snake)
        {
            RoundBox(g, r, 28 * sc, GRoad, border, 3 * sc);
            int pad = R(24);
            Rectangle img = portrait
                ? new Rectangle(r.X + pad, r.Y + pad, r.Width - 2 * pad, R(250))
                : new Rectangle(r.X + pad, r.Y + pad, r.Width - 2 * pad, R(230));
            RoundBox(g, img, 18 * sc, snake ? LcdBg : Shapes.Hex(0x1c1f24), Color.Empty, 0);
            if (snake) DrawSnakeArt(g, img);
            else DrawComboioArt(g, img);
            RectangleF titleR = new RectangleF(r.X + pad, img.Bottom + R(16), r.Width - 2 * pad, R(40));
            DrawText(g, title, cardTitleFont, theme.Ink, titleR, LeftFmt);
            Size cs = TextRenderer.MeasureText(chip, labelFont);
            Rectangle chipR = new Rectangle((int)titleR.Right - cs.Width - R(24), (int)(titleR.Y + (titleR.Height - R(28)) / 2), cs.Width + R(24), R(28));
            RoundBox(g, chipR, chipR.Height / 2f, snake ? LcdBg : Shapes.Hex(0x1c1f24), Color.Empty, 0);
            DrawText(g, chip, labelFont, snake ? LcdDark : theme.Ink, chipR, CenterFmt);
            DrawText(g, desc, bodyFont, theme.Muted, new RectangleF(r.X + pad, titleR.Bottom + R(14), r.Width - 2 * pad, R(60)), WrapFmt);
        }

        // Mini cena do Comboio (cartões da tela inicial e da escolha de jogo).
        void DrawComboioArt(Graphics g, Rectangle img)
        {
            GraphicsState st = g.Save();
            g.SetClip(img);
            float cx = img.X + img.Width / 2f;
            g.DrawImageUnscaled(spSmall, (int)(cx - img.Width * 0.3f) - spSmall.Width / 2, img.Y + img.Height / 4 - spSmall.Height / 2);
            g.DrawImageUnscaled(spBig, (int)cx - spBig.Width / 2, img.Y + img.Height / 5 - spBig.Height / 2);
            g.DrawImageUnscaled(spSmall, (int)(cx + img.Width * 0.3f) - spSmall.Width / 2, img.Y + img.Height / 3 - spSmall.Height / 2);
            for (int i = 0; i < 3; i++)
                g.DrawImageUnscaled(spBullet, (int)(cx + (i - 1) * 40 * fs) - spBullet.Width / 2, img.Y + img.Height / 2 + (i == 1 ? -R(10) : 0));
            for (int i = 0; i < 3; i++)
                g.DrawImageUnscaled(spTruck, (int)(cx + (i - 1) * 40 * fs) - spTruck.Width / 2, img.Bottom - spTruck.Height + R(4));
            g.Restore(st);
        }

        void DrawSnakeArt(Graphics g, Rectangle img)
        {
            float c = Math.Min(img.Width / 20f, img.Height / 14f);
            float ox = img.X + (img.Width - 20 * c) / 2, oy = img.Y + (img.Height - 14 * c) / 2;
            int[] cells = { 3, 9, 4, 9, 5, 9, 6, 9, 6, 8, 6, 7, 6, 6, 7, 6, 8, 6, 9, 6, 10, 6, 10, 5, 10, 4 };
            using (SolidBrush b = new SolidBrush(LcdDark))
                for (int i = 0; i < cells.Length; i += 2)
                    g.FillRectangle(b, ox + cells[i] * c + c * 0.05f, oy + cells[i + 1] * c + c * 0.05f, c * 0.9f, c * 0.9f);
            using (SolidBrush b = new SolidBrush(LcdMid)) g.FillEllipse(b, ox + 14 * c + c * 0.08f, oy + 4 * c + c * 0.08f, c * 0.84f, c * 0.84f);
        }

        void HubDown(Point p)
        {
            if (hubBack.Contains(p)) GoTo(Screen.Inicio);
            else if (hubCards[0].Contains(p)) GoTo(Screen.Jogo);
            else if (hubCards[1].Contains(p)) GoTo(Screen.Cobra);
        }

        // ---------- cobrinha ----------

        Snake snake;
        SState sstate = SState.Pronto;
        bool snakeRecord;
        Rectangle sField, sLcd, sBack, sPause, sPanel, sBtnA, sBtnB;
        RectangleF sScoreR, sRecR;
        readonly Rectangle[] sArrows = new Rectangle[4]; // cima, baixo, esquerda, direita
        static readonly Point[] ArrowDir = { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) };
        readonly Dictionary<int, int> sPressed = new Dictionary<int, int>();
        readonly Timer snakeTimer = new Timer();
        readonly Stopwatch snakeWatch = new Stopwatch();
        double snakeLast, snakeAcc;
        Font sScoreFont;
        Point sPadCenter;
        int sPadRadius;

        void LayoutSnake()
        {
            if (sScoreFont != null) sScoreFont.Dispose();
            sScoreFont = F(44, FontStyle.Bold);
            int W = view.Width, H = view.Height, gap = R(8);
            sBack = new Rectangle(R(24), R(24), R(56), R(56));
            int btn, cx, cy;
            if (!portrait)
            {
                // campo à direita; cruz de controle à esquerda (dedão esquerdo)
                int outer = Math.Min(H - R(80), W - R(560));
                sField = new Rectangle(W - R(40) - outer, (H - outer) / 2, outer, outer);
                sPause = new Rectangle(sField.X - R(24) - R(64), R(24), R(64), R(64));
                sScoreR = new RectangleF(R(104), R(14), R(200), R(70));
                sRecR = new RectangleF(R(104), R(92), R(200), R(54));
                btn = Math.Min(R(130), (sField.X - R(64)) / 3);
                cx = sField.X / 2;
                cy = Math.Max(R(180) + btn * 3 / 2, H - R(48) - btn * 3 / 2);
            }
            else
            {
                // campo em cima; cruz de controle embaixo, no centro
                btn = R(118);
                int outer = Math.Min(W - R(40), H - R(110) - 3 * btn - R(90));
                sField = new Rectangle((W - outer) / 2, R(110), outer, outer);
                sPause = new Rectangle(W - R(24) - R(64), R(24), R(64), R(64));
                sScoreR = new RectangleF(R(104), R(14), R(200), R(70));
                sRecR = new RectangleF(R(330), R(22), R(220), R(54));
                cx = W / 2;
                cy = sField.Bottom + (H - sField.Bottom) / 2;
            }
            sPadCenter = new Point(cx, cy);
            sPadRadius = btn * 3 / 2 + gap + R(20);
            sArrows[0] = new Rectangle(cx - btn / 2, cy - btn / 2 - gap - btn, btn, btn);
            sArrows[1] = new Rectangle(cx - btn / 2, cy + btn / 2 + gap, btn, btn);
            sArrows[2] = new Rectangle(cx - btn / 2 - gap - btn, cy - btn / 2, btn, btn);
            sArrows[3] = new Rectangle(cx + btn / 2 + gap, cy - btn / 2, btn, btn);
            sLcd = Rectangle.Inflate(sField, -R(14), -R(14));

            sPanel = Center(new Rectangle(Point.Empty, view), Math.Min(W - R(40), R(520)), R(sstate == SState.Fim ? 400 : 300));
            int bw = sPanel.Width - R(80);
            sBtnB = new Rectangle(sPanel.Right - R(40) - R(140), sPanel.Bottom - R(36) - R(68), R(140), R(68));
            sBtnA = new Rectangle(sPanel.X + R(40), sBtnB.Y, sBtnB.X - R(12) - sPanel.X - R(40), R(68));
            if (bw < 0) bw = 0;
        }

        void EnterSnake()
        {
            snake = new Snake(Environment.TickCount);
            sstate = SState.Pronto;
            sPressed.Clear();
            if (snakeTimer.Interval != 15)
            {
                snakeTimer.Interval = 15;
                snakeTimer.Tick += OnSnakeTick;
            }
        }

        void StartSnakeClock()
        {
            snakeWatch.Reset();
            snakeWatch.Start();
            snakeLast = snakeAcc = 0;
            snakeTimer.Start();
        }

        void StopSnakeClock()
        {
            snakeTimer.Stop();
            snakeWatch.Stop();
        }

        void OnSnakeTick(object sender, EventArgs e)
        {
            if (screen != Screen.Cobra || sstate != SState.Jogando || snake == null) { StopSnakeClock(); return; }
            double now = snakeWatch.Elapsed.TotalSeconds;
            snakeAcc += Math.Min(0.25, now - snakeLast);
            snakeLast = now;
            bool moved = false;
            while (snakeAcc >= snake.Interval && !snake.Over)
            {
                snakeAcc -= snake.Interval;
                snake.Step();
                moved = true;
            }
            if (snake.Over) { SnakeOver(); return; }
            if (moved)
            {
                Invalidate(sLcd);
                Invalidate(Rectangle.Round(sScoreR));
            }
        }

        void SnakeOver()
        {
            StopSnakeClock();
            sstate = SState.Fim;
            snakeRecord = snake.Score > Config.SnakeRecord;
            if (snakeRecord)
            {
                Config.SnakeRecord = snake.Score;
                Config.Save();
            }
            LayoutSnake();
            Invalidate();
        }

        void SnakeTurn(int arrow)
        {
            if (snake == null) return;
            if (sstate == SState.Pronto)
            {
                sstate = SState.Jogando;
                StartSnakeClock();
                Invalidate();
            }
            if (sstate != SState.Jogando) return;
            snake.Turn(ArrowDir[arrow].X, ArrowDir[arrow].Y);
        }

        void PauseSnake()
        {
            if (sstate != SState.Jogando) return;
            StopSnakeClock();
            sstate = SState.Pausado;
            LayoutSnake();
            Invalidate();
        }

        void PaintSnake(Graphics g, Rectangle clip)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (snake == null) return;

            // LCD
            if (clip.IntersectsWith(sField))
            {
                RoundBox(g, sField, 24 * sc, theme.Panel, theme.Panel, 0);
                RoundBox(g, sLcd, 10 * sc, LcdBg, Color.Empty, 0);
                float c = sLcd.Width / (float)Snake.N;
                g.SmoothingMode = SmoothingMode.None;
                using (Pen grid = new Pen(Color.FromArgb(20, LcdDark), 1))
                    for (int i = 1; i < Snake.N; i++)
                    {
                        g.DrawLine(grid, sLcd.X + i * c, sLcd.Y, sLcd.X + i * c, sLcd.Bottom);
                        g.DrawLine(grid, sLcd.X, sLcd.Y + i * c, sLcd.Right, sLcd.Y + i * c);
                    }
                float m = Math.Max(1, c * 0.08f);
                using (SolidBrush b = new SolidBrush(LcdDark))
                    for (int i = 1; i < snake.Body.Count; i++)
                    {
                        Point p = snake.Body[i];
                        g.FillRectangle(b, sLcd.X + p.X * c + m, sLcd.Y + p.Y * c + m, c - 2 * m, c - 2 * m);
                    }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Point h = snake.Body[0];
                RectangleF hr = new RectangleF(sLcd.X + h.X * c + m * 0.5f, sLcd.Y + h.Y * c + m * 0.5f, c - m, c - m);
                using (GraphicsPath hp = Shapes.RoundRect(hr, c * 0.3f))
                using (SolidBrush b = new SolidBrush(LcdDark)) g.FillPath(b, hp);
                using (SolidBrush eye = new SolidBrush(LcdBg))
                {
                    float ex = hr.X + hr.Width / 2 + snake.Dir.X * hr.Width * 0.18f, ey = hr.Y + hr.Height / 2 + snake.Dir.Y * hr.Height * 0.18f, er = c * 0.12f;
                    g.FillEllipse(eye, ex - snake.Dir.Y * c * 0.18f - er, ey - snake.Dir.X * c * 0.18f - er, 2 * er, 2 * er);
                    g.FillEllipse(eye, ex + snake.Dir.Y * c * 0.18f - er, ey + snake.Dir.X * c * 0.18f - er, 2 * er, 2 * er);
                }
                RectangleF fr = new RectangleF(sLcd.X + snake.Food.X * c + c * 0.15f, sLcd.Y + snake.Food.Y * c + c * 0.15f, c * 0.7f, c * 0.7f);
                using (SolidBrush b = new SolidBrush(LcdMid)) g.FillEllipse(b, fr);
                using (Pen p = new Pen(LcdDark, Math.Max(1.5f, c * 0.13f))) g.DrawEllipse(p, fr);
                if (sstate == SState.Pronto)
                    DrawText(g, "Toque numa seta para começar", buttonFont, LcdDark, new RectangleF(sLcd.X, sLcd.Y + sLcd.Height * 0.62f, sLcd.Width, R(40)), CenterFmt);
            }

            // placar e botões de cima
            DrawBackButton(g, sBack, null);
            DrawText(g, "PONTOS", sectionFont, theme.Muted, new RectangleF(sScoreR.X, sScoreR.Y, sScoreR.Width, R(18)), LeftFmt);
            DrawText(g, snake.Score.ToString("0000"), sScoreFont, LcdBg, new RectangleF(sScoreR.X - R(2), sScoreR.Y + R(18), sScoreR.Width, R(52)), LeftFmt);
            DrawText(g, "RECORDE", sectionFont, theme.Muted, new RectangleF(sRecR.X, sRecR.Y, sRecR.Width, R(18)), LeftFmt);
            DrawText(g, Config.SnakeRecord.ToString("0000"), gScore, theme.Ink, new RectangleF(sRecR.X - R(2), sRecR.Y + R(18), sRecR.Width, R(38)), LeftFmt);
            RoundBox(g, sPause, 18 * sc, theme.Panel, theme.Borda, 2 * sc);
            using (SolidBrush b = new SolidBrush(theme.Ink))
            {
                g.FillRectangle(b, sPause.X + sPause.Width / 2 - R(12), sPause.Y + R(19), R(8), R(26));
                g.FillRectangle(b, sPause.X + sPause.Width / 2 + R(4), sPause.Y + R(19), R(8), R(26));
            }

            // cruz de controle
            int hub = sArrows[0].Width;
            Rectangle mid = new Rectangle(sPadCenter.X - hub / 2, sPadCenter.Y - hub / 2, hub, hub);
            RoundBox(g, Rectangle.Inflate(mid, R(4), R(4)), 18 * sc, theme.Panel, theme.Panel, 0);
            using (SolidBrush b = new SolidBrush(theme.Borda)) g.FillEllipse(b, Rectangle.Inflate(mid, -hub / 3, -hub / 3));
            for (int i = 0; i < 4; i++)
            {
                bool on = sPressed.ContainsValue(i);
                Rectangle r = sArrows[i];
                RoundBox(g, r, 28 * sc, on ? theme.Taxi : theme.Panel, on ? theme.Cone : theme.Borda, (on ? 3 : 2) * sc);
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = r.Width * 0.2f;
                Point d = ArrowDir[i];
                PointF tip = new PointF(cx + d.X * s, cy + d.Y * s);
                PointF b1 = new PointF(cx - d.X * s * 0.8f - d.Y * s, cy - d.Y * s * 0.8f - d.X * s);
                PointF b2 = new PointF(cx - d.X * s * 0.8f + d.Y * s, cy - d.Y * s * 0.8f + d.X * s);
                using (SolidBrush b = new SolidBrush(on ? theme.OnTaxi : theme.Ink)) g.FillPolygon(b, new PointF[] { tip, b1, b2 });
            }

            if (sstate == SState.Pausado || sstate == SState.Fim) DrawSnakeCard(g);
        }

        void DrawSnakeCard(Graphics g)
        {
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(204, 14, 16, 19))) g.FillRectangle(dim, new Rectangle(Point.Empty, view));
            RoundBox(g, sPanel, 28 * sc, theme.Panel, theme.Borda, 2 * sc);
            float y = sPanel.Y + R(32);
            if (sstate == SState.Pausado)
            {
                DrawText(g, "Pausado", titleFont, theme.Ink, new RectangleF(sPanel.X, y + R(20), sPanel.Width, R(52)), CenterFmt);
                RoundBox(g, sBtnA, 18 * sc, theme.Taxi, theme.Cone, 3 * sc);
                DrawText(g, "Continuar", buttonFont, theme.OnTaxi, sBtnA, CenterFmt);
            }
            else
            {
                if (snakeRecord)
                {
                    Size ts = TextRenderer.MeasureText("Novo recorde!", labelFont);
                    Rectangle badge = new Rectangle(sPanel.X + (sPanel.Width - ts.Width - R(32)) / 2, (int)y, ts.Width + R(32), R(34));
                    RoundBox(g, badge, badge.Height / 2f, theme.Taxi, theme.Taxi, 0);
                    DrawText(g, "Novo recorde!", labelFont, theme.OnTaxi, badge, CenterFmt);
                }
                y += R(44);
                DrawText(g, "Mordeu o rabo!", titleFont, theme.Ink, new RectangleF(sPanel.X, y, sPanel.Width, R(52)), CenterFmt);
                y += R(66);
                int bw = (sPanel.Width - R(80) - R(12)) / 2;
                string[] labels = { "PONTOS", "TAMANHO" };
                string[] vals = { snake.Score.ToString("0000"), snake.Body.Count.ToString() };
                for (int i = 0; i < 2; i++)
                {
                    Rectangle r = new Rectangle(sPanel.X + R(40) + i * (bw + R(12)), (int)y, bw, R(84));
                    RoundBox(g, r, 18 * sc, theme.Surface, theme.Surface, 0);
                    Label(g, labels[i], r.X + R(18), r.Y + R(14), r.Width - R(30));
                    DrawText(g, vals[i], gScore, i == 0 ? LcdBg : theme.Ink, new RectangleF(r.X + R(16), r.Y + R(34), r.Width - R(30), R(40)), LeftFmt);
                }
                RoundBox(g, sBtnA, 18 * sc, theme.Taxi, theme.Cone, 3 * sc);
                DrawText(g, "Jogar de novo", buttonFont, theme.OnTaxi, sBtnA, CenterFmt);
            }
            RoundBox(g, sBtnB, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, "Sair", buttonFont, theme.Ink, sBtnB, CenterFmt);
        }

        // Qualquer ponto da cruz vale: a direção é o lado mais próximo, a partir do centro.
        int ArrowAt(Point p)
        {
            int dx = p.X - sPadCenter.X, dy = p.Y - sPadCenter.Y;
            if (dx * dx + dy * dy > sPadRadius * sPadRadius) return -1;
            if (Math.Abs(dx) < R(14) && Math.Abs(dy) < R(14)) return -2; // bem no meio: não muda
            if (Math.Abs(dx) > Math.Abs(dy)) return dx < 0 ? 2 : 3;
            return dy < 0 ? 0 : 1;
        }

        void SnakeDown(Contact c, Point p)
        {
            if (sstate == SState.Pausado || sstate == SState.Fim)
            {
                if (sBtnA.Contains(p))
                {
                    if (sstate == SState.Pausado) { sstate = SState.Jogando; StartSnakeClock(); }
                    else EnterSnake();
                    LayoutSnake();
                    Invalidate();
                }
                else if (sBtnB.Contains(p)) GoTo(Screen.Jogos);
                return;
            }
            if (sBack.Contains(p)) { GoTo(Screen.Jogos); return; }
            if (sPause.Contains(p)) { PauseSnake(); return; }

            int a = ArrowAt(p);
            if (a == -1) return;
            // vira ao encostar; rolar o dedão pela cruz também vira
            sPressed[c.Id] = a;
            if (a >= 0)
            {
                SnakeTurn(a);
                Invalidate(Rectangle.Inflate(sArrows[a], R(8), R(8)));
            }
            c.Role = Role.Arrastar;
            int id = c.Id;
            c.Drag = delegate(Point q)
            {
                int now = ArrowAt(q);
                int before;
                sPressed.TryGetValue(id, out before);
                if (now >= 0 && now != before)
                {
                    sPressed[id] = now;
                    SnakeTurn(now);
                    if (before >= 0) Invalidate(Rectangle.Inflate(sArrows[before], R(8), R(8)));
                    Invalidate(Rectangle.Inflate(sArrows[now], R(8), R(8)));
                }
            };
        }

        void SnakeUp(int id)
        {
            int a;
            if (!sPressed.TryGetValue(id, out a)) return;
            sPressed.Remove(id);
            if (a >= 0) Invalidate(Rectangle.Inflate(sArrows[a], R(8), R(8)));
        }

        bool SnakeKey(Keys key)
        {
            if (screen != Screen.Cobra || snake == null) return false;
            int a = key == Keys.Up || key == Keys.W ? 0 : key == Keys.Down || key == Keys.S ? 1 : key == Keys.Left || key == Keys.A ? 2 : key == Keys.Right || key == Keys.D ? 3 : -1;
            if (a >= 0 && (sstate == SState.Pronto || sstate == SState.Jogando)) { SnakeTurn(a); return true; }
            if (key == Keys.P || key == Keys.Escape)
            {
                if (sstate == SState.Jogando) PauseSnake();
                else if (sstate == SState.Pausado) { sstate = SState.Jogando; StartSnakeClock(); Invalidate(); }
                return true;
            }
            return false;
        }
    }
}
