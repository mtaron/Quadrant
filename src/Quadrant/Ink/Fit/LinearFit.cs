using System;
using NumericsFit = MathNet.Numerics.Fit;

namespace Quadrant.Ink.Fit
{
    internal sealed class LinearFit : StrokeFit
    {
        private readonly double _intercept;
        private readonly double _slope;

        public LinearFit(in StrokeData strokeData)
            : base(strokeData)
        {
            if (strokeData.Points.Length < 2)
            {
                IsValid = false;
            }
            else
            {
                Tuple<double, double> coefficients = NumericsFit.Line(StrokeData.X, StrokeData.Y);
                _intercept = coefficients.Item1;
                _slope = coefficients.Item2;
            }
        }

        protected override double Cost => 3.0;

        protected override FitGroup Group => FitGroup.Polynomial;

        public override string GetExpression()
        {
            string b = FormatValue(_intercept, includePlusSign: true);
            string a = FormatValue(_slope, includePlusSign: false);
            return string.Concat(a, "x", b);
        }

        protected override Func<double, double> GetFitFunction()
            => x => _slope * x + _intercept;
    }
}
