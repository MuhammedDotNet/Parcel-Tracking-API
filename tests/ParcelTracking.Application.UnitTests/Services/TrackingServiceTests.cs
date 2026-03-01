using FluentAssertions;
using Moq;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Application.Validators;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.Services;

public class TrackingServiceTests
{
    [Fact]
    public async Task AddEventAsync_WhenParcelDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        var repoMock = new Mock<IParcelRepository>();
        var service = new TrackingService(repoMock.Object);

        repoMock.Setup(r => r.ParcelExistsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreateTrackingEventRequest
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = EventType.PickedUp,
            Description = "Test event"
        };

        var act = async () => await service.AddEventAsync(999, request, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Parcel 999 not found.");
    }

    [Fact]
    public void Validation_WhenTimestampIsMissing_ShouldFailValidation()
    {
        var validator = new CreateTrackingEventRequestValidator();
        
        var request = new CreateTrackingEventRequest
        {
            Timestamp = default,
            EventType = EventType.PickedUp,
            Description = "Test event"
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateTrackingEventRequest.Timestamp) &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Validation_WhenEventTypeIsInvalid_ShouldFailValidation()
    {
        var validator = new CreateTrackingEventRequestValidator();
        
        var request = new CreateTrackingEventRequest
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = (EventType)999,
            Description = "Test event"
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateTrackingEventRequest.EventType) &&
            e.ErrorMessage.Contains("valid event type"));
    }

    [Fact]
    public void Validation_WhenDescriptionIsMissing_ShouldFailValidation()
    {
        var validator = new CreateTrackingEventRequestValidator();
        
        var request = new CreateTrackingEventRequest
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = EventType.PickedUp,
            Description = ""
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateTrackingEventRequest.Description) &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public async Task GetHistoryAsync_WhenParcelHasNoEvents_ShouldReturnEmptyArray()
    {
        var repoMock = new Mock<IParcelRepository>();
        var service = new TrackingService(repoMock.Object);
        
        var parcelId = 123;

        repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrackingEvent>());

        var result = await service.GetHistoryAsync(parcelId, null, null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_WhenParcelDoesNotExist_ShouldThrowKeyNotFoundException()
    {
        var repoMock = new Mock<IParcelRepository>();
        var service = new TrackingService(repoMock.Object);

        repoMock.Setup(r => r.ParcelExistsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = async () => await service.GetHistoryAsync(999, null, null, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Parcel 999 not found.");
    }
}
