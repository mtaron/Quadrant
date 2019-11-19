using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Quadrant.Controls;
using Quadrant.Functions;
using Quadrant.Graph;
using Quadrant.Persistence;
using Quadrant.Protocol;
using Quadrant.Telemetry;
using Quadrant.Utility;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Quadrant
{
    public sealed partial class GraphView : UserControl, INotifyPropertyChanged, IProtocol
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public const string BeginEditAnimationName = "BeginEditAnimation";
        private static readonly Uri FeedbackUri = new Uri($"feedback-hub:?tabid=2&appid={App.AppUserModelId}");

        private readonly SuspensionManager _suspensionManager = new SuspensionManager();

        private bool _isCtrlPressed;
        private DataTransferManager _dataTransferManager;

        private Visual _panelVisual;
        private CompositionAnimationGroup _functionBarAnimation;

        public GraphView()
        {
            InitializeTitleBar();
            InitializeComponent();

            FunctionManager.Invalidated += (s, e) => UpdateFunctions();
            SizeChanged += (s, e) => UpdateInkToolsMargin();
            ProtocolHandler = new ProtocolHandler(this);
        }

        public FunctionManager FunctionManager { get; } = new FunctionManager();

        public string CurrentTheme
        {
            get
            {
                if (ActualTheme == ElementTheme.Dark)
                {
                    return AppUtilities.GetString("LightTheme");
                }
                else
                {
                    return AppUtilities.GetString("DarkTheme");
                }
            }
        }

        internal ProtocolHandler ProtocolHandler { get; }

        internal async Task SuspendAsync()
        {
            await _suspensionManager.SuspendAsync(
                  serializer =>
                  {
                      FunctionManager.Serialize(serializer);
                      return Graph.SerializeAsync(serializer);
                  }).ConfigureAwait(false);

            string description = string.Join('\n', FunctionManager.Functions.Select(f => f.ToString()));
            await _suspensionManager.UpdateUserSessionAsync(description).ConfigureAwait(false);
            await _suspensionManager.DeleteOldFilesAsync().ConfigureAwait(false);
        }

        internal Task ResumeAsync(string sessionId)
            => _suspensionManager.ResumeAsync(
                sessionId,
                deserializer =>
                {
                    FunctionManager.Deserialize(deserializer);
                    return Graph.DeserializeAsync(deserializer);
                });

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled)
            {
                return;
            }

            switch (e.Key)
            {
                case VirtualKey.Control:
                    _isCtrlPressed = true;
                    break;
                case VirtualKey.P:
                    if (_isCtrlPressed)
                    {
                        e.Handled = true;
                        var action = GraphPrinter.PrintAsync(Graph);
                    }
                    break;
                case VirtualKey.N:
                    if (_isCtrlPressed)
                    {
                        e.Handled = true;
                        NewFunction();
                    }
                    break;
                case VirtualKey.F11:
                    {
                        ApplicationView view = ApplicationView.GetForCurrentView();
                        if (view.IsFullScreenMode)
                        {
                            view.ExitFullScreenMode();
                            e.Handled = true;
                        }
                        else
                        {
                            e.Handled = view.TryEnterFullScreenMode();
                        }
                    }
                    break;
#if PERF_TEST
                case VirtualKey.F5:
                    PerfTest();
                    break;
#endif
            }
        }

        protected override void OnKeyUp(KeyRoutedEventArgs e)
        {
            base.OnKeyUp(e);

            if (e.Key == VirtualKey.Control)
            {
                _isCtrlPressed = false;
            }
        }

        private void InitializeTitleBar()
        {
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            if (ActualTheme == ElementTheme.Dark)
            {
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonInactiveForegroundColor = Colors.White;
            }
        }

        private void NewFunction()
            => OnFunctionEdited(FunctionManager.CreateFunction());

        private void OnFunctionEdited(FunctionData function)
        {
            Graph.SetStrokes(function.Strokes);
            if (InkTools != null)
            {
                HideInkTools();
            }

            VisualStateManager.GoToState(this, "EditFunction", useTransitions: true);
            EditBar.Function = function;
        }

        private void OnEditCompleted()
        {
            UpdateFunctions();
            Graph.SetStrokes(null);

            VisualStateManager.GoToState(this, "Normal", useTransitions: true);
            BeginFunctionBarAnimation();

            if (InkButton.IsChecked == true)
            {
                ShowInkTools();
            }
        }

        private void BeginFunctionBarAnimation()
        {
            Visual panelVisual = GetPrimaryCommandsVisual();
            if (panelVisual == null)
            {
                return;
            }

            EnsureFunctionBarAninmation(panelVisual.Compositor);
            panelVisual.StartAnimationGroup(_functionBarAnimation);
        }

        private Visual GetPrimaryCommandsVisual()
        {
            if (_panelVisual != null)
            {
                return _panelVisual;
            }

            if (!(FunctionBar.PrimaryCommands.FirstOrDefault() is FrameworkElement command))
            {
                return null;
            }

            if (!(VisualTreeHelper.GetParent(command) is Panel panel))
            {
                return null;
            }

            _panelVisual = ElementCompositionPreview.GetElementVisual(panel);
            return _panelVisual;
        }

        private void EnsureFunctionBarAninmation(Compositor compositor)
        {
            if (_functionBarAnimation != null)
            {
                return;
            }

            ConnectedAnimationService animationService = ConnectedAnimationService.GetForCurrentView();

            _functionBarAnimation = compositor.CreateAnimationGroup();
            
            ScalarKeyFrameAnimation offsetAnimation = compositor.CreateScalarKeyFrameAnimation();
            offsetAnimation.Target = "Offset.x";
            offsetAnimation.InsertKeyFrame(0, -5.0f);
            offsetAnimation.InsertKeyFrame(1, 0, animationService.DefaultEasingFunction);
            offsetAnimation.Duration = animationService.DefaultDuration;

            ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Target = "Opacity";
            opacityAnimation.InsertKeyFrame(0, 0);
            opacityAnimation.InsertKeyFrame(1, 1, animationService.DefaultEasingFunction);
            opacityAnimation.Duration = animationService.DefaultDuration;

            _functionBarAnimation.Add(offsetAnimation);
            _functionBarAnimation.Add(opacityAnimation);
        }

        private void UpdateFunctions()
            => Graph.Functions = FunctionManager.Functions;

        private async void PrintButton_ClickAsync(object sender, RoutedEventArgs e)
            => await GraphPrinter.PrintAsync(Graph).ConfigureAwait(false);

        private void SwitchThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActualTheme == ElementTheme.Dark)
            {
                RequestedTheme = ElementTheme.Light;
                AppTelemetry.Current.TrackEvent(TelemetryEvents.LightThemeButton);
            }
            else
            {
                RequestedTheme = ElementTheme.Dark;
                AppTelemetry.Current.TrackEvent(TelemetryEvents.DarkThemeButton);
            }

            SetTitleBarButtonForeground(RequestedTheme);
            ApplicationData.Current.LocalSettings.Values[nameof(RequestedTheme)] = RequestedTheme.ToString();
            OnPropertyChanged(nameof(CurrentTheme));
        }

        private static void SetTitleBarButtonForeground(ElementTheme theme)
        {
            Color color;
            if (theme == ElementTheme.Dark)
            {
                color = Colors.White;
            }
            else
            {
                color = Colors.Black;
            }

            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonForegroundColor = color;
            titleBar.ButtonInactiveForegroundColor = color;
        }

        private async void FeedbackButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            AppTelemetry.Current.TrackEvent(TelemetryEvents.FeedbackButton);
            await Launcher.LaunchUriAsync(FeedbackUri).AsTask().ConfigureAwait(false);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null)
            {
                return;
            }

            FlyoutBase flyout = FlyoutBase.GetAttachedFlyout(element);
            if (flyout == null)
            {
                return;
            }

            if (About == null)
            {
                FindName(nameof(About));
            }

            flyout.ShowAt(this);
            AppTelemetry.Current.TrackEvent(TelemetryEvents.AboutButton);
        }

        private void FunctionList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is FunctionData function))
            {
                return;
            }

            // Don't animate the first item since it is already in its final position on the EditBar.
            if (function.Id != 1 && FunctionList.ContainerFromItem(function) is UIElement container)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(BeginEditAnimationName, container);
            }

            AppTelemetry.Current.TrackEvent(
                TelemetryEvents.EditFunction,
                TelemetryProperties.Function,
                function.Name);

            OnFunctionEdited(function);
        }

        private void EditBar_EditComplete(object sender, FunctionDataEventArgs e)
            => OnEditCompleted();

        private Flyout GetColorFlyout()
            => ColorFlyout ?? (Flyout)FindName(nameof(ColorFlyout));

        private void EditBar_ColorEdited(FrameworkElement sender, FunctionDataEventArgs args)
        {
            Flyout colorFlyout = GetColorFlyout();
            ColorControl.Function = args.Function;
            colorFlyout.ShowAt(sender);
        }

        private void FunctionLabel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!(sender is FunctionLabel functionLabel) || functionLabel.Function == null)
            {
                return;
            }

            Graph.HighlightedFunction = functionLabel.Function;
        }

        private void FunctionLabel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (FunctionList.SelectedItem == null)
            {
                Graph.HighlightedFunction = null;
            }
        }

        private void RecenterFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element))
            {
                return;
            }

            if (element.DataContext is FunctionData funtion)
            {
                Graph.EnsureCentered(funtion);
            }
        }

        private void ColorlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element))
            {
                return;
            }

            if (element.DataContext is FunctionData funtion)
            {
                Flyout colorFlyout = GetColorFlyout();

                ColorControl.Function = funtion;
                if (FunctionList.ContainerFromItem(funtion) is FrameworkElement container)
                {
                    colorFlyout.ShowAt(container);
                }
            }
        }

        private async void DeleteFlyoutItem_ClickAsync(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element))
            {
                return;
            }

            if (element.DataContext is FunctionData function)
            {
                await FunctionManager.DeleteFunctionAsync(function).ConfigureAwait(false);
            }
        }

        private void ColorControl_ColorChanged(object sender, EventArgs e)
            => Graph.Invalidate();

        private void OnScaleFlyoutClosed(object sender, object e)
            => AppTelemetry.Current.TrackEvent(
                TelemetryEvents.SettingsButton,
                TelemetryProperties.ScaleX, Graph.ScaleX.ToString(CultureInfo.InvariantCulture),
                TelemetryProperties.ScaleY, Graph.ScaleY.ToString(CultureInfo.InvariantCulture));

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
            AppTelemetry.Current.TrackEvent(TelemetryEvents.ShareButton);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _dataTransferManager = DataTransferManager.GetForCurrentView();
            _dataTransferManager.DataRequested += OnDataRequested;

            await _suspensionManager.InitializeUserSessionAsync().ConfigureAwait(false);
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataPackage package = args.Request.Data;

            package.Properties.ApplicationName = AppUtilities.GetString("AppName");
            package.Properties.Title = AppUtilities.GetString("ShareTitle");
            package.SetDataProvider(StandardDataFormats.Bitmap, ProvideData);
        }

        private async void ProvideData(DataProviderRequest request)
        {
            DataProviderDeferral deferral = request.GetDeferral();
            var stream = new InMemoryRandomAccessStream();
            await Graph.SaveImageAsync(stream);
            request.SetData(RandomAccessStreamReference.CreateFromStream(stream));
            deferral.Complete();
        }

        private void UpdateInkToolsMargin()
        {
            if (InkTools != null)
            {
                GeneralTransform transform = InkButton.TransformToVisual(this);
                Point center = transform.TransformPoint(new Point(InkButton.ActualWidth / 2.0, 0));

                double width;
                if (InkTools.ActualWidth <= 0)
                {
                    InkTools.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    width = InkTools.DesiredSize.Width;
                }
                else
                {
                    width = InkTools.ActualWidth;
                }

                InkTools.Margin = new Thickness(center.X - width / 2.0, 0, 0, 0);
            }
        }

        private void ShowInkTools()
        {
            bool isFirstShow = InkTools == null;
            VisualStateManager.GoToState(this, "InkToolsShown", useTransitions: true);
            UpdateInkToolsMargin();

            if (isFirstShow
                && ActualTheme == ElementTheme.Dark
                && InkTools.ActiveTool is InkToolbarPenButton pen)
            {
                pen.SelectedBrushIndex = 1;
            }

            AppTelemetry.Current.TrackEvent(TelemetryEvents.InkButton, TelemetryProperties.IsChecked, true);
        }

        private void HideInkTools()
        {
            StencilButton.IsChecked = false;
            VisualStateManager.GoToState(this, "InkToolsHidden", useTransitions: true);
            AppTelemetry.Current.TrackEvent(TelemetryEvents.InkButton, TelemetryProperties.IsChecked, false);
        }

        void IProtocol.ToggleAngleType()
            =>  FunctionManager.UseRadians = !FunctionManager.UseRadians;

        void IProtocol.SetTelemetryMode(bool isEnabled)
            => AppTelemetry.Current.IsEnabled = isEnabled;

        int IProtocol.AddFunction(string function, out IReadOnlyList<string> errors)
        {
            errors = null;

            FunctionData newFunction = FunctionManager.CreateFunction();
            newFunction.Expression = function;
            if (FunctionManager.Compile())
            {
                UpdateFunctions();
                return newFunction.Id;
            }

            errors = FunctionManager.Errors.Select(e => e.Text).ToList();
            return -1;
        }

        IReadOnlyList<int> IProtocol.RemoveFunction(int id)
        {
            FunctionData function = FunctionManager.Functions.SingleOrDefault(f => f.Id == id);
            if (function == null)
            {
                return new int[0];
            }

            Task<IReadOnlyList<int>> task = FunctionManager.DeleteFunctionAsync(function, showPrompt: false);
            task.Wait();
            return task.Result;
        }

        private async void FunctionBar_OpeningAsync(object sender, object e)
            => await Dispatcher.RunAsync(CoreDispatcherPriority.Low,
                () => PrintButton.Focus(FocusState.Programmatic)).AsTask().ConfigureAwait(false);

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

#if PERF_TEST
        private int _perfTestMultiple = 1;

        /// <summary>
        /// Test code that will be removed before shipping.
        /// </summary>
        private void PerfTest()
        {
            using (var supression = AppTelemetry.Current.Supress())
            {
                FunctionManager.Functions.Clear();

                int lowerBound = -10 * _perfTestMultiple;
                int upperBound = 10 * _perfTestMultiple;
                FunctionData[] functions = new FunctionData[upperBound - lowerBound];

                for (int i = lowerBound; i < upperBound; i++)
                {
                    FunctionData test = FunctionManager.CreateFunction();
                    test.Expression = $"sin(x + {i}) + {i}/2";
                    functions[i - lowerBound] = test;
                }

                FunctionManager.Compile();
                Graph.Functions = functions;
                _perfTestMultiple++;
            }
        }
#endif
    }
}
