using System;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using System.Collections.Generic;

namespace Quadrant.UITest.Framework
{
    /// <summary>
    /// Aggregates the durations of the specified scenario across the session duration.
    /// </summary>
    public abstract class AggregateScenario : Scenario
    {
        private readonly Dictionary<Guid, Duration> _activites = new Dictionary<Guid, Duration>();
        private readonly List<Duration> _durations = new List<Duration>();

        public AggregateScenario(
            string name,
            string startEventProvider,
            string startEvent,
            string endEventProvider,
            string endEvent)
            : base(name, startEventProvider, startEvent, endEventProvider, endEvent)
        {
        }

        protected override void LogResultInternal(PerformanceTestContext context)
        {
            IReadOnlyCollection<Duration> durations = GetDurations();
            double average = Math.Round(durations.Select(d => d.End - d.Start).Average(), 3);
            context.LogMessage($"{Name}: {durations.Count} events with average duration of {average} ms");
        }

        public override bool Contains(double timeStamp)
        {
            foreach (Duration duration in GetDurations())
            {
                if (timeStamp >= duration.Start && timeStamp <= duration.End)
                {
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyCollection<Duration> GetDurations()
        {
            return _activites.Any() ? (IReadOnlyCollection<Duration>)_activites.Values : _durations;
        }

        protected override void OnStart(TraceEvent startEvent)
        {
            var duration = new Duration() { Start = startEvent.TimeStampRelativeMSec };
            if (startEvent.ActivityID != Guid.Empty)
            {
                _activites.Add(startEvent.ActivityID, duration);
            }
            else
            {
                _durations.Add(duration);
            }
        }

        protected override void OnEnd(TraceEvent endEvent)
        {
            if (endEvent.ActivityID != Guid.Empty)
            {
                _activites[endEvent.ActivityID].End = endEvent.TimeStampRelativeMSec;
            }
            else
            {
                _durations.Last().End = endEvent.TimeStampRelativeMSec;
            }
        }

        private sealed class Duration
        {
            public double Start { get; set; }
            public double End { get; set; }
        }
    }
}
