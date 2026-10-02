using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Pintinho
{
    enum Tool { Pincel, Spray, ArcoIris, Balde, Carimbo, Borracha }
    enum Mode { Pintar, Galeria, Pais }

    class Btn
    {
        public Rectangle R;
        public readonly string Id, Label;
        public Btn(string id, string label) { Id = id; Label = label; }
    }

    // A janela inteira é desenhada à mão (um controle só = leve no Atom).
    // Medidas em pixels do design (1280x800 paisagem / 800x1280 retrato) multiplicadas por "sc".
    class MainForm : Form
    {
        static readonly float[] Sizes = { 6f, 14f, 28f };     // espessura do pincel (folha de 1000px)
        static readonly float[] SizeDots = { 7f, 12f, 18f };  // bolinha do botão Tamanho
        const int LockHoldMs = 3000;
        const float PaperAspect = 10f / 7f;

        static readonly StringFormat CenterFmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        static readonly StringFormat LeftFmt = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetProp(IntPtr hWnd, string name, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern ushort GlobalAddAtom(string name);

        public string CaptureDir;

        readonly bool kiosk;
        KioskGuard guard;
        bool allowClose;

        Theme theme;
        Surface surface;
        Gallery gallery;
        GalleryItem current;
        Mode mode = Mode.Galeria;

        // layout
        Size view;
        bool portrait;
        float sc = 1f;
        Rectangle canvasArea, paperRect;
        readonly Btn[] toolBtns = {
            new Btn("pincel", "Pincel"), new Btn("spray", "Spray"), new Btn("arco", "Arco-íris"),
            new Btn("balde", "Balde"), new Btn("carimbo", "Carimbo"), new Btn("borracha", "Borracha")
        };
        readonly Btn[] actionBtns = {
            new Btn("desenhos", "Desenhos"), new Btn("desfazer", "Desfazer"), new Btn("limpar", "Limpar"), new Btn("salvar", "Salvar")
        };
        readonly Btn sizeBtn = new Btn("tamanho", "Tamanho");
        readonly Btn lockBtn = new Btn("cadeado", "");
        readonly Rectangle[] swatches = new Rectangle[Theme.Palette.Length];
        Font labelFont, smallFont, titleFont, toastFont, buttonFont;

        Rectangle galPanel, galClose, galPrev, galNext;
        Rectangle[] galCells = new Rectangle[0];
        Size thumbBox, thumbSize;

        Rectangle parentPanel, parentTheme, parentExit, parentBack;

        // pintura
        Tool tool = Tool.Pincel;
        Color color = Theme.Palette[4];
        int sizeIdx = 1, stampKind;
        bool drawing;
        Point last;
        float hue;
        readonly Random rnd = new Random();
        readonly Timer timer = new Timer();
        int lockDownAt = -1;
        float lockProgress;
        string toast;
        int toastEnd;

        public MainForm(bool kiosk)
        {
            this.kiosk = kiosk;
            Config.Load();
            theme = Config.DarkTheme ? Theme.Escuro : Theme.Claro;

            Text = "Pintinho";
            BackColor = theme.Surface;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            KeyPreview = true;
            if (kiosk)
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                TopMost = true;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.FixedSingle;
                MaximizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(1280, 800);
            }
            timer.Interval = 40;
            timer.Tick += OnTick;
        }

        // ---------- ciclo de vida ----------

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                // Toque: sem "segurar = clique direito" e sem gestos de flick.
                GlobalAddAtom("MicrosoftTabletPenServiceProperty");
                SetProp(Handle, "MicrosoftTabletPenServiceProperty", new IntPtr(0x00010001));
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (kiosk) Bounds = Screen.PrimaryScreen.Bounds;
            gallery = new Gallery();
            current = gallery.Items[0];
            DoLayout(ClientSize);
            if (kiosk)
            {
                guard = new KioskGuard();
                guard.ExitRequested += delegate { BeginInvoke(new MethodInvoker(ExitApp)); };
                SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            }
            timer.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (CaptureDir != null) BeginInvoke(new MethodInvoker(RunCapture));
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            // tablet girou ou resolução mudou: ocupa a tela inteira de novo
            BeginInvoke(new MethodInvoker(delegate { Bounds = Screen.PrimaryScreen.Bounds; }));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (gallery == null || CaptureDir != null || ClientSize.Width < 100 || ClientSize.Height < 100) return;
            if (ClientSize == view) return;
            drawing = false;
            DoLayout(ClientSize);
            Invalidate();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (kiosk && !allowClose)
                BeginInvoke(new MethodInvoker(delegate { if (!allowClose) Activate(); }));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (kiosk && !allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            if (kiosk) SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            if (guard != null) guard.Dispose();
            if (surface != null) surface.Dispose();
            base.OnFormClosed(e);
        }

        void ExitApp()
        {
            allowClose = true;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z)) { DoUndo(); return true; }
            if (keyData == (Keys.Control | Keys.Shift | Keys.Q)) { ExitApp(); return true; }
            if (!kiosk && keyData == Keys.Escape) { ExitApp(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------- layout ----------

        int R(float v) { return (int)Math.Round(v * sc); }

        static Rectangle Center(Rectangle area, int w, int h)
        {
            return new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        }

        static void Column(Btn[] bs, int x, int y0, int height, int size, int gap)
        {
            int total = bs.Length * size + (bs.Length - 1) * gap;
            int y = y0 + (height - total) / 2;
            for (int i = 0; i < bs.Length; i++) bs[i].R = new Rectangle(x, y + i * (size + gap), size, size);
        }

        static void Row(Btn[] bs, int x0, int width, int y, int size, int gap)
        {
            int total = bs.Length * size + (bs.Length - 1) * gap;
            int x = x0 + (width - total) / 2;
            for (int i = 0; i < bs.Length; i++) bs[i].R = new Rectangle(x + i * (size + gap), y, size, size);
        }

        void DoLayout(Size sz)
        {
            view = sz;
            int W = sz.Width, H = sz.Height;
            portrait = H > W;
            sc = portrait ? Math.Min(W / 800f, H / 1280f) : Math.Min(W / 1280f, H / 800f);

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
                lockBtn.R = Center(new Rectangle(W - col, areaH, col, bottom), R(64), R(64));
                palArea = new Rectangle(col, areaH, W - 2 * col, bottom);
                palCols = 16;
            }
            else
            {
                int top = R(128), toolsH = R(128), palH = R(160);
                sizeBtn.R = Center(new Rectangle(0, 0, col, top), btn, R(80));
                lockBtn.R = Center(new Rectangle(W - col, 0, col, top), R(64), R(64));
                Row(actionBtns, 0, W, (top - btn) / 2, btn, gap);
                canvasArea = new Rectangle(pad, top, W - 2 * pad, H - top - toolsH - palH);
                Row(toolBtns, 0, W, H - palH - toolsH + (toolsH - btn) / 2, btn, gap);
                palArea = new Rectangle(R(8), H - palH, W - R(16), palH);
                palCols = 8;
            }

            int palRows = (swatches.Length + palCols - 1) / palCols, rowGap = R(18);
            float cellW = palArea.Width / (float)palCols;
            int y0 = palArea.Y + (palArea.Height - (palRows * sw + (palRows - 1) * rowGap)) / 2;
            for (int i = 0; i < swatches.Length; i++)
            {
                float cx = palArea.X + cellW * (i % palCols + 0.5f);
                swatches[i] = new Rectangle((int)(cx - sw / 2f), y0 + (i / palCols) * (sw + rowGap), sw, sw);
            }

            // Folha com proporção fixa 10:7, centralizada na área branca.
            int inset = R(10);
            int aw = Math.Max(20, canvasArea.Width - 2 * inset), ah = Math.Max(14, canvasArea.Height - 2 * inset);
            int pw = Math.Min(aw, (int)(ah * PaperAspect));
            int ph = (int)(pw / PaperAspect);
            paperRect = new Rectangle(canvasArea.X + (canvasArea.Width - pw) / 2, canvasArea.Y + (canvasArea.Height - ph) / 2, pw, ph);

            MakeFonts();

            if (surface == null) surface = new Surface(pw, ph);
            else if (surface.W != pw || surface.H != ph)
            {
                surface.Resize(pw, ph);
                surface.SetOverlay(Gallery.MakeOverlay(current, pw, ph));
            }

            LayoutGallery();
            LayoutParent();
        }

        void MakeFonts()
        {
            if (labelFont != null)
            {
                labelFont.Dispose(); smallFont.Dispose(); titleFont.Dispose(); toastFont.Dispose(); buttonFont.Dispose();
            }
            labelFont = new Font("Segoe UI", 14 * sc, FontStyle.Bold, GraphicsUnit.Pixel);
            smallFont = new Font("Segoe UI", 12 * sc, FontStyle.Bold, GraphicsUnit.Pixel);
            titleFont = new Font("Segoe UI", 38 * sc, FontStyle.Bold, GraphicsUnit.Pixel);
            toastFont = new Font("Segoe UI", 26 * sc, FontStyle.Bold, GraphicsUnit.Pixel);
            buttonFont = new Font("Segoe UI", 20 * sc, FontStyle.Bold, GraphicsUnit.Pixel);
        }

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
            thumbBox = new Size((int)(188 * f), (int)(132 * f));
            thumbSize = new Size((int)(180 * f), (int)(126 * f));
        }

        void LayoutParent()
        {
            int pw = Math.Min(view.Width - R(64), R(560)), ph = R(470);
            parentPanel = new Rectangle((view.Width - pw) / 2, (view.Height - ph) / 2, pw, ph);
            int bw = pw - R(64), bh = R(80), x = parentPanel.X + R(32), y = parentPanel.Y + R(100);
            parentTheme = new Rectangle(x, y, bw, bh);
            parentExit = new Rectangle(x, y + bh + R(16), bw, bh);
            parentBack = new Rectangle(x, y + 2 * (bh + R(16)), bw, bh);
        }

        int GalleryPages()
        {
            int per = Math.Max(1, galCells.Length);
            return (gallery.Items.Count + per - 1) / per;
        }

        // ---------- desenho da interface ----------

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintAll(e.Graphics, e.ClipRectangle);
        }

        void PaintAll(Graphics g, Rectangle clip)
        {
            if (surface == null) { g.Clear(theme.Surface); return; }
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (mode == Mode.Galeria) { DrawGallery(g, clip); return; }

            using (SolidBrush bg = new SolidBrush(theme.Surface)) g.FillRectangle(bg, clip);
            if (clip.IntersectsWith(canvasArea)) DrawCanvas(g, clip);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < toolBtns.Length; i++)
                if (clip.IntersectsWith(toolBtns[i].R)) DrawButton(g, toolBtns[i], (int)tool == i);
            foreach (Btn b in actionBtns)
                if (clip.IntersectsWith(b.R)) DrawButton(g, b, false);
            if (clip.IntersectsWith(sizeBtn.R)) DrawSizeButton(g);
            for (int i = 0; i < swatches.Length; i++)
                if (clip.IntersectsWith(Rectangle.Inflate(swatches[i], R(10), R(10)))) DrawSwatch(g, i);
            if (clip.IntersectsWith(Rectangle.Inflate(lockBtn.R, R(8), R(8)))) DrawLock(g);
            if (toast != null) DrawToast(g);
            if (mode == Mode.Pais) DrawParent(g);
        }

        void RoundBox(Graphics g, Rectangle r, float radius, Color fill, Color border, float bw)
        {
            RectangleF rf = new RectangleF(r.X + bw / 2f, r.Y + bw / 2f, r.Width - bw, r.Height - bw);
            using (GraphicsPath p = Shapes.RoundRect(rf, radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (bw > 0)
                    using (Pen pen = new Pen(border, bw)) g.DrawPath(pen, p);
            }
        }

        static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r, StringFormat fmt)
        {
            using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, r, fmt);
        }

        void DrawCanvas(Graphics g, Rectangle clip)
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
            g.DrawImage(surface.Paint, cr, src, GraphicsUnit.Pixel);
            if (surface.Overlay != null) g.DrawImage(surface.Overlay, cr, src, GraphicsUnit.Pixel);
            g.CompositingQuality = CompositingQuality.Default;
            g.InterpolationMode = InterpolationMode.Default;
            g.PixelOffsetMode = PixelOffsetMode.Default;
        }

        void DrawButton(Graphics g, Btn b, bool sel)
        {
            Color fg = sel ? theme.OnTaxi : theme.Ink;
            RoundBox(g, b.R, 18 * sc, sel ? theme.Taxi : theme.Panel, sel ? theme.Cone : theme.Borda, (sel ? 3 : 2) * sc);
            float icon = 56 * sc, lh = labelFont.Height;
            float top = b.R.Y + (b.R.Height - (icon + 6 * sc + lh)) / 2f;
            Icons.Draw(g, b.Id, new RectangleF(b.R.X + (b.R.Width - icon) / 2f, top, icon, icon), color, fg, theme, stampKind);
            DrawText(g, b.Label, labelFont, fg, new RectangleF(b.R.X, top + icon + 6 * sc, b.R.Width, lh), CenterFmt);
        }

        void DrawSizeButton(Graphics g)
        {
            Rectangle r = sizeBtn.R;
            RoundBox(g, r, 18 * sc, theme.Panel, theme.Borda, 2 * sc);
            float box = 44 * sc, lh = smallFont.Height;
            float top = r.Y + (r.Height - (box + 4 * sc + lh)) / 2f;
            float cx = r.X + r.Width / 2f, cy = top + box / 2f, rad = SizeDots[sizeIdx] * sc;
            using (SolidBrush b = new SolidBrush(color)) g.FillEllipse(b, cx - rad, cy - rad, 2 * rad, 2 * rad);
            using (Pen p = new Pen(theme.Ink, 1.5f * sc)) g.DrawEllipse(p, cx - rad, cy - rad, 2 * rad, 2 * rad);
            DrawText(g, sizeBtn.Label, smallFont, theme.Ink, new RectangleF(r.X, top + box + 4 * sc, r.Width, lh), CenterFmt);
        }

        void DrawSwatch(Graphics g, int i)
        {
            Rectangle s = swatches[i];
            Color c = Theme.Palette[i];
            float bw = 2 * sc;
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, s);
            bool whiteOnLight = c.ToArgb() == Color.White.ToArgb() && theme == Theme.Claro;
            using (Pen p = new Pen(whiteOnLight ? theme.Borda : theme.SwatchEdge, bw))
                g.DrawEllipse(p, s.X + bw / 2, s.Y + bw / 2, s.Width - bw, s.Height - bw);
            if (c.ToArgb() == color.ToArgb())
            {
                float ring = 4 * sc, off = 4 * sc + ring / 2;
                using (Pen p = new Pen(theme.Cone, ring))
                    g.DrawEllipse(p, s.X - off, s.Y - off, s.Width + 2 * off, s.Height + 2 * off);
            }
        }

        void DrawLock(Graphics g)
        {
            Rectangle r = lockBtn.R;
            RoundBox(g, r, 18 * sc, theme.Surface, theme.Borda, 2 * sc);
            float icon = 36 * sc;
            Icons.Draw(g, "cadeado", new RectangleF(r.X + (r.Width - icon) / 2f, r.Y + (r.Height - icon) / 2f, icon, icon), color, theme.Ink, theme, 0);
            if (lockProgress > 0)
                using (Pen p = new Pen(theme.Trator, 5 * sc))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    Rectangle a = Rectangle.Inflate(r, R(4), R(4));
                    g.DrawArc(p, a, -90, 360 * lockProgress);
                }
        }

        Rectangle ToastRect()
        {
            Size ts = TextRenderer.MeasureText(toast ?? "", toastFont);
            int w = ts.Width + R(48), h = R(56);
            return new Rectangle(paperRect.X + (paperRect.Width - w) / 2, paperRect.Y + R(16), w, h);
        }

        void DrawToast(Graphics g)
        {
            Rectangle r = ToastRect();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RoundBox(g, r, r.Height / 2f, theme.Taxi, theme.Cone, 2 * sc);
            DrawText(g, toast, toastFont, theme.OnTaxi, r, CenterFmt);
        }

        void DrawGallery(Graphics g, Rectangle clip)
        {
            using (SolidBrush veil = new SolidBrush(theme.Veil)) g.FillRectangle(veil, clip);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RoundBox(g, galPanel, 28 * sc, theme.Surface, theme.Surface, 0);

            int titleX = galPanel.X + R(32);
            int pages = GalleryPages();
            int titleRight = pages > 1 ? galPrev.X - R(12) : galClose.X - R(12);
            DrawText(g, "Escolha um desenho!", titleFont, theme.Ink, new RectangleF(titleX, galClose.Y, titleRight - titleX, galClose.Height), LeftFmt);

            // fechar
            using (SolidBrush b = new SolidBrush(theme.Bombeiro)) g.FillEllipse(b, galClose);
            using (Pen p = Shapes.RoundPen(Color.White, 5.8f * sc))
            {
                float cx = galClose.X + galClose.Width / 2f, cy = galClose.Y + galClose.Height / 2f, h = 10.8f * sc;
                g.DrawLine(p, cx - h, cy - h, cx + h, cy + h);
                g.DrawLine(p, cx + h, cy - h, cx - h, cy + h);
            }

            if (pages > 1)
            {
                DrawArrow(g, galPrev, false, gallery.Page > 0);
                DrawArrow(g, galNext, true, gallery.Page < pages - 1);
            }

            int start = gallery.Page * galCells.Length;
            for (int k = 0; k < galCells.Length; k++)
            {
                int idx = start + k;
                if (idx >= gallery.Items.Count) break;
                Rectangle cell = galCells[k];
                if (!clip.IntersectsWith(cell)) continue;
                GalleryItem it = gallery.Items[idx];
                bool sel = it == current;
                Color fg = sel ? theme.OnTaxi : theme.Ink;
                RoundBox(g, cell, 18 * sc, sel ? theme.Taxi : theme.Panel, sel ? theme.Cone : theme.Borda, (sel ? 3 : 2) * sc);

                float lh = labelFont.Height;
                float top = cell.Y + (cell.Height - (thumbBox.Height + 8 * sc + lh)) / 2f;
                Rectangle box = new Rectangle(cell.X + (cell.Width - thumbBox.Width) / 2, (int)top, thumbBox.Width, thumbBox.Height);
                RoundBox(g, box, 10 * sc, Theme.Papel, Theme.Papel, 0);
                Bitmap th = gallery.GetThumb(it, thumbSize.Width, thumbSize.Height);
                g.DrawImage(th, new Rectangle(box.X + (box.Width - th.Width) / 2, box.Y + (box.Height - th.Height) / 2, th.Width, th.Height));
                DrawText(g, it.Name, labelFont, fg, new RectangleF(cell.X + 4, top + thumbBox.Height + 8 * sc, cell.Width - 8, lh), CenterFmt);
            }
        }

        void DrawArrow(Graphics g, Rectangle r, bool right, bool enabled)
        {
            RoundBox(g, r, 18 * sc, theme.Panel, theme.Borda, 2 * sc);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = 14 * sc, d = right ? 1 : -1;
            PointF[] tri = { new PointF(cx - d * s * 0.6f, cy - s), new PointF(cx + d * s * 0.8f, cy), new PointF(cx - d * s * 0.6f, cy + s) };
            using (SolidBrush b = new SolidBrush(enabled ? theme.Ink : theme.Borda)) g.FillPolygon(b, tri);
        }

        void DrawParent(Graphics g)
        {
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(170, theme.Veil)))
                g.FillRectangle(dim, new Rectangle(Point.Empty, view));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RoundBox(g, parentPanel, 28 * sc, theme.Surface, theme.Borda, 2 * sc);
            DrawText(g, "Área dos pais", toastFont, theme.Ink,
                new RectangleF(parentPanel.X, parentPanel.Y + R(24), parentPanel.Width, R(52)), CenterFmt);

            RoundBox(g, parentTheme, 18 * sc, theme.Panel, theme.Borda, 2 * sc);
            DrawText(g, theme == Theme.Claro ? "Tema: Claro  ›  Escuro" : "Tema: Escuro  ›  Claro", buttonFont, theme.Ink, parentTheme, CenterFmt);
            RoundBox(g, parentExit, 18 * sc, theme.Bombeiro, theme.Bombeiro, 0);
            DrawText(g, "Sair do Pintinho", buttonFont, Color.White, parentExit, CenterFmt);
            RoundBox(g, parentBack, 18 * sc, theme.Taxi, theme.Cone, 3 * sc);
            DrawText(g, "Voltar a pintar", buttonFont, theme.OnTaxi, parentBack, CenterFmt);
            DrawText(g, "Atalho: Ctrl+Shift+Q também sai", smallFont, theme.Muted,
                new RectangleF(parentPanel.X, parentBack.Bottom + R(12), parentPanel.Width, R(28)), CenterFmt);
        }

        // ---------- toque / mouse ----------

        Point ToPaper(Point p) { return new Point(p.X - paperRect.X, p.Y - paperRect.Y); }

        void InvalidatePaper(Rectangle r)
        {
            if (r.IsEmpty) return;
            r.Offset(paperRect.X, paperRect.Y);
            r.Intersect(paperRect);
            if (!r.IsEmpty) Invalidate(r);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (surface == null || e.Button != MouseButtons.Left) return;
            Point p = e.Location;

            if (mode == Mode.Galeria) { GalleryClick(p); return; }
            if (mode == Mode.Pais) { ParentClick(p); return; }

            if (lockBtn.R.Contains(p))
            {
                lockDownAt = Environment.TickCount;
                lockProgress = 0;
                return;
            }
            for (int i = 0; i < toolBtns.Length; i++)
            {
                if (!toolBtns[i].R.Contains(p)) continue;
                Tool t = (Tool)i;
                if (t == Tool.Carimbo && tool == Tool.Carimbo) stampKind = (stampKind + 1) % Shapes.StampKinds;
                tool = t;
                Invalidate();
                return;
            }
            foreach (Btn b in actionBtns)
            {
                if (!b.R.Contains(p)) continue;
                DoAction(b.Id);
                return;
            }
            if (sizeBtn.R.Contains(p))
            {
                sizeIdx = (sizeIdx + 1) % Sizes.Length;
                Invalidate(sizeBtn.R);
                return;
            }
            for (int i = 0; i < swatches.Length; i++)
            {
                if (!Rectangle.Inflate(swatches[i], R(8), R(8)).Contains(p)) continue;
                color = Theme.Palette[i];
                if (tool == Tool.Borracha) tool = Tool.Pincel;
                Invalidate();
                return;
            }
            if (canvasArea.Contains(p)) BeginStroke(ToPaper(p));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drawing && (e.Button & MouseButtons.Left) != 0) StrokeTo(ToPaper(e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            drawing = false;
            if (lockDownAt >= 0)
            {
                lockDownAt = -1;
                lockProgress = 0;
                Invalidate(Rectangle.Inflate(lockBtn.R, R(8), R(8)));
            }
        }

        void OnTick(object sender, EventArgs e)
        {
            if (lockDownAt >= 0)
            {
                int el = Environment.TickCount - lockDownAt;
                lockProgress = Math.Min(1f, el / (float)LockHoldMs);
                Invalidate(Rectangle.Inflate(lockBtn.R, R(8), R(8)));
                if (el >= LockHoldMs)
                {
                    lockDownAt = -1;
                    lockProgress = 0;
                    mode = Mode.Pais;
                    Invalidate();
                }
            }
            if (drawing && tool == Tool.Spray) StrokeTo(last);
            if (toast != null && Environment.TickCount - toastEnd >= 0)
            {
                Rectangle r = ToastRect();
                toast = null;
                Invalidate(r);
            }
        }

        void DoAction(string id)
        {
            switch (id)
            {
                case "desenhos":
                    gallery.Page = Math.Max(0, gallery.Items.IndexOf(current)) / Math.Max(1, galCells.Length);
                    mode = Mode.Galeria;
                    Invalidate();
                    break;
                case "desfazer":
                    DoUndo();
                    break;
                case "limpar":
                    surface.SaveUndo();
                    surface.Clear();
                    Invalidate(paperRect);
                    break;
                case "salvar":
                    Save();
                    break;
            }
        }

        void DoUndo()
        {
            if (mode == Mode.Pintar && surface != null && surface.Undo()) Invalidate(paperRect);
        }

        float PaperScale() { return paperRect.Width / 1000f; }

        void BeginStroke(Point cp)
        {
            surface.SaveUndo();
            switch (tool)
            {
                case Tool.Balde:
                    {
                        Cursor = Cursors.WaitCursor;
                        Rectangle r = surface.Fill(cp.X, cp.Y, color);
                        Cursor = Cursors.Default;
                        if (r.IsEmpty) surface.DropLastUndo();
                        else InvalidatePaper(r);
                        break;
                    }
                case Tool.Carimbo:
                    InvalidatePaper(surface.Stamp(cp, stampKind, color, (18 + Sizes[sizeIdx] * 1.7f) * PaperScale()));
                    break;
                default:
                    drawing = true;
                    last = cp;
                    StrokeTo(cp);
                    break;
            }
        }

        void StrokeTo(Point cp)
        {
            float w = Sizes[sizeIdx] * PaperScale();
            Rectangle r;
            switch (tool)
            {
                case Tool.Borracha:
                    r = surface.Line(last, cp, Color.White, w * 1.8f);
                    break;
                case Tool.ArcoIris:
                    hue = (hue + 8) % 360;
                    r = surface.Line(last, cp, Shapes.FromHue(hue), w * 1.3f);
                    break;
                case Tool.Spray:
                    r = surface.Spray(cp, color, w * 2.2f, rnd);
                    break;
                default:
                    r = surface.Line(last, cp, color, w);
                    break;
            }
            last = cp;
            InvalidatePaper(r);
        }

        void GalleryClick(Point p)
        {
            if (Rectangle.Inflate(galClose, R(8), R(8)).Contains(p))
            {
                mode = Mode.Pintar;
                Invalidate();
                return;
            }
            int pages = GalleryPages();
            if (pages > 1 && galPrev.Contains(p))
            {
                if (gallery.Page > 0) { gallery.Page--; Invalidate(); }
                return;
            }
            if (pages > 1 && galNext.Contains(p))
            {
                if (gallery.Page < pages - 1) { gallery.Page++; Invalidate(); }
                return;
            }
            int start = gallery.Page * galCells.Length;
            for (int k = 0; k < galCells.Length; k++)
            {
                if (!galCells[k].Contains(p)) continue;
                int idx = start + k;
                if (idx < gallery.Items.Count)
                {
                    LoadItem(gallery.Items[idx]);
                    mode = Mode.Pintar;
                    Invalidate();
                }
                return;
            }
        }

        void ParentClick(Point p)
        {
            if (parentTheme.Contains(p))
            {
                Config.DarkTheme = theme == Theme.Claro;
                Config.Save();
                theme = Config.DarkTheme ? Theme.Escuro : Theme.Claro;
                BackColor = theme.Surface;
                Invalidate();
            }
            else if (parentExit.Contains(p)) ExitApp();
            else if (parentBack.Contains(p) || !parentPanel.Contains(p))
            {
                mode = Mode.Pintar;
                Invalidate();
            }
        }

        void LoadItem(GalleryItem it)
        {
            Cursor = Cursors.WaitCursor;
            current = it;
            surface.ClearUndo();
            surface.Clear();
            surface.SetOverlay(Gallery.MakeOverlay(it, surface.W, surface.H));
            Cursor = Cursors.Default;
        }

        void Save()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Desenhos do Pintinho");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "desenho_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png");
                using (Bitmap b = surface.Compose()) b.Save(file, ImageFormat.Png);
                ShowToast("Salvo!");
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                ShowToast("Não deu pra salvar");
            }
        }

        void ShowToast(string text)
        {
            if (toast != null) Invalidate(ToastRect());
            toast = text;
            toastEnd = Environment.TickCount + 1800;
            Invalidate(ToastRect());
        }

        // ---------- modo captura (screenshots para conferir a UI) ----------

        void RunCapture()
        {
            try
            {
                Directory.CreateDirectory(CaptureDir);
                Size land = new Size(1280, 800), port = new Size(800, 1280);
                DoLayout(land);
                LoadItem(gallery.Items[2]); // bombeiro
                DemoPaint();

                Theme[] themes = { Theme.Claro, Theme.Escuro };
                Size[] sizes = { land, port };
                foreach (Theme t in themes)
                {
                    theme = t;
                    foreach (Size sz in sizes)
                    {
                        DoLayout(sz);
                        string tag = (sz == land ? "paisagem" : "retrato") + "-" + (t == Theme.Claro ? "claro" : "escuro");
                        mode = Mode.Pintar;
                        toast = sz == land ? "Salvo!" : null;
                        Shot("tela-" + tag);
                        toast = null;
                        mode = Mode.Galeria;
                        gallery.Page = 0;
                        Shot("galeria-" + tag);
                    }
                }
                theme = Theme.Claro;
                DoLayout(land);
                mode = Mode.Pais;
                Shot("pais");

                foreach (Drawings.Item d in Drawings.All)
                    using (Bitmap ov = Drawings.Render(d, 1000, 700))
                    using (Bitmap b = new Bitmap(1000, 700))
                    {
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            g.Clear(Color.White);
                            g.DrawImage(ov, new Rectangle(0, 0, 1000, 700));
                        }
                        b.Save(Path.Combine(CaptureDir, "desenho-" + d.Key + ".png"), ImageFormat.Png);
                    }
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                try { File.WriteAllText(Path.Combine(CaptureDir, "erro.txt"), ex.ToString()); } catch { }
            }
            allowClose = true;
            Close();
        }

        Point Design(float x, float y) { return Drawings.ToPixel(x, y, surface.W, surface.H); }

        void DemoPaint()
        {
            surface.Fill(Design(4, 4).X, Design(4, 4).Y, Theme.Palette[10]);        // céu
            surface.Fill(Design(19, 75).X, Design(19, 75).Y, Theme.Palette[4]);     // carroceria
            surface.Fill(Design(128, 92).X, Design(128, 92).Y, Theme.Palette[4]);   // cabine
            surface.Fill(Design(150, 62).X, Design(150, 62).Y, Shapes.Hex(0xb3e5fc)); // vidro
            surface.Fill(Design(46, 77).X, Design(46, 77).Y, Theme.Palette[6]);     // porta
            surface.Fill(Design(45, 97).X, Design(45, 97).Y, Theme.Palette[0]);     // roda
            surface.Fill(Design(155, 97).X, Design(155, 97).Y, Theme.Palette[0]);   // roda
            surface.Stamp(Design(178, 18), 0, Theme.Palette[6], 40 * PaperScale());
            tool = Tool.ArcoIris;
            last = Design(10, 130);
            for (int x = 10; x <= 190; x += 4) StrokeTo(Design(x, 130 + (float)Math.Sin(x / 12.0) * 3));
            tool = Tool.Balde;
        }

        void Shot(string name)
        {
            using (Bitmap b = new Bitmap(view.Width, view.Height))
            {
                using (Graphics g = Graphics.FromImage(b)) PaintAll(g, new Rectangle(Point.Empty, view));
                b.Save(Path.Combine(CaptureDir, name + ".png"), ImageFormat.Png);
            }
        }
    }
}
