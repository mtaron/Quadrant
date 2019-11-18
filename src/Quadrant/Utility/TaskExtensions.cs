using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AppCenter.Crashes;

namespace Quadrant.Utility
{
    internal static class TaskExtensions
    {
        public static Task TrackExceptions(this Task task, CancellationToken cancelationToken)
        {
            return task.ContinueWith(t =>
                {
                    AggregateException exceptions = t.Exception.Flatten();
                    foreach (Exception exception in exceptions.InnerExceptions)
                    {
                        Crashes.TrackError(exception);
                    }
                },
                cancelationToken,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
