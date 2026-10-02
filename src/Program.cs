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
            string capture = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--janela") kiosk = false;
                else if (args[i] == "--captura" && i + 1 < args.Length) capture = args[++i];
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log(e.Exception); };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MainForm form = new MainForm(kiosk);
            form.CaptureDir = capture;
            Application.Run(form);
        }

        public static void Log(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Config.Dir);
                File.AppendAllText(Path.Combine(Config.Dir, "erros.log"), DateTime.Now + "\r\n" + ex + "\r\n\r\n");
            }
            catch { }
        }
    }
}
