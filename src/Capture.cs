using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Pintinho
{
    // Modo captura (--captura <pasta>): salva PNGs de todas as telas para conferir a interface sem
    // olhar a tela de verdade, e um PNG de cada desenho.
    partial class MainForm
    {
        static readonly Size Land = new Size(1280, 800), Port = new Size(800, 1280);

        void RunCapture()
        {
            try
            {
                Directory.CreateDirectory(CaptureDir);

                // tela inicial
                DoLayout(Land);
                Shot("inicio-paisagem-claro");
                Config.DarkTheme = true; UpdateTheme();
                Shot("inicio-paisagem-escuro");
                Config.DarkTheme = false; UpdateTheme();
                DoLayout(Port);
                Shot("inicio-retrato-claro");

                // infantil
                DoLayout(Land);
                screen = Screen.Infantil; kidsEntered = true; UpdateTheme();
                LoadKidsItem(kidsGallery.Items[2]);
                DemoKids();
                Shot("infantil-paisagem-claro");
                overlay = Overlay.MaisCores; Shot("infantil-mais-cores");
                overlay = Overlay.Carimbos; kidsStamp = 4; Shot("infantil-carimbos");
                overlay = Overlay.Galeria; Shot("infantil-galeria");
                overlay = Overlay.Nenhum;
                Config.DarkTheme = true; UpdateTheme();
                DoLayout(Port);
                Shot("infantil-retrato-escuro");
                Config.DarkTheme = false; UpdateTheme();

                // aconchego
                DoLayout(Land);
                screen = Screen.Aconchego; UpdateTheme();
                cozyEntered = true;
                cozySurface = new Surface(CozyPaper, CozyPaper, 8);
                LoadCozyItem(cozyGallery.Items[1]);
                DemoCozy();
                Shot("aconchego-paisagem-claro");
                hintUntil = Environment.TickCount + 100000; Shot("aconchego-dica"); hintUntil = 0;
                OpenMixer(); Shot("aconchego-misturador");
                overlay = Overlay.Carimbos; Shot("aconchego-carimbos");
                overlay = Overlay.Galeria; Shot("aconchego-galeria");
                overlay = Overlay.Nenhum;
                ZoomBy(2.5f); Shot("aconchego-zoom");
                Config.DarkTheme = true; UpdateTheme();
                DoLayout(Land);
                Shot("aconchego-paisagem-escuro");
                Config.DarkTheme = false; UpdateTheme();
                DoLayout(Port);
                Shot("aconchego-retrato-claro");

                // área dos pais
                DoLayout(Land);
                overlay = Overlay.Pais;
                Shot("pais");
                overlay = Overlay.Nenhum;

                SaveDrawings(Drawings.All, 1000, 700, "desenho-");
                SaveDrawings(Drawings.Cozy, 900, 900, "aconchego-");
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                try { File.WriteAllText(Path.Combine(CaptureDir, "erro.txt"), ex.ToString()); } catch { }
            }
            allowClose = true;
            Close();
        }

        void SaveDrawings(Drawings.Item[] items, int w, int h, string prefix)
        {
            foreach (Drawings.Item d in items)
                using (Bitmap ov = Drawings.Render(d, w, h))
                using (Bitmap b = new Bitmap(w, h))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.White);
                        g.DrawImage(ov, new Rectangle(0, 0, w, h));
                    }
                    b.Save(Path.Combine(CaptureDir, prefix + d.Key + ".png"), ImageFormat.Png);
                }
        }

        void Shot(string name)
        {
            using (Bitmap b = new Bitmap(view.Width, view.Height))
            {
                using (Graphics g = Graphics.FromImage(b)) PaintAll(g, new Rectangle(Point.Empty, view));
                b.Save(Path.Combine(CaptureDir, name + ".png"), ImageFormat.Png);
            }
        }

        void KidsFill(float x, float y, Color c)
        {
            Point p = Drawings.ToPixel(kidsCurrent.Builtin, x, y, kidsSurface.W, kidsSurface.H);
            kidsSurface.Fill(p.X, p.Y, c);
        }

        void DemoKids()
        {
            KidsFill(4, 4, Theme.Palette[10]);
            KidsFill(19, 75, Theme.Palette[4]);
            KidsFill(128, 92, Theme.Palette[4]);
            KidsFill(150, 62, Shapes.Hex(0xb3e5fc));
            KidsFill(46, 77, Theme.Palette[6]);
            KidsFill(45, 97, Theme.Palette[0]);
            KidsFill(155, 97, Theme.Palette[0]);
            Point star = Drawings.ToPixel(kidsCurrent.Builtin, 178, 18, kidsSurface.W, kidsSurface.H);
            kidsSurface.Stamp(star, 0, Theme.Palette[6], 40 * KidsScale());
            Contact c = new Contact();
            c.Tool = Tool.ArcoIris;
            Point a = Drawings.ToPixel(kidsCurrent.Builtin, 10, 130, kidsSurface.W, kidsSurface.H);
            c.Paper = a;
            for (int x = 10; x <= 190; x += 4)
            {
                Point q = Drawings.ToPixel(kidsCurrent.Builtin, x, 130 + (float)Math.Sin(x / 12.0) * 3, kidsSurface.W, kidsSurface.H);
                KidsStroke(c, new Point(q.X + paperRect.X, q.Y + paperRect.Y));
            }
            kidsTool = Tool.Balde;
        }

        void CozyFill(float x, float y, Color c)
        {
            Point p = Drawings.ToPixel(cozyCurrent.Builtin, x, y, CozyPaper, CozyPaper);
            cozySurface.Fill(p.X, p.Y, c);
        }

        void DemoCozy()
        {
            CozyFill(90, 55, Shapes.Hex(0xa7d3e8));
            CozyFill(150, 55, Shapes.Hex(0xa7d3e8));
            CozyFill(40, 90, Shapes.Hex(0xc9b8ea));
            CozyFill(20, 60, Shapes.Hex(0xf7c6c7));
            CozyFill(180, 60, Shapes.Hex(0xf7c6c7));
            CozyFill(80, 140, Shapes.Hex(0xf9d5a7));
            CozyFill(165, 112, Shapes.Hex(0xb5d8b1));
            CozyFill(169, 140, Shapes.Hex(0xf4a7a3));
            CozyFill(100, 185, Shapes.Hex(0xe3c19a));
            CozyFill(64, 40, Shapes.Hex(0xfbe7a1));
            CozyFill(28, 141, Shapes.Hex(0xc9a27a));
            cozySurface.SaveUndo();
            List<PointF> path = new List<PointF>();
            for (int x = 112; x <= 168; x += 4)
            {
                Point q = Drawings.ToPixel(cozyCurrent.Builtin, x, 182 + (float)Math.Sin(x / 6.0) * 2, CozyPaper, CozyPaper);
                path.Add(q);
                cozySurface.StrokePath(path, Color.FromArgb(115, 0xe0, 0x7a, 0x6f), 30, true);
            }
            cozyTool = Tool.Lapis;
            cozyColor = Theme.Cozy[4];
        }
    }
}
