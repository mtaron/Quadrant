using System;
using System.Numerics;

namespace Quadrant.Utility
{
    public static class Matrix3x2Extensions
    {
        public static Vector2 Scale(this Matrix3x2 matrix)
        {
            float xScale = GetLength(matrix.M11, matrix.M12);
            float yScale = GetLength(matrix.M21, matrix.M22);
            return new Vector2(xScale, yScale);
        }

        private static float GetLength(float x, float y)
            => (float)Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
    }
}
