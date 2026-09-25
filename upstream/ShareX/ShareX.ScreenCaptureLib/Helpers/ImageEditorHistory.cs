#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2025 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ShareX.ScreenCaptureLib
{
    internal class ImageEditorHistory : IDisposable
    {
        public bool CanUndo => undoMementoStack.Count > 0;
        public bool CanRedo => redoMementoStack.Count > 0;

        private readonly ShapeManager shapeManager;
        private Stack<ImageEditorMemento> undoMementoStack = new Stack<ImageEditorMemento>();
        private Stack<ImageEditorMemento> redoMementoStack = new Stack<ImageEditorMemento>();
        private readonly List<string> shotCabStepLabels = new List<string>();
        internal int ShotCabVersion { get; private set; }

        internal int ShotCabCurrentStep => undoMementoStack.Count;
        internal int ShotCabStepCount => shotCabStepLabels.Count + 1;

        internal string ShotCabStepTitle(int step)
        {
            if (step < 0 || step >= ShotCabStepCount) throw new ArgumentOutOfRangeException(nameof(step));
            bool english = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en";
            return step == 0 ? (english ? "Opened image" : "打开图片") : shotCabStepLabels[step - 1];
        }

        internal void ShotCabJumpTo(int step)
        {
            if (step < 0 || step >= ShotCabStepCount) throw new ArgumentOutOfRangeException(nameof(step));
            while (ShotCabCurrentStep > step) Undo();
            while (ShotCabCurrentStep < step) Redo();
        }

        public ImageEditorHistory(ShapeManager shapeManager)
        {
            this.shapeManager = shapeManager;
        }

        private void AddMemento(ImageEditorMemento memento, string label)
        {
            // A new edit after Undo discards the abandoned future branch.
            while (shotCabStepLabels.Count > undoMementoStack.Count)
                shotCabStepLabels.RemoveAt(shotCabStepLabels.Count - 1);
            shotCabStepLabels.Add(label);
            ShotCabVersion++;
            undoMementoStack.Push(memento);

            foreach (ImageEditorMemento redoMemento in redoMementoStack)
            {
                redoMemento?.Dispose();
            }

            redoMementoStack.Clear();
        }

        private ImageEditorMemento GetMementoFromCanvas()
        {
            List<BaseShape> shapes = shapeManager.Shapes.Select(x => x.Duplicate()).ToList();
            Bitmap canvas = (Bitmap)shapeManager.Form.Canvas.Clone();
            return new ImageEditorMemento(shapes, shapeManager.Form.CanvasRectangle, canvas);
        }

        private ImageEditorMemento GetMementoFromShapes()
        {
            List<BaseShape> shapes = shapeManager.Shapes.Select(x => x.Duplicate()).ToList();
            return new ImageEditorMemento(shapes, shapeManager.Form.CanvasRectangle);
        }

        public void CreateCanvasMemento()
        {
            ImageEditorMemento memento = GetMementoFromCanvas();
            AddMemento(memento, System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "Image adjustment" : "图像调整");
        }

        public void CreateShapesMemento()
        {
            if (!shapeManager.IsCurrentShapeTypeRegion && shapeManager.CurrentTool != ShapeType.ToolCrop && shapeManager.CurrentTool != ShapeType.ToolCutOut)
            {
                ImageEditorMemento memento = GetMementoFromShapes();
                AddMemento(memento, ShotCabActionTitle());
            }
        }

        private string ShotCabActionTitle()
        {
            bool english = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en";
            switch (shapeManager.CurrentShapeTool)
            {
                case ShapeType.DrawingRectangle: return english ? "Rectangle" : "矩形";
                case ShapeType.DrawingEllipse: return english ? "Ellipse" : "椭圆";
                case ShapeType.DrawingArrow: return english ? "Arrow" : "箭头";
                case ShapeType.DrawingFreehand: return english ? "Brush" : "画笔";
                case ShapeType.DrawingTextOutline:
                case ShapeType.DrawingTextBackground: return english ? "Text" : "文字";
                case ShapeType.EffectPixelate: return english ? "Pixelate" : "马赛克";
                case ShapeType.EffectBlur: return english ? "Blur" : "模糊";
                case ShapeType.EffectSolidMask: return english ? "Mask" : "遮挡";
                case ShapeType.EffectHighlight: return english ? "Highlight" : "荧光笔";
                case ShapeType.EffectSpotlight: return english ? "Spotlight" : "聚光灯";
                case ShapeType.ToolCrop: return english ? "Crop" : "裁剪";
                default: return english ? "Edit annotation" : "调整标注";
            }
        }

        public void Undo()
        {
            if (CanUndo)
            {
                ImageEditorMemento undoMemento = undoMementoStack.Pop();

                if (undoMemento.Shapes != null)
                {
                    if (undoMemento.Canvas == null)
                    {
                        ImageEditorMemento redoMemento = GetMementoFromShapes();
                        redoMementoStack.Push(redoMemento);

                        shapeManager.RestoreState(undoMemento);
                    }
                    else
                    {
                        ImageEditorMemento redoMemento = GetMementoFromCanvas();
                        redoMementoStack.Push(redoMemento);

                        shapeManager.RestoreState(undoMemento);
                    }
                }
            }
        }

        public void Redo()
        {
            if (CanRedo)
            {
                ImageEditorMemento redoMemento = redoMementoStack.Pop();

                if (redoMemento.Shapes != null)
                {
                    if (redoMemento.Canvas == null)
                    {
                        ImageEditorMemento undoMemento = GetMementoFromShapes();
                        undoMementoStack.Push(undoMemento);

                        shapeManager.RestoreState(redoMemento);
                    }
                    else
                    {
                        ImageEditorMemento undoMemento = GetMementoFromCanvas();
                        undoMementoStack.Push(undoMemento);

                        shapeManager.RestoreState(redoMemento);
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (ImageEditorMemento undoMemento in undoMementoStack)
            {
                undoMemento?.Dispose();
            }

            undoMementoStack.Clear();

            foreach (ImageEditorMemento redoMemento in redoMementoStack)
            {
                redoMemento?.Dispose();
            }

            redoMementoStack.Clear();
            shotCabStepLabels.Clear();
        }
    }
}
