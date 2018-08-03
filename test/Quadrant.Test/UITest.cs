using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.System;
using FluentAssertions;
using System.Diagnostics;

namespace Quadrant.Test
{
    public class UITest
    {
        public const string QuadrantPackageFamilyName = "30267MichaelTaron.Quadrant_gwd693w8am0m6";

        private const string CommandKey = "Command";
        private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
        private static readonly Uri QuadrantProtocol = new Uri("quadrant-app:");
        private static readonly LauncherOptions LauncherOptions = new LauncherOptions()
        {
            TargetApplicationPackageFamilyName = QuadrantPackageFamilyName
        };

        public int AddFunction(string function)
        {
            var valueSet = new ValueSet
            {
                [CommandKey] = "AddFunction",
                ["Function"] = function
            };
            ValueSet results = ExecuteCommand(valueSet);
            return (int)results["Id"];
        }

        public IReadOnlyList<int> RemoveFunction(int id)
        {
            var valueSet = new ValueSet
            {
                [CommandKey] = "RemoveFunction",
                ["Id"] = id
            };
            ValueSet results = ExecuteCommand(valueSet);
            return results["RemovedFunctions"] as IReadOnlyList<int>;
        }

        public void ToggleAngleType()
        {
            var valueSet = new ValueSet
            {
                [CommandKey] = "ToggleAngleType"
            };
            ExecuteCommand(valueSet);
        }

        private static ValueSet ExecuteCommand(ValueSet valueSet)
        {
            Task<LaunchUriResult> task = Launcher.LaunchUriForResultsAsync(QuadrantProtocol, LauncherOptions, valueSet).AsTask();
            if (!task.Wait(CommandTimeout) && !Debugger.IsAttached)
            {
                throw new TimeoutException($"Timeout waiting for Quadrant protocol handler after {CommandTimeout.Seconds} seconds.");
            }

            LaunchUriResult result = task.Result;
            result.Status.Should().Be(LaunchUriStatus.Success);
            return result.Result;
        }
    }
}
