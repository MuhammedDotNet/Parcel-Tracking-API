using FluentAssertions;
using Moq;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.Services;

public class ParcelRegistrationServiceTests
{
    private readonly Mock<IParcelRepository>        _repoMock       = new();
    private readonly Mock<ITrackingNumberGenerator> _generatorMock  = new();
    private readonly Mock<IDeliveryEstimator>       _estimatorMock  = new();
    private readonly ParcelRegistrationService      _sut;

    public ParcelRegistrationServiceTests()
    {
        _sut = new ParcelRegistrationService(
            _repoMock.Object,
            _generatorMock.Object,
            _estimatorMock.Object);
    }

    private static RegisterParcelRequest ValidRequest(int shipperId = 1, int recipientId = 2) => new()
    {
        ShipperAddressId   = shipperId,
        RecipientAddressId = recipientId,
        ServiceType        = "Express",
        Description        = "Test parcel",
        Weight             = new WeightDto { Value = 2.5m, Unit = "kg" },
        Dimensions         = new DimensionsDto { Length = 40, Width = 30, Height = 10, Unit = "cm" },
        DeclaredValue      = new DeclaredValueDto { Amount = 150m, Currency = "USD" },
        ContentItems       =
        [
            new ContentItemDto
            {
                HsCode          = "8471.30",
                Description     = "Laptop",
                Quantity        = 1,
                UnitValue       = 150m,
                Currency        = "USD",
                Weight          = 2.1m,
                WeightUnit      = "kg",
                CountryOfOrigin = "CN"
            }
        ]
    };

    private void SetupAddressExists(int addressId, bool exists)
        => _repoMock.Setup(r => r.AddressExistsAsync(addressId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(exists);

    [Fact]
    public async Task RegisterAsync_WhenShipperAddressNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        SetupAddressExists(99, false);
        _repoMock.Setup(r => r.AddressExistsAsync(2, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var request = ValidRequest(shipperId: 99);

        // Act
        var act = () => _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task RegisterAsync_WhenRecipientAddressNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        SetupAddressExists(1, true);
        SetupAddressExists(88, false);

        var request = ValidRequest(shipperId: 1, recipientId: 88);

        // Act
        var act = () => _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*88*");
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_ReturnsMappedResponse()
    {
        // Arrange
        SetupAddressExists(1, true);
        SetupAddressExists(2, true);
        _generatorMock.Setup(g => g.Generate()).Returns("PKG-20260225-ABCDEF");
        _estimatorMock.Setup(e => e.Estimate(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                      .Returns(DateTimeOffset.UtcNow.AddDays(3));
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Parcel>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var request = ValidRequest();

        // Act
        var response = await _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        response.TrackingNumber.Should().Be("PKG-20260225-ABCDEF");
        response.Status.Should().Be(nameof(ParcelStatus.LabelCreated));
        response.ShipperAddressId.Should().Be(1);
        response.RecipientAddressId.Should().Be(2);
        response.ServiceType.Should().Be("Express");
        response.ContentItems.Should().HaveCount(1);
        response.ContentItems[0].HsCode.Should().Be("8471.30");
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_CallsSaveChangesOnce()
    {
        // Arrange
        SetupAddressExists(1, true);
        SetupAddressExists(2, true);
        _generatorMock.Setup(g => g.Generate()).Returns("PKG-TEST-000001");
        _estimatorMock.Setup(e => e.Estimate(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                      .Returns(DateTimeOffset.UtcNow.AddDays(3));
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Parcel>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        // Act
        await _sut.RegisterAsync(ValidRequest(), CancellationToken.None);

        // Assert — exactly one transaction
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_AddsInitialTrackingEvent()
    {
        // Arrange
        SetupAddressExists(1, true);
        SetupAddressExists(2, true);
        _generatorMock.Setup(g => g.Generate()).Returns("PKG-TEST-000002");
        _estimatorMock.Setup(e => e.Estimate(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                      .Returns(DateTimeOffset.UtcNow.AddDays(3));
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Parcel>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddTrackingEventAsync(It.IsAny<TrackingEvent>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        // Act
        await _sut.RegisterAsync(ValidRequest(), CancellationToken.None);

        // Assert — one TrackingEvent (initial label event) was added
        _repoMock.Verify(
            r => r.AddTrackingEventAsync(
                It.Is<TrackingEvent>(e => e.EventType == EventType.LabelCreated),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
