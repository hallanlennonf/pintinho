using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Pintinho
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }

            bool kiosk = true;
            string capture = null, icon = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--janela") kiosk = false;
                else if (args[i] == "--captura" && i + 1 < args.Length) capture = args[++i];
                else if (args[i] == "--icone" && i + 1 < args.Length) icon = args[++i];
            }

            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--simular")
                {
                    Sim.Run(args[i + 1]);
                    return;
                }

            if (icon != null)
            {
                AppIcon.Save(icon);
                return;
            }

            // O instalador espera este mutex sumir antes de copiar a versão nova.
            bool created;
            using (Mutex mutex = new Mutex(true, "PintinhoAppMutex", out created))
            {
                if (!created && capture == null) return; // já está aberto

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log(e.Exception); };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                MainForm form = new MainForm(kiosk);
                form.CaptureDir = capture;
                Application.Run(form);
                GC.KeepAlive(mutex);
            }
        }

        public static void Log(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Config.Dir);
                File.AppendAllText(Path.Combine(Config.Dir, "erros.log"), DateTime.Now + " v" + AppInfo.Version + "\r\n" + ex + "\r\n\r\n");
            }
            catch { }
        }
    }
}
