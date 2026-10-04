using System;
using System.Collections.Generic;
using System.Drawing;

namespace Pintinho
{
    // Comboio: lógica pura do jogo (sem desenho). Coordenadas no espaço do design: 760 de largura,
    // altura H (800 em paisagem). Atualizada em passos fixos de 1/60 s.
    class Enemy
    {
        public float X, Y, R, Hp, MaxHp, Speed, Phase;
        public int Kind; // 0 gosma, 1 gosmona, 2 chefão
        public bool Alive = true;
    }

    class Gate
    {
        static int nextId = 1;
        public readonly int Id = nextId++;
        public float Y;
        public int[] Kind = new int[2], Val = new int[2], Hits = new int[2];
        public bool Alive = true;
    }

    class Particle
    {
        public float X, Y, Vx, Vy, Life, MaxLife, Size;
        public Color Color;
        public string Text;
    }

    class Game
    {
        public const float W = 760f;
        public const int MaxDisplay = 24, MaxBullets = 320;
        public const float BulletSpeed = 900f;
        // Tipos de plaquinha
        public const int GUnits = 0, GDouble = 1, GDamage = 2, GRate = 3, GJet = 4, GShield = 5;
        public static readonly float[] GateLeft = { 42, 396 };
        public const float GateWidth = 322, GateHalf = 35;

        public float H = 800;
        public float SquadX = W / 2, SquadTarget = W / 2;
        public int Units, Wave, Score, Kills, Jets, Shield, ShieldMax;
        public float Damage, Rate, CoinBonus, CoinsF, Time;
        public int WaveTotal, WaveDone;
        public bool Over;
        public float Banner;
        public string BannerText = "";

        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Gate> Gates = new List<Gate>();
        public readonly List<Particle> Particles = new List<Particle>();
        public readonly List<string> Picked = new List<string>();
        public readonly float[] BX = new float[MaxBullets], BY = new float[MaxBullets], BVX = new float[MaxBullets], BDmg = new float[MaxBullets];
        readonly int[] BGate = new int[MaxBullets]; // última plaquinha que o jato já contou
        public int BulletCount;

        readonly Random rnd;
        int toSmall, toBig;
        bool bossPending;
        float spawnTimer, gateTimer, fireTimer, waveDelay;

        public int Coins { get { return (int)CoinsF; } }
        // primeira fileira da frota; as outras ficam embaixo dela
        public float SquadY { get { return H - 70 - (Rows() - 1) * 50; } }
        public float SquadTop { get { return SquadY - 30; } }
        public int Display { get { return Math.Min(Units, MaxDisplay); } }

        // upg: frota, dano, cadência, jatos, ímã, para-choque
        public Game(int[] upg, int seed)
        {
            rnd = new Random(seed);
            Units = 1 + upg[0];
            Damage = 1 + upg[1];
            Rate = 2f * (1 + 0.15f * upg[2]);
            Jets = 1 + upg[3];
            CoinBonus = 0.2f * upg[4];
            ShieldMax = upg[5];
            StartWave(1);
        }

        float R01() { return (float)rnd.NextDouble(); }

        void StartWave(int k)
        {
            Wave = k;
            Shield = ShieldMax;
            toSmall = 10 + 4 * k;
            toBig = k / 2 + (k >= 3 ? 1 : 0);
            bossPending = k % 5 == 0;
            WaveTotal = toSmall + toBig + (bossPending ? 1 : 0);
            WaveDone = 0;
            spawnTimer = 0.8f;
            if (k == 1) gateTimer = 4f;
            Banner = 2.2f;
            BannerText = bossPending ? "Onda " + k + " · CHEFÃO!" : "Onda " + k;
        }

        public int Rows() { return (Display + Columns() - 1) / Columns(); }

        // teto do número de uma plaquinha de caminhões (atirar nela aumenta até aqui)
        public int GateCap() { return 3 + 2 * Wave; }

        public int Columns()
        {
            int d = Display;
            return d <= 3 ? Math.Max(1, d) : d <= 8 ? 4 : d <= 15 ? 5 : 8;
        }

        public void UnitPos(int i, out float x, out float y)
        {
            int cols = Columns();
            int row = i / cols, col = i % cols;
            int inRow = Math.Min(cols, Display - row * cols);
            x = SquadX + (col - (inRow - 1) / 2f) * 38f;
            y = SquadY + row * 50f;
        }

        public void Step(float dt)
        {
            if (Over) return;
            Time += dt;

            float half = Columns() * 19f + 6;
            SquadTarget = Math.Max(24 + half, Math.Min(W - 24 - half, SquadTarget));
            SquadX += (SquadTarget - SquadX) * Math.Min(1f, dt * 20f);

            fireTimer -= dt;
            if (fireTimer <= 0)
            {
                fireTimer += 1f / Rate;
                if (fireTimer < 0) fireTimer = 0;
                Fire();
            }

            if (waveDelay > 0)
            {
                waveDelay -= dt;
                if (waveDelay <= 0) StartWave(Wave + 1);
            }
            else
            {
                bool pending = toSmall > 0 || toBig > 0 || bossPending;
                spawnTimer -= dt;
                if (pending && spawnTimer <= 0)
                {
                    SpawnEnemy();
                    spawnTimer = Math.Max(0.16f, 0.85f - Wave * 0.04f) * (0.6f + R01() * 0.8f);
                }
                if (!pending && Enemies.Count == 0)
                {
                    waveDelay = 1.8f;
                    Score += Wave * 100;
                }
            }

            gateTimer -= dt;
            if (gateTimer <= 0)
            {
                SpawnGate();
                gateTimer = 7f + R01() * 3f;
            }

            UpdateBullets(dt);
            UpdateEnemies(dt);
            UpdateGates(dt);
            UpdateParticles(dt);
            if (Banner > 0) Banner -= dt;
        }

        void Fire()
        {
            int d = Display;
            float mul = Units / (float)Math.Max(1, d); // frota grande: cada jato vale mais
            for (int i = 0; i < d; i++)
            {
                float x, y;
                UnitPos(i, out x, out y);
                for (int j = 0; j < Jets; j++)
                {
                    if (BulletCount >= MaxBullets) return;
                    float a = (j - (Jets - 1) / 2f) * 0.13f;
                    int b = BulletCount++;
                    BX[b] = x;
                    BY[b] = y - 28;
                    BVX[b] = (float)Math.Sin(a) * BulletSpeed;
                    BDmg[b] = Damage * mul;
                    BGate[b] = 0;
                }
            }
        }

        void SpawnEnemy()
        {
            Enemy e = new Enemy();
            e.Phase = R01() * 6.28f;
            float hpMul = (float)Math.Pow(1.17, Wave - 1); // cada onda fica ~17% mais resistente
            int total = toSmall + toBig;
            if (bossPending && toSmall <= (10 + 4 * Wave) / 2)
            {
                bossPending = false;
                e.Kind = 2;
                e.R = 64;
                e.MaxHp = 60 * (Wave / 5f) * (1 + Wave * 0.3f) * hpMul;
                e.Speed = 22;
            }
            else if (toBig > 0 && (toSmall == 0 || rnd.Next(total) < toBig))
            {
                toBig--;
                e.Kind = 1;
                e.R = 30;
                e.MaxHp = (6 + Wave * 2f) * hpMul;
                e.Speed = 30 + Wave * 2 + R01() * 10;
            }
            else if (toSmall > 0)
            {
                toSmall--;
                e.Kind = 0;
                e.R = 18;
                e.MaxHp = (1 + Wave * 0.5f) * hpMul;
                e.Speed = 45 + Wave * 3 + R01() * 20;
            }
            else return;
            e.Hp = e.MaxHp;
            e.X = 40 + e.R + R01() * (W - 80 - 2 * e.R);
            e.Y = -e.R;
            Enemies.Add(e);
        }

        void SpawnGate()
        {
            Gate g = new Gate();
            g.Y = -GateHalf;
            int good = rnd.Next(2);
            if (R01() < 0.6f)
            {
                // caminhões: um lado bom, outro ruim (ou dobrar)
                if (Units < 40 && R01() < 0.25f) { g.Kind[good] = GDouble; g.Val[good] = 2; }
                else { g.Kind[good] = GUnits; g.Val[good] = 3 + rnd.Next(4) + Wave; }
                g.Kind[1 - good] = GUnits;
                g.Val[1 - good] = -(2 + rnd.Next(4) + Wave / 2);
            }
            else
            {
                List<int> ups = new List<int> { GDamage, GRate, GShield };
                if (Jets < 3) ups.Add(GJet);
                int a = ups[rnd.Next(ups.Count)];
                ups.Remove(a);
                int b = ups[rnd.Next(ups.Count)];
                g.Kind[0] = a;
                g.Kind[1] = b;
            }
            Gates.Add(g);
        }

        public static void GateText(int kind, int val, out string big, out string small)
        {
            switch (kind)
            {
                case GUnits: big = (val >= 0 ? "+" : "−") + Math.Abs(val); small = "CAMINHÕES"; break;
                case GDouble: big = "×2"; small = "CAMINHÕES"; break;
                case GDamage: big = "Dano +1"; small = "JATO MAIS FORTE"; break;
                case GRate: big = "+15%"; small = "CADÊNCIA"; break;
                case GJet: big = "Jato extra"; small = "TIRO EM LEQUE"; break;
                default: big = "+1"; small = "PARA-CHOQUE"; break;
            }
        }

        // 0 = verde (bom), 1 = vermelho (ruim), 2 = azul (melhoria)
        public static int GateColor(int kind, int val)
        {
            if (kind == GUnits) return val >= 0 ? 0 : 1;
            if (kind == GDouble) return 0;
            return 2;
        }

        void UpdateBullets(float dt)
        {
            int n = 0;
            for (int b = 0; b < BulletCount; b++)
            {
                float x = BX[b] + BVX[b] * dt, y = BY[b] - BulletSpeed * dt;
                bool alive = y > -20 && x > 0 && x < W;
                if (alive) HitGate(x, y, b); // o jato atravessa a plaquinha
                if (alive && HitEnemy(x, y, BDmg[b])) alive = false;
                if (!alive) continue;
                BX[n] = x; BY[n] = y; BVX[n] = BVX[b]; BDmg[n] = BDmg[b]; BGate[n] = BGate[b];
                n++;
            }
            BulletCount = n;
        }

        // Conta o acerto na plaquinha (uma vez por jato); o jato segue em frente.
        void HitGate(float x, float y, int b)
        {
            foreach (Gate g in Gates)
            {
                if (y < g.Y - GateHalf || y > g.Y + GateHalf) continue;
                if (BGate[b] == g.Id) return;
                int side = x < W / 2 ? 0 : 1;
                if (x < GateLeft[side] || x > GateLeft[side] + GateWidth) return;
                BGate[b] = g.Id;
                if (g.Kind[side] == GUnits)
                {
                    g.Hits[side]++;
                    if (g.Hits[side] >= 6 && g.Val[side] < GateCap()) { g.Hits[side] = 0; g.Val[side]++; }
                }
                return;
            }
        }

        bool HitEnemy(float x, float y, float dmg)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                float dx = x - e.X, dy = y - e.Y;
                if (dx * dx + dy * dy > e.R * e.R) continue;
                e.Hp -= dmg;
                if (e.Hp <= 0 && e.Alive) Kill(e);
                return true;
            }
            return false;
        }

        static readonly Color[] KindColor = { Shapes.Hex(0x8bc34a), Shapes.Hex(0x9575cd), Shapes.Hex(0xe5533e) };

        void Kill(Enemy e)
        {
            e.Alive = false;
            Enemies.Remove(e);
            Kills++;
            WaveDone++;
            Score += e.Kind == 0 ? 10 : e.Kind == 1 ? 50 : 1000;
            CoinsF += (e.Kind == 0 ? 1 : e.Kind == 1 ? 5 : 60) * (1 + CoinBonus);
            Pop(e.X, e.Y, KindColor[e.Kind], e.Kind == 2 ? 24 : 8, e.R);
            if (e.Kind == 2) Text(e.X, e.Y, "+1000");
        }

        void UpdateEnemies(float dt)
        {
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = Enemies[i];
                e.Y += e.Speed * dt;
                e.X += (float)Math.Sin(Time * 3 + e.Phase) * 10 * dt;
                if (e.Y > H * 0.45f)
                {
                    float d = SquadX - e.X, step = 45 * dt;
                    e.X += Math.Abs(d) < step ? d : Math.Sign(d) * step;
                }
                if (e.Y + e.R * 0.6f < SquadTop) continue;

                // encostou na frota
                Enemies.RemoveAt(i);
                e.Alive = false;
                WaveDone++;
                int dmg = e.Kind == 0 ? 1 : e.Kind == 1 ? 3 : Math.Max(8, Units / 2);
                if (Shield > 0)
                {
                    Shield--;
                    Text(e.X, SquadTop - 20, "Para-choque!");
                }
                else
                {
                    Units -= dmg;
                    Text(e.X, SquadTop - 20, "−" + dmg);
                }
                Pop(e.X, e.Y, KindColor[e.Kind], 10, e.R);
                if (Units <= 0)
                {
                    Units = 0;
                    Over = true;
                    return;
                }
            }
        }

        void UpdateGates(float dt)
        {
            for (int i = Gates.Count - 1; i >= 0; i--)
            {
                Gate g = Gates[i];
                g.Y += 80 * dt;
                if (g.Y < SquadTop) continue;
                int side = SquadX < W / 2 ? 0 : 1;
                Apply(g.Kind[side], g.Val[side]);
                Gates.RemoveAt(i);
                if (Units <= 0) { Units = 0; Over = true; return; }
            }
        }

        void Apply(int kind, int val)
        {
            string big, small;
            GateText(kind, val, out big, out small);
            switch (kind)
            {
                case GUnits: Units = Math.Max(0, Math.Min(999, Units + val)); break;
                case GDouble: Units = Math.Min(999, Units * 2); break;
                case GDamage: Damage += 1; break;
                case GRate: Rate *= 1.15f; break;
                case GJet: if (Jets < 3) Jets++; else Damage += 1; break;
                case GShield: Shield++; ShieldMax++; break;
            }
            string label = kind == GUnits ? big + " caminhões" : kind == GDouble ? "×2 caminhões" : kind == GRate ? "Cadência +15%" : kind == GShield ? "Para-choque +1" : big;
            Picked.Insert(0, label);
            if (Picked.Count > 6) Picked.RemoveAt(Picked.Count - 1);
            Text(SquadX, SquadTop - 40, label);
        }

        void Pop(float x, float y, Color c, int n, float r)
        {
            for (int i = 0; i < n && Particles.Count < 60; i++)
            {
                Particle p = new Particle();
                double a = R01() * Math.PI * 2;
                float sp = 60 + R01() * 120 + r * 2;
                p.X = x; p.Y = y;
                p.Vx = (float)Math.Cos(a) * sp;
                p.Vy = (float)Math.Sin(a) * sp;
                p.MaxLife = p.Life = 0.35f + R01() * 0.3f;
                p.Size = 4 + R01() * 5 + r * 0.08f;
                p.Color = c;
                Particles.Add(p);
            }
        }

        void Text(float x, float y, string s)
        {
            Particle p = new Particle();
            p.X = x; p.Y = y; p.Vy = -40;
            p.MaxLife = p.Life = 1.1f;
            p.Text = s;
            Particles.Add(p);
        }

        void UpdateParticles(float dt)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                Particle p = Particles[i];
                p.Life -= dt;
                if (p.Life <= 0) { Particles.RemoveAt(i); continue; }
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                p.Vx *= 0.92f;
                p.Vy *= 0.92f;
            }
        }

        public bool BossAlive()
        {
            foreach (Enemy e in Enemies) if (e.Kind == 2) return true;
            return false;
        }
    }
}
