using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace Pintinho
{
    // Preferências, num arquivo de texto simples (chave=valor).
    static class Config
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pintinho");
        static readonly string FilePath = Path.Combine(Dir, "config.txt");

        public const int MaxMyColors = 8;

        public static bool DarkTheme;
        public static int CozyPalette; // 0 = Básica

        // Joguinho Comboio: moedas, recorde e níveis da oficina
        // (frota, dano, cadência, jatos, ímã, para-choque)
        public static int GameCoins, GameRecordWave, GameRecordScore;
        public static readonly int[] GameUpg = new int[6];
        public static int SnakeRecord;
        public static readonly List<Color> MyColors = new List<Color>();

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim(), val = line.Substring(eq + 1).Trim();
                    if (key == "tema") DarkTheme = val == "escuro";
                    else if (key == "paleta") { int.TryParse(val, out CozyPalette); if (CozyPalette < 0 || CozyPalette > 3) CozyPalette = 0; }
                    else if (key == "jogo")
                    {
                        string[] p = val.Split(',');
                        int v;
                        if (p.Length > 0 && int.TryParse(p[0], out v)) GameCoins = v;
                        if (p.Length > 1 && int.TryParse(p[1], out v)) GameRecordWave = v;
                        if (p.Length > 2 && int.TryParse(p[2], out v)) GameRecordScore = v;
                        for (int i = 0; i < GameUpg.Length && i + 3 < p.Length; i++)
                            if (int.TryParse(p[i + 3], out v)) GameUpg[i] = v;
                    }
                    else if (key == "cobra") int.TryParse(val, out SnakeRecord);
                    else if (key == "minhascores")
                    {
                        MyColors.Clear();
                        foreach (string hex in val.Split(','))
                        {
                            int rgb;
                            string h = hex.Trim().TrimStart('#');
                            if (h.Length == 6 && int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                                MyColors.Add(Shapes.Hex(rgb));
                        }
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                sb.Append("tema=").Append(DarkTheme ? "escuro" : "claro").Append("\r\n");
                sb.Append("paleta=").Append(CozyPalette).Append("\r\n");
                sb.Append("jogo=").Append(GameCoins).Append(',').Append(GameRecordWave).Append(',').Append(GameRecordScore);
                foreach (int u in GameUpg) sb.Append(',').Append(u);
                sb.Append("\r\n");
                sb.Append("cobra=").Append(SnakeRecord).Append("\r\n");
                sb.Append("minhascores=");
                for (int i = 0; i < MyColors.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(Shapes.ToHex(MyColors[i]));
                }
                sb.Append("\r\n");
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch { }
        }

        public static void AddMyColor(Color c)
        {
            for (int i = MyColors.Count - 1; i >= 0; i--)
                if (MyColors[i].ToArgb() == c.ToArgb()) MyColors.RemoveAt(i);
            MyColors.Insert(0, c);
            while (MyColors.Count > MaxMyColors) MyColors.RemoveAt(MyColors.Count - 1);
            Save();
        }
    }
}
