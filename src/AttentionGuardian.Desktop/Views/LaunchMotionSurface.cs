using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace AttentionGuardian.Desktop.Views;

public sealed class LaunchMotionSurface : Control
{
    private const double DurationSeconds = 7.2;
    private readonly DispatcherTimer animationTimer;
    private readonly Stopwatch animationClock = new();
    private Window? hostWindow;
    private double progress;

    public LaunchMotionSurface()
    {
        ClipToBounds = true;
        Focusable = true;
        animationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            Animate);
    }

    public event EventHandler? Completed;

    internal static string? GetShaderCompilationError() =>
        LaunchDrawOperation.GetShaderCompilationError();

    internal static int GetLetteringContourCount() =>
        LaunchDrawOperation.GetLetteringContourCount();

    internal static int GetLetteringGeometryCount() =>
        LaunchDrawOperation.GetLetteringGeometryCount();

    internal static string GetLetteringResourceSha256() =>
        LaunchDrawOperation.GetLetteringResourceSha256();

    internal static int GetLetteringStageCount() =>
        LaunchDrawOperation.GetLetteringStageCount();

    internal static double GetLetteringOutlineCoverageRatio() =>
        LaunchDrawOperation.GetLetteringOutlineCoverageRatio();

    internal static double GetLetteringStageMaximumJump(int stage) =>
        LaunchDrawOperation.GetLetteringStageMaximumJump(stage);

    internal static Rect GetLetteringMappedBounds(
        double width,
        double height) =>
        LaunchDrawOperation.GetLetteringMappedBounds(width, height);

    public void Start()
    {
        if (MotionPreferences.IsReducedMotionEnabled)
        {
            Finish();
            return;
        }

        progress = 0;
        Opacity = 1;
        IsVisible = true;
        IsHitTestVisible = true;
        animationClock.Restart();
        animationTimer.Start();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new LaunchDrawOperation(
            new Rect(Bounds.Size),
            (float)progress));
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
    }

    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs eventArgs)
    {
        StopAnimation();
        DetachHostWindow();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void Animate(object? sender, EventArgs eventArgs)
    {
        if (hostWindow is not { IsVisible: true }
            || hostWindow.WindowState == WindowState.Minimized)
        {
            animationClock.Stop();
            animationTimer.Stop();
            return;
        }

        progress = Math.Clamp(
            animationClock.Elapsed.TotalSeconds / DurationSeconds,
            0,
            1);
        InvalidateVisual();
        if (progress >= 1)
        {
            Finish();
        }
    }

    private void OnHostWindowPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property != Window.WindowStateProperty
            && eventArgs.Property != IsVisibleProperty)
        {
            return;
        }

        if (hostWindow is { IsVisible: true }
            && hostWindow.WindowState != WindowState.Minimized
            && IsVisible
            && progress < 1
            && !animationTimer.IsEnabled)
        {
            animationClock.Start();
            animationTimer.Start();
        }
    }

    private void OnHostWindowClosed(object? sender, EventArgs eventArgs)
    {
        StopAnimation();
        DetachHostWindow();
    }

    private void Finish()
    {
        StopAnimation();
        progress = 1;
        IsHitTestVisible = false;
        IsVisible = false;
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void StopAnimation()
    {
        animationTimer.Stop();
        animationClock.Stop();
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

    private sealed class LaunchDrawOperation(
        Rect bounds,
        float progress) : ICustomDrawOperation
    {
        private const string ShaderSource = """
            uniform float2 resolution;
            uniform float time;
            uniform float reveal;

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
                return value;
            }

            half4 main(float2 fragCoord) {
                float2 uv = fragCoord / resolution;
                float aspect = resolution.x / max(resolution.y, 1.0);
                float2 p = float2((uv.x - 0.5) * aspect, uv.y - 0.5);
                float t = time * 0.42;

                float2 flow = float2(
                    fbm(p * 2.15 + float2(t * 0.31, -t * 0.19)),
                    fbm(p * 2.05 + float2(-t * 0.23, t * 0.27)));
                float2 warped = p + (flow - 0.5) * 0.46;
                float fieldA = fbm(warped * 2.55 + float2(t * 0.16, t * 0.11));
                float fieldB = fbm(
                    warped.yx * 2.05 + float2(-t * 0.12, t * 0.18) + 5.7);

                half3 deep = half3(0.028, 0.075, 0.185);
                half3 cobalt = half3(0.090, 0.285, 0.525);
                half3 cyan = half3(0.190, 0.650, 0.735);
                half3 violet = half3(0.280, 0.245, 0.570);
                half3 color = mix(deep, cobalt, smoothstep(0.22, 0.84, fieldA));
                color = mix(color, cyan, smoothstep(0.32, 0.82, fieldB) * 0.58);
                color = mix(
                    color,
                    violet,
                    smoothstep(0.55, 0.91, 1.0 - fieldB) * 0.23);

                float radius = mix(0.005, 1.22, reveal);
                float organicEdge = radius + (fieldA - 0.5) * 0.18 * reveal;
                float mask = 1.0 - smoothstep(organicEdge - 0.16, organicEdge, length(p));
                float centerGlow = exp(-length(p) * 18.0) * (1.0 - reveal * 0.55);
                color += half3(0.22, 0.48, 0.70) * centerGlow;
                return half4(color * mask, 1.0);
            }
            """;

        private static readonly Lazy<SKRuntimeEffect?> RuntimeEffect =
            new(CreateRuntimeEffect);
        private static readonly Lazy<LetteringGeometry> Lettering =
            new(LoadLetteringGeometry);
        private const string LetteringResourceName =
            "AttentionGuardian.Desktop.Assets.final.svg";

        public Rect Bounds { get; } = bounds;

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point point) => Bounds.Contains(point);

        public void Render(ImmediateDrawingContext context)
        {
            if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(
                    out var leaseFeature))
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            var surfaceOpacity = 1 - SmoothStep(0.88f, 1, progress);
            using var layerPaint = new SKPaint
            {
                Color = new SKColor(
                    255,
                    255,
                    255,
                    (byte)(255 * surfaceOpacity))
            };
            canvas.SaveLayer(layerPaint);
            canvas.DrawColor(SKColors.Black);

            DrawFluid(canvas);
            DrawLettering(canvas);
            canvas.Restore();
        }

        private void DrawFluid(SKCanvas canvas)
        {
            if (RuntimeEffect.Value is not { } effect)
            {
                return;
            }

            var reveal = SmoothStep(0.14f, 0.58f, progress);
            using var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["resolution"] = new[]
                {
                    (float)Bounds.Width,
                    (float)Bounds.Height
                },
                ["time"] = progress * (float)DurationSeconds,
                ["reveal"] = reveal
            };
            using var shader = effect.ToShader(uniforms);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Shader = shader
            };
            canvas.DrawRect(
                SKRect.Create((float)Bounds.Width, (float)Bounds.Height),
                paint);

            var pointOpacity =
                (1 - SmoothStep(0.14f, 0.20f, progress))
                * SmoothStep(0.008f, 0.035f, progress);
            if (pointOpacity <= 0)
            {
                return;
            }

            var center = new SKPoint(
                (float)Bounds.Width / 2,
                (float)Bounds.Height / 2);
            using var pointPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(205, 238, 255, (byte)(235 * pointOpacity)),
                MaskFilter = SKMaskFilter.CreateBlur(
                    SKBlurStyle.Normal,
                    8 + (10 * reveal))
            };
            canvas.DrawCircle(center, 2.2f + (3 * reveal), pointPaint);
        }

        private void DrawLettering(SKCanvas canvas)
        {
            var outlineProgress = SmoothStep(0.24f, 0.68f, progress);
            if (outlineProgress <= 0)
            {
                return;
            }

            var fillProgress = SmoothStep(0.68f, 0.77f, progress);
            var shimmer = Pulse(0.77f, 0.90f, progress);
            var fade = 1 - SmoothStep(0.92f, 0.99f, progress);
            var alpha = SmoothStep(0, 0.07f, outlineProgress) * fade;
            var geometry = Lettering.Value;
            var placement = CalculatePlacement(
                (float)Bounds.Width,
                (float)Bounds.Height,
                geometry.Bounds);
            var remainingLength =
                geometry.TotalFragmentLength * outlineProgress;
            SKPoint? penPosition = null;
            canvas.Save();
            canvas.Translate(placement.Left, placement.Top);
            canvas.Scale(placement.Scale);
            canvas.Translate(-geometry.Bounds.Left, -geometry.Bounds.Top);
            using var glowPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6.4f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                Color = new SKColor(126, 208, 255, (byte)(82 * alpha)),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 9)
            };
            using var outlinePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.65f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                Color = new SKColor(224, 245, 255, (byte)(232 * alpha))
            };

            foreach (var fragment in geometry.OrderedFragments)
            {
                if (remainingLength <= 0)
                {
                    break;
                }

                var visibleLength = Math.Min(remainingLength, fragment.Length);
                using var segment = new SKPath();
                using var measure = new SKPathMeasure(fragment.Path, false);
                measure.GetSegment(
                    0,
                    visibleLength,
                    segment,
                    true);
                canvas.DrawPath(segment, glowPaint);
                canvas.DrawPath(segment, outlinePaint);
                if (visibleLength < fragment.Length
                    && measure.GetPositionAndTangent(
                        visibleLength,
                        out var position,
                        out _))
                {
                    penPosition = position;
                }

                remainingLength -= fragment.Length;
            }

            if (fillProgress > 0)
            {
                using var fillGlowPaint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(
                        112,
                        201,
                        255,
                        (byte)((58 + (20 * shimmer))
                            * fillProgress
                            * fade)),
                    MaskFilter = SKMaskFilter.CreateBlur(
                        SKBlurStyle.Normal,
                        9)
                };
                using var fillPaint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(
                        225,
                        245,
                        255,
                        (byte)(238 * fillProgress * fade))
                };
                using var shimmerPaint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(
                        242,
                        251,
                        255,
                        (byte)(30 * shimmer * fade))
                };
                canvas.DrawPath(geometry.Path, fillGlowPaint);
                canvas.DrawPath(geometry.Path, fillPaint);
                if (shimmer > 0)
                {
                    canvas.DrawPath(geometry.Path, shimmerPaint);
                }
            }

            if (penPosition is { } tip && outlineProgress < 0.995f)
            {
                using var tipPaint = new SKPaint
                {
                    IsAntialias = true,
                    Color = new SKColor(225, 247, 255, (byte)(210 * alpha)),
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
                };
                canvas.DrawCircle(tip, 3.4f, tipPaint);
            }

            canvas.Restore();
        }

        private static LetteringGeometry LoadLetteringGeometry()
        {
            using var stream = typeof(LaunchMotionSurface)
                .Assembly
                .GetManifestResourceStream(LetteringResourceName)
                ?? throw new InvalidOperationException(
                    "Guardian lettering resource could not be loaded.");
            using var reader = new StreamReader(stream);
            var document = XDocument.Load(reader);
            XNamespace svgNamespace = "http://www.w3.org/2000/svg";
            var pathElements = document
                .Descendants(svgNamespace + "path")
                .ToArray();
            if (pathElements.Length != 1)
            {
                throw new InvalidOperationException(
                    "The final Guardian lettering must contain exactly one path.");
            }

            var pathData = pathElements[0].Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(pathData))
            {
                throw new InvalidOperationException(
                    "The final Guardian lettering path is empty.");
            }

            var path = SKPath.ParseSvgPathData(pathData)
                ?? throw new InvalidOperationException(
                    "The final Guardian lettering path could not be parsed.");
            var contours = new List<LetteringContour>();
            using var measure = new SKPathMeasure(path, false);
            do
            {
                var length = measure.Length;
                if (length <= 0)
                {
                    continue;
                }

                var contourPath = new SKPath();
                measure.GetSegment(0, length, contourPath, true);
                contours.Add(new LetteringContour(contourPath, length));
            }
            while (measure.NextContour());

            if (contours.Count == 0)
            {
                throw new InvalidOperationException(
                    "The final Guardian lettering has no drawable contours.");
            }

            var bounds = path.Bounds;
            var orderedFragments = CreateOrderedFragments(contours, bounds);
            return new LetteringGeometry(
                path,
                contours,
                orderedFragments,
                orderedFragments.Sum(fragment => fragment.Length),
                bounds);
        }

        private static IReadOnlyList<LetteringFragment>
            CreateOrderedFragments(
                IReadOnlyList<LetteringContour> contours,
                SKRect bounds)
        {
            var stages = Enumerable.Range(0, 8)
                .Select(_ => new List<LetteringFragment>())
                .ToArray();

            // final.svg is immutable. These ranges only control when its
            // existing contour becomes visible. The G is deliberately
            // completed as a human-readable C gesture before its crossing
            // stroke and lower loop are allowed to appear.
            AddContourRange(contours[0], 820, 1300, 0, stages[0]);
            AddContourRange(contours[0], 300, 820, 0, stages[0]);

            AddContourRange(contours[0], 1300, 1360, 1, stages[1]);
            AddContourRange(
                contours[0],
                2240,
                contours[0].Length,
                1,
                stages[1]);
            AddContourRange(contours[0], 0, 300, 1, stages[1]);
            AddContourRange(
                contours[1],
                0,
                contours[1].Length,
                1,
                stages[1]);
            AddContourRange(contours[0], 1360, 1400, 1, stages[1]);

            AddSpatiallyStagedRange(
                contours[0],
                1400,
                2240,
                bounds,
                stages);
            for (var index = 2; index < contours.Count; index++)
            {
                AddSpatiallyStagedRange(
                    contours[index],
                    0,
                    contours[index].Length,
                    bounds,
                    stages);
            }

            return stages.SelectMany(stage => stage).ToArray();
        }

        private static void AddSpatiallyStagedRange(
            LetteringContour contour,
            float rangeStart,
            float rangeEnd,
            SKRect bounds,
            IReadOnlyList<List<LetteringFragment>> stages)
        {
            const float fragmentLength = 11;
            using var measure = new SKPathMeasure(contour.Path, false);
            for (var start = rangeStart; start < rangeEnd;)
            {
                var stop = Math.Min(start + fragmentLength, rangeEnd);
                measure.GetPositionAndTangent(
                    (start + stop) / 2,
                    out var midpoint,
                    out _);
                var stage = GetLowercaseStage(midpoint, bounds);
                AddFragment(
                    measure,
                    contour.Length,
                    start,
                    stop,
                    stage,
                    stages[stage]);
                start = stop;
            }
        }

        private static void AddContourRange(
            LetteringContour contour,
            float rangeStart,
            float rangeEnd,
            int stage,
            ICollection<LetteringFragment> destination)
        {
            const float fragmentLength = 11;
            using var measure = new SKPathMeasure(contour.Path, false);
            for (var start = rangeStart; start < rangeEnd;)
            {
                var stop = Math.Min(start + fragmentLength, rangeEnd);
                AddFragment(
                    measure,
                    contour.Length,
                    start,
                    stop,
                    stage,
                    destination);
                start = stop;
            }
        }

        private static void AddFragment(
            SKPathMeasure measure,
            float contourLength,
            float start,
            float stop,
            int stage,
            ICollection<LetteringFragment> destination)
        {
            var path = new SKPath();
            measure.GetSegment(start, stop, path, true);
            measure.GetPositionAndTangent(
                Math.Min(start, contourLength - 0.001f),
                out var startPoint,
                out _);
            measure.GetPositionAndTangent(
                Math.Min(stop, contourLength - 0.001f),
                out var endPoint,
                out _);
            destination.Add(new LetteringFragment(
                path,
                stop - start,
                startPoint,
                endPoint,
                stage));
        }

        private static int GetLowercaseStage(
            SKPoint point,
            SKRect bounds)
        {
            var normalizedX = (point.X - bounds.Left) / bounds.Width;
            if (normalizedX < 0.46f)
            {
                return 2;
            }

            if (normalizedX < 0.535f)
            {
                return 3;
            }

            if (normalizedX < 0.66f)
            {
                return 4;
            }

            if (normalizedX < 0.72f)
            {
                return 5;
            }

            if (normalizedX < 0.81f)
            {
                return 6;
            }

            return 7;
        }

        private static LetteringPlacement CalculatePlacement(
            float width,
            float height,
            SKRect glyphBounds)
        {
            var drawingWidth = Math.Min(width * 0.76f, 860);
            var scale = drawingWidth / glyphBounds.Width;
            var drawingHeight = glyphBounds.Height * scale;
            return new LetteringPlacement(
                (width - drawingWidth) / 2,
                (height - drawingHeight) / 2,
                drawingWidth,
                drawingHeight,
                scale);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            var x = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
            return x * x * (3 - (2 * x));
        }

        private static float Pulse(float edge0, float edge1, float value)
        {
            var x = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
            return MathF.Sin(MathF.PI * x);
        }

        private static SKRuntimeEffect? CreateRuntimeEffect() =>
            SKRuntimeEffect.CreateShader(ShaderSource, out _);

        internal static string? GetShaderCompilationError()
        {
            using var effect =
                SKRuntimeEffect.CreateShader(ShaderSource, out var errorText);
            return effect is null ? errorText : null;
        }

        internal static int GetLetteringContourCount() =>
            Lettering.Value.Contours.Count;

        internal static int GetLetteringGeometryCount() => 1;

        internal static int GetLetteringStageCount() =>
            Lettering.Value.OrderedFragments
                .Select(fragment => fragment.Stage)
                .Distinct()
                .Count();

        internal static double GetLetteringOutlineCoverageRatio() =>
            Lettering.Value.TotalFragmentLength
            / Lettering.Value.Contours.Sum(contour => contour.Length);

        internal static double GetLetteringStageMaximumJump(int stage)
        {
            var fragments = Lettering.Value.OrderedFragments
                .Where(fragment => fragment.Stage == stage)
                .ToArray();
            var maximum = 0f;
            for (var index = 1; index < fragments.Length; index++)
            {
                maximum = Math.Max(
                    maximum,
                    MathF.Sqrt(DistanceSquared(
                        fragments[index - 1].End,
                        fragments[index].Start)));
            }

            return maximum;
        }

        internal static Rect GetLetteringMappedBounds(
            double width,
            double height)
        {
            var placement = CalculatePlacement(
                (float)width,
                (float)height,
                Lettering.Value.Bounds);
            return new Rect(
                placement.Left,
                placement.Top,
                placement.Width,
                placement.Height);
        }

        internal static string GetLetteringResourceSha256()
        {
            using var stream = typeof(LaunchMotionSurface)
                .Assembly
                .GetManifestResourceStream(LetteringResourceName)
                ?? throw new InvalidOperationException(
                    "Guardian lettering resource could not be loaded.");
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static float DistanceSquared(SKPoint first, SKPoint second)
        {
            var deltaX = first.X - second.X;
            var deltaY = first.Y - second.Y;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }

        private sealed record LetteringGeometry(
            SKPath Path,
            IReadOnlyList<LetteringContour> Contours,
            IReadOnlyList<LetteringFragment> OrderedFragments,
            float TotalFragmentLength,
            SKRect Bounds);

        private sealed record LetteringContour(SKPath Path, float Length);

        private sealed record LetteringFragment(
            SKPath Path,
            float Length,
            SKPoint Start,
            SKPoint End,
            int Stage);

        private readonly record struct LetteringPlacement(
            float Left,
            float Top,
            float Width,
            float Height,
            float Scale);
    }
}
