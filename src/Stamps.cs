using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pintinho
{
    // Os 12 carimbos, desenhados numa grade 10x10 (iguais aos SVGs do design).
    static class Stamps
    {
        public static readonly string[] Names = {
            "Estrela", "Coração", "Bolinha", "Gota", "Flor", "Lua", "Nuvem", "Sol", "Folha", "Cogumelo", "Carro", "Avião"
        };

        // 0 = Formas, 1 = Natureza, 2 = Veículos
        public static readonly int[] Category = { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2 };
        public static readonly string[] CategoryNames = { "Todos", "Formas", "Natureza", "Veículos" };

        public static int Count { get { return Names.Length; } }

        static readonly Color Miolo = Shapes.Hex(0xfbe7a1);

        // Desenha o carimbo centrado em (cx, cy) com raio "size". lineW em pixels (0 = sem contorno).
        public static void Draw(Graphics g, int kind, float cx, float cy, float size, Color fill, Color line, float lineW)
        {
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = size / 4.5f;
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(k, k);
            g.TranslateTransform(-5, -5);
            float lw = lineW / k;
            Color white = Color.FromArgb(fill.A, 255, 255, 255);

            switch (kind)
            {
                case 0:
                    Shape(g, "M5 1.2 L6.06 3.74 L8.8 3.96 L6.71 5.76 L7.35 8.44 L5 7 L2.65 8.44 L3.29 5.76 L1.2 3.96 L3.94 3.74 Z", fill, line, lw);
                    break;
                case 1:
                    Shape(g, "M5 8.8 C1 6 0.8 3.8 2 2.6 C3.2 1.4 4.6 2 5 3.2 C5.4 2 6.8 1.4 8 2.6 C9.2 3.8 9 6 5 8.8 Z", fill, line, lw);
                    break;
                case 2:
                    Shape(g, "M1.4 5 A3.6 3.6 0 1 1 8.6 5 A3.6 3.6 0 1 1 1.4 5 Z", fill, line, lw);
                    break;
                case 3:
                    Shape(g, "M5 1.2 C6.8 3.6 8 5.1 8 6.5 A3 3 0 0 1 2 6.5 C2 5.1 3.2 3.6 5 1.2 Z", fill, line, lw);
                    break;
                case 4:
                    {
                        // contorno de todas as pétalas antes do preenchimento: as linhas internas somem
                        float[] petals = { 5, 2.7f, 7.2f, 4.3f, 6.4f, 6.9f, 3.6f, 6.9f, 2.8f, 4.3f };
                        if (lw > 0)
                            using (Pen p = new Pen(line, lw * 2))
                                for (int i = 0; i < petals.Length; i += 2) g.DrawEllipse(p, petals[i] - 1.6f, petals[i + 1] - 1.6f, 3.2f, 3.2f);
                        using (SolidBrush b = new SolidBrush(fill))
                            for (int i = 0; i < petals.Length; i += 2) g.FillEllipse(b, petals[i] - 1.6f, petals[i + 1] - 1.6f, 3.2f, 3.2f);
                        Shape(g, "M3.7 5 A1.3 1.3 0 1 1 6.3 5 A1.3 1.3 0 1 1 3.7 5 Z", Color.FromArgb(fill.A, Miolo), line, lw);
                        break;
                    }
                case 5:
                    Shape(g, "M6.5 1.5 A3.8 3.8 0 1 0 8.6 7.6 A3.2 3.2 0 1 1 6.5 1.5 Z", fill, line, lw);
                    break;
                case 6:
                    Shape(g, "M2.6 7.6 A1.8 1.8 0 0 1 2.4 4.1 A2.4 2.4 0 0 1 6.8 3.4 A2 2 0 0 1 8.2 7.6 Z", fill, line, lw);
                    break;
                case 7:
                    using (Pen p = Shapes.RoundPen(fill, 0.7f))
                    {
                        float[] rays = { 5, 0.8f, 5, 1.9f, 5, 8.1f, 5, 9.2f, 0.8f, 5, 1.9f, 5, 8.1f, 5, 9.2f, 5, 2, 2, 2.8f, 2.8f, 7.2f, 7.2f, 8, 8, 8, 2, 7.2f, 2.8f, 2.8f, 7.2f, 2, 8 };
                        for (int i = 0; i < rays.Length; i += 4) g.DrawLine(p, rays[i], rays[i + 1], rays[i + 2], rays[i + 3]);
                    }
                    Shape(g, "M2.8 5 A2.2 2.2 0 1 1 7.2 5 A2.2 2.2 0 1 1 2.8 5 Z", fill, line, lw);
                    break;
                case 8:
                    Shape(g, "M2 8 C2 4 4.5 1.8 8.4 1.6 C8.2 5.5 6 8 2 8 Z", fill, line, lw);
                    if (lw > 0) Shape(g, "M2 8 L6.4 3.6", Color.Empty, line, lw);
                    break;
                case 9:
                    Shape(g, "M3.8 5.4 H6.2 V8.2 Q6.2 8.8 5.6 8.8 H4.4 Q3.8 8.8 3.8 8.2 Z", white, line, lw);
                    Shape(g, "M1.4 5.4 C1.4 2.8 3 1.6 5 1.6 C7 1.6 8.6 2.8 8.6 5.4 Z", fill, line, lw);
                    Shape(g, "M3 3.8 A0.6 0.6 0 1 1 4.2 3.8 A0.6 0.6 0 1 1 3 3.8 Z", white, Color.Empty, 0);
                    Shape(g, "M5.5 3 A0.5 0.5 0 1 1 6.5 3 A0.5 0.5 0 1 1 5.5 3 Z", white, Color.Empty, 0);
                    break;
                case 10:
                    {
                        Color roda = lw > 0 ? line : Shapes.Hex(0x2b2118);
                        Shape(g, "M1 7 V5.6 L2.4 5.2 L3.6 3.4 H6.6 L7.8 5.2 L9 5.6 V7 Z", fill, line, lw);
                        Shape(g, "M4 4 H5 V5.1 H3.3 Z", white, line, lw);
                        Shape(g, "M2 7.2 A1 1 0 1 1 4 7.2 A1 1 0 1 1 2 7.2 Z", Color.FromArgb(fill.A, roda), Color.Empty, 0);
                        Shape(g, "M6 7.2 A1 1 0 1 1 8 7.2 A1 1 0 1 1 6 7.2 Z", Color.FromArgb(fill.A, roda), Color.Empty, 0);
                        break;
                    }
                case 11:
                    Shape(g, "M3.8 5 L5.6 1.4 L6.6 1.4 L6 5 L6.6 8.6 L5.6 8.6 Z", fill, line, lw);
                    Shape(g, "M1.2 5 L1.6 3.2 L2.3 3.2 L2.6 5 L2.3 6.8 L1.6 6.8 Z", fill, line, lw);
                    Shape(g, "M1 5 A4.2 0.95 0 1 1 9.4 5 A4.2 0.95 0 1 1 1 5 Z", fill, line, lw);
                    break;
            }
            g.Restore(st);
        }

        static void Shape(Graphics g, string d, Color fill, Color line, float lw)
        {
            using (GraphicsPath p = SvgPath.Parse(d))
            {
                if (fill != Color.Empty && fill.A > 0)
                    using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (lw > 0 && line != Color.Empty)
                    using (Pen pen = Shapes.RoundPen(line, lw)) g.DrawPath(pen, p);
            }
        }
    }
}
