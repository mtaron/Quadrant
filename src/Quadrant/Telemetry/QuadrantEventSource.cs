using System.Diagnostics.Tracing;

namespace Quadrant.Telemetry
{
    public sealed class QuadrantEventSource : EventSource
    {
        public static readonly QuadrantEventSource Log = new QuadrantEventSource();

        private static readonly EventSourceOptions InformationalOptions = new EventSourceOptions()
        {
            Level = EventLevel.Informational
        };

        private static readonly EventSourceOptions InformationalStartOptions = new EventSourceOptions()
        {
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start
        };

        private static readonly EventSourceOptions InformationalStopOptions = new EventSourceOptions()
        {
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop
        };

        private static readonly EventSourceOptions StartOptions = new EventSourceOptions()
        {
            Opcode = EventOpcode.Start
        };

        private static readonly EventSourceOptions StopOptions = new EventSourceOptions()
        {
            Opcode = EventOpcode.Stop
        };

        private QuadrantEventSource()
            : base("QuadrantApp")
        {
        }

        public void AppConstructed() => Write(nameof(AppConstructed), InformationalOptions);

        public void AppSuspended() => Write(nameof(AppSuspended), InformationalOptions);

        public void ViewCreated(int id) => Write(nameof(ViewCreated), InformationalOptions, new { Id = id });

        public void ViewClosed(int id) => Write(nameof(ViewClosed), InformationalOptions, new { Id = id });

        private const string Prelaunch = "Prelaunch";
        public void PrelaunchStart() => Write(Prelaunch, InformationalStartOptions);
        public void PrelaunchStop() => Write(Prelaunch, InformationalStopOptions);

        private const string Compile = "Compile";
        public void CompileStart() => Write(Compile, InformationalStartOptions);
        public void CompileStop(bool hasError) => Write(Compile, InformationalStopOptions, new { HasError = hasError });

        private const string DrawGraph = "DrawGraph";
        public void DrawGraphStart(double height, double width) => Write(DrawGraph, StartOptions, new { Height = height, Width = width } );
        public void DrawGraphStop() => Write(DrawGraph, StopOptions);

        private const string DrawFunction = "DrawFunction";
        public void DrawFunctionStart(string function) => Write(DrawFunction, StartOptions, new { Function = function });
        public void DrawFunctionStop() => Write(DrawFunction, StopOptions);

        public void DrawFunctionCanceled(string function) => Write(nameof(DrawFunctionCanceled), new { Function = function });

        private const string Suspend = "Suspend";
        public void SuspendStart() => Write(Suspend, InformationalStartOptions);
        public void SuspendStop() => Write(Suspend, InformationalStopOptions);

        private const string Resume = "Resume";
        public void ResumeStart() => Write(Resume, InformationalStartOptions);
        public void ResumeStop() => Write(Resume, InformationalStopOptions);

        private const string DryInk = "DryInk";
        public void DryInkStart() => Write(DryInk, StartOptions);
        public void DryInkStop() => Write(DryInk, StopOptions);
    }
}
