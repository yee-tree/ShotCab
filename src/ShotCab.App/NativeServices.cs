using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal static class Native
    {
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] internal static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool AddClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool RemoveClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr h, int index, int value);
        [DllImport("shell32.dll")] internal static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string text);
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
        [StructLayout(LayoutKind.Sequential)] internal struct AppBarData { public uint Size; public IntPtr Hwnd; public uint CallbackMessage; public uint Edge; public Rect Rect; public IntPtr Param; }
        internal static bool IsFullscreen(IntPtr exclude)
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero || h == exclude || h == GetShellWindow() || h == GetDesktopWindow() || !GetWindowRect(h, out var r)) return false;
            var bounds = Screen.FromHandle(h).Bounds;
            return r.Rectangle == bounds;
        }
    }

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();
        public event Action ClipboardChanged;
        public HotkeyWindow() { CreateHandle(new CreateParams { Caption = "ShotCab.Events", Parent = new IntPtr(-3) }); Native.AddClipboardFormatListener(Handle); }
        public string Configure(IEnumerable<KeyValuePair<string, Action>> bindings)
        {
            foreach (var id in actions.Keys) Native.UnregisterHotKey(Handle, id);
            actions.Clear(); var failed = new List<string>(); int next = 1;
            foreach (var pair in bindings)
            {
                try
                {
                    var keys = (Keys)new KeysConverter().ConvertFromInvariantString(pair.Key);
                    uint modifiers = 0x4000;
                    if ((keys & Keys.Control) != 0) modifiers |= 2;
                    if ((keys & Keys.Alt) != 0) modifiers |= 1;
                    if ((keys & Keys.Shift) != 0) modifiers |= 4;
                    if (!Native.RegisterHotKey(Handle, next, modifiers, (uint)(keys & Keys.KeyCode))) failed.Add(pair.Key);
                    else actions[next] = pair.Value;
                    next++;
                }
                catch { failed.Add(pair.Key); }
            }
            return string.Join("、", failed);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && actions.TryGetValue(m.WParam.ToInt32(), out var action)) action();
            if (m.Msg == 0x031D) ClipboardChanged?.Invoke();
            base.WndProc(ref m);
        }
        public void Dispose() { foreach (var id in actions.Keys) Native.UnregisterHotKey(Handle, id); Native.RemoveClipboardFormatListener(Handle); DestroyHandle(); }
    }

    internal sealed class ClipboardPayload : DataObject, IDisposable
    {
        private readonly List<IDisposable> resources = new List<IDisposable>();
        internal T Own<T>(T value) where T : IDisposable { resources.Add(value); return value; }
        public void Dispose() { foreach (var resource in resources) resource.Dispose(); resources.Clear(); }
    }

    internal sealed class ClipboardService
    {
        private readonly string exports;
        private uint sequence;
        public string ItemId { get; private set; }
        public bool OwnsClipboard => ItemId != null && sequence == Native.GetClipboardSequenceNumber();
        public ClipboardService(string root) { exports = Path.Combine(root, "exports"); Directory.CreateDirectory(exports); }
        public ClipboardPayload Build(Bitmap image, bool asFile, bool cut, string extension = "png", int quality = 90)
        {
            var data = new ClipboardPayload();
            try
            {
            data.SetData(DataFormats.Bitmap, true, data.Own(new Bitmap(image)));
            // Explicit DIB supports applications that do not accept PNG or OLE bitmap conversion.
            using (var compatible = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
            using (var g = Graphics.FromImage(compatible))
            using (var bmp = new MemoryStream())
            {
                g.Clear(Color.White); g.DrawImageUnscaled(image, 0, 0);
                compatible.Save(bmp, System.Drawing.Imaging.ImageFormat.Bmp);
                byte[] bytes = bmp.ToArray(); data.SetData(DataFormats.Dib, false, data.Own(new MemoryStream(bytes, 14, bytes.Length - 14)));
            }
            using (var png = new MemoryStream()) { image.Save(png, System.Drawing.Imaging.ImageFormat.Png); data.SetData("PNG", false, data.Own(new MemoryStream(png.ToArray()))); }
            if (asFile)
            {
                var path = Path.Combine(exports, "ShotCab-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "." + extension);
                Images.Export(image, path, quality);
                data.SetFileDropList(new StringCollection { path });
                data.SetData("Preferred DropEffect", data.Own(new MemoryStream(BitConverter.GetBytes(cut ? 2 : 1))));
            }
            return data;
            }
            catch { data.Dispose(); throw; }
        }
        public void Copy(Bitmap image, string id, bool cut = false, string extension = "png", int quality = 90)
        {
            ItemId = null; sequence = 0;
            using (var data = Build(image, cut, cut, extension, quality))
            {
                Clipboard.SetDataObject(data, true, 10, 100);
                if (!Clipboard.ContainsImage()) throw new ExternalException("图片未能写入剪贴板，请稍后重试复制。");
            }
            ItemId = id; sequence = Native.GetClipboardSequenceNumber();
        }
        public void CopyText(string text) { Clipboard.SetText(text ?? ""); ItemId = null; }
        public void Cleanup()
        {
            foreach (var file in Directory.EnumerateFiles(exports))
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-3))
                    try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    internal static class Images
    {
        public static Bitmap Thumbnail(string path)
        {
            var thumbnail = Path.Combine(Path.GetDirectoryName(path), "thumbnail.png");
            if (File.Exists(thumbnail)) return Load(thumbnail);
            Bitmap result;
            using (var full = Load(path))
            {
                double scale = Math.Min(1, Math.Min(320d / full.Width, 220d / full.Height));
                result = new Bitmap(full, Math.Max(1, (int)(full.Width * scale)), Math.Max(1, (int)(full.Height * scale)));
            }
            try { result.Save(thumbnail, System.Drawing.Imaging.ImageFormat.Png); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Runtime.InteropServices.ExternalException) { /* Cache failure must not prevent viewing. */ }
            return result;
        }
        public static Bitmap Load(string path) { using (var image = Image.FromFile(path)) return new Bitmap(image); }
        public static byte[] Png(Image image) { using (var stream = new MemoryStream()) { image.Save(stream, System.Drawing.Imaging.ImageFormat.Png); return stream.ToArray(); } }
        public static Bitmap FromBytes(byte[] bytes) { using (var stream = new MemoryStream(bytes)) using (var image = Image.FromStream(stream)) return new Bitmap(image); }
        public static void Export(Image image, string path, int quality)
        {
            if (Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                using (var bitmap = new Bitmap(image.Width, image.Height))
                using (var g = Graphics.FromImage(bitmap))
                using (var parameters = new System.Drawing.Imaging.EncoderParameters(1))
                {
                    g.Clear(Color.White); g.DrawImageUnscaled(image, 0, 0);
                    parameters.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)Math.Max(1, Math.Min(100, quality)));
                    var codec = Array.Find(System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders(), c => c.MimeType == "image/jpeg");
                    bitmap.Save(path, codec, parameters);
                }
            }
            else image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
