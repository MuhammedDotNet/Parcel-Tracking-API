using Bogus;
using FluentAssertions;
using Moq;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.Services;

public class TrackingServicePropertyTests
{
    private static readonly ParcelStatus[] NonTerminalStatuses =
    {
        ParcelStatus.LabelCreated,
        ParcelStatus.PickedUp,
        ParcelStatus.InTransit,
        ParcelStatus.OutForDelivery,
        ParcelStatus.Exception
    };

    private static readonly EventType[] NonTerminalEventTypes =
    {
        EventType.PickedUp,
        EventType.DepartedFacility,
        EventType.ArrivedAtFacility,
        EventType.InTransit,
        EventType.OutForDelivery,
        EventType.DeliveryAttempted,
        EventType.Exception
    };

    /// <summary>
    /// Returns a valid parcel status from which the given event type can transition.
    /// This is necessary because TrackingService now validates transitions through ParcelStatusRules.
    /// </summary>
    private static ParcelStatus GetValidStatusForEventType(EventType eventType) => eventType switch
    {
        EventType.PickedUp => ParcelStatus.LabelCreated,
        EventType.DepartedFacility or EventType.ArrivedAtFacility or EventType.InTransit => ParcelStatus.PickedUp,
        EventType.OutForDelivery or EventType.DeliveryAttempted => ParcelStatus.InTransit,
        EventType.Delivered => ParcelStatus.OutForDelivery,
        EventType.Exception => ParcelStatus.InTransit,
        EventType.Returned => ParcelStatus.Exception,
        _ => ParcelStatus.InTransit
    };

    // Feature: tracking-events-history, Property 1: Event persistence round-trip
    // Validates: Requirements 1.1, 1.3, 1.4
    [Fact]
    public async Task Property_EventPersistenceRoundTrip_ValidatesDataPreservation()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);

            var parcelId = faker.Random.Int(1, 1000);

            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackingEvent?)null);

            var eventType = faker.PickRandom<EventType>();

            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = GetValidStatusForEventType(eventType),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            var timestamp = faker.Date.RecentOffset(days: 10);
            var description = faker.Lorem.Sentence();
            var locationCity = faker.Random.Bool() ? faker.Address.City() : null;
            var locationState = faker.Random.Bool() ? faker.Address.State() : null;
            var locationCountry = faker.Random.Bool() ? faker.Address.Country() : null;
            var delayReason = faker.Random.Bool() ? faker.Lorem.Sentence() : null;

            TrackingEvent? capturedEvent = null;
            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) =>
                {
                    evt.Id = faker.Random.Int(1, 10000);
                    capturedEvent = evt;
                })
                .Returns(Task.CompletedTask);

            repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var request = new CreateTrackingEventRequest
            {
                Timestamp = timestamp,
                EventType = eventType,
                Description = description,
                LocationCity = locationCity,
                LocationState = locationState,
                LocationCountry = locationCountry,
                DelayReason = delayReason
            };

            var response = await service.AddEventAsync(parcelId, request, CancellationToken.None);

            response.Should().NotBeNull();
            response.ParcelId.Should().Be(parcelId);
            response.Timestamp.Should().Be(timestamp);
            response.EventType.Should().Be(eventType.ToString());
            response.Description.Should().Be(description);
            response.LocationCity.Should().Be(locationCity);
            response.LocationState.Should().Be(locationState);
            response.LocationCountry.Should().Be(locationCountry);
            response.DelayReason.Should().Be(delayReason);

            capturedEvent.Should().NotBeNull();
            capturedEvent!.ParcelId.Should().Be(parcelId);
            capturedEvent.Timestamp.Should().Be(timestamp);
            capturedEvent.EventType.Should().Be(eventType);
            capturedEvent.Description.Should().Be(description);
        }
    }

    // Feature: tracking-events-history, Property 2: Unique event identifiers
    // Validates: Requirements 1.2
    [Fact]
    public async Task Property_UniqueEventIdentifiers_ValidatesIdUniqueness()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);

            var parcelId = faker.Random.Int(1, 1000);

            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackingEvent?)null);

            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = GetValidStatusForEventType(faker.PickRandom(NonTerminalEventTypes)),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            var eventCount = faker.Random.Int(2, 10);
            var eventIds = new HashSet<int>();
            var usedIds = new HashSet<int>();

            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) =>
                {
                    int newId;
                    do
                    {
                        newId = faker.Random.Int(1, 100000);
                    } while (usedIds.Contains(newId));

                    usedIds.Add(newId);
                    evt.Id = newId;
                })
                .Returns(Task.CompletedTask);

            repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var baseTimestamp = faker.Date.PastOffset(30);

            for (int j = 0; j < eventCount; j++)
            {
                var request = new CreateTrackingEventRequest
                {
                    Timestamp = baseTimestamp.AddMinutes(j * 10),
                    EventType = faker.PickRandom(NonTerminalEventTypes),
                    Description = faker.Lorem.Sentence(),
                    LocationCity = faker.Address.City(),
                    LocationState = faker.Address.State(),
                    LocationCountry = faker.Address.Country()
                };

                // Update parcel status to a valid predecessor for the next event
                parcel.Status = GetValidStatusForEventType(request.EventType);

                var response = await service.AddEventAsync(parcelId, request, CancellationToken.None);
                eventIds.Add(response.Id);
            }

            eventIds.Count.Should().Be(eventCount,
                $"all {eventCount} events should have unique IDs");
        }
    }

    // Feature: tracking-events-history, Property 3: All event types supported
    // Validates: Requirements 1.5
    [Fact]
    public async Task Property_AllEventTypesSupported_ValidatesAllEnumValues()
    {
        const int iterations = 100;
        var faker = new Faker();
        var allEventTypes = Enum.GetValues<EventType>();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);

            var parcelId = faker.Random.Int(1, 1000);

            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackingEvent?)null);

            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = GetValidStatusForEventType(faker.PickRandom<EventType>()),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) =>
                {
                    evt.Id = faker.Random.Int(1, 10000);
                })
                .Returns(Task.CompletedTask);

            repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var eventType = faker.PickRandom(allEventTypes);

            var request = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 10),
                EventType = eventType,
                Description = faker.Lorem.Sentence(),
                LocationCity = faker.Address.City(),
                LocationState = faker.Address.State(),
                LocationCountry = faker.Address.Country()
            };

            // Set parcel to a valid predecessor state for the event
            parcel.Status = GetValidStatusForEventType(eventType);

            var response = await service.AddEventAsync(parcelId, request, CancellationToken.None);

            response.Should().NotBeNull();
            response.EventType.Should().Be(eventType.ToString(),
                $"event type {eventType} should be supported and preserved");
        }
    }

    // Feature: tracking-events-history, Property 4: Chronological ordering enforcement
    // Validates: Requirements 3.1
    [Fact]
    public async Task Property_ChronologicalOrderingEnforcement_ValidatesTimestampOrdering()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);

            var parcelId = faker.Random.Int(1, 1000);

            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = GetValidStatusForEventType(faker.PickRandom(NonTerminalEventTypes)),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hasExistingEvents = faker.Random.Bool();
            TrackingEvent? latestEvent = null;

            if (hasExistingEvents)
            {
                var latestTimestamp = faker.Date.RecentOffset(days: 10);
                latestEvent = new TrackingEvent
                {
                    Id = faker.Random.Int(1, 1000),
                    ParcelId = parcelId,
                    Timestamp = latestTimestamp,
                    EventType = faker.PickRandom(NonTerminalEventTypes),
                    Description = faker.Lorem.Sentence()
                };
            }

            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(latestEvent);

            if (latestEvent == null)
            {
                var firstEventType = faker.PickRandom(NonTerminalEventTypes);
                var firstEventRequest = new CreateTrackingEventRequest
                {
                    Timestamp = faker.Date.RecentOffset(days: 1),
                    EventType = firstEventType,
                    Description = faker.Lorem.Sentence()
                };

                // Set parcel to a valid state for the event type
                parcel.Status = GetValidStatusForEventType(firstEventType);

                var result = await service.AddEventAsync(parcelId, firstEventRequest, CancellationToken.None);
                result.Should().NotBeNull();
            }
            else
            {
                var validTimestamp = latestEvent.Timestamp.AddMinutes(faker.Random.Int(1, 1000));
                var validRequest = new CreateTrackingEventRequest
                {
                    Timestamp = validTimestamp,
                    EventType = faker.PickRandom(NonTerminalEventTypes),
                    Description = faker.Lorem.Sentence()
                };

                // Set parcel to a valid state for the event type
                parcel.Status = GetValidStatusForEventType(validRequest.EventType);

                var result = await service.AddEventAsync(parcelId, validRequest, CancellationToken.None);
                result.Should().NotBeNull();

                var invalidTimestamp = latestEvent.Timestamp.AddMinutes(-faker.Random.Int(1, 1000));
                var invalidEventType = faker.PickRandom(NonTerminalEventTypes);
                var invalidRequest = new CreateTrackingEventRequest
                {
                    Timestamp = invalidTimestamp,
                    EventType = invalidEventType,
                    Description = faker.Lorem.Sentence()
                };

                // Set parcel to a valid state for the event type (the exception should come from timestamp, not status)
                parcel.Status = GetValidStatusForEventType(invalidEventType);

                var invalidAct = async () => await service.AddEventAsync(parcelId, invalidRequest, CancellationToken.None);
                await invalidAct.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage($"Event timestamp {invalidTimestamp} is earlier than the most recent event at {latestEvent.Timestamp}.");

                var equalEventType = faker.PickRandom(NonTerminalEventTypes);
                var equalRequest = new CreateTrackingEventRequest
                {
                    Timestamp = latestEvent.Timestamp,
                    EventType = equalEventType,
                    Description = faker.Lorem.Sentence()
                };

                // Set parcel to a valid state for the event type
                parcel.Status = GetValidStatusForEventType(equalEventType);

                var equalResult = await service.AddEventAsync(parcelId, equalRequest, CancellationToken.None);
                equalResult.Should().NotBeNull();
            }
        }
    }

    // Feature: tracking-events-history, Property 5: Status synchronization mapping
    // Validates: Requirements 4.1
    [Fact]
    public async Task Property_StatusSynchronizationMapping_ValidatesEventTypeToStatusMapping()
    {
        const int iterations = 100;
        var faker = new Faker();

        var expectedMappings = new Dictionary<EventType, ParcelStatus>
        {
            { EventType.PickedUp, ParcelStatus.PickedUp },
            { EventType.DepartedFacility, ParcelStatus.InTransit },
            { EventType.ArrivedAtFacility, ParcelStatus.InTransit },
            { EventType.InTransit, ParcelStatus.InTransit },
            { EventType.OutForDelivery, ParcelStatus.OutForDelivery },
            { EventType.DeliveryAttempted, ParcelStatus.OutForDelivery },
            { EventType.Delivered, ParcelStatus.Delivered },
            { EventType.Exception, ParcelStatus.Exception },
            { EventType.Returned, ParcelStatus.Returned }
        };

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);

            var parcelId = faker.Random.Int(1, 1000);

            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackingEvent?)null);

            var eventType = faker.PickRandom(expectedMappings.Keys.ToList());
            var expectedStatus = expectedMappings[eventType];

            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = GetValidStatusForEventType(eventType),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            TrackingEvent? capturedEvent = null;
            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) => capturedEvent = evt)
                .Returns(Task.CompletedTask);

            var saveChangesCalled = false;
            repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => saveChangesCalled = true)
                .Returns(Task.CompletedTask);

            var request = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = eventType,
                Description = faker.Lorem.Sentence(),
                LocationCity = faker.Address.City(),
                LocationState = faker.Address.State(),
                LocationCountry = faker.Address.Country(),
                DelayReason = faker.Random.Bool() ? faker.Lorem.Sentence() : null
            };

            var response = await service.AddEventAsync(parcelId, request, CancellationToken.None);

            parcel.Status.Should().Be(expectedStatus,
                $"EventType {eventType} should map to ParcelStatus {expectedStatus}");

            capturedEvent.Should().NotBeNull();
            capturedEvent!.EventType.Should().Be(eventType);
            capturedEvent.ParcelId.Should().Be(parcelId);

            saveChangesCalled.Should().BeTrue("changes should be saved atomically");

            response.Should().NotBeNull();
            response.ParcelId.Should().Be(parcelId);
            response.EventType.Should().Be(eventType.ToString());
            response.Description.Should().Be(request.Description);
        }
    }
}



