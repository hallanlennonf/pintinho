using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Pintinho
{
    // Lê o atributo "d" de um <path> SVG (M L H V C Q T Z, absolutos e relativos).
    static class SvgPath
    {
        static readonly Regex Tokens = new Regex(@"[A-Za-z]|-?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?");

        public static GraphicsPath Parse(string d)
        {
            List<string> tok = new List<string>();
            foreach (Match m in Tokens.Matches(d)) tok.Add(m.Value);

            GraphicsPath path = new GraphicsPath();
            PointF cur = PointF.Empty, start = PointF.Empty, lastCtrl = PointF.Empty;
            char cmd = 'M', prev = ' ';
            int i = 0;
            while (i < tok.Count)
            {
                if (char.IsLetter(tok[i][0]))
                {
                    cmd = tok[i][0];
                    i++;
                    if (cmd == 'Z' || cmd == 'z')
                    {
                        path.CloseFigure();
                        cur = start;
                        prev = 'Z';
                        continue;
                    }
                }
                if (i >= tok.Count) break;

                bool rel = char.IsLower(cmd);
                PointF b = rel ? cur : PointF.Empty;
                switch (char.ToUpper(cmd))
                {
                    case 'M':
                        {
                            PointF p = Pt(tok, ref i, b);
                            path.StartFigure();
                            cur = start = p;
                            cmd = rel ? 'l' : 'L'; // pares seguintes são linhas
                            prev = 'M';
                            continue;
                        }
                    case 'L':
                        {
                            PointF p = Pt(tok, ref i, b);
                            path.AddLine(cur, p);
                            cur = p;
                            break;
                        }
                    case 'H':
                        {
                            float x = Num(tok, ref i) + (rel ? cur.X : 0);
                            PointF p = new PointF(x, cur.Y);
                            path.AddLine(cur, p);
                            cur = p;
                            break;
                        }
                    case 'V':
                        {
                            float y = Num(tok, ref i) + (rel ? cur.Y : 0);
                            PointF p = new PointF(cur.X, y);
                            path.AddLine(cur, p);
                            cur = p;
                            break;
                        }
                    case 'C':
                        {
                            PointF c1 = Pt(tok, ref i, b), c2 = Pt(tok, ref i, b), e = Pt(tok, ref i, b);
                            path.AddBezier(cur, c1, c2, e);
                            lastCtrl = c2;
                            cur = e;
                            break;
                        }
                    case 'Q':
                        {
                            PointF q = Pt(tok, ref i, b), e = Pt(tok, ref i, b);
                            AddQuad(path, cur, q, e);
                            lastCtrl = q;
                            cur = e;
                            break;
                        }
                    case 'T':
                        {
                            PointF q = (prev == 'Q' || prev == 'T') ? new PointF(2 * cur.X - lastCtrl.X, 2 * cur.Y - lastCtrl.Y) : cur;
                            PointF e = Pt(tok, ref i, b);
                            AddQuad(path, cur, q, e);
                            lastCtrl = q;
                            cur = e;
                            break;
                        }
                    case 'A':
                        {
                            float rx = Num(tok, ref i), ry = Num(tok, ref i), rot = Num(tok, ref i);
                            bool large = Num(tok, ref i) != 0, sweep = Num(tok, ref i) != 0;
                            PointF e = Pt(tok, ref i, b);
                            AddArc(path, cur, e, rx, ry, rot, large, sweep);
                            cur = e;
                            break;
                        }
                    default:
                        i++;
                        break;
                }
                prev = char.ToUpper(cmd);
            }
            return path;
        }

        // Arco elíptico do SVG convertido em curvas de Bézier (algoritmo da especificação SVG).
        static void AddArc(GraphicsPath path, PointF p0, PointF p1, float rx, float ry, float angleDeg, bool large, bool sweep)
        {
            if (rx == 0 || ry == 0 || p0 == p1)
            {
                path.AddLine(p0, p1);
                return;
            }
            double phi = angleDeg * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (p0.X - p1.X) / 2.0, dy = (p0.Y - p1.Y) / 2.0;
            double x1p = cos * dx + sin * dy, y1p = -sin * dx + cos * dy;
            double Rx = Math.Abs(rx), Ry = Math.Abs(ry);
            double lam = x1p * x1p / (Rx * Rx) + y1p * y1p / (Ry * Ry);
            if (lam > 1)
            {
                double k = Math.Sqrt(lam);
                Rx *= k;
                Ry *= k;
            }
            double num = Rx * Rx * Ry * Ry - Rx * Rx * y1p * y1p - Ry * Ry * x1p * x1p;
            double den = Rx * Rx * y1p * y1p + Ry * Ry * x1p * x1p;
            double coef = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, num / den));
            double cxp = coef * Rx * y1p / Ry, cyp = -coef * Ry * x1p / Rx;
            double cx = cos * cxp - sin * cyp + (p0.X + p1.X) / 2.0;
            double cy = sin * cxp + cos * cyp + (p0.Y + p1.Y) / 2.0;
            double th1 = Angle(1, 0, (x1p - cxp) / Rx, (y1p - cyp) / Ry);
            double dth = Angle((x1p - cxp) / Rx, (y1p - cyp) / Ry, (-x1p - cxp) / Rx, (-y1p - cyp) / Ry);
            if (!sweep && dth > 0) dth -= 2 * Math.PI;
            else if (sweep && dth < 0) dth += 2 * Math.PI;

            int segs = Math.Max(1, (int)Math.Ceiling(Math.Abs(dth) / (Math.PI / 2)));
            double delta = dth / segs, t = 4.0 / 3.0 * Math.Tan(delta / 4);
            double a = th1;
            PointF prevPt = p0;
            for (int k = 0; k < segs; k++)
            {
                double a2 = a + delta;
                PointF c1 = Map(cx, cy, Rx, Ry, cos, sin, Math.Cos(a) - t * Math.Sin(a), Math.Sin(a) + t * Math.Cos(a));
                PointF c2 = Map(cx, cy, Rx, Ry, cos, sin, Math.Cos(a2) + t * Math.Sin(a2), Math.Sin(a2) - t * Math.Cos(a2));
                PointF end = k == segs - 1 ? p1 : Map(cx, cy, Rx, Ry, cos, sin, Math.Cos(a2), Math.Sin(a2));
                path.AddBezier(prevPt, c1, c2, end);
                prevPt = end;
                a = a2;
            }
        }

        static PointF Map(double cx, double cy, double rx, double ry, double cos, double sin, double u, double v)
        {
            return new PointF((float)(cx + rx * u * cos - ry * v * sin), (float)(cy + rx * u * sin + ry * v * cos));
        }

        static double Angle(double ux, double uy, double vx, double vy)
        {
            return Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        }

        static void AddQuad(GraphicsPath path, PointF p0, PointF q, PointF e)
        {
            PointF c1 = new PointF(p0.X + 2f / 3f * (q.X - p0.X), p0.Y + 2f / 3f * (q.Y - p0.Y));
            PointF c2 = new PointF(e.X + 2f / 3f * (q.X - e.X), e.Y + 2f / 3f * (q.Y - e.Y));
            path.AddBezier(p0, c1, c2, e);
        }

        static float Num(List<string> tok, ref int i)
        {
            if (i >= tok.Count || char.IsLetter(tok[i][0])) return 0;
            return float.Parse(tok[i++], CultureInfo.InvariantCulture);
        }

        static PointF Pt(List<string> tok, ref int i, PointF b)
        {
            float x = Num(tok, ref i), y = Num(tok, ref i);
            return new PointF(b.X + x, b.Y + y);
        }
    }
}
