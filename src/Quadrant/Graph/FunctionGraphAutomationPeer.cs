using System.Numerics;
using Quadrant.Utility;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;

namespace Quadrant.Graph
{
    public partial class FunctionGraph
    {
        private sealed class FunctionGraphAutomationPeer : FrameworkElementAutomationPeer, ITransformProvider2
        {
            private readonly FunctionGraph _graph;

            public FunctionGraphAutomationPeer(FunctionGraph owner)
                : base(owner)
            {
                _graph = owner;
            }

            public bool CanMove
            {
                get => true;
            }

            public bool CanResize
            {
                get => false;
            }

            public bool CanRotate
            {
                get => false;
            }

            public bool CanZoom
            {
                get => true;
            }

            /// <summary>
            /// Gets the maximum zoom level, as a percentage.
            /// </summary>
            public double MaxZoom
            {
                get => 100;
            }

            /// <summary>
            /// Gets the minimum zoom leve, as a percentage.
            /// </summary>
            public double MinZoom
            {
                get => 0;
            }

            /// <summary>
            /// Gets the current zoom level as a percentage.
            /// </summary>
            public double ZoomLevel
            {
                get => _graph._desiredLongEdge / (LogicalConstants.MaximumZoom - LogicalConstants.MinimumZoom);
            }

            public void Move(double x, double y)
            {
                if (x.IsReal() && y.IsReal())
                {
                    _graph.Origin = new Vector2((float)x, (float)y);
                    _graph.Invalidate();
                }
            }

            public void Resize(double width, double height)
            {
                // Not supported.
            }

            public void Rotate(double degrees)
            {
                // Not supported.
            }

            /// <summary>
            /// Zooms the graph.
            /// </summary>
            /// <param name="zoom">The amount to zoom the graph, specified as a percentage.</param>
            public void Zoom(double zoom)
            {
                if (zoom >= -100 && zoom <= 100)
                {
                    _graph.ScaleGraph((float)zoom);
                }
            }

            public void ZoomByUnit(ZoomUnit zoomUnit)
            {
                float delta;
                switch (zoomUnit)
                {
                    case ZoomUnit.LargeDecrement:
                        delta = -100;
                        break;
                    case ZoomUnit.LargeIncrement:
                        delta = 100;
                        break;
                    case ZoomUnit.SmallDecrement:
                        delta = -20;
                        break;
                    case ZoomUnit.SmallIncrement:
                        delta = 20;
                        break;
                    case ZoomUnit.NoAmount:
                    default:
                        return;
                }

                _graph.ScaleGraph(delta);
            }

            protected override string GetClassNameCore()
                => nameof(FunctionGraph);

            protected override AutomationControlType GetAutomationControlTypeCore()
                => AutomationControlType.Custom;

            protected override string GetLocalizedControlTypeCore()
                =>AppUtilities.GetString("GraphControlTypeName");

            protected override object GetPatternCore(PatternInterface patternInterface)
            {
                if (patternInterface == PatternInterface.Transform
                    || patternInterface == PatternInterface.Transform2)
                {
                    return this;
                }

                return base.GetPatternCore(patternInterface);
            }
        }
    }
}
