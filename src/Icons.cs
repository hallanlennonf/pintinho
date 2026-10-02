using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pintinho
{
    // Ícones dos botões, numa grade 10x10 (iguais aos SVGs do design).
    static class Icons
    {
        static readonly Color Cinza = Shapes.Hex(0x9aa3ad);
        static readonly Color Madeira = Shapes.Hex(0x8d5524);
        static readonly Color Escuro = Shapes.Hex(0x2b2118);
        static readonly Color[] Arco = { Shapes.Hex(0xe53935), Shapes.Hex(0xfb8c00), Shapes.Hex(0xfdd835), Shapes.Hex(0x43a047), Shapes.Hex(0x1e88e5) };

        // cur = cor escolhida; fg = cor do contorno (ink do tema ou on-taxi quando selecionado)
        public static void Draw(Graphics g, string id, RectangleF r, Color cur, Color fg, Theme t, int stampKind)
        {
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X, r.Y);
            g.ScaleTransform(r.Width / 10f, r.Height / 10f);
            switch (id)
            {
                case "pincel":
                    Stroke(g, "M0.8 8.8 C2.5 7 4.5 10.2 7 8.4", cur, 0.9f);
                    Stroke(g, "M8.8 1.2 L5.6 4.4", Madeira, 1.3f);
                    using (Pen p = new Pen(Cinza, 1.7f)) g.DrawLine(p, 5.6f, 4.4f, 4.6f, 5.4f);
                    FillStroke(g, "M4 4.8 L5.2 6 L3 7.6 L2.4 7.2 Z", cur, fg, 0.25f);
                    break;
                case "spray":
                    FillStroke(g, "M4.6 3.6 H8.2 V9.4 H4.6 Z", Cinza, fg, 0.3f);
                    Fill(g, "M5.4 2.4 H7.4 V3.6 H5.4 Z", fg);
                    float[] dots = { 1.5f, 1.6f, 2.6f, 2.4f, 1.2f, 3.3f, 3.1f, 1.2f, 2.3f, 3.9f, 3.7f, 2.9f, 0.8f, 2.3f };
                    for (int i = 0; i < dots.Length; i += 2) Ellipse(g, dots[i], dots[i + 1], 0.38f, 0.38f, cur, fg, 0.12f);
                    break;
                case "arco":
                    float[] radii = { 4.4f, 3.75f, 3.1f, 2.45f, 1.8f };
                    for (int i = 0; i < radii.Length; i++)
                        using (Pen p = new Pen(Arco[i], 0.62f))
                            g.DrawArc(p, 5 - radii[i], 8.4f - radii[i], 2 * radii[i], 2 * radii[i], 180, 180);
                    break;
                case "balde":
                    using (Pen p = new Pen(fg, 0.35f)) g.DrawArc(p, 1.6f, 1.5f, 5f, 5f, 180, 180);
                    FillStroke(g, "M1.6 4 L6.6 4 L5.9 9.2 L2.3 9.2 Z", Cinza, fg, 0.3f);
                    Ellipse(g, 4.1f, 4f, 2.4f, 0.6f, cur, fg, 0.2f);
                    FillStroke(g, "M6.4 4 L7.4 4.2 L8.8 7.5 L8 7.6 Z", cur, fg, 0.15f);
                    Ellipse(g, 8.2f, 9f, 1.6f, 0.55f, cur, fg, 0.15f);
                    break;
                case "carimbo":
                    using (GraphicsPath p = Shapes.StampPath(stampKind, 5f, 5.1f, 4f))
                    using (SolidBrush b = new SolidBrush(cur))
                    using (Pen pen = Shapes.RoundPen(fg, 0.3f))
                    {
                        g.FillPath(b, p);
                        g.DrawPath(pen, p);
                    }
                    break;
                case "borracha":
                    g.TranslateTransform(5, 5);
                    g.RotateTransform(-35);
                    g.TranslateTransform(-5, -5);
                    Fill(g, "M1 3.2 H9 V6.8 H1 Z", Shapes.Hex(0xf48fb1));
                    Fill(g, "M1 3.2 H4 V6.8 H1 Z", Shapes.Hex(0x64b5f6));
                    Stroke(g, "M1 3.2 H9 V6.8 H1 Z", fg, 0.3f);
                    break;
                case "desenhos":
                    FillStroke(g, "M1 1 H4.6 V4.6 H1 Z", Shapes.Hex(0xffc727), fg, 0.3f);
                    FillStroke(g, "M5.4 1 H9 V4.6 H5.4 Z", Shapes.Hex(0x3fa7e0), fg, 0.3f);
                    FillStroke(g, "M1 5.4 H4.6 V9 H1 Z", Shapes.Hex(0x3e9b4f), fg, 0.3f);
                    FillStroke(g, "M5.4 5.4 H9 V9 H5.4 Z", Shapes.Hex(0xd93a26), fg, 0.3f);
                    break;
                case "desfazer":
                    Stroke(g, "M8.5 7.8 C8.5 4.6 6.5 3.4 4.2 3.4 L2 3.4", t.Ceu, 1.1f);
                    Stroke(g, "M3.6 1.6 L1.8 3.4 L3.6 5.2", t.Ceu, 1.1f);
                    break;
                case "limpar":
                    FillStroke(g, "M2 1 L6.5 1 L8 2.5 L8 9 L2 9 Z", Color.White, Escuro, 0.35f);
                    Stroke(g, "M6.5 1 L6.5 2.5 L8 2.5", Escuro, 0.3f);
                    FillStroke(g, "M7.6 5.4 L8.1 7.1 L9.8 7.6 L8.1 8.1 L7.6 9.8 L7.1 8.1 L5.4 7.6 L7.1 7.1 Z", Shapes.Hex(0xffc727), Escuro, 0.25f);
                    break;
                case "salvar":
                    Stroke(g, "M5 1.2 L5 6.2", t.Trator, 1.1f);
                    Stroke(g, "M3 4.4 L5 6.4 L7 4.4", t.Trator, 1.1f);
                    Stroke(g, "M1.5 6 L1.5 8.8 L8.5 8.8 L8.5 6", fg, 0.9f);
                    break;
                case "cadeado":
                    using (Pen p = new Pen(t.Muted, 0.9f))
                    {
                        g.DrawLine(p, 3.2f, 5f, 3.2f, 3.6f);
                        g.DrawArc(p, 3.2f, 1.8f, 3.6f, 3.6f, 180, 180);
                        g.DrawLine(p, 6.8f, 3.6f, 6.8f, 5f);
                    }
                    FillStroke(g, "M2.4 4.8 H7.6 V9 H2.4 Z", Shapes.Hex(0xffc727), Escuro, 0.3f);
                    Ellipse(g, 5f, 6.7f, 0.55f, 0.55f, Escuro, Escuro, 0f);
                    break;
            }
            g.Restore(st);
        }

        static void Fill(Graphics g, string d, Color c)
        {
            using (GraphicsPath p = SvgPath.Parse(d))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        static void Stroke(Graphics g, string d, Color c, float w)
        {
            using (GraphicsPath p = SvgPath.Parse(d))
            using (Pen pen = Shapes.RoundPen(c, w))
                g.DrawPath(pen, p);
        }

        static void FillStroke(Graphics g, string d, Color fill, Color stroke, float w)
        {
            Fill(g, d, fill);
            Stroke(g, d, stroke, w);
        }

        static void Ellipse(Graphics g, float cx, float cy, float rx, float ry, Color fill, Color stroke, float w)
        {
            using (SolidBrush b = new SolidBrush(fill)) g.FillEllipse(b, cx - rx, cy - ry, 2 * rx, 2 * ry);
            if (w > 0)
                using (Pen p = new Pen(stroke, w)) g.DrawEllipse(p, cx - rx, cy - ry, 2 * rx, 2 * ry);
        }
    }
}
