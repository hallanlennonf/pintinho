using System;
using System.IO;
using System.Text;

namespace Pintinho
{
    // Jogador automático do Comboio para ajustar a dificuldade sem precisar jogar:
    //   Pintinho.exe --simular resultado.txt
    // Joga várias partidas com 3 níveis de oficina e anota em que onda cada uma terminou.
    static class Sim
    {
        public static void Run(string outFile)
        {
            int[][] profiles = {
                new int[] { 0, 0, 0, 0, 0, 0 },
                new int[] { 2, 2, 2, 1, 2, 1 },
                new int[] { 4, 4, 4, 2, 4, 3 }
            };
            string[] names = { "oficina zerada", "oficina média", "oficina no máximo" };
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("HpGrowth=" + Game.HpGrowth + " HpPerUnit=" + Game.HpPerUnit + " SpeedPerWave=" + Game.SpeedPerWave + " DamageExponent=" + Game.DamageExponent);
            for (int p = 0; p < profiles.Length; p++)
            {
                int runs = 12, sum = 0, min = 999, max = 0, coins = 0, maxUnits = 0;
                double time = 0;
                for (int r = 0; r < runs; r++)
                {
                    Game g = new Game(profiles[p], 1000 + r);
                    int peak = 0;
                    while (!g.Over && g.Time < 1800)
                    {
                        g.SquadTarget = Target(g);
                        g.Step(1 / 60f);
                        if (g.Units > peak) peak = g.Units;
                    }
                    sum += g.Wave;
                    min = Math.Min(min, g.Wave);
                    max = Math.Max(max, g.Wave);
                    coins += g.Coins;
                    time += g.Time;
                    maxUnits = Math.Max(maxUnits, peak);
                }
                sb.AppendLine(string.Format("{0}: onda média {1:0.0} (mín {2}, máx {3}), duração média {4:0}s, moedas médias {5}, maior frota {6}",
                    names[p], sum / (float)runs, min, max, time / runs, coins / runs, maxUnits));
            }
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(true));
        }

        // Estratégia simples: escolhe o melhor lado da plaquinha que está chegando; senão,
        // vai para baixo da gosma mais perigosa.
        static float Target(Game g)
        {
            Gate next = null;
            foreach (Gate gt in g.Gates)
                if (gt.Y < g.SquadTop && gt.Y > g.SquadTop - 320 && (next == null || gt.Y > next.Y)) next = gt;
            if (next != null)
            {
                float a = Score(g, next.Kind[0], next.Val[0]), b = Score(g, next.Kind[1], next.Val[1]);
                return a >= b ? Game.GateLeft[0] + Game.GateWidth / 2 : Game.GateLeft[1] + Game.GateWidth / 2;
            }
            Enemy worst = null;
            foreach (Enemy e in g.Enemies)
                if (worst == null || e.Y > worst.Y) worst = e;
            return worst != null ? worst.X : Game.W / 2;
        }

        static float Score(Game g, int kind, int val)
        {
            if (kind == Game.GUnits) return val;
            if (kind == Game.GDouble) return g.Units;
            return 6;
        }
    }
}
