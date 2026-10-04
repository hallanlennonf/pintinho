using System;
using System.Collections.Generic;
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
    enum Screen { Inicio, Infantil, Aconchego, Jogo, Jogos, Cobra }
    enum Overlay { Nenhum, Galeria, Pais, MaisCores, Carimbos, Misturador }
    enum Tool { Pincel, Spray, ArcoIris, Balde, Carimbo, Borracha, Lapis, Marcador, ContaGotas }
    enum Role { Nenhum, Pintar, Pinca, Arrastar, Cadeado }

    // Um dedo (ou o mouse) encostado na tela. Cada um tem seu papel, então dá para pintar com um
    // dedo enquanto outro troca a cor.
    class Contact
    {
        public int Id;
        public Role Role;
        public Point Screen;
        public PointF Paper;
        public float Hue;
        public int DownAt;
        public Tool Tool;
        public List<PointF> Path;
        public Action<Point> Drag;
    }

    class Btn
    {
        public Rectangle R;
        public readonly string Id, Label;
        public Btn(string id, string label) { Id = id; Label = label; }
    }

    // A janela inteira é desenhada à mão (um controle só = leve no Atom).
    // Medidas em pixels do design (1280x800 paisagem / 800x1280 retrato) multiplicadas por "sc".
    // Cada tela fica num arquivo: HomeScreen, KidsScreen, CozyScreen; o toque em Touch.cs.
    partial class MainForm : Form
    {
        const int LockHoldMs = 3000;

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
        static readonly StringFormat RightFmt = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        static readonly StringFormat WrapFmt = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisWord
        };

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetProp(IntPtr hWnd, string name, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern ushort GlobalAddAtom(string name);

        public string CaptureDir;

        readonly bool kiosk;
        KioskGuard guard;
        bool allowClose;

        Screen screen = Screen.Inicio;
        Overlay overlay = Overlay.Nenhum;
        Theme theme;

        Size view;
        bool portrait;
        float sc = 1f;
        Font labelFont, smallFont, titleFont, toastFont, buttonFont, bigFont, bodyFont, compactFont, sectionFont;

        readonly Dictionary<int, Contact> contacts = new Dictionary<int, Contact>();
        readonly Random rnd = new Random();
        readonly Timer timer = new Timer();
        string toast;
        int toastEnd;
        int startedAt;
        bool checkedUpdates;

        public MainForm(bool kiosk)
        {
            this.kiosk = kiosk;
            Config.Load();
            UpdateTheme();

            Text = "Pintinho";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            KeyPreview = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
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

        void UpdateTheme()
        {
            if (screen == Screen.Jogo || screen == Screen.Jogos || screen == Screen.Cobra) theme = Theme.Escuro; // joguinhos: sempre noturno
            else if (screen == Screen.Aconchego) theme = Config.DarkTheme ? Theme.AconchegoEscuro : Theme.AconchegoClaro;
            else theme = Config.DarkTheme ? Theme.Escuro : Theme.Claro;
            BackColor = theme.Surface;
        }

        void GoTo(Screen s)
        {
            foreach (Contact c in contacts.Values) c.Role = Role.Nenhum;
            StopLoop();
            StopSnakeClock();
            screen = s;
            overlay = Overlay.Nenhum;
            UpdateTheme();
            if (s == Screen.Infantil) EnterKids();
            else if (s == Screen.Aconchego) EnterCozy();
            else if (s == Screen.Jogo) EnterGame();
            else if (s == Screen.Cobra) { EnterSnake(); LayoutSnake(); }
            Invalidate();
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
            if (kiosk) Bounds = Screen_Bounds();
            startedAt = Environment.TickCount;
            kidsGallery = new Gallery(Drawings.All, "desenhos");
            cozyGallery = new Gallery(Drawings.Cozy, "desenhos-aconchego");
            kidsCurrent = kidsGallery.Items[0];
            cozyCurrent = cozyGallery.Items[0];
            DoLayout(ClientSize);
            Updater.Changed += OnUpdaterChanged;
            if (kiosk)
            {
                guard = new KioskGuard();
                guard.ExitRequested += delegate { BeginInvoke(new MethodInvoker(ExitApp)); };
                SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            }
            timer.Start();
        }

        static Rectangle Screen_Bounds() { return System.Windows.Forms.Screen.PrimaryScreen.Bounds; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (CaptureDir != null) BeginInvoke(new MethodInvoker(RunCapture));
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            // tablet girou ou resolução mudou: ocupa a tela inteira de novo
            BeginInvoke(new MethodInvoker(delegate { Bounds = Screen_Bounds(); }));
        }

        void OnUpdaterChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated) Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (kidsGallery == null || CaptureDir != null || ClientSize.Width < 100 || ClientSize.Height < 100) return;
            if (ClientSize == view) return;
            foreach (Contact c in contacts.Values) c.Role = Role.Nenhum;
            DoLayout(ClientSize);
            Invalidate();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (screen == Screen.Jogo) PauseGame();
            if (screen == Screen.Cobra) PauseSnake();
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
            StopLoop();
            StopSnakeClock();
            Updater.Changed -= OnUpdaterChanged;
            if (kiosk) SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            if (guard != null) guard.Dispose();
            if (kidsSurface != null) kidsSurface.Dispose();
            if (cozySurface != null) cozySurface.Dispose();
            base.OnFormClosed(e);
        }

        void ExitApp()
        {
            allowClose = true;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (GameKey(keyData)) return true;
            if (SnakeKey(keyData)) return true;
            if (keyData == (Keys.Control | Keys.Z)) { DoUndo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { DoRedo(); return true; }
            if (keyData == (Keys.Control | Keys.Shift | Keys.Q)) { ExitApp(); return true; }
            if (!kiosk && keyData == Keys.Escape) { ExitApp(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void DoUndo()
        {
            if (overlay != Overlay.Nenhum) return;
            if (screen == Screen.Infantil && kidsSurface.Undo()) Invalidate(paperRect);
            else if (screen == Screen.Aconchego && cozySurface != null && cozySurface.Undo()) InvalidateCozyCanvas();
        }

        void DoRedo()
        {
            if (overlay != Overlay.Nenhum) return;
            if (screen == Screen.Infantil && kidsSurface.Redo()) Invalidate(paperRect);
            else if (screen == Screen.Aconchego && cozySurface != null && cozySurface.Redo()) InvalidateCozyCanvas();
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
            portrait = sz.Height > sz.Width;
            sc = portrait ? Math.Min(sz.Width / 800f, sz.Height / 1280f) : Math.Min(sz.Width / 1280f, sz.Height / 800f);
            MakeFonts();
            LayoutHome();
            LayoutKids();
            LayoutCozy();
            LayoutGame();
            LayoutHub();
            LayoutSnake();
            LayoutGallery();
            LayoutParent();
        }

        Font F(float px, FontStyle style)
        {
            return new Font("Segoe UI", Math.Max(6f, px * sc), style, GraphicsUnit.Pixel);
        }

        void MakeFonts()
        {
            Font[] old = { labelFont, smallFont, titleFont, toastFont, buttonFont, bigFont, bodyFont, compactFont, sectionFont };
            foreach (Font f in old) if (f != null) f.Dispose();
            labelFont = F(14, FontStyle.Bold);
            smallFont = F(12, FontStyle.Bold);
            titleFont = F(38, FontStyle.Bold);
            toastFont = F(26, FontStyle.Bold);
            buttonFont = F(20, FontStyle.Bold);
            bigFont = F(68, FontStyle.Bold);
            bodyFont = F(17, FontStyle.Regular);
            compactFont = F(11, FontStyle.Bold);
            sectionFont = F(13, FontStyle.Bold);
        }

        // ---------- desenho ----------

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintAll(e.Graphics, e.ClipRectangle);
        }

        void PaintAll(Graphics g, Rectangle clip)
        {
            if (kidsSurface == null) { g.Clear(theme.Surface); return; }
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (overlay == Overlay.Galeria) { DrawGallery(g, clip); return; }

            using (SolidBrush bg = new SolidBrush(theme.Surface)) g.FillRectangle(bg, clip);
            switch (screen)
            {
                case Screen.Inicio: PaintHome(g, clip); break;
                case Screen.Infantil: PaintKids(g, clip); break;
                case Screen.Aconchego: PaintCozy(g, clip); break;
                case Screen.Jogo: PaintGame(g, clip); break;
                case Screen.Jogos: PaintHub(g); break;
                case Screen.Cobra: PaintSnake(g, clip); break;
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (toast != null) DrawToast(g);
            switch (overlay)
            {
                case Overlay.Pais: DrawParent(g); break;
                case Overlay.MaisCores: DrawKidsColors(g); break;
                case Overlay.Carimbos:
                    if (screen == Screen.Infantil) DrawKidsStamps(g);
                    else DrawCozyStamps(g);
                    break;
                case Overlay.Misturador: DrawMixer(g); break;
            }
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

        void DashedBox(Graphics g, Rectangle r, float radius, Color fill, Color border, float bw)
        {
            RectangleF rf = new RectangleF(r.X + bw / 2f, r.Y + bw / 2f, r.Width - bw, r.Height - bw);
            using (GraphicsPath p = Shapes.RoundRect(rf, radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                using (Pen pen = new Pen(border, bw))
                {
                    pen.DashPattern = new float[] { 3f, 2.5f };
                    g.DrawPath(pen, p);
                }
            }
        }

        static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r, StringFormat fmt)
        {
            using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, r, fmt);
        }

        void DrawCloseCircle(Graphics g, Rectangle r)
        {
            using (SolidBrush b = new SolidBrush(theme.Bombeiro)) g.FillEllipse(b, r);
            using (Pen p = Shapes.RoundPen(Color.White, r.Width * 0.072f))
            {
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, h = r.Width * 0.135f;
                g.DrawLine(p, cx - h, cy - h, cx + h, cy + h);
                g.DrawLine(p, cx + h, cy - h, cx - h, cy + h);
            }
        }

        void DrawLockButton(Graphics g, Rectangle r, float radius)
        {
            RoundBox(g, r, radius, theme.Surface, theme.Borda, Math.Max(1.5f, 2 * sc));
            float icon = r.Width * 0.56f;
            Icons.Draw(g, "cadeado", new RectangleF(r.X + (r.Width - icon) / 2f, r.Y + (r.Height - icon) / 2f, icon, icon), theme.Taxi, theme.Ink, theme, 0);
            float progress = LockProgress();
            if (progress > 0)
                using (Pen p = new Pen(theme.Trator, 5 * sc))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawArc(p, Rectangle.Inflate(r, R(4), R(4)), -90, 360 * progress);
                }
        }

        Rectangle LockRect()
        {
            switch (screen)
            {
                case Screen.Infantil: return kidsLock;
                case Screen.Aconchego: return cozyLock;
                case Screen.Jogo: return gstate == GState.Menu ? homeLock : Rectangle.Empty;
                case Screen.Jogos: return homeLock;
                case Screen.Cobra: return Rectangle.Empty;
                default: return homeLock;
            }
        }

        float LockProgress()
        {
            float best = 0;
            foreach (Contact c in contacts.Values)
                if (c.Role == Role.Cadeado)
                    best = Math.Max(best, Math.Min(1f, (Environment.TickCount - c.DownAt) / (float)LockHoldMs));
            return best;
        }

        // ---------- aviso rápido ("Salvo!") ----------

        Rectangle ToastRect()
        {
            Size ts = TextRenderer.MeasureText(toast ?? "", toastFont);
            int w = ts.Width + R(48), h = R(56);
            Rectangle area = screen == Screen.Infantil ? paperRect : screen == Screen.Aconchego ? cozyCanvas : new Rectangle(Point.Empty, view);
            return new Rectangle(area.X + (area.Width - w) / 2, area.Y + R(16), w, h);
        }

        void DrawToast(Graphics g)
        {
            Rectangle r = ToastRect();
            RoundBox(g, r, r.Height / 2f, theme.Taxi, theme.Cone, 2 * sc);
            DrawText(g, toast, toastFont, theme.OnTaxi, r, CenterFmt);
        }

        void ShowToast(string text)
        {
            if (toast != null) Invalidate(ToastRect());
            toast = text;
            toastEnd = Environment.TickCount + 1800;
            Invalidate(ToastRect());
        }

        // ---------- relógio: cadeado, spray, avisos, atualização ----------

        void OnTick(object sender, EventArgs e)
        {
            List<Contact> list = new List<Contact>(contacts.Values);
            foreach (Contact c in list)
            {
                if (c.Role == Role.Cadeado)
                {
                    Invalidate(Rectangle.Inflate(LockRect(), R(10), R(10)));
                    if (Environment.TickCount - c.DownAt >= LockHoldMs)
                    {
                        c.Role = Role.Nenhum;
                        overlay = Overlay.Pais;
                        Invalidate();
                    }
                }
                else if (c.Role == Role.Pintar && c.Tool == Tool.Spray)
                {
                    if (screen == Screen.Infantil) KidsStroke(c, c.Screen);
                    else if (screen == Screen.Aconchego) CozyStroke(c, c.Screen);
                }
            }
            if (toast != null && Environment.TickCount - toastEnd >= 0)
            {
                Rectangle r = ToastRect();
                toast = null;
                Invalidate(r);
            }
            if (hintUntil != 0 && Environment.TickCount - hintUntil >= 0)
            {
                hintUntil = 0;
                InvalidateCozyCanvas();
            }
            if (!checkedUpdates && CaptureDir == null && Environment.TickCount - startedAt > 4000)
            {
                checkedUpdates = true;
                Updater.Check();
            }
        }

        // ---------- salvar ----------

        void Save(Surface s, string prefix)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Desenhos do Pintinho");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, prefix + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png");
                using (Bitmap b = s.Compose()) b.Save(file, ImageFormat.Png);
                ShowToast("Salvo!");
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                ShowToast("Não deu pra salvar");
            }
        }
    }
}
