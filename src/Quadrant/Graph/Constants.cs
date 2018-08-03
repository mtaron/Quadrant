using System.Numerics;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI.Text;

namespace Quadrant.Graph
{
    internal static class LogicalConstants
    {
        public const float MinimumOriginValue = -1E20f;

        public const float MaximumOriginValue = 1E20f;

        public const float MinimumZoom = 0.01f;

        public const float MaximumZoom = 1E10f;

        public const float DefaultDesiredLongEdge = 22;

        public const double DefaultScale = 1;
    }

    internal static class DisplayConstants
    {
        public const float BackgroundBlurAmount = 12f;

        public const float DefaultEvaluationInterval = 3;

        public const float MinimumEvaluationInterval = 0.1f;

        public const float CanceledEvaluationInterval = 2f;

        public const double InterialDeceleration = -0.999;

        public const float MaxScaleDelta = 0.5f;

        public const float KeyboardScaleFactor = 10;

        public const float MouseWheelScaleFactor = 1 / 400f;

        public const float SnappingTolerance = 8;

        public const float MajorGridLineWidth = 0.2f;

        public const float MinorGridLineWidth = 0.1f;

        public const float AxisLineWidth = 1f;

        public const double ContactRectSize = 30;

        public static readonly Vector2 GridInterval = 80 * Vector2.One;

        public static readonly CanvasTextFormat NumberFormat = new CanvasTextFormat()
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiLight,
            WordWrapping = CanvasWordWrapping.NoWrap,
        };
    }
}
