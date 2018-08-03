using System.Numerics;
using System.Linq;
using Quadrant.Utility;
using Windows.Foundation;

namespace Quadrant.Ink
{
    internal readonly struct StrokeData
    {
        private static StrokeData EmptyInstance = new StrokeData();
        public static ref readonly StrokeData Empty => ref EmptyInstance;

        public StrokeData(
            Vector2[] points,
            double[] intersections,
            Rect boundingRect,
            int decimalPlaces)
        {
            Points = points;
            BoundingRect = boundingRect;
            MathUtility.Split(points, out double[] xValues, out double[] yValues);

            X = xValues;
            Y = yValues;
            Intersections = intersections;
            Frequency = MathUtility.GetAverageFrequency(Intersections);
            DecimalPlaces = decimalPlaces;
        }

        public Vector2[] Points { get; }
        public Rect BoundingRect { get; }
        public double[] X { get; }
        public double[] Y { get; }
        public double[] Intersections { get; }
        public double Frequency { get; }
        public int DecimalPlaces { get; }

        public Vector2 GetMaximumY()
        {
            Vector2 max = Points[0];
            double maxY = max.Y;
            foreach (Vector2 vector in Points.Skip(1))
            {
                if (vector.Y > maxY)
                {
                    max = vector;
                    maxY = vector.Y;
                }
            }

            return max;
        }

        public Vector2 GetMinimumY()
        {
            Vector2 min = Points[0];
            double minY = min.Y;
            foreach (Vector2 vector in Points.Skip(1))
            {
                if (vector.Y < minY)
                {
                    min = vector;
                    minY = vector.Y;
                }
            }

            return min;
        }
    }
}
