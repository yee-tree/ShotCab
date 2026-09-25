using System;
using System.Drawing;

namespace ShotCab.App
{
    internal static class ImageCombiner
    {
        internal static Bitmap Combine(Bitmap first, Bitmap second, bool horizontal)
        {
            if (first == null || second == null) throw new ArgumentNullException(first == null ? nameof(first) : nameof(second));
            long width = horizontal ? (long)first.Width + second.Width : Math.Max(first.Width, second.Width);
            long height = horizontal ? Math.Max(first.Height, second.Height) : (long)first.Height + second.Height;
            if (width <= 0 || height <= 0 || width > int.MaxValue || height > int.MaxValue || width * height > 120000000)
                throw new InvalidOperationException("拼接图过大，请分批处理。");

            var output = new Bitmap((int)width, (int)height);
            using (var graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.White);
                graphics.DrawImageUnscaled(first, 0, 0);
                graphics.DrawImageUnscaled(second, horizontal ? first.Width : 0, horizontal ? 0 : first.Height);
            }
            return output;
        }
    }
}
