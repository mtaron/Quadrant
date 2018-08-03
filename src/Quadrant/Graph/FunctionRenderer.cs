using System;
using System.Numerics;
using System.Threading;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Quadrant.Functions;
using Quadrant.Telemetry;
using Quadrant.Utility;

namespace Quadrant.Graph
{
    internal static class FunctionRenderer
    {
        public static void DrawFunction(
            CanvasDrawingSession drawingSession,
            CancellationToken cancellationToken,
            IFunction function,
            Transform transform,
            bool isHighlighted)
        {
            string functionString = function.ToString();
            QuadrantEventSource.Log.DrawFunctionStart(functionString);

            float interval = transform.DefaultInterval;
            float largeChangeThreshold = (transform.Top - transform.Bottom) / 20;
            float smallChangeThreshold = Math.Abs(transform.GetLogicalNormal(Vector2.UnitY).Y);
            float x = transform.Left;
            float difference = float.NaN;
            float previousY = float.NaN;
            float previousX = float.NaN;

            while (x <= transform.Right)
            {
                float y = (float)function.Function(x);

                if (!transform.IsOutsideVericalRange(y))
                {
                    using (var pathBuilder = new CanvasPathBuilder(drawingSession))
                    {
                        if (previousY.IsReal())
                        {
                            pathBuilder.BeginFigure(previousX, previousY);
                            pathBuilder.AddLine(x, y);
                        }
                        else
                        {
                            pathBuilder.BeginFigure(x, y);
                        }

                        while (x <= transform.Right)
                        {
                            previousX = x;
                            previousY = y;

                            x += interval;
                            y = (float)function.Function(x);
                            if (!y.IsReal())
                            {
                                break;
                            }

                            float previousDifference = difference;
                            difference = y - previousY;
                            float absoluteDifferece = Math.Abs(difference);

                            bool isAsymptote = false;
                            if (cancellationToken.IsCancellationRequested)
                            {
                                if (interval != transform.CanceledInterval)
                                {
                                    interval = transform.CanceledInterval;
                                    QuadrantEventSource.Log.DrawFunctionCanceled(functionString);
                                }
                            }
                            else if (absoluteDifferece > largeChangeThreshold)
                            {
                                if (interval > transform.MinimumInterval)
                                {
                                    x = previousX;
                                    y = previousY;
                                    difference = previousDifference;
                                    interval = transform.MinimumInterval;
                                    continue;
                                }

                                if (!float.IsNaN(previousDifference) && (Math.Sign(difference) * Math.Sign(previousDifference)) < 0)
                                {
                                    isAsymptote = true;
                                }
                            }
                            else if (absoluteDifferece < smallChangeThreshold && interval < transform.DefaultInterval)
                            {
                                interval = Math.Min(2 * interval, transform.DefaultInterval);
                            }

                            bool shouldStartNewFigure = TryGetExitPoint(function, transform, x, y, isAsymptote, out Vector2 point);
                            pathBuilder.AddLine(point);

                            if (shouldStartNewFigure)
                            {
                                break;
                            }
                        }

                        pathBuilder.EndFigure(CanvasFigureLoop.Open);

                        using (var brush = new CanvasSolidColorBrush(drawingSession, function.Color))
                        using (CanvasGeometry geometry = CanvasGeometry.CreatePath(pathBuilder))
                        {
                            CanvasGeometry transformedGeometry = geometry.Transform(transform.DisplayTransform);
                            const float strokeWidth = 2;
                            if (isHighlighted)
                            {
                                var highlightCommandList = new CanvasCommandList(drawingSession.Device);
                                using (CanvasDrawingSession highlightDrawingSession = highlightCommandList.CreateDrawingSession())
                                {
                                    highlightDrawingSession.DrawGeometry(
                                        transformedGeometry,
                                        Vector2.Zero,
                                        brush,
                                        strokeWidth);
                                }

                                var shadowEffect = new ShadowEffect()
                                {
                                    ShadowColor = function.Color,
                                    BlurAmount = 3,
                                    Source = highlightCommandList,
                                    Optimization = EffectOptimization.Speed
                                };

                                drawingSession.DrawImage(shadowEffect);
                            }

                            drawingSession.DrawGeometry(
                                transformedGeometry,
                                Vector2.Zero,
                                brush,
                                strokeWidth);
                        }
                    }
                }

                if (y.IsReal() && previousY.IsReal())
                {
                    difference = y - previousY;
                }
                else
                {
                    difference = float.NaN;
                }

                previousY = y;
                previousX = x;
                x += interval;
            }

            QuadrantEventSource.Log.DrawFunctionStop();
        }

        private static bool TryGetExitPoint(
            IFunction function,
            Transform transform,
            float x,
            float y,
            bool isAsymptote,
            out Vector2 point)
        {
            point = new Vector2(x, y);
            int derivativeSign = isAsymptote ? Math.Sign(function.Derivative(x)) : 0;

            // Check if we are leaving the graph. If so, find the intersection the edge
            // of the graph so we can have nice clean edges.
            if (y > transform.Top)
            {
                if (isAsymptote)
                {
                    if (derivativeSign > 0)
                    {
                        point = new Vector2(x, transform.Top);
                    }
                    else
                    {
                        point = new Vector2(x, transform.Bottom);
                    }
                }

                return true;
            }

            if (y < transform.Bottom)
            {
                if (isAsymptote)
                {
                    if (derivativeSign > 0)
                    {
                        point = new Vector2(x, transform.Top);
                    }
                    else
                    {
                        point = new Vector2(x, transform.Bottom);
                    }
                }

                return true;
            }

            return false;
        }
    }
}
