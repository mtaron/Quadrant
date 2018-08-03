using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Quadrant.Functions;
using Quadrant.Utility;

namespace Quadrant.Graph
{
    internal sealed class IntersectionService
    {
        private readonly IList<IFunction> _functions;
        private List<Func<double, double>>[] _intersections;

        public IntersectionService(IList<IFunction> functions)
            => _functions = functions;

        public double GetSnapValue(IFunction function, double x, float tolerance)
        {
            // Check if we are near zero already.
            if (Math.Abs(x) <= tolerance)
            {
                return 0;
            }

            double lowerBound = x - tolerance;
            double upperBound = x + tolerance;

            // Look for x-axis intersections.
            if (MathUtility.TryFindRoot(function.Function, lowerBound, upperBound, out double root))
            {
                return root;
            }

            // Look for critical points.
            if (MathUtility.TryFindRoot(function.Derivative, lowerBound, upperBound, out root))
            {
                return root;
            }

            // Look for intersections with other functions.
            IEnumerable<Func<double, double>> intersections = GetIntersections(function);
            if (intersections != null)
            {
                foreach (Func<double, double> differenceFunction in intersections)
                {
                    if (MathUtility.TryFindRoot(differenceFunction, lowerBound, upperBound, out root))
                    {
                        return root;
                    }
                }
            }

            return x;
        }

        public IFunction GetNearestFunction(Vector2 point, double lowerBound, double upperBound, out Vector2 functionPoint)
        {
            IFunction nearestFunction = null;
            float leastDistance = float.MaxValue;
            functionPoint = Vector2.Zero;

            foreach (IFunction function in _functions)
            {
                Vector2? nearestPoint = function.GetNearestPoint(point, lowerBound, upperBound);
                if (nearestPoint == null)
                {
                    continue;
                }

                Vector2 nearestPointValue = nearestPoint.Value;
                float distance = (nearestPointValue - point).LengthSquared();
                if (distance < leastDistance)
                {
                    nearestFunction = function;
                    leastDistance = distance;
                    functionPoint = nearestPointValue;
                }
            }

            return nearestFunction;
        }

        private IEnumerable<Func<double, double>> GetIntersections(IFunction function)
        {
            int functionCount = _functions.Count;
            if (functionCount <= 1)
            {
                return null;
            }

            if (_intersections == null)
            {
                _intersections = new List<Func<double, double>>[functionCount];
            }

            int index = _functions.IndexOf(function);
            List<Func<double, double>> functionIntersections = _intersections[index];
            if (functionIntersections != null)
            {
                return functionIntersections;
            }

            functionIntersections = new List<Func<double, double>>(_functions.Count - 1);
            foreach (IFunction currentFunction in _functions.Where(f => f != function))
            {
                functionIntersections.Add((x) => currentFunction.Function(x) - function.Function(x));
            }

            return functionIntersections;
        }
    }
}
