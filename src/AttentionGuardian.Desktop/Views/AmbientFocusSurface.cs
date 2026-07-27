using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace AttentionGuardian.Desktop.Views;

public sealed class AmbientFocusSurface : Control
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AmbientFocusSurface, bool>(nameof(IsActive));

    public static readonly StyledProperty<string> TaskKeyProperty =
        AvaloniaProperty.Register<AmbientFocusSurface, string>(
            nameof(TaskKey),
            string.Empty);

    private readonly DispatcherTimer animationTimer;
    private readonly Stopwatch frameClock = new();
    private readonly bool reduceMotion = MotionPreferences.IsReducedMotionEnabled;
    private Window? hostWindow;
    private double phase;
    private double focusBlend;
    private double transitionPulse;
    private bool hasObservedTask;

    public AmbientFocusSurface()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        animationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            Animate);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string TaskKey
    {
        get => GetValue(TaskKeyProperty);
        set => SetValue(TaskKeyProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(CreateFallbackBrush(), null, bounds);
        context.Custom(new FluidShaderDrawOperation(
            bounds,
            (float)phase,
            (float)focusBlend,
            (float)transitionPulse));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            transitionPulse = 1;
            if (reduceMotion)
            {
                focusBlend = IsActive ? 1 : 0;
            }

            UpdateAnimationState();
            InvalidateVisual();
        }

        if (change.Property == TaskKeyProperty)
        {
            if (hasObservedTask && IsActive)
            {
                transitionPulse = 1;
            }

            hasObservedTask = true;
            UpdateAnimationState();
            InvalidateVisual();
        }

        if (change.Property == IsVisibleProperty)
        {
            UpdateAnimationState();
        }
    }

    protected override void OnAttachedToVisualTree(
        VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        hostWindow = TopLevel.GetTopLevel(this) as Window;
        if (hostWindow is not null)
        {
            hostWindow.PropertyChanged += OnHostWindowPropertyChanged;
            hostWindow.Closed += OnHostWindowClosed;
        }

        UpdateAnimationState();
    }

    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs eventArgs)
    {
        DetachHostWindow();
        animationTimer.Stop();
        frameClock.Reset();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void OnHostWindowPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == Window.WindowStateProperty
            || eventArgs.Property == IsVisibleProperty)
        {
            UpdateAnimationState();
        }
    }

    private void OnHostWindowClosed(object? sender, EventArgs eventArgs)
    {
        DetachHostWindow();
        animationTimer.Stop();
        frameClock.Reset();
    }

    private void DetachHostWindow()
    {
        if (hostWindow is null)
        {
            return;
        }

        hostWindow.PropertyChanged -= OnHostWindowPropertyChanged;
        hostWindow.Closed -= OnHostWindowClosed;
        hostWindow = null;
    }

    private void UpdateAnimationState()
    {
        var shouldRender = !reduceMotion
            && IsVisible
            && VisualRoot is not null
            && hostWindow is { IsVisible: true }
            && hostWindow.WindowState != WindowState.Minimized;
        if (!shouldRender)
        {
            animationTimer.Stop();
            frameClock.Reset();
            return;
        }

        frameClock.Restart();
        animationTimer.Start();
    }

    private void Animate(object? sender, EventArgs eventArgs)
    {
        if (hostWindow is not { IsVisible: true }
            || hostWindow.WindowState == WindowState.Minimized)
        {
            UpdateAnimationState();
            return;
        }

        var elapsed = Math.Clamp(frameClock.Elapsed.TotalSeconds, 0, 0.05);
        frameClock.Restart();

        var targetFocus = IsActive ? 1d : 0d;
        var blendRate = targetFocus > focusBlend ? 0.72 : 0.9;
        focusBlend = MoveTowards(focusBlend, targetFocus, elapsed * blendRate);
        transitionPulse = Math.Max(0, transitionPulse - (elapsed * 0.74));

        var idleSpeed = 0.42;
        var focusedSpeed = 0.105;
        var baseSpeed = idleSpeed + ((focusedSpeed - idleSpeed) * focusBlend);
        phase += elapsed * (baseSpeed + (transitionPulse * 1.35));
        InvalidateVisual();
    }

    private static double MoveTowards(double value, double target, double amount)
    {
        if (value < target)
        {
            return Math.Min(target, value + amount);
        }

        return Math.Max(target, value - amount);
    }

    private static LinearGradientBrush CreateFallbackBrush() =>
        new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(48, 132, 166), 0),
                new GradientStop(Color.FromRgb(41, 99, 143), 0.45),
                new GradientStop(Color.FromRgb(28, 58, 111), 1)
            }
        };

    private sealed class FluidShaderDrawOperation(
        Rect bounds,
        float phase,
        float focusBlend,
        float transitionPulse) : ICustomDrawOperation
    {
        private const string ShaderSource = """
            uniform float2 resolution;
            uniform float time;
            uniform float focus;
            uniform float pulse;

            float hash(float2 p) {
                return fract(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = fract(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return mix(
                    mix(hash(i), hash(i + float2(1.0, 0.0)), u.x),
                    mix(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x),
                    u.y);
            }

            float fbm(float2 p) {
                float value = 0.0;
                float amplitude = 0.55;
                value += amplitude * noise(p);
                p = p * 2.03 + float2(3.1, 1.7);
                amplitude *= 0.5;
                value += amplitude * noise(p);
                p = p * 2.01 + float2(1.3, 4.2);
                amplitude *= 0.5;
                value += amplitude * noise(p);
                p = p * 2.04 + float2(4.7, 2.4);
                value += amplitude * 0.5 * noise(p);
                return value;
            }

            half4 main(float2 fragCoord) {
                float2 uv = fragCoord / resolution;
                float aspect = resolution.x / max(resolution.y, 1.0);
                float2 p = float2((uv.x - 0.5) * aspect, uv.y - 0.5);

                float2 flowA = float2(
                    fbm(p * 2.15 + float2(time * 0.31, -time * 0.19)),
                    fbm(p * 2.05 + float2(-time * 0.23, time * 0.27)));
                float2 warped = p + (flowA - 0.5) * (0.42 + pulse * 0.12);
                float fieldA = fbm(warped * 2.55 + float2(time * 0.16, time * 0.11));
                float fieldB = fbm(
                    warped.yx * 2.05 + float2(-time * 0.12, time * 0.18) + 5.7);

                half3 deep = half3(0.055, 0.145, 0.315);
                half3 cobalt = half3(0.105, 0.305, 0.545);
                half3 cyan = half3(0.235, 0.690, 0.760);
                half3 mist = half3(0.480, 0.830, 0.830);
                half3 violet = half3(0.325, 0.380, 0.685);

                float contrast = mix(1.18, 0.64, focus);
                float ridge = smoothstep(0.22, 0.84, fieldA);
                float veil = smoothstep(0.30, 0.82, fieldB);
                half3 color = mix(deep, cobalt, ridge);
                color = mix(color, cyan, veil * 0.58 * contrast);
                color = mix(color, mist, smoothstep(0.62, 0.93, fieldA) * 0.30 * contrast);
                color = mix(color, violet, smoothstep(0.54, 0.90, 1.0 - fieldB) * 0.24);

                float centerDepth = smoothstep(0.82, 0.12, length(p));
                color = mix(color, half3(0.095, 0.255, 0.455), centerDepth * 0.14);
                color = mix(color, half3(dot(color, half3(0.24, 0.62, 0.14))),
                            focus * 0.13);
                color += half3(0.025, 0.050, 0.070) * pulse;
                return half4(color, 1.0);
            }
            """;

        private static readonly Lazy<SKRuntimeEffect?> RuntimeEffect =
            new(CreateRuntimeEffect);

        public Rect Bounds { get; } = bounds;

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point point) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(
                    out var leaseFeature)
                || RuntimeEffect.Value is not { } effect)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            using var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["resolution"] = new[]
                {
                    (float)Bounds.Width,
                    (float)Bounds.Height
                },
                ["time"] = phase,
                ["focus"] = focusBlend,
                ["pulse"] = transitionPulse
            };
            using var shader = effect.ToShader(uniforms);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Shader = shader
            };
            lease.SkCanvas.DrawRect(
                SKRect.Create((float)Bounds.Width, (float)Bounds.Height),
                paint);
        }

        private static SKRuntimeEffect? CreateRuntimeEffect()
        {
            var effect = SKRuntimeEffect.CreateShader(ShaderSource, out _);
            return effect;
        }
    }
}
