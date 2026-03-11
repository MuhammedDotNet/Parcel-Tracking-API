using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.UnitTests.DTOs;

public class ParcelSearchParamsPropertyTests
{
    // Feature: parcel-search-filter-pagination, Property 9: Page size clamping preserves valid range
    // Validates: Requirements 6.2, 6.3, 6.4
    [Property(MaxTest = 100)]
    public void ClampPageSize_AnyInput_ReturnsValueBetween1And100(int requestedPageSize)
    {
        // Act
        var clampedPageSize = ParcelSearchParams.ClampPageSize(requestedPageSize);

        // Assert - For any input, the clamped value must be between 1 and 100 inclusive
        clampedPageSize.Should().BeInRange(1, 100);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-100, 1)]
    [InlineData(int.MinValue, 1)]
    public void ClampPageSize_BelowMinimum_Returns1(int requestedPageSize, int expected)
    {
        // Act
        var clampedPageSize = ParcelSearchParams.ClampPageSize(requestedPageSize);

        // Assert - Requirement 6.2: Values less than 1 clamp to 1
        clampedPageSize.Should().Be(expected);
    }

    [Theory]
    [InlineData(101, 100)]
    [InlineData(200, 100)]
    [InlineData(1000, 100)]
    [InlineData(int.MaxValue, 100)]
    public void ClampPageSize_AboveMaximum_Returns100(int requestedPageSize, int expected)
    {
        // Act
        var clampedPageSize = ParcelSearchParams.ClampPageSize(requestedPageSize);

        // Assert - Requirement 6.3: Values greater than 100 clamp to 100
        clampedPageSize.Should().Be(expected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void ClampPageSize_ValidRange_ReturnsUnchanged(int requestedPageSize)
    {
        // Act
        var clampedPageSize = ParcelSearchParams.ClampPageSize(requestedPageSize);

        // Assert - Requirement 6.4: Valid values (1-100) pass through unchanged
        clampedPageSize.Should().Be(requestedPageSize);
    }

    // Feature: parcel-search-filter-pagination, Property 12: Date range validation rejects invalid ranges
    // Validates: Requirements 10.1, 10.2
    [Property(MaxTest = 100)]
    public void ValidateDateRange_CreatedFromAfterCreatedTo_ReturnsError(
        PositiveInt daysOffset)
    {
        // Arrange - Generate dates where CreatedFrom is after CreatedTo
        var createdTo = DateTimeOffset.UtcNow;
        var createdFrom = createdTo.AddDays(daysOffset.Get); // CreatedFrom is after CreatedTo

        // Act
        var result = ParcelSearchParams.ValidateDateRange(createdFrom, createdTo);

        // Assert - Requirement 10.2: Invalid ranges should return an error
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ErrorMessage.Should().Contain("CreatedFrom");
        result.ErrorMessage.Should().Contain("CreatedTo");
    }

    [Fact]
    public void ValidateDateRange_CreatedFromBeforeCreatedTo_ReturnsValid()
    {
        // Arrange
        var createdFrom = DateTimeOffset.UtcNow.AddDays(-7);
        var createdTo = DateTimeOffset.UtcNow;

        // Act
        var result = ParcelSearchParams.ValidateDateRange(createdFrom, createdTo);

        // Assert - Requirement 10.1: Valid ranges should pass validation
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateDateRange_CreatedFromEqualsCreatedTo_ReturnsValid()
    {
        // Arrange
        var date = DateTimeOffset.UtcNow;

        // Act
        var result = ParcelSearchParams.ValidateDateRange(date, date);

        // Assert - Requirement 10.1: Equal dates should be valid
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateDateRange_OnlyCreatedFromProvided_ReturnsValid()
    {
        // Arrange
        var createdFrom = DateTimeOffset.UtcNow.AddDays(-7);

        // Act
        var result = ParcelSearchParams.ValidateDateRange(createdFrom, null);

        // Assert - Requirement 10.3: Open-ended ranges should be valid
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateDateRange_OnlyCreatedToProvided_ReturnsValid()
    {
        // Arrange
        var createdTo = DateTimeOffset.UtcNow;

        // Act
        var result = ParcelSearchParams.ValidateDateRange(null, createdTo);

        // Assert - Requirement 10.4: Open-ended ranges should be valid
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateDateRange_BothNull_ReturnsValid()
    {
        // Act
        var result = ParcelSearchParams.ValidateDateRange(null, null);

        // Assert - No date range filter should be valid
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }
}
