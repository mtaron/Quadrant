using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using MathNet.Numerics.Statistics;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
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
                AppCenter.Start("837863c5-4ceb-4213-8002-6282688dd7c8", typeof(Analytics), typeof(Crashes));
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
            TrackEvent(TelemetryMetrics.Load, "Value", _loadStopwatch.ElapsedMilliseconds);
            _loadStopwatch = null;
        }

        public void TrackEvent(string eventName)
        {
            if (IsEnabled)
            {
                TrackEvent(eventName, properties: null);
            }
        }

        public void TrackEvent(string eventName, string propertyName, string propertyValue)
        {
            if (IsEnabled)
            {
                var properties = new Dictionary<string, string>()
                {
                    { propertyName, propertyValue }
                };

                TrackEvent(eventName, properties);
            }
        }

        public void TrackEvent(string eventName, string propertyName, object propertyValue)
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
                var properties = new Dictionary<string, string>()
                {
                    { propertyNameOne, propertyValueOne },
                    { propertyNameTwo, propertyValueTwo }
                };

                TrackEvent(eventName, properties);
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
                        TrackMetricTelemetry(metric.Key, statistics);
                    }
                }

                _metrics.Clear();
            }
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

        private void TrackMetricTelemetry(string metricName, RunningStatistics statistics)
        {
            var properties = new Dictionary<string, string>()
            {
                { "Count", statistics.Count.ToString(CultureInfo.InvariantCulture) },
                { "Mean", statistics.Mean.ToString(CultureInfo.InvariantCulture) },
                { "Max", statistics.Maximum.ToString(CultureInfo.InvariantCulture) },
                { "Min", statistics.Minimum.ToString(CultureInfo.InvariantCulture) },
            };

            if (statistics.Count >= 2)
            {
                properties.Add("StandardDeviation", statistics.StandardDeviation.ToString(CultureInfo.InvariantCulture));
            }

            TrackEvent(metricName, properties);
        }

        private void TrackEvent(string name, IDictionary<string, string> properties)
        {
#if DEBUG
            if (properties == null)
            {
                properties = new Dictionary<string, string>(capacity: 1);
            }

            properties.Add(TelemetryProperties.IsDebug, "true");
#endif

            Analytics.TrackEvent(name, properties);
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
