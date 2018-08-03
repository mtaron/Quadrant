using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Printing;
using Microsoft.Graphics.Canvas.Text;
using Quadrant.Functions;
using Quadrant.Telemetry;
using Quadrant.Utility;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.Graphics.Printing.OptionDetails;
using Windows.UI;

namespace Quadrant.Graph
{
    internal sealed class GraphPrinter : IDisposable
    {
        private static readonly CanvasTextFormat LabelFormat = new CanvasTextFormat()
        {
            FontSize = 20,
            WordWrapping = CanvasWordWrapping.NoWrap,
            LineSpacing = 30,
            LineSpacingBaseline = 15
        };

        private static readonly CanvasTypography SubScriptTypography;

        private readonly FunctionGraph _graph;
        private readonly CanvasPrintDocument _printDocument;
        private readonly string _title;
        private readonly string _graphSizeOption;
        private readonly string _fullPageItem;
        private readonly string _windowSizeItem;
        private readonly string _labelLocationOption;
        private readonly string _noneItem;
        private readonly string _topLeftItem;
        private readonly string _topRightItem;
        private readonly string _bottomLeftItem;
        private readonly string _bottomRightItem;

        private Vector2 _pageSize;

        static GraphPrinter()
        {
            SubScriptTypography = new CanvasTypography();
            SubScriptTypography.AddFeature(CanvasTypographyFeatureName.Subscript, 1u);
        }

        private GraphPrinter(FunctionGraph graph)
        {
            _graph = graph;

            // Resources cannot be loaded from the print thread, so do it here.
            _title = AppUtilities.GetString("PrintTaskTitle");
            _graphSizeOption = AppUtilities.GetString(nameof(GraphSize));
            _fullPageItem = AppUtilities.GetString(nameof(GraphSize.FullPage));
            _windowSizeItem = AppUtilities.GetString(nameof(GraphSize.Window));
            _labelLocationOption = AppUtilities.GetString(nameof(LabelLocation));
            _noneItem = AppUtilities.GetString(nameof(LabelLocation.None));
            _topLeftItem = AppUtilities.GetString(nameof(LabelLocation.TopLeft));
            _topRightItem = AppUtilities.GetString(nameof(LabelLocation.TopRight));
            _bottomLeftItem = AppUtilities.GetString(nameof(LabelLocation.BottomLeft));
            _bottomRightItem = AppUtilities.GetString(nameof(LabelLocation.BottomRight));

            _printDocument = new CanvasPrintDocument();
            _printDocument.Preview += PrintDocument_Preview;
            _printDocument.Print += PrintDocument_Print;
            _printDocument.PrintTaskOptionsChanged += PrintDocument_PrintTaskOptionsChanged;
        }

        public static Task<bool> PrintAsync(FunctionGraph graph)
        {
            var printer = new GraphPrinter(graph);

            // Show the print UI, with the print manager connected to us.
            PrintManager printManager = PrintManager.GetForCurrentView();
            printManager.PrintTaskRequested += printer.PrintTaskRequested;

            return PrintManager.ShowPrintUIAsync().AsTask();
        }

        public void Dispose()
            => _printDocument.Dispose();

        private void PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            sender.PrintTaskRequested -= PrintTaskRequested;

            PrintTask printTask = args.Request.CreatePrintTask(_title, (createPrintTaskArgs) =>
            {
                createPrintTaskArgs.SetSource(_printDocument);
            });

            PrintTaskOptionDetails printDetailedOptions = PrintTaskOptionDetails.GetFromPrintTaskOptions(printTask.Options);
            CreateGraphOptions(printDetailedOptions);

            printDetailedOptions.OptionChanged += PrintDetailedOptions_OptionChanged;

            printTask.Completed += (s, e) =>
            {
                AppTelemetry.Current.TrackEvent(
                    TelemetryEvents.Print,
                    TelemetryProperties.PrintResult,
                    e.Completion.ToString());
            };
        }

        private void CreateGraphOptions(PrintTaskOptionDetails printTaskOptions)
        {
            PrintCustomItemListOptionDetails graphSizeOption = printTaskOptions.CreateItemListOption(nameof(GraphSize), _graphSizeOption);
            graphSizeOption.AddItem(nameof(GraphSize.FullPage), _fullPageItem);
            graphSizeOption.AddItem(nameof(GraphSize.Window), _windowSizeItem);
            printTaskOptions.DisplayedOptions.Add(nameof(GraphSize));

            PrintCustomItemListOptionDetails labelLocationOption = printTaskOptions.CreateItemListOption(nameof(LabelLocation), _labelLocationOption);
            labelLocationOption.AddItem(nameof(LabelLocation.TopLeft), _topLeftItem);
            labelLocationOption.AddItem(nameof(LabelLocation.TopRight), _topRightItem);
            labelLocationOption.AddItem(nameof(LabelLocation.BottomLeft), _bottomLeftItem);
            labelLocationOption.AddItem(nameof(LabelLocation.BottomRight), _bottomRightItem);
            labelLocationOption.AddItem(nameof(LabelLocation.None), _noneItem);
            printTaskOptions.DisplayedOptions.Add(nameof(LabelLocation));
        }

        private void PrintDetailedOptions_OptionChanged(PrintTaskOptionDetails sender, PrintTaskOptionChangedEventArgs args)
        {
            if (ShouldInvalidatePreview(args.OptionId))
            {
                _printDocument.InvalidatePreview();
            }
        }

        private static bool ShouldInvalidatePreview(object changeOptionId)
        {
            if (changeOptionId == null)
            {
                // We are likely switching printers.
                return true;
            }

            string optionString = changeOptionId as string;
            if (optionString == nameof(GraphSize) || optionString == nameof(LabelLocation))
            {
                return true;
            }

            return false;
        }

        private void PrintDocument_PrintTaskOptionsChanged(CanvasPrintDocument sender, CanvasPrintTaskOptionsChangedEventArgs args)
        {
            PrintPageDescription pageDesc = args.PrintTaskOptions.GetPageDescription(1);
            Vector2 newPageSize = pageDesc.PageSize.ToVector2();

            if (_pageSize == newPageSize)
            {
                // We've already figured out the pages and the page size hasn't changed,
                // so there's nothing left for us to do here.
                return;
            }

            _pageSize = newPageSize;
            sender.InvalidatePreview();

            sender.SetPageCount(1);
            args.NewPreviewPageNumber = 1;
        }

        private void PrintDocument_Preview(CanvasPrintDocument sender, CanvasPreviewEventArgs args)
        {
            PrintTaskOptions options = args.PrintTaskOptions;
            PrintTaskOptionDetails printDetailedOptions = PrintTaskOptionDetails.GetFromPrintTaskOptions(options);
            Rect imageableRect = GetImageableRect(options, args.PageNumber);
            GraphSize size = GetOptionValue<GraphSize>(printDetailedOptions);
            LabelLocation labelLocation = GetOptionValue<LabelLocation>(printDetailedOptions);
            DrawPage(args.DrawingSession, imageableRect, size, labelLocation);
        }

        private void PrintDocument_Print(CanvasPrintDocument sender, CanvasPrintEventArgs args)
        {
            PrintTaskOptions options = args.PrintTaskOptions;
            PrintTaskOptionDetails printDetailedOptions = PrintTaskOptionDetails.GetFromPrintTaskOptions(options);
            Rect imageableRect = GetImageableRect(options, pageNumber: 1);
            GraphSize size = GetOptionValue<GraphSize>(printDetailedOptions);
            LabelLocation labelLocation = GetOptionValue<LabelLocation>(printDetailedOptions);

            using (CanvasDrawingSession drawingSession = args.CreateDrawingSession())
            {
                DrawPage(drawingSession, imageableRect, size, labelLocation);
            }
        }

        private void DrawPage(CanvasDrawingSession drawingSession, Rect imageableRect, GraphSize graphSize, LabelLocation labelLocation)
        {
            drawingSession.Transform = GetPrintTransfrom(imageableRect, graphSize, out Size size);

            _graph.DrawGraph(drawingSession, size, Colors.Black);

            IReadOnlyList<IFunction> functions = _graph.Functions;
            if (functions == null || labelLocation == LabelLocation.None)
            {
                return;
            }

            // Draw function labels.
            string functionString = GetFunctionString(functions, out FuntionLocation[] locations);
            using (var layout = new CanvasTextLayout(drawingSession, functionString, LabelFormat, 100, 20))
            {
                foreach (FuntionLocation location in locations)
                {
                    int start = location.Start;
                    layout.SetColor(start, location.Length, location.Color);
                    int parenLocation = functionString.IndexOf('(', start);

                    // ++ for the f
                    layout.SetTypography(++start, parenLocation - start, SubScriptTypography);
                }

                Vector2 labelPoint = GetLabelLocation(labelLocation, layout.LayoutBounds, size);
                drawingSession.DrawTextLayout(layout, labelPoint, Colors.Black);
            }
        }

        private Matrix3x2 GetPrintTransfrom(Rect imageableRect, GraphSize graphSize, out Size size)
        {
            var translateVector = new Vector2((float)imageableRect.X, (float)imageableRect.Y);
            Matrix3x2 transform = Matrix3x2.CreateTranslation(translateVector);
            if (graphSize == GraphSize.FullPage)
            {
                size = new Size(imageableRect.Width, imageableRect.Height);
            }
            else if (graphSize == GraphSize.Window)
            {
                size = _graph.RenderSize;
                double ratio = Math.Min(imageableRect.Width / size.Width, imageableRect.Height / size.Height);
                if (ratio < 1)
                {
                    transform *= Matrix3x2.CreateScale((float)ratio);
                }
            }
            else
            {
                Debug.Fail($"Unexpected GraphSize {graphSize}.");
            }

            return transform;
        }

        private static Vector2 GetLabelLocation(LabelLocation labelLocation, Rect textLayoutBounds, Size canvasSize)
        {
            const float offset = 15;
            switch (labelLocation)
            {
                case LabelLocation.TopLeft:
                    return new Vector2(offset, offset);
                case LabelLocation.TopRight:
                    return new Vector2((float)canvasSize.Width - (float)textLayoutBounds.Width - offset, offset);
                case LabelLocation.BottomLeft:
                    return new Vector2(offset, (float)canvasSize.Height - (float)textLayoutBounds.Height - offset);
                case LabelLocation.BottomRight:
                    return new Vector2(
                        (float)canvasSize.Width - (float)textLayoutBounds.Width - offset,
                        (float)canvasSize.Height - (float)textLayoutBounds.Height - offset);
                default:
                    throw new InvalidOperationException($"Unexpected LabelLocation {labelLocation}.");
            }
        }

        private static Rect GetImageableRect(PrintTaskOptions options, uint pageNumber)
            => options.GetPageDescription(pageNumber).ImageableRect;

        private static T GetOptionValue<T>(PrintTaskOptionDetails printDetailedOptions) where T : struct
        {
            Type type = typeof(T);
            string value = (string)printDetailedOptions.Options[type.Name].Value;
            return (T)Enum.Parse(type, value);
        }

        private static string GetFunctionString(IReadOnlyList<IFunction> functions, out FuntionLocation[] locations)
        {
            locations = new FuntionLocation[functions.Count];

            StringBuilder stringBuilder = new StringBuilder();
            for (int functionIndex = 0; functionIndex < functions.Count; functionIndex++)
            {
                IFunction function = functions[functionIndex];
                string functionString = function.ToString();
                locations[functionIndex] = new FuntionLocation(stringBuilder.Length, functionString.Length, function.Color);
                stringBuilder.AppendLine(functionString);
            }

            return stringBuilder.ToString();
        }

        private enum GraphSize
        {
            FullPage,
            Window
        }

        private enum LabelLocation
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            None
        }

        private readonly struct FuntionLocation
        {
            public FuntionLocation(int start, int length, Color color)
            {
                Start = start;
                Length = length;
                Color = color;
            }

            public int Start { get; }
            public int Length { get; }
            public Color Color { get; }
        }
    }
}
