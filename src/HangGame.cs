using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;

namespace Pintinho
{
    partial class MainForm
    {
        // Forca, versão amigável: em vez do enforcado, um bonequinho segura balões e cada letra
        // errada estoura um. Fases: palavras cada vez maiores e menos balões.
        class HangGame : Mini
        {
            public override string Key { get { return "forca"; } }
            public override string Title { get { return "Forca"; } }
            public override Color Accent { get { return Shapes.Hex(0x00897b); } }
            public override string Info { get { return word == null ? null : "Erros " + errors + "/" + maxErrors; } }
            public override string LostText { get { return "Era " + word + "!"; } }

            static readonly string[] Words = {
                "Animal|GATO", "Animal|PATO", "Animal|SAPO", "Animal|URSO", "Animal|LEÃO", "Animal|VACA", "Animal|RATO", "Animal|BOI",
                "Animal|ZEBRA", "Animal|COBRA", "Animal|CAVALO", "Animal|MACACO", "Animal|GIRAFA", "Animal|COELHO", "Animal|ELEFANTE",
                "Animal|BORBOLETA", "Animal|TARTARUGA", "Animal|PAPAGAIO", "Animal|JACARÉ", "Animal|PINGUIM", "Animal|CORUJA",
                "Animal|GOLFINHO", "Animal|CAMELO", "Animal|TUBARÃO", "Animal|FORMIGA", "Animal|ABELHA", "Animal|GALINHA",
                "Animal|OVELHA", "Animal|CACHORRO", "Animal|HIPOPÓTAMO", "Animal|RINOCERONTE", "Animal|CROCODILO", "Animal|ESQUILO",
                "Animal|CANGURU", "Animal|MORCEGO", "Animal|BALEIA", "Animal|POLVO", "Animal|PEIXE", "Animal|LOBO", "Animal|TIGRE",
                "Fruta|UVA", "Fruta|KIWI", "Fruta|MAÇÃ", "Fruta|PERA", "Fruta|COCO", "Fruta|LIMÃO", "Fruta|MANGA", "Fruta|BANANA",
                "Fruta|LARANJA", "Fruta|MORANGO", "Fruta|ABACAXI", "Fruta|MELANCIA", "Fruta|MAMÃO", "Fruta|GOIABA", "Fruta|CEREJA",
                "Fruta|AMORA", "Fruta|PÊSSEGO", "Fruta|CARAMBOLA", "Fruta|JABUTICABA", "Fruta|MARACUJÁ", "Fruta|TANGERINA",
                "Fruta|FRAMBOESA", "Fruta|ACEROLA", "Fruta|CAJU", "Fruta|PITANGA", "Fruta|ABACATE",
                "Cor|AZUL", "Cor|ROSA", "Cor|VERDE", "Cor|PRETO", "Cor|BRANCO", "Cor|AMARELO", "Cor|VERMELHO", "Cor|ROXO",
                "Cor|CINZA", "Cor|MARROM", "Cor|DOURADO", "Cor|PRATEADO", "Cor|LILÁS",
                "Veículo|CARRO", "Veículo|MOTO", "Veículo|TREM", "Veículo|BARCO", "Veículo|AVIÃO", "Veículo|ÔNIBUS", "Veículo|TRATOR",
                "Veículo|NAVIO", "Veículo|FOGUETE", "Veículo|BICICLETA", "Veículo|CAMINHÃO", "Veículo|HELICÓPTERO", "Veículo|SUBMARINO",
                "Veículo|PATINETE", "Veículo|AMBULÂNCIA", "Veículo|CARROÇA", "Veículo|METRÔ", "Veículo|JIPE", "Veículo|TÁXI",
                "Veículo|ESCAVADEIRA", "Veículo|BETONEIRA",
                "Objeto|BOLA", "Objeto|MESA", "Objeto|CAMA", "Objeto|COPO", "Objeto|LÁPIS", "Objeto|CADEIRA", "Objeto|JANELA",
                "Objeto|ESPELHO", "Objeto|TESOURA", "Objeto|TELEFONE", "Objeto|GUARDA-CHUVA", "Objeto|TRAVESSEIRO", "Objeto|GELADEIRA",
                "Objeto|COMPUTADOR", "Objeto|ESCOVA", "Objeto|CHAVE", "Objeto|RELÓGIO", "Objeto|MOCHILA", "Objeto|CANETA",
                "Objeto|BORRACHA", "Objeto|PANELA", "Objeto|VASSOURA", "Objeto|LANTERNA", "Objeto|ÓCULOS", "Objeto|SOFÁ",
                "Comida|PÃO", "Comida|BOLO", "Comida|SOPA", "Comida|ARROZ", "Comida|FEIJÃO", "Comida|PIZZA", "Comida|PIPOCA",
                "Comida|SORVETE", "Comida|CHOCOLATE", "Comida|BRIGADEIRO", "Comida|MACARRÃO", "Comida|PASTEL", "Comida|COXINHA",
                "Comida|PUDIM", "Comida|BISCOITO", "Comida|SANDUÍCHE", "Comida|PANQUECA", "Comida|TAPIOCA", "Comida|QUEIJO",
                "Comida|IOGURTE", "Comida|OVO", "Comida|MEL",
                "Natureza|SOL", "Natureza|LUA", "Natureza|MAR", "Natureza|RIO", "Natureza|FLOR", "Natureza|CHUVA", "Natureza|NUVEM",
                "Natureza|ÁRVORE", "Natureza|ESTRELA", "Natureza|MONTANHA", "Natureza|ARCO-ÍRIS", "Natureza|CACHOEIRA",
                "Natureza|VULCÃO", "Natureza|FLORESTA", "Natureza|PRAIA", "Natureza|PLANETA", "Natureza|TROVÃO", "Natureza|VENTO",
                "Natureza|AREIA", "Natureza|OCEANO", "Natureza|FOLHA",
                "Profissão|MÉDICO", "Profissão|BOMBEIRO", "Profissão|PROFESSOR", "Profissão|PILOTO", "Profissão|COZINHEIRO",
                "Profissão|DENTISTA", "Profissão|PADEIRO", "Profissão|CARTEIRO", "Profissão|ASTRONAUTA", "Profissão|VETERINÁRIO",
                "Profissão|MOTORISTA", "Profissão|POLICIAL", "Profissão|PINTOR", "Profissão|CANTOR", "Profissão|JARDINEIRO",
                "Profissão|PESCADOR", "Profissão|ENFERMEIRA", "Profissão|MECÂNICO"
            };
            static readonly int[] MinLen = { 3, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 10 };
            static readonly int[] MaxLen = { 4, 4, 5, 5, 6, 6, 7, 7, 8, 9, 10, 20 };
            static readonly string[] KeyRows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };

            string word, category;
            readonly bool[] guessed = new bool[26];
            readonly bool[] wrong = new bool[26];
            int errors, maxErrors;
            float[] popT; // -1 = inteiro; >= 0 = estourando (segundos); 99 = sumiu
            Rectangle picR;
            RectangleF catR, wordR;
            readonly Rectangle[] keys = new Rectangle[26];
            Font keyFont, letterFont;
            readonly List<string> recent = new List<string>();

            public static char Base(char c)
            {
                switch (c)
                {
                    case 'Á': case 'À': case 'Â': case 'Ã': return 'A';
                    case 'É': case 'Ê': return 'E';
                    case 'Í': return 'I';
                    case 'Ó': case 'Ô': case 'Õ': return 'O';
                    case 'Ú': case 'Ü': return 'U';
                    case 'Ç': return 'C';
                    default: return c;
                }
            }

            static int Letters(string w)
            {
                int n = 0;
                foreach (char c in w) if (char.IsLetter(c)) n++;
                return n;
            }

            public override void Start(Random rnd)
            {
                List<string> pool = new List<string>();
                for (int widen = 0; pool.Count < 3; widen++)
                {
                    pool.Clear();
                    foreach (string e in Words)
                    {
                        int len = Letters(e.Substring(e.IndexOf('|') + 1));
                        if (len >= MinLen[Level - 1] - widen && len <= MaxLen[Level - 1] + widen && !recent.Contains(e)) pool.Add(e);
                    }
                }
                string pick = pool[rnd.Next(pool.Count)];
                recent.Add(pick);
                if (recent.Count > 20) recent.RemoveAt(0);
                category = pick.Substring(0, pick.IndexOf('|'));
                word = pick.Substring(pick.IndexOf('|') + 1);
                for (int i = 0; i < 26; i++) guessed[i] = wrong[i] = false;
                errors = 0;
                maxErrors = Level <= 4 ? 7 : Level <= 8 ? 6 : 5;
                popT = new float[maxErrors];
                for (int i = 0; i < popT.Length; i++) popT[i] = -1;
            }

            public override void Layout()
            {
                if (word == null) return;
                if (keyFont != null) { keyFont.Dispose(); letterFont.Dispose(); }
                RectangleF right;
                if (!M.portrait)
                {
                    picR = new Rectangle(Area.X, Area.Y, (int)(Area.Width * 0.34f), Area.Height);
                    right = new RectangleF(picR.Right + R(32), Area.Y, Area.Right - picR.Right - R(32), Area.Height);
                }
                else
                {
                    picR = new Rectangle(Area.X, Area.Y, Area.Width, (int)(Area.Height * 0.4f));
                    right = new RectangleF(Area.X, picR.Bottom + R(16), Area.Width, Area.Bottom - picR.Bottom - R(16));
                }
                float gap = R(10);
                float key = Math.Min((right.Width - 9 * gap) / 10f, R(84));
                float kbH = 3 * key + 2 * gap;
                for (int r = 0; r < 3; r++)
                {
                    string row = KeyRows[r];
                    float rw = row.Length * key + (row.Length - 1) * gap, x0 = right.X + (right.Width - rw) / 2;
                    for (int i = 0; i < row.Length; i++)
                        keys[row[i] - 'A'] = new Rectangle((int)(x0 + i * (key + gap)), (int)(right.Bottom - kbH + r * (key + gap)), (int)key, (int)key);
                }
                keyFont = M.F(Math.Min(34, key / Sc * 0.45f), FontStyle.Bold);
                catR = new RectangleF(right.X, right.Y + R(10), right.Width, R(40));
                float wordTop = catR.Bottom + R(10), wordBottom = right.Bottom - kbH - R(20);
                int count = word.Length;
                float box = Math.Min(R(72), (right.Width - (count - 1) * R(8)) / count);
                box = Math.Min(box, wordBottom - wordTop);
                float ww = count * box + (count - 1) * R(8);
                wordR = new RectangleF(right.X + (right.Width - ww) / 2, wordTop + (wordBottom - wordTop - box) / 2, ww, box);
                letterFont = M.F(Math.Max(12, box / Sc * 0.6f), FontStyle.Bold);
            }

            bool Revealed(char c) { return !char.IsLetter(c) || guessed[Base(c) - 'A']; }

            public override void Paint(Graphics g, Rectangle clip)
            {
                if (word == null) return;
                DrawScene(g, picR, maxErrors, popT, Lost, Won);

                DrawText(g, "Dica: " + category, M.toastFont, M.theme.Muted, catR, CenterFmt);
                float box = wordR.Height, gap = R(8);
                for (int i = 0; i < word.Length; i++)
                {
                    char c = word[i];
                    RectangleF r = new RectangleF(wordR.X + i * (box + gap), wordR.Y, box, box);
                    if (c == ' ') continue;
                    if (c == '-') { DrawText(g, "-", letterFont, M.theme.Ink, r, CenterFmt); continue; }
                    bool show = Revealed(c);
                    using (GraphicsPath p = Shapes.RoundRect(r, box * 0.18f))
                    using (SolidBrush b = new SolidBrush(show ? Color.White : Shapes.Hex(0xfaecd0))) g.FillPath(b, p);
                    using (Pen p = Shapes.RoundPen(show ? Accent : M.theme.Borda, 3 * Sc)) g.DrawLine(p, r.X + box * 0.12f, r.Bottom - R(3), r.Right - box * 0.12f, r.Bottom - R(3));
                    if (show) DrawText(g, c.ToString(), letterFont, Accent, r, CenterFmt);
                    else if (Lost) DrawText(g, c.ToString(), letterFont, M.theme.Bombeiro, r, CenterFmt);
                }

                for (int i = 0; i < 26; i++)
                {
                    Rectangle r = keys[i];
                    Color fill = wrong[i] ? Shapes.Hex(0xe0d6c4) : guessed[i] ? Accent : Color.White;
                    M.RoundBox(g, r, 14 * Sc, fill, guessed[i] ? fill : M.theme.Borda, 2 * Sc);
                    DrawText(g, ((char)('A' + i)).ToString(), keyFont, wrong[i] ? Shapes.Hex(0xa89a84) : guessed[i] ? Color.White : M.theme.Ink, r, CenterFmt);
                }
            }

            // Céu com o bonequinho segurando os balões que sobraram.
            static void DrawScene(Graphics g, Rectangle r, int total, float[] pops, bool sad, bool happy)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Shapes.RoundRect(r, 28 * Math.Min(r.Width, r.Height) / 600f + 8)) g.SetClip(clip, CombineMode.Intersect);
                using (LinearGradientBrush b = new LinearGradientBrush(r, Shapes.Hex(0x90caf9), Shapes.Hex(0xe3f2fd), 90f)) g.FillRectangle(b, r);
                float u = Math.Min(r.Width / 400f, r.Height / 560f);
                float cx = r.X + r.Width / 2f, ground = r.Bottom - 60 * u;
                using (SolidBrush b = new SolidBrush(Shapes.Hex(0x9ccc65))) g.FillEllipse(b, r.X - r.Width * 0.2f, ground - 10 * u, r.Width * 1.4f, 200 * u);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                {
                    g.FillEllipse(b, r.X + 30 * u, r.Y + 50 * u, 90 * u, 34 * u);
                    g.FillEllipse(b, r.X + 60 * u, r.Y + 34 * u, 60 * u, 40 * u);
                    g.FillEllipse(b, r.Right - 140 * u, r.Y + 90 * u, 100 * u, 36 * u);
                }

                // bonequinho
                float lw = Math.Max(1.5f, 3.5f * u);
                float headY = ground - 150 * u, handX = cx + 52 * u, handY = headY - 44 * u; // mão levantada, cordinhas acima da cabeça
                using (Pen ink = Shapes.RoundPen(FInk, lw))
                {
                    using (Pen leg = Shapes.RoundPen(Shapes.Hex(0x1e63d6), 14 * u))
                    {
                        g.DrawLine(leg, cx - 12 * u, ground - 60 * u, cx - 16 * u, ground - 8 * u);
                        g.DrawLine(leg, cx + 12 * u, ground - 60 * u, cx + 16 * u, ground - 8 * u);
                    }
                    using (GraphicsPath shirt = Shapes.RoundRect(new RectangleF(cx - 30 * u, headY + 34 * u, 60 * u, 66 * u), 18 * u))
                    using (SolidBrush b = new SolidBrush(Shapes.Hex(0xffc727))) { g.FillPath(b, shirt); g.DrawPath(ink, shirt); }
                    using (Pen arm = Shapes.RoundPen(Shapes.Hex(0xf1c27d), 11 * u))
                    {
                        g.DrawLine(arm, cx - 26 * u, headY + 46 * u, cx - 40 * u, headY + 86 * u);
                        g.DrawLine(arm, cx + 26 * u, headY + 46 * u, handX, handY);
                    }
                    RectangleF head = new RectangleF(cx - 30 * u, headY - 30 * u, 60 * u, 62 * u);
                    using (SolidBrush b = new SolidBrush(Shapes.Hex(0xf1c27d))) g.FillEllipse(b, head);
                    g.DrawEllipse(ink, head);
                    using (SolidBrush b = new SolidBrush(Shapes.Hex(0x5d4037))) g.FillPie(b, head.X - 2 * u, head.Y - 4 * u, head.Width + 4 * u, head.Height * 0.8f, 180, 180);
                    using (SolidBrush b = new SolidBrush(FInk))
                    {
                        g.FillEllipse(b, cx - 14 * u, headY - 2 * u, 7 * u, 8 * u);
                        g.FillEllipse(b, cx + 7 * u, headY - 2 * u, 7 * u, 8 * u);
                    }
                    if (sad) g.DrawArc(ink, cx - 11 * u, headY + 14 * u, 22 * u, 14 * u, 200, 140);
                    else g.DrawArc(ink, cx - 12 * u, headY + 4 * u, 24 * u, happy ? 20 * u : 14 * u, 20, 140);
                }

                // balões em leque acima da mão
                for (int i = 0; i < total; i++)
                {
                    float p = pops[i];
                    if (p >= 99) continue;
                    double a = (-90 + (i - (total - 1) / 2f) * (100f / Math.Max(1, total - 1))) * Math.PI / 180;
                    float len = 200 * u + (i % 2) * 30 * u;
                    float bx = handX + (float)Math.Cos(a) * len * 0.8f, by = handY + (float)Math.Sin(a) * len;
                    float br = 30 * u;
                    if (p < 0)
                    {
                        using (Pen s = new Pen(Color.FromArgb(160, FInk), Math.Max(1, 1.5f * u))) g.DrawLine(s, handX, handY, bx, by + br);
                        DrawBalloonOnly(g, bx, by, br, Bright[i % Bright.Length], lw * 0.7f);
                    }
                    else
                    {
                        // estourando: pedacinhos voando
                        float k = p / 0.35f;
                        using (Pen s = Shapes.RoundPen(Bright[i % Bright.Length], 4 * u))
                            for (int j = 0; j < 8; j++)
                            {
                                double b = j * Math.PI / 4;
                                float d0 = br * (0.4f + k), d1 = br * (0.7f + k * 1.4f);
                                g.DrawLine(s, bx + (float)Math.Cos(b) * d0, by + (float)Math.Sin(b) * d0, bx + (float)Math.Cos(b) * d1, by + (float)Math.Sin(b) * d1);
                            }
                    }
                }
                g.Restore(st);
            }

            static void DrawBalloonOnly(Graphics g, float cx, float cy, float r, Color c, float line)
            {
                using (SolidBrush b = new SolidBrush(c))
                {
                    g.FillPolygon(b, new PointF[] { new PointF(cx, cy + r * 0.9f), new PointF(cx - r * 0.15f, cy + r * 1.1f), new PointF(cx + r * 0.15f, cy + r * 1.1f) });
                    g.FillEllipse(b, cx - r * 0.82f, cy - r, r * 1.64f, r * 1.95f);
                }
                using (Pen p = new Pen(FInk, line)) g.DrawEllipse(p, cx - r * 0.82f, cy - r, r * 1.64f, r * 1.95f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(130, 255, 255, 255))) g.FillEllipse(b, cx - r * 0.5f, cy - r * 0.7f, r * 0.32f, r * 0.5f);
            }

            public override void Tick(float dt)
            {
                bool any = false;
                for (int i = 0; i < popT.Length; i++)
                    if (popT[i] >= 0 && popT[i] < 99)
                    {
                        popT[i] += dt;
                        if (popT[i] > 0.35f) popT[i] = 99;
                        any = true;
                    }
                if (any) Repaint(picR);
            }

            public Point KeyCenter(char ch)
            {
                Rectangle r = keys[ch - 'A'];
                return new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            }

            public override void Down(Contact c, Point p)
            {
                for (int i = 0; i < 26; i++)
                {
                    if (!keys[i].Contains(p) || guessed[i] || wrong[i]) continue;
                    char letter = (char)('A' + i);
                    bool hit = false;
                    foreach (char ch in word) if (Base(ch) == letter) hit = true;
                    if (hit)
                    {
                        guessed[i] = true;
                        bool all = true;
                        foreach (char ch in word) if (!Revealed(ch)) all = false;
                        if (all) Won = true;
                    }
                    else
                    {
                        wrong[i] = true;
                        // estoura o balão mais à direita que ainda está inteiro
                        for (int b = popT.Length - 1; b >= 0; b--) if (popT[b] < 0) { popT[b] = 0; break; }
                        errors++;
                        if (errors >= maxErrors) Lost = true;
                    }
                    RepaintAll();
                    return;
                }
            }

            public override void DrawArt(Graphics g, Rectangle img)
            {
                float[] pops = { -1, -1, -1, 99, -1 };
                DrawScene(g, img, 5, pops, false, true);
            }
        }
    }
}
