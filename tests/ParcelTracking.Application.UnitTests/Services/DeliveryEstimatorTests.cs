using FluentAssertions;
using ParcelTracking.Infrastructure.Services;

namespace ParcelTracking.Application.UnitTests.Services;

public class DeliveryEstimatorTests
{
    private readonly DeliveryEstimator _sut = new();

    [Fact]
    public void Estimate_Overnight_Returns1BusinessDay()
    {
        // Arrange — register on a Monday
        var monday = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero); // Monday

        // Act
        var result = _sut.Estimate("Overnight", monday);

        // Assert — next business day is Tuesday
        result.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        result.Date.Should().Be(monday.AddDays(1).Date);
    }

    [Fact]
    public void Estimate_Overnight_OnFriday_ReturnsMonday()
    {
        // Arrange — register on a Friday
        var friday = new DateTimeOffset(2026, 2, 27, 12, 0, 0, TimeSpan.Zero); // Friday

        // Act
        var result = _sut.Estimate("Overnight", friday);

        // Assert — skips weekend, lands on Monday
        result.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void Estimate_Express_Returns2BusinessDaysFromMonday()
    {
        // Arrange — Monday
        var monday = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = _sut.Estimate("Express", monday);

        // Assert — Mon+2 business days = Wednesday
        result.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
    }

    [Fact]
    public void Estimate_Standard_Returns5BusinessDaysFromMonday()
    {
        // Arrange — Monday 23 Feb 2026
        var monday = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = _sut.Estimate("Standard", monday);

        // Assert — 5 business days from Mon = Mon 2 Mar 2026
        result.Date.Should().Be(new DateTime(2026, 3, 2));
    }

    [Fact]
    public void Estimate_Economy_Returns7BusinessDaysFromMonday()
    {
        // Arrange — Monday 23 Feb 2026
        var monday = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = _sut.Estimate("Economy", monday);

        // Assert — 7 business days skipping 1 weekend = Wed 4 Mar 2026
        result.Date.Should().Be(new DateTime(2026, 3, 4));
    }

    [Fact]
    public void Estimate_UnknownServiceType_DefaultsTo5BusinessDays()
    {
        // Arrange
        var monday = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = _sut.Estimate("CustomType", monday);

        // Assert — defaults to 7 days (same as Economy/Standard max)
        var expected = _sut.Estimate("Standard", monday);
        result.Date.Should().Be(expected.Date);
    }

    [Theory]
    [InlineData("overnight")]
    [InlineData("OVERNIGHT")]
    [InlineData("Overnight")]
    public void Estimate_IsCaseInsensitive(string serviceType)
    {
        var monday = new DateTimeOffset(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);
        var result = _sut.Estimate(serviceType, monday);

        result.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
    }
}
