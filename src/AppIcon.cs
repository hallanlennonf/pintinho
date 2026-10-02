using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Pintinho
{
    // Gera o ícone do app (pintinho amarelo com um tufo de tinta) como .ico com várias resoluções.
    // Uso: Pintinho.exe --icone assets\pintinho.ico
    static class AppIcon
    {
        static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

        public static Bitmap Render(int s)
        {
            Bitmap b = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.ScaleTransform(s / 100f, s / 100f);
                using (GraphicsPath bg = Shapes.RoundRect(new RectangleF(2, 2, 96, 96), 22))
                using (SolidBrush br = new SolidBrush(Shapes.Hex(0xe8680c)))
                    g.FillPath(br, bg);

                Color ink = Shapes.Hex(0x2b2118);
                float lw = s < 32 ? 5f : 3.5f;
                // tufo de tinta (três pinceladas coloridas)
                using (Pen p = Shapes.RoundPen(Shapes.Hex(0x4fc3f7), 7)) g.DrawLine(p, 44, 22, 38, 12);
                using (Pen p = Shapes.RoundPen(Shapes.Hex(0xe53935), 7)) g.DrawLine(p, 52, 21, 53, 10);
                using (Pen p = Shapes.RoundPen(Shapes.Hex(0x9ccc65), 7)) g.DrawLine(p, 60, 23, 67, 14);
                // corpo/cabeça
                using (SolidBrush br = new SolidBrush(Shapes.Hex(0xffc727))) g.FillEllipse(br, 18, 22, 66, 66);
                using (Pen p = new Pen(ink, lw)) g.DrawEllipse(p, 18, 22, 66, 66);
                // asa
                using (SolidBrush br = new SolidBrush(Shapes.Hex(0xffb300))) g.FillEllipse(br, 24, 54, 22, 16);
                // olho e bochecha
                using (SolidBrush br = new SolidBrush(ink)) g.FillEllipse(br, 55, 42, 9, 11);
                using (SolidBrush br = new SolidBrush(Color.White)) g.FillEllipse(br, 59, 44, 3, 3);
                using (SolidBrush br = new SolidBrush(Shapes.Hex(0xf48fb1))) g.FillEllipse(br, 60, 57, 10, 6);
                // bico
                PointF[] beak = { new PointF(70, 48), new PointF(88, 52), new PointF(70, 57) };
                using (SolidBrush br = new SolidBrush(Shapes.Hex(0xfb8c00))) g.FillPolygon(br, beak);
                using (Pen p = new Pen(ink, lw * 0.7f)) { p.LineJoin = LineJoin.Round; g.DrawPolygon(p, beak); }
            }
            return b;
        }

        public static void Save(string path)
        {
            List<byte[]> pngs = new List<byte[]>();
            foreach (int s in Sizes)
                using (Bitmap b = Render(s))
                using (MemoryStream ms = new MemoryStream())
                {
                    b.Save(ms, ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }

            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(dir);
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0);
                w.Write((short)1);
                w.Write((short)Sizes.Length);
                int offset = 6 + 16 * Sizes.Length;
                for (int i = 0; i < Sizes.Length; i++)
                {
                    int s = Sizes[i];
                    w.Write((byte)(s >= 256 ? 0 : s));
                    w.Write((byte)(s >= 256 ? 0 : s));
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((short)1);
                    w.Write((short)32);
                    w.Write(pngs[i].Length);
                    w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] png in pngs) w.Write(png);
            }
            using (Bitmap b = Render(256)) b.Save(Path.ChangeExtension(path, ".png"), ImageFormat.Png);
        }
    }
}
