using System;
using System.Globalization;
using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Quadrant.Graph
{
    public sealed partial class CoordinateControl : UserControl
    {
        private readonly Visual _visual;

        public CoordinateControl()
        {
            InitializeComponent();

            _visual = ElementCompositionPreview.GetElementVisual(this);
            ElementCompositionPreview.SetIsTranslationEnabled(this, true);
            _visual.AnchorPoint = new Vector2(0, 0.5f);

            InitializeDropShadow();
            InitializeAnimations();

            RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        }

        public void SetCoordinates(Vector2 displayLocation, Vector2 logicalLocation)
        {
            if (TextGrid.ActualWidth > TextGrid.MinWidth)
            {
                TextGrid.MinWidth = TextGrid.ActualWidth;
            }

            CoordTextBlock.Text = GetDisplayCoordinates(logicalLocation);

            const float halfCircle = 5;
            _visual.Offset = new Vector3(displayLocation.X - halfCircle, displayLocation.Y, 0);
        }

        private void InitializeAnimations()
        {
            Visual circleVisual = ElementCompositionPreview.GetElementVisual(CirlceHost);
            float radius = (float)CirlceHost.Width / 2.0f;
            circleVisual.CenterPoint = new Vector3(radius, radius, 0);
            Compositor compositor = circleVisual.Compositor;

            var duration = TimeSpan.FromMilliseconds(360);
            var easingFunction = compositor.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0.3f), new Vector2(0, 1));

            // Show cirlce animations
            var circleShowAnimation = compositor.CreateVector2KeyFrameAnimation();
            circleShowAnimation.InsertKeyFrame(1, Vector2.One);
            circleShowAnimation.InsertKeyFrame(0, Vector2.Zero, easingFunction);
            circleShowAnimation.Duration = duration;
            circleShowAnimation.Target = "Scale.xy";
            ElementCompositionPreview.SetImplicitShowAnimation(CirlceHost, circleShowAnimation);

            // Show text animation
            var textShowOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            textShowOpacityAnimation.InsertKeyFrame(0, 0);
            textShowOpacityAnimation.InsertKeyFrame(1, 1, easingFunction);
            textShowOpacityAnimation.Duration = duration;
            textShowOpacityAnimation.Target = "Opacity";
            ElementCompositionPreview.SetImplicitShowAnimation(TextGrid, textShowOpacityAnimation);

            var textShowOffsetAnimation = compositor.CreateScalarKeyFrameAnimation();
            textShowOffsetAnimation.InsertKeyFrame(0, -15);
            textShowOffsetAnimation.InsertKeyFrame(1, 0, easingFunction);
            textShowOffsetAnimation.Duration = duration;
            textShowOffsetAnimation.Target = "Offset.x";
            ElementCompositionPreview.SetImplicitShowAnimation(TranslationElement, textShowOffsetAnimation);
        }

        private void InitializeDropShadow()
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(ShadowSource);
            Compositor compositor = hostVisual.Compositor;

            DropShadow dropShadow = compositor.CreateDropShadow();
            dropShadow.Color = Color.FromArgb(210, 0, 0, 0);
            dropShadow.BlurRadius = 5.0f;
            dropShadow.Offset = new Vector3(0, 1f, 0);
            dropShadow.Mask = ShadowSource.GetAlphaMask();

            SpriteVisual shadowVisual = compositor.CreateSpriteVisual();
            shadowVisual.Shadow = dropShadow;

            ElementCompositionPreview.SetElementChildVisual(ShadowSource, shadowVisual);

            ExpressionAnimation bindSizeAnimation = compositor.CreateExpressionAnimation("hostVisual.Size");
            bindSizeAnimation.SetReferenceParameter("hostVisual", hostVisual);

            shadowVisual.StartAnimation("Size", bindSizeAnimation);
        }

        private void OnVisibilityChanged(DependencyObject sender, DependencyProperty property)
        {
            if (Visibility == Visibility.Visible)
            {
                const double defaultMinWidth = 40;
                TextGrid.MinWidth = defaultMinWidth;
            }
        }

        private static string GetDisplayCoordinates(Vector2 point)
        {
            string coordinateSeperator;
            string xString = Math.Round(point.X, 2).ToString(CultureInfo.CurrentUICulture);
            string yString = Math.Round(point.Y, 2).ToString(CultureInfo.CurrentUICulture);

            // If the decimal seperator is a comma, use a pipe.
            string decimalSeperator = CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator;
            if (decimalSeperator?.Length == 1 && decimalSeperator[0] == ',')
            {
                coordinateSeperator = "|";
            }
            else
            {
                coordinateSeperator = ", ";
            }

            return $"{xString}{coordinateSeperator}{yString}";
        }
    }
}
