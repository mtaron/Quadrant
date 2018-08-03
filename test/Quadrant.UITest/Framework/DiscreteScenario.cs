using System;
using System.Threading;
using Microsoft.Diagnostics.Tracing;

namespace Quadrant.UITest.Framework
{
    /// <summary>
    /// Records the start and end time of a single scenario.
    /// </summary>
    public abstract class DiscreteScenario : Scenario, IDisposable
    {
        private readonly DateTime _scenarioCreationTime;
        private readonly SemaphoreSlim _endSemaphore = new SemaphoreSlim(initialCount: 0, maxCount: 1);
        private double? _startTime;
        private double? _endTime;
        private bool _isDisposed;

        public DiscreteScenario(
            string name,
            string startEventProvider,
            string startEvent,
            string endEventProvider,
            string endEvent)
            : base(name, startEventProvider, startEvent, endEventProvider, endEvent)
        {
            _scenarioCreationTime = DateTime.Now;
        }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

        protected override void LogResultInternal(PerformanceTestContext context)
        {
            if (_startTime == null)
            {
                context.LogError($"Did not find expected start event {StartEvent} from provider {StartEventProvider}.");
            }
            else if (_endTime == null)
            {
                context.LogError($"Did not find expected end event {EndEvent} from provider {EndEventProvider}.");
            }
            else
            {
                double duration = Math.Round(_endTime.Value - _startTime.Value, 3);
                context.LogMessage($"{Name}: {duration}");
            }
        }

        public override bool Contains(double timeStamp)
        {
            if (!_endTime.HasValue || !_startTime.HasValue)
            {
                return false;
            }

            return timeStamp >= _startTime.Value && timeStamp <= _endTime.Value;
        }

        protected override void OnStart(TraceEvent startEvent)
        {
            if (_startTime == null && startEvent.TimeStamp >= _scenarioCreationTime)
            {
                _startTime = startEvent.TimeStampRelativeMSec;
            }
        }

        protected override void OnEnd(TraceEvent endEvent)
        {
            if (_startTime == null || endEvent.TimeStampRelativeMSec <= _startTime)
            {
                return;
            }

            _endTime = endEvent.TimeStampRelativeMSec;
            Unregister();
            _endSemaphore.Release();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing && !_endTime.HasValue)
                {
                    _endSemaphore.Wait(Timeout);
                }
            }

            _isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }
    }
}
