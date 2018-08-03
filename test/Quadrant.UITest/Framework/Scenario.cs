using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace Quadrant.UITest.Framework
{
    public abstract class Scenario
    {
        private TraceEventDispatcher _source;
        private readonly List<Counter> _counters = new List<Counter>();

        public Scenario(
            string name,
            string startEventProvider,
            string startEvent,
            string endEventProvider,
            string endEvent)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            StartEventProvider = startEventProvider ?? throw new ArgumentNullException(nameof(startEventProvider));
            StartEvent = startEvent ?? throw new ArgumentNullException(nameof(startEvent));
            EndEventProvider = endEventProvider ?? throw new ArgumentNullException(nameof(endEventProvider));
            EndEvent = endEvent ?? throw new ArgumentNullException(nameof(endEvent));
        }

        public string Name { get; }
        public string StartEventProvider { get; }
        public string StartEvent { get; }
        public string EndEventProvider { get; }
        public string EndEvent { get; }

        public IReadOnlyList<Counter> Counters
        {
            get => _counters;
        }

        public void Register(TraceEventDispatcher source)
        {
            _source = source;
            DynamicTraceEventParser dynamic = source.Dynamic;
            dynamic.AddCallbackForProviderEvent(StartEventProvider, StartEvent, OnStart);
            dynamic.AddCallbackForProviderEvent(EndEventProvider, EndEvent, OnEnd);
        }

        public void Unregister()
        {
            if (_source != null)
            {
                DynamicTraceEventParser dynamic = _source.Dynamic;
                dynamic.RemoveCallback<TraceEvent>(OnStart);
                dynamic.RemoveCallback<TraceEvent>(OnEnd);
            }
        }

        public void AddCounter(Counter counter)
            => _counters.Add(counter);

        public void LogResult(PerformanceTestContext context)
        {
            LogResultInternal(context);

            foreach (Counter counter in Counters)
            {
                context.LogMessage($"\t{counter.Name}: {counter.Count}");
            }
        }

        protected abstract void LogResultInternal(PerformanceTestContext context);
        public abstract bool Contains(double timeStamp);

        protected abstract void OnStart(TraceEvent startEvent);
        protected abstract void OnEnd(TraceEvent endEvent);
    }
}
