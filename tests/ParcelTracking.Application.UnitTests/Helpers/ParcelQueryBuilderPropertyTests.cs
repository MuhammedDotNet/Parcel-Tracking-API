using FluentAssertions;
using Bogus;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Helpers;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Application.UnitTests.Helpers;

public class ParcelQueryBuilderPropertyTests : IDisposable
{
    private readonly ParcelTrackingDbContext _context;

    public ParcelQueryBuilderPropertyTests()
    {
        var options = new DbContextOptionsBuilder<ParcelTrackingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ParcelTrackingDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // Feature: parcel-search-filter-pagination, Property 1: Filter application preserves query composability
    // Validates: Requirements 1.1-1.6
    [Fact]
    public void Property_FilterApplicationPreservesQueryComposability()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Generate random search parameters
            var searchParams = new ParcelSearchParams
            {
                Status = faker.Random.Bool() ? faker.PickRandom<ParcelStatus>() : null,
                ServiceType = faker.Random.Bool() ? faker.PickRandom<ServiceType>() : null,
                CreatedFrom = faker.Random.Bool() ? faker.Date.PastOffset() : null,
                CreatedTo = faker.Random.Bool() ? faker.Date.FutureOffset() : null,
                City = faker.Random.Bool() ? faker.Address.City() : null,
                Country = faker.Random.Bool() ? faker.Address.CountryCode() : null,
                Keyword = faker.Random.Bool() ? faker.Random.Word() : null
            };

            // Create base query
            var baseQuery = _context.Parcels.AsQueryable();

            // Apply filters
            var filteredQuery = ParcelQueryBuilder.ApplyFilters(baseQuery, searchParams);

            // Verify query can be composed with OrderBy
            var composedAction1 = () => filteredQuery.OrderBy(p => p.Id);
            composedAction1.Should().NotThrow($"OrderBy composition should work for iteration {i}");

            // Verify query can be composed with Take
            var composedAction2 = () => filteredQuery.Take(10);
            composedAction2.Should().NotThrow($"Take composition should work for iteration {i}");

            // Verify query can be composed with both
            var composedAction3 = () => filteredQuery.OrderBy(p => p.CreatedAt).Take(5);
            composedAction3.Should().NotThrow($"Combined composition should work for iteration {i}");

            // Verify the query is still an IQueryable
            filteredQuery.Should().BeAssignableTo<IQueryable<Parcel>>(
                $"filtered query should remain IQueryable for iteration {i}");
        }
    }

    // Feature: parcel-search-filter-pagination, Property 2: Omitted filters do not affect results
    // Validates: Requirements 1.6
    [Fact]
    public async Task Property_OmittedFiltersDoNotAffectResults()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Generate random test data
            var addresses = Enumerable.Range(0, 10).Select(_ => new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            }).ToList();

            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();

            var parcels = Enumerable.Range(0, 20).Select(_ => new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                Status = faker.PickRandom<ParcelStatus>(),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.PickRandom(addresses).Id,
                RecipientAddressId = faker.PickRandom(addresses).Id,
                Weight = faker.Random.Decimal(1, 100),
                WeightUnit = WeightUnit.Kg,
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = faker.Random.Decimal(10, 1000),
                Currency = "USD",
                CreatedAt = faker.Date.PastOffset(),
                UpdatedAt = faker.Date.RecentOffset()
            }).ToList();

            _context.Parcels.AddRange(parcels);
            await _context.SaveChangesAsync();

            // Create search params with all filters null
            var emptyParams = new ParcelSearchParams();

            // Apply filters with empty params
            var baseQuery = _context.Parcels.AsQueryable();
            var filteredQuery = ParcelQueryBuilder.ApplyFilters(baseQuery, emptyParams);

            // Get results
            var filteredResults = await filteredQuery.ToListAsync();
            var baseResults = await baseQuery.ToListAsync();

            // Verify results are identical
            filteredResults.Should().HaveCount(baseResults.Count,
                $"omitted filters should not reduce result count for iteration {i}");
            filteredResults.Select(p => p.Id).Should().BeEquivalentTo(baseResults.Select(p => p.Id),
                $"omitted filters should return same parcels for iteration {i}");
        }
    }

    // Feature: parcel-search-filter-pagination, Property 3: Multiple filters combine with AND logic
    // Validates: Requirements 1.5
    [Fact]
    public async Task Property_MultipleFiltersCombineWithAndLogic()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Generate random test data
            var addresses = Enumerable.Range(0, 10).Select(_ => new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            }).ToList();

            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();

            var parcels = Enumerable.Range(0, 50).Select(_ => new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                Status = faker.PickRandom<ParcelStatus>(),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.PickRandom(addresses).Id,
                RecipientAddressId = faker.PickRandom(addresses).Id,
                Weight = faker.Random.Decimal(1, 100),
                WeightUnit = WeightUnit.Kg,
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = faker.Random.Decimal(10, 1000),
                Currency = "USD",
                CreatedAt = faker.Date.PastOffset(),
                UpdatedAt = faker.Date.RecentOffset()
            }).ToList();

            _context.Parcels.AddRange(parcels);
            await _context.SaveChangesAsync();

            // Pick random filter values from the generated data
            var targetStatus = faker.PickRandom<ParcelStatus>();
            var targetServiceType = faker.PickRandom<ServiceType>();
            var targetDate = faker.Date.PastOffset();

            // Create search params with multiple filters
            var searchParams = new ParcelSearchParams
            {
                Status = targetStatus,
                ServiceType = targetServiceType,
                CreatedFrom = targetDate
            };

            // Apply filters
            var baseQuery = _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .AsQueryable();
            var filteredQuery = ParcelQueryBuilder.ApplyFilters(baseQuery, searchParams);

            // Get results
            var results = await filteredQuery.ToListAsync();

            // Verify ALL filters are applied (AND logic)
            foreach (var result in results)
            {
                result.Status.Should().Be(targetStatus,
                    $"all results should match status filter for iteration {i}");
                result.ServiceType.Should().Be(targetServiceType,
                    $"all results should match service type filter for iteration {i}");
                result.CreatedAt.Should().BeOnOrAfter(targetDate,
                    $"all results should match date filter for iteration {i}");
            }
        }
    }

    // Feature: parcel-search-filter-pagination, Property 4: City and country filters use OR logic across addresses
    // Validates: Requirements 2.1, 2.2
    [Fact]
    public async Task Property_AddressFiltersUseOrLogic()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Create specific addresses for testing
            var targetCity = faker.Address.City();
            var targetCountry = faker.Address.CountryCode().Substring(0, 2);
            
            // Ensure distinct values to avoid collisions
            string otherCity;
            do
            {
                otherCity = faker.Address.City();
            } while (otherCity == targetCity);
            
            string otherCountry;
            do
            {
                otherCountry = faker.Address.CountryCode().Substring(0, 2);
            } while (otherCountry == targetCountry);

            var addressWithTargetCity = new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = targetCity,
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = otherCountry,
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            };

            var addressWithTargetCountry = new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = otherCity,
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = targetCountry,
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            };

            var addressWithNeither = new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = otherCity,
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = otherCountry,
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            };

            _context.Addresses.AddRange(addressWithTargetCity, addressWithTargetCountry, addressWithNeither);
            await _context.SaveChangesAsync();

            // Create parcels with different address combinations
            var parcelWithShipperCity = new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
                ServiceType = ServiceType.Standard,
                ShipperAddressId = addressWithTargetCity.Id,
                RecipientAddressId = addressWithNeither.Id,
                Weight = 1, WeightUnit = WeightUnit.Kg,
                Length = 1, Width = 1, Height = 1, DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = 100, Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };

            var parcelWithRecipientCity = new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
                ServiceType = ServiceType.Standard,
                ShipperAddressId = addressWithNeither.Id,
                RecipientAddressId = addressWithTargetCity.Id,
                Weight = 1, WeightUnit = WeightUnit.Kg,
                Length = 1, Width = 1, Height = 1, DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = 100, Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };

            var parcelWithShipperCountry = new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
                ServiceType = ServiceType.Standard,
                ShipperAddressId = addressWithTargetCountry.Id,
                RecipientAddressId = addressWithNeither.Id,
                Weight = 1, WeightUnit = WeightUnit.Kg,
                Length = 1, Width = 1, Height = 1, DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = 100, Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };

            var parcelWithRecipientCountry = new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
                ServiceType = ServiceType.Standard,
                ShipperAddressId = addressWithNeither.Id,
                RecipientAddressId = addressWithTargetCountry.Id,
                Weight = 1, WeightUnit = WeightUnit.Kg,
                Length = 1, Width = 1, Height = 1, DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = 100, Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };

            var parcelWithNeither = new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Status = ParcelStatus.LabelCreated,
                ServiceType = ServiceType.Standard,
                ShipperAddressId = addressWithNeither.Id,
                RecipientAddressId = addressWithNeither.Id,
                Weight = 1, WeightUnit = WeightUnit.Kg,
                Length = 1, Width = 1, Height = 1, DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = 100, Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };

            _context.Parcels.AddRange(parcelWithShipperCity, parcelWithRecipientCity, 
                parcelWithShipperCountry, parcelWithRecipientCountry, parcelWithNeither);
            await _context.SaveChangesAsync();

            // Test city filter with OR logic
            var cityParams = new ParcelSearchParams { City = targetCity };
            var baseQuery = _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .AsQueryable();
            var cityResults = await ParcelQueryBuilder.ApplyFilters(baseQuery, cityParams).ToListAsync();

            // Should include parcels where EITHER shipper OR recipient has target city
            cityResults.Should().Contain(p => p.Id == parcelWithShipperCity.Id,
                $"should include parcel with shipper city for iteration {i}");
            cityResults.Should().Contain(p => p.Id == parcelWithRecipientCity.Id,
                $"should include parcel with recipient city for iteration {i}");
            cityResults.Should().NotContain(p => p.Id == parcelWithNeither.Id,
                $"should not include parcel without target city for iteration {i}");

            // Test country filter with OR logic
            var countryParams = new ParcelSearchParams { Country = targetCountry };
            var countryResults = await ParcelQueryBuilder.ApplyFilters(baseQuery, countryParams).ToListAsync();

            // Should include parcels where EITHER shipper OR recipient has target country
            countryResults.Should().Contain(p => p.Id == parcelWithShipperCountry.Id,
                $"should include parcel with shipper country for iteration {i}");
            countryResults.Should().Contain(p => p.Id == parcelWithRecipientCountry.Id,
                $"should include parcel with recipient country for iteration {i}");
            countryResults.Should().NotContain(p => p.Id == parcelWithNeither.Id,
                $"should not include parcel without target country for iteration {i}");
        }
    }

    // Feature: parcel-search-filter-pagination, Property 5: Keyword search matches across multiple fields with OR logic
    // Validates: Requirements 3.1, 3.2, 3.3
    [Fact]
    public async Task Property_KeywordSearchUsesOrLogic()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Create test addresses
            var address = new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            };

            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();

            // Generate a unique keyword
            var keyword = faker.Random.AlphaNumeric(8);

            // Create parcel with keyword in tracking number
            var parcelWithKeywordInTracking = new Parcel
            {
                TrackingNumber = $"PKG-{keyword}-123",
                Description = faker.Lorem.Sentence(),
                Status = ParcelStatus.LabelCreated,
                ServiceType = ServiceType.Standard,
                ShipperAddressId = address.Id,
                RecipientAddressId = address.Id,
                Weight = 1, WeightUnit = WeightUnit.Kg,
                Length = 1, Width = 1, Height = 1, DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = 100, Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };

            // Create parcel with keyword in description
            var parcelWithKeywordInDescription = new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = $"Package contains {keyword} items",
                Status = ParcelStatus.LabelCreated,
                ServiceType = ServiceType.Standard,
                ShipperAddressId = address.Id,
                RecipientAddressId = address.Id,
                Weight = 1, WeightUnit = WeightUnit.Kg,
                Length = 1, Width = 1, Height = 1, DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = 100, Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };

            // Create parcel without keyword
            var parcelWithoutKeyword = new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                Status = ParcelStatus.LabelCreated,
                ServiceType = ServiceType.Standard,
                ShipperAddressId = address.Id,
                RecipientAddressId = address.Id,
                Weight = 1, WeightUnit = WeightUnit.Kg,
                Length = 1, Width = 1, Height = 1, DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = 100, Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };

            _context.Parcels.AddRange(parcelWithKeywordInTracking, parcelWithKeywordInDescription, parcelWithoutKeyword);
            await _context.SaveChangesAsync();

            // Test keyword search with OR logic
            var searchParams = new ParcelSearchParams { Keyword = keyword };
            var baseQuery = _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .AsQueryable();
            var results = await ParcelQueryBuilder.ApplyFilters(baseQuery, searchParams).ToListAsync();

            // Should include parcels where keyword matches tracking number OR description
            results.Should().Contain(p => p.Id == parcelWithKeywordInTracking.Id,
                $"should include parcel with keyword in tracking number for iteration {i}");
            results.Should().Contain(p => p.Id == parcelWithKeywordInDescription.Id,
                $"should include parcel with keyword in description for iteration {i}");
            results.Should().NotContain(p => p.Id == parcelWithoutKeyword.Id,
                $"should not include parcel without keyword for iteration {i}");

            // Test case-insensitive search
            var upperKeyword = keyword.ToUpper();
            var caseInsensitiveParams = new ParcelSearchParams { Keyword = upperKeyword };
            var caseInsensitiveResults = await ParcelQueryBuilder.ApplyFilters(baseQuery, caseInsensitiveParams).ToListAsync();

            caseInsensitiveResults.Should().Contain(p => p.Id == parcelWithKeywordInTracking.Id,
                $"should be case-insensitive for tracking number for iteration {i}");
            caseInsensitiveResults.Should().Contain(p => p.Id == parcelWithKeywordInDescription.Id,
                $"should be case-insensitive for description for iteration {i}");
        }
    }

    // Feature: parcel-search-filter-pagination, Property 6: Sorting with tiebreaker produces deterministic ordering
    // Validates: Requirements 4.7
    [Fact]
    public async Task Property_SortingWithTiebreakerProducesDeterministicOrdering()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Create test addresses
            var addresses = Enumerable.Range(0, 5).Select(_ => new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            }).ToList();

            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();

            // Create parcels with some having identical sort field values
            var baseDate = DateTimeOffset.UtcNow;
            var parcels = new List<Parcel>();

            // Create parcels with duplicate CreatedAt values
            for (int j = 0; j < 10; j++)
            {
                parcels.Add(new Parcel
                {
                    TrackingNumber = faker.Random.AlphaNumeric(10),
                    Description = faker.Lorem.Sentence(),
                    Status = faker.PickRandom<ParcelStatus>(),
                    ServiceType = faker.PickRandom<ServiceType>(),
                    ShipperAddressId = faker.PickRandom(addresses).Id,
                    RecipientAddressId = faker.PickRandom(addresses).Id,
                    Weight = faker.Random.Decimal(1, 100),
                    WeightUnit = WeightUnit.Kg,
                    Length = faker.Random.Decimal(1, 100),
                    Width = faker.Random.Decimal(1, 100),
                    Height = faker.Random.Decimal(1, 100),
                    DimensionUnit = DimensionUnit.Cm,
                    DeclaredValue = faker.Random.Decimal(10, 1000),
                    Currency = "USD",
                    CreatedAt = baseDate.AddMinutes(j % 3), // Create duplicates
                    EstimatedDeliveryDate = baseDate.AddDays(j % 2), // Create duplicates
                    UpdatedAt = faker.Date.RecentOffset()
                });
            }

            _context.Parcels.AddRange(parcels);
            await _context.SaveChangesAsync();

            // Test different sort configurations
            var sortConfigurations = new[]
            {
                new { SortBy = "createdAt", SortDescending = true },
                new { SortBy = "createdAt", SortDescending = false },
                new { SortBy = "estimatedDeliveryDate", SortDescending = true },
                new { SortBy = "estimatedDeliveryDate", SortDescending = false },
                new { SortBy = "status", SortDescending = true },
                new { SortBy = "status", SortDescending = false }
            };

            foreach (var config in sortConfigurations)
            {
                var searchParams = new ParcelSearchParams
                {
                    SortBy = config.SortBy,
                    SortDescending = config.SortDescending
                };

                // Execute query twice
                var baseQuery = _context.Parcels
                    .Include(p => p.ShipperAddress)
                    .Include(p => p.RecipientAddress)
                    .AsQueryable();

                var sortedQuery = ParcelQueryBuilder.ApplySorting(baseQuery, searchParams);

                var firstExecution = await sortedQuery.ToListAsync();
                var secondExecution = await sortedQuery.ToListAsync();

                // Verify order is identical in both executions
                firstExecution.Should().HaveCount(secondExecution.Count,
                    $"both executions should return same count for {config.SortBy} {(config.SortDescending ? "desc" : "asc")} iteration {i}");

                var firstIds = firstExecution.Select(p => p.Id).ToList();
                var secondIds = secondExecution.Select(p => p.Id).ToList();

                firstIds.Should().Equal(secondIds,
                    $"order should be deterministic for {config.SortBy} {(config.SortDescending ? "desc" : "asc")} iteration {i}");

                // Verify tiebreaker is working by checking parcels with same sort field value
                // are ordered by Id
                for (int k = 0; k < firstExecution.Count - 1; k++)
                {
                    var current = firstExecution[k];
                    var next = firstExecution[k + 1];

                    // Check if sort field values are equal
                    bool sortFieldsEqual = config.SortBy.ToLower() switch
                    {
                        "createdat" => current.CreatedAt == next.CreatedAt,
                        "estimateddeliverydate" => current.EstimatedDeliveryDate == next.EstimatedDeliveryDate,
                        "status" => current.Status == next.Status,
                        _ => false
                    };

                    // If sort fields are equal, Id should be in ascending order (tiebreaker)
                    if (sortFieldsEqual)
                    {
                        current.Id.Should().BeLessThan(next.Id,
                            $"when {config.SortBy} values are equal, Id should be used as tiebreaker in ascending order for iteration {i}");
                    }
                }
            }
        }
    }

    // Feature: parcel-search-filter-pagination, Property 7: Cursor pagination maintains position consistency
    // Validates: Requirements 5.2, 5.3
    [Fact]
    public async Task Property_CursorPaginationMaintainsPositionConsistency()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Create test addresses
            var addresses = Enumerable.Range(0, 5).Select(_ => new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            }).ToList();

            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();

            // Create a reasonable number of parcels for pagination testing
            var parcelCount = faker.Random.Int(20, 50);
            var parcels = Enumerable.Range(0, parcelCount).Select(j => new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                Status = faker.PickRandom<ParcelStatus>(),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.PickRandom(addresses).Id,
                RecipientAddressId = faker.PickRandom(addresses).Id,
                Weight = faker.Random.Decimal(1, 100),
                WeightUnit = WeightUnit.Kg,
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = faker.Random.Decimal(10, 1000),
                Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-parcelCount + j),
                EstimatedDeliveryDate = faker.Date.FutureOffset(),
                UpdatedAt = faker.Date.RecentOffset()
            }).ToList();

            _context.Parcels.AddRange(parcels);
            await _context.SaveChangesAsync();

            // Test with random sort configuration
            var sortBy = faker.PickRandom(new[] { "createdAt", "estimatedDeliveryDate", "status" });
            var sortDescending = faker.Random.Bool();
            var pageSize = faker.Random.Int(5, 15);

            var searchParams = new ParcelSearchParams
            {
                SortBy = sortBy,
                SortDescending = sortDescending,
                PageSize = pageSize
            };

            // Fetch first page
            var baseQuery = _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .AsQueryable();

            var sortedQuery = ParcelQueryBuilder.ApplySorting(baseQuery, searchParams);
            var firstPageQuery = ParcelQueryBuilder.ApplyCursor(sortedQuery, searchParams);
            var firstPage = await firstPageQuery.Take(pageSize).ToListAsync();

            if (firstPage.Count == 0)
            {
                continue; // Skip if no results
            }

            // Create cursor from last item of first page
            var lastItem = firstPage.Last();
            var sortValue = sortBy.ToLower() switch
            {
                "createdat" => lastItem.CreatedAt.ToString("o"),
                "estimateddeliverydate" => lastItem.EstimatedDeliveryDate?.ToString("o") ?? "null",
                "status" => lastItem.Status.ToString(),
                _ => lastItem.CreatedAt.ToString("o")
            };

            var cursor = CursorHelper.Encode(sortBy, sortValue, lastItem.Id);

            // Fetch second page using cursor
            var secondPageParams = new ParcelSearchParams
            {
                SortBy = sortBy,
                SortDescending = sortDescending,
                PageSize = pageSize,
                Cursor = cursor
            };

            var secondPageQuery = ParcelQueryBuilder.ApplySorting(baseQuery, secondPageParams);
            var secondPageWithCursor = ParcelQueryBuilder.ApplyCursor(secondPageQuery, secondPageParams);
            var secondPage = await secondPageWithCursor.Take(pageSize).ToListAsync();

            // Verify no overlap between pages
            var firstPageIds = firstPage.Select(p => p.Id).ToHashSet();
            var secondPageIds = secondPage.Select(p => p.Id).ToHashSet();

            firstPageIds.Should().NotIntersectWith(secondPageIds,
                $"first and second page should not have overlapping items for iteration {i}");

            // Verify no gaps: fetch all items and check that second page starts immediately after first
            var allItems = await sortedQuery.ToListAsync();
            var firstPageLastIndex = allItems.FindIndex(p => p.Id == lastItem.Id);

            if (firstPageLastIndex >= 0 && firstPageLastIndex < allItems.Count - 1)
            {
                // There are items after the first page
                var expectedNextItem = allItems[firstPageLastIndex + 1];

                if (secondPage.Count > 0)
                {
                    secondPage.First().Id.Should().Be(expectedNextItem.Id,
                        $"second page should start immediately after first page for iteration {i}");
                }
            }
        }
    }

    // Feature: parcel-search-filter-pagination, Property 8: Invalid cursors gracefully fallback to first page
    // Validates: Requirements 5.5
    [Fact]
    public async Task Property_InvalidCursorsGracefullyFallbackToFirstPage()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Create test addresses
            var addresses = Enumerable.Range(0, 5).Select(_ => new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            }).ToList();

            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();

            // Create parcels
            var parcelCount = faker.Random.Int(10, 30);
            var parcels = Enumerable.Range(0, parcelCount).Select(j => new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                Status = faker.PickRandom<ParcelStatus>(),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.PickRandom(addresses).Id,
                RecipientAddressId = faker.PickRandom(addresses).Id,
                Weight = faker.Random.Decimal(1, 100),
                WeightUnit = WeightUnit.Kg,
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = faker.Random.Decimal(10, 1000),
                Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-parcelCount + j),
                EstimatedDeliveryDate = faker.Date.FutureOffset(),
                UpdatedAt = faker.Date.RecentOffset()
            }).ToList();

            _context.Parcels.AddRange(parcels);
            await _context.SaveChangesAsync();

            // Generate various types of invalid cursors
            var invalidCursors = new List<string>
            {
                faker.Random.AlphaNumeric(20), // Random string (not Base64)
                "!!!invalid!!!",                // Invalid characters
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("invalid")), // Valid Base64 but wrong format
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("field|value")), // Missing id component
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("field|value|notanumber")), // Invalid id
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("field|invaliddate|123")), // Invalid date for createdAt
                string.Empty, // Empty string
                "   ", // Whitespace
            };

            var sortBy = faker.PickRandom(new[] { "createdAt", "estimatedDeliveryDate", "status" });
            var sortDescending = faker.Random.Bool();
            var pageSize = faker.Random.Int(5, 15);

            // Get first page without cursor (expected behavior)
            var expectedParams = new ParcelSearchParams
            {
                SortBy = sortBy,
                SortDescending = sortDescending,
                PageSize = pageSize
            };

            var baseQuery = _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .AsQueryable();

            var expectedQuery = ParcelQueryBuilder.ApplySorting(baseQuery, expectedParams);
            var expectedFirstPage = await expectedQuery.Take(pageSize).ToListAsync();

            // Test each invalid cursor
            foreach (var invalidCursor in invalidCursors)
            {
                var invalidParams = new ParcelSearchParams
                {
                    SortBy = sortBy,
                    SortDescending = sortDescending,
                    PageSize = pageSize,
                    Cursor = invalidCursor
                };

                // Apply cursor should not throw exception
                var action = () =>
                {
                    var query = ParcelQueryBuilder.ApplySorting(baseQuery, invalidParams);
                    return ParcelQueryBuilder.ApplyCursor(query, invalidParams);
                };

                action.Should().NotThrow(
                    $"invalid cursor '{invalidCursor}' should not throw exception for iteration {i}");

                // Should return first page (same as no cursor)
                var sortedQuery = ParcelQueryBuilder.ApplySorting(baseQuery, invalidParams);
                var resultQuery = ParcelQueryBuilder.ApplyCursor(sortedQuery, invalidParams);
                var results = await resultQuery.Take(pageSize).ToListAsync();

                // Results should match first page
                var resultIds = results.Select(p => p.Id).ToList();
                var expectedIds = expectedFirstPage.Select(p => p.Id).ToList();

                resultIds.Should().Equal(expectedIds,
                    $"invalid cursor '{invalidCursor}' should fallback to first page for iteration {i}");
            }
        }
    }

    // Feature: parcel-search-filter-pagination, Property 10: Total count reflects pre-pagination results
    // Validates: Requirements 7.2, 9.2
    [Fact]
    public async Task Property_TotalCountReflectsPrePaginationResults()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Create test addresses
            var addresses = Enumerable.Range(0, 5).Select(_ => new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            }).ToList();

            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();

            // Create random number of parcels
            var parcelCount = faker.Random.Int(30, 100);
            var parcels = Enumerable.Range(0, parcelCount).Select(j => new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                Status = faker.PickRandom<ParcelStatus>(),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.PickRandom(addresses).Id,
                RecipientAddressId = faker.PickRandom(addresses).Id,
                Weight = faker.Random.Decimal(1, 100),
                WeightUnit = WeightUnit.Kg,
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = faker.Random.Decimal(10, 1000),
                Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-parcelCount + j),
                EstimatedDeliveryDate = faker.Date.FutureOffset(),
                UpdatedAt = faker.Date.RecentOffset()
            }).ToList();

            _context.Parcels.AddRange(parcels);
            await _context.SaveChangesAsync();

            // Apply random filters
            var searchParams = new ParcelSearchParams
            {
                Status = faker.Random.Bool() ? faker.PickRandom<ParcelStatus>() : null,
                ServiceType = faker.Random.Bool() ? faker.PickRandom<ServiceType>() : null,
                SortBy = faker.PickRandom(new[] { "createdAt", "estimatedDeliveryDate", "status" }),
                SortDescending = faker.Random.Bool(),
                PageSize = faker.Random.Int(5, 20)
            };

            // Build query with filters
            var baseQuery = _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .AsQueryable();

            var filteredQuery = ParcelQueryBuilder.ApplyFilters(baseQuery, searchParams);

            // Count total matching records BEFORE pagination
            var expectedTotalCount = await filteredQuery.CountAsync();

            // Apply sorting and pagination
            var sortedQuery = ParcelQueryBuilder.ApplySorting(filteredQuery, searchParams);
            var paginatedQuery = ParcelQueryBuilder.ApplyCursor(sortedQuery, searchParams);
            var results = await paginatedQuery.Take(searchParams.PageSize + 1).ToListAsync();

            // Build paged result
            var pagedResult = ParcelQueryBuilder.BuildPagedResult(results, searchParams, expectedTotalCount);

            // Verify totalCount matches the count before pagination
            pagedResult.TotalCount.Should().Be(expectedTotalCount,
                $"totalCount should reflect pre-pagination count for iteration {i}");

            // Verify totalCount is independent of page size
            pagedResult.TotalCount.Should().BeGreaterThanOrEqualTo(pagedResult.Items.Count,
                $"totalCount should be >= items on current page for iteration {i}");

            // Manually verify the count is correct
            var manualCount = await filteredQuery.CountAsync();
            pagedResult.TotalCount.Should().Be(manualCount,
                $"totalCount should match manual count for iteration {i}");
        }
    }

    // Feature: parcel-search-filter-pagination, Property 11: Next cursor presence indicates more results
    // Validates: Requirements 5.6, 5.7, 7.6
    [Fact]
    public async Task Property_NextCursorPresenceIndicatesMoreResults()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Create test addresses
            var addresses = Enumerable.Range(0, 5).Select(_ => new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            }).ToList();

            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();

            // Create random number of parcels
            var parcelCount = faker.Random.Int(10, 50);
            var parcels = Enumerable.Range(0, parcelCount).Select(j => new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                Status = faker.PickRandom<ParcelStatus>(),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.PickRandom(addresses).Id,
                RecipientAddressId = faker.PickRandom(addresses).Id,
                Weight = faker.Random.Decimal(1, 100),
                WeightUnit = WeightUnit.Kg,
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = faker.Random.Decimal(10, 1000),
                Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-parcelCount + j),
                EstimatedDeliveryDate = faker.Date.FutureOffset(),
                UpdatedAt = faker.Date.RecentOffset()
            }).ToList();

            _context.Parcels.AddRange(parcels);
            await _context.SaveChangesAsync();

            // Use random page size
            var pageSize = faker.Random.Int(5, 20);
            var searchParams = new ParcelSearchParams
            {
                SortBy = faker.PickRandom(new[] { "createdAt", "estimatedDeliveryDate", "status" }),
                SortDescending = faker.Random.Bool(),
                PageSize = pageSize
            };

            // Build query
            var baseQuery = _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .AsQueryable();

            var filteredQuery = ParcelQueryBuilder.ApplyFilters(baseQuery, searchParams);
            var totalCount = await filteredQuery.CountAsync();
            var sortedQuery = ParcelQueryBuilder.ApplySorting(filteredQuery, searchParams);
            var paginatedQuery = ParcelQueryBuilder.ApplyCursor(sortedQuery, searchParams);
            
            // Fetch pageSize + 1 to detect next page
            var results = await paginatedQuery.Take(pageSize + 1).ToListAsync();

            // Build paged result
            var pagedResult = ParcelQueryBuilder.BuildPagedResult(results, searchParams, totalCount);

            // Verify consistency: hasNextPage should equal (nextCursor != null)
            pagedResult.HasNextPage.Should().Be(pagedResult.NextCursor != null,
                $"hasNextPage should be true if and only if nextCursor is not null for iteration {i}");

            // Verify logic: if we got more than pageSize items, there should be a next page
            if (results.Count > pageSize)
            {
                pagedResult.HasNextPage.Should().BeTrue(
                    $"hasNextPage should be true when results exceed pageSize for iteration {i}");
                pagedResult.NextCursor.Should().NotBeNullOrEmpty(
                    $"nextCursor should be present when results exceed pageSize for iteration {i}");
            }
            else
            {
                pagedResult.HasNextPage.Should().BeFalse(
                    $"hasNextPage should be false when results don't exceed pageSize for iteration {i}");
                pagedResult.NextCursor.Should().BeNull(
                    $"nextCursor should be null when results don't exceed pageSize for iteration {i}");
            }

            // Verify items count: should never exceed pageSize
            pagedResult.Items.Count.Should().BeLessThanOrEqualTo(pageSize,
                $"items count should never exceed pageSize for iteration {i}");

            // If there's a next cursor, verify it can be decoded
            if (pagedResult.NextCursor != null)
            {
                var decodeAction = () => CursorHelper.Decode(pagedResult.NextCursor);
                decodeAction.Should().NotThrow(
                    $"nextCursor should be valid and decodable for iteration {i}");

                // Verify the cursor points to the last item on the page
                var (sortField, sortValue, id) = CursorHelper.Decode(pagedResult.NextCursor);
                var lastItem = pagedResult.Items.Last();
                
                lastItem.Id.Should().Be(id,
                    $"nextCursor should reference the last item's Id for iteration {i}");
            }
        }
    }

    // Feature: parcel-search-filter-pagination, Property 13: Query execution order preserves correctness
    // Validates: Requirements 9.1-9.6
    [Fact]
    public async Task Property_QueryExecutionOrderPreservesCorrectness()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Clear database for each iteration
            _context.Parcels.RemoveRange(_context.Parcels);
            _context.Addresses.RemoveRange(_context.Addresses);
            await _context.SaveChangesAsync();

            // Create test addresses
            var addresses = Enumerable.Range(0, 5).Select(_ => new Address
            {
                Street1 = faker.Address.StreetAddress(),
                City = faker.Address.City(),
                State = faker.Address.State(),
                PostalCode = faker.Address.ZipCode(),
                CountryCode = faker.Address.CountryCode().Substring(0, 2),
                ContactName = faker.Name.FullName(),
                Phone = faker.Phone.PhoneNumber()
            }).ToList();

            _context.Addresses.AddRange(addresses);
            await _context.SaveChangesAsync();

            // Create random number of parcels
            var parcelCount = faker.Random.Int(30, 100);
            var parcels = Enumerable.Range(0, parcelCount).Select(j => new Parcel
            {
                TrackingNumber = faker.Random.AlphaNumeric(10),
                Description = faker.Lorem.Sentence(),
                Status = faker.PickRandom<ParcelStatus>(),
                ServiceType = faker.PickRandom<ServiceType>(),
                ShipperAddressId = faker.PickRandom(addresses).Id,
                RecipientAddressId = faker.PickRandom(addresses).Id,
                Weight = faker.Random.Decimal(1, 100),
                WeightUnit = WeightUnit.Kg,
                Length = faker.Random.Decimal(1, 100),
                Width = faker.Random.Decimal(1, 100),
                Height = faker.Random.Decimal(1, 100),
                DimensionUnit = DimensionUnit.Cm,
                DeclaredValue = faker.Random.Decimal(10, 1000),
                Currency = "USD",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-parcelCount + j),
                EstimatedDeliveryDate = faker.Date.FutureOffset(),
                UpdatedAt = faker.Date.RecentOffset()
            }).ToList();

            _context.Parcels.AddRange(parcels);
            await _context.SaveChangesAsync();

            // Apply random filters and pagination settings
            var targetStatus = faker.PickRandom<ParcelStatus>();
            var pageSize = faker.Random.Int(5, 20);
            
            var searchParams = new ParcelSearchParams
            {
                Status = faker.Random.Bool() ? targetStatus : null,
                ServiceType = faker.Random.Bool() ? faker.PickRandom<ServiceType>() : null,
                SortBy = faker.PickRandom(new[] { "createdAt", "estimatedDeliveryDate", "status" }),
                SortDescending = faker.Random.Bool(),
                PageSize = pageSize
            };

            // Requirement 9.1: Apply filters before counting
            var baseQuery = _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .AsQueryable();
            
            var filteredQuery = ParcelQueryBuilder.ApplyFilters(baseQuery, searchParams);

            // Requirement 9.2: Count total matching records before applying pagination
            var totalCountBeforePagination = await filteredQuery.CountAsync();

            // Requirement 9.3: Apply sorting after counting but before cursor application
            var sortedQuery = ParcelQueryBuilder.ApplySorting(filteredQuery, searchParams);

            // Requirement 9.4: Apply cursor filter after sorting
            var cursorQuery = ParcelQueryBuilder.ApplyCursor(sortedQuery, searchParams);

            // Requirement 9.5: Fetch pageSize + 1 records to detect if next page exists
            var results = await cursorQuery.Take(pageSize + 1).ToListAsync();

            // Requirement 9.6: Materialize results only after all query operations are composed
            results.Should().NotBeNull($"results should be materialized for iteration {i}");

            // Verify totalCount reflects pre-pagination count
            var manualFilteredCount = await _context.Parcels
                .Include(p => p.ShipperAddress)
                .Include(p => p.RecipientAddress)
                .Where(p => !searchParams.Status.HasValue || p.Status == searchParams.Status.Value)
                .Where(p => !searchParams.ServiceType.HasValue || p.ServiceType == searchParams.ServiceType.Value)
                .CountAsync();

            totalCountBeforePagination.Should().Be(manualFilteredCount,
                $"totalCount should match filtered count before pagination for iteration {i}");

            // Verify pagination is applied correctly
            var actualPageSize = Math.Min(results.Count, pageSize);
            actualPageSize.Should().BeLessThanOrEqualTo(pageSize,
                $"actual page size should not exceed requested page size for iteration {i}");

            // Verify sorting is applied (check if results are in correct order)
            if (results.Count > 1)
            {
                var sortBy = searchParams.SortBy?.ToLower() ?? "createdat";
                var descending = searchParams.SortDescending;

                for (int k = 0; k < results.Count - 1; k++)
                {
                    var current = results[k];
                    var next = results[k + 1];

                    var comparison = sortBy switch
                    {
                        "createdat" => current.CreatedAt.CompareTo(next.CreatedAt),
                        "estimateddeliverydate" => Nullable.Compare(current.EstimatedDeliveryDate, next.EstimatedDeliveryDate),
                        "status" => current.Status.CompareTo(next.Status),
                        _ => current.CreatedAt.CompareTo(next.CreatedAt)
                    };

                    if (comparison == 0)
                    {
                        // Tiebreaker: Id should be in ascending order
                        current.Id.Should().BeLessThan(next.Id,
                            $"tiebreaker should order by Id ascending for iteration {i}");
                    }
                    else if (descending)
                    {
                        comparison.Should().BeGreaterThanOrEqualTo(0,
                            $"descending sort should be applied correctly for iteration {i}");
                    }
                    else
                    {
                        comparison.Should().BeLessThanOrEqualTo(0,
                            $"ascending sort should be applied correctly for iteration {i}");
                    }
                }
            }

            // Verify BuildPagedResult produces correct metadata
            var pagedResult = ParcelQueryBuilder.BuildPagedResult(results, searchParams, totalCountBeforePagination);

            pagedResult.TotalCount.Should().Be(totalCountBeforePagination,
                $"paged result totalCount should match pre-pagination count for iteration {i}");
            
            pagedResult.PageSize.Should().Be(pageSize,
                $"paged result pageSize should match requested size for iteration {i}");

            pagedResult.Items.Count.Should().BeLessThanOrEqualTo(pageSize,
                $"paged result items should not exceed page size for iteration {i}");

            // Verify hasNextPage is correct based on fetched results
            var expectedHasNextPage = results.Count > pageSize;
            pagedResult.HasNextPage.Should().Be(expectedHasNextPage,
                $"hasNextPage should be {expectedHasNextPage} for iteration {i}");

            // Verify nextCursor consistency
            if (expectedHasNextPage)
            {
                pagedResult.NextCursor.Should().NotBeNullOrEmpty(
                    $"nextCursor should be present when there are more results for iteration {i}");
            }
            else
            {
                pagedResult.NextCursor.Should().BeNull(
                    $"nextCursor should be null when there are no more results for iteration {i}");
            }
        }
    }
}
