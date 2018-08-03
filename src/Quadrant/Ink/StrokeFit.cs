using System;
using System.Globalization;
using MathNet.Numerics;
using Quadrant.Utility;

namespace Quadrant.Ink
{
    internal abstract class StrokeFit : IComparable<StrokeFit>, IComparable
    {
        protected enum FitGroup
        {
            None,
            Polynomial,
        }

        private double? _error;

        protected StrokeFit(in StrokeData strokeData)
        {
            StrokeData = strokeData;
        }

        public double Error
        {
            get
            {
                if (!_error.HasValue)
                {
                    _error = ComputeError();
                }

                return _error.Value;
            }
        }

        public abstract string GetExpression();

        protected StrokeData StrokeData { get; }

        public bool IsValid { get; protected set; } = true;

        protected virtual FitGroup Group { get; } = FitGroup.None;

        protected virtual double Cost { get; } = 1.0;

        protected abstract Func<double, double> GetFitFunction();

        protected string FormatValue(double value, bool includePlusSign)
        {
            if (!value.IsReal())
            {
                throw new ArgumentException(nameof(value));
            }

            int decimalPlaces = StrokeData.DecimalPlaces;
            string displayValue = value.ToString($"G{decimalPlaces}", CultureInfo.CurrentCulture);

            if (includePlusSign && value > 0.0)
            {
                return "+" + displayValue;
            }

            return displayValue;
        }

        private double ComputeError()
        {
            Func<double, double> functon = GetFitFunction();
            if (functon == null)
            {
                return double.PositiveInfinity;
            }

            double[] x = StrokeData.X;
            double[] y = StrokeData.Y;
            double[] modeledValues = new double[x.Length];
            for (int index = 0; index < x.Length; index++)
            {
                modeledValues[index] = functon(x[index]);
            }

            double error = GoodnessOfFit.PopulationStandardError(modeledValues, y);
            if (!error.IsReal())
            {
                return double.PositiveInfinity;
            }

            return error;
        }

        public int CompareTo(StrokeFit other)
        {
            if (other == this)
            {
                return 0;
            }

            if (other == null)
            {
                return -1;
            }

            double error = Error;
            double otherError = other.Error;
            double errorDifference = error - otherError;

            if (!double.IsNaN(errorDifference) && Group != other.Group)
            {
                return Math.Sign(errorDifference);
            }

            double costDifference = Cost - other.Cost;
            if (error == otherError)
            {
                return Math.Sign(costDifference);
            }

            if (error < otherError)
            {
                if (costDifference > 0 && otherError < costDifference * error)
                {
                    return 1;
                }

                return -1;
            }

            if (costDifference < 0 && error < -costDifference * otherError)
            {
                return -1;
            }

            return 1;
        }

        public int CompareTo(object obj)
            => CompareTo(obj as StrokeFit);
    }
}
