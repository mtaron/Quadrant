using System;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Quadrant.Controls
{
    /// <summary>
    /// Represents a specialized CommandBar that adaptively collapses the label of AppBarButtons
    /// and toggles tooltips based on the presence of the label.
    /// </summary>
    public class AdapativeCommandBar : CommandBar
    {
        public AdapativeCommandBar()
            => RegisterPropertyChangedCallback(DefaultLabelPositionProperty, OnDefaultLabelPositionChanged);

        public double MinCommandWidth { get; set; }

        public double MinDefaultLabelRightWidth { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            double availableWidth = availableSize.Width;
            var content = (FrameworkElement)Content;
            content.MaxWidth = Math.Max(availableWidth - MinCommandWidth, 0);

            Size returnValue = base.MeasureOverride(availableSize);
            if (availableWidth - content.DesiredSize.Width >= MinDefaultLabelRightWidth)
            {
                DefaultLabelPosition = CommandBarDefaultLabelPosition.Right;
            }
            else
            {
                DefaultLabelPosition = CommandBarDefaultLabelPosition.Collapsed;
            }

            return returnValue;
        }

        private void OnDefaultLabelPositionChanged(DependencyObject sender, DependencyProperty property)
        {
            bool areToolTipsVisible = DefaultLabelPosition == CommandBarDefaultLabelPosition.Collapsed;
            foreach (DependencyObject command in PrimaryCommands.Cast<DependencyObject>())
            {
                string label = GetLabel(command);
                if (areToolTipsVisible)
                {
                    ToolTipService.SetToolTip(command, label);
                }
                else
                {
                    ToolTipService.SetToolTip(command, null);
                }
            }
        }

        private static string GetLabel(DependencyObject command)
        {
            if (command is AppBarButton appBarButton)
            {
                return appBarButton.Label;
            }
            else if (command is AppBarToggleButton appBarToggleButton)
            {
                return appBarToggleButton.Label;
            }

            return null;
        }
    }
}
