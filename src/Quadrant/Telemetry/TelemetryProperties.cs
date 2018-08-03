namespace Quadrant.Telemetry
{
    public static class TelemetryProperties
    {
        public const string PrintResult = nameof(PrintResult);
        public const string Function = nameof(Function);
        public const string AngleType = nameof(AngleType);
        public const string RecenterType = nameof(RecenterType);
        public const string ScaleX = nameof(ScaleX);
        public const string ScaleY = nameof(ScaleY);
        public const string IsChecked = nameof(IsChecked);
        public const string IsSuccess = nameof(IsSuccess);
#if DEBUG
        public const string IsDebug = nameof(IsDebug);
#endif
    }
}
