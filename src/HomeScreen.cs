using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pintinho
{
    // Tela inicial (escolha do modo), galeria de desenhos e Área dos pais.
    partial class MainForm
    {
        static readonly Color KidsCardBg = Shapes.Hex(0xffc727), KidsCardBorder = Shapes.Hex(0xe8680c), KidsCardInk = Shapes.Hex(0x2b2118);
        static readonly Color CozyCardBg = Shapes.Hex(0xf7d9c4), CozyCardBorder = Shapes.Hex(0xc8714b), CozyCardInk = Shapes.Hex(0x3a3330);

        readonly Rectangle[] homeCards = new Rectangle[3];
        Rectangle homeLock;
        Font subtitleFont, cardTitleFont;

        Rectangle galPanel, galClose, galPrev, galNext;
        Rectangle[] galCells = new Rectangle[0];
        Size kidsThumbBox, kidsThumbSize, cozyThumbBox, cozyThumbSize;

        // ---------- tela inicial ----------

        void LayoutHome()
        {
            if (subtitleFont != null) { subtitleFont.Dispose(); cardTitleFont.Dispose(); }
            subtitleFont = F(24, FontStyle.Regular);
            cardTitleFont = F(34, FontStyle.Bold);

            int W = view.Width, H = view.Height, y0 = R(196);
            if (!portrait)
            {
                int w = R(352), h = R(470), gap = R(32), x0 = (W - (3 * w + 2 * gap)) / 2;
                for (int i = 0; i < 3; i++) homeCards[i] = new Rectangle(x0 + i * (w + gap), y0, w, h);
            }
            else
            {
                int w = W - 2 * R(48), h = R(250), gap = R(24);
                for (int i = 0; i < 3; i++) homeCards[i] = new Rectangle(R(48), y0 + i * (h + gap), w, h);
            }
            homeLock = new Rectangle(W - R(28) - R(64), H - R(20) - R(64), R(64), R(64));
        }

        void PaintHome(Graphics g, Rectangle clip)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawText(g, "Pintinho", bigFont, theme.Ink, new RectangleF(0, R(44), view.Width, R(84)), CenterFmt);
            DrawText(g, "O que vamos pintar hoje?", subtitleFont, theme.Muted, new RectangleF(0, R(128), view.Width, R(36)), CenterFmt);

            DrawHomeCard(g, homeCards[0], KidsCardBg, KidsCardBorder, KidsCardInk, Color.White, KidsCardInk, false,
                "Infantil", "3+ anos", "Veículos para colorir, botões grandes e balde mágico.", kidsGallery.Items[2], false);
            DrawHomeCard(g, homeCards[1], CozyCardBg, CozyCardBorder, CozyCardInk, Color.White, CozyCardInk, false,
                "Aconchego", "Adultos", "Desenhos fofos e cheios de detalhes para relaxar, com zoom de pinça.", cozyGallery.Items[1], true);
            DrawHomeCard(g, homeCards[2], GRoad, Shapes.Hex(0xffc727), Shapes.Hex(0xf5ecdc), Shapes.Hex(0xffc727), Shapes.Hex(0x2b2118), false,
                "Joguinhos", "Novo", "Comboio e Cobrinha: escolha um e bora jogar.", null, false);

            DrawText(g, "v" + AppInfo.Version + " alpha", smallFont, theme.Muted, new RectangleF(R(32), view.Height - R(52), R(300), R(28)), LeftFmt);
            DrawLockButton(g, homeLock, 18 * sc);

            if (Updater.Status == Updater.State.Disponivel)
            {
                string text = "Nova versão " + Updater.LatestVersion + " disponível · segure o cadeado";
                Size ts = System.Windows.Forms.TextRenderer.MeasureText(text, labelFont);
                Rectangle chip = new Rectangle(view.Width - R(24) - ts.Width - R(32), R(24), ts.Width + R(32), R(40));
                RoundBox(g, chip, chip.Height / 2f, theme.Taxi, theme.Cone, 2 * sc);
                DrawText(g, text, labelFont, theme.OnTaxi, chip, CenterFmt);
            }
        }

        void DrawHomeCard(Graphics g, Rectangle r, Color bg, Color border, Color ink, Color chipBg, Color chipInk, bool dashed,
            string title, string chip, string desc, GalleryItem art, bool square)
        {
            if (dashed) DashedBox(g, r, 28 * sc, bg, border, 2 * sc);
            else RoundBox(g, r, 28 * sc, bg, border, 3 * sc);

            int pad = R(24);
            Rectangle img;
            RectangleF titleR, descR;
            if (!portrait)
            {
                img = new Rectangle(r.X + pad, r.Y + pad, r.Width - 2 * pad, R(210));
                titleR = new RectangleF(r.X + pad, img.Bottom + R(16), r.Width - 2 * pad, R(40));
                descR = new RectangleF(r.X + pad, titleR.Bottom + R(16), r.Width - 2 * pad, r.Bottom - titleR.Bottom - R(16) - pad);
            }
            else
            {
                img = new Rectangle(r.X + pad, r.Y + pad, R(300), r.Height - 2 * pad);
                titleR = new RectangleF(img.Right + R(28), r.Y + R(48), r.Right - img.Right - R(28) - pad, R(40));
                descR = new RectangleF(titleR.X, titleR.Bottom + R(16), titleR.Width, r.Bottom - titleR.Bottom - R(16) - pad);
            }

            bool gameCard = art == null;
            RoundBox(g, img, 18 * sc, gameCard ? Shapes.Hex(0x1c1f24) : Color.White, Color.White, 0);
            if (art != null)
            {
                float k = Math.Min(img.Width * 0.92f / (square ? 1f : 1.43f), img.Height * 0.92f);
                int th = (int)k, tw = (int)(square ? k : k * 1.43f);
                Bitmap thumb = HomeArt(square, art, tw, th);
                g.DrawImage(thumb, new Rectangle(img.X + (img.Width - tw) / 2, img.Y + (img.Height - th) / 2, tw, th));
            }
            else
            {
                DrawComboioArt(g, img);
            }

            DrawText(g, title, cardTitleFont, ink, titleR, LeftFmt);
            Size cs = System.Windows.Forms.TextRenderer.MeasureText(chip, labelFont);
            Rectangle chipR = new Rectangle((int)titleR.Right - cs.Width - R(24), (int)(titleR.Y + (titleR.Height - R(28)) / 2), cs.Width + R(24), R(28));
            RoundBox(g, chipR, chipR.Height / 2f, chipBg, chipBg, 0);
            DrawText(g, chip, labelFont, chipInk, chipR, CenterFmt);
            DrawText(g, desc, bodyFont, ink, descR, WrapFmt);
        }

        Bitmap homeKidsArt, homeCozyArt;

        // Desenho do cartão (cache próprio, separado das miniaturas da galeria).
        Bitmap HomeArt(bool cozy, GalleryItem art, int w, int h)
        {
            Bitmap b = cozy ? homeCozyArt : homeKidsArt;
            if (b != null && b.Width == w && b.Height == h) return b;
            if (b != null) b.Dispose();
            b = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                using (Bitmap ov = Gallery.MakeOverlay(art, w, h))
                    if (ov != null) g.DrawImage(ov, new Rectangle(0, 0, w, h));
            }
            if (cozy) homeCozyArt = b; else homeKidsArt = b;
            return b;
        }

        void HomeDown(Point p)
        {
            if (homeCards[0].Contains(p)) GoTo(Screen.Infantil);
            else if (homeCards[1].Contains(p)) GoTo(Screen.Aconchego);
            else if (homeCards[2].Contains(p)) GoTo(Screen.Jogos);
        }

        // ---------- galeria ----------

        Gallery CurrentGallery() { return screen == Screen.Aconchego ? cozyGallery : kidsGallery; }
        GalleryItem CurrentItem() { return screen == Screen.Aconchego ? cozyCurrent : kidsCurrent; }

        void LayoutGallery()
        {
            int W = view.Width, H = view.Height, m = R(32), inner = R(32), head = R(80);
            galPanel = new Rectangle(m, m, W - 2 * m, H - 2 * m);
            galClose = new Rectangle(galPanel.Right - inner - head, galPanel.Y + R(20), head, head);
            galNext = new Rectangle(galClose.X - R(24) - head, galClose.Y, head, head);
            galPrev = new Rectangle(galNext.X - R(12) - head, galClose.Y, head, head);
            Rectangle grid = Rectangle.FromLTRB(galPanel.X + inner, galClose.Bottom + R(20), galPanel.Right - inner, galPanel.Bottom - inner);

            int cols = portrait ? 3 : 5, rows = portrait ? 5 : 3, gx = R(20), gy = R(16);
            float cw = (grid.Width - (cols - 1) * gx) / (float)cols;
            float ch = (grid.Height - (rows - 1) * gy) / (float)rows;
            galCells = new Rectangle[cols * rows];
            for (int k = 0; k < galCells.Length; k++)
                galCells[k] = new Rectangle((int)(grid.X + (k % cols) * (cw + gx)), (int)(grid.Y + (k / cols) * (ch + gy)), (int)cw, (int)ch);

            float availW = cw - R(16), availH = ch - R(24) - labelFont.Height;
            float f = Math.Max(0.2f, Math.Min(Math.Min(availW / 188f, availH / 132f), sc));
            kidsThumbBox = new Size((int)(188 * f), (int)(132 * f));
            kidsThumbSize = new Size((int)(180 * f), (int)(126 * f));
            int sq = (int)Math.Max(20, Math.Min(availW, Math.Min(availH, 150 * sc)));
            cozyThumbBox = new Size(sq, sq);
            cozyThumbSize = new Size(sq - R(8), sq - R(8));
        }

        int GalleryPages()
        {
            int per = Math.Max(1, galCells.Length);
            return (CurrentGallery().Items.Count + per - 1) / per;
        }

        void OpenGallery()
        {
            Gallery gal = CurrentGallery();
            gal.Page = Math.Max(0, gal.Items.IndexOf(CurrentItem())) / Math.Max(1, galCells.Length);
            overlay = Overlay.Galeria;
            Invalidate();
        }

        void DrawGallery(Graphics g, Rectangle clip)
        {
            Gallery gal = CurrentGallery();
            bool cozy = screen == Screen.Aconchego;
            using (SolidBrush veil = new SolidBrush(theme.Veil)) g.FillRectangle(veil, clip);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RoundBox(g, galPanel, 28 * sc, theme.Surface, theme.Surface, 0);

            int titleX = galPanel.X + R(32);
            int pages = GalleryPages();
            int titleRight = pages > 1 ? galPrev.X - R(12) : galClose.X - R(12);
            DrawText(g, "Escolha um desenho!", titleFont, theme.Ink, new RectangleF(titleX, galClose.Y, titleRight - titleX, galClose.Height), LeftFmt);
            DrawCloseCircle(g, galClose);

            if (pages > 1)
            {
                DrawArrow(g, galPrev, false, gal.Page > 0);
                DrawArrow(g, galNext, true, gal.Page < pages - 1);
            }

            Size box = cozy ? cozyThumbBox : kidsThumbBox, ts = cozy ? cozyThumbSize : kidsThumbSize;
            GalleryItem current = CurrentItem();
            int start = gal.Page * galCells.Length;
            for (int k = 0; k < galCells.Length; k++)
            {
                int idx = start + k;
                if (idx >= gal.Items.Count) break;
                Rectangle cell = galCells[k];
                if (!clip.IntersectsWith(cell)) continue;
                GalleryItem it = gal.Items[idx];
                bool sel = it == current;
                Color fg = sel ? theme.OnTaxi : theme.Ink;
                RoundBox(g, cell, 18 * sc, sel ? theme.Taxi : theme.Panel, sel ? theme.Cone : theme.Borda, (sel ? 3 : 2) * sc);

                float lh = labelFont.Height;
                float top = cell.Y + (cell.Height - (box.Height + 8 * sc + lh)) / 2f;
                Rectangle b = new Rectangle(cell.X + (cell.Width - box.Width) / 2, (int)top, box.Width, box.Height);
                RoundBox(g, b, 10 * sc, Theme.Papel, Theme.Papel, 0);
                Bitmap th = gal.GetThumb(it, ts.Width, ts.Height);
                g.DrawImage(th, new Rectangle(b.X + (b.Width - th.Width) / 2, b.Y + (b.Height - th.Height) / 2, th.Width, th.Height));
                DrawText(g, it.Name, labelFont, fg, new RectangleF(cell.X + 4, top + box.Height + 8 * sc, cell.Width - 8, lh), CenterFmt);
            }
        }

        void DrawArrow(Graphics g, Rectangle r, bool right, bool enabled)
        {
            RoundBox(g, r, 18 * sc, theme.Panel, theme.Borda, 2 * sc);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = 14 * sc, d = right ? 1 : -1;
            PointF[] tri = { new PointF(cx - d * s * 0.6f, cy - s), new PointF(cx + d * s * 0.8f, cy), new PointF(cx - d * s * 0.6f, cy + s) };
            using (SolidBrush b = new SolidBrush(enabled ? theme.Ink : theme.Borda)) g.FillPolygon(b, tri);
        }

        void GalleryDown(Point p)
        {
            Gallery gal = CurrentGallery();
            if (Rectangle.Inflate(galClose, R(8), R(8)).Contains(p))
            {
                overlay = Overlay.Nenhum;
                Invalidate();
                return;
            }
            int pages = GalleryPages();
            if (pages > 1 && galPrev.Contains(p))
            {
                if (gal.Page > 0) { gal.Page--; Invalidate(); }
                return;
            }
            if (pages > 1 && galNext.Contains(p))
            {
                if (gal.Page < pages - 1) { gal.Page++; Invalidate(); }
                return;
            }
            int start = gal.Page * galCells.Length;
            for (int k = 0; k < galCells.Length; k++)
            {
                if (!galCells[k].Contains(p)) continue;
                int idx = start + k;
                if (idx < gal.Items.Count)
                {
                    if (screen == Screen.Aconchego) LoadCozyItem(gal.Items[idx]);
                    else LoadKidsItem(gal.Items[idx]);
                    overlay = Overlay.Nenhum;
                    Invalidate();
                }
                return;
            }
        }

        // ---------- Área dos pais ----------

        Rectangle parentPanel;

        void LayoutParent() { }

        List<string> ParentItems()
        {
            List<string> items = new List<string>();
            items.Add("tema");
            if (screen != Screen.Inicio) items.Add("inicio");
            items.Add("atualizar");
            items.Add("sair");
            items.Add("voltar");
            return items;
        }

        Rectangle ParentSlot(int i)
        {
            int bw = parentPanel.Width - R(64), bh = R(72);
            return new Rectangle(parentPanel.X + R(32), parentPanel.Y + R(96) + i * (bh + R(14)), bw, bh);
        }

        void ComputeParentPanel()
        {
            int n = ParentItems().Count;
            int pw = Math.Min(view.Width - R(64), R(600));
            int ph = R(96) + n * R(72) + (n - 1) * R(14) + R(64);
            parentPanel = new Rectangle((view.Width - pw) / 2, (view.Height - ph) / 2, pw, ph);
        }

        string UpdateLabel()
        {
            switch (Updater.Status)
            {
                case Updater.State.Verificando: return "Procurando atualização...";
                case Updater.State.Disponivel: return "Atualizar para a versão " + Updater.LatestVersion;
                case Updater.State.Baixando: return "Baixando... " + Updater.Progress + "%";
                case Updater.State.Erro: return "Sem conexão · tentar de novo";
                case Updater.State.Atualizado: return "Você já tem a versão mais nova";
                default: return "Procurar atualização";
            }
        }

        void DrawParent(Graphics g)
        {
            ComputeParentPanel();
            using (SolidBrush dim = new SolidBrush(theme.Dim)) g.FillRectangle(dim, new Rectangle(Point.Empty, view));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RoundBox(g, parentPanel, 28 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, "Área dos pais", toastFont, theme.Ink, new RectangleF(parentPanel.X, parentPanel.Y + R(24), parentPanel.Width, R(52)), CenterFmt);

            List<string> items = ParentItems();
            for (int i = 0; i < items.Count; i++)
            {
                Rectangle r = ParentSlot(i);
                switch (items[i])
                {
                    case "tema":
                        RoundBox(g, r, 18 * sc, theme.Panel, theme.Borda, 2 * sc);
                        DrawText(g, Config.DarkTheme ? "Tema: Escuro  ›  Claro" : "Tema: Claro  ›  Escuro", buttonFont, theme.Ink, r, CenterFmt);
                        break;
                    case "inicio":
                        RoundBox(g, r, 18 * sc, theme.Panel, theme.Borda, 2 * sc);
                        DrawText(g, "Trocar de modo (tela inicial)", buttonFont, theme.Ink, r, CenterFmt);
                        break;
                    case "atualizar":
                        {
                            bool hot = Updater.Status == Updater.State.Disponivel;
                            RoundBox(g, r, 18 * sc, hot ? theme.Trator : theme.Panel, hot ? theme.Trator : theme.Borda, 2 * sc);
                            if (Updater.Status == Updater.State.Baixando)
                            {
                                Rectangle bar = new Rectangle(r.X, r.Y, r.Width * Updater.Progress / 100, r.Height);
                                RoundBox(g, bar, 18 * sc, Color.FromArgb(70, theme.Trator), Color.Empty, 0);
                            }
                            DrawText(g, UpdateLabel(), buttonFont, hot ? theme.OnTrator : theme.Ink, r, CenterFmt);
                            break;
                        }
                    case "sair":
                        RoundBox(g, r, 18 * sc, theme.Bombeiro, theme.Bombeiro, 0);
                        DrawText(g, "Sair do Pintinho", buttonFont, Color.White, r, CenterFmt);
                        break;
                    case "voltar":
                        RoundBox(g, r, 18 * sc, theme.Taxi, theme.Cone, 3 * sc);
                        DrawText(g, "Voltar a pintar", buttonFont, theme.OnTaxi, r, CenterFmt);
                        break;
                }
            }
            DrawText(g, "Versão " + AppInfo.Version + " · Ctrl+Shift+Q também sai", smallFont, theme.Muted,
                new RectangleF(parentPanel.X, parentPanel.Bottom - R(48), parentPanel.Width, R(28)), CenterFmt);
        }

        void ParentDown(Point p)
        {
            ComputeParentPanel();
            if (!parentPanel.Contains(p))
            {
                overlay = Overlay.Nenhum;
                Invalidate();
                return;
            }
            List<string> items = ParentItems();
            for (int i = 0; i < items.Count; i++)
            {
                if (!ParentSlot(i).Contains(p)) continue;
                switch (items[i])
                {
                    case "tema":
                        Config.DarkTheme = !Config.DarkTheme;
                        Config.Save();
                        UpdateTheme();
                        Invalidate();
                        break;
                    case "inicio":
                        GoTo(Screen.Inicio);
                        break;
                    case "atualizar":
                        if (Updater.Status == Updater.State.Disponivel)
                            Updater.Download(delegate(string path) { if (Updater.RunInstaller(path)) ExitApp(); });
                        else if (Updater.Status != Updater.State.Verificando && Updater.Status != Updater.State.Baixando)
                            Updater.Check();
                        Invalidate();
                        break;
                    case "sair":
                        ExitApp();
                        break;
                    case "voltar":
                        overlay = Overlay.Nenhum;
                        Invalidate();
                        break;
                }
                return;
            }
        }
    }
}
