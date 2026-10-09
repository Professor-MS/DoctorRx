using DoctorRx.Domain.Common;
using Xunit;

namespace DoctorRx.Tests;

public class DoctorDisplayNameFormatterTests
{
    [Theory]
    [InlineData("Dr.", "Muhammad Tariq", "Dr. Muhammad Tariq")]
    [InlineData("Dr.", "Dr. Muhammad Tariq", "Dr. Muhammad Tariq")]
    [InlineData("Dr.", "Dr Muhammad Tariq", "Dr. Muhammad Tariq")]
    [InlineData("Dr.", "dr. muhammad tariq", "Dr. muhammad tariq")]
    [InlineData("Dr", "Dr. Muhammad Tariq", "Dr Muhammad Tariq")]
    [InlineData("Dr", "Muhammad Tariq", "Dr Muhammad Tariq")]
    [InlineData("Prof. Dr.", "Prof. Dr. Muhammad Tariq", "Prof. Dr. Muhammad Tariq")]
    [InlineData("Prof. Dr.", "Dr. Muhammad Tariq", "Prof. Dr. Muhammad Tariq")]
    [InlineData("Prof. Dr.", "Muhammad Tariq", "Prof. Dr. Muhammad Tariq")]
    [InlineData("Dr.", "Prof. Dr. Muhammad Tariq", "Prof. Dr. Muhammad Tariq")]
    [InlineData("Dr.", "Prof. Muhammad Tariq", "Prof. Muhammad Tariq")]
    [InlineData(null, "Muhammad Tariq", "Muhammad Tariq")]
    [InlineData("", "Muhammad Tariq", "Muhammad Tariq")]
    [InlineData("   ", "Muhammad Tariq", "Muhammad Tariq")]
    [InlineData("Dr.", null, "")]
    [InlineData("Dr.", "", "")]
    [InlineData("Dr.", "   ", "")]
    public void Format_DeduplicatesTitlesAndPunctuationCorrectly(string? titlePrefix, string? name, string expected)
    {
        var result = DoctorDisplayNameFormatter.Format(titlePrefix, name);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_UrduHonorifics_DeduplicatesAndFormats()
    {
        // Without prefix in name
        Assert.Equal("ڈاکٹر محمد طارق", DoctorDisplayNameFormatter.Format("ڈاکٹر", "محمد طارق"));
        // With duplicate prefix in name
        Assert.Equal("ڈاکٹر محمد طارق", DoctorDisplayNameFormatter.Format("ڈاکٹر", "ڈاکٹر محمد طارق"));

        // Hakeem title
        Assert.Equal("حکیم اجمل خان", DoctorDisplayNameFormatter.Format("حکیم", "اجمل خان"));
        Assert.Equal("حکیم اجمل خان", DoctorDisplayNameFormatter.Format("حکیم", "حکیم اجمل خان"));
    }
}
