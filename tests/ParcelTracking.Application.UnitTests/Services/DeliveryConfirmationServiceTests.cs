using FluentAssertions;
using Moq;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.Services;

public class DeliveryConfirmationServiceTests
{
    private readonly Mock<IParcelRepository> _parcelRepoMock;
    private readonly Mock<IDeliveryConfirmationRepository> _confirmationRepoMock;
    private readonly DeliveryConfirmationService _service;

    public DeliveryConfirmationServiceTests()
    {
        _parcelRepoMock = new Mock<IParcelRepository>();
        _confirmationRepoMock = new Mock<IDeliveryConfirmationRepository>();
        _service = new DeliveryConfirmationService(
            _parcelRepoMock.Object,
            _confirmationRepoMock.Object);
    }

    private static Parcel CreateTestParcel(
        ParcelStatus status = ParcelStatus.InTransit,
        DateTimeOffset? estimatedDelivery = null)
    {
        return new Parcel
        {
            Id = 1,
            TrackingNumber = "PKG-TEST-000001",
            Status = status,
            EstimatedDeliveryDate = estimatedDelivery,
            RecipientAddress = new Address
            {
                Id = 10,
                City = "TestCity",
                State = "TS",
                CountryCode = "US",
                Street1 = "123 Test St",
                PostalCode = "00000",
                ContactName = "Test",
                Phone = "555-0000"
            },
            RecipientAddressId = 10,
            ShipperAddressId = 20,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    private static ConfirmDeliveryRequest CreateTestRequest() => new()
    {
        ReceivedBy = "John Doe",
        DeliveryLocation = "Front door",
        DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
    };

    // ─── ConfirmDeliveryAsync Tests ────────────────────────────────────────────

    [Fact]
    public async Task ConfirmDeliveryAsync_HappyPath_Returns201WithCorrectData()
    {
        var parcel = CreateTestParcel();
        var request = CreateTestRequest();

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        result.Should().NotBeNull();
        result.TrackingNumber.Should().Be(parcel.TrackingNumber);
        result.ReceivedBy.Should().Be(request.ReceivedBy);
        result.DeliveryLocation.Should().Be(request.DeliveryLocation);
        result.HasSignature.Should().BeFalse();
        result.DeliveredAt.Should().Be(request.DeliveredAt);
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_WithSignature_HasSignatureIsTrue()
    {
        var parcel = CreateTestParcel();
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Jane Doe",
            DeliveryLocation = "Lobby",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1),
            SignatureImage = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        };

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        result.HasSignature.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_ParcelNotFound_ThrowsKeyNotFoundException()
    {
        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync("PKG-NONEXISTENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Parcel?)null);

        var act = async () => await _service.ConfirmDeliveryAsync(
            "PKG-NONEXISTENT", CreateTestRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*PKG-NONEXISTENT*");
    }

    [Theory]
    [InlineData(ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.Exception)]
    [InlineData(ParcelStatus.Returned)]
    public async Task ConfirmDeliveryAsync_InvalidStatus_ThrowsInvalidOperationException(ParcelStatus status)
    {
        var parcel = CreateTestParcel(status);

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);

        var act = async () => await _service.ConfirmDeliveryAsync(
            parcel.TrackingNumber, CreateTestRequest(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(status.ToString());
        ex.Which.Message.Should().Contain("InTransit");
        ex.Which.Message.Should().Contain("OutForDelivery");
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_DeliveredStatus_ThrowsConflictException()
    {
        var parcel = CreateTestParcel(ParcelStatus.Delivered);

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);

        var act = async () => await _service.ConfirmDeliveryAsync(
            parcel.TrackingNumber, CreateTestRequest(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().StartWith("CONFLICT:");
        ex.Which.Message.Should().Contain(parcel.TrackingNumber);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_DuplicateConfirmation_ThrowsConflictException()
    {
        var parcel = CreateTestParcel();

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _service.ConfirmDeliveryAsync(
            parcel.TrackingNumber, CreateTestRequest(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().StartWith("CONFLICT:");
        ex.Which.Message.Should().Contain(parcel.TrackingNumber);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_UpdatesParcelStatusToDelivered()
    {
        var parcel = CreateTestParcel();
        var request = CreateTestRequest();

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _service.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        parcel.Status.Should().Be(ParcelStatus.Delivered);
        parcel.ActualDeliveryDate.Should().Be(request.DeliveredAt);
        parcel.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_CreatesTrackingEvent()
    {
        var parcel = CreateTestParcel();
        var request = CreateTestRequest();
        TrackingEvent? capturedEvent = null;

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _parcelRepoMock
            .Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => capturedEvent = e)
            .Returns(Task.CompletedTask);

        await _service.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        capturedEvent.Should().NotBeNull();
        capturedEvent!.EventType.Should().Be(EventType.Delivered);
        capturedEvent.Timestamp.Should().Be(request.DeliveredAt);
        capturedEvent.Description.Should().Contain(request.ReceivedBy);
        capturedEvent.Description.Should().Contain(request.DeliveryLocation);
        capturedEvent.LocationCity.Should().Be("TestCity");
        capturedEvent.LocationState.Should().Be("TS");
        capturedEvent.LocationCountry.Should().Be("US");
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_SavesChangesOnce()
    {
        var parcel = CreateTestParcel();

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _service.ConfirmDeliveryAsync(parcel.TrackingNumber, CreateTestRequest(), CancellationToken.None);

        _parcelRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmDeliveryAsync_OutForDelivery_Succeeds()
    {
        var parcel = CreateTestParcel(ParcelStatus.OutForDelivery);
        var request = CreateTestRequest();

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        result.Should().NotBeNull();
        parcel.Status.Should().Be(ParcelStatus.Delivered);
    }

    // ─── GetDeliveryConfirmationAsync Tests ────────────────────────────────────

    [Fact]
    public async Task GetDeliveryConfirmationAsync_HappyPath_ReturnsFullDetails()
    {
        var estimatedDate = DateTimeOffset.UtcNow.AddDays(2);
        var deliveredAt = DateTimeOffset.UtcNow.AddHours(-1);
        var parcel = CreateTestParcel(ParcelStatus.Delivered, estimatedDate);
        var confirmation = new DeliveryConfirmation
        {
            Id = 42,
            ParcelId = parcel.Id,
            ReceivedBy = "John Doe",
            DeliveryLocation = "Front door",
            SignatureImage = "dGVzdA==",
            DeliveredAt = deliveredAt,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.GetByParcelIdAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmation);

        var result = await _service.GetDeliveryConfirmationAsync(parcel.TrackingNumber, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(42);
        result.TrackingNumber.Should().Be(parcel.TrackingNumber);
        result.ReceivedBy.Should().Be("John Doe");
        result.DeliveryLocation.Should().Be("Front door");
        result.SignatureImage.Should().Be("dGVzdA==");
        result.DeliveredAt.Should().Be(deliveredAt);
        result.EstimatedDeliveryDate.Should().Be(estimatedDate);
        result.IsOnTime.Should().BeTrue();
        result.CreatedAt.Should().Be(confirmation.CreatedAt);
    }

    [Fact]
    public async Task GetDeliveryConfirmationAsync_MissingParcel_ReturnsNull()
    {
        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync("PKG-NONEXISTENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Parcel?)null);

        var result = await _service.GetDeliveryConfirmationAsync("PKG-NONEXISTENT", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDeliveryConfirmationAsync_MissingConfirmation_ReturnsNull()
    {
        var parcel = CreateTestParcel();

        _parcelRepoMock
            .Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        _confirmationRepoMock
            .Setup(r => r.GetByParcelIdAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryConfirmation?)null);

        var result = await _service.GetDeliveryConfirmationAsync(parcel.TrackingNumber, CancellationToken.None);

        result.Should().BeNull();
    }

    // ─── CalculateIsOnTime Tests ──────────────────────────────────────────────

    [Fact]
    public void CalculateIsOnTime_DeliveredOnEstimatedDate_ReturnsTrue()
    {
        var estimatedDate = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var deliveredAt = new DateTimeOffset(2026, 3, 1, 18, 0, 0, TimeSpan.Zero);

        DeliveryConfirmationService.CalculateIsOnTime(deliveredAt, estimatedDate).Should().BeTrue();
    }

    [Fact]
    public void CalculateIsOnTime_DeliveredBeforeEstimatedDate_ReturnsTrue()
    {
        var estimatedDate = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
        var deliveredAt = new DateTimeOffset(2026, 3, 3, 10, 0, 0, TimeSpan.Zero);

        DeliveryConfirmationService.CalculateIsOnTime(deliveredAt, estimatedDate).Should().BeTrue();
    }

    [Fact]
    public void CalculateIsOnTime_DeliveredAfterEstimatedDate_ReturnsFalse()
    {
        var estimatedDate = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var deliveredAt = new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

        DeliveryConfirmationService.CalculateIsOnTime(deliveredAt, estimatedDate).Should().BeFalse();
    }

    [Fact]
    public void CalculateIsOnTime_NoEstimatedDate_ReturnsFalse()
    {
        var deliveredAt = DateTimeOffset.UtcNow;

        DeliveryConfirmationService.CalculateIsOnTime(deliveredAt, null).Should().BeFalse();
    }
}
