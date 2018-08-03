using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quadrant.Ink.Fit;

namespace Quadrant.Ink
{
    internal static class InkToFunction
    {
        private static readonly Func<StrokeData, StrokeFit>[] FitFunctions = new Func<StrokeData, StrokeFit>[]
        {
            s => new ConstantFit(s),
            s => new LinearFit(s),
            s => new PolynomialFit(s, 2),
            s => new PolynomialFit(s, 3),
            s => new OscillatorFit(s, "sin", x => Math.Sin(x)),
            s => new OscillatorFit(s, "cos", x => Math.Cos(x)),
            s => new LeftShiftFit(s, "log", x => Math.Log(x)),
            s => new NegativeLogFit(s),
            s => new FunctionFit(s, "e^x", x => Math.Exp(x)),
            s => new FunctionFit(s, "e^-x", x => Math.Exp(-x)),
            s => new ReciprocalFit(s, -1),
            s => new ReciprocalFit(s, -2),
            s => new CenterShiftFit(s, "abs", x => Math.Abs(x)),
            s => new FunctionFit(s, "acos(x)", x => Math.Acos(x)),
            s => new FunctionFit(s, "asin(x)", x => Math.Asin(x)),
            s => new FunctionFit(s, "sinh(x)", x => Math.Sinh(x)),
            s => new CenterShiftFit(s, "cosh", x => Math.Cosh(x)),
            s => new IntersectionShiftFit(s, "tanh", x => Math.Tanh(x)),
            s => new IntersectionShiftFit(s, "atan", x => Math.Atan(x)),
        };

        public static Task<IEnumerable<StrokeFit>> GetFunctionAsync(TransformedStrokes strokes)
        {
            return Task.Run(() => GetFunctions(strokes));
        }

        private static IEnumerable<StrokeFit> GetFunctions(TransformedStrokes strokes)
        {
            if (strokes == null)
            {
                return Enumerable.Empty<StrokeFit>();
            }

            StrokeData strokeData = strokes.GetStrokeData();
            if (strokeData.Points.Length <= 3)
            {
                return Enumerable.Empty<StrokeFit>();
            }

            return FitFunctions.Select(f => f(strokeData)).Where(f => f.IsValid).AsParallel().OrderBy(f => f);
        }
    }
}
