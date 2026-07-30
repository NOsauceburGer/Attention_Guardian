using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AttentionGuardian.Desktop.Controls;

public partial class GlassNumberStepper : UserControl
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<GlassNumberStepper, decimal>(
            nameof(Value),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<decimal> MinimumProperty =
        AvaloniaProperty.Register<GlassNumberStepper, decimal>(nameof(Minimum));

    public static readonly StyledProperty<decimal> MaximumProperty =
        AvaloniaProperty.Register<GlassNumberStepper, decimal>(
            nameof(Maximum),
            decimal.MaxValue);

    public static readonly StyledProperty<decimal> IncrementProperty =
        AvaloniaProperty.Register<GlassNumberStepper, decimal>(nameof(Increment), 1);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<GlassNumberStepper, string>(
            nameof(Label),
            "数值");

    public GlassNumberStepper()
    {
        InitializeComponent();
        RefreshText(Value);
    }

    public decimal Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public decimal Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public decimal Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public decimal Increment
    {
        get => GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string DecreaseLabel => $"减少{Label}";

    public string IncreaseLabel => $"增加{Label}";

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && ValueText is not null)
        {
            RefreshText(Value);
        }
    }

    private void Decrease(object? sender, RoutedEventArgs eventArgs) =>
        ChangeValue(-Increment);

    private void Increase(object? sender, RoutedEventArgs eventArgs) =>
        ChangeValue(Increment);

    private void ChangeValue(decimal amount)
    {
        CommitCurrentText();
        SetCurrentValue(ValueProperty, Math.Clamp(Value + amount, Minimum, Maximum));
    }

    private void CommitText(object? sender, RoutedEventArgs eventArgs) =>
        CommitCurrentText();

    private void ValueTextKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Enter)
        {
            return;
        }

        CommitCurrentText();
        eventArgs.Handled = true;
    }

    private void CommitCurrentText()
    {
        if (decimal.TryParse(
                ValueText.Text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out var parsed))
        {
            SetCurrentValue(
                ValueProperty,
                Math.Clamp(decimal.Truncate(parsed), Minimum, Maximum));
        }

        RefreshText(Value);
    }

    private void RefreshText(decimal value)
    {
        var text = decimal.Truncate(value).ToString(
            "0",
            CultureInfo.CurrentCulture);
        if (ValueText.Text != text)
        {
            ValueText.Text = text;
        }
    }
}
