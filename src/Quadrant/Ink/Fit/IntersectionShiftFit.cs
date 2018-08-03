using System;

namespace Quadrant.Ink.Fit
{
    internal class IntersectionShiftFit : ShiftFit
    {
        public IntersectionShiftFit(in StrokeData strokeData, string functionName, Func<double, double> function)
            : base(strokeData, functionName, function)
        {
        }

        protected override double GetShift()
        {
            if (StrokeData.Intersections.Length != 1)
            {
                return double.NaN;
            }

            return StrokeData.Intersections[0];
        }
    }
}
