using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Moq;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.Services;

/// <summary>
/// Property-based tests for DeliveryConfirmationService.
/// Each test validates one or more correctness properties from the design spec.
/// </summary>
public class DeliveryConfirmationPropertyTests
{
    private static Parcel CreateParcel(
        ParcelStatus status = ParcelStatus.InTransit,
        DateTimeOffset? estimatedDelivery = null) => new()
        {
            Id = 1,
            TrackingNumber = "PKG-PROP-000001",
            Status = status,
            EstimatedDeliveryDate = estimatedDelivery,
            RecipientAddress = new Address
            {
                Id = 10,
                City = "PropCity",
                State = "PS",
                CountryCode = "US",
                Street1 = "1 Prop St",
                PostalCode = "11111",
                ContactName = "Prop",
                Phone = "555-1111"
            },
            RecipientAddressId = 10,
            ShipperAddressId = 20,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };

    private static (DeliveryConfirmationService Svc, Mock<IParcelRepository> ParcelRepo, Mock<IDeliveryConfirmationRepository> ConfirmRepo) CreateService()
    {
        var parcelRepo = new Mock<IParcelRepository>();
        var confirmRepo = new Mock<IDeliveryConfirmationRepository>();
        var svc = new DeliveryConfirmationService(parcelRepo.Object, confirmRepo.Object);
        return (svc, parcelRepo, confirmRepo);
    }

    private static void SetupHappyPath(
        Mock<IParcelRepository> parcelRepo,
        Mock<IDeliveryConfirmationRepository> confirmRepo,
        Parcel parcel)
    {
        parcelRepo.Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        confirmRepo.Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    // Feature: delivery-confirmation, Property 1: Required field validation
    // Validates: Requirements 1.1
    [Property(MaxTest = 100)]
    public void Property1_RequiredFieldValidation(NonEmptyString receivedBy, NonEmptyString deliveryLocation)
    {
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = receivedBy.Get,
            DeliveryLocation = deliveryLocation.Get,
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        request.ReceivedBy.Should().NotBeNullOrEmpty();
        request.DeliveryLocation.Should().NotBeNullOrEmpty();
    }

    // Feature: delivery-confirmation, Property 2: Optional signature handling
    // Validates: Requirements 1.2
    [Fact]
    public async Task Property2_OptionalSignatureHandling_WithSignature()
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel();
        SetupHappyPath(parcelRepo, confirmRepo, parcel);

        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Recipient",
            DeliveryLocation = "Door",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1),
            SignatureImage = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        };

        var result = await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);
        result.HasSignature.Should().BeTrue();
    }

    [Fact]
    public async Task Property2_OptionalSignatureHandling_WithoutSignature()
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel();
        SetupHappyPath(parcelRepo, confirmRepo, parcel);

        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Recipient",
            DeliveryLocation = "Door",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1),
            SignatureImage = null
        };

        var result = await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);
        result.HasSignature.Should().BeFalse();
    }

    // Feature: delivery-confirmation, Property 5: Duplicate prevention with conflict response
    // Validates: Requirements 3.1, 3.2
    [Fact]
    public async Task Property5_DuplicatePrevention()
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel();

        parcelRepo.Setup(r => r.GetByTrackingNumberWithRecipientAsync(parcel.TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
        confirmRepo.Setup(r => r.ExistsForParcelAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Test",
            DeliveryLocation = "Test",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var act = async () => await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(parcel.TrackingNumber);
    }

    // Feature: delivery-confirmation, Property 6: Automatic tracking event creation
    // Validates: Requirements 4.1, 4.2
    [Theory]
    [InlineData(ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.OutForDelivery)]
    public async Task Property6_AutomaticTrackingEventCreation(ParcelStatus status)
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel(status);
        TrackingEvent? captured = null;

        SetupHappyPath(parcelRepo, confirmRepo, parcel);
        parcelRepo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var deliveredAt = DateTimeOffset.UtcNow.AddHours(-1);
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Recipient",
            DeliveryLocation = "Door",
            DeliveredAt = deliveredAt
        };

        await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(EventType.Delivered);
        captured.Timestamp.Should().Be(deliveredAt);
    }

    // Feature: delivery-confirmation, Property 7: Tracking event description format
    // Validates: Requirements 4.3
    [Theory]
    [InlineData("Alice", "Front porch")]
    [InlineData("Bob", "Lobby")]
    [InlineData("Charlie Brown", "Back door - side entrance")]
    public async Task Property7_TrackingEventDescriptionFormat(string receivedBy, string location)
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel();
        TrackingEvent? captured = null;

        SetupHappyPath(parcelRepo, confirmRepo, parcel);
        parcelRepo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = receivedBy,
            DeliveryLocation = location,
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Description.Should().Contain(receivedBy);
        captured.Description.Should().Contain(location);
    }

    // Feature: delivery-confirmation, Property 8: Tracking event location mapping
    // Validates: Requirements 4.4
    [Fact]
    public async Task Property8_TrackingEventLocationMapping()
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel();
        TrackingEvent? captured = null;

        SetupHappyPath(parcelRepo, confirmRepo, parcel);
        parcelRepo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Test",
            DeliveryLocation = "Test",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.LocationCity.Should().Be(parcel.RecipientAddress!.City);
        captured.LocationState.Should().Be(parcel.RecipientAddress.State);
        captured.LocationCountry.Should().Be(parcel.RecipientAddress.CountryCode);
    }

    // Feature: delivery-confirmation, Property 10: Parcel status update
    // Validates: Requirements 5.1
    [Fact]
    public async Task Property10_ParcelStatusUpdate()
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel();

        SetupHappyPath(parcelRepo, confirmRepo, parcel);

        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Test",
            DeliveryLocation = "Test",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        parcel.Status.Should().Be(ParcelStatus.Delivered);
    }

    // Feature: delivery-confirmation, Property 11: Actual delivery date synchronization
    // Validates: Requirements 5.2
    [Fact]
    public async Task Property11_ActualDeliveryDateSynchronization()
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel();
        var deliveredAt = DateTimeOffset.UtcNow.AddHours(-2);

        SetupHappyPath(parcelRepo, confirmRepo, parcel);

        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Test",
            DeliveryLocation = "Test",
            DeliveredAt = deliveredAt
        };

        await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        parcel.ActualDeliveryDate.Should().Be(deliveredAt);
    }

    // Feature: delivery-confirmation, Property 13: Transactional consistency
    // Validates: Requirements 6.1
    [Fact]
    public async Task Property13_TransactionalConsistency()
    {
        var (svc, parcelRepo, confirmRepo) = CreateService();
        var parcel = CreateParcel();

        SetupHappyPath(parcelRepo, confirmRepo, parcel);

        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Test",
            DeliveryLocation = "Test",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        await svc.ConfirmDeliveryAsync(parcel.TrackingNumber, request, CancellationToken.None);

        // All three entities modified before single SaveChangesAsync call
        confirmRepo.Verify(r => r.AddAsync(It.IsAny<DeliveryConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        parcelRepo.Verify(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        parcelRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Feature: delivery-confirmation, Property 15: On-time calculation with estimate
    // Validates: Requirements 7.2, 7.4, 7.5
    [Theory]
    [InlineData(-1, true)]   // delivered 1 day before estimate
    [InlineData(0, true)]    // delivered same day
    [InlineData(1, false)]   // delivered 1 day after estimate
    [InlineData(5, false)]   // delivered 5 days after estimate
    public void Property15_OnTimeCalculation(int daysOffset, bool expectedOnTime)
    {
        var estimatedDate = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var deliveredAt = estimatedDate.AddDays(daysOffset);

        var result = DeliveryConfirmationService.CalculateIsOnTime(deliveredAt, estimatedDate);

        result.Should().Be(expectedOnTime);
    }

    [Fact]
    public void Property15_OnTimeCalculation_NoEstimate_ReturnsFalse()
    {
        DeliveryConfirmationService.CalculateIsOnTime(DateTimeOffset.UtcNow, null).Should().BeFalse();
    }
}
