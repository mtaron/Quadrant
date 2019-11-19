using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Quadrant.Functions;
using Quadrant.Ink;
using Quadrant.Telemetry;
using Quadrant.Utility;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

namespace Quadrant.Controls
{
    public sealed partial class EditCommandBar : AdapativeCommandBar, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event TypedEventHandler<EditCommandBar, FunctionDataEventArgs> EditComplete;
        public event TypedEventHandler<FrameworkElement, FunctionDataEventArgs> ColorEdited;

        private readonly CoreDispatcher _dispatcher;

        private FunctionData _function;
        private string _beforeEditExpression;
        private int _caretIndex;
        private int _postCompletionCaretIndex;
        private string _selectionChosenText;
        private int _replacementStart;
        private int _replacementLength;
        private FlyoutBase _flyout;
        private double _flyoutWidth;
        private bool _isSuggestionChange;
        private bool _isSuggestionListClosing;
        private IEnumerable<string> _independentFunctions;
        private string _errorMessage;
        private readonly object _fitLock = new object();
        private StrokeFit[] _strokeFits;
        private int _fitIndex;
        private ConnectedAnimation _entranceAnimation;
        private TextBox _internalTextBox;

        public EditCommandBar()
        {
            InitializeComponent();
            _dispatcher = Window.Current.Dispatcher;
            FunctionInput.RegisterPropertyChangedCallback(AutoSuggestBox.IsSuggestionListOpenProperty, IsSuggestionListOpenChanged);
            SizeChanged += OnSizeChanged;
        }

        public FunctionManager FunctionManager { get; set; }

        public double FlyoutWidth
        {
            get => _flyoutWidth;
            set
            {
                if (_flyoutWidth != value)
                {
                    _flyoutWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;

            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public FunctionData Function
        {
            get => _function;
            set
            {
                if (_function != value)
                {
                    if (value != null)
                    {
                        _function = value;
                        BeginEdit();
                    }
                    else
                    {
                        EditComplete?.Invoke(this, new FunctionDataEventArgs(_function));
                        _function = null;
                    }

                    OnPropertyChanged();
                }
            }
        }

        private void BeginEdit()
        {
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            if (Function.Strokes != null)
            {
                Function.Strokes.StrokesChanged += StrokesChangedAsync;
            }

            _independentFunctions = FunctionManager.GetIndependentFunctions(Function);
            _isSuggestionListClosing = false;
            _isSuggestionChange = false;
            _selectionChosenText = null;
            _beforeEditExpression = Function.Expression ?? string.Empty;
            UpdateRetryButtonVisibility(shouldShowRetryButton: false);

            FunctionInput.Text = _beforeEditExpression;
            if (string.IsNullOrEmpty(FunctionInput.Text))
            {
                AcceptButton.IsEnabled = false;
            }

            CommandEnterStoryboard.Begin();

            _entranceAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation(GraphView.BeginEditAnimationName);
            if (_entranceAnimation != null)
            {
                _entranceAnimation.Configuration = new DirectConnectedAnimationConfiguration();
                InputEnterStoryboard.Begin();
                _entranceAnimation.TryStart(FunctionLabel);
            }

            IAsyncAction action = _dispatcher.RunIdleAsync((b) =>
            {
                FunctionInput.Focus(FocusState.Programmatic);
            });
        }

        private async void StrokesChangedAsync(object sender, EventArgs e)
        {
            var strokes = (TransformedStrokes)sender;
            string expression = null;
            IEnumerable<StrokeFit> fits = await InkToFunction.GetFunctionAsync(strokes).ConfigureAwait(false);
            lock (_fitLock)
            {
                _strokeFits = fits.Where(f => f.Error.IsReal()).Take(3).ToArray();
                _fitIndex = 0;
                if (_strokeFits.Length == 0)
                {
                    expression = string.Empty;
                }
                else
                {
                    expression = _strokeFits[_fitIndex].GetExpression();
                }
            }

            AppTelemetry.Current.TrackEvent(TelemetryEvents.InkToFunction, TelemetryProperties.Function, Function.Name);

            await RunOnUIThreadAsync(() =>
            {
                bool shouldEnableInkRetry = !string.IsNullOrEmpty(expression);
                if (shouldEnableInkRetry)
                {
                    FunctionInput.Text = expression;
                }

                UpdateRetryButtonVisibility(shouldEnableInkRetry);
            }).ConfigureAwait(false);
        }

        private void FinishEdit()
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;

            if (Function?.Strokes != null)
            {
                Function.Strokes.StrokesChanged -= StrokesChangedAsync;
                Function.Strokes.Clear();
            }

            if (_entranceAnimation != null)
            {
                _entranceAnimation.Cancel();
                _entranceAnimation = null;
            }

            Function = null;
        }

        private void Update(string expression)
        {
            if (Function != null)
            {
                Function.Expression = expression;
                AcceptButton.IsEnabled = FunctionManager.Compile();
            }
        }

        private bool Validate(string expression = null)
        {
            Function.Expression = expression ?? FunctionInput.Text;
            bool isValid = FunctionManager.Compile();
            if (!isValid)
            {
                ShowErrorMessage();
            }
            else
            {
                ErrorMessage = null;
            }

            return isValid;
        }

        private void ShowErrorMessage()
        {
            if (!FunctionManager.Errors.Any())
            {
                return;
            }

            ErrorMessage = string.Join("\r\n", FunctionManager.Errors.Select(e => e.Text));
            _flyout = FlyoutBase.GetAttachedFlyout(FunctionInput);
            FlyoutWidth = ActualWidth;
            _flyout.ShowAt(FunctionInput);
        }

        private void CancelEdit()
        {
            FunctionInput.Text = _beforeEditExpression;
            bool isDelete = string.IsNullOrEmpty(_beforeEditExpression);

            if (isDelete)
            {
                FunctionManager.DeleteFunction(Function);
            }
            else
            {
                Function.Expression = _beforeEditExpression;
                FunctionManager.Compile();
            }

            FinishEdit();
            AppTelemetry.Current.TrackEvent(TelemetryEvents.CancelEdit);
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (_isSuggestionListClosing)
            {
                _isSuggestionListClosing = false;
            }
            else if (Function != null)
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.Escape:
                    case VirtualKey.GamepadB:
                        CancelEdit();
                        args.Handled = true;
                        break;
                    case VirtualKey.GamepadA:
                        Accept();
                        args.Handled = true;
                        break;
                }
            }
        }

        private void IsSuggestionListOpenChanged(DependencyObject sender, DependencyProperty property)
            => _isSuggestionListClosing = !(bool)sender.GetValue(property);

        private async void ClickDeleteButtonAsync(object sender, RoutedEventArgs e)
        {
            await FunctionManager.DeleteFunctionAsync(Function);
            FinishEdit();
        }

        private void Accept()
        {
            if (Validate())
            {
                FinishEdit();
            }
        }

        private IEnumerable<string> GetMatchingFunctions(string query)
        {
            return _independentFunctions.Where(f => f.IndexOf(query, StringComparison.OrdinalIgnoreCase) > -1).
                OrderByDescending(f => f.StartsWith(query, StringComparison.OrdinalIgnoreCase));
        }

        private void FunctionInput_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            string text = sender.Text;
            Update(text);

            if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            {
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                sender.ItemsSource = null;
                return;
            }

            text.GetLetterExtent(_caretIndex, out _replacementStart, out _replacementLength);
            if (_replacementLength == 0)
            {
                sender.ItemsSource = null;
                return;
            }

            string queryString = text.Substring(_replacementStart, _replacementLength);
            if (string.Equals("x", queryString, StringComparison.OrdinalIgnoreCase))
            {
                // Don't suggest anything for x.
                sender.ItemsSource = null;
                return;
            }

            sender.ItemsSource = GetMatchingFunctions(queryString);
        }

        private void FunctionInput_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                string text = sender.Text;
                bool isNextCharacterOpenParan = _replacementStart + _replacementLength < text.Length
                    && text[_replacementStart + _replacementLength] == '(';
                _selectionChosenText = text.Remove(_replacementStart, _replacementLength);
                string replacement = (string)args.ChosenSuggestion;
                _postCompletionCaretIndex = _replacementStart + replacement.Length;
                if (!isNextCharacterOpenParan)
                {
                    replacement += "()";
                    _postCompletionCaretIndex++;
                }

                _selectionChosenText = _selectionChosenText.Insert(_replacementStart, replacement);
                _isSuggestionChange = true;
                sender.Text = _selectionChosenText;
                sender.ItemsSource = null;
                FunctionInput.Focus(FocusState.Programmatic);
            }
            else if (Validate(args.QueryText))
            {
                FinishEdit();
            }
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
            => _internalTextBox = (TextBox)sender;

        private void UpdateRetryButtonVisibility(bool shouldShowRetryButton)
        {
            if (_internalTextBox == null)
            {
                return;
            }

            VisualStateManager.GoToState(
                _internalTextBox,
                shouldShowRetryButton ? "RetryButtonVisible" : "RetryButtonCollapsed",
                useTransitions: true);
        }

        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (_isSuggestionChange)
            {
                _isSuggestionChange = false;
                _caretIndex = _postCompletionCaretIndex;
                textBox.SelectionStart = _postCompletionCaretIndex;
            }
            else
            {
                _caretIndex = Math.Max(textBox.SelectionStart - 1, 0);
            }
        }

        private void ClickPopup(object sender, RoutedEventArgs e)
        {
            DismissPopup();
        }

        private void KeyDownPopup(object sender, KeyRoutedEventArgs e)
            => DismissPopup();

        private void DismissPopup()
        {
            _flyout.Hide();
            FunctionInput.Focus(FocusState.Programmatic);
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled && Function != null)
            {
                CancelEdit();
                e.Handled = true;
            }
        }

        private void GetNextStrokeFit(object sender, RoutedEventArgs e)
        {
            string expression = GetNextFit();
            if (!string.IsNullOrEmpty(expression))
            {
                FunctionInput.Text = expression;
            }

            AppTelemetry.Current.TrackEvent(TelemetryEvents.RetryInkFit, TelemetryProperties.Function, Function.Name);
        }

        private string GetNextFit()
        {
            lock (_fitLock)
            {
                if (_strokeFits == null || _strokeFits.Length == 0)
                {
                    return null;
                }

                if (_fitIndex >= _strokeFits.Length - 1)
                {
                    _fitIndex = 0;
                }
                else
                {
                    _fitIndex++;
                }

                return _strokeFits[_fitIndex].GetExpression();
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
            => ColorEdited?.Invoke(FunctionLabel, new FunctionDataEventArgs(Function));

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width;
            if (width < 500)
            {
                CancelButton.Visibility = Visibility.Collapsed;

                if (width < 400)
                {
                    AcceptButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AcceptButton.Visibility = Visibility.Visible;
                }
            }
            else
            {
                CancelButton.Visibility = Visibility.Visible;
                AcceptButton.Visibility = Visibility.Visible;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private Task RunOnUIThreadAsync(Action action)
            => _dispatcher.RunAsync(CoreDispatcherPriority.Low, () => action()).AsTask();
    }
}
