using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Pintinho
{
    // Atualização pelo GitHub: procura a release mais nova, baixa o instalador e roda em silêncio.
    // O instalador espera o app fechar (mutex) e abre a versão nova sozinho.
    static class Updater
    {
        const string Repo = "hallanlennonf/pintinho";
        const string ApiUrl = "https://api.github.com/repos/" + Repo + "/releases?per_page=10";

        public enum State { Parado, Verificando, Atualizado, Disponivel, Baixando, Erro }

        public static State Status = State.Parado;
        public static string LatestVersion = "";
        public static int Progress;
        static string setupUrl;

        public static event EventHandler Changed;

        static void Notify()
        {
            if (Changed != null) Changed(null, EventArgs.Empty);
        }

        static WebClient NewClient()
        {
            // Windows 10 antigo: liga TLS 1.2 (o GitHub exige)
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "Pintinho/" + AppInfo.Version;
            return wc;
        }

        // Precisa ser chamado na thread da interface: os eventos do WebClient voltam para ela.
        public static void Check()
        {
            if (Status == State.Verificando || Status == State.Baixando) return;
            Status = State.Verificando;
            Notify();
            WebClient wc = NewClient();
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            wc.DownloadStringCompleted += delegate(object s, DownloadStringCompletedEventArgs e)
            {
                wc.Dispose();
                if (e.Error != null)
                {
                    Status = State.Erro;
                    Notify();
                    return;
                }
                try { Parse(e.Result); }
                catch (Exception ex)
                {
                    Program.Log(ex);
                    Status = State.Erro;
                }
                Notify();
            };
            wc.DownloadStringAsync(new Uri(ApiUrl));
        }

        static void Parse(string json)
        {
            // A API devolve as releases da mais nova para a mais velha. Pega a primeira que tem instalador.
            MatchCollection tags = Regex.Matches(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            for (int i = 0; i < tags.Count; i++)
            {
                int start = tags[i].Index, end = i + 1 < tags.Count ? tags[i + 1].Index : json.Length;
                string block = json.Substring(start, end - start);
                Match asset = Regex.Match(block, "\"browser_download_url\"\\s*:\\s*\"([^\"]*Setup[^\"]*\\.exe)\"", RegexOptions.IgnoreCase);
                if (!asset.Success) continue;
                string ver = tags[i].Groups[1].Value;
                if (Compare(ver, AppInfo.Version) > 0)
                {
                    LatestVersion = Clean(ver);
                    setupUrl = asset.Groups[1].Value;
                    Status = State.Disponivel;
                }
                else
                {
                    LatestVersion = AppInfo.Version;
                    Status = State.Atualizado;
                }
                return;
            }
            Status = State.Atualizado;
        }

        static string Clean(string v)
        {
            v = v.Trim().TrimStart('v', 'V');
            int dash = v.IndexOf('-');
            return dash >= 0 ? v.Substring(0, dash) : v;
        }

        public static int Compare(string a, string b)
        {
            string[] pa = Clean(a).Split('.'), pb = Clean(b).Split('.');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int x = 0, y = 0;
                if (i < pa.Length) int.TryParse(pa[i], out x);
                if (i < pb.Length) int.TryParse(pb[i], out y);
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        // Baixa o instalador e chama onReady(caminho) quando terminar.
        public static void Download(Action<string> onReady)
        {
            if (Status != State.Disponivel || setupUrl == null) return;
            Status = State.Baixando;
            Progress = 0;
            Notify();
            string path = Path.Combine(Path.GetTempPath(), "Pintinho-Setup-" + LatestVersion + ".exe");
            WebClient wc = NewClient();
            wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
            {
                Progress = e.ProgressPercentage;
                Notify();
            };
            wc.DownloadFileCompleted += delegate(object s, AsyncCompletedEventArgs e)
            {
                wc.Dispose();
                if (e.Error != null || e.Cancelled)
                {
                    if (e.Error != null) Program.Log(e.Error);
                    Status = State.Erro;
                    Notify();
                    return;
                }
                onReady(path);
            };
            wc.DownloadFileAsync(new Uri(setupUrl), path);
        }

        public static bool RunInstaller(string path)
        {
            try
            {
                Process.Start(path, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
                return true;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                Status = State.Erro;
                Notify();
                return false;
            }
        }
    }
}
