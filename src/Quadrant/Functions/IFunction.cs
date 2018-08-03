using System;
using Windows.UI;

namespace Quadrant.Functions
{
    public interface IFunction
    {
        Func<double, double> Function { get; }
        Func<double, double> Derivative { get; }
        Color Color { get; }
        string DisplayExpression { get; }
    }
}
