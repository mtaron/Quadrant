using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MathNet.Numerics.Statistics;
using Microsoft.HockeyApp;
using Microsoft.HockeyApp.DataContracts;
using Windows.Storage;

namespace Quadrant.Telemetry
{
    internal sealed class AppTelemetry
    {
        private const string TelemetryEnabledKey = "TelemetryEnabled";
        private readonly Dictionary<string, RunningStatistics> _metrics = new Dictionary<string, RunningStatistics>();
        private Stopwatch _loadStopwatch;
        private int _supressionCount;
        private bool? _isEnabled;

        private AppTelemetry()
        {
            if (IsEnabled)
            {
                HockeyClient.Current.Configure("837863c54ceb421380026282688dd7c8");
            }
        }

        public static AppTelemetry Current { get; } = new AppTelemetry();

        public bool IsEnabled
        {
            get
            {
#if DEBUG
                return false;
#else
                if (IsSupressed)
                {
                    return false;
                }

                if (_isEnabled.HasValue)
                {
                    return _isEnabled.Value;
                }

                if (ApplicationData.Current.RoamingSettings.Values.TryGetValue(TelemetryEnabledKey, out object isEnabledObject)
                    && isEnabledObject is bool)
                {
                    _isEnabled = (bool)isEnabledObject;
                }
                else
                {
                    _isEnabled = true;
                }

                return _isEnabled.Value;
#endif
            }

            set
            {
                _isEnabled = value;
                ApplicationData.Current.RoamingSettings.Values[TelemetryEnabledKey] = value;
            }
        }

        private bool IsSupressed
        {
            get { return _supressionCount > 0; }
        }

        public IDisposable Supress()
        {
            return new SupressionToken(this);
        }

        public void TrackLoadStart()
        {
            _loadStopwatch = Stopwatch.StartNew();
        }

        public void TrackLoadStop()
        {
            if (_loadStopwatch == null)
            {
                return;
            }

            _loadStopwatch.Stop();
            if (IsEnabled)
            {
                var telemetry = new MetricTelemetry(TelemetryMetrics.Load, _loadStopwatch.ElapsedMilliseconds);
                SetCommonProperties(telemetry.Properties);
                HockeyClient.Current.TrackMetric(telemetry);
            }
            _loadStopwatch = null;
        }

        public void TrackEvent(string eventName)
        {
            if (IsEnabled)
            {
                EventTelemetry eventTelemetry = CreateEventTelemetry(eventName);
                HockeyClient.Current.TrackEvent(eventTelemetry);
            }
        }

        public void TrackEvent(string eventName, string propertyName, string propertyValue)
        {
            if (IsEnabled)
            {
                EventTelemetry eventTelemetry = CreateEventTelemetry(eventName);
                eventTelemetry.Properties.Add(propertyName, propertyValue);
                HockeyClient.Current.TrackEvent(eventTelemetry);
            }
        }

        public void TrackEvent(string eventName, string propertyName, bool propertyValue)
        {
            TrackEvent(eventName, propertyName, propertyValue.ToString());
        }

        public void TrackEvent(
            string eventName,
            string propertyNameOne, string propertyValueOne,
            string propertyNameTwo, string propertyValueTwo)
        {
            if (IsEnabled)
            {
                EventTelemetry eventTelemetry = CreateEventTelemetry(eventName);
                eventTelemetry.Properties.Add(propertyNameOne, propertyValueOne);
                eventTelemetry.Properties.Add(propertyNameTwo, propertyValueTwo);
                HockeyClient.Current.TrackEvent(eventTelemetry);
            }
        }

        public IDisposable TrackDuration(string metricName)
        {
            if (IsEnabled)
            {
                return new DurationTracker(this, metricName);
            }

            return Disposable.Empty;
        }

        public void Flush()
        {
            if (!IsEnabled)
            {
                return;
            }

            lock (_metrics)
            {
                foreach (var metric in _metrics)
                {
                    RunningStatistics statistics = metric.Value;
                    if (statistics.Count > 0)
                    {
                        MetricTelemetry metrics = CreateMetricTelemetry(metric.Key, statistics);
                        HockeyClient.Current.TrackMetric(metrics);
                    }
                }

                _metrics.Clear();
            }

            HockeyClient.Current.Flush();
        }

        private void TrackMetric(string metricName, double value)
        {
            lock (_metrics)
            {
                if (!_metrics.TryGetValue(metricName, out RunningStatistics statistics))
                {
                    statistics = new RunningStatistics();
                    _metrics.Add(metricName, statistics);
                }

                statistics.Push(value);
            }
        }

        private EventTelemetry CreateEventTelemetry(string eventName)
        {
            var telemetry = new EventTelemetry(eventName);
            SetCommonProperties(telemetry.Properties);
            return telemetry;
        }

        private MetricTelemetry CreateMetricTelemetry(string metricName, RunningStatistics statistics)
        {
            var telemetry = new MetricTelemetry()
            {
                Name = metricName,
                Count = (int)statistics.Count,
                Value = statistics.Mean,
                Max = statistics.Maximum,
                Min = statistics.Minimum
            };

            if (statistics.Count >= 2)
            {
                telemetry.StandardDeviation = statistics.StandardDeviation;
            }

            SetCommonProperties(telemetry.Properties);

            return telemetry;
        }

        private void SetCommonProperties(IDictionary<string, string> properties)
        {
#if DEBUG
            properties.Add(TelemetryProperties.IsDebug, "true");
#endif
        }

        private sealed class SupressionToken : IDisposable
        {
            private readonly AppTelemetry _appTelemetry;

            public SupressionToken(AppTelemetry appTelemetry)
            {
                _appTelemetry = appTelemetry;
                Interlocked.Increment(ref _appTelemetry._supressionCount);
            }

            public void Dispose()
            {
                Interlocked.Decrement(ref _appTelemetry._supressionCount);
            }
        }

        private sealed class DurationTracker : IDisposable
        {
            private readonly string _metricName;
            private readonly AppTelemetry _telemetry;
            private readonly Stopwatch _stopwatch;
            private bool _isDisposed;

            public DurationTracker(AppTelemetry telemetry, string metricName)
            {
                _telemetry = telemetry;
                _metricName = metricName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _stopwatch.Stop();
                long duration = _stopwatch.ElapsedMilliseconds;
                _telemetry.TrackMetric(_metricName, duration);
            }
        }

        private sealed class Disposable : IDisposable
        {
            public static readonly Disposable Empty = new Disposable();

            public void Dispose()
            {
            }
        }
    }
}
