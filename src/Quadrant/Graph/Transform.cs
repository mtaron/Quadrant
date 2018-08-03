using System.Numerics;
using Windows.Foundation;

namespace Quadrant.Graph
{
    internal sealed class Transform
    {
        public Matrix3x2 LogicalTransform { get; private set; }
        public Matrix3x2 DisplayTransform { get; private set; }

        public Size CanvasSize { get; private set; }
        public float Top { get; private set; }
        public float Bottom { get; private set; }
        public float Left { get; private set; }
        public float Right { get; private set; }
        public float SnappingTolerance { get; private set; }
        public float DefaultInterval { get; private set; }
        public float MinimumInterval { get; private set; }
        public float CanceledInterval { get; private set; }

        public void Update(Size canvasSize, Vector2 origin, Vector2 scale)
        {
            CanvasSize = canvasSize;
            Vector2 sizeVector = canvasSize.ToVector2();

            Vector2 displayOrigin = sizeVector / 2;
            Matrix3x2 displayTransform =
                Matrix3x2.CreateScale(scale.X, scale.Y)
                * Matrix3x2.CreateTranslation(displayOrigin.X - origin.X * scale.X, displayOrigin.Y - origin.Y * scale.Y);

            if (!Matrix3x2.Invert(displayTransform, out Matrix3x2 logicalTransform))
            {
                return;
            }

            DisplayTransform = displayTransform;
            LogicalTransform = logicalTransform;

            Vector2 upperLeft = Vector2.Transform(Vector2.Zero, LogicalTransform);
            Vector2 lowerRight = Vector2.Transform(sizeVector, LogicalTransform);

            Left = upperLeft.X;
            Right = lowerRight.X;
            Top = upperLeft.Y;
            Bottom = lowerRight.Y;
            SnappingTolerance = Vector2.TransformNormal(
                new Vector2(DisplayConstants.SnappingTolerance, 0), LogicalTransform).X;
            DefaultInterval = Vector2.TransformNormal(
                new Vector2(DisplayConstants.DefaultEvaluationInterval, 0), LogicalTransform).X;
            MinimumInterval = Vector2.TransformNormal(
                new Vector2(DisplayConstants.MinimumEvaluationInterval, 0), LogicalTransform).X;
            CanceledInterval = Vector2.TransformNormal(
                new Vector2(DisplayConstants.CanceledEvaluationInterval, 0), LogicalTransform).X;
        }

        public Vector2 GetLogicalVector(Vector2 displayVector)
            => Vector2.Transform(displayVector, LogicalTransform);

        public Vector2 GetLogicalNormal(Vector2 displayVector)
            => Vector2.TransformNormal(displayVector, LogicalTransform);

        public Vector2 GetDisplayVector(Vector2 logicalVector)
            => Vector2.Transform(logicalVector, DisplayTransform);

        public Vector2 GetDisplayVector(float logicalX, float logicalY)
            => Vector2.Transform(new Vector2(logicalX, logicalY), DisplayTransform);

        public Vector2 GetDisplayVector(double logicalX, double logicalY)
            => GetDisplayVector((float)logicalX, (float)logicalY);

        public bool IsOutsideVericalRange(double y)
            => double.IsNaN(y) || double.IsInfinity(y) || y > Top || y < Bottom;
    }
}
