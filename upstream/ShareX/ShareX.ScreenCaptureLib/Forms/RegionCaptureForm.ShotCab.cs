// ShotCab extensions to ShareX 17.1.0. Licensed under the upstream GPL terms.
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ShareX.ScreenCaptureLib
{
    public sealed partial class RegionCaptureForm
    {
        // Invoked after the upstream toolbar is built but before its window first appears.
        public event Action ShotCabToolbarPreparing;
        internal void PrepareShotCabToolbar() => ShotCabToolbarPreparing?.Invoke();
        public bool ShotCabHasAnnotations => ShapeManager.Shapes.Any(s => s.ShapeCategory == ShapeCategory.Drawing || s.ShapeCategory == ShapeCategory.Effect);
        public bool ShotCabHasRedactions => ShapeManager.Shapes.Any(IsRedaction);
        private static bool IsRedaction(BaseShape s) => s is SolidMaskEffectShape || (s is BlurEffectShape blur && blur.BlurRadius > 1) || (s is PixelateEffectShape pixel && pixel.PixelSize > 1);

        public Bitmap ShotCabBaseImage() => new Bitmap(Canvas);

        // Default capture mode initializes a rectangle regardless of LastRegionTool.
        // The tray's freehand action must select its tool after that initialization.
        public void ShotCabSelectRegionTool(ShapeType tool)
        {
            if (tool != ShapeType.RegionRectangle && tool != ShapeType.RegionFreehand)
                throw new ArgumentOutOfRangeException(nameof(tool));
            ShapeManager.CurrentTool = tool;
        }

        public void ShotCabReplaceCanvas(Bitmap image) => ShapeManager.ShotCabReplaceCanvas(image);

        public string ExportShotCabDocument()
            => ExportShotCabDocument(CanvasRectangle.Location);

        private string ExportShotCabDocument(PointF origin)
        {
            var shapes = new JArray();
            foreach (var current in ShapeManager.Shapes.Where(s => s.ShapeCategory == ShapeCategory.Drawing || s.ShapeCategory == ShapeCategory.Effect))
            {
                using (var shape = current.Duplicate())
                {
                    shape.Move(-origin.X, -origin.Y);
                    var values = new JObject();
                    foreach (var property in PersistentProperties(shape.GetType()))
                        values[property.Name] = JToken.FromObject(property.GetValue(shape) ?? JValue.CreateNull());
                    var item = new JObject { ["Type"] = shape.ShapeType.ToString(), ["Properties"] = values };
                    if (shape is FreehandDrawingShape freehand) item["Points"] = JToken.FromObject(freehand.ShotCabPoints);
                    if (shape is ImageDrawingShape image && image.Image != null)
                        using (var stream = new MemoryStream()) { image.Image.Save(stream, ImageFormat.Png); item["ImagePng"] = Convert.ToBase64String(stream.ToArray()); }
                    shapes.Add(item);
                }
            }
            return new JObject { ["Version"] = 1, ["Shapes"] = shapes }.ToString(Formatting.None);
        }

        public Bitmap ShotCabCaptureBase(out string document)
        {
            Bitmap result;
            if ((Result == RegionResult.Region || Result == RegionResult.LastRegion) && (regionFillPath != null || LastRegionFillPath != null))
            {
                result = RegionCaptureTasks.ApplyRegionPathToImage(Canvas, Result == RegionResult.LastRegion ? LastRegionFillPath : regionFillPath, out var rect);
                document = ExportShotCabDocument(rect.Location);
            }
            else if (Result == RegionResult.Monitor || Result == RegionResult.ActiveMonitor)
            {
                var bounds = RectangleToClient(GetSelectedRectangle());
                if (bounds.IsEmpty) throw new InvalidOperationException("所选显示器已不可用。");
                result = ShareX.HelpersLib.ImageHelpers.CropBitmap(Canvas, bounds);
                document = ExportShotCabDocument(bounds.Location);
            }
            else
            {
                document = ExportShotCabDocument(Point.Empty);
                result = Result == RegionResult.Close ? null : new Bitmap(Canvas);
            }
            return result;
        }

        public void ShotCabAddCommand(string text, Action action) => ShapeManager.ShotCabAddCommand(text, action);
        public void ShotCabOrganizeTools(bool dark = true)
        {
            canvasBackgroundColor = dark ? System.Drawing.Color.FromArgb(29, 29, 31) : System.Drawing.Color.FromArgb(242, 242, 247);
            ShotCabApplyChromeTheme(dark);
            ShapeManager.ShotCabOrganizeTools(dark);
        }
        public void ShotCabEffectStyleDialog() => ShapeManager.ShotCabEffectStyleDialog();

        public void RestoreShotCabDocument(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var document = JObject.Parse(json);
            if ((int?)document["Version"] != 1) throw new InvalidDataException("不支持的标注文档版本。");
            var shapes = document["Shapes"] as JArray ?? throw new InvalidDataException("缺少标注列表。");
            if (shapes.Count > 10000) throw new InvalidDataException("标注数量过多。");
            var restored = new System.Collections.Generic.List<BaseShape>();
            try
            {
            foreach (var token in shapes)
            {
                if (!Enum.TryParse((string)token["Type"], out ShapeType type) || !Enum.IsDefined(typeof(ShapeType), type)) throw new InvalidDataException("不支持的标注类型。");
                var shape = ShapeManager.CreateShape(type);
                if (shape.ShapeCategory != ShapeCategory.Drawing && shape.ShapeCategory != ShapeCategory.Effect) { shape.Dispose(); throw new InvalidDataException("非法标注对象。"); }
                try
                {
                    foreach (var property in PersistentProperties(shape.GetType()))
                    {
                        var value = token["Properties"]?[property.Name];
                        if (value != null) property.SetValue(shape, value.ToObject(property.PropertyType));
                    }
                    if (shape is FreehandDrawingShape freehand && token["Points"] != null) freehand.ShotCabPoints = token["Points"].ToObject<PointF[]>();
                    if (shape is ImageDrawingShape image && token["ImagePng"] != null)
                    {
                        var rect = shape.Rectangle;
                        using (var stream = new MemoryStream(Convert.FromBase64String((string)token["ImagePng"])))
                        using (var bitmap = new Bitmap(stream)) image.SetImage(new Bitmap(bitmap), false);
                        shape.Rectangle = rect;
                    }
                    shape.Move(CanvasRectangle.X, CanvasRectangle.Y);
                    restored.Add(shape);
                }
                catch { shape.Dispose(); throw; }
            }
            }
            catch { foreach (var shape in restored) shape.Dispose(); throw; }
            ShapeManager.Shapes.AddRange(restored);
            Invalidate();
        }

        private static PropertyInfo[] PersistentProperties(Type type) => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead && p.GetSetMethod(true) != null &&
                p.Name != "LastPosition" && p.Name != "StartPosition" && p.Name != "EndPosition" &&
                (p.PropertyType.IsValueType || p.PropertyType == typeof(string) || p.PropertyType == typeof(PointF[]) || p.PropertyType == typeof(TextDrawingOptions)))
            .OrderBy(p => p.Name == "Rectangle" ? 0 : 1).ToArray();

        // Creates a new redacted base plus remaining editable layers. Caller persists
        // atomically and closes this editor, so its old undo stack cannot undo redaction.
        public Bitmap ShotCabRedactedBase(out string remainingDocument)
        {
            var selected = ShapeManager.Shapes.Where(IsRedaction).ToArray();
            var output = new Bitmap(Canvas);
            ShapeManager.MoveAll(-CanvasRectangle.X, -CanvasRectangle.Y);
            try
            {
                using (var g = Graphics.FromImage(output)) foreach (BaseEffectShape s in selected) s.OnDrawFinal(g, output);
            }
            finally { ShapeManager.MoveAll(CanvasRectangle.X, CanvasRectangle.Y); }
            // Only export the remaining layers; restore live state even on serialization failure.
            var saved = ShapeManager.Shapes.ToArray();
            try { foreach (var s in selected) ShapeManager.Shapes.Remove(s); remainingDocument = ExportShotCabDocument(); }
            finally { ShapeManager.Shapes.Clear(); ShapeManager.Shapes.AddRange(saved); }
            return output;
        }
    }
}
