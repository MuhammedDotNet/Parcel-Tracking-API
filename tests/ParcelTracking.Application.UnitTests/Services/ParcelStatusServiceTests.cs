using Bogus;
using FluentAssertions;
using Moq;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Domain.Rules;

namespace ParcelTracking.Application.UnitTests.Services;

public class ParcelStatusServiceTests
{

    // Feature: parcel-status-lifecycle, Property 1: Valid transitions are accepted
    // Validates: Requirements 2.1, 2.4, 2.5
    [Fact]
    public async Task Property_ValidTransitionsAreAccepted_ValidatesAllValidTransitions()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        // Get all valid transitions from the state machine
        var validTransitions = new List<(ParcelStatus from, ParcelStatus to)>();
        foreach (var status in Enum.GetValues<ParcelStatus>())
        {
            var allowedTransitions = ParcelStatusRules.GetAllowedTransitions(status);
            foreach (var nextStatus in allowedTransitions)
            {
                validTransitions.Add((status, nextStatus));
            }
        }

        // If there are no valid transitions, skip the test
        if (validTransitions.Count == 0)
        {
            return;
        }

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new ParcelStatusService(repoMock.Object);

            // Pick a random valid transition
            var (fromStatus, toStatus) = faker.PickRandom(validTransitions);

            // Create a parcel with the initial status
            var parcel = new Parcel
            {
                Id = faker.Random.Int(1, 10000),
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                ServiceType = faker.PickRandom<ServiceType>(),
                Status = fromStatus,
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100),
                Weight = faker.Random.Decimal(0.1m, 100m),
                WeightUnit = faker.PickRandom<WeightUnit>(),
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = faker.PickRandom<DimensionUnit>(),
                DeclaredValue = faker.Random.Decimal(1, 10000),
                Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            // Setup repository to return the parcel
            repoMock.Setup(r => r.GetByIdAsync(parcel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            // Track if tracking event was added
            TrackingEvent? capturedEvent = null;
            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback<TrackingEvent, CancellationToken>((evt, ct) => capturedEvent = evt)
                .Returns(Task.CompletedTask);

            // Track if SaveChanges was called
            var saveChangesCalled = false;
            repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => saveChangesCalled = true)
                .Returns(Task.CompletedTask);

            // Execute the transition
            var result = await service.TransitionStatusAsync(parcel.Id, toStatus);

            // Verify the transition succeeded
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue(
                $"transition from {fromStatus} to {toStatus} should succeed");
            result.Parcel.Should().NotBeNull();
            result.Parcel!.Status.Should().Be(toStatus,
                $"parcel status should be updated to {toStatus}");
            result.ErrorType.Should().BeNull();
            result.ErrorMessage.Should().BeNull();

            // Verify a tracking event was created
            capturedEvent.Should().NotBeNull("a tracking event should be created");
            capturedEvent!.ParcelId.Should().Be(parcel.Id);
            capturedEvent.EventType.Should().Be(MapStatusToEventType(toStatus),
                "tracking event type should match the new status");
            capturedEvent.Description.Should().Contain(toStatus.ToString(),
                "tracking event description should mention the new status");

            // Verify SaveChanges was called
            saveChangesCalled.Should().BeTrue("changes should be saved");
        }
    }

    // Feature: parcel-status-lifecycle, Property 2: Invalid transitions are rejected
    // Validates: Requirements 2.1, 2.2, 2.3
    [Fact]
    public async Task Property_InvalidTransitionsAreRejected_ValidatesAllInvalidTransitions()
    {
        // Run 100 iterations as specified in the design document
        const int iterations = 100;
        var faker = new Faker();

        // Get all invalid transitions (all possible transitions minus valid ones)
        var allStatuses = Enum.GetValues<ParcelStatus>().ToList();
        var invalidTransitions = new List<(ParcelStatus from, ParcelStatus to)>();

        foreach (var fromStatus in allStatuses)
        {
            var allowedTransitions = ParcelStatusRules.GetAllowedTransitions(fromStatus);
            foreach (var toStatus in allStatuses)
            {
                if (!allowedTransitions.Contains(toStatus))
                {
                    invalidTransitions.Add((fromStatus, toStatus));
                }
            }
        }

        // If there are no invalid transitions, skip the test
        if (invalidTransitions.Count == 0)
        {
            return;
        }

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new ParcelStatusService(repoMock.Object);

            // Pick a random invalid transition
            var (fromStatus, toStatus) = faker.PickRandom(invalidTransitions);

            // Skip if fromStatus is terminal (will be caught by terminal state check)
            if (ParcelStatusRules.IsTerminal(fromStatus))
            {
                continue;
            }

            // Create a parcel with the initial status
            var parcel = new Parcel
            {
                Id = faker.Random.Int(1, 10000),
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                ServiceType = faker.PickRandom<ServiceType>(),
                Status = fromStatus,
                ShipperAddressId = faker.Random.Int(1, 100),
                RecipientAddressId = faker.Random.Int(1, 100),
                Weight = faker.Random.Decimal(0.1m, 100m),
                WeightUnit = faker.PickRandom<WeightUnit>(),
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = faker.PickRandom<DimensionUnit>(),
                DeclaredValue = faker.Random.Decimal(1, 10000),
                Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            // Setup repository to return the parcel
            repoMock.Setup(r => r.GetByIdAsync(parcel.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(parcel);

            // Track if tracking event was added (it shouldn't be)
            var trackingEventAdded = false;
            repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                .Callback(() => trackingEventAdded = true)
                .Returns(Task.CompletedTask);

            // Track if SaveChanges was called (it shouldn't be)
            var saveChangesCalled = false;
            repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => saveChangesCalled = true)
                .Returns(Task.CompletedTask);

            // Execute the invalid transition
            var result = await service.TransitionStatusAsync(parcel.Id, toStatus);

            // Verify the transition was rejected
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse(
                $"transition from {fromStatus} to {toStatus} should be rejected");
            result.ErrorType.Should().Be("invalid_transition",
                "error type should be 'invalid_transition'");
            result.ErrorMessage.Should().Contain(fromStatus.ToString(),
                "error message should mention the current status");
            result.ErrorMessage.Should().Contain(toStatus.ToString(),
                "error message should mention the requested status");
            result.CurrentStatus.Should().Be(fromStatus,
                "current status should be included in the result");
            result.RequestedStatus.Should().Be(toStatus,
                "requested status should be included in the result");
            result.AllowedStatuses.Should().NotBeNull(
                "allowed statuses should be included in the result");

            var allowedStatuses = ParcelStatusRules.GetAllowedTransitions(fromStatus);
            result.AllowedStatuses.Should().BeEquivalentTo(allowedStatuses,
                "allowed statuses should match the state machine rules");

            // Verify the parcel status was NOT changed
            parcel.Status.Should().Be(fromStatus,
                "parcel status should remain unchanged after invalid transition");

            // Verify no tracking event was created
            trackingEventAdded.Should().BeFalse(
                "no tracking event should be created for invalid transition");

            // Verify SaveChanges was not called
            saveChangesCalled.Should().BeFalse(
                "SaveChanges should not be called for invalid transition");
        }
    }

    private static EventType MapStatusToEventType(ParcelStatus status)
    {
        return status switch
        {
            ParcelStatus.LabelCreated => EventType.LabelCreated,
            ParcelStatus.PickedUp => EventType.PickedUp,
            ParcelStatus.InTransit => EventType.InTransit,
            ParcelStatus.OutForDelivery => EventType.OutForDelivery,
            ParcelStatus.Delivered => EventType.Delivered,
            ParcelStatus.Exception => EventType.Exception,
            ParcelStatus.Returned => EventType.Returned,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown parcel status")
        };
    }
}
