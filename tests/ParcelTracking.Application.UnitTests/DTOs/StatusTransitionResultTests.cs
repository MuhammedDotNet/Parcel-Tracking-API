using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.DTOs;

public class StatusTransitionResultTests
{
    // Feature: parcel-status-lifecycle, Property 13: Success result structure
    // Validates: Requirements 10.2
    [Property(MaxTest = 100)]
    public void SuccessResult_HasCorrectStructure(int id, NonEmptyString trackingNumber, ParcelStatus status, ServiceType serviceType)
    {
        // Arrange
        var parcel = new Parcel
        {
            Id = Math.Abs(id) + 1,
            TrackingNumber = trackingNumber.Get.Length > 50 ? trackingNumber.Get.Substring(0, 50) : trackingNumber.Get,
            Status = status,
            ServiceType = serviceType,
            ShipperAddressId = 1,
            RecipientAddressId = 2,
            Weight = 1.0m,
            WeightUnit = WeightUnit.Kg,
            Length = 10,
            Width = 10,
            Height = 10,
            DimensionUnit = DimensionUnit.Cm,
            DeclaredValue = 100,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = StatusTransitionResult.Success(parcel);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Parcel.Should().Be(parcel);
        result.ErrorType.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.CurrentStatus.Should().BeNull();
        result.RequestedStatus.Should().BeNull();
        result.AllowedStatuses.Should().BeNull();
    }

    // Feature: parcel-status-lifecycle, Property 14: Terminal state result structure
    // Validates: Requirements 10.4
    [Property(MaxTest = 100)]
    public bool TerminalStateResult_HasCorrectStructure(bool useDelivered)
    {
        // Arrange - use the boolean to select which terminal status to test
        var terminalStatus = useDelivered ? ParcelStatus.Delivered : ParcelStatus.Returned;

        // Act
        var result = StatusTransitionResult.TerminalState(terminalStatus);

        // Assert
        return result.IsSuccess == false &&
               result.ErrorType == "terminal_state" &&
               result.ErrorMessage != null &&
               result.ErrorMessage.Contains(terminalStatus.ToString()) &&
               result.CurrentStatus == terminalStatus &&
               result.Parcel == null &&
               result.RequestedStatus == null &&
               result.AllowedStatuses == null;
    }

    // Feature: parcel-status-lifecycle, Property 15: Invalid transition result structure
    // Validates: Requirements 10.5
    [Property(MaxTest = 100)]
    public void InvalidTransitionResult_HasCorrectStructure(ParcelStatus currentStatus, ParcelStatus requestedStatus)
    {
        // Arrange
        var allowedStatuses = new HashSet<ParcelStatus> { ParcelStatus.PickedUp, ParcelStatus.Exception };

        // Act
        var result = StatusTransitionResult.InvalidTransition(
            currentStatus,
            requestedStatus,
            allowedStatuses);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be("invalid_transition");
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain(currentStatus.ToString());
        result.ErrorMessage.Should().Contain(requestedStatus.ToString());
        result.CurrentStatus.Should().Be(currentStatus);
        result.RequestedStatus.Should().Be(requestedStatus);
        result.AllowedStatuses.Should().BeEquivalentTo(allowedStatuses);
        result.Parcel.Should().BeNull();
    }
}
