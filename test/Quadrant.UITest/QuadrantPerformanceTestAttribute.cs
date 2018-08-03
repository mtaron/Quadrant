using System;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Quadrant.UITest.Framework;

namespace Quadrant.UITest
{
    public class QuadrantPerformanceTestAttribute : PerformanceTestAttribute
    {
        public const string QuadrantProvider = "QuadrantApp";

        protected override void EnableProviders(TraceEventSession session)
        {
            var options = new TraceEventProviderOptions()
            {
                ProcessNameFilter = new string[] { "Quadrant.exe" }
            };

            Guid eventSource = TraceEventProviders.GetEventSourceGuidFromName(QuadrantProvider);
            session.EnableProvider(eventSource, options: options);

            EnabledDefaultProviders(session, options);
        }

        protected override PerformanceTestContext CreateContext(TraceEventDispatcher source)
        {
            return new QuadrantTestContext(source);
        }

        protected override void OnIterationEnded(PerformanceTestContext context)
        {
            var quadrantScenarios = (QuadrantTestContext)context;
            quadrantScenarios.WaitForClose();
        }
    }
}
