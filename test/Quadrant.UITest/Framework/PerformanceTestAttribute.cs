using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quadrant.UITest.Framework
{
    /// <summary>
    /// Provides a <see cref="PerformanceTestContext"/> to a test method that is executed repeatedly.
    /// </summary>
    public abstract class PerformanceTestAttribute : TestMethodAttribute
    {
        public const string XamlProviderName = "Microsoft-Windows-XAML";
        public static readonly Guid XamlProviderGuid = new Guid("531A35AB-63CE-4BCF-AA98-F88C7A89E455");

        public const string DotNetRuntimeProviderName = "Microsoft-Windows-DotNETRuntime";
        public static readonly Guid DotNetRuntimeProviderGuid = new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85");

        private static readonly Type ContextType = typeof(PerformanceTestContext);

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            TestResult[] errorResults = ValidateElevated(testMethod);
            if (errorResults != null)
            {
                return errorResults;
            }

            Exception signatureException = GetMethodSignatureException(testMethod);
            if (signatureException != null)
            {
                return testMethod.CreateExceptionResult(signatureException);
            }

            var runParameters = TestRunParameters.Read();
            string logFolder = runParameters.LogFolder;
            bool shouldLog = !string.IsNullOrEmpty(logFolder);
            if (shouldLog)
            {
                try
                {
                    logFolder = CreateLogFolder(logFolder, testMethod.TestMethodName);
                }
                catch (Exception e)
                {
                    return testMethod.CreateExceptionResult(e);
                }
            }

            int iterations = runParameters.Iterations;
            var results = new TestResult[iterations];
            for (int iteration = 1; iteration <= iterations; iteration++)
            {
                string sessionName = $"{testMethod.TestMethodName}-{iteration}";

                using (var session = new TraceEventSession(sessionName))
                {
                    EnableKernelProviders(session, shouldLog);

                    TraceEventDispatcher source;
                    ZippedETLWriter writer = null;
                    if (shouldLog)
                    {
                        string etlPath = Path.Combine(logFolder, $"Iteration{iteration}.etl");
                        source = new ETWReloggerTraceEventSource(sessionName, TraceEventSourceType.Session, etlPath);
                        writer = new ZippedETLWriter(etlPath);
                    }
                    else
                    {
                        source = session.Source;
                    }

                    EnableProviders(session);
                    PerformanceTestContext context = CreateContext(source);

                    Task<TestResult> testTask = Task.Run(() => testMethod.Invoke(new object[] { context }));

                    // This is a blocking call that in the case of ETWReloggerTraceEventSource, must be run on the same
                    // thread as ETWReloggerTraceEventSource was created on. It will become unblocked when the 
                    // PerformanceTestContext calls StopProcessing on the source.
                    source.Process();

                    TestResult result = testTask.Result;
                    string displayName = testMethod.TestMethodName;
                    if (iterations > 1)
                    {
                        displayName += $" [{iteration}/{iterations}]";
                    }

                    result.DisplayName = displayName;

                    session.Flush();
                    OnIterationEnded(context);

                    context.LogScenarios();
                    context.LogMemoryDelta();
                    context.LogMessage($"{displayName} completed. {session.EventsLost} events lost.");
                    context.WriteLogsToResult(result, writer);

                    results[iteration - 1] = result;
                }
            }

            return results;
        }

        protected virtual void EnableKernelProviders(TraceEventSession session, bool isLoggingEnabled)
        {
            if (isLoggingEnabled)
            {
                // Traces cannot be symbolicated without the ImageLoad provider.
                session.EnableKernelProvider(
                    KernelTraceEventParser.Keywords.Profile
                    | KernelTraceEventParser.Keywords.Process
                    | KernelTraceEventParser.Keywords.ImageLoad
                    | KernelTraceEventParser.Keywords.Thread,
                    KernelTraceEventParser.Keywords.Profile);
            }
        }

        protected void EnabledDefaultProviders(TraceEventSession session, TraceEventProviderOptions options = null)
        {
            // This provider is required to get ActivityID support on TraceEvents.
            session.EnableProvider(
                TplEtwProviderTraceEventParser.ProviderGuid,
                TraceEventLevel.Informational,
                (ulong)TplEtwProviderTraceEventParser.Keywords.TasksFlowActivityIds,
                options);

            session.EnableProvider(DotNetRuntimeProviderGuid, options: options);
            session.EnableProvider(XamlProviderGuid, options: options);
        }

        protected abstract void EnableProviders(TraceEventSession session);
        protected abstract PerformanceTestContext CreateContext(TraceEventDispatcher source);
        protected abstract void OnIterationEnded(PerformanceTestContext context);

        private static TestResult[] ValidateElevated(ITestMethod testMethod)
        {
            bool? isElevated = TraceEventSession.IsElevated();
            if (!isElevated.HasValue || isElevated.Value == false)
            {
                var result = new TestResult()
                {
                    DisplayName = testMethod.TestMethodName,
                    Outcome = UnitTestOutcome.NotRunnable,
                    LogError = "Performance tests must be run from an evelvated (Administrator) environment."
                };
                return new TestResult[] { result };
            }

            return null;
        }

        private static string CreateLogFolder(string logFolderRoot, string testMethodName)
        {
            if (!Path.IsPathRooted(logFolderRoot))
            {
                logFolderRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), logFolderRoot));
            }

            string folder = Path.Combine(logFolderRoot, testMethodName, DateTime.Now.ToString("yyyy_MM_dd_HH_mm"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static Exception GetMethodSignatureException(ITestMethod testMethod)
        {
            ParameterInfo[] parameters = testMethod.ParameterTypes;
            if (parameters.Length != 1)
            {
                return new TargetParameterCountException(
                    $"Expected a single parameter of type {ContextType}, but found {parameters.Length} parameters.");
            }

            Type parameterType = parameters[0].ParameterType;
            if (!ContextType.IsAssignableFrom(parameterType))
            {
                return new ArgumentException(
                    $"Expected a single parameter of type {ContextType}, but found a parameter of type {parameterType}.",
                    parameters[0].Name);
            }

            return null;
        }
    }
}
