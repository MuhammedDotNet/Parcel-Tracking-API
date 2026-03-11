using FluentAssertions;
using ParcelTracking.Infrastructure.Services;
using System.Text.RegularExpressions;

namespace ParcelTracking.Application.UnitTests.Services;

public class TrackingNumberGeneratorTests
{
    private readonly TrackingNumberGenerator _sut = new();

    [Fact]
    public void Generate_ReturnsStringStartingWithPKG()
    {
        var result = _sut.Generate();

        result.Should().StartWith("PKG-");
    }

    [Fact]
    public void Generate_ContainsDateSegment()
    {
        var expectedDate = DateTimeOffset.UtcNow.ToString("yyyyMMdd");

        var result = _sut.Generate();

        result.Should().Contain(expectedDate);
    }

    [Fact]
    public void Generate_RandomPartIs6UpperAlphanumeric()
    {
        var result = _sut.Generate();
        // Format: PKG-YYYYMMDD-XXXXXX
        var parts = result.Split('-');
        parts.Should().HaveCount(3);

        var random = parts[2];
        random.Should().HaveLength(6);
        Regex.IsMatch(random, @"^[A-Z0-9]{6}$").Should().BeTrue(
            because: "the random segment must contain only uppercase letters and digits");
    }

    [Fact]
    public void Generate_TwoConsecutiveCalls_AreVeryLikelyDifferent()
    {
        // Probabilistic test — same result would mean an ≈1/36^6 collision
        var first  = _sut.Generate();
        var second = _sut.Generate();

        // Allow the test to acknowledge the theoretical collision possibility
        // but assert they are different (overwhelmingly likely in practice)
        first.Should().NotBe(second,
            because: "two independently generated tracking numbers should be unique");
    }
}
