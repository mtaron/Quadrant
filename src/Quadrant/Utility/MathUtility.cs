using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.RootFinding;

namespace Quadrant.Utility
{
    internal static class MathUtility
    {
        public static double Lerp(double a, double b, double t)
            => (1 - t) * a + t * b;

        public static void Split(Vector2[] points, out double[] x, out double[] y)
        {
            x = points.Select(p => (double)p.X).ToArray();
            y = points.Select(p => (double)p.Y).ToArray();
        }

        public static double RoundToMultiple(double value, double multiple)
            => Math.Round(value / multiple) * multiple;

        public static double GetNearestMultiple(double value, double multiple)
            => Math.Pow(multiple, Math.Floor(Math.Log(value, multiple)));

        public static double GetNearestMultipleFloor(double value, double multiple)
            => Math.Pow(multiple, Math.Ceiling(Math.Log(value, multiple)));

        public static float Clamp(float value, float minValue, float maxValue)
            => Math.Max(Math.Min(value, maxValue), minValue);

        public static bool IsReal(this float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsReal(this double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// Get the approximate intersections of the curve formed by interpreting
        /// the given points as a continuous line and the given y value.
        /// </summary>
        public static double[] GetIntersections(Vector2[] points, double yValue)
        {
            if (points.Length < 3)
            {
                return new double[0];
            }

            Vector2[] sortedPoints = points.OrderBy(p => p.X).ToArray();
            var intersections = new List<double>();

            Vector2 previousPoint = sortedPoints[0];
            int previousSign = Math.Sign(previousPoint.Y - yValue);
            for (int pointIndex = 1; pointIndex < sortedPoints.Length; pointIndex++)
            {
                Vector2 currentPoint = sortedPoints[pointIndex];
                int currentSign = Math.Sign(currentPoint.Y - yValue);
                if (currentSign != previousSign)
                {
                    // Use the point-slope formula to find the intersection.
                    double itersection = (((previousPoint.X - currentPoint.X) * (yValue - currentPoint.Y))
                        / (previousPoint.Y - currentPoint.Y)) + currentPoint.X;
                    intersections.Add(itersection);
                }

                previousPoint = currentPoint;
                previousSign = currentSign;
            }

            return intersections.ToArray();
        }

        public static double GetAverageFrequency(double[] values)
        {
            if (values == null || values.Length < 2)
            {
                return 0;
            }

            var distances = new List<double>();
            for (int intersectionIndex = 1; intersectionIndex < values.Length; intersectionIndex++)
            {
                double distance = Math.Abs(values[intersectionIndex] - values[intersectionIndex - 1]);
                distances.Add(distance);
            }

            return 1.0 / distances.Average();
        }

        public static bool TryFindRoot(Func<double, double> function, double lowerBound, double upperBound, out double root, double accuracy = 1e-6, int maxIterations = 100)
        {
            try
            {
                double lowerBoundValue = function(lowerBound);
                if (lowerBoundValue == 0.0 && lowerBoundValue == function(upperBound))
                {
                    // For our usage, we don't want to consider a constant function at zero to have any roots.
                    root = lowerBound;
                    return false;
                }

                if (Brent.TryFindRoot(function, lowerBound, upperBound, accuracy: accuracy, maxIterations: maxIterations, root: out root))
                {
                    return true;
                }

                return Bisection.TryFindRoot(function, lowerBound, upperBound, accuracy: accuracy, maxIterations: maxIterations, root: out root);
            }
            catch (ArithmeticException)
            {
                root = double.NaN;
                return false;
            }
        }
    }
}
