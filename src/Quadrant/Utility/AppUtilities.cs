using System.Globalization;
using Windows.ApplicationModel.Resources;

namespace Quadrant.Utility
{
    internal static class AppUtilities
    { 
        public static string GetString(string keyName, params object[] arguments)
        {
            ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("Resources");
            string resource = resourceLoader.GetString(keyName);

            if (arguments != null && arguments.Length > 0)
            {
                return string.Format(CultureInfo.CurrentUICulture, resource, arguments);
            }

            return resource;
        }
    }
}
