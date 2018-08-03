using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Quadrant.UITest.Framework;

namespace Quadrant.UITest
{
    public sealed class QuadrantTestContext : PerformanceTestContext
    {
        public const string QuadrantPackageFamilyName = "30267MichaelTaron.Quadrant_gwd693w8am0m6";

        private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(1);
        private readonly SemaphoreSlim _closeSemaphore = new SemaphoreSlim(initialCount: 0, maxCount: 1);

        private readonly List<double> _elementCreationTimes = new List<double>();
        private readonly List<double> _resourceDictionaryAddTimes = new List<double>();

        public QuadrantTestContext(TraceEventDispatcher source)
            : base(source)
        {
            source.Dynamic.AddCallbackForProviderEvent(
                QuadrantPerformanceTestAttribute.QuadrantProvider,
                "AppSuspended",
                e =>
                {
                    source.StopProcessing();
                    _closeSemaphore.Release();
                });

            source.Dynamic.AddCallbackForProviderEvent(
                PerformanceTestAttribute.XamlProviderName,
                "ElementCreated",
                e => _elementCreationTimes.Add(e.TimeStampRelativeMSec));

            source.Dynamic.AddCallbackForProviderEvent(
                PerformanceTestAttribute.XamlProviderName,
                "ResourceDictionaryAdd",
                e => _resourceDictionaryAddTimes.Add(e.TimeStampRelativeMSec));
        }

        protected override string PackageFamilyName => QuadrantPackageFamilyName;

        public IDisposable TrackLoad()
        {
            return Track<LoadScenario>();
        }

        public IDisposable TrackSuspend()
        {
            return Track<SuspendScenario>();
        }

        public IDisposable TrackCompile(string expression)
        {
            var scenario = new CompileScenario(expression);
            Track(scenario);
            return scenario;
        }

        public IDisposable TrackDrawFunction(string expression)
        {
            var scenario = new DrawFunctionScenario(expression);
            Track(scenario);
            return scenario;
        }

        public IDisposable TrackDrawGraph(string scenarioName = "Draw graph")
        {
            var scenario = new DrawGraphScenario(scenarioName);
            Track(scenario);
            return scenario;
        }

        public void WaitForClose()
        {
            _closeSemaphore.Wait(CloseTimeout);
        }

        public void AggregateDrawGraph()
        {
            Track<AggregateDrawGraphScenario>();
        }

        protected override void ComputeCounters()
        {
            foreach (Scenario scenario in Scenarios)
            {
                var elementCreationCounter = new Counter(
                    "ElementsCreated",
                    _elementCreationTimes.Where(t => scenario.Contains(t)).Count());

                scenario.AddCounter(elementCreationCounter);

                var resourceAddedCounter = new Counter(
                    "ResourcesAdded",
                    _resourceDictionaryAddTimes.Where(t => scenario.Contains(t)).Count());

                scenario.AddCounter(resourceAddedCounter);
            }
        }

        private class QuadrantDiscreteScenario : DiscreteScenario
        {
            public QuadrantDiscreteScenario(string name, string startEvent, string endEvent)
                : base(name,
                      QuadrantPerformanceTestAttribute.QuadrantProvider,
                      startEvent,
                      QuadrantPerformanceTestAttribute.QuadrantProvider,
                      endEvent)
            {
            }
        }

        private class QuadrantAggregateScenario : AggregateScenario
        {
            public QuadrantAggregateScenario(string name, string startEvent, string endEvent)
                : base(name,
                      QuadrantPerformanceTestAttribute.QuadrantProvider,
                      startEvent,
                      QuadrantPerformanceTestAttribute.QuadrantProvider,
                      endEvent)
            {
            }
        }

        private class CompileScenario : QuadrantDiscreteScenario
        {
            public CompileScenario(string expression)
                : base($"Compile {expression}", "Compile/Start", "Compile/Stop")
            {
                if (string.IsNullOrEmpty(expression))
                {
                    throw new ArgumentNullException(nameof(expression));
                }
            }
        }

        private class DrawFunctionScenario : QuadrantDiscreteScenario
        {
            private readonly string _function;

            public DrawFunctionScenario(string function)
                : base($"Draw {function}", "DrawFunction/Start", "DrawFunction/Stop")
            {
                if (string.IsNullOrEmpty(function))
                {
                    throw new ArgumentNullException(nameof(function));
                }

                _function = function;
            }

            protected override void OnStart(TraceEvent startEvent)
            {
                string functionName = GetFunction(startEvent);
                if (functionName != null && functionName.EndsWith(_function, StringComparison.OrdinalIgnoreCase))
                {
                    base.OnStart(startEvent);
                }
            }

            private static string GetFunction(TraceEvent traceEvent)
            {
                return traceEvent.PayloadByName("Function") as string;
            }
        }

        private class DrawGraphScenario : QuadrantDiscreteScenario
        {
            public DrawGraphScenario(string scenarioName)
                : base(scenarioName, "DrawGraph/Start", "DrawGraph/Stop")
            {
            }
        }

        private class AggregateDrawGraphScenario : QuadrantAggregateScenario
        {
            public AggregateDrawGraphScenario()
                : base("Aggregate graph draws", "DrawGraph/Start", "DrawGraph/Stop")
            {
            }
        }

        private class LoadScenario : QuadrantDiscreteScenario
        {
            public LoadScenario()
                : base("Load", "AppConstructed", "DrawGraph/Stop")
            {
            }
        }

        private class SuspendScenario : QuadrantDiscreteScenario
        {
            public SuspendScenario()
                : base("Suspend", "Suspend/Start", "Suspend/Stop")
            {
            }
        }
    }
}
