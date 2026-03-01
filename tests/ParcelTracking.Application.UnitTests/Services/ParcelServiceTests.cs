using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Moq;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Domain.Exceptions;

namespace ParcelTracking.Application.UnitTests.Services;

public class ParcelServiceTests
{
    // Feature: parcel-status-lifecycle, Property 4: Terminal state modification rejection
    // Validates: Requirements 4.1, 4.2, 4.3
    [Property(MaxTest = 100)]
    public async Task GetWritableParcelAsync_TerminalStateParcel_ThrowsException(
        int parcelId,
        NonEmptyString trackingNumber,
        bool useDelivered)
    {
        // Arrange - use the boolean to select which terminal status to test
        var terminalStatus = useDelivered ? ParcelStatus.Delivered : ParcelStatus.Returned;
        
        var parcel = new Parcel
        {
            Id = Math.Abs(parcelId) + 1,
            TrackingNumber = trackingNumber.Get.Length > 50 ? trackingNumber.Get[..50] : trackingNumber.Get,
            Status = terminalStatus,
            ServiceType = ServiceType.Standard,
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

        var mockRepository = new Mock<IParcelRepository>();
        mockRepository
            .Setup(r => r.GetByIdAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);

        var service = new ParcelService(mockRepository.Object);

        // Act
        Func<Task> act = async () => await service.GetWritableParcelAsync(parcel.Id);

        // Assert - Requirement 4.1: Exception is thrown (rejection)
        var exception = await act.Should().ThrowAsync<ParcelInTerminalStateException>();
        
        // Requirement 4.2: Error message indicates terminal state
        exception.WithMessage($"Parcel {parcel.Id} is in terminal state '{terminalStatus}' and cannot be modified");
        
        // Requirement 4.3: Current terminal status is included in exception
        exception.Which.ParcelId.Should().Be(parcel.Id);
        exception.Which.Status.Should().Be(terminalStatus);
    }

    [Fact]
    public async Task GetWritableParcelAsync_NonTerminalStateParcel_ReturnsParcel()
    {
        // Arrange
        var parcel = new Parcel
        {
            Id = 1,
            TrackingNumber = "TEST123",
            Status = ParcelStatus.InTransit,
            ServiceType = ServiceType.Standard,
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

        var mockRepository = new Mock<IParcelRepository>();
        mockRepository
            .Setup(r => r.GetByIdAsync(parcel.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parcel);

        var service = new ParcelService(mockRepository.Object);

        // Act
        var result = await service.GetWritableParcelAsync(parcel.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(parcel);
    }

    [Fact]
    public async Task GetWritableParcelAsync_ParcelNotFound_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<IParcelRepository>();
        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Parcel?)null);

        var service = new ParcelService(mockRepository.Object);

        // Act
        var result = await service.GetWritableParcelAsync(999);

        // Assert
        result.Should().BeNull();
    }
}
