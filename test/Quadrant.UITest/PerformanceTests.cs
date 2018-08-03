using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quadrant.UITest
{
    [TestClass]
    public class PerformanceTests : QuadrantTest
    {
        [QuadrantPerformanceTest]
        public async Task PlotTwoFunctions(QuadrantTestContext context)
        {
            using (context.TrackLoad())
            using (context.TrackDrawGraph("First graph"))
            {
                await context.StartAsync();
            }

            const string functionOne = "e^x";
            using (context.TrackCompile(functionOne))
            using (context.TrackDrawFunction(functionOne))
            using (context.TrackDrawGraph("Graph with single function"))
            {
                AddFunction(functionOne);
            }

            const string functionTwo = "tan(x)/2";
            using (context.TrackCompile(functionTwo))
            using (context.TrackDrawFunction(functionTwo))
            using (context.TrackDrawGraph("Graph with two functions"))
            {
                AddFunction(functionTwo);
            }

            await context.CloseAsync();
        }

        [QuadrantPerformanceTest]
        public async Task PlotOneHundredSinFunctions(QuadrantTestContext context)
        {
            context.AggregateDrawGraph();

            await context.StartAsync();

            for (int i = -50; i < 50; i++)
            {
                AddFunction($"sin(x + {i}) + {i}/2");
            }

            await context.CloseAsync();
        }

        [QuadrantPerformanceTest]
        public async Task Suspend(QuadrantTestContext context)
        {
            using (context.TrackLoad())
            {
                await context.StartAsync();
            }

            using (context.TrackSuspend())
            {
                await context.SuspendAsync();
            }

            await context.CloseAsync();
        }
    }
}
