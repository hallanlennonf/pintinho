using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Pintinho
{
    class GalleryItem
    {
        public string Name;
        public Drawings.Item Builtin; // veículo embutido
        public string File;           // imagem da pasta "desenhos"
        public Bitmap Thumb;
    }

    class Gallery
    {
        static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        public readonly List<GalleryItem> Items = new List<GalleryItem>();
        public int Page;

        public Gallery()
        {
            GalleryItem blank = new GalleryItem();
            blank.Name = "Folha em branco";
            Items.Add(blank);
            foreach (Drawings.Item d in Drawings.All)
            {
                GalleryItem it = new GalleryItem();
                it.Name = d.Name;
                it.Builtin = d;
                Items.Add(it);
            }

            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desenhos");
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
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    float k = w / 200f;
                    using (Pen p = Shapes.RoundPen(Shapes.Hex(0x6e5f4e), Math.Max(1.5f, 4 * k)))
                    {
                        using (Pen dash = (Pen)p.Clone())
                        {
                            dash.DashPattern = new float[] { 2.5f, 2.5f };
                            dash.DashCap = System.Drawing.Drawing2D.DashCap.Round;
                            using (System.Drawing.Drawing2D.GraphicsPath r = Shapes.RoundRect(new RectangleF(20 * k, 12 * k, 160 * k, 116 * k), 10 * k))
                                g.DrawPath(dash, r);
                        }
                        g.DrawLine(p, 100 * k, 50 * k, 100 * k, 90 * k);
                        g.DrawLine(p, 80 * k, 70 * k, 120 * k, 70 * k);
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
