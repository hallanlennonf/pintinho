using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pintinho
{
    // Modo Infantil: veículos, botões grandes, folha 10:7. Vários dedos podem pintar ao mesmo tempo.
    partial class MainForm
    {
        static readonly float[] KidsSizes = { 6f, 14f, 28f };     // espessura do pincel (folha de 1000px)
        static readonly float[] KidsSizeDots = { 7f, 12f, 18f };  // bolinha do botão Tamanho
        const float KidsAspect = 10f / 7f;

        Surface kidsSurface;
        Gallery kidsGallery;
        GalleryItem kidsCurrent;
        bool kidsEntered;

        Tool kidsTool = Tool.Pincel;
        Color kidsColor = Theme.Palette[4];
        int kidsSizeIdx = 1, kidsStamp;

        Rectangle canvasArea, paperRect, kidsLock, kidsPlus;
        readonly Btn[] toolBtns = {
            new Btn("pincel", "Pincel"), new Btn("spray", "Spray"), new Btn("arco", "Arco-íris"),
            new Btn("balde", "Balde"), new Btn("carimbo", "Carimbo"), new Btn("borracha", "Borracha")
        };
        static readonly Tool[] KidsTools = { Tool.Pincel, Tool.Spray, Tool.ArcoIris, Tool.Balde, Tool.Carimbo, Tool.Borracha };
        readonly Btn[] actionBtns = {
            new Btn("desenhos", "Desenhos"), new Btn("desfazer", "Desfazer"), new Btn("limpar", "Limpar"), new Btn("salvar", "Salvar")
        };
        readonly Btn sizeBtn = new Btn("tamanho", "Tamanho");
        readonly Rectangle[] swatches = new Rectangle[Theme.Palette.Length];

        Rectangle kcPanel, kcClose, ksPanel, ksClose;
        readonly Rectangle[] kcCells = new Rectangle[Theme.KidsExtra.Length];
        readonly Rectangle[] ksCells = new Rectangle[Stamps.Count];

        void LayoutKids()
        {
            int W = view.Width, H = view.Height;
            int col = R(128), btn = R(104), gap = R(10), pad = R(12), sw = R(52);
            Rectangle palArea;
            int palCols;
            if (!portrait)
            {
                int bottom = R(100), areaH = H - bottom;
                Column(toolBtns, (col - btn) / 2, 0, areaH, btn, gap);
                Column(actionBtns, W - col + (col - btn) / 2, 0, areaH, btn, gap);
                canvasArea = new Rectangle(col, pad, W - 2 * col, areaH - 2 * pad);
                sizeBtn.R = Center(new Rectangle(0, areaH, col, bottom), btn, R(80));
                kidsLock = Center(new Rectangle(W - col, areaH, col, bottom), R(64), R(64));
                palArea = new Rectangle(col, areaH, W - 2 * col, bottom);
                palCols = 17;
            }
            else
            {
                int top = R(128), toolsH = R(128), palH = R(160);
                sizeBtn.R = Center(new Rectangle(0, 0, col, top), btn, R(80));
                kidsLock = Center(new Rectangle(W - col, 0, col, top), R(64), R(64));
                Row(actionBtns, 0, W, (top - btn) / 2, btn, gap);
                canvasArea = new Rectangle(pad, top, W - 2 * pad, H - top - toolsH - palH);
                Row(toolBtns, 0, W, H - palH - toolsH + (toolsH - btn) / 2, btn, gap);
                palArea = new Rectangle(R(8), H - palH, W - R(16), palH);
                palCols = 9;
            }

            // 16 cores + o botão "+" na mesma grade
            int total = swatches.Length + 1;
            int palRows = (total + palCols - 1) / palCols, rowGap = R(18);
            float cellW = palArea.Width / (float)palCols;
            int y0 = palArea.Y + (palArea.Height - (palRows * sw + (palRows - 1) * rowGap)) / 2;
            for (int i = 0; i < total; i++)
            {
                float cx = palArea.X + cellW * (i % palCols + 0.5f);
                Rectangle r = new Rectangle((int)(cx - sw / 2f), y0 + (i / palCols) * (sw + rowGap), sw, sw);
                if (i < swatches.Length) swatches[i] = r;
                else kidsPlus = r;
            }

            // Folha com proporção fixa 10:7, centralizada na área branca.
            int inset = R(10);
            int aw = Math.Max(20, canvasArea.Width - 2 * inset), ah = Math.Max(14, canvasArea.Height - 2 * inset);
            int pw = Math.Min(aw, (int)(ah * KidsAspect));
            int ph = (int)(pw / KidsAspect);
            paperRect = new Rectangle(canvasArea.X + (canvasArea.Width - pw) / 2, canvasArea.Y + (canvasArea.Height - ph) / 2, pw, ph);

            if (kidsSurface == null) kidsSurface = new Surface(pw, ph, 12);
            else if (kidsSurface.W != pw || kidsSurface.H != ph)
            {
                kidsSurface.Resize(pw, ph);
                kidsSurface.SetOverlay(Gallery.MakeOverlay(kidsCurrent, pw, ph));
            }

            LayoutKidsOverlays();
        }

        void LayoutKidsOverlays()
        {
            int W = view.Width, H = view.Height;
            int head = R(80), padX = R(32), padTop = R(24), padBottom = R(32), gapHead = R(24);

            int cols = portrait ? 6 : 8, rows = (kcCells.Length + cols - 1) / cols, cell = R(80), g = R(18);
            int pw = cols * cell + (cols - 1) * g + 2 * padX;
            int ph = padTop + head + gapHead + rows * cell + (rows - 1) * g + padBottom;
            kcPanel = new Rectangle((W - pw) / 2, (H - ph) / 2, pw, ph);
            kcClose = new Rectangle(kcPanel.Right - padX - head, kcPanel.Y + padTop, head, head);
            for (int i = 0; i < kcCells.Length; i++)
                kcCells[i] = new Rectangle(kcPanel.X + padX + (i % cols) * (cell + g), kcClose.Bottom + gapHead + (i / cols) * (cell + g), cell, cell);

            cols = portrait ? 4 : 6;
            rows = (ksCells.Length + cols - 1) / cols;
            int ch = R(120), sg = R(16);
            pw = portrait ? R(640) : R(880);
            int cw = (pw - 2 * padX - (cols - 1) * sg) / cols;
            ph = padTop + head + gapHead + rows * ch + (rows - 1) * sg + padBottom;
            ksPanel = new Rectangle((W - pw) / 2, (H - ph) / 2, pw, ph);
            ksClose = new Rectangle(ksPanel.Right - padX - head, ksPanel.Y + padTop, head, head);
            for (int i = 0; i < ksCells.Length; i++)
                ksCells[i] = new Rectangle(ksPanel.X + padX + (i % cols) * (cw + sg), ksClose.Bottom + gapHead + (i / cols) * (ch + sg), cw, ch);
        }

        void EnterKids()
        {
            if (!kidsEntered)
            {
                kidsEntered = true;
                OpenGallery();
            }
        }

        void LoadKidsItem(GalleryItem it)
        {
            kidsCurrent = it;
            kidsSurface.ClearUndo();
            kidsSurface.Clear();
            kidsSurface.SetOverlay(Gallery.MakeOverlay(it, kidsSurface.W, kidsSurface.H));
        }

        // ---------- desenho ----------

        void PaintKids(Graphics g, Rectangle clip)
        {
            if (clip.IntersectsWith(canvasArea)) DrawKidsCanvas(g, clip);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < toolBtns.Length; i++)
                if (clip.IntersectsWith(toolBtns[i].R)) DrawKidsButton(g, toolBtns[i], kidsTool == KidsTools[i]);
            foreach (Btn b in actionBtns)
                if (clip.IntersectsWith(b.R)) DrawKidsButton(g, b, false);
            if (clip.IntersectsWith(sizeBtn.R)) DrawSizeButton(g);
            for (int i = 0; i < swatches.Length; i++)
                if (clip.IntersectsWith(Rectangle.Inflate(swatches[i], R(10), R(10)))) DrawSwatch(g, swatches[i], Theme.Palette[i], kidsColor, 2 * sc, 4 * sc);
            if (clip.IntersectsWith(kidsPlus)) DrawPlusSwatch(g, kidsPlus);
            if (clip.IntersectsWith(Rectangle.Inflate(kidsLock, R(10), R(10)))) DrawLockButton(g, kidsLock, 18 * sc);
        }

        void DrawKidsCanvas(Graphics g, Rectangle clip)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RoundBox(g, canvasArea, 28 * sc, Theme.Papel, theme.Borda, 2 * sc);
            g.SmoothingMode = SmoothingMode.None;

            Rectangle cr = Rectangle.Intersect(clip, paperRect);
            if (cr.IsEmpty) return;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            Rectangle src = cr;
            src.Offset(-paperRect.X, -paperRect.Y);
            g.DrawImage(kidsSurface.Paint, cr, src, GraphicsUnit.Pixel);
            if (kidsSurface.Overlay != null) g.DrawImage(kidsSurface.Overlay, cr, src, GraphicsUnit.Pixel);
            g.CompositingQuality = CompositingQuality.Default;
            g.InterpolationMode = InterpolationMode.Default;
            g.PixelOffsetMode = PixelOffsetMode.Default;
        }

        void DrawKidsButton(Graphics g, Btn b, bool sel)
        {
            Color fg = sel ? theme.OnTaxi : theme.Ink;
            RoundBox(g, b.R, 18 * sc, sel ? theme.Taxi : theme.Panel, sel ? theme.Cone : theme.Borda, (sel ? 3 : 2) * sc);
            float icon = 56 * sc, lh = labelFont.Height;
            float top = b.R.Y + (b.R.Height - (icon + 6 * sc + lh)) / 2f;
            Icons.Draw(g, b.Id, new RectangleF(b.R.X + (b.R.Width - icon) / 2f, top, icon, icon), kidsColor, fg, theme, kidsStamp);
            DrawText(g, b.Label, labelFont, fg, new RectangleF(b.R.X, top + icon + 6 * sc, b.R.Width, lh), CenterFmt);
        }

        void DrawSizeButton(Graphics g)
        {
            Rectangle r = sizeBtn.R;
            RoundBox(g, r, 18 * sc, theme.Panel, theme.Borda, 2 * sc);
            float box = 44 * sc, lh = smallFont.Height;
            float top = r.Y + (r.Height - (box + 4 * sc + lh)) / 2f;
            float cx = r.X + r.Width / 2f, cy = top + box / 2f, rad = KidsSizeDots[kidsSizeIdx] * sc;
            using (SolidBrush b = new SolidBrush(kidsColor)) g.FillEllipse(b, cx - rad, cy - rad, 2 * rad, 2 * rad);
            using (Pen p = new Pen(theme.Ink, 1.5f * sc)) g.DrawEllipse(p, cx - rad, cy - rad, 2 * rad, 2 * rad);
            DrawText(g, sizeBtn.Label, smallFont, theme.Ink, new RectangleF(r.X, top + box + 4 * sc, r.Width, lh), CenterFmt);
        }

        void DrawSwatch(Graphics g, Rectangle s, Color c, Color selected, float bw, float ring)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, s);
            bool whiteOnLight = c.ToArgb() == Color.White.ToArgb() && !Config.DarkTheme;
            using (Pen p = new Pen(whiteOnLight ? theme.Borda : theme.SwatchEdge, bw))
                g.DrawEllipse(p, s.X + bw / 2, s.Y + bw / 2, s.Width - bw, s.Height - bw);
            if (c.ToArgb() == selected.ToArgb())
            {
                float off = ring + ring / 2;
                using (Pen p = new Pen(theme.Cone, ring))
                    g.DrawEllipse(p, s.X - off, s.Y - off, s.Width + 2 * off, s.Height + 2 * off);
            }
        }

        void DrawPlusSwatch(Graphics g, Rectangle s)
        {
            float bw = 2.5f * sc;
            using (SolidBrush b = new SolidBrush(theme.Panel)) g.FillEllipse(b, s);
            using (Pen p = new Pen(theme.Cone, bw))
            {
                p.DashPattern = new float[] { 2.2f, 1.6f };
                g.DrawEllipse(p, s.X + bw / 2, s.Y + bw / 2, s.Width - bw, s.Height - bw);
            }
            using (Pen p = Shapes.RoundPen(theme.Cone, 3.5f * sc))
            {
                float cx = s.X + s.Width / 2f, cy = s.Y + s.Height / 2f, h = s.Width * 0.22f;
                g.DrawLine(p, cx - h, cy, cx + h, cy);
                g.DrawLine(p, cx, cy - h, cx, cy + h);
            }
        }

        void DrawKidsPanel(Graphics g, Rectangle panel, Rectangle close, string title)
        {
            using (SolidBrush dim = new SolidBrush(theme.Dim)) g.FillRectangle(dim, new Rectangle(Point.Empty, view));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RoundBox(g, panel, 28 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, title, titleFont, theme.Ink, new RectangleF(panel.X + R(32), close.Y, close.X - panel.X - R(44), close.Height), LeftFmt);
            DrawCloseCircle(g, close);
        }

        void DrawKidsColors(Graphics g)
        {
            DrawKidsPanel(g, kcPanel, kcClose, "Mais cores!");
            for (int i = 0; i < kcCells.Length; i++) DrawSwatch(g, kcCells[i], Theme.KidsExtra[i], kidsColor, 2 * sc, 4 * sc);
        }

        void DrawKidsStamps(Graphics g)
        {
            DrawKidsPanel(g, ksPanel, ksClose, "Escolha um carimbo!");
            for (int i = 0; i < ksCells.Length; i++)
            {
                bool sel = i == kidsStamp;
                Rectangle r = ksCells[i];
                RoundBox(g, r, 18 * sc, sel ? theme.Taxi : theme.Panel, sel ? theme.Cone : theme.Borda, (sel ? 3 : 2) * sc);
                Stamps.Draw(g, i, r.X + r.Width / 2f, r.Y + r.Height / 2f, 36 * sc, kidsColor, sel ? theme.OnTaxi : theme.Ink, 2.2f * sc);
            }
        }

        // ---------- toque ----------

        void KidsDown(Contact c, Point p)
        {
            for (int i = 0; i < toolBtns.Length; i++)
            {
                if (!toolBtns[i].R.Contains(p)) continue;
                Tool t = KidsTools[i];
                if (t == Tool.Carimbo && kidsTool == Tool.Carimbo) overlay = Overlay.Carimbos;
                kidsTool = t;
                Invalidate();
                return;
            }
            foreach (Btn b in actionBtns)
            {
                if (!b.R.Contains(p)) continue;
                switch (b.Id)
                {
                    case "desenhos": OpenGallery(); break;
                    case "desfazer": DoUndo(); break;
                    case "limpar":
                        kidsSurface.SaveUndo();
                        kidsSurface.Clear();
                        Invalidate(paperRect);
                        break;
                    case "salvar": Save(kidsSurface, "infantil"); break;
                }
                return;
            }
            if (sizeBtn.R.Contains(p))
            {
                kidsSizeIdx = (kidsSizeIdx + 1) % KidsSizes.Length;
                Invalidate(sizeBtn.R);
                return;
            }
            for (int i = 0; i < swatches.Length; i++)
            {
                if (!Rectangle.Inflate(swatches[i], R(8), R(8)).Contains(p)) continue;
                PickKidsColor(Theme.Palette[i]);
                return;
            }
            if (Rectangle.Inflate(kidsPlus, R(8), R(8)).Contains(p))
            {
                overlay = Overlay.MaisCores;
                Invalidate();
                return;
            }
            if (canvasArea.Contains(p)) KidsBeginStroke(c, p);
        }

        void PickKidsColor(Color col)
        {
            kidsColor = col;
            if (kidsTool == Tool.Borracha) kidsTool = Tool.Pincel;
            Invalidate();
        }

        PointF KidsPaper(Point p) { return new PointF(p.X - paperRect.X, p.Y - paperRect.Y); }

        float KidsScale() { return paperRect.Width / 1000f; }

        void InvalidatePaper(Rectangle r)
        {
            if (r.IsEmpty) return;
            r.Offset(paperRect.X, paperRect.Y);
            r.Intersect(paperRect);
            if (!r.IsEmpty) Invalidate(r);
        }

        void KidsBeginStroke(Contact c, Point p)
        {
            PointF cp = KidsPaper(p);
            kidsSurface.SaveUndo();
            switch (kidsTool)
            {
                case Tool.Balde:
                    {
                        Rectangle r = kidsSurface.Fill((int)cp.X, (int)cp.Y, kidsColor);
                        if (r.IsEmpty) kidsSurface.DropLastUndo();
                        else InvalidatePaper(r);
                        break;
                    }
                case Tool.Carimbo:
                    InvalidatePaper(kidsSurface.Stamp(cp, kidsStamp, kidsColor, (18 + KidsSizes[kidsSizeIdx] * 1.7f) * KidsScale()));
                    break;
                default:
                    c.Role = Role.Pintar;
                    c.Tool = kidsTool;
                    c.Paper = cp;
                    c.Hue = (float)(rnd.NextDouble() * 360);
                    KidsStroke(c, p);
                    break;
            }
        }

        void KidsStroke(Contact c, Point p)
        {
            PointF cp = KidsPaper(p);
            float w = KidsSizes[kidsSizeIdx] * KidsScale();
            Rectangle r;
            switch (c.Tool)
            {
                case Tool.Borracha:
                    r = kidsSurface.Line(c.Paper, cp, Color.White, w * 1.8f);
                    break;
                case Tool.ArcoIris:
                    c.Hue = (c.Hue + 8) % 360;
                    r = kidsSurface.Line(c.Paper, cp, Shapes.FromHue(c.Hue), w * 1.3f);
                    break;
                case Tool.Spray:
                    r = kidsSurface.Spray(cp, kidsColor, w * 2.2f, rnd);
                    break;
                default:
                    r = kidsSurface.Line(c.Paper, cp, kidsColor, w);
                    break;
            }
            c.Paper = cp;
            InvalidatePaper(r);
        }

        void KidsColorsDown(Point p)
        {
            if (Rectangle.Inflate(kcClose, R(8), R(8)).Contains(p) || !kcPanel.Contains(p))
            {
                overlay = Overlay.Nenhum;
                Invalidate();
                return;
            }
            for (int i = 0; i < kcCells.Length; i++)
            {
                if (!Rectangle.Inflate(kcCells[i], R(8), R(8)).Contains(p)) continue;
                overlay = Overlay.Nenhum;
                PickKidsColor(Theme.KidsExtra[i]);
                return;
            }
        }

        void KidsStampsDown(Point p)
        {
            if (Rectangle.Inflate(ksClose, R(8), R(8)).Contains(p) || !ksPanel.Contains(p))
            {
                overlay = Overlay.Nenhum;
                Invalidate();
                return;
            }
            for (int i = 0; i < ksCells.Length; i++)
            {
                if (!ksCells[i].Contains(p)) continue;
                kidsStamp = i;
                kidsTool = Tool.Carimbo;
                overlay = Overlay.Nenhum;
                Invalidate();
                return;
            }
        }
    }
}
