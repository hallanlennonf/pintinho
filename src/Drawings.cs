using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Pintinho
{
    // Desenhos de colorir. Cada veículo é um roteiro no espaço 200x140 (o mesmo das miniaturas do
    // design), um comando por linha. Comandos com "S" na frente são formas sólidas: apagam o que
    // está atrás antes de desenhar o contorno, então a ordem é de trás para frente.
    //   P d      contorno de path SVG          S d      path sólido
    //   R x y w h [rx]  retângulo              SR ...   retângulo sólido
    //   C cx cy r       círculo                SC ...   círculo sólido
    //   G x,y x,y ...   polígono               SG ...   polígono sólido
    //   SE cx cy rx ry rot   elipse sólida girada
    //   L x1 y1 x2 y2   linha                  W k      multiplica a espessura da caneta
    static class Drawings
    {
        public const float SpaceW = 200f, SpaceH = 140f;

        public class Item
        {
            public string Key, Name, Script;
            public Item(string key, string name, string script) { Key = key; Name = name; Script = script; }
        }

        const string CarBody = "S M20 98 V76 L42 70 L66 44 H130 L156 70 L182 76 V98 Z\nP M73 52 L58 69 H96 V52 Z\nP M104 52 H126 L144 69 H104 Z\n";

        public static readonly Item[] All = {
            new Item("carro", "Carro",
                "L 10 118 190 118\nC 178 20 11\n" + CarBody +
                "SC 55 100 16\nC 55 100 6\nSC 148 100 16\nC 148 100 6"),
            new Item("bombeiro", "Bombeiro",
                "SR 15 50 110 55 4\nR 24 60 44 34 3\nR 74 60 44 34 3\n" +
                "S M125 105 V40 H160 Q175 45 182 75 V105 Z\nP M132 47 H158 Q168 55 172 75 H132 Z\n" +
                "SR 22 34 95 11 2\nL 42 34 42 45\nL 62 34 62 45\nL 82 34 82 45\nL 102 34 102 45\n" +
                "SR 145 29 14 11 2\nSC 45 108 15\nC 45 108 6\nSC 155 108 15\nC 155 108 6"),
            new Item("trator", "Trator",
                "L 160 56 160 30\nSR 65 22 55 56 4\nR 76 32 33 24 3\nSR 120 56 58 32 4\n" +
                "SC 85 98 32\nC 85 98 11\nSC 160 108 18\nC 160 108 6"),
            new Item("onibus", "Ônibus",
                "SR 15 32 170 70 12\nR 26 44 25 22 3\nR 59 44 25 22 3\nR 92 44 25 22 3\nR 125 44 25 22 3\n" +
                "R 158 44 18 48 3\nL 15 78 158 78\nSC 50 104 14\nC 50 104 5\nSC 145 104 14\nC 145 104 5"),
            new Item("trem", "Trem",
                "L 10 124 190 124\nSR 50 26 20 30\nSR 30 54 92 46 8\nSR 120 28 56 72 4\nR 132 40 32 24 3\n" +
                "SR 114 20 68 10 3\nSC 52 108 14\nC 52 108 5\nSC 94 108 14\nC 94 108 5\nSC 150 108 14\nC 150 108 5"),
            new Item("aviao", "Avião",
                "SG 30,64 16,30 38,30 58,62\n" +
                "S M22 76 Q22 62 46 62 H160 Q184 64 190 76 Q184 90 160 90 H46 Q22 90 22 76 Z\n" +
                "SG 84,78 112,118 134,118 120,78\n" +
                "C 70 72 5\nC 88 72 5\nC 106 72 5\nC 124 72 5\nC 142 72 5\nP M162 68 Q176 68 183 74"),
            new Item("heli", "Helicóptero",
                "SR 40 21 132 6 3\nL 106 27 106 40\nS M64 60 L16 52 L16 70 L64 78\nSC 16 61 9\n" +
                "S M62 68 Q62 40 96 40 H120 Q152 40 152 72 Q152 96 122 96 H86 Q62 96 62 68 Z\n" +
                "P M122 48 Q142 50 145 72 H122 Z\nL 88 96 82 112\nL 126 96 132 112\nL 66 112 150 112"),
            new Item("foguete", "Foguete",
                "S M88 105 L100 132 L112 105\nS M80 80 L58 112 H80 Z\nS M120 80 L142 112 H120 Z\n" +
                "S M80 108 V55 Q80 25 100 8 Q120 25 120 55 V108 Z\nC 100 58 11\nL 83 38 117 38\n" +
                "G 30,30 34,40 44,41 36,47 39,57 30,51 21,57 24,47 16,41 26,40\n" +
                "G 168,62 171,70 179,71 173,76 175,84 168,79 161,84 163,76 157,71 165,70\nC 165 20 6\nC 40 100 4"),
            new Item("barco", "Barco",
                "C 30 28 12\nL 100 86 100 18\nS M104 24 V80 H152 Z\nS M96 34 V80 H54 Z\n" +
                "S M28 86 H172 L150 112 H50 Z\nC 78 98 5\nC 100 98 5\nC 122 98 5\n" +
                "P M8 124 Q28 112 48 124 T88 124 T128 124 T168 124 T200 124"),
            new Item("moto", "Moto",
                "SC 45 96 24\nC 45 96 7\nSC 155 96 24\nC 155 96 7\nP M45 96 L78 64 H124 L155 96\n" +
                "S M76 64 Q84 46 112 50 L122 64 Z\nL 122 64 136 40\nL 128 40 146 40\nP M92 64 L100 88 H120"),
            new Item("betoneira", "Betoneira",
                "SR 14 88 124 12 3\nSE 74 58 56 30 -12\nP M46 36 Q60 60 52 84\nP M78 30 Q92 56 84 80\nP M108 26 Q120 50 114 74\n" +
                "S M136 100 V45 H166 Q182 55 186 80 V100 Z\nP M143 52 H162 Q172 60 176 75 H143 Z\n" +
                "SC 44 106 14\nC 44 106 5\nSC 104 106 14\nC 104 106 5\nSC 164 106 14\nC 164 106 5"),
            new Item("ambulancia", "Ambulância",
                "SR 68 22 22 12 3\nS M15 102 V34 H140 L164 60 L186 66 V102 Z\nP M142 40 L160 60 H142 Z\n" +
                "P M64 48 H78 V60 H90 V74 H78 V86 H64 V74 H52 V60 H64 Z\nSC 50 104 15\nC 50 104 6\nSC 152 104 15\nC 152 104 6"),
            new Item("policia", "Polícia",
                "SR 82 32 36 12 4\nL 100 32 100 44\n" + CarBody +
                "L 20 84 182 84\nSC 55 100 16\nC 55 100 6\nSC 148 100 16\nC 148 100 6"),
            new Item("escavadeira", "Escavadeira",
                "W 2.5\nP M92 62 L148 24 L180 62\nW 1\nS M172 60 L194 60 L188 88 L164 82 Z\n" +
                "SR 38 46 58 48 4\nR 48 54 26 20 3\nSR 18 94 114 28 14\nC 34 108 7\nC 60 108 7\nC 88 108 7\nC 116 108 7")
        };

        static readonly Regex Number = new Regex(@"-?[0-9]*\.?[0-9]+");

        static float[] Nums(string s)
        {
            MatchCollection m = Number.Matches(s);
            float[] r = new float[m.Count];
            for (int i = 0; i < r.Length; i++) r[i] = float.Parse(m[i].Value, CultureInfo.InvariantCulture);
            return r;
        }

        static GraphicsPath Shape(string op, float[] n)
        {
            GraphicsPath p = new GraphicsPath();
            switch (op)
            {
                case "R":
                    using (GraphicsPath rr = Shapes.RoundRect(new RectangleF(n[0], n[1], n[2], n[3]), n.Length > 4 ? n[4] : 0))
                        p.AddPath(rr, false);
                    break;
                case "C":
                    p.AddEllipse(n[0] - n[2], n[1] - n[2], 2 * n[2], 2 * n[2]);
                    break;
                case "G":
                    {
                        PointF[] pts = new PointF[n.Length / 2];
                        for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(n[2 * i], n[2 * i + 1]);
                        p.AddPolygon(pts);
                        break;
                    }
                case "E":
                    p.AddEllipse(n[0] - n[2], n[1] - n[3], 2 * n[2], 2 * n[3]);
                    if (n.Length > 4)
                        using (Matrix m = new Matrix())
                        {
                            m.RotateAt(n[4], new PointF(n[0], n[1]));
                            p.Transform(m);
                        }
                    break;
            }
            return p;
        }

        // Apaga o que estiver atrás da forma e desenha o contorno.
        static void Solid(Graphics g, Pen pen, GraphicsPath path)
        {
            CompositingMode old = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceCopy;
            using (SolidBrush clear = new SolidBrush(Color.Transparent)) g.FillPath(clear, path);
            g.CompositingMode = old;
            g.DrawPath(pen, path);
        }

        public static void Run(string script, Graphics g, Pen pen)
        {
            float baseWidth = pen.Width;
            foreach (string raw in script.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                int sp = line.IndexOf(' ');
                string op = sp < 0 ? line : line.Substring(0, sp);
                string arg = sp < 0 ? "" : line.Substring(sp + 1);
                bool solid = op.Length > 1 && op[0] == 'S';
                string kind = solid ? op.Substring(1) : op;

                if (op == "W") { pen.Width = baseWidth * Nums(arg)[0]; continue; }
                if (op == "L")
                {
                    float[] n = Nums(arg);
                    g.DrawLine(pen, n[0], n[1], n[2], n[3]);
                    continue;
                }

                GraphicsPath path = (op == "P" || op == "S") ? SvgPath.Parse(arg) : Shape(kind, Nums(arg));
                using (path)
                {
                    if (op == "S" || solid) Solid(g, pen, path);
                    else g.DrawPath(pen, path);
                }
            }
            pen.Width = baseWidth;
        }

        // Retângulo (no bitmap w x h) onde o espaço 200x140 é desenhado, e sua escala.
        public static float Fit(int w, int h, out float ox, out float oy)
        {
            float s = Math.Min(w / SpaceW, h / SpaceH) * 0.94f;
            ox = (w - SpaceW * s) / 2f;
            oy = (h - SpaceH * s) / 2f;
            return s;
        }

        public static Point ToPixel(float x, float y, int w, int h)
        {
            float ox, oy;
            float s = Fit(w, h, out ox, out oy);
            return new Point((int)(ox + x * s), (int)(oy + y * s));
        }

        public static Bitmap Render(Item item, int w, int h)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float ox, oy;
                float s = Fit(w, h, out ox, out oy);
                g.TranslateTransform(ox, oy);
                g.ScaleTransform(s, s);
                // ~1.6 unidades na folha grande (~7px); nas miniaturas, nunca mais fino que 2.6px.
                using (Pen p = Shapes.RoundPen(Theme.Linha, Math.Max(1.6f, 2.6f / s)))
                    Run(item.Script, g, p);
            }
            return b;
        }

        // Imagem da pasta "desenhos": linhas escuras viram contorno, o claro fica transparente.
        public static Bitmap FromFile(string file, int w, int h)
        {
            try
            {
                Bitmap tmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Image img = Image.FromFile(file))
                using (Graphics g = Graphics.FromImage(tmp))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float s = Math.Min(w * 0.96f / img.Width, h * 0.96f / img.Height);
                    float dw = img.Width * s, dh = img.Height * s;
                    g.DrawImage(img, (w - dw) / 2f, (h - dh) / 2f, dw, dh);
                }
                BitmapData d = tmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int[] a = new int[w * h];
                Marshal.Copy(d.Scan0, a, 0, a.Length);
                for (int i = 0; i < a.Length; i++)
                {
                    int c = a[i];
                    int lum = (((c >> 16) & 255) * 299 + ((c >> 8) & 255) * 587 + (c & 255) * 114) / 1000;
                    int alpha = Math.Max(0, Math.Min(255, (225 - lum) * 255 / 150));
                    a[i] = alpha << 24;
                }
                Marshal.Copy(a, 0, d.Scan0, a.Length);
                tmp.UnlockBits(d);

                Bitmap res = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(res))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(tmp, new Rectangle(0, 0, w, h));
                }
                tmp.Dispose();
                return res;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                return null;
            }
        }
    }
}
