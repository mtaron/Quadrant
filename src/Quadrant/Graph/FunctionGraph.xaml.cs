using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Quadrant.Functions;
using Quadrant.Ink;
using Quadrant.Persistence;
using Quadrant.Telemetry;
using Quadrant.Utility;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Precision = MathNet.Numerics.Precision;

namespace Quadrant.Graph
{
    public sealed partial class FunctionGraph : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private const double MinCanvasSize = 20;

        private static readonly VirtualKey s_keyboardAdd = (VirtualKey)187;   // VK_OEM_PLUS
        private static readonly VirtualKey s_keyboardMinus = (VirtualKey)189; // VK_OEM_MINUS

        public static readonly DependencyProperty InkInputOnlyProperty = DependencyProperty.Register(
            "InkInputOnly",
            typeof(bool),
            typeof(FunctionGraph),
            new PropertyMetadata(false, InkInputOnlyChanged));

        private readonly object _syncObject = new object();
        private readonly CoreDispatcher _dispatcher;
        private readonly Transform _transform = new Transform();

        private double _scaleX = LogicalConstants.DefaultScale;
        private double _scaleY = LogicalConstants.DefaultScale;
        private float _desiredLongEdge = LogicalConstants.DefaultDesiredLongEdge;
        private Vector2 _origin = Vector2.Zero;

        private uint _keyDownMultiplier = 1;
        private bool _isShiftPressed;
        private CanvasRenderTarget _snapshot;
        private IFunction _selectedFunction;
        private IFunction _highlightedFunction;
        private Vector2 _evaulationPoint;
        private Color _backgroundColor;
        private Color _foregroundColor;

        private bool _isSnapshotValid;
        private bool _shouldCancelInertia;
        private bool _canRecenter;
        private IFunction[] _functions;
        private IntersectionService _intersectionService;
        private AccessibilitySettings _accessibilitySettings;
        private bool _isHighContrast;

        private CancellationTokenSource _invalidateTokenSource;
        private readonly object _invalidateTokenLock = new object();

        private readonly TransformedStrokes _rootStrokes = new TransformedStrokes();
        private TransformedStrokes _currentStrokes;
        private readonly InkSynchronizer _inkSynchronizer;
        private readonly InkInputProcessingConfiguration _inkInputProcessing;
        private IReadOnlyList<InkStroke> _pendingDry;
        private bool _isErasing;
        private Point _previousPoint;

        public FunctionGraph()
        {
            InitializeComponent();
            _backgroundColor = Background.SolidColor();
            _foregroundColor = Foreground.SolidColor();
            Canvas.ClearColor = _backgroundColor;

            ManipulationMode =
                ManipulationModes.Scale
                | ManipulationModes.TranslateX
                | ManipulationModes.TranslateY
                | ManipulationModes.TranslateInertia
                | ManipulationModes.ScaleInertia;

            _dispatcher = Window.Current.Dispatcher;
            _currentStrokes = _rootStrokes;

            InkPresenter inkPresenter = InkInput.InkPresenter;
            inkPresenter.UpdateDefaultDrawingAttributes(new InkDrawingAttributes()
            {
                Size = new Size(4, 4),
                Color = _foregroundColor
            });

            CoreInkIndependentInputSource coreInputSource = CoreInkIndependentInputSource.Create(inkPresenter);
            coreInputSource.PointerMoving += CoreInputSource_PointerMoving;
            coreInputSource.PointerPressing += CoreInputSource_PointerPressing;

            _inkInputProcessing = inkPresenter.InputProcessingConfiguration;
            inkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
            _inkSynchronizer = inkPresenter.ActivateCustomDrying();

            SizeChanged += (s, e) => ReduceFunctionDetail();
            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
        }

        public double ScaleX
        {
            get => _scaleX;
            set
            {
                if (_scaleX != value)
                {
                    _scaleX = value;
                    Invalidate();
                    if (!CanRecenter && _scaleX != LogicalConstants.DefaultScale)
                    {
                        CanRecenter = true;
                    }
                }
            }
        }

        public double ScaleY
        {
            get => _scaleY;
            set
            {
                if (_scaleY != value)
                {
                    _scaleY = value;
                    Invalidate();
                    if (!CanRecenter && _scaleY != LogicalConstants.DefaultScale)
                    {
                        CanRecenter = true;
                    }
                }
            }
        }

        public bool CanRecenter
        {
            get => _canRecenter;
            private set
            {
                if (_canRecenter != value)
                {
                    _canRecenter = value;
                    OnPropertyChanged();
                }
            }
        }

        public IFunction HighlightedFunction
        {
            get => _highlightedFunction;
            set
            {
                if (_highlightedFunction != value)
                {
                    _highlightedFunction = value;
                    Invalidate();
                }
            }
        }

        public bool InkInputOnly
        {
            get => (bool)GetValue(InkInputOnlyProperty);
            set => SetValue(InkInputOnlyProperty, value);
        }

        public IReadOnlyList<IFunction> Functions
        {
            get => _functions;
            set
            {
                SelectedFunction = null;
                _functions = value.Where(f => f.Function != null).ToArray();
                _intersectionService = new IntersectionService(_functions);
                Invalidate();
            }
        }

        internal InkCanvas InkLayer
        {
            get => InkInput;
        }

        private Vector2 Origin
        {
            get => _origin;
            set
            {
                float x = MathUtility.Clamp(value.X, LogicalConstants.MinimumOriginValue, LogicalConstants.MaximumOriginValue);
                float y = MathUtility.Clamp(value.Y, LogicalConstants.MinimumOriginValue, LogicalConstants.MaximumOriginValue);

                _origin = new Vector2(x, y);
                UpdateCanRecenter();
            }
        }

        private IFunction SelectedFunction
        {
            get => _selectedFunction;
            set
            {
                if (_selectedFunction != value)
                {
                    _selectedFunction = value;
                    if (value == null)
                    {
                        HideCoordinates();
                    }
                    else
                    {
                        ShowCoordinates();
                    }

                    OnPropertyChanged();
                }
            }
        }

        private CanvasRenderTarget Snapshot
        {
            get => _snapshot;
            set
            {
                if (_snapshot == value)
                {
                    return;
                }

                if (_snapshot != null)
                {
                    _snapshot.Dispose();
                }

                _snapshot = value;
            }
        }

        internal void DrawGraph(CanvasDrawingSession drawingSession, Size size, Color gridColor)
        {
            Vector2 scale = GetComputedScale(size);
            _transform.Update(size, Origin, scale);

            GridRenderer.DrawGrid(drawingSession, _transform, gridColor);

            IFunction localHighlight = HighlightedFunction;
            IFunction[] localFunctions = _functions;
            if (localFunctions != null && localFunctions.Length > 0)
            {
                CancellationToken token;
                lock (_invalidateTokenLock)
                {
                    _invalidateTokenSource = new CancellationTokenSource();
                    token = _invalidateTokenSource.Token;
                }

                Parallel.ForEach(localFunctions, (f) =>
                {
                    using (AppTelemetry.Current.TrackDuration(TelemetryMetrics.DrawFunction))
                    {
                        FunctionRenderer.DrawFunction(drawingSession, token, f, _transform, f == localHighlight);
                    }
                });
            }

            _currentStrokes.Render(drawingSession, _transform.DisplayTransform, _isHighContrast);
        }

        public void EnsureCentered(IFunction function)
        {
            Point xIntercept = new Point(0, function.Function(0));
            float y = (float)xIntercept.Y;
            if (float.IsNaN(y) || float.IsInfinity(y) || Math.Abs(y) > LogicalConstants.MaximumOriginValue)
            {
                return;
            }

            Origin = xIntercept.ToVector2();
            Invalidate();
        }

        internal void SetStrokes(TransformedStrokes strokes)
        {
            _currentStrokes = strokes ?? _rootStrokes;

            Invalidate();
        }

        internal void EnableErase()
        {
            _isErasing = true;
            _inkInputProcessing.Mode = InkInputProcessingMode.None;
        }

        internal void DisableErase()
        {
            _isErasing = false;
            _inkInputProcessing.Mode = InkInputProcessingMode.Inking;
        }

        internal void EraseAllInk()
        {
            _currentStrokes.Clear();
            Invalidate();
        }

        internal Task SerializeAsync(Serializer serializer)
        {
            serializer.Write(_desiredLongEdge);
            serializer.Write(_origin);
            serializer.Write(_scaleX);
            serializer.Write(_scaleY);

            return _rootStrokes.SerializeAsync(serializer);
        }

        internal async Task DeserializeAsync(Deserializer deserializer)
        {
            _desiredLongEdge = deserializer.ReadSingle();
            _origin = deserializer.ReadVector2();
            _scaleX = deserializer.ReadDouble();
            _scaleY = deserializer.ReadDouble();

            _currentStrokes.Clear();
            await _rootStrokes.DeserializeAsync(deserializer);

            UpdateCanRecenter();
            Invalidate();
        }

        internal Task SaveImageAsync(IRandomAccessStream stream)
        {
            Size size = Canvas.Size;
            using (var renderTarget = new CanvasRenderTarget(Canvas, size))
            {
                using (CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession())
                {
                    DrawGraph(drawingSession, size, _foregroundColor);
                }

                return renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png).AsTask();
            }
        }

        internal void Recenter(RecenterType type)
        {
            AppTelemetry.Current.TrackEvent(TelemetryEvents.Recenter, TelemetryProperties.RecenterType, type.ToString());
            Origin = Vector2.Zero;
            _shouldCancelInertia = true;
            _desiredLongEdge = LogicalConstants.DefaultDesiredLongEdge;
            ScaleX = LogicalConstants.DefaultScale;
            ScaleY = LogicalConstants.DefaultScale;
            UpdateCanRecenter();
            Invalidate();
            Focus(FocusState.Programmatic);
            OnPropertyChanged(propertyName: null);
        }

        internal void RecenterWithButton()
            => Recenter(RecenterType.Button);

        protected override void OnManipulationInertiaStarting(ManipulationInertiaStartingRoutedEventArgs e)
        {
            e.TranslationBehavior.DesiredDeceleration = DisplayConstants.InterialDeceleration;
            e.ExpansionBehavior.DesiredDeceleration = DisplayConstants.InterialDeceleration;
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (SelectedFunction != null)
            {
                return;
            }

            // Allow the left stick to do navigation as expected
            // instead of mapping to Left/Right/Up/Down.
            switch (e.OriginalKey)
            {
                case VirtualKey.GamepadLeftThumbstickLeft:
                case VirtualKey.GamepadLeftThumbstickRight:
                case VirtualKey.GamepadLeftThumbstickUp:
                case VirtualKey.GamepadLeftThumbstickDown:
                    return;
            }

            VirtualKey key = e.Key;
            float scaleDelta = 0;
            bool isKeyboardScale = false;
            if (_isShiftPressed)
            {
                if (key == s_keyboardAdd)
                {
                    scaleDelta = DisplayConstants.KeyboardScaleFactor;
                    isKeyboardScale = true;
                }
                else if (key == s_keyboardMinus)
                {
                    scaleDelta = -DisplayConstants.KeyboardScaleFactor;
                    isKeyboardScale = true;
                }
            }

            if (isKeyboardScale)
            {
                e.Handled = true;
            }

            Vector2 translationDelta = Vector2.Zero;
            const float scalar = 4;
            switch (key)
            {
                case VirtualKey.Shift:
                    _isShiftPressed = true;
                    break;
                case VirtualKey.Left:
                case VirtualKey.GamepadRightThumbstickLeft:
                    translationDelta = -scalar * Vector2.UnitX;
                    e.Handled = true;
                    break;
                case VirtualKey.Right:
                case VirtualKey.GamepadRightThumbstickRight:
                    translationDelta = scalar * Vector2.UnitX;
                    e.Handled = true;
                    break;
                case VirtualKey.Up:
                case VirtualKey.GamepadRightThumbstickUp:
                    translationDelta = -scalar * Vector2.UnitY;
                    e.Handled = true;
                    break;
                case VirtualKey.Down:
                case VirtualKey.GamepadRightThumbstickDown:
                    translationDelta = scalar * Vector2.UnitY;
                    e.Handled = true;
                    break;
                case VirtualKey.Add:
                case VirtualKey.GamepadRightTrigger:
                    scaleDelta = DisplayConstants.KeyboardScaleFactor;
                    e.Handled = true;
                    break;
                case VirtualKey.Subtract:
                case VirtualKey.GamepadLeftTrigger:
                    scaleDelta = -DisplayConstants.KeyboardScaleFactor;
                    e.Handled = true;
                    break;
            }

            if (translationDelta != Vector2.Zero)
            {
                float multiplier = _keyDownMultiplier++;
                Origin += _transform.GetLogicalNormal(multiplier * translationDelta);
                Invalidate();
            }
            else if (scaleDelta != 0)
            {
                ScaleX += ScaleX / scaleDelta;
                ScaleY += ScaleY / scaleDelta;
            }
        }

        protected override void OnKeyUp(KeyRoutedEventArgs e)
        {
            _keyDownMultiplier = 1;
            if (e.Key == VirtualKey.Shift)
            {
                _isShiftPressed = false;
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (InkInputOnly)
            {
                return;
            }

            Focus(FocusState.Pointer);
            CapturePointer(e.Pointer);
            _shouldCancelInertia = false;
            if (_functions == null || _functions.Length == 0)
            {
                return;
            }

            PointerPoint pointerPoint = e.GetCurrentPoint(Canvas);
            Vector2 point = pointerPoint.Position.ToVector2();
            Rect contactRect = new Rect(pointerPoint.Position, pointerPoint.Position);
            contactRect = contactRect.Inflate(DisplayConstants.ContactRectSize);

            Vector2 logicalPoint = _transform.GetLogicalVector(point);
            Rect logicalRect = contactRect.Transform(_transform.LogicalTransform);
            IFunction nearestFunction = _intersectionService.GetNearestFunction(
                logicalPoint,
                logicalRect.Left,
                logicalRect.Right,
                out Vector2 nearestPoint);
            if (nearestFunction != null)
            {
                if (logicalRect.Contains(nearestPoint.ToPoint()))
                {
                    _evaulationPoint = nearestPoint;
                    SelectedFunction = nearestFunction;
                    AppTelemetry.Current.TrackEvent(TelemetryEvents.EvaluatePoint);
                }
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            if (InkInputOnly)
            {
                return;
            }

            ReleasePointerCapture(e.Pointer);
            SelectedFunction = null;

            // Don't let the ScrollView at the root have a chance to take focus.
            e.Handled = true;
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            if (InkInputOnly)
            {
                return;
            }

            e.Handled = true;
            if (e.IsInertial && _shouldCancelInertia)
            {
                return;
            }

            Vector2 translationDelta = e.Delta.Translation.ToVector2();
            float scaleDelta = e.Delta.Scale - 1;

            if (!Precision.AlmostEqual(scaleDelta, 0.0))
            {
                ScaleAroundPoint(scaleDelta, e.Position);
                SelectedFunction = null;
                return;
            }

            Vector2 logicalTranslation = _transform.GetLogicalNormal(translationDelta);
            float length = logicalTranslation.Length();
            if (Precision.AlmostEqual(length, 0.0))
            {
                return;
            }

            if (SelectedFunction != null)
            {
                Vector2 translatedEvaluationPoint = _evaulationPoint + logicalTranslation;
                double lowerBound = translatedEvaluationPoint.X - length;
                double upperBound = translatedEvaluationPoint.X + length;
                Vector2? nearestPoint = SelectedFunction.GetNearestPoint(
                    translatedEvaluationPoint,
                    lowerBound,
                    upperBound);
                if (nearestPoint.HasValue)
                {
                    _evaulationPoint = nearestPoint.Value;
                    UpdateCoordinates(Coordinates);
                }

                return;
            }

            if (!_shouldCancelInertia)
            {
                Origin -= logicalTranslation;
            }

            Invalidate();
        }

        protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
        {
            if (InkInputOnly)
            {
                return;
            }

            Recenter(RecenterType.DoubleClick);
            e.Handled = true;
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            if (SelectedFunction != null)
            {
                return;
            }

            e.Handled = true;

            PointerPoint position = e.GetCurrentPoint(Canvas);
            float mouseDelta = DisplayConstants.MouseWheelScaleFactor * position.Properties.MouseWheelDelta;
            if (Precision.AlmostEqual(mouseDelta, 0))
            {
                return;
            }

            ScaleAroundPoint(mouseDelta, position.Position);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
            => new FunctionGraphAutomationPeer(this);

        private void ScaleAroundPoint(float delta, Point point)
        {
            delta = MathUtility.Clamp(delta, -DisplayConstants.MaxScaleDelta, DisplayConstants.MaxScaleDelta);
            float longEdgeBefore = _desiredLongEdge;
            if (ScaleGraph(delta))
            {
                float scale = _desiredLongEdge / longEdgeBefore;
                var scaleTransform = Matrix3x2.CreateScale(scale, point.ToVector2());
                Vector2 renderOrigin = _transform.GetDisplayVector(Origin);
                renderOrigin = Vector2.Transform(renderOrigin, scaleTransform);
                Origin = _transform.GetLogicalVector(renderOrigin);
            }
        }

        internal void Invalidate()
        {
            Canvas.Invalidate();
            ReduceFunctionDetail();
        }

        private void ReduceFunctionDetail()
        {
            lock (_invalidateTokenLock)
            {
                _invalidateTokenSource?.Cancel();
            }
        }

        private bool ScaleGraph(float delta)
        {
            float change = (delta * _desiredLongEdge);
            float newDesiredLongEdge = _desiredLongEdge - change;

            newDesiredLongEdge = MathUtility.Clamp(newDesiredLongEdge, LogicalConstants.MinimumZoom, LogicalConstants.MaximumZoom);
            if (newDesiredLongEdge != _desiredLongEdge)
            {
                _desiredLongEdge = newDesiredLongEdge;
                UpdateCanRecenter();
                Invalidate();
                return true;
            }

            return false;
        }

        private void DrawGraph(ICanvasAnimatedControl sender, CanvasDrawingSession drawingSession)
            => DrawGraph(drawingSession, sender.Size, _foregroundColor);

        private void EnsureSnapshot(ICanvasAnimatedControl sender)
        {
            if (Snapshot == null || Snapshot.Size != sender.Size)
            {
                Snapshot = new CanvasRenderTarget(sender, sender.Size);
                _isSnapshotValid = false;
            }

            if (!_isSnapshotValid)
            {
                using (CanvasDrawingSession drawingSession = Snapshot.CreateDrawingSession())
                {
                    drawingSession.Clear(_backgroundColor);
                    DrawGraph(sender, drawingSession);
                }

                _isSnapshotValid = true;
            }
        }

        private void Canvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            double width = sender.Size.Width;
            double height = sender.Size.Height;
            QuadrantEventSource.Log.DrawGraphStart(width, height);
            if (width >= MinCanvasSize && height >= MinCanvasSize)
            {
                using (AppTelemetry.Current.TrackDuration(TelemetryMetrics.DrawGraph))
                using (CanvasDrawingSession drawingSession = args.DrawingSession)
                {
                    if (_pendingDry != null)
                    {
                        EnsureSnapshot(sender);
                        drawingSession.DrawImage(Snapshot);
                        DrawWetInk(sender, drawingSession);
                    }
                    else
                    {
                        _isSnapshotValid = false;
                        DrawGraph(sender, drawingSession);
                    }
                }

                if (!sender.Paused)
                {
                    // If the graph is not paused, then this is the first draw.
                    sender.Paused = true;
                    AppTelemetry.Current.TrackLoadStop();
                }
            }

            QuadrantEventSource.Log.DrawGraphStop();
        }

        private void DrawWetInk(ICanvasAnimatedControl sender, CanvasDrawingSession drawingSession)
        {
            drawingSession.DrawInk(_pendingDry, _isHighContrast);
            if (!sender.Paused)
            {
                _isSnapshotValid = false;
                sender.Invalidate();
            }
            else
            {
                _pendingDry = null;
                RunOnUIThreadAsync(() => _inkSynchronizer.EndDry()).ContinueWith(
                    t => QuadrantEventSource.Log.DryInkStop());
            }
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            Debug.Assert(_pendingDry == null);
            QuadrantEventSource.Log.DryInkStart();
            _pendingDry = _inkSynchronizer.BeginDry();
            _currentStrokes.AddRange(_pendingDry, _transform.LogicalTransform);
            Canvas.Paused = false;
        }

        private void HideCoordinates()
        {
            _shouldCancelInertia = true;
            if (Coordinates != null)
            {
                Coordinates.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowCoordinates()
        {
            CoordinateControl control = GetCoordinates();
            control.Background = new SolidColorBrush(SelectedFunction.Color);
            UpdateCoordinates(control);
            control.Visibility = Visibility.Visible;
        }

        private void UpdateCoordinates(CoordinateControl control)
        {
            Vector2? evaluationPoint = GetEvaluationPoint(SelectedFunction, _evaulationPoint.X);
            if (evaluationPoint == null)
            {
                return;
            }

            Vector2 logicalPoint = evaluationPoint.Value;
            Vector2 displayPoint = _transform.GetDisplayVector(logicalPoint);
            control.SetCoordinates(displayPoint, logicalPoint);
        }

        private CoordinateControl GetCoordinates()
        {
            CoordinateControl control = Coordinates;
            if (control != null)
            {
                return control;
            }

            return (CoordinateControl)FindName(nameof(Coordinates));
        }

        private Vector2? GetEvaluationPoint(IFunction function, double x)
        {
            double snapX = _intersectionService.GetSnapValue(function, x, _transform.SnappingTolerance);
            double y = function.Function(snapX);
            if (_transform.IsOutsideVericalRange(y))
            {
                if (snapX == x)
                {
                    return null;
                }

                snapX = x;
                y = function.Function(snapX);
                if (_transform.IsOutsideVericalRange(y))
                {
                    return null;
                }
            }

            return new Vector2((float)snapX, (float)y);
        }

        private Vector2 GetComputedScale(Size canvasSize)
        {
            Vector2 sizeVector = canvasSize.ToVector2();
            float xScale;
            float yScale;

            if (sizeVector.X > sizeVector.Y)
            {
                xScale = sizeVector.Y / _desiredLongEdge;
                yScale = xScale;
            }
            else
            {
                yScale = sizeVector.X / _desiredLongEdge;
                xScale = yScale;
            }

            xScale *= (float)ScaleX;
            yScale *= -1f * (float)ScaleY;

            return new Vector2(xScale, yScale);
        }

        private void UpdateCanRecenter()
        {
            CanRecenter = Origin != Vector2.Zero
                || _desiredLongEdge != LogicalConstants.DefaultDesiredLongEdge
                || ScaleX != LogicalConstants.DefaultScale
                || ScaleY != LogicalConstants.DefaultScale;
        }

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (_accessibilitySettings == null)
            {
                _accessibilitySettings = new AccessibilitySettings();
                _isHighContrast = _accessibilitySettings.HighContrast;
                _accessibilitySettings.HighContrastChanged += OnHighContrastChanged;
            }
        }

        private static void InkInputOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            FunctionGraph graph = (FunctionGraph)d;
            bool newValue = (bool)e.NewValue;
            graph.InkInput.IsHitTestVisible = newValue;
            CoreInputDeviceTypes deviceTypss = CoreInputDeviceTypes.Pen;
            if (newValue)
            {
                deviceTypss |= CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Touch;
            }

            graph.InkInput.InkPresenter.InputDeviceTypes = deviceTypss;
        }

        private async void CoreInputSource_PointerPressing(CoreInkIndependentInputSource sender, PointerEventArgs args)
        {
            _previousPoint = args.CurrentPoint.Position;
            await EraseAsync(args).ConfigureAwait(false);
        }

        private async void CoreInputSource_PointerMoving(CoreInkIndependentInputSource sender, PointerEventArgs args)
        {
            await EraseAsync(args).ConfigureAwait(false);
        }

        private async Task EraseAsync(PointerEventArgs args)
        {
            PointerPoint pointerPoint = args.CurrentPoint;
            bool isEraserDevice = pointerPoint.Properties.IsEraser;
            if (!_isErasing && !isEraserDevice)
            {
                _inkInputProcessing.Mode = InkInputProcessingMode.Inking;
            }
            else
            {
                if (isEraserDevice)
                {
                    _inkInputProcessing.Mode = InkInputProcessingMode.None;
                }

                args.Handled = true;
                Point currentPoint = pointerPoint.Position;

                bool isErased = await _currentStrokes.EraseBetweenAsync(_dispatcher, _previousPoint, currentPoint).ConfigureAwait(false);
                if (isErased)
                {
                    Invalidate();
                }

                _previousPoint = currentPoint;
            }
        }

        private void OnHighContrastChanged(AccessibilitySettings sender, object args)
        {
            _isHighContrast = sender.HighContrast;
            Invalidate();
        }

        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty property)
        {
            _backgroundColor = Background.SolidColor();
            Canvas.ClearColor = _backgroundColor;
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty property)
        {
            Color oldForeground = _foregroundColor;
            _foregroundColor = Foreground.SolidColor();

            InkPresenter inkPresenter = InkInput.InkPresenter;
            InkDrawingAttributes attributes = inkPresenter.CopyDefaultDrawingAttributes();
            if (attributes.Color == oldForeground)
            {
                attributes.Color = _foregroundColor;
                inkPresenter.UpdateDefaultDrawingAttributes(attributes);
            }

            Invalidate();
        }

        private void Canvas_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_accessibilitySettings != null)
            {
                _accessibilitySettings.HighContrastChanged -= OnHighContrastChanged;
                _accessibilitySettings = null;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_dispatcher.HasThreadAccess)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                using var source = new CancellationTokenSource();
                _ = RunOnUIThreadAsync(() =>
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                    }).TrackExceptions(source.Token);
            }
        }

        private Task RunOnUIThreadAsync(Action action)
            => _dispatcher.RunAsync(CoreDispatcherPriority.Low, () => action()).AsTask();
    }
}
