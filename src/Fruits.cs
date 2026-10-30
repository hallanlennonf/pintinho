using System;
using System.Collections.Generic;
using System.Drawing;

namespace Pintinho
{
    class FruitPiece
    {
        public int Kind;   // Fruits.Melancia ... Fruits.Ourico
        public int Half;   // 0 = inteira, 1 = metade de cima, 2 = metade de baixo
        public float X, Y, VX, VY, G, Rot, VRot;
        public bool Dead;
    }

    class Drop { public float X, Y, VX, VY, Life, Size; public Color C; }
    class Splat { public float X, Y, Size, Life; public int Seed; public Color C; }
    class Popup { public float X, Y, Life; public string Text; public bool Big; public Color C; }

    // Lógica pura da Salada de Frutas, em pixels do design (W x H).
    // Pequenos: frutas devagar e grandes, sem ouriço, nunca perde (um toque já corta).
    // Desafio: 3 vidas; perde uma quando uma fruta cai inteira ou quando corta um ouriço.
    class Fruits
    {
        public const int Melancia = 0, Laranja = 1, Maca = 2, Limao = 3, Kiwi = 4, Coco = 5, Carambola = 6, Ourico = 7, Count = 8;
        public static readonly Color[] Juice = {
            Shapes.Hex(0xff4d5a), Shapes.Hex(0xffa726), Shapes.Hex(0xffd54f), Shapes.Hex(0xc0ca33),
            Shapes.Hex(0x8bc34a), Shapes.Hex(0xb3e5fc), Shapes.Hex(0xffca28), Shapes.Hex(0x5e35b1)
        };

        public readonly bool Kids;
        public float W, H, R;
        public readonly List<FruitPiece> Pieces = new List<FruitPiece>();
        public readonly List<Drop> Drops = new List<Drop>();
        public readonly List<Splat> Splats = new List<Splat>();
        public readonly List<Popup> Popups = new List<Popup>();
        public int Score, Sliced, Lives = 3;
        public bool Over;
        public string OverText;
        public float Time, Shake, Flash;

        float spawnIn = 0.7f;
        readonly Random rnd;

        public Fruits(bool kids, float w, float h, int seed)
        {
            Kids = kids;
            W = w;
            H = h;
            R = kids ? 64 : 48;
            rnd = new Random(seed);
        }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        float Gravity { get { return Kids ? 430 : 980 + Math.Min(380, Time * 3.2f); } }

        public void Step(float dt)
        {
            if (Over) return;
            Time += dt;
            spawnIn -= dt;
            if (spawnIn <= 0) Spawn();

            for (int i = Pieces.Count - 1; i >= 0; i--)
            {
                FruitPiece p = Pieces[i];
                p.VY += p.G * dt;
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.Rot += p.VRot * dt;
                if (p.Dead || (p.VY > 0 && p.Y > H + R * 2))
                {
                    if (!p.Dead && p.Half == 0 && !Kids && p.Kind < Carambola) Miss(p);
                    Pieces.RemoveAt(i);
                }
            }
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                Drop d = Drops[i];
                d.VY += 900 * dt;
                d.X += d.VX * dt;
                d.Y += d.VY * dt;
                d.Life -= dt;
                if (d.Life <= 0) Drops.RemoveAt(i);
            }
            for (int i = Splats.Count - 1; i >= 0; i--)
            {
                Splats[i].Life -= dt * 0.45f;
                if (Splats[i].Life <= 0) Splats.RemoveAt(i);
            }
            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                Popups[i].Life -= dt;
                Popups[i].Y -= 40 * dt;
                if (Popups[i].Life <= 0) Popups.RemoveAt(i);
            }
            Shake = Math.Max(0, Shake - dt * 1.5f);
            Flash = Math.Max(0, Flash - dt * 2.5f);
        }

        void Spawn()
        {
            int n;
            if (Kids)
            {
                spawnIn = Rand(1.2f, 1.8f);
                n = rnd.NextDouble() < 0.3 ? 2 : 1;
            }
            else
            {
                spawnIn = Math.Max(0.55f, 1.5f - Time * 0.01f) * Rand(0.8f, 1.2f);
                n = 1 + rnd.Next(1 + Math.Min(3, (int)(Time / 18)));
            }
            float bombChance = Kids || Time < 6 ? 0 : Math.Min(0.2f, 0.05f + Time * 0.0015f);
            for (int i = 0; i < n; i++)
            {
                int kind = rnd.Next(6);
                double roll = rnd.NextDouble();
                if (roll < bombChance) kind = Ourico;
                else if (roll < bombChance + (Kids ? 0.06 : 0.03)) kind = Carambola;
                Launch(kind);
            }
        }

        void Launch(int kind)
        {
            FruitPiece p = new FruitPiece();
            p.Kind = kind;
            p.G = Gravity;
            p.X = W * Rand(0.12f, 0.88f);
            p.Y = H + R;
            float apexY = H * Rand(0.1f, 0.42f);
            p.VY = -(float)Math.Sqrt(2 * p.G * (p.Y - apexY));
            float t = -p.VY / p.G;
            p.VX = (W * Rand(0.22f, 0.78f) - p.X) / t;
            p.Rot = Rand(0, 360);
            p.VRot = Rand(-160, 160);
            Pieces.Add(p);
        }

        void Miss(FruitPiece p)
        {
            Lives--;
            Popup m = new Popup();
            m.X = Math.Max(R, Math.Min(W - R, p.X));
            m.Y = H - R;
            m.Life = 1.2f;
            m.Text = "✕";
            m.Big = true;
            m.C = Shapes.Hex(0xe5533e);
            Popups.Add(m);
            if (Lives <= 0) { Over = true; OverText = "Caiu fruta demais!"; }
        }

        // Um traço do dedo de a até b. Devolve quantas frutas cortou.
        public int Slice(PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len2 = dx * dx + dy * dy;
            if (len2 < 9) return 0;
            float ang = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
            float hit = R * (Kids ? 1.15f : 0.95f);
            int count = 0;
            for (int i = 0; i < Pieces.Count; i++)
            {
                FruitPiece p = Pieces[i];
                if (p.Dead || p.Half != 0) continue;
                float t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
                float qx = a.X + t * dx - p.X, qy = a.Y + t * dy - p.Y;
                if (qx * qx + qy * qy <= hit * hit) { Cut(p, ang); count++; }
            }
            return count;
        }

        // Toque parado (só no modo Pequenos): corta a fruta embaixo do dedo.
        public int Tap(PointF q)
        {
            float hit = R * 1.25f;
            for (int i = Pieces.Count - 1; i >= 0; i--)
            {
                FruitPiece p = Pieces[i];
                if (p.Dead || p.Half != 0) continue;
                float dx = q.X - p.X, dy = q.Y - p.Y;
                if (dx * dx + dy * dy <= hit * hit) { Cut(p, Rand(-40, 40)); return 1; }
            }
            return 0;
        }

        void Cut(FruitPiece p, float angDeg)
        {
            p.Dead = true;
            Color juice = Juice[p.Kind];
            if (p.Kind == Ourico)
            {
                Lives--;
                Flash = 1;
                Shake = 0.6f;
                AddPopup(p.X, p.Y, "Ai! Ouriço!", true, Shapes.Hex(0xe5533e));
                Burst(p.X, p.Y, Shapes.Hex(0x311b92), 22, 520);
                if (Lives <= 0) { Over = true; OverText = "Cortou o ouriço!"; }
                return;
            }

            double a = angDeg * Math.PI / 180;
            float nx = -(float)Math.Sin(a), ny = (float)Math.Cos(a); // normal do corte
            for (int h = 1; h <= 2; h++)
            {
                FruitPiece half = new FruitPiece();
                half.Kind = p.Kind;
                half.Half = h;
                half.G = p.G;
                half.X = p.X;
                half.Y = p.Y;
                float push = (h == 1 ? -1 : 1) * 130;
                half.VX = p.VX * 0.6f + nx * push;
                half.VY = Math.Min(p.VY, 0) * 0.4f + ny * push - 60;
                half.Rot = angDeg;
                half.VRot = (h == 1 ? -1 : 1) * Rand(60, 200);
                Pieces.Add(half);
            }
            Burst(p.X, p.Y, juice, 14, 380);
            Splat s = new Splat();
            s.X = p.X;
            s.Y = p.Y;
            s.Size = R * Rand(1.3f, 1.8f);
            s.Life = 1;
            s.Seed = rnd.Next(1000);
            s.C = juice;
            Splats.Add(s);
            if (Splats.Count > 14) Splats.RemoveAt(0);

            Sliced++;
            if (p.Kind == Carambola)
            {
                Score += 5;
                AddPopup(p.X, p.Y, "Carambola! +5", true, Shapes.Hex(0xf9a825));
            }
            else Score++;
            if (Kids && Sliced % 10 == 0)
            {
                string[] cheers = { "Muito bem!", "Uau!", "Que salada!", "Isso aí!" };
                AddPopup(W / 2, H * 0.35f, cheers[(Sliced / 10 - 1) % cheers.Length], true, Shapes.Hex(0x43a047));
                Burst(W / 2, H * 0.35f, Shapes.Hex(0xffc727), 30, 600);
            }
        }

        public void Combo(int n, PointF at)
        {
            if (n < 3) return;
            Score += n;
            AddPopup(at.X, at.Y, "Combo ×" + n + "  +" + n, true, Shapes.Hex(0xe8680c));
        }

        void AddPopup(float x, float y, string text, bool big, Color c)
        {
            Popup p = new Popup();
            p.X = Math.Max(R * 2, Math.Min(W - R * 2, x));
            p.Y = Math.Max(R, y - R);
            p.Life = 1.1f;
            p.Text = text;
            p.Big = big;
            p.C = c;
            Popups.Add(p);
        }

        void Burst(float x, float y, Color c, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                Drop d = new Drop();
                double a = rnd.NextDouble() * Math.PI * 2;
                float v = Rand(0.3f, 1f) * speed;
                d.X = x;
                d.Y = y;
                d.VX = (float)Math.Cos(a) * v;
                d.VY = (float)Math.Sin(a) * v - 120;
                d.Life = Rand(0.4f, 0.8f);
                d.Size = Rand(5, 12);
                d.C = c;
                Drops.Add(d);
            }
            while (Drops.Count > 160) Drops.RemoveAt(0);
        }
    }
}
