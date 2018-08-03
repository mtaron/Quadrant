using System.Numerics;
using Windows.Foundation;

namespace Quadrant.Utility
{
    public static class RectExtensions
    {
        public static double Area(this Rect rect)
            => rect.Width * rect.Height;

        public static Rect Inflate(this Rect rect, double value)
        {
            double halfValue = value / 2;
            return new Rect(new Point(rect.Left - halfValue, rect.Top - halfValue), new Point(rect.Right + halfValue, rect.Bottom + halfValue));
        }

        public static Rect Transform(this Rect rect, Matrix3x2 transform)
        {
            var topLeft = new Vector2((float)rect.X, (float)rect.Y);
            var bottomRight = new Vector2((float)rect.Right, (float)rect.Bottom);
            return new Rect(Vector2.Transform(topLeft, transform).ToPoint(), Vector2.Transform(bottomRight, transform).ToPoint());
        }

        public static Rect Offset(this Rect rect, Vector2 offset)
            => new Rect(rect.Left + offset.X, rect.Top + offset.Y, rect.Width, rect.Height);
    }
}
