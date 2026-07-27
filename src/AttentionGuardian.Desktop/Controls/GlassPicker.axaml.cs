using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace AttentionGuardian.Desktop.Controls;

public partial class GlassPicker : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<GlassPicker, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<GlassPicker, object?>(
            nameof(SelectedItem),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> PopupWidthProperty =
        AvaloniaProperty.Register<GlassPicker, double>(nameof(PopupWidth), 168);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<GlassPicker, string>(nameof(Label), string.Empty);

    public GlassPicker()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public double PopupWidth
    {
        get => GetValue(PopupWidthProperty);
        set => SetValue(PopupWidthProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private void OpenPicker(object? sender, RoutedEventArgs eventArgs)
    {
        PickerPopup.IsOpen = true;
    }

    private void PickerSelectionChanged(
        object? sender,
        SelectionChangedEventArgs eventArgs)
    {
        if (PickerPopup.IsOpen && eventArgs.AddedItems.Count > 0)
        {
            PickerPopup.IsOpen = false;
        }
    }
}
