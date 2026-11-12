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
                DoLayout(Land);
                LoadKidsItem(kidsGallery.Items[Drawings.All.Length + 2]);
                Shot("infantil-desenho-aconchego");
                kidsGallery.Page = 2; overlay = Overlay.Galeria; Shot("infantil-galeria-aconchego");
                kidsGallery.Page = 0; overlay = Overlay.Nenhum;
                LoadKidsItem(kidsGallery.Items[2]);

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

                // joguinho
                screen = Screen.Jogo; UpdateTheme(); DoLayout(Land);
                Config.GameCoins = 1240; Config.GameRecordWave = 12; Config.GameRecordScore = 48300;
                Config.GameUpg[0] = 2; Config.GameUpg[1] = 1; Config.GameUpg[2] = 3; Config.GameUpg[3] = 1; Config.GameUpg[5] = 3;
                gstate = GState.Menu; Shot("jogo-menu");
                gstate = GState.Shop; Shot("jogo-oficina");
                game = new Game(Config.GameUpg, 7);
                game.H = gameH;
                gstate = GState.Play;
                for (int i = 0; i < 60 * 40; i++)
                {
                    game.SquadTarget = Game.W / 2 + (float)Math.Sin(i / 90.0) * 200;
                    if (game.Units < 12) game.Units = 12;
                    game.Step(1 / 60f);
                }
                gFps = 60;
                Shot("jogo-partida");
                gstate = GState.Pause; Shot("jogo-pausa");
                game.Over = true; lastCoins = game.Coins; newRecord = true;
                gstate = GState.Over; Shot("jogo-fim");
                DoLayout(Port);
                game.Over = false; gstate = GState.Play; Shot("jogo-partida-retrato");
                game = null; gstate = GState.Menu; Shot("jogo-menu-retrato");
                screen = Screen.Inicio; UpdateTheme(); DoLayout(Land);
                Shot("inicio-com-jogo");

                // escolha de jogo e cobrinha
                Config.SnakeRecord = 118;
                screen = Screen.Jogos; UpdateTheme(); DoLayout(Land); Shot("jogos-escolha");
                DoLayout(Port); Shot("jogos-escolha-retrato");
                screen = Screen.Cobra; UpdateTheme(); EnterSnake(); DoLayout(Land);
                Shot("cobra-pronto");
                sstate = SState.Jogando;
                for (int i = 0; i < 400 && !snake.Over; i++)
                {
                    Point h = snake.Body[0];
                    if (snake.Food.X != h.X) snake.Turn(snake.Food.X > h.X ? 1 : -1, 0);
                    else snake.Turn(0, snake.Food.Y > h.Y ? 1 : -1);
                    snake.Step();
                }
                sPressed[1] = 3;
                Shot("cobra-paisagem");
                DoLayout(Port); Shot("cobra-retrato");
                sPressed.Clear();
                snakeRecord = true; sstate = SState.Fim; DoLayout(Land); Shot("cobra-fim");

                // salada de frutas
                Config.FruitRecord = 87;
                screen = Screen.Frutas; UpdateTheme(); EnterFruits(); DoLayout(Land);
                Shot("frutas-menu");
                DoLayout(Port); Shot("frutas-menu-retrato");
                DoLayout(Land);
                CaptureFruits(false, "frutas-desafio");
                fstate = FState.Pausado; LayoutFruits(); Shot("frutas-pausa");
                fruits.Over = true; fruits.OverText = "Cortou o ouriço!"; fRecord = true;
                fstate = FState.Fim; LayoutFruits(); Shot("frutas-fim");
                DoLayout(Port); CaptureFruits(true, "frutas-pequenos-retrato");
                DoLayout(Land); CaptureFruits(true, "frutas-pequenos");
                screen = Screen.Aconchego; UpdateTheme();

                // joguinhos de fases
                Config.SetMiniBest("toupeira", 3); Config.SetMiniBest("forca", 12);
                screen = Screen.Jogos; UpdateTheme(); DoLayout(Land); Shot("jogos-escolha-8");
                DoLayout(Port); Shot("jogos-escolha-8-retrato");
                string[] names = { "lavajato", "toupeira", "deslize", "forca", "ligue", "erros" };
                int[][] lvls = { new int[] { 1, 10 }, new int[] { 1, 9 }, new int[] { 1, 7 }, new int[] { 2, 11 }, new int[] { 3, 11 }, new int[] { 1, 10 } };
                for (int i = 0; i < minis.Length; i++)
                {
                    mini = minis[i]; mstate = MState.Fases; screen = Screen.Mini; UpdateTheme(); DoLayout(Land);
                    if (i == 0) Shot("mini-fases");
                    foreach (int lv in lvls[i])
                    {
                        Random r = new Random(lv * 7 + i);
                        mini.Level = lv; mini.Won = mini.Lost = false; mini.Start(r); mstate = MState.Jogando;
                        DemoMini(mini, r);
                        DoLayout(Land); Shot("mini-" + names[i] + "-" + lv);
                        DoLayout(Port); Shot("mini-" + names[i] + "-" + lv + "-retrato");
                    }
                }
                mini = minis[0]; mini.Level = 2; mini.Won = mini.Lost = false; mini.Start(new Random(4)); DoLayout(Land);
                ((WashGame)mini).DemoStage(1); Shot("mini-lavajato-enxaguar");
                ((WashGame)mini).DemoStage(2); Shot("mini-lavajato-pintar");
                DoLayout(Port); Shot("mini-lavajato-pintar-retrato");
                mini = minis[5];
                DoLayout(Land);
                mini.Won = true; mstate = MState.Fim; LayoutMini(); Shot("mini-fim-ganhou");
                mini.Won = false; mini.Lost = true; Shot("mini-fim-perdeu");
                mini = minis[3]; mini.Lost = true; Shot("mini-fim-forca");
                screen = Screen.Aconchego; UpdateTheme();

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

        // Joga uns segundos sozinho e corta uma fruta no meio do caminho.
        void CaptureFruits(bool kids, string name)
        {
            fKids = kids;
            fruits = new Fruits(kids, view.Width / sc, view.Height / sc, 3);
            fstate = FState.Jogando;
            LayoutFruits();
            blades.Clear();
            oldBlades.Clear();
            fruits.Time = kids ? 0 : 50; // mais frutas no ar
            for (int i = 0; i < 60 * 4; i++)
            {
                fruits.Step(1 / 60f);
                if (fruits.Lives < 3) fruits.Lives = 3;
            }
            fruits.Over = false;
            foreach (FruitPiece p in fruits.Pieces)
                if (!p.Dead && p.Half == 0 && p.Kind != Fruits.Ourico)
                {
                    fruits.Slice(new PointF(p.X - 120, p.Y + 60), new PointF(p.X + 120, p.Y - 60));
                    break;
                }
            for (int i = 0; i < 9; i++) fruits.Step(1 / 60f);
            Blade b = new Blade();
            for (int i = 0; i <= 10; i++)
            {
                b.Pts.Add(new PointF(view.Width / sc * (0.2f + i * 0.05f), view.Height / sc * (0.75f - i * 0.03f - (float)Math.Sin(i / 3.0) * 0.04f)));
                b.Times.Add(fruits.Time - (10 - i) * 0.012f);
            }
            blades[99] = b;
            Shot(name);
            blades.Clear();
        }

        // Avança um pouco cada joguinho para a captura não mostrar só a fase vazia.
        void DemoMini(Mini m, Random r)
        {
            DoLayout(Land);
            for (int i = 0; i < 90; i++) m.Tick(1 / 30f);
            if (m is HangGame)
            {
                // chuta umas letras
                foreach (char ch in "AEOSRT") m.Down(new Contact(), ((HangGame)m).KeyCenter(ch));
            }
            if (m is DiffGame) { m.Hint(); m.Tick(0.2f); }
            if (m is WashGame)
            {
                // esfrega um pedaço do veículo
                Contact c = new Contact();
                Rectangle a = m.Area;
                m.Down(c, new Point(a.X + a.Width / 4, a.Y + a.Height / 3));
                for (int i = 0; i <= 20; i++) c.Drag(new Point(a.X + a.Width / 4 + i * a.Width / 50, a.Y + a.Height / 3 + (i % 2) * a.Height / 6));
            }
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
