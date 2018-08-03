using System;
using System.Diagnostics;

namespace Quadrant.Ink.Fit
{
    [DebuggerDisplay("{Expression}")]
    internal class FunctionFit : LinearCombinationFit
    {
        private readonly Func<double, double>[] _functions;

        public FunctionFit(in StrokeData strokeData, string expression, Func<double, double> function)
            : base(strokeData)
        {
            Expression = expression;
            _functions = new Func<double, double>[]
            {
                x => 1.0,
                x => function(x)
            };
        }

        protected string Expression { get; }

        protected override Func<double, double>[] Functions => _functions;

        public override string GetExpression()
        {
            double[] coefficients = GetCoefficients();
            string constant = FormatValue(coefficients[0], includePlusSign: true);
            string scale = FormatValue(coefficients[1], includePlusSign: false);
            return string.Concat(scale, Expression, constant);
        }
    }
}
