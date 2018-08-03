using System;
using NumericsFit = MathNet.Numerics.Fit;

namespace Quadrant.Ink.Fit
{
    internal sealed class PolynomialFit : StrokeFit
    {
        private readonly int _order;

        public PolynomialFit(in StrokeData strokeData, int order)
            : base(strokeData)
        {
            _order = order;

            if (strokeData.X.Length <= _order)
            {
                IsValid = false;
            }
        }

        protected override double Cost => 3 * _order;

        protected override FitGroup Group => FitGroup.Polynomial;

        protected override Func<double, double> GetFitFunction()
            => NumericsFit.PolynomialFunc(StrokeData.X, StrokeData.Y, _order);

        public override string GetExpression()
        {
            double[] coefficients = NumericsFit.Polynomial(StrokeData.X, StrokeData.Y, _order);
            string expression = null;
            for (int index = coefficients.Length - 1; index >= 0; index--)
            {
                string value = FormatValue(coefficients[index], includePlusSign: expression != null);

                if (expression == null)
                {
                    expression = value;
                }
                else
                {
                    expression += value;
                }

                if (index > 0)
                {
                    expression += "x";

                    if (index > 1)
                    {
                        expression += $"^{index}";
                    }
                }
            }

            return expression ?? "0";
        }
    }
}
