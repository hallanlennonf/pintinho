using System;
using System.IO;

namespace Pintinho
{
    // Preferências dos pais, num arquivo de texto simples (chave=valor).
    static class Config
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pintinho");
        static readonly string FilePath = Path.Combine(Dir, "config.txt");

        public static bool DarkTheme;

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
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, "tema=" + (DarkTheme ? "escuro" : "claro") + "\r\n");
            }
            catch { }
        }
    }
}
