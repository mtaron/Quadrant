using System;
using Windows.UI.Xaml.Data;

namespace Quadrant.Converters
{
    public sealed class ScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Math.Log((double)value) * 21.714725;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return Math.Pow(Math.E, 0.0460517 * (double)value);
        }
    }
}
