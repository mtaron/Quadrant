using System;
using System.Threading.Tasks;
using Microsoft.HockeyApp;

namespace Quadrant.Utility
{
    internal static class TaskExtensions
    {
        public static void TrackExceptions(this Task task)
        {
            task.ContinueWith(t =>
            {
                AggregateException exceptions = t.Exception.Flatten();
                foreach (Exception exception in exceptions.InnerExceptions)
                {
                    HockeyClient.Current.TrackException(exception);
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
