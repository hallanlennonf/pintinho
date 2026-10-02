using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pintinho
{
    static class Shapes
    {
        public const int StampKinds = 4;

        public static Color Hex(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        public static Color FromHue(float h)
        {
            float s = 0.85f, v = 0.95f;
            float c = v * s, x = c * (1 - Math.Abs((h / 60f) % 2 - 1)), m = v - c;
            float r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb(255, (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
        }

        public static Pen RoundPen(Color c, float w)
        {
            Pen p = new Pen(c, w);
            p.StartCap = LineCap.Round;
            p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;
            return p;
        }

        public static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2f);
            if (rad <= 0.01f) { p.AddRectangle(r); return p; }
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath Star(float cx, float cy, float ro, float ri, int n)
        {
            PointF[] pts = new PointF[n * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / n;
                float r = i % 2 == 0 ? ro : ri;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
            }
            GraphicsPath p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        public static GraphicsPath Heart(float cx, float cy, float s)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddBezier(cx, cy - 0.35f * s, cx - 0.05f * s, cy - 0.95f * s, cx - 1.0f * s, cy - 0.95f * s, cx - 1.0f * s, cy - 0.3f * s);
            p.AddBezier(cx - 1.0f * s, cy - 0.3f * s, cx - 1.0f * s, cy + 0.2f * s, cx - 0.35f * s, cy + 0.55f * s, cx, cy + 1.0f * s);
            p.AddBezier(cx, cy + 1.0f * s, cx + 0.35f * s, cy + 0.55f * s, cx + 1.0f * s, cy + 0.2f * s, cx + 1.0f * s, cy - 0.3f * s);
            p.AddBezier(cx + 1.0f * s, cy - 0.3f * s, cx + 1.0f * s, cy - 0.95f * s, cx + 0.05f * s, cy - 0.95f * s, cx, cy - 0.35f * s);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath Circle(float cx, float cy, float r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
            return p;
        }

        public static GraphicsPath Flower(float cx, float cy, float r)
        {
            GraphicsPath p = new GraphicsPath(FillMode.Winding);
            float pr = r * 0.45f;
            for (int i = 0; i < 5; i++)
            {
                double a = -Math.PI / 2 + i * 2 * Math.PI / 5;
                float px = cx + (float)Math.Cos(a) * r * 0.55f, py = cy + (float)Math.Sin(a) * r * 0.55f;
                p.AddEllipse(px - pr, py - pr, 2 * pr, 2 * pr);
            }
            p.AddEllipse(cx - r * 0.35f, cy - r * 0.35f, r * 0.7f, r * 0.7f);
            return p;
        }

        public static GraphicsPath StampPath(int kind, float cx, float cy, float size)
        {
            switch (kind)
            {
                case 1: return Heart(cx, cy, size * 0.9f);
                case 2: return Circle(cx, cy, size * 0.75f);
                case 3: return Flower(cx, cy, size);
                default: return Star(cx, cy, size, size * 0.45f, 5);
            }
        }
    }
}
