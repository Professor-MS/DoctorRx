using System;
using System.Globalization;
using System.Windows.Data;
using DoctorRx.Application.Common;
using DoctorRx.Application.DTOs;
using DoctorRx.Domain.Enums;
using DoctorRx.Domain.Models;

namespace DoctorRx.Presentation.Converters;

public class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        return value switch
        {
            PrescriptionDatePreset preset => EnumDisplayFormatter.GetDisplayName(preset),
            PrescriptionStatusSearchFilter filter => EnumDisplayFormatter.GetDisplayName(filter),
            PrescriptionSortOption sort => EnumDisplayFormatter.GetDisplayName(sort),
            PrescriptionStatus status => EnumDisplayFormatter.GetDisplayName(status),
            FollowUpMode mode => EnumDisplayFormatter.GetDisplayName(mode),
            Gender gender => EnumDisplayFormatter.GetDisplayName(gender),
            MealRelation meal => EnumDisplayFormatter.GetDisplayName(meal),
            RouteOfAdministration route => EnumDisplayFormatter.GetDisplayName(route),
            _ => value.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
