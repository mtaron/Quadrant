using System;
using System.Numerics;
using Quadrant.Functions;

namespace Quadrant.Utility
{
    public static class FunctionExtensions
    {
        public static Vector2? GetNearestPoint(this IFunction function, Vector2 point, double lowerBound, double upperBound)
        {
            Func<double, double> distanceFunction = GetDistanceFunction(function, point.X, point.Y);
            if (MathUtility.TryFindRoot(distanceFunction, lowerBound, upperBound, out double x, accuracy: 1e-3, maxIterations: 50))
            {
                return new Vector2((float)x, (float)function.Function(x));
            }

            return null;
        }

        private static Func<double, double> GetDistanceFunction(IFunction function, double x1, double y1)
        {
            return (x) => 2 * (-x1 + (function.Function(x) - y1) * function.Derivative(x) + x);
        }
    }
}
