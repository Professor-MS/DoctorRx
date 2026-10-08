using System;
using System.Globalization;
using System.Windows.Data;
using DoctorRx.Application.DTOs;

namespace DoctorRx.Presentation.Converters;

public class FollowUpModeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FollowUpMode mode)
        {
            return mode switch
            {
                FollowUpMode.None => "None",
                FollowUpMode.InDays => "Days",
                FollowUpMode.InWeeks => "Weeks",
                FollowUpMode.InMonths => "Months",
                FollowUpMode.SOS => "As needed (SOS)",
                FollowUpMode.PRN => "When required (PRN)",
                _ => mode.ToString()
            };
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
