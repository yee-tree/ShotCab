using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShareX.ScreenCaptureLib;

namespace ShotCab.App
{
    internal static class OcrRuntimeTests
    {
        internal static int Run(string output, string worker)
        {
            Directory.CreateDirectory(output);
            var log = new StringBuilder();
            try
            {
                using (var app = new CabinetContext(Path.Combine(output, "data"), false))
                using (var image = new Bitmap(720, 220))
                {
                    app.Settings.OcrExecutablePath = Path.GetFullPath(worker);
                    using (var g = Graphics.FromImage(image))
                    using (var font = Ui.Font(30))
                    { g.Clear(Color.White); g.DrawString("ShotCab 图柜", font, Brushes.Black, 30, 30); g.DrawString("Screenshot Cabinet 2026", font, Brushes.Black, 30, 110); }
                    Task.Run(async () =>
                    {
                        var time = Stopwatch.StartNew();
                        var result = await OcrService.Recognize(app, image, CancellationToken.None);
                        if (!result.Text.Contains("ShotCab") || !result.Text.Contains("图柜") || !result.Text.Contains("2026")) throw new Exception("Mixed language sample did not match: " + result.Text);
                        if (!result.Text.Contains("Screenshot Cabinet 2026")) throw new Exception("OCR block reading order or spacing is incorrect: " + result.Text);
                        var characters = result.Lines.SelectMany(line => line.Chars).ToArray();
                        if (characters.Length < 20 || characters.Any(c => c.Points == null || c.Points.Length < 3)) throw new Exception("Per-character polygons missing");
                        if (characters.SelectMany(c => c.Points).Any(p => p.X < 0 || p.Y < 0 || p.X > image.Width || p.Y > image.Height)) throw new Exception("Character coordinates escaped the input image");
                        log.AppendLine("PASS actual mixed Chinese/English worker result and character polygons; ms=" + time.ElapsedMilliseconds);
                        EnsureClean(app);
                        using (var cancel = new CancellationTokenSource())
                        {
                            cancel.CancelAfter(30); bool cancelled = false;
                            try { await OcrService.Recognize(app, image, cancel.Token); }
                            catch (OperationCanceledException) { cancelled = true; }
                            if (!cancelled) throw new Exception("Running OCR was not cancelled");
                        }
                        EnsureClean(app); log.AppendLine("PASS running cancellation and job-directory cleanup");
                        using (var cancel = new CancellationTokenSource())
                        {
                            cancel.Cancel(); bool cancelled = false;
                            try { await OcrService.Recognize(app, image, cancel.Token); }
                            catch (OperationCanceledException) { cancelled = true; }
                            if (!cancelled) throw new Exception("Pre-cancelled OCR started");
                        }
                        app.Settings.OcrExecutablePath = Path.Combine(output, "not-installed", "ShotCab.Ocr.exe");
                        bool missing = false;
                        try { await OcrService.Recognize(app, image, CancellationToken.None); }
                        catch (FileNotFoundException) { missing = true; }
                        if (!missing) throw new Exception("Missing component was not reported");
                        EnsureClean(app); log.AppendLine("PASS pre-cancellation and missing-component failure");
                    }).GetAwaiter().GetResult();
                    app.Settings.OcrExecutablePath = Path.GetFullPath(worker);
                    OcrForm view;
                    using (var temporary = new Bitmap(image)) view = new OcrForm(app, temporary, null);
                    using (view)
                    {
                        view.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                        view.Location = new Point(-10000, -10000); view.Show();
                        var text = (System.Windows.Forms.TextBox)typeof(OcrForm).GetField("text", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(view);
                        var wait = Stopwatch.StartNew();
                        while (!text.Text.Contains("2026") && wait.ElapsedMilliseconds < 20000) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(20); }
                        if (!text.Text.Contains("图柜")) throw new Exception("OCR window did not display the real worker result");
                        var fit = view.Controls.OfType<System.Windows.Forms.FlowLayoutPanel>().SelectMany(p => p.Controls.OfType<System.Windows.Forms.Button>()).Single(b => b.Text == "适应窗口");
                        fit.PerformClick(); System.Windows.Forms.Application.DoEvents();
                        using (var rendered = new Bitmap(view.Width, view.Height)) { view.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size)); rendered.Save(Path.Combine(output, "ocr-window.png")); }
                        view.Close();
                    }
                    EnsureClean(app); log.AppendLine("PASS actual OCR window, disposed caller-image lifetime, fit action and render");

                    // Exercise the real editor entry point with automatic OCR enabled.
                    // The timer runs inside ShowDialog's UI loop and keeps the test off-screen.
                    app.Settings.AutoOcrOnEdit = true;
                    app.Settings.OcrPreviewSeconds = 0;
                    var probe = new EditorAutoOcrProbe();
                    System.Windows.Forms.Application.AddMessageFilter(probe);
                    try { app.Edit(new Bitmap(image)); }
                    finally { System.Windows.Forms.Application.RemoveMessageFilter(probe); }
                    if (!probe.Recognized) throw new Exception(probe.Failure ?? "Automatic editor OCR did not start");
                    EnsureClean(app); log.AppendLine("PASS automatic editor OCR, in-window result and selectable image text");
                }
                File.WriteAllText(Path.Combine(output, "ocr-tests.txt"), log.ToString()); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "ocr-tests.txt"), log + ex.ToString()); return 1; }
        }
        private static void EnsureClean(CabinetContext app)
        {
            string jobs = Path.Combine(app.Store.RootPath, "ocr-jobs");
            if (Directory.Exists(jobs) && Directory.EnumerateFileSystemEntries(jobs).Any()) throw new Exception("OCR temporary input or result survived completion");
        }

        private sealed class EditorAutoOcrProbe : System.Windows.Forms.IMessageFilter
        {
            private readonly Stopwatch watch = Stopwatch.StartNew();
            private bool moved, closing;
            public bool Recognized { get; private set; }
            public string Failure { get; private set; }

            public bool PreFilterMessage(ref System.Windows.Forms.Message message)
            {
                if (closing) return false;
                var editor = System.Windows.Forms.Application.OpenForms.OfType<RegionCaptureForm>()
                    .FirstOrDefault(form => form.IsEditorMode);
                if (editor == null || !editor.IsHandleCreated) return false;
                if (!moved) { editor.Location = new Point(-10000, -10000); moved = true; }
                var panel = editor.Controls.Find("ShotCabEditorOcr", true).FirstOrDefault();
                var field = editor.Controls.Find("ShotCabOcrText", true).OfType<System.Windows.Forms.TextBox>().FirstOrDefault();
                var selection = editor.Controls.Find("ShotCabOcrImageSelection", true).OfType<OcrCanvas>().FirstOrDefault();
                if (panel != null && panel.Visible && field != null && field.Text.Contains("ShotCab") && selection != null)
                {
                    selection.SelectAllText();
                    Recognized = selection.SelectedText.Contains("ShotCab") && selection.SelectedText.Contains("图柜");
                    if (!Recognized) Failure = "Image text selection did not use the real OCR polygons: " + selection.SelectedText;
                    Close(editor);
                }
                else if (watch.ElapsedMilliseconds > 20000)
                {
                    var status = editor.Controls.Find("ShotCabOcrStatus", true).OfType<System.Windows.Forms.Label>().FirstOrDefault();
                    Failure = "Automatic editor OCR timed out: panel=" + (panel != null) + ", text=" + field?.Text + ", status=" + status?.Text;
                    Close(editor);
                }
                return false;
            }

            private void Close(RegionCaptureForm editor)
            {
                closing = true;
                editor.BeginInvoke(new Action(() => { if (!editor.IsDisposed) editor.Close(); }));
            }
        }
    }
}
