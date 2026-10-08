using System;
using System.Windows;
using System.Windows.Controls;

namespace DoctorRx.Presentation.Behaviors;

public static class AutoFlowDirection
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AutoFlowDirection),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if ((bool)e.NewValue)
            {
                textBox.TextChanged += OnTextBoxTextChanged;
                UpdateDirection(textBox);
            }
            else
            {
                textBox.TextChanged -= OnTextBoxTextChanged;
            }
        }
        else if (d is TextBlock textBlock)
        {
            if ((bool)e.NewValue)
            {
                UpdateTextBlockDirection(textBlock);
            }
        }
    }

    private static void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateDirection(textBox);
        }
    }

    private static void UpdateDirection(TextBox textBox)
    {
        var text = textBox.Text;
        if (IsUrduOrArabic(text))
        {
            textBox.FlowDirection = FlowDirection.RightToLeft;
            textBox.TextAlignment = TextAlignment.Right;
        }
        else
        {
            textBox.FlowDirection = FlowDirection.LeftToRight;
            textBox.TextAlignment = TextAlignment.Left;
        }
    }

    private static void UpdateTextBlockDirection(TextBlock textBlock)
    {
        var text = textBlock.Text;
        if (IsUrduOrArabic(text))
        {
            textBlock.FlowDirection = FlowDirection.RightToLeft;
            textBlock.TextAlignment = TextAlignment.Right;
        }
        else
        {
            textBlock.FlowDirection = FlowDirection.LeftToRight;
            textBlock.TextAlignment = TextAlignment.Left;
        }
    }

    public static bool IsUrduOrArabic(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsDigit(c))
            {
                continue;
            }

            // Arabic & Urdu Unicode blocks
            if ((c >= '\u0600' && c <= '\u06FF') ||
                (c >= '\u0750' && c <= '\u077F') ||
                (c >= '\u08A0' && c <= '\u08FF') ||
                (c >= '\uFB50' && c <= '\uFDFF') ||
                (c >= '\uFE70' && c <= '\uFEFF'))
            {
                return true;
            }

            // If first significant letter is Latin/English, return false
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
            {
                return false;
            }
        }

        return false;
    }
}
