using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Quadrant.Functions;
using Quadrant.Telemetry;
using Quadrant.Utility;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Markup;

namespace Quadrant.Controls
{
    public sealed partial class DeleteConfirmationDialog : ContentDialog
    {
        private const string SpanXaml = "<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' >{0}</Span>";

        private DeleteConfirmationDialog(string function, IReadOnlyCollection<string> dependentFunctions)
        {
            InitializeComponent();

            string message;
            if (dependentFunctions.Count > 1)
            {
                message = string.Format(CultureInfo.CurrentUICulture, AppUtilities.GetString("DeleteMessage"), function);
            }
            else
            {
                message = AppUtilities.GetString("DeleteSingleMessage", function, dependentFunctions.First());
            }

            Span span = (Span)XamlReader.Load(string.Format(CultureInfo.CurrentUICulture, SpanXaml, message));
            MessageBlock.Inlines.Add(span);

            if (dependentFunctions.Count > 1)
            {
                MessageBlock.Inlines.Add(new LineBreak());

                Run run = new Run()
                {
                    Text = "    " + string.Join("\r\n    ", dependentFunctions)
                };

                Bold functionList = new Bold();
                functionList.Inlines.Add(run);
                MessageBlock.Inlines.Add(functionList);
            }
        }

        public static async Task<IReadOnlyList<int>> DeleteFunctionAsync(FunctionData function, FunctionManager functionManager, bool showPrompt = true)
        {
            AppTelemetry.Current.TrackEvent(
                TelemetryEvents.DeleteFunction,
                TelemetryProperties.Function,
                function.Name);

            var dependentFunctionNames = new HashSet<string>(functionManager.GetDependentFunctions(function));
            dependentFunctionNames.Remove(function.Name);

            if (!dependentFunctionNames.Any())
            {
                functionManager.DeleteFunction(function);
                return new int[] { function.Id };
            }

            if (showPrompt)
            {
                var dialog = new DeleteConfirmationDialog(function.Name, dependentFunctionNames);
                ContentDialogResult result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    return new int[0];
                }
            }

            FunctionData[] functionsToRemove = functionManager.Functions.Where(f => dependentFunctionNames.Contains(f.Name)).ToArray();
            foreach (FunctionData dependentFunction in functionsToRemove)
            {
                functionManager.DeleteFunction(dependentFunction);
            }

            functionManager.DeleteFunction(function);

            return functionsToRemove.Select(f => f.Id).Append(function.Id).ToList();
        }
    }
}
