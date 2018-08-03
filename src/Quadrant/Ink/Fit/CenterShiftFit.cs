using System;

namespace Quadrant.Ink.Fit
{
    internal sealed class CenterShiftFit : ShiftFit
    {
        public CenterShiftFit(in StrokeData strokeData, string functionName, Func<double, double> function)
            : base(strokeData, functionName, function)
        {
        }

        protected override double GetShift()
        {
            if (StrokeData.Intersections.Length != 2)
            {
                return double.NaN;
            }

            return GetCenter();
        }

        private double GetCenter()
        {
            double leftIntersection = StrokeData.Intersections[0];
            double rightIntersection = StrokeData.Intersections[1];

            double max = StrokeData.GetMaximumY().X;
            double min = StrokeData.GetMinimumY().X;

            bool isMaxBetweenIntersections = (leftIntersection < max) && (max < rightIntersection);
            bool isMinBetweenIntersections = (leftIntersection < min) && (min < rightIntersection);

            if (isMaxBetweenIntersections == isMinBetweenIntersections)
            {
                return 0.0;
            }

            if (isMaxBetweenIntersections)
            {
                return max;
            }

            return min;
        }
    }
}
