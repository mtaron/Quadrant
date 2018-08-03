using System;
using System.Diagnostics;
using MathNet.Numerics;
using Quadrant.Utility;

namespace Quadrant.Ink.Fit
{
    [DebuggerDisplay("{functionName}")]
    internal abstract class ShiftFit : LinearCombinationFit
    {
        private readonly Func<double, double>[] _functions;
        private readonly double _shift;
        private readonly string _functionName;

        public ShiftFit(in StrokeData strokeData, string functionName, Func<double, double> function)
            : base(strokeData)
        {
            _functionName = functionName;
            _shift = GetShift();
            if (!_shift.IsReal())
            {
                IsValid = false;
            }
            else
            {
                if (Precision.AlmostEqual(_shift, 0.0, strokeData.DecimalPlaces))
                {
                    _shift = 0;
                }

                _functions = new Func<double, double>[]
                {
                        x => 1.0,
                        x => function(x - _shift)
                };
            }
        }

        protected override Func<double, double>[] Functions => _functions;

        protected abstract double GetShift();

        public override string GetExpression()
        {
            double[] coefficients = GetCoefficients();
            string constant = FormatValue(coefficients[0], includePlusSign: true);
            string scale = FormatValue(coefficients[1], includePlusSign: false);

            if (_shift == 0.0)
            {
                return $"{scale}{_functionName}(x){constant}";
            }
            else
            {
                string shiftString = FormatValue(-_shift, includePlusSign: true);
                return $"{scale}{_functionName}(x{shiftString}){constant}";
            }
        }
    }
}
