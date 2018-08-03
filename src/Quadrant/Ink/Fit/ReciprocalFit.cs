using System;

namespace Quadrant.Ink.Fit
{
    internal sealed class ReciprocalFit : LinearCombinationFit
    {
        private readonly Func<double, double>[] _functions;
        private readonly int _order;

        public ReciprocalFit(in StrokeData strokeData, int order)
            : base(strokeData)
        {
            _order = order;
            _functions = new Func<double, double>[]
            {
                    x => 1.0,
                    x => Math.Pow(x, _order)
            };
        }

        protected override Func<double, double>[] Functions => _functions;

        public override string GetExpression()
        {
            double[] coefficients = GetCoefficients();
            string constant = FormatValue(coefficients[0], includePlusSign: true);
            string numerator = FormatValue(coefficients[1], includePlusSign: false);
            string expression = $"{numerator}/x";
            int positiveOrder = -_order;
            if (positiveOrder > 1)
            {
                expression += $"^{positiveOrder}";
            }
            return expression + constant;
        }
    }
}
