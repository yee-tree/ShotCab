using System;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json.Linq;
using ShareX.ScreenCaptureLib;
internal class Program
{
    [STAThread] static int Main()
    {
        try
        {
            using (var input = new Bitmap(120, 80))
            {
                using (var g = Graphics.FromImage(input)) g.Clear(Color.White);
                var options = new RegionCaptureOptions();
                using (var first = new RegionCaptureForm(RegionCaptureMode.Editor, options, new Bitmap(input)))
                {
                    string json = "{\"Version\":1,\"Shapes\":[{\"Type\":\"DrawingRectangle\",\"Properties\":{\"Rectangle\":{\"X\":10,\"Y\":10,\"Width\":40,\"Height\":30},\"FillColor\":\"Red\",\"BorderSize\":0}}]}";
                    first.RestoreShotCabDocument(json);
                    if (!first.ShotCabHasAnnotations) throw new Exception("Restored shape missing");
                    string document = first.ExportShotCabDocument();
                    if (JObject.Parse(document)["Shapes"].Count() != 1) throw new Exception("Shape not serialized");
                    using (var second = new RegionCaptureForm(RegionCaptureMode.Editor, options, new Bitmap(input)))
                    {
                        second.RestoreShotCabDocument(document);
                        using (var image = second.GetResultImage()) if (image.GetPixel(20, 20).R != 255 || image.GetPixel(20, 20).G != 0) throw new Exception("Rectangle did not survive round trip");
                    }
                    bool rejected = false;
                    try { first.RestoreShotCabDocument("{\"Version\":999,\"Shapes\":[]}"); } catch (System.IO.InvalidDataException) { rejected = true; }
                    if (!rejected) throw new Exception("Unsupported document version accepted");

                    string beforeInvalidRestore = first.ExportShotCabDocument();
                    rejected = false;
                    try
                    {
                        first.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"DrawingEllipse\",\"Properties\":{\"Rectangle\":{\"X\":1,\"Y\":1,\"Width\":4,\"Height\":4}}},{\"Type\":\"ToolCrop\",\"Properties\":{}}]}");
                    }
                    catch (System.IO.InvalidDataException) { rejected = true; }
                    if (!rejected) throw new Exception("Illegal shape accepted");
                    if (first.ExportShotCabDocument() != beforeInvalidRestore) throw new Exception("Failed restore changed the live document");
                }

                using (var maskEditor = new RegionCaptureForm(RegionCaptureMode.Editor, options, new Bitmap(input)))
                {
                    maskEditor.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"EffectPixelate\",\"Properties\":{\"Rectangle\":{\"X\":10,\"Y\":10,\"Width\":30,\"Height\":20},\"PixelSize\":8}},{\"Type\":\"DrawingRectangle\",\"Properties\":{\"Rectangle\":{\"X\":60,\"Y\":20,\"Width\":20,\"Height\":20},\"FillColor\":\"Blue\",\"BorderSize\":0}}]}");
                    using (Bitmap baked = maskEditor.ShotCabRedactedBase(out string remaining))
                    {
                        JArray remainingShapes = (JArray)JObject.Parse(remaining)["Shapes"];
                        if (remainingShapes.Count != 1 || (string)remainingShapes[0]["Type"] != "DrawingRectangle") throw new Exception("Baking a mask did not retain other editable layers");
                    }
                    if (JObject.Parse(maskEditor.ExportShotCabDocument())["Shapes"].Count() != 2) throw new Exception("Baking a mask changed the live editor document");
                }
                using (var patterned = new Bitmap(120, 80))
                {
                    using (var g = Graphics.FromImage(patterned)) { g.Clear(Color.White); g.FillRectangle(Brushes.Red, 20, 20, 10, 10); }
                    string layers = "{\"Version\":1,\"Shapes\":[{\"Type\":\"EffectPixelate\",\"Properties\":{\"Rectangle\":{\"X\":10,\"Y\":10,\"Width\":40,\"Height\":40},\"PixelSize\":40}},{\"Type\":\"DrawingMagnify\",\"Properties\":{\"Rectangle\":{\"X\":10,\"Y\":10,\"Width\":40,\"Height\":40},\"MagnifyStrength\":200,\"BorderSize\":0}}]}";
                    using (var masked = new RegionCaptureForm(RegionCaptureMode.Editor, options, new Bitmap(patterned)))
                    {
                        masked.RestoreShotCabDocument(layers);
                        using (var composed = masked.GetResultImage())
                        using (var baked = masked.ShotCabRedactedBase(out var remaining))
                        using (var reopened = new RegionCaptureForm(RegionCaptureMode.Editor, options, new Bitmap(baked)))
                        {
                            reopened.RestoreShotCabDocument(remaining);
                            using (var after = reopened.GetResultImage())
                                for (int y = 18; y < 42; y++) for (int x = 18; x < 42; x++)
                                    if (composed.GetPixel(x, y) != after.GetPixel(x, y)) throw new Exception("Magnifier exposed pixels from the unredacted source");
                            if (baked.GetPixel(25, 25) == patterned.GetPixel(25, 25)) throw new Exception("Mask did not alter sensitive pixels");
                        }
                    }
                }
                using (var effects = new RegionCaptureForm(RegionCaptureMode.Editor, options, new Bitmap(input)))
                {
                    effects.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"EffectSolidMask\",\"Properties\":{\"Rectangle\":{\"X\":10.5,\"Y\":10.5,\"Width\":20,\"Height\":20}}},{\"Type\":\"EffectSpotlight\",\"Properties\":{\"Rectangle\":{\"X\":60,\"Y\":20,\"Width\":30,\"Height\":30},\"Darkness\":150}}]}");
                    if (!effects.ShotCabHasRedactions) throw new Exception("Solid mask missing from redaction state");
                    using (var composed = effects.GetResultImage())
                    {
                        if (composed.GetPixel(10, 10).ToArgb() != Color.Black.ToArgb()) throw new Exception("Fractional mask boundary exposed pixels");
                        if (composed.GetPixel(75, 35).ToArgb() != Color.White.ToArgb()) throw new Exception("Spotlight darkened the selected center");
                        if (composed.GetPixel(100, 60).R >= 200) throw new Exception("Spotlight did not darken outside");
                    }
                    using (var baked = effects.ShotCabRedactedBase(out var remaining))
                    {
                        if (baked.GetPixel(20, 20).ToArgb() != Color.Black.ToArgb()) throw new Exception("Solid mask was not baked");
                        if (baked.GetPixel(100, 60).ToArgb() != Color.White.ToArgb()) throw new Exception("Spotlight was incorrectly baked as redaction");
                        if ((string)JObject.Parse(remaining)["Shapes"][0]["Type"] != "EffectSpotlight") throw new Exception("Spotlight editable layer lost");
                        using (var reopened = new RegionCaptureForm(RegionCaptureMode.Editor, options, new Bitmap(baked)))
                        {
                            reopened.RestoreShotCabDocument(remaining);
                            if (reopened.ShotCabHasRedactions) throw new Exception("Spotlight falsely classified as redaction");
                        }
                    }
                }
            }
            CheckEffectStyleHistory();
            CheckEditorTimelineAndPointer();
            CheckCombinedCanvasUndo();
            CheckFreehandRegionMask();
            CheckDrawingStyleAndSampleCover();
            CheckScrollingOverlap();
            Console.WriteLine("PASS editable roundtrip, masks, style history, combined-canvas undo, freehand region mask, editor timeline and cursor"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void CheckEffectStyleHistory()
    {
        var options = new RegionCaptureOptions();
        using (var source = new Bitmap(80, 60))
        using (var form = new RegionCaptureForm(RegionCaptureMode.Editor, options, new Bitmap(source)))
        {
            form.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"EffectSolidMask\",\"Properties\":{\"Rectangle\":{\"X\":5,\"Y\":5,\"Width\":30,\"Height\":30}}}]}");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            object manager = typeof(RegionCaptureForm).GetProperty("ShapeManager", flags).GetValue(form);
            var type = manager.GetType();
            var shapes = (System.Collections.IList)type.GetProperty("Shapes", flags).GetValue(manager);
            type.GetProperty("CurrentTool", flags).SetValue(manager, ShapeType.EffectSolidMask);
            type.GetProperty("CurrentShape", flags).SetValue(manager, shapes[0]);
            type.GetMethod("ShotCabApplyEffectStyle", flags).Invoke(manager, new object[] { Color.FromArgb(0, 255, 0, 0), 500 });
            using (var image = form.GetResultImage()) if (image.GetPixel(15, 15).ToArgb() != Color.Red.ToArgb()) throw new Exception("Mask style was not forced opaque");
            if (options.AnnotationOptions.SolidMaskColor.A != 255 || options.AnnotationOptions.SpotlightDarkness != 255) throw new Exception("Style defaults not normalized or remembered");
            object history = type.GetField("history", flags).GetValue(manager);
            history.GetType().GetMethod("Undo", flags).Invoke(history, null);
            using (var image = form.GetResultImage()) if (image.GetPixel(15, 15).ToArgb() != Color.Black.ToArgb()) throw new Exception("Mask style undo failed");
            history.GetType().GetMethod("Redo", flags).Invoke(history, null);
            using (var image = form.GetResultImage()) if (image.GetPixel(15, 15).ToArgb() != Color.Red.ToArgb()) throw new Exception("Mask style redo failed");
        }
    }
    private static void CheckCombinedCanvasUndo()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        using (var source = new Bitmap(20, 10))
        using (var form = new RegionCaptureForm(RegionCaptureMode.Editor, new RegionCaptureOptions(), new Bitmap(source)))
        {
            form.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"DrawingRectangle\",\"Properties\":{\"Rectangle\":{\"X\":1,\"Y\":1,\"Width\":5,\"Height\":5}}}]}");
            form.ShotCabReplaceCanvas(new Bitmap(30, 10));
            if (form.Canvas.Size != new Size(30, 10) || form.ShotCabHasAnnotations)
                throw new Exception("Combined canvas did not replace the current image and flatten prior annotations");
            var manager = typeof(RegionCaptureForm).GetProperty("ShapeManager", flags).GetValue(form);
            manager.GetType().GetMethod("ShotCabJumpToHistory", flags).Invoke(manager, new object[] { 0 });
            if (form.Canvas.Size != new Size(20, 10) || !form.ShotCabHasAnnotations)
                throw new Exception("Undo did not restore the canvas and annotations before combining");
        }
    }
    private static void CheckFreehandRegionMask()
    {
        var region = new FreehandRegionShape();
        var points = (System.Collections.Generic.List<PointF>)typeof(FreehandRegionShape)
            .GetField("points", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(region);
        points.AddRange(new[] { new PointF(2, 2), new PointF(22, 2), new PointF(2, 22) });
        using (var source = new Bitmap(30, 30))
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            using (var graphics = Graphics.FromImage(source)) graphics.Clear(Color.Red);
            region.OnShapePathRequested(path, new RectangleF(2, 2, 20, 20));
            using (var result = RegionCaptureTasks.ApplyRegionPathToImage(source, path, out var bounds))
            {
                if (bounds.Width < 19 || bounds.Height < 19 || result.GetPixel(4, 4).A != 255 || result.GetPixel(18, 18).A != 0)
                    throw new Exception("Freehand region output kept pixels outside its drawn outline");
            }
        }
    }
    private static void CheckEditorTimelineAndPointer()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        using (var source = new Bitmap(80, 60))
        using (var form = new RegionCaptureForm(RegionCaptureMode.Editor, new RegionCaptureOptions(), new Bitmap(source)))
        {
            var manager = typeof(RegionCaptureForm).GetProperty("ShapeManager", flags).GetValue(form);
            var managerType = manager.GetType();
            if ((ShapeType)managerType.GetProperty("CurrentTool", flags).GetValue(manager) != ShapeType.ToolSelect)
                throw new Exception("Editor did not open with the select tool");
            managerType.GetProperty("CurrentTool", flags).SetValue(manager, ShapeType.ToolSelect);
            form.SetDefaultCursor();
            if (form.Cursor != System.Windows.Forms.Cursors.Default) throw new Exception("Select tool kept a crosshair cursor");
            managerType.GetProperty("CurrentTool", flags).SetValue(manager, ShapeType.DrawingRectangle);
            form.SetDefaultCursor();
            if (form.Cursor == System.Windows.Forms.Cursors.Default) throw new Exception("Drawing tool lost its drawing cursor");

            form.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"EffectSolidMask\",\"Properties\":{\"Rectangle\":{\"X\":5,\"Y\":5,\"Width\":30,\"Height\":30}}}]}");
            var shapes = (System.Collections.IList)managerType.GetProperty("Shapes", flags).GetValue(manager);
            managerType.GetProperty("CurrentTool", flags).SetValue(manager, ShapeType.EffectSolidMask);
            managerType.GetProperty("CurrentShape", flags).SetValue(manager, shapes[0]);
            managerType.GetMethod("ShotCabApplyEffectStyle", flags).Invoke(manager, new object[] { Color.Red, 120 });
            var history = managerType.GetField("history", flags).GetValue(manager);
            var historyType = history.GetType();
            Func<int> current = () => (int)historyType.GetProperty("ShotCabCurrentStep", flags).GetValue(history);
            Func<int> count = () => (int)historyType.GetProperty("ShotCabStepCount", flags).GetValue(history);
            if (count() != 2 || current() != 1) throw new Exception("Editor timeline did not include the edited state");
            historyType.GetMethod("ShotCabJumpTo", flags).Invoke(history, new object[] { 0 });
            using (var image = form.GetResultImage()) if (image.GetPixel(15, 15).ToArgb() != Color.Black.ToArgb()) throw new Exception("Jump to original timeline state failed");
            if (current() != 0) throw new Exception("Timeline cursor did not follow jump back");
            historyType.GetMethod("ShotCabJumpTo", flags).Invoke(history, new object[] { 1 });
            using (var image = form.GetResultImage()) if (image.GetPixel(15, 15).ToArgb() != Color.Red.ToArgb()) throw new Exception("Jump to edited timeline state failed");
            if (current() != 1) throw new Exception("Timeline cursor did not follow jump forward");
            historyType.GetMethod("ShotCabJumpTo", flags).Invoke(history, new object[] { 0 });
            shapes = (System.Collections.IList)managerType.GetProperty("Shapes", flags).GetValue(manager);
            managerType.GetProperty("CurrentTool", flags).SetValue(manager, ShapeType.EffectSolidMask);
            managerType.GetProperty("CurrentShape", flags).SetValue(manager, shapes[0]);
            managerType.GetMethod("ShotCabApplyEffectStyle", flags).Invoke(manager, new object[] { Color.Blue, 120 });
            if (count() != 2 || current() != 1 || (bool)historyType.GetProperty("CanRedo", flags).GetValue(history))
                throw new Exception("Editing after a history jump retained an abandoned future branch");
            using (var image = form.GetResultImage()) if (image.GetPixel(15, 15).ToArgb() != Color.Blue.ToArgb()) throw new Exception("New history branch was not applied");
        }
    }
    private static void CheckDrawingStyleAndSampleCover()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        using (var source = new Bitmap(100, 80))
        using (var form = new RegionCaptureForm(RegionCaptureMode.Editor, new RegionCaptureOptions(), new Bitmap(source)))
        {
            using (var g = Graphics.FromImage(form.Canvas)) g.Clear(Color.White);
            form.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"DrawingRectangle\",\"Properties\":{\"Rectangle\":{\"X\":10,\"Y\":10,\"Width\":50,\"Height\":40},\"BorderColor\":\"Red\",\"BorderSize\":4,\"FillColor\":\"Yellow\"}}]}");
            var manager = typeof(RegionCaptureForm).GetProperty("ShapeManager", flags).GetValue(form);
            var managerType = manager.GetType();
            var shapes = (System.Collections.IList)managerType.GetProperty("Shapes", flags).GetValue(manager);
            managerType.GetProperty("CurrentTool", flags).SetValue(manager, ShapeType.DrawingRectangle);
            managerType.GetProperty("CurrentShape", flags).SetValue(manager, shapes[0]);
            managerType.GetMethod("ShotCabApplyDrawingStyle", flags).Invoke(manager,
                new object[] { ShapeType.DrawingRectangle, Color.Blue, 6, Color.Transparent });
            var rectangle = (BaseDrawingShape)shapes[0];
            if (rectangle.BorderColor.ToArgb() != Color.Blue.ToArgb() || rectangle.BorderSize != 6 || rectangle.FillColor.A != 0)
                throw new Exception("Drawing style did not apply border, width and transparent interior");
            using (var image = form.GetResultImage())
                if (image.GetPixel(35, 30).ToArgb() != Color.White.ToArgb())
                    throw new Exception("Transparent drawing fill obscured the original image");
            var history = managerType.GetField("history", flags).GetValue(manager);
            var historyType = history.GetType();
            historyType.GetMethod("Undo", flags).Invoke(history, null);
            rectangle = (BaseDrawingShape)((System.Collections.IList)managerType.GetProperty("Shapes", flags).GetValue(manager))[0];
            if (rectangle.BorderColor.ToArgb() != Color.Red.ToArgb() || rectangle.FillColor.ToArgb() != Color.Yellow.ToArgb())
                throw new Exception("Undo did not restore the previous drawing style");
            historyType.GetMethod("Redo", flags).Invoke(history, null);
            rectangle = (BaseDrawingShape)((System.Collections.IList)managerType.GetProperty("Shapes", flags).GetValue(manager))[0];
            if (rectangle.BorderColor.ToArgb() != Color.Blue.ToArgb() || rectangle.FillColor.A != 0)
                throw new Exception("Redo did not restore the new drawing style");
        }
        using (var source = new Bitmap(80, 60))
        using (var form = new RegionCaptureForm(RegionCaptureMode.Editor, new RegionCaptureOptions(), new Bitmap(source)))
        {
            using (var g = Graphics.FromImage(form.Canvas)) g.Clear(Color.White);
            form.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"DrawingSpeechBalloon\",\"Properties\":{\"Rectangle\":{\"X\":12,\"Y\":10,\"Width\":50,\"Height\":35},\"Text\":\"\"}}]}");
            var manager = typeof(RegionCaptureForm).GetProperty("ShapeManager", flags).GetValue(form);
            var managerType = manager.GetType();
            var shapes = (System.Collections.IList)managerType.GetProperty("Shapes", flags).GetValue(manager);
            managerType.GetProperty("CurrentTool", flags).SetValue(manager, ShapeType.DrawingSpeechBalloon);
            managerType.GetProperty("CurrentShape", flags).SetValue(manager, shapes[0]);
            managerType.GetMethod("ShotCabApplyDrawingStyle", flags).Invoke(manager,
                new object[] { ShapeType.DrawingSpeechBalloon, Color.Blue, 4, Color.Transparent });
            var bubble = (BaseDrawingShape)shapes[0];
            if (bubble.BorderColor.ToArgb() != Color.Blue.ToArgb() || bubble.BorderSize != 4 || bubble.FillColor.A != 0)
                throw new Exception("Speech balloon lost its border or transparent interior");
            using (var image = form.GetResultImage())
                if (image.GetPixel(40, 25).ToArgb() != Color.White.ToArgb())
                    throw new Exception("Speech balloon's transparent interior obscured the image");
        }
        using (var source = new Bitmap(80, 60))
        using (var form = new RegionCaptureForm(RegionCaptureMode.Editor, new RegionCaptureOptions(), new Bitmap(source)))
        {
            using (var g = Graphics.FromImage(form.Canvas)) g.Clear(Color.White);
            form.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"DrawingSmartEraser\",\"Properties\":{\"Rectangle\":{\"X\":10,\"Y\":10,\"Width\":30,\"Height\":20},\"EraserColor\":\"Lime\"}}]}");
            if (!form.ExportShotCabDocument().Contains("EraserColor")) throw new Exception("Sample-cover color was not persisted in the editable document");
            using (var image = form.GetResultImage())
                if (image.GetPixel(20, 20).ToArgb() != Color.Lime.ToArgb())
                    throw new Exception("Reopened sample-cover region lost its sampled color");
        }
    }
    private static void CheckScrollingOverlap()
    {
        var type = typeof(RegionCaptureForm).Assembly.GetType("ShareX.ScreenCaptureLib.ScrollingCaptureManager");
        using (var manager = (IDisposable)Activator.CreateInstance(type, new object[] { new ScrollingCaptureOptions { AutoIgnoreBottomEdge = false } }))
        using (var first = new Bitmap(120, 200))
        using (var next = new Bitmap(120, 200))
        using (var unrelated = new Bitmap(120, 200))
        {
            for (int y = 0; y < 200; y++) for (int x = 0; x < 120; x++)
            {
                first.SetPixel(x, y, Color.FromArgb(y % 256, y * 7 % 256, 123));
                next.SetPixel(x, y, Color.FromArgb((y + 80) % 256, (y + 80) * 7 % 256, 123));
                unrelated.SetPixel(x, y, Color.Fuchsia);
            }
            var method = type.GetMethod("CombineImages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            using (var combined = (Bitmap)method.Invoke(manager, new object[] { first, next }))
            {
                if (combined == null || combined.Height != 280) throw new Exception("Scrolling overlap did not produce expected height");
                for (int y = 0; y < combined.Height; y++) if (combined.GetPixel(60, y) != Color.FromArgb(y % 256, y * 7 % 256, 123)) throw new Exception("Scrolling join contains duplicate or missing rows");
                using (var failed = (Bitmap)method.Invoke(manager, new object[] { combined, unrelated }))
                    if (failed != null) throw new Exception("Scrolling guessed a join with no matching overlap");
                if (combined.GetPixel(60, 279).B != 123) throw new Exception("Failed join modified the retained result");
            }
        }
    }
}
