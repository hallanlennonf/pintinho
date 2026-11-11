using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pintinho
{
    partial class MainForm
    {
        // Quebra-cabeça de deslizar (tipo o de 15 números), mas as peças formam um desenho do app,
        // pintado sozinho com cores alegres. Fases: 2x2 → 3x3 → 4x4 → 5x5, cada vez mais embaralhado.
        // Toque numa peça da mesma linha/coluna do buraco e ela (e as do meio) deslizam.
        class SlideGame : Mini
        {
            public override string Key { get { return "deslize"; } }
            public override string Title { get { return "Quebra-cabeça"; } }
            public override Color Accent { get { return Shapes.Hex(0x5c6bc0); } }
            public override string Info { get { return board == null ? null : moves + " movimentos"; } }

            static readonly int[] Sizes = { 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5 };
            static readonly int[] Shuffles = { 8, 10, 20, 40, 100, 25, 50, 100, 250, 50, 120, 400 };

            int n, moves, seed;
            int[] board, from; // board[posição] = peça (n*n-1 = buraco); from[peça] = posição antes do último movimento
            float animT = 1;
            bool showNums;
            Drawings.Item item;
            Bitmap pic, artPic;
            Rectangle boardR, previewR, numsBtn;
            Font numFont;

            public override void Start(Random rnd)
            {
                n = Sizes[Level - 1];
                board = new int[n * n];
                from = new int[n * n];
                for (int i = 0; i < board.Length; i++) board[i] = from[i] = i;
                int empty = board.Length - 1, last = -1;
                for (int s = 0; s < Shuffles[Level - 1] || Solved(); s++)
                {
                    int er = empty / n, ec = empty % n;
                    int[] cand = { empty - n, empty + n, empty - 1, empty + 1 };
                    int pick;
                    do
                    {
                        pick = cand[rnd.Next(4)];
                    } while (pick < 0 || pick >= board.Length || pick == last ||
                             (pick == empty - 1 && ec == 0) || (pick == empty + 1 && ec == n - 1) || (pick / n != er && pick % n != ec));
                    board[empty] = board[pick];
                    board[pick] = board.Length - 1;
                    last = empty;
                    empty = pick;
                }
                for (int i = 0; i < board.Length; i++) from[board[i]] = i;
                animT = 1;
                moves = 0;
                showNums = false;
                Drawings.Item[] all = Drawings.Cozy;
                item = rnd.NextDouble() < 0.3 ? Drawings.All[rnd.Next(Drawings.All.Length)] : all[rnd.Next(all.Length)];
                seed = rnd.Next();
                if (pic != null) { pic.Dispose(); pic = null; }
            }

            bool Solved()
            {
                for (int i = 0; i < board.Length; i++) if (board[i] != i) return false;
                return true;
            }

            public override void Layout()
            {
                if (board == null) return;
                if (numFont != null) numFont.Dispose();
                numFont = M.F(Math.Max(14, 36 - n * 4), FontStyle.Bold);
                int side, panel = R(240), gap = R(36);
                if (!M.portrait)
                {
                    side = Math.Min(Area.Height - R(8), Area.Width - panel - gap);
                    int x0 = Area.X + (Area.Width - side - gap - panel) / 2;
                    boardR = new Rectangle(x0, Area.Y + (Area.Height - side) / 2, side, side);
                    previewR = new Rectangle(boardR.Right + gap, boardR.Y + R(40), panel, panel);
                    numsBtn = new Rectangle(previewR.X, previewR.Bottom + R(28), panel, R(64));
                }
                else
                {
                    side = Math.Min(Area.Width - R(16), Area.Height - R(220));
                    boardR = new Rectangle(Area.X + (Area.Width - side) / 2, Area.Y + R(8), side, side);
                    int ps = Math.Min(R(180), Area.Bottom - boardR.Bottom - R(56));
                    previewR = new Rectangle(boardR.X, boardR.Bottom + R(44), ps, ps);
                    numsBtn = new Rectangle(previewR.Right + R(28), previewR.Y + (ps - R(64)) / 2, R(240), R(64));
                }
                if (pic == null || pic.Width != side)
                {
                    if (pic != null) pic.Dispose();
                    pic = AutoColor(item, side, side, seed);
                }
            }

            RectangleF TileRect(int pos)
            {
                float ts = boardR.Width / (float)n;
                return new RectangleF(boardR.X + (pos % n) * ts, boardR.Y + (pos / n) * ts, ts, ts);
            }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (pic == null) return;
                float ts = boardR.Width / (float)n, gp = Math.Max(1.5f, 3 * Sc);
                RoundBox2(g, Rectangle.Inflate(boardR, R(10), R(10)), 22 * Sc, Shapes.Hex(0x3949ab));
                if (Won)
                {
                    g.DrawImage(pic, boardR);
                }
                else
                {
                    using (SolidBrush hole = new SolidBrush(Shapes.Hex(0x283593))) g.FillRectangle(hole, boardR);
                    float e = animT >= 1 ? 1 : 1 - (1 - animT) * (1 - animT);
                    for (int pos = 0; pos < board.Length; pos++)
                    {
                        int t = board[pos];
                        if (t == board.Length - 1) continue;
                        RectangleF to = TileRect(pos), fr = TileRect(from[t]);
                        RectangleF d = new RectangleF(fr.X + (to.X - fr.X) * e, fr.Y + (to.Y - fr.Y) * e, ts, ts);
                        RectangleF src = TileRect(t);
                        src.Offset(-boardR.X, -boardR.Y);
                        RectangleF dd = RectangleF.Inflate(d, -gp / 2, -gp / 2);
                        src = RectangleF.Inflate(src, -gp / 2, -gp / 2);
                        g.DrawImage(pic, dd, src, GraphicsUnit.Pixel);
                        using (Pen p = new Pen(Color.FromArgb(150, 255, 255, 255), Math.Max(1, 1.5f * Sc))) g.DrawRectangle(p, dd.X, dd.Y, dd.Width, dd.Height);
                        if (showNums)
                        {
                            float br = Math.Max(R(16), ts * 0.16f);
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 255, 255, 255))) g.FillEllipse(b, dd.X + R(6), dd.Y + R(6), br * 2, br * 2);
                            DrawText(g, (t + 1).ToString(), numFont, Shapes.Hex(0x283593), new RectangleF(dd.X + R(6), dd.Y + R(6), br * 2, br * 2), CenterFmt);
                        }
                    }
                }

                // modelo e botão de números
                RoundBox2(g, Rectangle.Inflate(previewR, R(6), R(6)), 16 * Sc, Color.White);
                g.DrawImage(pic, previewR);
                DrawText(g, "Modelo", M.labelFont, M.theme.Muted, new RectangleF(previewR.X, previewR.Y - R(34), previewR.Width, R(24)), M.portrait ? LeftFmt : CenterFmt);
                M.RoundBox(g, numsBtn, 16 * Sc, showNums ? Accent : Color.White, Accent, 2 * Sc);
                DrawText(g, showNums ? "Números: sim" : "Números: não", M.buttonFont, showNums ? Color.White : Accent, numsBtn, CenterFmt);
            }

            void RoundBox2(Graphics g, Rectangle r, float rad, Color c)
            {
                using (GraphicsPath p = Shapes.RoundRect(r, rad))
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
            }

            public override void Tick(float dt)
            {
                if (animT >= 1) return;
                animT = Math.Min(1, animT + dt / 0.12f);
                Repaint(Rectangle.Inflate(boardR, R(12), R(12)));
            }

            public override void Down(Contact c, Point p)
            {
                if (numsBtn.Contains(p)) { showNums = !showNums; RepaintAll(); return; }
                if (!boardR.Contains(p)) return;
                float ts = boardR.Width / (float)n;
                int col = Math.Min(n - 1, (int)((p.X - boardR.X) / ts)), row = Math.Min(n - 1, (int)((p.Y - boardR.Y) / ts));
                int pos = row * n + col, empty = Array.IndexOf(board, board.Length - 1);
                int er = empty / n, ec = empty % n;
                if (pos == empty || (row != er && col != ec)) return;
                for (int i = 0; i < board.Length; i++) from[board[i]] = i;
                // empurra as peças entre o toque e o buraco, uma casa na direção do buraco
                int step = row == er ? (col < ec ? -1 : 1) : (row < er ? -n : n);
                int cur = empty;
                while (cur != pos)
                {
                    board[cur] = board[cur + step];
                    cur += step;
                }
                board[pos] = board.Length - 1;
                moves++;
                animT = 0;
                if (Solved()) Won = true;
                Repaint(Rectangle.Inflate(boardR, R(12), R(12)));
                M.InvalidateMiniHeader();
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(img, 18 * Sc)) g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x3949ab))) g.FillRectangle(b, img);
                int side = (int)(Math.Min(img.Width, img.Height) * 0.86f);
                if (artPic == null || artPic.Width != side)
                {
                    if (artPic != null) artPic.Dispose();
                    artPic = AutoColor(Drawings.Cozy[0], side, side, 11);
                }
                float x0 = img.X + (img.Width - side) / 2f, y0 = img.Y + (img.Height - side) / 2f, ts = side / 3f;
                for (int i = 0; i < 9; i++)
                {
                    if (i == 5) continue;
                    float dx = i == 4 ? ts * 0.35f : 0;
                    RectangleF src = new RectangleF((i % 3) * ts + 1, (i / 3) * ts + 1, ts - 2, ts - 2);
                    g.DrawImage(artPic, new RectangleF(x0 + (i % 3) * ts + 1 + dx, y0 + (i / 3) * ts + 1, ts - 2, ts - 2), src, GraphicsUnit.Pixel);
                }
                g.Restore(st);
            }
        }
    }
}
