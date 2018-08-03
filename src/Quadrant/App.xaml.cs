using System;
using System.Threading.Tasks;
using Quadrant.Telemetry;
using Quadrant.Utility;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Quadrant
{
    public sealed partial class App : Application
    {
        public static string StoreId = "9nblggh5k7hw";
        public static string AppUserModelId = "30267MichaelTaron.Quadrant_gwd693w8am0m6!App";

        private bool? _prelauchStarted;
        private bool _prelauchFinished;

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code executed.
        /// </summary>
        public App()
        {
            QuadrantEventSource.Log.AppConstructed();
            AppTelemetry.Current.TrackLoadStart();
            InitializeTheme();
            InitializeComponent();
            Current.RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
            Suspending += (o, e) => QuadrantEventSource.Log.AppSuspended();
            EnteredBackground += OnEnteredBackgroundAsync;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            GraphView graphView = InitializeWindow(args);
            if (args is ProtocolActivatedEventArgs protocolArgs && protocolArgs.Data != null)
            {
                graphView.ProtocolHandler.Handle(protocolArgs.Data);
            }
            else if (args is ProtocolForResultsActivatedEventArgs forResultsProtocolArgs)
            {
                ValueSet results = graphView.ProtocolHandler.Handle(forResultsProtocolArgs.Data);
                forResultsProtocolArgs.ProtocolForResultsOperation.ReportCompleted(results);
            }
            else if (args.PreviousExecutionState == ApplicationExecutionState.Running
                && args is IApplicationViewActivatedEventArgs viewActivatedArgs)
            {
                ShowNewWindowAsync(viewActivatedArgs).ContinueWith(result =>
                {
                    AppTelemetry.Current.TrackEvent(
                        TelemetryEvents.NewWindow,
                        TelemetryProperties.IsSuccess,
                        result.Result);
                });
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.
        /// </summary>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (HandlePrelaucn(args))
            {
                return;
            }

            if (args.PreviousExecutionState != ApplicationExecutionState.Running)
            {
                InitializeWindow(args);
            }
            else
            {
                ShowNewWindowAsync(args).ContinueWith(result =>
                {
                    AppTelemetry.Current.TrackEvent(
                        TelemetryEvents.NewWindow,
                        TelemetryProperties.IsSuccess,
                        result.Result);
                });
            }
        }

        private bool HandlePrelaucn(IPrelaunchActivatedEventArgs args)
        {
            if (!_prelauchStarted.HasValue)
            {
                CoreApplication.EnablePrelaunch(true);
                _prelauchStarted = args.PrelaunchActivated;

                if (args.PrelaunchActivated)
                {
                    QuadrantEventSource.Log.PrelaunchStart();
                }

                return false;
            }

            if (_prelauchStarted.Value && !_prelauchFinished)
            {
                _prelauchFinished = true;
                QuadrantEventSource.Log.PrelaunchStop();
                return true;
            }

            return false;
        }

        private static async Task<bool> ShowNewWindowAsync(IApplicationViewActivatedEventArgs args)
        {
            CoreApplicationView newView = CoreApplication.CreateNewView();
            int newViewId = await newView.Dispatcher.AwaitableRunAsync(() =>
            {
                InitializeWindow(args);
                ApplicationView view = ApplicationView.GetForCurrentView();
                view.Consolidated += View_Consolidated;
                QuadrantEventSource.Log.ViewCreated(view.Id);
                return view.Id;
            });

            return await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId,
                ViewSizePreference.Default,
                args.CurrentlyShownApplicationViewId,
                ViewSizePreference.Default);
        }

        private static void View_Consolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            QuadrantEventSource.Log.ViewClosed(sender.Id);
            sender.Consolidated -= View_Consolidated;
            Window.Current.Close();
        }

        private void InitializeTheme()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(nameof(RequestedTheme), out object requestedTheme)
                && Enum.TryParse(requestedTheme as string, out ApplicationTheme theme))
            {
                RequestedTheme = theme;
            }
        }

        private static GraphView InitializeWindow(IActivatedEventArgs args)
        {
            var graphView = Window.Current.Content as GraphView;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active.
            if (graphView == null)
            {
                graphView = new GraphView();
                string resourceFlowDirection = ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];
                if (resourceFlowDirection == "LTR")
                {
                    graphView.FlowDirection = FlowDirection.LeftToRight;
                }
                else
                {
                    graphView.FlowDirection = FlowDirection.RightToLeft;
                }

                Window.Current.Content = graphView;

                if (TryGetResumeSessionId(args, out string sessionId))
                {
                    QuadrantEventSource.Log.ResumeStart();
                    graphView.ResumeAsync(sessionId).ContinueWith(
                        _ => QuadrantEventSource.Log.ResumeStop());
                }
            }

            Window.Current.Activate();
            return graphView;
        }

        private static bool TryGetResumeSessionId(IActivatedEventArgs args, out string sessionId)
        {
            sessionId = null;
            if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
            {
                return true;
            }

            if (args.Kind != ActivationKind.Protocol)
            {
                return false;
            }

            var protocolArgs = (ProtocolActivatedEventArgs)args;
            if (!protocolArgs.Uri.LocalPath.Equals("resume", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            AppTelemetry.Current.TrackEvent(TelemetryEvents.TimelineResume);
            sessionId = protocolArgs.Uri.Query.TrimStart('?');
            return true;
        }

        private async void OnEnteredBackgroundAsync(object sender, EnteredBackgroundEventArgs e)
        {
            QuadrantEventSource.Log.SuspendStart();

            using (e.GetDeferral())
            {
                AppTelemetry.Current.Flush();

                if (Window.Current.Content is GraphView graphView)
                {
                    await graphView.SuspendAsync().ConfigureAwait(false);
                }
            }

            QuadrantEventSource.Log.SuspendStop();
        }
    }
}
