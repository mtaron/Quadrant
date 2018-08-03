using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Diagnostics.Tracing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Quadrant.UITest.Framework
{
    public abstract class PerformanceTestContext
    {
        private readonly TraceEventDispatcher _source;
        private readonly ETWReloggerTraceEventSource _relogger;
        private readonly List<Scenario> _scenarios = new List<Scenario>();
        private StringBuilder _errorString;
        private StringBuilder _messageString;

        private AppResourceGroupInfo _appInfo;
        private AppResourceGroupMemoryReport _initialMemoryReport;
        private AppResourceGroupMemoryReport _finalMemoryReport;

        protected PerformanceTestContext(TraceEventDispatcher source)
        {
            _source = source;
            _relogger = source as ETWReloggerTraceEventSource;
            IsWriteEnabled = _relogger != null;

            if (IsWriteEnabled)
            {
                _relogger.Dynamic.All += WriteEvent;
                _relogger.Kernel.All += WriteEvent;
                _relogger.Clr.All += WriteEvent;
            }
        }

        protected IReadOnlyList<Scenario> Scenarios
        {
            get => _scenarios;
        }

        protected bool IsWriteEnabled { get; } 

        protected abstract string PackageFamilyName { get; }

        public async Task StartAsync()
        {
            IList<AppDiagnosticInfo> infos = await AppDiagnosticInfo.RequestInfoForPackageAsync(PackageFamilyName);
            infos.Count.Should().Be(1);

            AppActivationResult activationResult = await infos[0].LaunchAsync();
            if (activationResult.AppResourceGroupInfo == null)
            {
                throw activationResult.ExtendedError;
            }

            _appInfo = activationResult.AppResourceGroupInfo;
            _initialMemoryReport = _appInfo.GetMemoryReport();

            if (Debugger.IsAttached)
            {
                // Wait here to allow time to attach to the running app, if desired.
                Debugger.Break();
            }
        }

        public async Task CloseAsync()
        {
            _finalMemoryReport = _appInfo.GetMemoryReport();
            AppExecutionStateChangeResult changeResult = await _appInfo.StartTerminateAsync();
            if (changeResult.ExtendedError != null)
            {
                throw changeResult.ExtendedError;
            }
        }

        public async Task SuspendAsync()
        {
            AppExecutionStateChangeResult changeResult = await _appInfo.StartSuspendAsync();
            if (changeResult.ExtendedError != null)
            {
                throw changeResult.ExtendedError;
            }
        }

        public async Task ResumeAsync()
        {
            AppExecutionStateChangeResult changeResult = await _appInfo.StartResumeAsync();
            if (changeResult.ExtendedError != null)
            {
                throw changeResult.ExtendedError;
            }
        }

        public void LogMemoryDelta()
        {
            ulong privateDelta = _finalMemoryReport.PrivateCommitUsage - _initialMemoryReport.PrivateCommitUsage;
            ulong totalDelta = _finalMemoryReport.TotalCommitUsage - _initialMemoryReport.TotalCommitUsage;

            LogMessage($"\r\nMemory usage delta:\r\nTotal: {totalDelta}\r\nPrivate: {privateDelta}");
        }

        public void LogMemoryReport(string name = null)
        {
            if (name != null)
            {
                LogMessage(name);
            }

            AppResourceGroupMemoryReport report = _appInfo.GetMemoryReport();
            LogMessage($"Commit usage level: {report.CommitUsageLevel}");
            LogMessage($"Private commit usage: {report.PrivateCommitUsage}");
            LogMessage($"Total commit usage: {report.TotalCommitUsage}");
        }

        public void LogError(string message)
        {
            if (_errorString == null)
            {
                _errorString = new StringBuilder();
            }

            _errorString.AppendLine(message);
        }

        public void LogMessage(string message)
        {
            if (_messageString == null)
            {
                _messageString = new StringBuilder();
            }

            _messageString.AppendLine(message);
        }

        public void WriteLogsToResult(TestResult result, ZippedETLWriter zipWriter = null)
        {
            if (zipWriter != null)
            {
                if (!zipWriter.WriteArchive())
                {
                    LogError("Failed to create zip archive");
                }
                else
                {
                    result.ResultFiles = new string[] { zipWriter.ZipArchivePath };
                }
            }

            if (_errorString != null)
            {
                result.LogError = _errorString.ToString();
                if (result.Outcome == UnitTestOutcome.Passed
                    || result.Outcome == UnitTestOutcome.Unknown)
                {
                    result.Outcome = UnitTestOutcome.Error;
                }
            }

            if (_messageString != null)
            {
                result.LogOutput = _messageString.ToString();
            }
        }

        public void LogScenarios()
        {
            ComputeCounters();

            LogMessage("\r\nScenario results (ms)");
            foreach (Scenario scenario in _scenarios)
            {
                scenario.LogResult(this);
            }
        }

        protected virtual void ComputeCounters()
        {
        }

        protected void WriteEvent(TraceEvent traceEvent)
        {
            _relogger.WriteEvent(traceEvent);
        }

        protected void Track(Scenario scenario)
        {
            _scenarios.Add(scenario);
            scenario.Register(_source);
        }

        protected T Track<T>() where T : Scenario, new()
        {
            T scenario = new T();
            Track(scenario);
            return scenario;
        }
    }
}
