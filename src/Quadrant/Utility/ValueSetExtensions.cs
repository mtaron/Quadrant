using Windows.Foundation.Collections;

namespace Quadrant.Utility
{
    public static class ValueSetExtensions
    {
        public static T GetValueOrDefault<T>(this ValueSet data, string key)
        {
            if (data.TryGetValue(key, out object value) && value is T)
            {
                return (T)value;
            }

            return default;
        }
    }
}
