using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Pintinho
{
    // Modo Aconchego (adultos): folha quadrada de 1200px com zoom de pinça, 8 ferramentas,
    // tamanho/opacidade, paleta pastel, misturador de cor e carimbos.
    partial class MainForm
    {
        const int CozyPaper = 1200;
        const float CozyPx = CozyPaper / 688f; // "1 px" do controle = tamanho na folha a 100%

        static readonly string[] CozyNames = {
            "Tinta", "Branco", "Cinza", "Areia", "Pêssego", "Rosa-bebê", "Damasco", "Baunilha", "Creme", "Pistache", "Menta", "Água",
            "Céu", "Pervinca", "Lavanda", "Lilás", "Coral", "Tangerina", "Mostarda", "Folha", "Jade", "Azul-jeans", "Uva", "Framboesa"
        };
        static readonly Tool[] CozyTools = { Tool.Pincel, Tool.Lapis, Tool.Marcador, Tool.Spray, Tool.Balde, Tool.Borracha, Tool.Carimbo, Tool.ContaGotas };
        static readonly ImageAttributes ClampEdges = MakeClamp();

        static ImageAttributes MakeClamp()
        {
            ImageAttributes ia = new ImageAttributes();
            ia.SetWrapMode(WrapMode.TileFlipXY); // sem bordas escuras ao desenhar pedaços reduzidos
            return ia;
        }

        Surface cozySurface;
        Gallery cozyGallery;
        GalleryItem cozyCurrent;
        bool cozyEntered;

        Tool cozyTool = Tool.Pincel, cozyPrevTool = Tool.Pincel;
        Color cozyColor = Theme.Cozy[4];
        int cozySize = 8, cozyOpacity = 100, cozyStamp = 1, cozyStampSize = 50, stampCat;
        readonly List<Color> recent = new List<Color> {
            Shapes.Hex(0xf4a7a3), Shapes.Hex(0xb5d8b1), Shapes.Hex(0xfbe7a1), Shapes.Hex(0xa7d3e8), Shapes.Hex(0xf9d5a7), Shapes.Hex(0xc9b8ea)
        };

        float viewScale = 0.5f, fitScale = 0.5f;
        PointF viewOrigin;
        Contact pinchA, pinchB;
        float pinchD0, pinchS0;
        PointF pinchM0, pinchO0;
        int hintUntil;

        Rectangle cozyTop, cozyToolsRect, cozySide, cozyCanvas, cozyLock;
        Rectangle homeBtnR, undoBtnR, redoBtnR, zoomBox, zoomOutR, zoomLabelR, zoomInR, fitBtnR, galleryBtnR, saveBtnR, cozyTitleR;
        readonly Btn[] cozyBtns = {
            new Btn("pincel", "Pincel"), new Btn("lapis", "Lápis"), new Btn("marcador", "Marcador"), new Btn("spray", "Spray"),
            new Btn("balde", "Balde"), new Btn("borracha", "Borracha"), new Btn("carimbo", "Carimbo"), new Btn("contagotas", "Conta-gotas")
        };
        Rectangle secCor, secTam, secOpa, secPal, secRec, colorBig, colorText, plusR, sizeTrack, opacityTrack;
        readonly Rectangle[] palR = new Rectangle[Theme.Cozy.Length];
        readonly Rectangle[] recentR = new Rectangle[6];
        Font cozyTitleFont, cozySubFont, zoomFont, plusFont;

        // misturador
        Rectangle mixPanel, mixClose, svR, hueR, prevR, hexR, myLabelR, cancelR, useR;
        readonly Rectangle[] myR = new Rectangle[Config.MaxMyColors];
        float mixH, mixS, mixV, svBmpHue = -1;
        Bitmap svBmp, hueBmp;

        // carimbos
        Rectangle stPanel, stClose, stSizeTrack, stSizeLabel;
        readonly Rectangle[] stChips = new Rectangle[4];
        readonly List<Rectangle> stCells = new List<Rectangle>();
        readonly List<int> stVisible = new List<int>();

        // ---------- layout ----------

        void LayoutCozy()
        {
            Font[] old = { cozyTitleFont, cozySubFont, zoomFont, plusFont };
            foreach (Font f in old) if (f != null) f.Dispose();
            cozyTitleFont = F(16, FontStyle.Bold);
            cozySubFont = F(12, FontStyle.Regular);
            zoomFont = F(22, FontStyle.Regular);
            plusFont = F(24, FontStyle.Bold);

            int W = view.Width, H = view.Height, topH = R(64), bs = R(64), bg = R(8);
            cozyTop = new Rectangle(0, 0, W, topH);
            if (!portrait)
            {
                int toolsW = R(88), sideW = R(260);
                cozyToolsRect = new Rectangle(0, topH, toolsW, H - topH);
                cozySide = new Rectangle(W - sideW, topH, sideW, H - topH);
                cozyCanvas = new Rectangle(toolsW, topH, W - toolsW - sideW, H - topH);
                for (int i = 0; i < cozyBtns.Length; i++)
                    cozyBtns[i].R = new Rectangle((toolsW - bs) / 2, topH + R(12) + i * (bs + bg), bs, bs);
                LayoutSidePanel(cozySide, false);
            }
            else
            {
                int toolsH = R(88), bottomH = R(250);
                cozySide = new Rectangle(0, H - bottomH, W, bottomH);
                cozyToolsRect = new Rectangle(0, H - bottomH - toolsH, W, toolsH);
                cozyCanvas = new Rectangle(0, topH, W, H - topH - toolsH - bottomH);
                Row(cozyBtns, 0, W, cozyToolsRect.Y + (toolsH - bs) / 2, bs, bg);
                LayoutSidePanel(cozySide, true);
            }

            int bh = R(44), by = (topH - bh) / 2;
            homeBtnR = new Rectangle(R(16), by, bh, bh);
            cozyLock = new Rectangle(W - R(16) - bh, by, bh, bh);
            saveBtnR = new Rectangle(cozyLock.X - R(8) - R(112), by, R(112), bh);
            galleryBtnR = new Rectangle(saveBtnR.X - R(8) - R(136), by, R(136), bh);
            int groupW = bh + R(8) + bh + R(16) + R(214);
            int gx = (W - groupW) / 2;
            bool showTitle = true;
            if (gx + groupW > galleryBtnR.X - R(12))
            {
                gx = homeBtnR.Right + R(16);
                showTitle = false;
            }
            undoBtnR = new Rectangle(gx, by, bh, bh);
            redoBtnR = new Rectangle(gx + bh + R(8), by, bh, bh);
            zoomBox = new Rectangle(redoBtnR.Right + R(16), by, R(214), bh);
            zoomOutR = new Rectangle(zoomBox.X, by, R(40), bh);
            zoomLabelR = new Rectangle(zoomOutR.Right, by, R(58), bh);
            zoomInR = new Rectangle(zoomLabelR.Right, by, R(40), bh);
            fitBtnR = new Rectangle(zoomInR.Right, by, zoomBox.Right - zoomInR.Right, bh);
            cozyTitleR = showTitle ? new Rectangle(homeBtnR.Right + R(12), 0, undoBtnR.X - homeBtnR.Right - R(24), topH) : Rectangle.Empty;

            LayoutMixer();
            LayoutCozyStamps();
            FitCozyView();
        }

        void LayoutSidePanel(Rectangle area, bool twoCol)
        {
            int pad = R(18), lab = R(16), labGap = R(26), secGap = R(18), sw = R(30), g = R(8);
            int x = area.X + pad, y = area.Y + pad;
            int w = twoCol ? area.Width / 2 - pad - pad / 2 : area.Width - 2 * pad;

            secCor = new Rectangle(x, y, w, lab); y += labGap;
            colorBig = new Rectangle(x, y, R(56), R(56));
            plusR = new Rectangle(x + w - R(44), y + R(6), R(44), R(44));
            colorText = new Rectangle(colorBig.Right + R(12), y, plusR.X - colorBig.Right - R(20), R(56));
            y += R(56) + secGap;
            secTam = new Rectangle(x, y, w, lab); y += labGap;
            sizeTrack = new Rectangle(x, y, w, R(22)); y += R(22) + secGap;
            secOpa = new Rectangle(x, y, w, lab); y += labGap;
            opacityTrack = new Rectangle(x, y, w, R(22)); y += R(22) + secGap;

            int cols = 6;
            if (twoCol)
            {
                x = area.X + area.Width / 2 + pad / 2;
                y = area.Y + pad;
                cols = 8;
            }
            secPal = new Rectangle(x, y, w, lab); y += labGap;
            float cellW = (w - (cols - 1) * g) / (float)cols;
            for (int i = 0; i < palR.Length; i++)
                palR[i] = new Rectangle((int)(x + (i % cols) * (cellW + g) + (cellW - sw) / 2), y + (i / cols) * (sw + g), sw, sw);
            y += ((palR.Length + cols - 1) / cols) * (sw + g) - g + secGap;
            secRec = new Rectangle(x, y, w, lab); y += labGap;
            for (int i = 0; i < recentR.Length; i++) recentR[i] = new Rectangle(x + i * (sw + g), y, sw, sw);
        }

        void LayoutMixer()
        {
            int w = R(420), h = R(548), pad = R(20);
            int x = portrait ? (view.Width - w) / 2 : cozySide.X - R(16) - w;
            int y = portrait ? Math.Max(cozyTop.Bottom + R(8), cozySide.Y - R(16) - h) : cozyTop.Bottom + R(16);
            mixPanel = new Rectangle(x, y, w, h);
            mixClose = new Rectangle(mixPanel.Right - pad - R(36), y + pad, R(36), R(36));
            int iy = mixClose.Bottom + R(16), iw = w - 2 * pad, ix = x + pad;
            svR = new Rectangle(ix, iy, iw, R(210)); iy = svR.Bottom + R(16);
            hueR = new Rectangle(ix, iy, iw, R(18)); iy = hueR.Bottom + R(16);
            prevR = new Rectangle(ix, iy, R(96), R(44));
            hexR = new Rectangle(prevR.Right + R(12), iy, iw - R(96) - R(12), R(44)); iy = prevR.Bottom + R(16);
            myLabelR = new Rectangle(ix, iy, iw, R(16)); iy += R(24);
            for (int i = 0; i < myR.Length; i++) myR[i] = new Rectangle(ix + i * (R(36) + R(8)), iy, R(36), R(36));
            iy += R(36) + R(16);
            useR = new Rectangle(mixPanel.Right - pad - R(150), iy, R(150), R(44));
            cancelR = new Rectangle(useR.X - R(10) - R(110), iy, R(110), R(44));
        }

        void LayoutCozyStamps()
        {
            int w = R(452), pad = R(20), cellH = R(88), g = R(10);
            int h = pad + R(36) + R(14) + R(32) + R(14) + 3 * cellH + 2 * g + R(14) + R(22) + pad;
            int x = portrait ? (view.Width - w) / 2 : cozyToolsRect.Right + R(12);
            int y = portrait ? Math.Max(cozyTop.Bottom + R(8), cozyToolsRect.Y - R(12) - h) : cozyTop.Bottom + R(184);
            stPanel = new Rectangle(x, y, w, h);
            stClose = new Rectangle(stPanel.Right - pad - R(36), y + pad, R(36), R(36));
            int cy = stClose.Bottom + R(14), cx = x + pad;
            for (int i = 0; i < stChips.Length; i++)
            {
                int tw = System.Windows.Forms.TextRenderer.MeasureText(Stamps.CategoryNames[i], sectionFont).Width + R(28);
                stChips[i] = new Rectangle(cx, cy, tw, R(32));
                cx += tw + R(8);
            }
            cy += R(32) + R(14);
            stVisible.Clear();
            for (int i = 0; i < Stamps.Count; i++)
                if (stampCat == 0 || Stamps.Category[i] == stampCat) stVisible.Add(i);
            stCells.Clear();
            int cw = (w - 2 * pad - 3 * g) / 4;
            for (int k = 0; k < stVisible.Count; k++)
                stCells.Add(new Rectangle(x + pad + (k % 4) * (cw + g), cy + (k / 4) * (cellH + g), cw, cellH));
            cy += 3 * cellH + 2 * g + R(14);
            stSizeLabel = new Rectangle(x + pad, cy, R(90), R(22));
            stSizeTrack = new Rectangle(stSizeLabel.Right + R(12), cy, stPanel.Right - pad - stSizeLabel.Right - R(12), R(22));
        }

        // ---------- vista da folha (zoom) ----------

        void FitCozyView()
        {
            fitScale = Math.Max(0.05f, Math.Min(cozyCanvas.Width - R(48), cozyCanvas.Height - R(48)) / (float)CozyPaper);
            viewScale = fitScale;
            viewOrigin = new PointF(cozyCanvas.X + (cozyCanvas.Width - CozyPaper * viewScale) / 2f, cozyCanvas.Y + (cozyCanvas.Height - CozyPaper * viewScale) / 2f);
        }

        void ClampView()
        {
            float pw = CozyPaper * viewScale;
            float minX = cozyCanvas.X + cozyCanvas.Width * 0.3f - pw, maxX = cozyCanvas.Right - cozyCanvas.Width * 0.3f;
            float minY = cozyCanvas.Y + cozyCanvas.Height * 0.3f - pw, maxY = cozyCanvas.Bottom - cozyCanvas.Height * 0.3f;
            viewOrigin = new PointF(Math.Max(minX, Math.Min(maxX, viewOrigin.X)), Math.Max(minY, Math.Min(maxY, viewOrigin.Y)));
        }

        void ZoomBy(float f)
        {
            PointF m = new PointF(cozyCanvas.X + cozyCanvas.Width / 2f, cozyCanvas.Y + cozyCanvas.Height / 2f);
            PointF p = new PointF((m.X - viewOrigin.X) / viewScale, (m.Y - viewOrigin.Y) / viewScale);
            viewScale = Math.Max(fitScale * 0.5f, Math.Min(fitScale * 8f, viewScale * f));
            viewOrigin = new PointF(m.X - p.X * viewScale, m.Y - p.Y * viewScale);
            ClampView();
            InvalidateCozyCanvas();
        }

        PointF CozyPaperPt(Point p)
        {
            return new PointF((p.X - viewOrigin.X) / viewScale, (p.Y - viewOrigin.Y) / viewScale);
        }

        RectangleF PaperScreen()
        {
            return new RectangleF(viewOrigin.X, viewOrigin.Y, CozyPaper * viewScale, CozyPaper * viewScale);
        }

        void InvalidateCozyPaper(Rectangle r)
        {
            if (r.IsEmpty) return;
            Rectangle s = Rectangle.FromLTRB((int)Math.Floor(viewOrigin.X + r.Left * viewScale) - 2, (int)Math.Floor(viewOrigin.Y + r.Top * viewScale) - 2,
                (int)Math.Ceiling(viewOrigin.X + r.Right * viewScale) + 2, (int)Math.Ceiling(viewOrigin.Y + r.Bottom * viewScale) + 2);
            s.Intersect(cozyCanvas);
            if (!s.IsEmpty) Invalidate(s);
        }

        void InvalidateCozyCanvas()
        {
            Invalidate(cozyCanvas);
            Invalidate(zoomBox);
            Invalidate(undoBtnR);
            Invalidate(redoBtnR);
        }

        // ---------- entrar / carregar ----------

        void EnterCozy()
        {
            if (cozySurface == null)
            {
                cozySurface = new Surface(CozyPaper, CozyPaper, 8);
                FitCozyView();
            }
            if (!cozyEntered)
            {
                cozyEntered = true;
                hintUntil = Environment.TickCount + 8000;
                OpenGallery();
            }
        }

        void LoadCozyItem(GalleryItem it)
        {
            cozyCurrent = it;
            cozySurface.ClearUndo();
            cozySurface.Clear();
            cozySurface.SetOverlay(Gallery.MakeOverlay(it, CozyPaper, CozyPaper));
            FitCozyView();
        }

        void SetCozyColor(Color c)
        {
            cozyColor = Color.FromArgb(255, c.R, c.G, c.B);
            AddRecent(cozyColor);
            if (cozyTool == Tool.Borracha) cozyTool = Tool.Pincel;
            Invalidate();
        }

        void AddRecent(Color c)
        {
            for (int i = recent.Count - 1; i >= 0; i--)
                if (recent[i].ToArgb() == c.ToArgb()) recent.RemoveAt(i);
            recent.Insert(0, c);
            while (recent.Count > recentR.Length) recent.RemoveAt(recent.Count - 1);
        }

        Color CozyInk(float factor)
        {
            return Shapes.WithAlpha(cozyColor, (int)(cozyOpacity * 2.55f * factor));
        }

        static string ColorName(Color c)
        {
            for (int i = 0; i < Theme.Cozy.Length; i++)
                if (Theme.Cozy[i].ToArgb() == c.ToArgb()) return CozyNames[i];
            return "Minha cor";
        }

        // ---------- desenho ----------

        void PaintCozy(Graphics g, Rectangle clip)
        {
            if (clip.IntersectsWith(cozyCanvas)) DrawCozyCanvas(g, clip);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (clip.IntersectsWith(cozyTop)) DrawCozyTop(g);
            if (clip.IntersectsWith(cozyToolsRect)) DrawCozyTools(g);
            if (clip.IntersectsWith(cozySide)) DrawCozySide(g);
            if (hintUntil != 0) DrawHint(g);
        }

        void DrawCozyCanvas(Graphics g, Rectangle clip)
        {
            if (cozySurface == null) return;
            GraphicsState st = g.Save();
            g.SetClip(cozyCanvas, CombineMode.Intersect);
            RectangleF pr = PaperScreen();
            using (Pen p = new Pen(theme.Borda, Math.Max(1f, 1.5f * sc))) g.DrawRectangle(p, pr.X - 1, pr.Y - 1, pr.Width + 1, pr.Height + 1);

            Rectangle vis = Rectangle.Intersect(Rectangle.Intersect(clip, cozyCanvas), Rectangle.Round(pr));
            if (!vis.IsEmpty)
            {
                float s = viewScale;
                float sx = (vis.X - viewOrigin.X) / s, sy = (vis.Y - viewOrigin.Y) / s, sw = vis.Width / s, sh = vis.Height / s;
                // durante a pinça: rápido; parado: suave
                g.InterpolationMode = pinchA != null ? InterpolationMode.NearestNeighbor : InterpolationMode.Bilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.DrawImage(cozySurface.Paint, vis, sx, sy, sw, sh, GraphicsUnit.Pixel, ClampEdges);
                if (cozySurface.Overlay != null) g.DrawImage(cozySurface.Overlay, vis, sx, sy, sw, sh, GraphicsUnit.Pixel, ClampEdges);
            }
            g.Restore(st);
        }

        void DrawHint(Graphics g)
        {
            string text = "Dica: faça pinça com dois dedos para dar zoom";
            Size ts = System.Windows.Forms.TextRenderer.MeasureText(text, sectionFont);
            Rectangle r = new Rectangle(cozyCanvas.X + (cozyCanvas.Width - ts.Width - R(28)) / 2, cozyCanvas.Y + R(14), ts.Width + R(28), R(36));
            RoundBox(g, r, r.Height / 2f, theme.Panel, theme.Borda, 1);
            DrawText(g, text, sectionFont, theme.Muted, r, CenterFmt);
        }

        void SmallButton(Graphics g, Rectangle r, Color fill, Color border)
        {
            RoundBox(g, r, 12 * sc, fill, border, Math.Max(1f, 1.5f * sc));
        }

        void DrawCozyTop(Graphics g)
        {
            using (SolidBrush b = new SolidBrush(theme.Panel)) g.FillRectangle(b, cozyTop);
            using (Pen p = new Pen(theme.Borda, 1)) g.DrawLine(p, 0, cozyTop.Bottom - 1, cozyTop.Right, cozyTop.Bottom - 1);

            float ic = 24 * sc;
            SmallButton(g, homeBtnR, theme.Surface, theme.Borda);
            Icons.Draw(g, "casa", IconIn(homeBtnR, ic), theme.Ink, theme.Ink, theme, 0);
            if (!cozyTitleR.IsEmpty && cozyTitleR.Width > R(60))
            {
                DrawText(g, cozyCurrent.Name, cozyTitleFont, theme.Ink, new RectangleF(cozyTitleR.X, cozyTitleR.Y + R(10), cozyTitleR.Width, R(24)), LeftFmt);
                DrawText(g, "Aconchego", cozySubFont, theme.Muted, new RectangleF(cozyTitleR.X, cozyTitleR.Y + R(33), cozyTitleR.Width, R(18)), LeftFmt);
            }
            SmallButton(g, undoBtnR, theme.Panel, theme.Borda);
            Icons.Draw(g, "desfazer", IconIn(undoBtnR, ic), theme.Ink, theme.Ink, theme, 0);
            SmallButton(g, redoBtnR, theme.Panel, theme.Borda);
            Icons.Draw(g, "refazer", IconIn(redoBtnR, ic), theme.Ink, theme.Ink, theme, 0);

            SmallButton(g, zoomBox, theme.Panel, theme.Borda);
            DrawText(g, "−", zoomFont, theme.Ink, zoomOutR, CenterFmt);
            DrawText(g, (int)Math.Round(viewScale / fitScale * 100) + "%", labelFont, theme.Ink, zoomLabelR, CenterFmt);
            DrawText(g, "+", zoomFont, theme.Ink, zoomInR, CenterFmt);
            using (Pen p = new Pen(theme.Borda, Math.Max(1f, 1.5f * sc))) g.DrawLine(p, fitBtnR.X, fitBtnR.Y + R(6), fitBtnR.X, fitBtnR.Bottom - R(6));
            DrawText(g, "Ajustar", sectionFont, theme.Ink, fitBtnR, CenterFmt);

            SmallButton(g, galleryBtnR, theme.Panel, theme.Borda);
            Icons.Draw(g, "desenhos-pastel", new RectangleF(galleryBtnR.X + R(14), galleryBtnR.Y + (galleryBtnR.Height - R(22)) / 2f, R(22), R(22)), theme.Ink, theme.Ink, theme, 0);
            DrawText(g, "Desenhos", labelFont, theme.Ink, new RectangleF(galleryBtnR.X + R(42), galleryBtnR.Y, galleryBtnR.Width - R(48), galleryBtnR.Height), LeftFmt);
            RoundBox(g, saveBtnR, 12 * sc, theme.Trator, theme.Trator, 0);
            Icons.Draw(g, "salvar-branco", new RectangleF(saveBtnR.X + R(16), saveBtnR.Y + (saveBtnR.Height - R(20)) / 2f, R(20), R(20)), theme.OnTrator, theme.OnTrator, theme, 0);
            DrawText(g, "Salvar", labelFont, theme.OnTrator, new RectangleF(saveBtnR.X + R(42), saveBtnR.Y, saveBtnR.Width - R(48), saveBtnR.Height), LeftFmt);
            DrawLockButton(g, cozyLock, 12 * sc);
        }

        static RectangleF IconIn(Rectangle r, float size)
        {
            return new RectangleF(r.X + (r.Width - size) / 2f, r.Y + (r.Height - size) / 2f, size, size);
        }

        void DrawCozyTools(Graphics g)
        {
            using (SolidBrush b = new SolidBrush(theme.Panel)) g.FillRectangle(b, cozyToolsRect);
            using (Pen p = new Pen(theme.Borda, 1))
            {
                if (!portrait) g.DrawLine(p, cozyToolsRect.Right - 1, cozyToolsRect.Y, cozyToolsRect.Right - 1, cozyToolsRect.Bottom);
                else g.DrawLine(p, 0, cozyToolsRect.Y, cozyToolsRect.Right, cozyToolsRect.Y);
            }
            for (int i = 0; i < cozyBtns.Length; i++)
            {
                Btn b = cozyBtns[i];
                bool sel = cozyTool == CozyTools[i];
                Color fg = sel ? theme.OnTaxi : theme.Ink;
                RoundBox(g, b.R, 14 * sc, sel ? theme.Taxi : theme.Panel, sel ? theme.Cone : theme.Borda, (sel ? 2 : 1.5f) * sc);
                float icon = 32 * sc, lh = compactFont.Height;
                float top = b.R.Y + (b.R.Height - (icon + 2 * sc + lh)) / 2f;
                Icons.Draw(g, b.Id, new RectangleF(b.R.X + (b.R.Width - icon) / 2f, top, icon, icon), cozyColor, fg, theme, cozyStamp);
                DrawText(g, b.Label, compactFont, fg, new RectangleF(b.R.X - R(4), top + icon + 2 * sc, b.R.Width + R(8), lh), CenterFmt);
            }
        }

        void Section(Graphics g, Rectangle r, string title, string value)
        {
            DrawText(g, title, sectionFont, theme.Muted, r, LeftFmt);
            if (value != null) DrawText(g, value, sectionFont, theme.Ink, r, RightFmt);
        }

        void DrawSlider(Graphics g, Rectangle track, float frac)
        {
            frac = Math.Max(0, Math.Min(1, frac));
            float bh = 6 * sc, cy = track.Y + track.Height / 2f, knob = 22 * sc;
            float kx = track.X + knob / 2 + (track.Width - knob) * frac;
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(track.X, cy - bh / 2, track.Width, bh), bh / 2))
            using (SolidBrush b = new SolidBrush(theme.Borda)) g.FillPath(b, p);
            using (GraphicsPath p = Shapes.RoundRect(new RectangleF(track.X, cy - bh / 2, kx - track.X, bh), bh / 2))
            using (SolidBrush b = new SolidBrush(theme.Cone)) g.FillPath(b, p);
            using (SolidBrush b = new SolidBrush(theme.Panel)) g.FillEllipse(b, kx - knob / 2, cy - knob / 2, knob, knob);
            using (Pen p = new Pen(theme.Cone, 2.5f * sc)) g.DrawEllipse(p, kx - knob / 2 + 1.25f * sc, cy - knob / 2 + 1.25f * sc, knob - 2.5f * sc, knob - 2.5f * sc);
        }

        static float SliderFrac(Rectangle track, Point p)
        {
            return Math.Max(0, Math.Min(1, (p.X - track.X) / (float)Math.Max(1, track.Width)));
        }

        void DrawCozySide(Graphics g)
        {
            using (SolidBrush b = new SolidBrush(theme.Panel)) g.FillRectangle(b, cozySide);
            using (Pen p = new Pen(theme.Borda, 1))
            {
                if (!portrait) g.DrawLine(p, cozySide.X, cozySide.Y, cozySide.X, cozySide.Bottom);
                else g.DrawLine(p, 0, cozySide.Y, cozySide.Right, cozySide.Y);
            }

            Section(g, secCor, "COR", null);
            RoundBox(g, colorBig, 14 * sc, cozyColor, theme.Borda, 1.5f * sc);
            DrawText(g, ColorName(cozyColor), cozyTitleFont, theme.Ink, new RectangleF(colorText.X, colorText.Y + R(8), colorText.Width, R(22)), LeftFmt);
            DrawText(g, Shapes.ToHex(cozyColor), sectionFont, theme.Muted, new RectangleF(colorText.X, colorText.Y + R(30), colorText.Width, R(18)), LeftFmt);
            DashedBox(g, plusR, 12 * sc, theme.Surface, theme.Cone, 1.5f * sc);
            DrawText(g, "+", plusFont, theme.Cone, plusR, CenterFmt);

            Section(g, secTam, "TAMANHO", cozySize + " px");
            DrawSlider(g, sizeTrack, (cozySize - 1) / 39f);
            Section(g, secOpa, "OPACIDADE", cozyOpacity + "%");
            DrawSlider(g, opacityTrack, (cozyOpacity - 10) / 90f);

            Section(g, secPal, "PALETA PASTEL", null);
            for (int i = 0; i < palR.Length; i++) DrawSwatch(g, palR[i], Theme.Cozy[i], cozyColor, 1.5f * sc, 3 * sc);
            Section(g, secRec, "RECENTES", null);
            for (int i = 0; i < recentR.Length && i < recent.Count; i++) DrawSwatch(g, recentR[i], recent[i], Color.Empty, 1.5f * sc, 0);
        }

        // ---------- misturador de cor ----------

        void OpenMixer()
        {
            Shapes.ToHsv(cozyColor, out mixH, out mixS, out mixV);
            overlay = Overlay.Misturador;
            Invalidate();
        }

        void EnsureMixerBitmaps()
        {
            if (svBmp == null || svBmp.Width != svR.Width || svBmp.Height != svR.Height || svBmpHue != mixH)
            {
                if (svBmp != null) svBmp.Dispose();
                svBmp = Gradient(svR.Width, svR.Height, false);
                svBmpHue = mixH;
            }
            if (hueBmp == null || hueBmp.Width != hueR.Width || hueBmp.Height != hueR.Height)
            {
                if (hueBmp != null) hueBmp.Dispose();
                hueBmp = Gradient(hueR.Width, hueR.Height, true);
            }
        }

        Bitmap Gradient(int w, int h, bool hue)
        {
            w = Math.Max(1, w);
            h = Math.Max(1, h);
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int[] px = new int[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = hue
                        ? Shapes.FromHsv(360f * x / Math.Max(1, w - 1), 1, 1).ToArgb()
                        : Shapes.FromHsv(mixH, x / (float)Math.Max(1, w - 1), 1 - y / (float)Math.Max(1, h - 1)).ToArgb();
            Marshal.Copy(px, 0, d.Scan0, px.Length);
            b.UnlockBits(d);
            return b;
        }

        void DrawPopover(Graphics g, Rectangle panel, Rectangle close, string title)
        {
            RoundBox(g, panel, 20 * sc, theme.Panel, theme.Borda, 1.5f * sc);
            DrawText(g, title, cozyTitleFont, theme.Ink, new RectangleF(panel.X + R(20), close.Y, close.X - panel.X - R(28), close.Height), LeftFmt);
            SmallButton(g, close, theme.Surface, theme.Borda);
            using (Pen p = Shapes.RoundPen(theme.Ink, 2 * sc))
            {
                float cx = close.X + close.Width / 2f, cy = close.Y + close.Height / 2f, h = 6 * sc;
                g.DrawLine(p, cx - h, cy - h, cx + h, cy + h);
                g.DrawLine(p, cx + h, cy - h, cx - h, cy + h);
            }
        }

        void DrawRoundedImage(Graphics g, Bitmap b, Rectangle r, float radius)
        {
            GraphicsState st = g.Save();
            using (GraphicsPath p = Shapes.RoundRect(r, radius))
            {
                g.SetClip(p, CombineMode.Intersect);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(b, r);
            }
            g.Restore(st);
        }

        void DrawMixer(Graphics g)
        {
            EnsureMixerBitmaps();
            DrawPopover(g, mixPanel, mixClose, "Misturar cor");
            DrawRoundedImage(g, svBmp, svR, 12 * sc);
            DrawRoundedImage(g, hueBmp, hueR, hueR.Height / 2f);

            float kx = svR.X + mixS * svR.Width, ky = svR.Y + (1 - mixV) * svR.Height, k = 11 * sc;
            using (Pen p = new Pen(Color.White, 3 * sc)) g.DrawEllipse(p, kx - k, ky - k, 2 * k, 2 * k);
            using (Pen p = new Pen(Shapes.Hex(0x3a3330), 1)) g.DrawEllipse(p, kx - k - 1.5f * sc, ky - k - 1.5f * sc, 2 * k + 3 * sc, 2 * k + 3 * sc);
            float hx = hueR.X + mixH / 360f * hueR.Width;
            Rectangle hk = new Rectangle((int)(hx - 11 * sc), hueR.Y - R(3), R(22), hueR.Height + R(6));
            RoundBox(g, hk, 8 * sc, Color.White, Shapes.Hex(0x3a3330), 2 * sc);

            Color nc = Shapes.FromHsv(mixH, mixS, mixV);
            GraphicsState st = g.Save();
            using (GraphicsPath p = Shapes.RoundRect(prevR, 12 * sc))
            {
                g.SetClip(p, CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(cozyColor)) g.FillRectangle(b, prevR.X, prevR.Y, prevR.Width / 2, prevR.Height);
                using (SolidBrush b = new SolidBrush(nc)) g.FillRectangle(b, prevR.X + prevR.Width / 2, prevR.Y, prevR.Width - prevR.Width / 2, prevR.Height);
            }
            g.Restore(st);
            RoundBox(g, prevR, 12 * sc, Color.Transparent, theme.Borda, 1.5f * sc);
            RoundBox(g, hexR, 10 * sc, theme.Surface, theme.Borda, 1.5f * sc);
            DrawText(g, "Código", sectionFont, theme.Muted, new RectangleF(hexR.X + R(12), hexR.Y, R(70), hexR.Height), LeftFmt);
            DrawText(g, Shapes.ToHex(nc), labelFont, theme.Ink, new RectangleF(hexR.X + R(80), hexR.Y, hexR.Width - R(90), hexR.Height), LeftFmt);

            DrawText(g, "MINHAS CORES", sectionFont, theme.Muted, myLabelR, LeftFmt);
            for (int i = 0; i < myR.Length; i++)
            {
                if (i < Config.MyColors.Count)
                    using (SolidBrush b = new SolidBrush(Config.MyColors[i])) g.FillEllipse(b, myR[i]);
                else
                    using (Pen p = new Pen(theme.Borda, 1.5f * sc)) { p.DashPattern = new float[] { 2.5f, 2f }; g.DrawEllipse(p, myR[i]); }
            }

            SmallButton(g, cancelR, theme.Panel, theme.Borda);
            DrawText(g, "Cancelar", labelFont, theme.Ink, cancelR, CenterFmt);
            RoundBox(g, useR, 12 * sc, theme.Trator, theme.Trator, 0);
            DrawText(g, "Usar e guardar", labelFont, theme.OnTrator, useR, CenterFmt);
        }

        bool MixerDown(Contact c, Point p)
        {
            if (!mixPanel.Contains(p) || Rectangle.Inflate(mixClose, R(6), R(6)).Contains(p) || cancelR.Contains(p))
            {
                overlay = Overlay.Nenhum;
                Invalidate();
                return true;
            }
            if (Rectangle.Inflate(svR, R(6), R(6)).Contains(p))
            {
                c.Role = Role.Arrastar;
                c.Drag = delegate(Point q)
                {
                    mixS = Math.Max(0, Math.Min(1, (q.X - svR.X) / (float)svR.Width));
                    mixV = 1 - Math.Max(0, Math.Min(1, (q.Y - svR.Y) / (float)svR.Height));
                    Invalidate(mixPanel);
                };
                c.Drag(p);
                return true;
            }
            if (Rectangle.Inflate(hueR, R(4), R(10)).Contains(p))
            {
                c.Role = Role.Arrastar;
                c.Drag = delegate(Point q)
                {
                    mixH = SliderFrac(hueR, q) * 359.9f;
                    Invalidate(mixPanel);
                };
                c.Drag(p);
                return true;
            }
            for (int i = 0; i < myR.Length && i < Config.MyColors.Count; i++)
            {
                if (!myR[i].Contains(p)) continue;
                Shapes.ToHsv(Config.MyColors[i], out mixH, out mixS, out mixV);
                Invalidate(mixPanel);
                return true;
            }
            if (useR.Contains(p))
            {
                Color nc = Shapes.FromHsv(mixH, mixS, mixV);
                Config.AddMyColor(nc);
                overlay = Overlay.Nenhum;
                SetCozyColor(nc);
                return true;
            }
            return true;
        }

        // ---------- painel de carimbos ----------

        void DrawCozyStamps(Graphics g)
        {
            DrawPopover(g, stPanel, stClose, "Carimbos");
            for (int i = 0; i < stChips.Length; i++)
            {
                bool sel = i == stampCat;
                if (sel) RoundBox(g, stChips[i], stChips[i].Height / 2f, theme.Taxi, theme.Taxi, 0);
                else RoundBox(g, stChips[i], stChips[i].Height / 2f, theme.Panel, theme.Borda, 1);
                DrawText(g, Stamps.CategoryNames[i], sectionFont, sel ? theme.OnTaxi : theme.Ink, stChips[i], CenterFmt);
            }
            for (int k = 0; k < stCells.Count; k++)
            {
                int i = stVisible[k];
                bool sel = i == cozyStamp;
                Rectangle r = stCells[k];
                RoundBox(g, r, 14 * sc, sel ? theme.Taxi : theme.Surface, sel ? theme.Cone : theme.Borda, (sel ? 2 : 1.5f) * sc);
                Stamps.Draw(g, i, r.X + r.Width / 2f, r.Y + r.Height / 2f, 24 * sc, cozyColor, sel ? theme.OnTaxi : theme.Ink, 1.5f * sc);
            }
            DrawText(g, "TAMANHO", sectionFont, theme.Muted, stSizeLabel, LeftFmt);
            DrawSlider(g, stSizeTrack, (cozyStampSize - 10) / 90f);
        }

        bool CozyStampsDown(Contact c, Point p)
        {
            if (!stPanel.Contains(p) || Rectangle.Inflate(stClose, R(6), R(6)).Contains(p))
            {
                overlay = Overlay.Nenhum;
                Invalidate();
                return true;
            }
            for (int i = 0; i < stChips.Length; i++)
            {
                if (!stChips[i].Contains(p)) continue;
                stampCat = i;
                LayoutCozyStamps();
                Invalidate(stPanel);
                return true;
            }
            for (int k = 0; k < stCells.Count; k++)
            {
                if (!stCells[k].Contains(p)) continue;
                cozyStamp = stVisible[k];
                cozyTool = Tool.Carimbo;
                overlay = Overlay.Nenhum;
                Invalidate();
                return true;
            }
            if (Rectangle.Inflate(stSizeTrack, R(4), R(10)).Contains(p))
            {
                c.Role = Role.Arrastar;
                c.Drag = delegate(Point q)
                {
                    cozyStampSize = 10 + (int)Math.Round(SliderFrac(stSizeTrack, q) * 90);
                    Invalidate(stPanel);
                };
                c.Drag(p);
            }
            return true;
        }

        // ---------- toque ----------

        void CozyDown(Contact c, Point p)
        {
            if (homeBtnR.Contains(p)) { GoTo(Screen.Inicio); return; }
            if (undoBtnR.Contains(p)) { DoUndo(); return; }
            if (redoBtnR.Contains(p)) { DoRedo(); return; }
            if (zoomOutR.Contains(p)) { ZoomBy(1 / 1.25f); return; }
            if (zoomInR.Contains(p)) { ZoomBy(1.25f); return; }
            if (fitBtnR.Contains(p)) { FitCozyView(); InvalidateCozyCanvas(); return; }
            if (galleryBtnR.Contains(p)) { OpenGallery(); return; }
            if (saveBtnR.Contains(p)) { Save(cozySurface, "aconchego"); return; }

            for (int i = 0; i < cozyBtns.Length; i++)
            {
                if (!cozyBtns[i].R.Contains(p)) continue;
                Tool t = CozyTools[i];
                if (t == Tool.ContaGotas && cozyTool != Tool.ContaGotas) cozyPrevTool = cozyTool;
                cozyTool = t;
                if (t == Tool.Carimbo) overlay = Overlay.Carimbos;
                Invalidate();
                return;
            }

            if (cozySide.Contains(p))
            {
                if (plusR.Contains(p) || colorBig.Contains(p)) { OpenMixer(); return; }
                for (int i = 0; i < palR.Length; i++)
                    if (Rectangle.Inflate(palR[i], R(4), R(4)).Contains(p)) { SetCozyColor(Theme.Cozy[i]); return; }
                for (int i = 0; i < recentR.Length && i < recent.Count; i++)
                    if (Rectangle.Inflate(recentR[i], R(4), R(4)).Contains(p)) { SetCozyColor(recent[i]); return; }
                if (Rectangle.Inflate(sizeTrack, R(4), R(10)).Contains(p))
                {
                    c.Role = Role.Arrastar;
                    c.Drag = delegate(Point q) { cozySize = 1 + (int)Math.Round(SliderFrac(sizeTrack, q) * 39); Invalidate(cozySide); };
                    c.Drag(p);
                    return;
                }
                if (Rectangle.Inflate(opacityTrack, R(4), R(10)).Contains(p))
                {
                    c.Role = Role.Arrastar;
                    c.Drag = delegate(Point q) { cozyOpacity = 10 + (int)Math.Round(SliderFrac(opacityTrack, q) * 90); Invalidate(cozySide); };
                    c.Drag(p);
                    return;
                }
                return;
            }

            if (cozyCanvas.Contains(p)) CozyCanvasDown(c, p);
        }

        void CozyCanvasDown(Contact c, Point p)
        {
            if (pinchA != null) return; // já tem pinça: ignora o terceiro dedo
            foreach (Contact o in contacts.Values)
            {
                if (o == c || o.Role != Role.Pintar) continue;
                // segundo dedo na folha: vira pinça (o traço do primeiro é desfeito)
                if (o.Path != null) cozySurface.CancelStroke();
                InvalidateCozyCanvas();
                StartPinch(o, c);
                return;
            }

            c.Role = Role.Pintar;
            c.Tool = cozyTool;
            c.Paper = CozyPaperPt(p);
            if (cozyTool == Tool.Balde || cozyTool == Tool.Carimbo || cozyTool == Tool.ContaGotas) return; // age ao soltar o dedo

            cozySurface.SaveUndo();
            c.Path = new List<PointF>();
            c.Path.Add(c.Paper);
            CozyDraw(c, c.Paper, true);
        }

        float CozyWidth(Tool t)
        {
            switch (t)
            {
                case Tool.Lapis: return Math.Max(2f, cozySize * 0.5f * CozyPx);
                case Tool.Marcador: return cozySize * 2f * CozyPx;
                case Tool.Borracha: return cozySize * 2f * CozyPx;
                case Tool.Spray: return cozySize * 2.2f * CozyPx;
                default: return Math.Max(1.5f, cozySize * CozyPx);
            }
        }

        void CozyStroke(Contact c, Point p)
        {
            PointF cp = CozyPaperPt(p);
            if (c.Path == null) { c.Paper = cp; return; } // ferramentas de toque: só guarda a posição
            CozyDraw(c, cp, false);
        }

        void CozyDraw(Contact c, PointF cp, bool first)
        {
            float w = CozyWidth(c.Tool);
            Rectangle r = Rectangle.Empty;
            switch (c.Tool)
            {
                case Tool.Spray:
                    r = cozySurface.Spray(cp, CozyInk(1), w, rnd);
                    break;
                case Tool.Borracha:
                    r = cozySurface.Line(c.Paper, cp, Color.White, w);
                    break;
                case Tool.Pincel:
                    if (cozyOpacity >= 100) r = cozySurface.Line(c.Paper, cp, cozyColor, w);
                    else if (AddPathPoint(c, cp, w, first)) r = cozySurface.StrokePath(c.Path, CozyInk(1), w, false);
                    break;
                case Tool.Lapis:
                    if (AddPathPoint(c, cp, w, first)) r = cozySurface.StrokePath(c.Path, CozyInk(0.85f), w, false);
                    break;
                case Tool.Marcador:
                    if (AddPathPoint(c, cp, w, first)) r = cozySurface.StrokePath(c.Path, CozyInk(0.45f), w, true);
                    break;
            }
            c.Paper = cp;
            InvalidateCozyPaper(r);
        }

        static bool AddPathPoint(Contact c, PointF cp, float w, bool first)
        {
            if (first) return true;
            PointF last = c.Path[c.Path.Count - 1];
            float dx = cp.X - last.X, dy = cp.Y - last.Y;
            if (dx * dx + dy * dy < Math.Max(2.25f, w * w / 25f)) return false; // pontos muito juntos: pula
            c.Path.Add(cp);
            return true;
        }

        void CozyEndStroke(Contact c)
        {
            PointF cp = c.Paper;
            switch (c.Tool)
            {
                case Tool.Balde:
                    {
                        cozySurface.SaveUndo();
                        Rectangle r = cozySurface.Fill((int)cp.X, (int)cp.Y, CozyInk(1));
                        if (r.IsEmpty) cozySurface.DropLastUndo();
                        else { InvalidateCozyPaper(r); AddRecent(cozyColor); }
                        break;
                    }
                case Tool.Carimbo:
                    if (cp.X < 0 || cp.Y < 0 || cp.X >= CozyPaper || cp.Y >= CozyPaper) break;
                    cozySurface.SaveUndo();
                    InvalidateCozyPaper(cozySurface.Stamp(cp, cozyStamp, CozyInk(1), cozyStampSize * 1.4f));
                    break;
                case Tool.ContaGotas:
                    {
                        Color picked = cozySurface.PickColor((int)cp.X, (int)cp.Y);
                        if (picked != Color.Empty)
                        {
                            cozyTool = cozyPrevTool == Tool.ContaGotas ? Tool.Pincel : cozyPrevTool;
                            SetCozyColor(picked);
                        }
                        break;
                    }
                default:
                    AddRecent(cozyColor);
                    Invalidate(cozySide);
                    break;
            }
        }

        // ---------- pinça ----------

        static float Dist(Point a, Point b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        static PointF Mid(Point a, Point b) { return new PointF((a.X + b.X) / 2f, (a.Y + b.Y) / 2f); }

        void StartPinch(Contact a, Contact b)
        {
            a.Role = Role.Pinca;
            b.Role = Role.Pinca;
            a.Path = null;
            pinchA = a;
            pinchB = b;
            pinchD0 = Math.Max(20f, Dist(a.Screen, b.Screen));
            pinchM0 = Mid(a.Screen, b.Screen);
            pinchS0 = viewScale;
            pinchO0 = viewOrigin;
            hintUntil = 0;
        }

        void PinchMove()
        {
            if (pinchA == null || pinchB == null) return;
            float d = Math.Max(1f, Dist(pinchA.Screen, pinchB.Screen));
            PointF m = Mid(pinchA.Screen, pinchB.Screen);
            float s = Math.Max(fitScale * 0.5f, Math.Min(fitScale * 8f, pinchS0 * d / pinchD0));
            PointF paper = new PointF((pinchM0.X - pinchO0.X) / pinchS0, (pinchM0.Y - pinchO0.Y) / pinchS0);
            viewScale = s;
            viewOrigin = new PointF(m.X - paper.X * s, m.Y - paper.Y * s);
            ClampView();
            InvalidateCozyCanvas();
        }

        void EndPinch()
        {
            if (pinchA != null) pinchA.Role = Role.Nenhum;
            if (pinchB != null) pinchB.Role = Role.Nenhum;
            pinchA = pinchB = null;
            InvalidateCozyCanvas(); // redesenha com qualidade boa
        }
    }
}
