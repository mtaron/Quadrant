using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Quadrant.Functions;
using Quadrant.Telemetry;
using Windows.UI.Xaml.Controls;

namespace Quadrant.Controls
{
    public sealed partial class ColorControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler ColorChanged;

        private FunctionData _function;

        public ColorControl()
            => InitializeComponent();

        public FunctionData Function
        {
            get => _function;
            set
            {
                if (_function != value)
                {
                    _function = value;
                    OnPropertyChanged();

                    if (_function != null)
                    {
                        AppTelemetry.Current.TrackEvent(TelemetryEvents.ChangeColor, TelemetryProperties.Function, _function.Name);
                    }
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
