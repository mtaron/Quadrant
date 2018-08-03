using System;
using Microsoft.VisualStudio.TestPlatform.TestExecutor;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Quadrant.Test
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.App_Suspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            UnitTestClient.CreateDefaultUI();

            // Ensure the current window is active
            Window.Current.Activate();

            UnitTestClient.Run(e.Arguments);
        }

        private async void App_Suspending(object sender, SuspendingEventArgs e)
        {
            // Clear out any application state saved by the tests so that the next
            // run will be clean.
            await ApplicationData.Current.ClearAsync();
        }
    }
}
