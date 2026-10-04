using System;
using System.Collections.Generic;
using System.Drawing;

namespace Pintinho
{
    // Cobrinha clássica: grade 24x24, atravessa as bordas, só morre ao morder o próprio corpo.
    class Snake
    {
        public const int N = 24;

        public readonly List<Point> Body = new List<Point>(); // cabeça primeiro
        public Point Dir = new Point(1, 0);
        public Point Food;
        public int Score, Eaten;
        public bool Over;
        public float Interval = 0.17f; // segundos por passo (diminui a cada comidinha)

        readonly List<Point> pending = new List<Point>();
        readonly Random rnd;

        public Snake(int seed)
        {
            rnd = new Random(seed);
            for (int i = 0; i < 4; i++) Body.Add(new Point(8 - i, N / 2));
            PlaceFood();
        }

        // Guarda até 2 viradas, para não perder toques rápidos. Ignora meia-volta.
        public void Turn(int dx, int dy)
        {
            Point last = pending.Count > 0 ? pending[pending.Count - 1] : Dir;
            if (dx == -last.X && dy == -last.Y) return;
            if (dx == last.X && dy == last.Y) return;
            if (pending.Count < 2) pending.Add(new Point(dx, dy));
        }

        public void Step()
        {
            if (Over) return;
            if (pending.Count > 0)
            {
                Dir = pending[0];
                pending.RemoveAt(0);
            }
            Point h = Body[0];
            Point nh = new Point((h.X + Dir.X + N) % N, (h.Y + Dir.Y + N) % N);
            bool eat = nh == Food;
            int check = eat ? Body.Count : Body.Count - 1; // o rabo sai do lugar neste passo
            for (int i = 0; i < check; i++)
                if (Body[i] == nh) { Over = true; return; }

            Body.Insert(0, nh);
            if (eat)
            {
                Eaten++;
                Score += 10 + Eaten / 5;
                Interval = Math.Max(0.06f, Interval * 0.97f);
                PlaceFood();
            }
            else Body.RemoveAt(Body.Count - 1);
        }

        void PlaceFood()
        {
            if (Body.Count >= N * N) { Over = true; return; }
            while (true)
            {
                Point p = new Point(rnd.Next(N), rnd.Next(N));
                if (!Body.Contains(p)) { Food = p; return; }
            }
        }
    }
}
