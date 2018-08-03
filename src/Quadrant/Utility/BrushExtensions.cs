using Windows.UI;
using Windows.UI.Xaml.Media;

namespace Quadrant.Utility
{
    internal static class BrushExtensions
    {
        public static Color SolidColor(this Brush brush)
        {
            return ((SolidColorBrush)brush).Color;
        }
    }
}
