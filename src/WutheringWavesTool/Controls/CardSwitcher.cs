using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Controls
{
    public sealed partial class CardSwitcher:Control
    {
        public object Card1
        {
            get { return (object)GetValue(Card1Property); }
            set { SetValue(Card1Property, value); }
        }

        public static readonly DependencyProperty Card1Property =
            DependencyProperty.Register(nameof(Card1), typeof(object), typeof(CardSwitcher), new PropertyMetadata(null));

        public object Card2
        {
            get { return (object)GetValue(Card2Property); }
            set { SetValue(Card2Property, value); }
        }

        public ContentControl? CardBack { get; private set; }
        public ContentControl? CardFront { get; private set; }

        protected override void OnApplyTemplate()
        {
            this.CardBack = GetTemplateChild("CardBack") as ContentControl;
            this.CardFront = GetTemplateChild("CardFront") as ContentControl;
            InitRenderTransform(CardBack, 1.0);
            InitRenderTransform(CardFront, 0.75);
            UpdateZIndex();
            base.OnApplyTemplate();
        }

        public static readonly DependencyProperty Card2Property =
            DependencyProperty.Register(nameof(Card2), typeof(object), typeof(CardSwitcher), new PropertyMetadata(null));
        private bool isFront;

        public void Switch()
        {
            ContentControl fadeOutCard = isFront ? CardFront : CardBack;
            ContentControl fadeInCard = isFront ? CardBack : CardFront;

            InitRenderTransform(fadeInCard, 0.75);
            fadeInCard.Opacity = 0.0;

            var fadeOutAnim = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            var scaleOutAnimX = new DoubleAnimation
            {
                To = 0.75,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut }
            };
            var scaleOutAnimY = new DoubleAnimation
            {
                To = 0.75,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut }
            };

            var fadeInAnim = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut }
            };
            var scaleInAnimX = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut }
            };
            var scaleInAnimY = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard sb = new Storyboard();

            Storyboard.SetTarget(fadeOutAnim, fadeOutCard);
            Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
            Storyboard.SetTarget(scaleOutAnimX, fadeOutCard);
            Storyboard.SetTargetProperty(scaleOutAnimX, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
            Storyboard.SetTarget(scaleOutAnimY, fadeOutCard);
            Storyboard.SetTargetProperty(scaleOutAnimY, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");

            Storyboard.SetTarget(fadeInAnim, fadeInCard);
            Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            Storyboard.SetTarget(scaleInAnimX, fadeInCard);
            Storyboard.SetTargetProperty(scaleInAnimX, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
            Storyboard.SetTarget(scaleInAnimY, fadeInCard);
            Storyboard.SetTargetProperty(scaleInAnimY, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");

            sb.Children.Add(fadeOutAnim);
            sb.Children.Add(scaleOutAnimX);
            sb.Children.Add(scaleOutAnimY);
            sb.Children.Add(fadeInAnim);
            sb.Children.Add(scaleInAnimX);
            sb.Children.Add(scaleInAnimY);

            sb.Begin();

            isFront = !isFront;
            UpdateZIndex();
        }

        void UpdateZIndex()
        {
            if (isFront)
            {
                Canvas.SetZIndex(CardFront, 1);
                Canvas.SetZIndex(CardBack, -1);
            }
            else
            {
                Canvas.SetZIndex(CardFront, -1);
                Canvas.SetZIndex(CardBack, 1);
            }
        }

        private void InitRenderTransform(UIElement border, double scale)
        {
            if (!(border.RenderTransform is ScaleTransform st))
            {
                border.RenderTransform = st = new ScaleTransform();
            }
            st.ScaleX = scale;
            st.ScaleY = scale;
        }
    }
}
