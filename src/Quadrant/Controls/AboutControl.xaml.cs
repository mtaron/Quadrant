using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Quadrant.Telemetry;
using Quadrant.Utility;
using Windows.ApplicationModel;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Quadrant.Controls
{
    public sealed partial class AboutControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public AboutControl()
            => InitializeComponent();

        public string AppVersion
        {
            get
            {
                PackageVersion version = Package.Current.Id.Version;
                return AppUtilities.GetString("Version", version.Major, version.Minor, version.Build, version.Revision);
            }
        }

        public bool IsTelemetryEnabled
        {
            get => AppTelemetry.Current.IsEnabled;

            set
            {
                AppTelemetry.Current.IsEnabled = value;
                OnPropertyChanged();
            }
        }

        private async void RateButtonClickAsync(object sender, RoutedEventArgs e)
        {
            var uri = new Uri($"ms-windows-store://review/?ProductId={App.StoreId}");
            await Launcher.LaunchUriAsync(uri).AsTask().ConfigureAwait(false);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
