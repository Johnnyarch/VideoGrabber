using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace VideoGrabber
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                CrashLog("unhandled: " + (e.ExceptionObject == null ? "" : e.ExceptionObject.ToString()));
            };
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                CrashLog("thread: " + e.Exception.ToString());
            };
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                CrashLog("main: " + ex.ToString());
            }
        }

        private static void CrashLog(string msg)
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(p, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg + Environment.NewLine);
            }
            catch { }
        }
    }
}