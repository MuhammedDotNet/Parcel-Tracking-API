using FluentAssertions;
using Moq;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.UnitTests.Services;

public class AddressServiceTests
{
    private readonly Mock<IAddressRepository> _repoMock = new();
    private readonly AddressService _service;

    public AddressServiceTests()
    {
        _service = new AddressService(_repoMock.Object);
    }

    private static CreateAddressRequest SampleCreate() => new()
    {
        Street1 = "123 Main St",
        City = "New York",
        State = "NY",
        PostalCode = "10001",
        CountryCode = "US",
        IsResidential = true,
        ContactName = "John Doe",
        Phone = "+1-555-0100",
        Email = "john@example.com"
    };

    [Fact]
    public async Task GetAllAsync_ShouldReturnMappedResponses()
    {
        var entities = new List<Address>
        {
            new() { Id = 1, Street1 = "Street A", City = "City A", State = "SA", PostalCode = "11111", CountryCode = "US", ContactName = "A", Phone = "1" },
            new() { Id = 2, Street1 = "Street B", City = "City B", State = "SB", PostalCode = "22222", CountryCode = "US", ContactName = "B", Phone = "2" }
        };
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        var result = await _service.GetAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Street1.Should().Be("Street A");
        result[1].Street1.Should().Be("Street B");
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnMappedResponse()
    {
        var entity = new Address { Id = 42, Street1 = "123 Main St", City = "NYC", State = "NY", PostalCode = "10001", CountryCode = "US", ContactName = "John", Phone = "555" };
        _repoMock.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.GetByIdAsync(42, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(42);
        result.Street1.Should().Be("123 Main St");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);

        var result = await _service.GetByIdAsync(999, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldCallAddAndReturnResponse()
    {
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Address>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address a, CancellationToken _) => { a.Id = 1; return a; });

        var result = await _service.CreateAsync(SampleCreate(), CancellationToken.None);

        result.Id.Should().Be(1);
        result.Street1.Should().Be("123 Main St");
        result.ContactName.Should().Be("John Doe");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Address>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenExists_ShouldApplyUpdateAndReturn()
    {
        var entity = new Address { Id = 5, Street1 = "Old St", City = "Old City", State = "OC", PostalCode = "00000", CountryCode = "US", ContactName = "Old", Phone = "000" };
        _repoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var updateRequest = new UpdateAddressRequest
        {
            Street1 = "New St",
            City = "New City",
            State = "NC",
            PostalCode = "99999",
            CountryCode = "US",
            ContactName = "New Name",
            Phone = "999"
        };

        var result = await _service.UpdateAsync(5, updateRequest, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Street1.Should().Be("New St");
        result.City.Should().Be("New City");
        result.ContactName.Should().Be("New Name");
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotExists_ShouldReturnNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);

        var result = await _service.UpdateAsync(999, new UpdateAddressRequest
        {
            Street1 = "X",
            City = "X",
            State = "X",
            PostalCode = "X",
            CountryCode = "US",
            ContactName = "X",
            Phone = "X"
        }, CancellationToken.None);

        result.Should().BeNull();
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ShouldReturnNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);

        var result = await _service.DeleteAsync(999, CancellationToken.None);

        result.NotFound.Should().BeTrue();
        result.Conflict.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_ShouldRemoveAndReturnSuccess()
    {
        var entity = new Address { Id = 10, Street1 = "X", City = "X", State = "X", PostalCode = "X", CountryCode = "US", ContactName = "X", Phone = "X" };
        _repoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _repoMock.Setup(r => r.CountParcelReferencesAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _service.DeleteAsync(10, CancellationToken.None);

        result.NotFound.Should().BeFalse();
        result.Conflict.Should().BeFalse();
        _repoMock.Verify(r => r.Remove(entity), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenReferencedByParcels_ShouldReturnConflict()
    {
        var entity = new Address { Id = 10, Street1 = "X", City = "X", State = "X", PostalCode = "X", CountryCode = "US", ContactName = "X", Phone = "X" };
        _repoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _repoMock.Setup(r => r.CountParcelReferencesAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var result = await _service.DeleteAsync(10, CancellationToken.None);

        result.Conflict.Should().BeTrue();
        result.NotFound.Should().BeFalse();
        result.ConflictMessage.Should().Contain("3 parcel(s)");
        _repoMock.Verify(r => r.Remove(It.IsAny<Address>()), Times.Never);
    }
}
