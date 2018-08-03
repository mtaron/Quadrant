using System;
using NumericsFit = MathNet.Numerics.Fit;

namespace Quadrant.Ink.Fit
{
    internal abstract class LinearCombinationFit : StrokeFit
    {
        protected LinearCombinationFit(in StrokeData strokeData)
            : base(strokeData)
        {
        }

        protected abstract Func<double, double>[] Functions { get; }

        protected override Func<double, double> GetFitFunction()
            => NumericsFit.LinearCombinationFunc(StrokeData.X, StrokeData.Y, Functions);

        protected double[] GetCoefficients()
            => NumericsFit.LinearCombination(StrokeData.X, StrokeData.Y, Functions);
    }
}
