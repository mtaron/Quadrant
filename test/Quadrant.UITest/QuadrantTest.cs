using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Windows.Foundation.Collections;
using Windows.System;

namespace Quadrant.UITest
{
    public class QuadrantTest
    {
        private const string CommandKey = "Command";
        private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
        private static readonly Uri QuadrantProtocol = new Uri("quadrant-app:");
        private static readonly LauncherOptions LauncherOptions = new LauncherOptions()
        {
            TargetApplicationPackageFamilyName = QuadrantTestContext.QuadrantPackageFamilyName
        };

        public void AddFunction(string function)
        {
            var valueSet = new ValueSet
            {
                [CommandKey] = "AddFunction",
                ["Function"] = function
            };
            ExecuteCommand(valueSet);
        }

        public void RemoveFunction(int id)
        {
            var valueSet = new ValueSet
            {
                [CommandKey] = "RemoveFunction",
                ["Id"] = id
            };
            ExecuteCommand(valueSet);
        }

        public void ToggleAngleType()
        {
            var valueSet = new ValueSet
            {
                [CommandKey] = "ToggleAngleType"
            };
            ExecuteCommand(valueSet);
        }

        private static void ExecuteCommand(ValueSet valueSet)
        {
            Task<bool> task = Launcher.LaunchUriAsync(QuadrantProtocol, LauncherOptions, valueSet).AsTask();
            task.Should().NotBeNull();
            if (!task.Wait(CommandTimeout) && !Debugger.IsAttached)
            {
                throw new TimeoutException($"Timeout waiting for Quadrant protocol handler after {CommandTimeout.Seconds} seconds.");
            }

            task.Result.Should().BeTrue(because: "protocol command should succeed");
        }
    }
}
