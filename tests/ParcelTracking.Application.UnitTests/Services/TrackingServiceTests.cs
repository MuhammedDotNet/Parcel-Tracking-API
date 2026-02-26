using Bogus;
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
    // Feature: tracking-events-history, Property 1: Event persistence round-trip
    // Validates: Requirements 1.1, 1.3, 1.4
    [Fact]
    public async Task Property_EventPersistenceRoundTrip_ValidatesDataPreservation()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // No existing events (to avoid chronological validation issues)
            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackingEvent?)null);

            // Create a parcel for status updates
            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            // Generate random tracking event data
            var timestamp = faker.Date.RecentOffset(days: 10);
            var eventType = faker.PickRandom<EventType>();
            var description = faker.Lorem.Sentence();
            var locationCity = faker.Random.Bool() ? faker.Address.City() : null;
            var locationState = faker.Random.Bool() ? faker.Address.State() : null;
            var locationCountry = faker.Random.Bool() ? faker.Address.Country() : null;
            var delayReason = faker.Random.Bool() ? faker.Lorem.Sentence() : null;

            // Capture the tracking event that gets added
            TrackingEvent? capturedEvent = null;
            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) => 
                {
                    // Simulate database assigning an ID
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

            // Execute the method
            var response = await service.AddEventAsync(parcelId, request, CancellationToken.None);

            // Verify round-trip: all data from request should be preserved in response
            response.Should().NotBeNull();
            response.ParcelId.Should().Be(parcelId, "parcel ID should be preserved");
            response.Timestamp.Should().Be(timestamp, "timestamp should be preserved");
            response.EventType.Should().Be(eventType.ToString(), "event type should be preserved");
            response.Description.Should().Be(description, "description should be preserved");
            response.LocationCity.Should().Be(locationCity, "location city should be preserved");
            response.LocationState.Should().Be(locationState, "location state should be preserved");
            response.LocationCountry.Should().Be(locationCountry, "location country should be preserved");
            response.DelayReason.Should().Be(delayReason, "delay reason should be preserved");
            
            // Verify the event was persisted with correct data
            capturedEvent.Should().NotBeNull();
            capturedEvent!.ParcelId.Should().Be(parcelId);
            capturedEvent.Timestamp.Should().Be(timestamp);
            capturedEvent.EventType.Should().Be(eventType);
            capturedEvent.Description.Should().Be(description);
            capturedEvent.LocationCity.Should().Be(locationCity);
            capturedEvent.LocationState.Should().Be(locationState);
            capturedEvent.LocationCountry.Should().Be(locationCountry);
            capturedEvent.DelayReason.Should().Be(delayReason);
        }
    }

    // Feature: tracking-events-history, Property 2: Unique event identifiers
    // Validates: Requirements 1.2
    [Fact]
    public async Task Property_UniqueEventIdentifiers_ValidatesIdUniqueness()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // No existing events (to avoid chronological validation issues)
            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackingEvent?)null);

            // Create a parcel for status updates
            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            // Generate a random number of events (between 2 and 10)
            var eventCount = faker.Random.Int(2, 10);
            var eventIds = new HashSet<int>();
            var usedIds = new HashSet<int>();

            // Setup to simulate database assigning unique IDs
            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) => 
                {
                    // Simulate database assigning a unique ID
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

            // Create multiple events and collect their IDs
            var baseTimestamp = faker.Date.PastOffset(30);
            
            for (int j = 0; j < eventCount; j++)
            {
                var request = new CreateTrackingEventRequest
                {
                    Timestamp = baseTimestamp.AddMinutes(j * 10), // Ensure chronological order
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence(),
                    LocationCity = faker.Address.City(),
                    LocationState = faker.Address.State(),
                    LocationCountry = faker.Address.Country()
                };

                var response = await service.AddEventAsync(parcelId, request, CancellationToken.None);
                
                // Collect the event ID
                eventIds.Add(response.Id);
            }

            // Verify all IDs are unique
            eventIds.Count.Should().Be(eventCount, 
                $"all {eventCount} events should have unique IDs");
        }
    }

    // Feature: tracking-events-history, Property 3: All event types supported
    // Validates: Requirements 1.5
    [Fact]
    public async Task Property_AllEventTypesSupported_ValidatesAllEnumValues()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        // Get all EventType enum values
        var allEventTypes = Enum.GetValues<EventType>();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // No existing events (to avoid chronological validation issues)
            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackingEvent?)null);

            // Create a parcel for status updates
            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) => 
                {
                    // Simulate database assigning an ID
                    evt.Id = faker.Random.Int(1, 10000);
                })
                .Returns(Task.CompletedTask);

            repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Pick a random event type from all available types
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

            // Execute the method - should succeed for all event types
            var response = await service.AddEventAsync(parcelId, request, CancellationToken.None);

            // Verify the event type is preserved correctly
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
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Create a parcel for status updates
            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
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

            // Generate a random scenario: either no events or some events
            var hasExistingEvents = faker.Random.Bool();
            TrackingEvent? latestEvent = null;

            if (hasExistingEvents)
            {
                // Create a latest event with a random timestamp
                var latestTimestamp = faker.Date.RecentOffset(days: 10);
                latestEvent = new TrackingEvent
                {
                    Id = faker.Random.Int(1, 1000),
                    ParcelId = parcelId,
                    Timestamp = latestTimestamp,
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence()
                };
            }

            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(latestEvent);

            // Test case 1: First event (no existing events) - should always succeed (pass chronological check)
            if (latestEvent == null)
            {
                var firstEventRequest = new CreateTrackingEventRequest
                {
                    Timestamp = faker.Date.RecentOffset(days: 1),
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence()
                };

                // This should not throw any exception
                var result = await service.AddEventAsync(parcelId, firstEventRequest, CancellationToken.None);
                result.Should().NotBeNull();
            }
            else
            {
                // Test case 2: Valid timestamp (>= latest) - should succeed
                var validTimestamp = latestEvent.Timestamp.AddMinutes(faker.Random.Int(1, 1000));
                var validRequest = new CreateTrackingEventRequest
                {
                    Timestamp = validTimestamp,
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence()
                };

                var result = await service.AddEventAsync(parcelId, validRequest, CancellationToken.None);
                result.Should().NotBeNull();

                // Test case 3: Invalid timestamp (< latest) - should fail with InvalidOperationException
                var invalidTimestamp = latestEvent.Timestamp.AddMinutes(-faker.Random.Int(1, 1000));
                var invalidRequest = new CreateTrackingEventRequest
                {
                    Timestamp = invalidTimestamp,
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence()
                };

                var invalidAct = async () => await service.AddEventAsync(parcelId, invalidRequest, CancellationToken.None);
                await invalidAct.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage($"Event timestamp {invalidTimestamp} is earlier than the most recent event at {latestEvent.Timestamp}.");

                // Test case 4: Equal timestamp (== latest) - should succeed
                var equalRequest = new CreateTrackingEventRequest
                {
                    Timestamp = latestEvent.Timestamp,
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence()
                };

                var equalResult = await service.AddEventAsync(parcelId, equalRequest, CancellationToken.None);
                equalResult.Should().NotBeNull();
            }
        }
    }

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
            Timestamp = default, // Missing timestamp
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
            EventType = (EventType)999, // Invalid event type
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
            Description = "" // Missing description
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

    // Feature: tracking-events-history, Property 5: Status synchronization mapping
    // Validates: Requirements 4.1
    [Fact]
    public async Task Property_StatusSynchronizationMapping_ValidatesEventTypeToStatusMapping()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        // Define the expected mappings according to the design document
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
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // No existing events (to avoid chronological validation issues)
            repoMock.Setup(r => r.GetLatestTrackingEventAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrackingEvent?)null);

            // Pick a random event type from the ones we want to test
            var eventType = faker.PickRandom(expectedMappings.Keys.ToList());
            var expectedStatus = expectedMappings[eventType];

            // Create a parcel with a different status to verify it gets updated
            var parcel = new Parcel
            {
                Id = parcelId,
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = faker.PickRandom<ParcelStatus>(), // Random initial status
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100)
            };

            repoMock.Setup(r => r.GetByIdAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            // Track if AddTrackingEventAsync was called
            TrackingEvent? capturedEvent = null;
            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) => capturedEvent = evt)
                .Returns(Task.CompletedTask);

            // Track if SaveChangesAsync was called
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

            // Execute the method
            var response = await service.AddEventAsync(parcelId, request, CancellationToken.None);

            // Verify the parcel status was updated to the expected status
            parcel.Status.Should().Be(expectedStatus, 
                $"EventType {eventType} should map to ParcelStatus {expectedStatus}");

            // Verify the event was added
            capturedEvent.Should().NotBeNull();
            capturedEvent!.EventType.Should().Be(eventType);
            capturedEvent.ParcelId.Should().Be(parcelId);

            // Verify SaveChanges was called (atomic transaction)
            saveChangesCalled.Should().BeTrue("changes should be saved atomically");

            // Verify the response contains the correct data
            response.Should().NotBeNull();
            response.ParcelId.Should().Be(parcelId);
            response.EventType.Should().Be(eventType.ToString());
            response.Description.Should().Be(request.Description);
        }
    }

    // Feature: tracking-events-history, Property 6: Complete history retrieval
    // Validates: Requirements 5.1
    [Fact]
    public async Task Property_CompleteHistoryRetrieval_ValidatesAllEventsReturned()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Generate a random number of events (between 0 and 20)
            var eventCount = faker.Random.Int(0, 20);
            var events = new List<TrackingEvent>();

            for (int j = 0; j < eventCount; j++)
            {
                events.Add(new TrackingEvent
                {
                    Id = faker.Random.Int(1, 10000),
                    ParcelId = parcelId,
                    Timestamp = faker.Date.RecentOffset(days: 30),
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence(),
                    LocationCity = faker.Random.Bool() ? faker.Address.City() : null,
                    LocationState = faker.Random.Bool() ? faker.Address.State() : null,
                    LocationCountry = faker.Random.Bool() ? faker.Address.Country() : null,
                    DelayReason = faker.Random.Bool() ? faker.Lorem.Sentence() : null
                });
            }

            // Setup repository to return all events when no filters are provided
            repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(events);

            // Execute the method without filters
            var result = await service.GetHistoryAsync(parcelId, null, null, CancellationToken.None);

            // Verify all events are returned
            var resultList = result.ToList();
            resultList.Should().HaveCount(eventCount, 
                $"retrieving history without filters should return all {eventCount} events");

            // Verify each event is present in the result
            foreach (var evt in events)
            {
                resultList.Should().Contain(r => 
                    r.Id == evt.Id && 
                    r.ParcelId == evt.ParcelId &&
                    r.Timestamp == evt.Timestamp &&
                    r.EventType == evt.EventType.ToString() &&
                    r.Description == evt.Description,
                    $"event {evt.Id} should be present in the result");
            }
        }
    }

    // Feature: tracking-events-history, Property 7: Chronological ordering of results
    // Validates: Requirements 5.2
    [Fact]
    public async Task Property_ChronologicalOrderingOfResults_ValidatesAscendingTimestampOrder()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Generate a random number of events (between 2 and 20) with random timestamps
            var eventCount = faker.Random.Int(2, 20);
            var events = new List<TrackingEvent>();

            for (int j = 0; j < eventCount; j++)
            {
                events.Add(new TrackingEvent
                {
                    Id = faker.Random.Int(1, 10000),
                    ParcelId = parcelId,
                    Timestamp = faker.Date.RecentOffset(days: 30),
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence(),
                    LocationCity = faker.Address.City(),
                    LocationState = faker.Address.State(),
                    LocationCountry = faker.Address.Country()
                });
            }

            // Sort events by timestamp ascending (as the repository should do)
            var sortedEvents = events.OrderBy(e => e.Timestamp).ToList();

            // Setup repository to return events sorted by timestamp
            repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sortedEvents);

            // Execute the method
            var result = await service.GetHistoryAsync(parcelId, null, null, CancellationToken.None);

            // Verify events are in chronological order (ascending by timestamp)
            var resultList = result.ToList();
            resultList.Should().HaveCount(eventCount);

            for (int j = 0; j < resultList.Count - 1; j++)
            {
                resultList[j].Timestamp.Should().BeOnOrBefore(resultList[j + 1].Timestamp,
                    $"event at index {j} should have timestamp <= event at index {j + 1}");
            }

            // Verify the order matches the sorted events
            for (int j = 0; j < resultList.Count; j++)
            {
                resultList[j].Id.Should().Be(sortedEvents[j].Id,
                    $"event at index {j} should match the sorted order");
            }
        }
    }

    // Feature: tracking-events-history, Property 8: From date filtering
    // Validates: Requirements 6.1
    [Fact]
    public async Task Property_FromDateFiltering_ValidatesEventsAfterFromDate()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Generate a random from date
            var fromDate = faker.Date.RecentOffset(days: 30);

            // Generate a random number of events (between 5 and 20)
            var eventCount = faker.Random.Int(5, 20);
            var allEvents = new List<TrackingEvent>();

            for (int j = 0; j < eventCount; j++)
            {
                allEvents.Add(new TrackingEvent
                {
                    Id = faker.Random.Int(1, 10000),
                    ParcelId = parcelId,
                    Timestamp = faker.Date.RecentOffset(days: 60), // Random timestamp
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence(),
                    LocationCity = faker.Address.City(),
                    LocationState = faker.Address.State(),
                    LocationCountry = faker.Address.Country()
                });
            }

            // Filter events to only those >= fromDate (as the repository should do)
            var filteredEvents = allEvents
                .Where(e => e.Timestamp >= fromDate)
                .OrderBy(e => e.Timestamp)
                .ToList();

            // Setup repository to return filtered events
            repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, fromDate, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(filteredEvents);

            // Execute the method with from date filter
            var result = await service.GetHistoryAsync(parcelId, fromDate, null, CancellationToken.None);

            // Verify all returned events have timestamps >= fromDate
            var resultList = result.ToList();
            
            foreach (var evt in resultList)
            {
                evt.Timestamp.Should().BeOnOrAfter(fromDate,
                    $"all events should have timestamp >= {fromDate}");
            }

            // Verify the count matches the filtered events
            resultList.Should().HaveCount(filteredEvents.Count,
                "result should contain only events with timestamp >= fromDate");
        }
    }

    // Feature: tracking-events-history, Property 9: To date filtering
    // Validates: Requirements 6.2
    [Fact]
    public async Task Property_ToDateFiltering_ValidatesEventsBeforeToDate()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Generate a random to date
            var toDate = faker.Date.RecentOffset(days: 10);

            // Generate a random number of events (between 5 and 20)
            var eventCount = faker.Random.Int(5, 20);
            var allEvents = new List<TrackingEvent>();

            for (int j = 0; j < eventCount; j++)
            {
                allEvents.Add(new TrackingEvent
                {
                    Id = faker.Random.Int(1, 10000),
                    ParcelId = parcelId,
                    Timestamp = faker.Date.RecentOffset(days: 60), // Random timestamp
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence(),
                    LocationCity = faker.Address.City(),
                    LocationState = faker.Address.State(),
                    LocationCountry = faker.Address.Country()
                });
            }

            // Filter events to only those <= toDate (as the repository should do)
            var filteredEvents = allEvents
                .Where(e => e.Timestamp <= toDate)
                .OrderBy(e => e.Timestamp)
                .ToList();

            // Setup repository to return filtered events
            repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, null, toDate, It.IsAny<CancellationToken>()))
                .ReturnsAsync(filteredEvents);

            // Execute the method with to date filter
            var result = await service.GetHistoryAsync(parcelId, null, toDate, CancellationToken.None);

            // Verify all returned events have timestamps <= toDate
            var resultList = result.ToList();
            
            foreach (var evt in resultList)
            {
                evt.Timestamp.Should().BeOnOrBefore(toDate,
                    $"all events should have timestamp <= {toDate}");
            }

            // Verify the count matches the filtered events
            resultList.Should().HaveCount(filteredEvents.Count,
                "result should contain only events with timestamp <= toDate");
        }
    }

    // Feature: tracking-events-history, Property 10: Invalid date range rejection
    // Validates: Requirements 6.5
    [Fact]
    public async Task Property_InvalidDateRangeRejection_ValidatesFromAfterToRejection()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            // Parcel always exists for this test
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Generate a random date range where from > to
            var toDate = faker.Date.RecentOffset(days: 30);
            var fromDate = toDate.AddDays(faker.Random.Int(1, 30)); // from is after to

            // Execute the method with invalid date range
            var act = async () => await service.GetHistoryAsync(parcelId, fromDate, toDate, CancellationToken.None);

            // Verify ArgumentException is thrown
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("'from' date must be earlier than or equal to 'to' date.");
        }
    }

    // Feature: tracking-events-history, Property 11: Description length validation
    // Validates: Requirements 7.4
    [Fact]
    public void Property_DescriptionLengthValidation_ValidatesMaxLength()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();
        var validator = new CreateTrackingEventRequestValidator();

        for (int i = 0; i < iterations; i++)
        {
            // Generate a description that exceeds 500 characters
            var longDescription = faker.Random.String2(501, 1000);

            var request = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = longDescription,
                LocationCity = faker.Address.City(),
                LocationState = faker.Address.State(),
                LocationCountry = faker.Address.Country()
            };

            // Validate the request
            var result = validator.Validate(request);

            // Verify validation fails for description exceeding 500 characters
            result.IsValid.Should().BeFalse(
                $"description with {longDescription.Length} characters should fail validation");
            
            result.Errors.Should().Contain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.Description) &&
                e.ErrorMessage.Contains("500"),
                "validation error should mention the 500 character limit for Description");

            // Also test valid description (at or below 500 characters)
            var validDescription = faker.Random.String2(1, 500);
            var validRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = validDescription,
                LocationCity = faker.Address.City(),
                LocationState = faker.Address.State(),
                LocationCountry = faker.Address.Country()
            };

            var validResult = validator.Validate(validRequest);

            // Verify no description length error for valid length
            validResult.Errors.Should().NotContain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.Description) &&
                e.ErrorMessage.Contains("500"),
                $"description with {validDescription.Length} characters should not fail length validation");
        }
    }

    // Feature: tracking-events-history, Property 12: Location field length validation
    // Validates: Requirements 7.5
    [Fact]
    public void Property_LocationFieldLengthValidation_ValidatesMaxLength()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();
        var validator = new CreateTrackingEventRequestValidator();

        for (int i = 0; i < iterations; i++)
        {
            // Test LocationCity exceeding 100 characters
            var longCity = faker.Random.String2(101, 200);
            var cityRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = faker.Lorem.Sentence(),
                LocationCity = longCity
            };

            var cityResult = validator.Validate(cityRequest);
            cityResult.IsValid.Should().BeFalse(
                $"LocationCity with {longCity.Length} characters should fail validation");
            cityResult.Errors.Should().Contain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.LocationCity) &&
                e.ErrorMessage.Contains("100"),
                "validation error should mention the 100 character limit for LocationCity");

            // Test LocationState exceeding 100 characters
            var longState = faker.Random.String2(101, 200);
            var stateRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = faker.Lorem.Sentence(),
                LocationState = longState
            };

            var stateResult = validator.Validate(stateRequest);
            stateResult.IsValid.Should().BeFalse(
                $"LocationState with {longState.Length} characters should fail validation");
            stateResult.Errors.Should().Contain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.LocationState) &&
                e.ErrorMessage.Contains("100"),
                "validation error should mention the 100 character limit for LocationState");

            // Test LocationCountry exceeding 100 characters
            var longCountry = faker.Random.String2(101, 200);
            var countryRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = faker.Lorem.Sentence(),
                LocationCountry = longCountry
            };

            var countryResult = validator.Validate(countryRequest);
            countryResult.IsValid.Should().BeFalse(
                $"LocationCountry with {longCountry.Length} characters should fail validation");
            countryResult.Errors.Should().Contain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.LocationCountry) &&
                e.ErrorMessage.Contains("100"),
                "validation error should mention the 100 character limit for LocationCountry");

            // Test valid location fields (at or below 100 characters)
            var validCity = faker.Random.String2(1, 100);
            var validState = faker.Random.String2(1, 100);
            var validCountry = faker.Random.String2(1, 100);
            var validRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = faker.Lorem.Sentence(),
                LocationCity = validCity,
                LocationState = validState,
                LocationCountry = validCountry
            };

            var validResult = validator.Validate(validRequest);

            // Verify no location length errors for valid lengths
            validResult.Errors.Should().NotContain(e => 
                (e.PropertyName == nameof(CreateTrackingEventRequest.LocationCity) ||
                 e.PropertyName == nameof(CreateTrackingEventRequest.LocationState) ||
                 e.PropertyName == nameof(CreateTrackingEventRequest.LocationCountry)) &&
                e.ErrorMessage.Contains("100"),
                "valid location fields should not fail length validation");
        }
    }
}
