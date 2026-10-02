using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Pintinho
{
    class GalleryItem
    {
        public string Name;
        public Drawings.Item Builtin; // desenho embutido
        public string File;           // imagem da pasta de desenhos extras
        public Bitmap Thumb;
    }

    class Gallery
    {
        static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        public readonly List<GalleryItem> Items = new List<GalleryItem>();
        public int Page;

        public Gallery(Drawings.Item[] builtins, string folder)
        {
            GalleryItem blank = new GalleryItem();
            blank.Name = "Folha em branco";
            Items.Add(blank);
            foreach (Drawings.Item d in builtins)
            {
                GalleryItem it = new GalleryItem();
                it.Name = d.Name;
                it.Builtin = d;
                Items.Add(it);
            }

            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
            if (!Directory.Exists(dir)) return;
            List<string> files = new List<string>();
            foreach (string f in Directory.GetFiles(dir))
                if (Array.IndexOf(Extensions, Path.GetExtension(f).ToLowerInvariant()) >= 0) files.Add(f);
            files.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                GalleryItem it = new GalleryItem();
                it.Name = Path.GetFileNameWithoutExtension(f);
                it.File = f;
                Items.Add(it);
            }
        }

        public static Bitmap MakeOverlay(GalleryItem it, int w, int h)
        {
            if (it == null) return null;
            if (it.Builtin != null) return Drawings.Render(it.Builtin, w, h);
            if (it.File != null) return Drawings.FromFile(it.File, w, h);
            return null;
        }

        // Miniatura sobre papel branco; refeita só quando o tamanho muda (tela girou).
        public Bitmap GetThumb(GalleryItem it, int w, int h)
        {
            if (it.Thumb != null && it.Thumb.Width == w && it.Thumb.Height == h) return it.Thumb;
            if (it.Thumb != null) it.Thumb.Dispose();
            Bitmap t = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(t))
            {
                g.Clear(Theme.Papel);
                if (it.Builtin == null && it.File == null)
                {
                    // folha em branco: moldura tracejada com um "+"
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    float k = Math.Min(w, h) / 140f, cx = w / 2f, cy = h / 2f;
                    using (Pen p = Shapes.RoundPen(Shapes.Hex(0x6e5f4e), Math.Max(1.5f, 4 * k)))
                    {
                        using (Pen dash = (Pen)p.Clone())
                        {
                            dash.DashPattern = new float[] { 2.5f, 2.5f };
                            dash.DashCap = DashCap.Round;
                            using (GraphicsPath r = Shapes.RoundRect(new RectangleF(w * 0.1f, h * 0.09f, w * 0.8f, h * 0.82f), 10 * k))
                                g.DrawPath(dash, r);
                        }
                        g.DrawLine(p, cx, cy - 20 * k, cx, cy + 20 * k);
                        g.DrawLine(p, cx - 20 * k, cy, cx + 20 * k, cy);
                    }
                }
                using (Bitmap ov = MakeOverlay(it, w, h))
                    if (ov != null) g.DrawImage(ov, new Rectangle(0, 0, w, h));
            }
            it.Thumb = t;
            return t;
        }
    }
}
