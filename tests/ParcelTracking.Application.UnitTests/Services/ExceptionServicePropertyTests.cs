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
/// Property-based tests for ExceptionService.
/// Each test validates one or more correctness properties from the design spec.
/// </summary>
public class ExceptionServicePropertyTests
{
    private static Parcel CreateParcel(
        ParcelStatus status = ParcelStatus.InTransit,
        int deliveryAttempts = 0) => new()
        {
            Id = 1,
            TrackingNumber = "PKG-EXC-000001",
            Status = status,
            DeliveryAttempts = deliveryAttempts,
            EstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(5),
            RecipientAddress = new Address
            {
                Id = 10,
                City = "TestCity",
                State = "TS",
                CountryCode = "US",
                Street1 = "1 Test St",
                PostalCode = "11111",
                ContactName = "Test Recipient",
                Phone = "555-1111"
            },
            RecipientAddressId = 10,
            ShipperAddress = new Address
            {
                Id = 20,
                City = "ShipCity",
                State = "SS",
                CountryCode = "US",
                Street1 = "2 Ship St",
                PostalCode = "22222",
                ContactName = "Shipper",
                Phone = "555-2222"
            },
            ShipperAddressId = 20,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };

    private static (ExceptionService Svc, Mock<IParcelRepository> Repo) CreateService()
    {
        var repo = new Mock<IParcelRepository>();
        var svc = new ExceptionService(repo.Object);
        return (svc, repo);
    }

    private static void SetupParcel(Mock<IParcelRepository> repo, Parcel parcel)
    {
        repo.Setup(r => r.GetByIdWithRecipientAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);
    }

    // ──── Property 1: Exception reporting transitions valid statuses to Exception ────
    // Validates: Requirements 1.1, 1.2

    [Theory]
    [InlineData(ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.OutForDelivery)]
    public async Task Property1_ExceptionReporting_TransitionsValidStatuses_ToException(ParcelStatus status)
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(status);
        SetupParcel(repo, parcel);

        var request = new ReportExceptionRequest { Reason = ExceptionReason.RecipientUnavailable };
        await svc.ReportExceptionAsync(parcel.Id, request, CancellationToken.None);

        parcel.Status.Should().Be(ParcelStatus.Exception);
    }

    [Property(MaxTest = 100)]
    public void Property1_PBT_AllValidStatusesTransitionToException(bool useOutForDelivery)
    {
        var status = useOutForDelivery ? ParcelStatus.OutForDelivery : ParcelStatus.InTransit;
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(status);
        SetupParcel(repo, parcel);

        var reasons = Enum.GetValues<ExceptionReason>();
        var reason = reasons[Math.Abs(status.GetHashCode()) % reasons.Length];
        var request = new ReportExceptionRequest { Reason = reason };

        svc.ReportExceptionAsync(parcel.Id, request, CancellationToken.None).GetAwaiter().GetResult();

        parcel.Status.Should().Be(ParcelStatus.Exception);
    }

    // ──── Property 2: Exception reporting increments attempt counter ────
    // Validates: Requirements 1.3, 7.2

    [Property(MaxTest = 100)]
    public void Property2_ExceptionReporting_IncrementsAttemptCounter(byte initialAttempts)
    {
        // Limit to reasonable range
        var attempts = initialAttempts % 10;
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.InTransit, attempts);
        SetupParcel(repo, parcel);

        var request = new ReportExceptionRequest { Reason = ExceptionReason.AddressNotFound };
        svc.ReportExceptionAsync(parcel.Id, request, CancellationToken.None).GetAwaiter().GetResult();

        parcel.DeliveryAttempts.Should().Be(attempts + 1);
    }

    // ──── Property 3: Exception reporting creates tracking event with reason ────
    // Validates: Requirements 1.4, 6.1

    [Theory]
    [InlineData(ExceptionReason.AddressNotFound)]
    [InlineData(ExceptionReason.RecipientUnavailable)]
    [InlineData(ExceptionReason.DamagedPackage)]
    [InlineData(ExceptionReason.WeatherDelay)]
    [InlineData(ExceptionReason.CustomsHold)]
    [InlineData(ExceptionReason.RefusedByRecipient)]
    public async Task Property3_ExceptionReporting_CreatesTrackingEvent_WithReason(ExceptionReason reason)
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.InTransit);
        TrackingEvent? captured = null;

        SetupParcel(repo, parcel);
        repo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var request = new ReportExceptionRequest { Reason = reason };
        await svc.ReportExceptionAsync(parcel.Id, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(EventType.DeliveryAttempted);
        captured.DelayReason.Should().Be(reason.ToString());
    }

    // ──── Property 4: Exception reporting rejects invalid statuses ────
    // Validates: Requirements 1.5

    [Theory]
    [InlineData(ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.Returned)]
    [InlineData(ParcelStatus.Exception)]
    public async Task Property4_ExceptionReporting_RejectsInvalidStatuses(ParcelStatus status)
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(status);
        SetupParcel(repo, parcel);

        var request = new ReportExceptionRequest { Reason = ExceptionReason.WeatherDelay };
        var act = async () => await svc.ReportExceptionAsync(parcel.Id, request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ──── Property 16: Exception tracking events include recipient location ────
    // Validates: Requirements 6.2

    [Fact]
    public async Task Property16_ExceptionTrackingEvents_IncludeRecipientLocation()
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.InTransit);
        TrackingEvent? captured = null;

        SetupParcel(repo, parcel);
        repo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var request = new ReportExceptionRequest { Reason = ExceptionReason.AddressNotFound };
        await svc.ReportExceptionAsync(parcel.Id, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.LocationCity.Should().Be(parcel.RecipientAddress!.City);
        captured.LocationState.Should().Be(parcel.RecipientAddress.State);
        captured.LocationCountry.Should().Be(parcel.RecipientAddress.CountryCode);
    }

    // ──── Property 5: Retry transitions Exception to InTransit when under limit ────
    // Validates: Requirements 3.1

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Property5_Retry_TransitionsToInTransit_WhenUnderLimit(int attempts)
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.Exception, attempts);
        SetupParcel(repo, parcel);

        var request = new RetryDeliveryRequest
        {
            NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3)
        };

        var (result, autoReturned) = await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);

        autoReturned.Should().BeFalse();
        result.Status.Should().Be(ParcelStatus.InTransit);
    }

    // ──── Property 6: Retry updates estimated delivery date ────
    // Validates: Requirements 3.2

    [Fact]
    public async Task Property6_Retry_UpdatesEstimatedDeliveryDate()
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.Exception, 1);
        SetupParcel(repo, parcel);

        var newDate = DateTimeOffset.UtcNow.AddDays(7);
        var request = new RetryDeliveryRequest { NewEstimatedDeliveryDate = newDate };

        var (result, _) = await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);

        result.EstimatedDeliveryDate.Should().Be(newDate);
    }

    // ──── Property 7: Retry creates InTransit tracking event ────
    // Validates: Requirements 3.3, 6.3

    [Fact]
    public async Task Property7_Retry_CreatesInTransitTrackingEvent()
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.Exception, 1);
        TrackingEvent? captured = null;

        SetupParcel(repo, parcel);
        repo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var newDate = DateTimeOffset.UtcNow.AddDays(5);
        var request = new RetryDeliveryRequest { NewEstimatedDeliveryDate = newDate };

        await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(EventType.InTransit);
        captured.Description.Should().Contain("Redelivery scheduled");
        captured.Description.Should().Contain(newDate.ToString("yyyy-MM-dd"));
    }

    // ──── Property 8: Retry rejects non-Exception statuses ────
    // Validates: Requirements 3.4

    [Theory]
    [InlineData(ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.OutForDelivery)]
    [InlineData(ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.Returned)]
    public async Task Property8_Retry_RejectsNonExceptionStatuses(ParcelStatus status)
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(status, 1);
        SetupParcel(repo, parcel);

        var request = new RetryDeliveryRequest
        {
            NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3)
        };

        var act = async () => await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ──── Property 9: Retry rejects past dates ────
    // Validates: Requirements 3.5

    [Fact]
    public async Task Property9_Retry_RejectsPastDates()
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.Exception, 1);
        SetupParcel(repo, parcel);

        var request = new RetryDeliveryRequest
        {
            NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var act = async () => await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ──── Property 10: Retry preserves attempt counter ────
    // Validates: Requirements 7.3

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Property10_Retry_PreservesAttemptCounter(int attempts)
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.Exception, attempts);
        SetupParcel(repo, parcel);

        var request = new RetryDeliveryRequest
        {
            NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3)
        };

        var (result, _) = await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);

        result.DeliveryAttempts.Should().Be(attempts);
    }

    // ──── Property 11: Auto-return creates Returned tracking event ────
    // Validates: Requirements 4.2, 6.4

    [Fact]
    public async Task Property11_AutoReturn_CreatesReturnedTrackingEvent()
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.Exception, 3);
        TrackingEvent? captured = null;

        SetupParcel(repo, parcel);
        repo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var request = new RetryDeliveryRequest
        {
            NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3)
        };

        var (result, autoReturned) = await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);

        autoReturned.Should().BeTrue();
        result.Status.Should().Be(ParcelStatus.Returned);
        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(EventType.Returned);
        captured.Description.Should().Contain("Maximum delivery attempts");
    }

    // ──── Property 12: Auto-return is transactional ────
    // Validates: Requirements 4.4

    [Fact]
    public async Task Property12_AutoReturn_IsTransactional()
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.Exception, 3);
        SetupParcel(repo, parcel);

        var request = new RetryDeliveryRequest
        {
            NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3)
        };

        await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);

        // Both tracking event and save should happen together in a single SaveChangesAsync call
        repo.Verify(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──── Property 13: Exception monitoring returns only Exception status parcels ────
    // Validates: Requirements 5.1, 5.5

    [Fact]
    public async Task Property13_ExceptionMonitoring_ReturnsOnlyExceptionParcels()
    {
        var (svc, repo) = CreateService();
        var exceptionParcels = new List<Parcel>
        {
            CreateParcel(ParcelStatus.Exception, 1),
            CreateParcel(ParcelStatus.Exception, 2)
        };
        exceptionParcels[0].Id = 1;
        exceptionParcels[1].Id = 2;

        repo.Setup(r => r.GetExceptionParcelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(exceptionParcels);

        var result = await svc.GetExceptionParcelsAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.Status == ParcelStatus.Exception);
    }

    // ──── Property 14: Exception monitoring includes address data ────
    // Validates: Requirements 5.2

    [Fact]
    public async Task Property14_ExceptionMonitoring_IncludesAddressData()
    {
        var (svc, repo) = CreateService();
        var exceptionParcels = new List<Parcel> { CreateParcel(ParcelStatus.Exception, 1) };

        repo.Setup(r => r.GetExceptionParcelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(exceptionParcels);

        var result = await svc.GetExceptionParcelsAsync(CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].ShipperAddress.Should().NotBeNull();
        result[0].RecipientAddress.Should().NotBeNull();
        result[0].ShipperAddress.City.Should().Be("ShipCity");
        result[0].RecipientAddress.City.Should().Be("TestCity");
    }

    // ──── Property 15: Exception monitoring orders by oldest first ────
    // Validates: Requirements 5.3

    [Fact]
    public async Task Property15_ExceptionMonitoring_OrdersByOldestFirst()
    {
        var (svc, repo) = CreateService();
        var older = CreateParcel(ParcelStatus.Exception, 1);
        older.Id = 1;
        older.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-5);

        var newer = CreateParcel(ParcelStatus.Exception, 2);
        newer.Id = 2;
        newer.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        // Repository returns them already ordered (as per implementation)
        repo.Setup(r => r.GetExceptionParcelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Parcel> { older, newer });

        var result = await svc.GetExceptionParcelsAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].UpdatedAt.Should().BeBefore(result[1].UpdatedAt);
    }

    // ──── Property 17: Tracking events use UTC timestamps ────
    // Validates: Requirements 6.5

    [Fact]
    public async Task Property17_TrackingEvents_UseUtcTimestamps()
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.InTransit);
        TrackingEvent? captured = null;

        SetupParcel(repo, parcel);
        repo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var request = new ReportExceptionRequest { Reason = ExceptionReason.WeatherDelay };
        var before = DateTimeOffset.UtcNow;
        await svc.ReportExceptionAsync(parcel.Id, request, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        captured.Should().NotBeNull();
        captured!.Timestamp.Offset.Should().Be(TimeSpan.Zero, "timestamp should be UTC");
        captured.Timestamp.Should().BeOnOrAfter(before);
        captured.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task Property17_RetryTrackingEvents_UseUtcTimestamps()
    {
        var (svc, repo) = CreateService();
        var parcel = CreateParcel(ParcelStatus.Exception, 1);
        TrackingEvent? captured = null;

        SetupParcel(repo, parcel);
        repo.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var request = new RetryDeliveryRequest
        {
            NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(5)
        };

        var before = DateTimeOffset.UtcNow;
        await svc.RetryDeliveryAsync(parcel.Id, request, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        captured.Should().NotBeNull();
        captured!.Timestamp.Offset.Should().Be(TimeSpan.Zero, "timestamp should be UTC");
        captured.Timestamp.Should().BeOnOrAfter(before);
        captured.Timestamp.Should().BeOnOrBefore(after);
    }

    // ──── Additional edge case: Parcel not found ────

    [Fact]
    public async Task ReportException_ParcelNotFound_ThrowsKeyNotFoundException()
    {
        var (svc, repo) = CreateService();
        repo.Setup(r => r.GetByIdWithRecipientAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Parcel?)null);

        var request = new ReportExceptionRequest { Reason = ExceptionReason.AddressNotFound };
        var act = async () => await svc.ReportExceptionAsync(999, request, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RetryDelivery_ParcelNotFound_ThrowsKeyNotFoundException()
    {
        var (svc, repo) = CreateService();
        repo.Setup(r => r.GetByIdWithRecipientAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Parcel?)null);

        var request = new RetryDeliveryRequest
        {
            NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3)
        };
        var act = async () => await svc.RetryDeliveryAsync(999, request, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ──── Empty exception list ────

    [Fact]
    public async Task GetExceptionParcels_WhenNoExceptions_ReturnsEmptyList()
    {
        var (svc, repo) = CreateService();
        repo.Setup(r => r.GetExceptionParcelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Parcel>());

        var result = await svc.GetExceptionParcelsAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }
}
