// C# adaptation of HO-COOH/WinUIEssentials InAppAcrylicVisual and PlacementVisualBase.
// Copyright (c) 2023 HO-COOH. MIT license: ../../ThirdParty/WinUIEssentials/LICENSE.txt.
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WComposition = Windows.UI.Composition;
using MComposition = Microsoft.UI.Composition;

namespace Haiyu.Controls;

/// <summary>
/// A decorative acrylic surface using the system compositor's backdrop sampling,
/// bridged into WinUI through ContentExternalBackdropLink. Place behind content,
/// not inside the Background property. No dependency on WinUI3Package.dll.
/// </summary>
public sealed partial class InAppAcrylicVisual : Control
{
    private ElementTheme? appliedTheme;
    private ExternalBackdropLink? link;
    private WComposition.Compositor? systemCompositor;
    private WComposition.CompositionColorBrush? tint;
    private WComposition.CompositionColorBrush? luminosity;
    private WComposition.CompositionBackdropBrush? backdrop;
    private WComposition.CompositionEffectFactory? factory;
    private WComposition.CompositionEffectBrush? effectBrush;
    private WComposition.ImplicitAnimationCollection? colorAnimations;
    private WComposition.ColorKeyFrameAnimation? colorAnimation;
    private MComposition.Visual? placement;
    private MComposition.RectangleClip? clip;

    public InAppAcrylicVisual()
    {
        IsTabStop = false;
        IsHitTestVisible = false;
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
        ActualThemeChanged += (_, _) => UpdateColors();
        // A reattached control may update its inherited ActualTheme without
        // raising ActualThemeChanged. Reconcile only when the value differs.
        LayoutUpdated += (_, _) =>
        {
            if (tint is not null && appliedTheme != ActualTheme) UpdateColors();
        };
        RegisterPropertyChangedCallback(CornerRadiusProperty, (_, _) => UpdateCornerRadius());
    }

    private void Connect()
    {
        if (link is not null) return;
        try
        {
            var host = ElementCompositionPreview.GetElementVisual(this);
            link = ExternalBackdropLink.Create(CompositionTarget.GetCompositorForCurrentThread());
            link.ExternalBackdropBorderMode = MComposition.CompositionBorderMode.Soft;

            systemCompositor = new WComposition.Compositor();
            tint = systemCompositor.CreateColorBrush();
            luminosity = systemCompositor.CreateColorBrush();
            UpdateColors();
            colorAnimation = systemCompositor.CreateColorKeyFrameAnimation();
            var animation = colorAnimation;
            {
                animation.Target = "Color";
                animation.InsertExpressionKeyFrame(1, "this.FinalValue");
                animation.Duration = TimeSpan.FromMilliseconds(250);
                colorAnimations = systemCompositor.CreateImplicitAnimationCollection();
                colorAnimations["Color"] = animation;
                tint.ImplicitAnimations = colorAnimations;
                luminosity.ImplicitAnimations = colorAnimations;
            }

            using var blur = new GaussianBlurEffect
            {
                BlurAmount = 30,
                Optimization = EffectOptimization.Balanced,
                BorderMode = EffectBorderMode.Hard,
                Source = new WComposition.CompositionEffectSourceParameter("Backdrop"),
            };
            using var luminosityEffect = new BlendEffect
            {
                Mode = BlendEffectMode.Color,
                Background = blur,
                Foreground = new WComposition.CompositionEffectSourceParameter("Luminosity"),
            };
            using var tintEffect = new BlendEffect
            {
                Mode = BlendEffectMode.Luminosity,
                Background = luminosityEffect,
                Foreground = new WComposition.CompositionEffectSourceParameter("Tint"),
            };
            factory = systemCompositor.CreateEffectFactory(tintEffect);
            effectBrush = factory.CreateBrush();
            backdrop = systemCompositor.CreateBackdropBrush();
            effectBrush.SetSourceParameter("Backdrop", backdrop);
            effectBrush.SetSourceParameter("Luminosity", luminosity);
            effectBrush.SetSourceParameter("Tint", tint);
            link.SystemBackdrop = effectBrush;
            placement = link.PlacementVisual;
            placement.BorderMode = MComposition.CompositionBorderMode.Soft;
            using (var size = host.Compositor.CreateExpressionAnimation("host.Size"))
            {
                size.SetReferenceParameter("host", host);
                placement.StartAnimation("Size", size);
            }
            clip = host.Compositor.CreateRectangleClip();
            using (var right = host.Compositor.CreateExpressionAnimation("host.Size.X"))
            using (var bottom = host.Compositor.CreateExpressionAnimation("host.Size.Y"))
            {
                right.SetReferenceParameter("host", host);
                bottom.SetReferenceParameter("host", host);
                clip.StartAnimation("Right", right);
                clip.StartAnimation("Bottom", bottom);
            }
            placement.Clip = clip;
            UpdateCornerRadius();
            ElementCompositionPreview.SetElementChildVisual(this, placement);
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    private void UpdateColors()
    {
        if (tint is null || luminosity is null) return;
        appliedTheme = ActualTheme;
        bool light = appliedTheme == ElementTheme.Light;
        luminosity.ImplicitAnimations = null;
        luminosity.Color = light ? Color.FromArgb(217, 252, 252, 252) : Color.FromArgb(245, 44, 44, 44);
        tint.Color = light ? Color.FromArgb(0, 252, 252, 252) : Color.FromArgb(38, 44, 44, 44);
    }

    private void UpdateCornerRadius()
    {
        if (clip is null) return;
        var r = CornerRadius;
        clip.TopLeftRadius = new Vector2((float)r.TopLeft);
        clip.TopRightRadius = new Vector2((float)r.TopRight);
        clip.BottomLeftRadius = new Vector2((float)r.BottomLeft);
        clip.BottomRightRadius = new Vector2((float)r.BottomRight);
    }

    private void Disconnect()
    {
        ElementCompositionPreview.SetElementChildVisual(this, null);
        if (link is not null) link.SystemBackdrop = null;
        placement?.StopAnimation("Size");
        clip?.StopAnimation("Right");
        clip?.StopAnimation("Bottom");
        placement?.Dispose(); placement = null;
        clip?.Dispose(); clip = null;
        link?.Dispose(); link = null;
        effectBrush?.Dispose(); effectBrush = null;
        factory?.Dispose(); factory = null;
        backdrop?.Dispose(); backdrop = null;
        tint?.Dispose(); tint = null;
        luminosity?.Dispose(); luminosity = null;
        colorAnimations?.Dispose(); colorAnimations = null;
        colorAnimation?.Dispose(); colorAnimation = null;
        systemCompositor?.Dispose(); systemCompositor = null;
        appliedTheme = null;
    }
}





