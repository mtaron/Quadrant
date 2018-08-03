using System;

namespace Quadrant.Ink.Fit
{
    internal sealed class NegativeLogFit : LinearCombinationFit
    {
        private readonly Func<double, double>[] _functions;
        private readonly double _rightEdge;

        public NegativeLogFit(in StrokeData strokeData)
            : base(strokeData)
        {
            if (strokeData.Intersections.Length != 1)
            {
                IsValid = false;
            }

            _rightEdge = strokeData.BoundingRect.Right;
            _functions = new Func<double, double>[]
            {
                x => 1.0,
                x => Math.Log(-x + _rightEdge)
            };
        }

        protected override Func<double, double>[] Functions => _functions;

        public override string GetExpression()
        {
            double[] coefficients = GetCoefficients();
            string constant = FormatValue(coefficients[0], includePlusSign: true);
            string scale = FormatValue(coefficients[1], includePlusSign: false);
            string edge = FormatValue(_rightEdge, includePlusSign: true);
            return $"{scale}log(-x{edge}){constant}";
        }
    }
}
