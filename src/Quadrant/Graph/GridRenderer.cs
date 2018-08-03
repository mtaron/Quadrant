using System;
using System.Globalization;
using System.Numerics;
using MathNet.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Quadrant.Utility;
using Windows.Foundation;
using Windows.UI;

namespace Quadrant.Graph
{
    internal static class GridRenderer
    {
        public static void DrawGrid(CanvasDrawingSession drawingSession, Transform transform, Color gridColor)
        {
            Rect labelBounds = GetLabelBounds(transform.CanvasSize);

            Vector2 xAxisLeft = transform.GetDisplayVector(transform.Left, 0);
            Vector2 xAxisRight = transform.GetDisplayVector(transform.Right, 0);
            drawingSession.DrawLine(xAxisLeft, xAxisRight, gridColor, DisplayConstants.AxisLineWidth);

            Vector2 yAxisTop = transform.GetDisplayVector(0, transform.Top);
            Vector2 yAxisBottom = transform.GetDisplayVector(0, transform.Bottom);
            drawingSession.DrawLine(yAxisTop, yAxisBottom, gridColor, DisplayConstants.AxisLineWidth);

            Vector2 unitNormal = transform.GetLogicalNormal(DisplayConstants.GridInterval);
            double interval = GetInterval(unitNormal.X);
            double halfInterval = interval / 2;
            double x = transform.Left - (transform.Left % halfInterval);
            bool isLabelLine = Math.IEEERemainder(x, interval).AlmostEqual(0);
            for (; x <= transform.Right - (transform.Right % halfInterval); x = x + halfInterval)
            {
                if (x.AlmostEqual(0))
                {
                    isLabelLine = !isLabelLine;
                    continue;
                }

                Vector2 xLineStart = transform.GetDisplayVector(x, transform.Top);
                Vector2 xLineEnd = transform.GetDisplayVector(x, transform.Bottom);
                drawingSession.DrawLine(xLineStart, xLineEnd, gridColor, GetLineWidth(isLabelLine));

                // Add the interval labels.
                if (isLabelLine)
                {
                    CanvasTextLayout label = CreateLabel(drawingSession, x);
                    Rect layoutBounds = label.LayoutBounds.Offset(new Vector2(xLineStart.X + 1, xAxisLeft.Y));
                    layoutBounds = ClampY(layoutBounds, labelBounds);
                    drawingSession.DrawTextLayout(label, (float)layoutBounds.Left, (float)layoutBounds.Top, gridColor);
                }

                isLabelLine = !isLabelLine;
            }

            interval = GetInterval(unitNormal.Y);
            halfInterval = interval / 2;
            double y = transform.Top - (transform.Top % halfInterval);
            isLabelLine = Math.IEEERemainder(y, interval).AlmostEqual(0);
            for (; y >= transform.Bottom - (transform.Bottom % halfInterval); y = y - halfInterval)
            {
                if (y.AlmostEqual(0))
                {
                    isLabelLine = !isLabelLine;
                    continue;
                }

                Vector2 yLineStart = transform.GetDisplayVector(transform.Left, y);
                Vector2 yLineEnd = transform.GetDisplayVector(transform.Right, y);
                drawingSession.DrawLine(yLineStart, yLineEnd, gridColor, GetLineWidth(isLabelLine));

                if (isLabelLine)
                {
                    CanvasTextLayout label = CreateLabel(drawingSession, y);
                    Rect layoutBounds = label.LayoutBounds.Offset(new Vector2(yAxisTop.X + 2, yLineStart.Y));
                    layoutBounds = ClampX(layoutBounds, labelBounds);
                    drawingSession.DrawTextLayout(label, (float)layoutBounds.Left, (float)layoutBounds.Top, gridColor);
                }

                isLabelLine = !isLabelLine;
            }
        }

        private static CanvasTextLayout CreateLabel(CanvasDrawingSession drawingSession, double value)
        {
            return new CanvasTextLayout(drawingSession, FormatValue(value), DisplayConstants.NumberFormat, 20, 20);
        }

        private static float GetLineWidth(bool isLabelLine)
        {
            if (isLabelLine)
            {
                return DisplayConstants.MajorGridLineWidth;
            }
            else
            {
                return DisplayConstants.MinorGridLineWidth;
            }
        }

        private static string FormatValue(double value)
        {
            return Math.Round(value, 3).ToString("G4", CultureInfo.CurrentUICulture);
        }

        private static Rect GetLabelBounds(Size canvasSize)
        {
            // Add 5 px margins to the canvas, with additional space on the bottom for the command bar.
            return new Rect(5, 5, canvasSize.Width - 10, canvasSize.Height - 53);
        }

        private static Rect ClampX(Rect rect, Rect clamp)
        {
            double x;
            if (rect.X < clamp.X)
            {
                x = clamp.X;
            }
            else if (rect.Right > clamp.Right)
            {
                x = clamp.Right - rect.Width;
            }
            else
            {
                x = rect.X;
            }

            return new Rect(x, rect.Y, rect.Width, rect.Height);
        }

        private static Rect ClampY(Rect rect, Rect clamp)
        {
            double y;
            if (rect.Y < clamp.Y)
            {
                y = clamp.Y;
            }
            else if (rect.Bottom > clamp.Bottom)
            {
                y = clamp.Bottom - rect.Height;
            }
            else
            {
                y = rect.Y;
            }

            return new Rect(rect.X, y, rect.Width, rect.Height);
        }

        private static double GetInterval(float scale)
        {
            scale = Math.Abs(scale);
            double multiple;

            if (scale < 0.01)
            {
                multiple = MathUtility.GetNearestMultiple(scale, 1000);
                return MathUtility.RoundToMultiple(scale, multiple);
            }

            if (scale < 0.1)
            {
                multiple = MathUtility.GetNearestMultiple(scale, 100);
                return MathUtility.RoundToMultiple(scale, multiple);
            }

            if (scale <= 0.25)
            {
                return 0.25;
            }

            if (scale <= 0.5)
            {
                return 0.5;
            }

            if (scale <= 1)
            {
                return 1;
            }

            if (scale <= 2)
            {
                return 2;
            }

            if (scale <= 5)
            {
                return 5;
            }

            if (scale <= 10)
            {
                return 10;
            }

            multiple = MathUtility.GetNearestMultiple(scale, 10);
            return MathUtility.RoundToMultiple(scale, multiple);
        }
    }
}
