using System.Windows;

namespace DoctorRx.Presentation.Behaviors;

public static class NavProperties
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsActive",
            typeof(bool),
            typeof(NavProperties),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static void SetIsActive(UIElement element, bool value) =>
        element.SetValue(IsActiveProperty, value);

    public static bool GetIsActive(UIElement element) =>
        (bool)element.GetValue(IsActiveProperty);
}
