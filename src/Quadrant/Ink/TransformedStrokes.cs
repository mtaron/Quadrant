using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Quadrant.Persistence;
using Quadrant.Utility;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input.Inking;

namespace Quadrant.Ink
{
    internal sealed class TransformedStrokes
    {
        public event EventHandler StrokesChanged;

        private readonly bool _allowMultipleStrokes;
        private readonly List<InkStrokeWithTransform> _strokes = new List<InkStrokeWithTransform>();
        private readonly InkStrokeContainer _container = new InkStrokeContainer();

        public TransformedStrokes(bool allowMultipleStrokes = true)
        {
            _allowMultipleStrokes = allowMultipleStrokes;
        }

        public void Clear()
        {
            lock (_strokes)
            {
                _strokes.Clear();
                _container.Clear();
            }
        }

        public void Render(CanvasDrawingSession drawingSession, Matrix3x2 displayTransform, bool isHighContrast)
        {
            lock (_strokes)
            {
                if (_strokes.Count == 0)
                {
                    return;
                }

                foreach (var stroke in _strokes)
                {
                    stroke.Render(drawingSession, displayTransform, isHighContrast);
                }
            }

            drawingSession.Transform = Matrix3x2.Identity;
        }

        public void AddRange(IEnumerable<InkStroke> strokes, Matrix3x2 logicalTransform)
        {
            IEnumerable<InkStrokeWithTransform> strokesWithTransform = strokes.Select(s => new InkStrokeWithTransform(s, logicalTransform));
            lock (_strokes)
            {
                if (!_allowMultipleStrokes)
                {
                    Clear();
                }

                _strokes.AddRange(strokesWithTransform);
                _container.AddStrokes(strokes);
            }

            StrokesChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task<bool> EraseBetweenAsync(CoreDispatcher dispatcher, Point pointOne, Point pointTwo)
        {
            return dispatcher.AwaitableRunAsync(() =>
            {
                Rect result = _container.SelectWithLine(pointOne, pointTwo);
                if (result.Area() <= 0)
                {
                    return false;
                }

                lock (_strokes)
                {
                    foreach (InkStroke stroke in _container.GetStrokes().Where(s => s.Selected))
                    {
                        _strokes.RemoveAll(s => s.Equals(stroke));
                    }

                    _container.DeleteSelected();
                }

                StrokesChanged?.Invoke(this, EventArgs.Empty);
                return true;
            });
        }

        public StrokeData GetStrokeData()
        {
            lock (_strokes)
            {
                if (_strokes.Count == 0)
                {
                    return StrokeData.Empty;
                }

                Vector2[] points = _strokes.SelectMany(s => s.GetLogicalPoints()).ToArray();
                Rect boundingRect = _strokes.Select(s => s.GetBoundingRect()).Aggregate(
                    (r1, r2) =>
                    {
                        r1.Union(r2);
                        return r1;
                    });

                double halfVertical = (boundingRect.Top + boundingRect.Bottom) / 2;
                double[] intersections = MathUtility.GetIntersections(points, halfVertical);

                int decimals = _strokes[0].GetDecimalPlaces();

                return new StrokeData(
                    points,
                    intersections,
                    boundingRect,
                    decimals);
            }
        }

        public Task SerializeAsync(Serializer serializer)
        {
            foreach (InkStrokeWithTransform stroke in _strokes)
            {
                stroke.Serialize(serializer);
            }

            return serializer.WriteInkAsync(_container);
        }

        public async Task DeserializeAsync(Deserializer deserializer)
        {
            _strokes.Clear();
            await deserializer.ReadInkAsync(_container);

            foreach (InkStroke stroke in _container.GetStrokes())
            {
                Matrix3x2 matrix = deserializer.ReadMatrix3x2();
                _strokes.Add(new InkStrokeWithTransform(stroke, matrix));
            }
        }

        private readonly struct InkStrokeWithTransform : IEquatable<InkStroke>
        {
            private readonly Matrix3x2 _transform;

            public InkStrokeWithTransform(InkStroke stroke, Matrix3x2 transform)
            {
                Stroke = stroke;
                _transform = transform;
            }

            public InkStroke Stroke { get; }

            public void Render(CanvasDrawingSession drawingSession, Matrix3x2 displayTransform, bool isHighContrast)
            {
                SetDisplayTransform(displayTransform);
                drawingSession.DrawInk(new InkStroke[] { Stroke }, isHighContrast);
            }

            public void Serialize(Serializer serializer)
                => serializer.Write(_transform);

            public int GetDecimalPlaces()
            {
                float logicalUnit = Vector2.Transform(Vector2.One, _transform).Length();
                return (int)Math.Max(Math.Floor(Math.Log10(logicalUnit)) + 1, 2);
            }

            public IEnumerable<Vector2> GetLogicalPoints()
            {
                foreach (InkPoint point in Stroke.GetInkPoints())
                {
                    yield return Vector2.Transform(point.Position.ToVector2(), _transform);
                }
            }

            public Rect GetBoundingRect()
                => Stroke.BoundingRect.Transform(_transform);

            public override bool Equals(object obj)
            {
                if (!(obj is InkStrokeWithTransform))
                {
                    return false;
                }

                InkStrokeWithTransform other = (InkStrokeWithTransform)obj;
                return other.Stroke == Stroke;
            }

            public override int GetHashCode()
                => Stroke.GetHashCode();

            public bool Equals(InkStroke other)
                => Stroke == other;

            private void SetDisplayTransform(Matrix3x2 displayTransform)
            {
                Matrix3x2 pointTransform = _transform * displayTransform;
                Vector2 scale = pointTransform.Scale();
                double length = scale.LengthSquared();

                // PointTransform will throw if we provide a very small scale.
                if (length < 10E-7)
                {
                    scale = Vector2.One / scale;
                    pointTransform *= Matrix3x2.CreateScale(scale);
                }

                Stroke.PointTransform = pointTransform;
            }
        }
    }
}
