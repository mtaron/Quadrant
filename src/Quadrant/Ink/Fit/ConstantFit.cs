using System;
using System.Linq;

namespace Quadrant.Ink.Fit
{
    internal sealed class ConstantFit : StrokeFit
    {
        private readonly double _constant;

        public ConstantFit(in StrokeData strokeData)
            : base(strokeData)
            => _constant = StrokeData.Y.Average();

        protected override FitGroup Group => FitGroup.Polynomial;

        public override string GetExpression()
            => FormatValue(_constant, includePlusSign: false);

        protected override Func<double, double> GetFitFunction()
            => x => _constant;
    }
}
