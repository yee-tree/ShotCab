using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ShareX.HelpersLib;

namespace ShotCab.App
{
    internal static class Program
    {
        internal static bool Diagnostics;
        internal static void Trace(string text) { if (Diagnostics) File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"), DateTime.UtcNow.ToString("O") + " " + text + Environment.NewLine); }
        [STAThread]
        private static int Main(string[] args)
        {
            Diagnostics = Array.IndexOf(args, "--diagnose") >= 0; Trace("Starting");
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            ShareXResources.Name = "ShotCab";
            ShareXResources.Icon = (System.Drawing.Icon)Ui.AppIcon.Clone();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => { Trace(e.Exception.ToString()); Ui.Error(e.Exception); };
            if (args.Length > 0 && args[0] == "--smoke") return SmokeTests.Run(args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke"));
            if (args.Length > 1 && args[0] == "--performance") return SmokeTests.Performance(args[1]);
            if (args.Length > 1 && args[0] == "--clipboard-test") return SmokeTests.ClipboardRoundTrip(args[1]);
            if (args.Length > 2 && args[0] == "--ocr-test") return OcrRuntimeTests.Run(args[1], args[2]);
            using (var mutex = new Mutex(true, "Local\\ShotCab.Desktop", out var created))
            {
                if (!created) { MessageBox.Show("ShotCab 已在运行，请从系统托盘打开。", "ShotCab"); return 0; }
                try { using (var context = new CabinetContext()) { if (Array.IndexOf(args, "--history") >= 0) context.ShowHistory(); Application.Run(context); } return 0; }
                catch (Exception ex) { Trace(ex.ToString()); Ui.Error(ex); return 1; }
            }
        }
    }
}
