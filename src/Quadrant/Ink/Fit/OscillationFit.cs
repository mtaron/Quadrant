using System;

namespace Quadrant.Ink.Fit
{
    internal class OscillatorFit : LinearCombinationFit
    {
        private readonly string _name;
        private readonly double _adjustedFrequency;
        private readonly Func<double, double>[] _functions;

        public OscillatorFit(in StrokeData strokeData, string name, Func<double, double> function)
            : base(strokeData)
        {
            _name = name;

            if (strokeData.Intersections.Length < 3)
            {
                IsValid = false;
            }

            _adjustedFrequency = 3 * strokeData.Frequency;
            _functions = new Func<double, double>[]
            {
                    x => 1.0,
                    x => function(_adjustedFrequency * x)
            };
        }

        protected override Func<double, double>[] Functions => _functions;

        public override string GetExpression()
        {
            double[] coefficients = GetCoefficients();
            string constant = FormatValue(coefficients[0], includePlusSign: true);
            string amplitude = FormatValue(coefficients[1], includePlusSign: false);
            string frequency = FormatValue(_adjustedFrequency, includePlusSign: false);

            return $"{amplitude}{_name}({frequency}x){constant}";
        }
    }
}
