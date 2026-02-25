using FluentAssertions;
using Moq;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.Services;

public class ParcelRetrievalServiceTests
{
    private readonly Mock<IParcelRepository> _repoMock = new();
    private readonly ParcelRetrievalService _sut;

    public ParcelRetrievalServiceTests()
    {
        _sut = new ParcelRetrievalService(_repoMock.Object);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Parcel CreateParcel(
        ParcelStatus status = ParcelStatus.InTransit,
        DateTimeOffset? actualDeliveryDate = null)
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return new Parcel
        {
            Id = 1,
            TrackingNumber = "PKG-20260101-ABCDEF",
            Status = status,
            Description = "Test parcel",
            Weight = 2.5m,
            WeightUnit = WeightUnit.Kg,
            CreatedAt = createdAt,
            ActualDeliveryDate = actualDeliveryDate,
            ShipperAddress = new Address
            {
                Id = 10,
                Street1 = "1 Shipper St",
                City = "ShipCity",
                State = "SC",
                PostalCode = "10001",
                CountryCode = "US",
                ContactName = "Shipper Corp",
                Phone = "+1-555-0100"
            },
            RecipientAddress = new Address
            {
                Id = 20,
                Street1 = "2 Recipient Ave",
                City = "RecCity",
                State = "RC",
                PostalCode = "20002",
                CountryCode = "US",
                ContactName = "Jane Doe",
                Phone = "+1-555-0200"
            },
            ContentItems = new List<ParcelContentItem>
            {
                new()
                {
                    Id = 100,
                    HsCode = "8471.30",
                    Description = "Laptop",
                    Quantity = 1,
                    UnitValue = 1200m,
                    Currency = "USD",
                    Weight = 2.1m,
                    WeightUnit = WeightUnit.Kg,
                    CountryOfOrigin = "CN"
                }
            }
        };
    }

    // ── GetByIdAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(99, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Parcel?)null);

        var result = await _sut.GetByIdAsync(99, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_MapsAllFields()
    {
        var parcel = CreateParcel();
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(parcel);

        var result = await _sut.GetByIdAsync(1, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.TrackingNumber.Should().Be("PKG-20260101-ABCDEF");
        result.Status.Should().Be("InTransit");
        result.Weight.Should().Be(2.5m);
        result.WeightUnit.Should().Be("Kg");
        result.Description.Should().Be("Test parcel");

        result.ShipperAddress.Should().NotBeNull();
        result.ShipperAddress.Id.Should().Be(10);
        result.RecipientAddress.Should().NotBeNull();
        result.RecipientAddress.Id.Should().Be(20);

        result.ContentItems.Should().HaveCount(1);
        result.ContentItems[0].HsCode.Should().Be("8471.30");
    }

    [Fact]
    public async Task GetByIdAsync_WhenDelivered_DaysInTransitIsFixed()
    {
        var delivered = new DateTimeOffset(2026, 1, 4, 0, 0, 0, TimeSpan.Zero);
        var parcel = CreateParcel(ParcelStatus.Delivered, delivered);
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(parcel);

        var result = await _sut.GetByIdAsync(1, CancellationToken.None);

        result!.DaysInTransit.Should().Be(3);
        result.IsDelivered.Should().BeTrue();
        result.DeliveredAt.Should().Be(delivered);
    }

    [Fact]
    public async Task GetByIdAsync_WhenInTransit_IsDeliveredIsFalse()
    {
        var parcel = CreateParcel(ParcelStatus.InTransit);
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(parcel);

        var result = await _sut.GetByIdAsync(1, CancellationToken.None);

        result!.IsDelivered.Should().BeFalse();
        result.DeliveredAt.Should().BeNull();
        result.DaysInTransit.Should().BeGreaterThanOrEqualTo(0);
    }

    // ── GetByTrackingNumberAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetByTrackingNumberAsync_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByTrackingNumberWithRecipientAsync(
                     "PKG-INVALID", It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Parcel?)null);

        var result = await _sut.GetByTrackingNumberAsync("PKG-INVALID", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTrackingNumberAsync_WhenFound_MapsPublicFieldsOnly()
    {
        var parcel = CreateParcel();
        _repoMock.Setup(r => r.GetByTrackingNumberWithRecipientAsync(
                     parcel.TrackingNumber, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(parcel);

        var result = await _sut.GetByTrackingNumberAsync(
            parcel.TrackingNumber, CancellationToken.None);

        result.Should().NotBeNull();
        result!.TrackingNumber.Should().Be("PKG-20260101-ABCDEF");
        result.Status.Should().Be("InTransit");
        result.RecipientCity.Should().Be("RecCity");
        result.RecipientState.Should().Be("RC");
        result.Weight.Should().Be(2.5m);
        result.ShippedAt.Should().Be(parcel.CreatedAt);
        result.IsDelivered.Should().BeFalse();
    }
}
