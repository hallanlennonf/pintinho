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
                    default:
                        i++;
                        break;
                }
                prev = char.ToUpper(cmd);
            }
            return path;
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
