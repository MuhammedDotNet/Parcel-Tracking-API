using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Infrastructure.Data;
using ParcelTracking.Infrastructure.Services;

namespace ParcelTracking.Application.UnitTests.Services;

/// <summary>
/// Property-based tests for AnalyticsService.
/// Each test validates one or more correctness properties from the design spec.
/// </summary>
public class AnalyticsServicePropertyTests : IDisposable
{
    private readonly ParcelTrackingDbContext _context;
    private readonly AnalyticsService _service;

    public AnalyticsServicePropertyTests()
    {
        var options = new DbContextOptionsBuilder<ParcelTrackingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ParcelTrackingDbContext(options);
        _service = new AnalyticsService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // Feature: parcel-analytics-summary-endpoints, Property 4: Exception reasons response structure
    // Validates: Requirements 2.1, 2.3
    [Property(MaxTest = 100)]
    public void Property4_ExceptionReasonsResponseStructure(NonNegativeInt eventCount, NonNegativeInt reasonIndex1, NonNegativeInt reasonIndex2, NonNegativeInt reasonIndex3)
    {
        // Arrange
        var reasons = new[] { "RecipientUnavailable", "AddressIssue", "WeatherDelay", "VehicleBreakdown", "CustomsDelay" };
        var count = eventCount.Get % 51; // 0-50 events

        if (count == 0)
        {
            // Test empty case
            var emptyResult = _service.GetTopExceptionReasonsAsync(
                DateTimeOffset.UtcNow.AddDays(-30),
                DateTimeOffset.UtcNow).Result;
            emptyResult.Should().BeEmpty("No exception events should return empty list");
            return;
        }

        // Generate events with random reasons
        var events = new List<TrackingEvent>();
        for (int i = 0; i < count; i++)
        {
            var reasonIndex = (i % 3) switch
            {
                0 => reasonIndex1.Get % reasons.Length,
                1 => reasonIndex2.Get % reasons.Length,
                _ => reasonIndex3.Get % reasons.Length
            };

            events.Add(new TrackingEvent
            {
                Id = i + 1,
                ParcelId = (i % 10) + 1,
                EventType = EventType.Exception,
                DelayReason = reasons[reasonIndex],
                Timestamp = DateTimeOffset.UtcNow.AddDays(-(i % 29 + 1)),
                Description = $"Exception: {reasons[reasonIndex]}"
            });
        }

        SeedDatabase(events).Wait();

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;

        // Act
        var result = _service.GetTopExceptionReasonsAsync(from, to).Result;

        // Assert
        // Each response should have all required fields
        foreach (var reason in result)
        {
            reason.Reason.Should().NotBeNullOrEmpty("Reason field should be populated");
            reason.Count.Should().BeGreaterThan(0, "Count should be positive");
            reason.Percentage.Should().BeGreaterThan(0, "Percentage should be positive");
        }

        // Sum of all percentages should equal 100 (within 0.5% tolerance for rounding)
        var totalPercentage = result.Sum(r => r.Percentage);
        totalPercentage.Should().BeInRange(99.5, 100.5,
            "Sum of all percentages should equal 100 within rounding tolerance");

        // Clean up for next iteration
        CleanDatabase().Wait();
    }

    // Feature: parcel-analytics-summary-endpoints, Property 5: Exception filtering
    // Validates: Requirements 2.2
    [Property(MaxTest = 100)]
    public void Property5_ExceptionFiltering(NonNegativeInt exceptionCount, NonNegativeInt nonExceptionCount, NonNegativeInt nullReasonCount)
    {
        // Arrange
        var reasons = new[] { "RecipientUnavailable", "AddressIssue", "WeatherDelay" };
        var exceptionEvents = exceptionCount.Get % 31; // 0-30 exception events
        var nonExceptionEvents = nonExceptionCount.Get % 31; // 0-30 non-exception events
        var nullReasonEvents = nullReasonCount.Get % 31; // 0-30 exception events with null reason

        var events = new List<TrackingEvent>();
        var expectedCount = 0;

        // Add exception events with valid DelayReason (should be included)
        for (int i = 0; i < exceptionEvents; i++)
        {
            events.Add(new TrackingEvent
            {
                Id = events.Count + 1,
                ParcelId = (events.Count % 10) + 1,
                EventType = EventType.Exception,
                DelayReason = reasons[i % reasons.Length],
                Timestamp = DateTimeOffset.UtcNow.AddDays(-(i % 29 + 1)),
                Description = "Exception with reason"
            });
            expectedCount++;
        }

        // Add non-exception events (should be excluded)
        for (int i = 0; i < nonExceptionEvents; i++)
        {
            events.Add(new TrackingEvent
            {
                Id = events.Count + 1,
                ParcelId = (events.Count % 10) + 1,
                EventType = EventType.InTransit,
                DelayReason = reasons[i % reasons.Length],
                Timestamp = DateTimeOffset.UtcNow.AddDays(-(i % 29 + 1)),
                Description = "Non-exception event"
            });
        }

        // Add exception events with null DelayReason (should be excluded)
        for (int i = 0; i < nullReasonEvents; i++)
        {
            events.Add(new TrackingEvent
            {
                Id = events.Count + 1,
                ParcelId = (events.Count % 10) + 1,
                EventType = EventType.Exception,
                DelayReason = null,
                Timestamp = DateTimeOffset.UtcNow.AddDays(-(i % 29 + 1)),
                Description = "Exception without reason"
            });
        }

        SeedDatabase(events).Wait();

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;

        // Act
        var result = _service.GetTopExceptionReasonsAsync(from, to).Result;

        // Assert
        // Only exception events with non-null DelayReason should be included
        var totalCount = result.Sum(r => r.Count);
        totalCount.Should().Be(expectedCount,
            $"Only exception events with non-null DelayReason should be included. " +
            $"Expected: {expectedCount} (exception events with reason), " +
            $"Excluded: {nonExceptionEvents} (non-exception events) + {nullReasonEvents} (null reasons)");

        // Verify no non-exception event types are included
        foreach (var reason in result)
        {
            // All reasons should come from exception events only
            reason.Reason.Should().BeOneOf(reasons,
                "All reasons should come from exception events with valid DelayReason");
        }

        // Clean up for next iteration
        CleanDatabase().Wait();
    }

    // Feature: parcel-analytics-summary-endpoints, Property 6: Exception reasons ordering
    // Validates: Requirements 2.4
    [Property(MaxTest = 100)]
    public void Property6_ExceptionReasonsOrdering(NonNegativeInt reason1Count, NonNegativeInt reason2Count, NonNegativeInt reason3Count)
    {
        // Arrange
        var reasons = new[] { "RecipientUnavailable", "AddressIssue", "WeatherDelay" };
        var counts = new[]
        {
            (reason1Count.Get % 50) + 1, // 1-50
            (reason2Count.Get % 50) + 1, // 1-50
            (reason3Count.Get % 50) + 1  // 1-50
        };

        var events = new List<TrackingEvent>();

        // Add events for each reason with specific counts
        for (int reasonIndex = 0; reasonIndex < reasons.Length; reasonIndex++)
        {
            for (int i = 0; i < counts[reasonIndex]; i++)
            {
                events.Add(new TrackingEvent
                {
                    Id = events.Count + 1,
                    ParcelId = (events.Count % 10) + 1,
                    EventType = EventType.Exception,
                    DelayReason = reasons[reasonIndex],
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-(events.Count % 29 + 1)),
                    Description = $"Exception: {reasons[reasonIndex]}"
                });
            }
        }

        SeedDatabase(events).Wait();

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;

        // Act
        var result = _service.GetTopExceptionReasonsAsync(from, to).Result;

        // Assert
        // Results should be ordered by count in descending order
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].Count.Should().BeGreaterThanOrEqualTo(result[i + 1].Count,
                $"Exception reasons should be ordered by count in descending order. " +
                $"Reason '{result[i].Reason}' (count: {result[i].Count}) should have >= count than " +
                $"reason '{result[i + 1].Reason}' (count: {result[i + 1].Count})");
        }

        // Verify the counts match what we seeded
        var expectedCounts = counts.OrderByDescending(c => c).ToList();
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Count.Should().Be(expectedCounts[i],
                $"Count at position {i} should match the expected sorted count");
        }

        // Clean up for next iteration
        CleanDatabase().Wait();
    }

    private async Task SeedDatabase(List<TrackingEvent> events)
    {
        if (events.Count == 0) return;

        // Create parcels for the events
        var parcelIds = events.Select(e => e.ParcelId).Distinct().ToList();
        var parcels = parcelIds.Select(id => new Parcel
        {
            Id = id,
            TrackingNumber = $"PKG-TEST-{id:D6}",
            ServiceType = ServiceType.Standard,
            Status = ParcelStatus.Exception,
            ShipperAddressId = 1,
            RecipientAddressId = 2,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToList();

        await _context.Parcels.AddRangeAsync(parcels);
        await _context.TrackingEvents.AddRangeAsync(events);
        await _context.SaveChangesAsync();
    }

    private async Task CleanDatabase()
    {
        _context.TrackingEvents.RemoveRange(_context.TrackingEvents);
        _context.Parcels.RemoveRange(_context.Parcels);
        await _context.SaveChangesAsync();
    }

    // Feature: parcel-analytics-summary-endpoints, Property 7: Service breakdown response completeness
    // Validates: Requirements 3.1, 3.5
    [Property(MaxTest = 100)]
    public void Property7_ServiceBreakdownResponseCompleteness(NonNegativeInt economyCount, NonNegativeInt standardCount, NonNegativeInt expressCount, NonNegativeInt overnightCount)
    {
        // Arrange
        var serviceTypes = new[]
        {
            (ServiceType.Economy, economyCount.Get % 31), // 0-30 parcels
            (ServiceType.Standard, standardCount.Get % 31),
            (ServiceType.Express, expressCount.Get % 31),
            (ServiceType.Overnight, overnightCount.Get % 31)
        };

        var parcels = new List<Parcel>();
        var parcelId = 1;

        foreach (var (serviceType, count) in serviceTypes)
        {
            for (int i = 0; i < count; i++)
            {
                parcels.Add(new Parcel
                {
                    Id = parcelId++,
                    TrackingNumber = $"PKG-TEST-{parcelId:D6}",
                    ServiceType = serviceType,
                    Status = ParcelStatus.InTransit,
                    ShipperAddressId = 1,
                    RecipientAddressId = 2,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-15),
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        SeedParcelsDatabase(parcels).Wait();

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;

        // Act
        var result = _service.GetServiceBreakdownAsync(from, to).Result;

        // Assert
        // Each service type present in the data should have an entry
        var expectedServiceTypes = serviceTypes
            .Where(st => st.Item2 > 0)
            .Select(st => st.Item1.ToString())
            .ToList();

        result.Should().HaveCount(expectedServiceTypes.Count,
            "Response should include an entry for each service type present in the data");

        foreach (var breakdown in result)
        {
            // Each response should have all required fields
            breakdown.ServiceType.Should().NotBeNullOrEmpty("ServiceType field should be populated");
            breakdown.Count.Should().BeGreaterThan(0, "Count should be positive for service types in the data");
            breakdown.AverageDeliveryTimeHours.Should().BeGreaterThanOrEqualTo(0, "AverageDeliveryTimeHours should be non-negative");

            // Verify the service type is one we seeded
            breakdown.ServiceType.Should().BeOneOf(expectedServiceTypes,
                "ServiceType should be one of the service types present in the data");
        }

        // Verify counts match what we seeded
        foreach (var (serviceType, count) in serviceTypes)
        {
            if (count > 0)
            {
                var breakdown = result.FirstOrDefault(r => r.ServiceType == serviceType.ToString());
                breakdown.Should().NotBeNull($"Service type {serviceType} should be in the results");
                breakdown!.Count.Should().Be(count,
                    $"Count for {serviceType} should match the seeded count");
            }
        }

        // Clean up for next iteration
        CleanDatabase().Wait();
    }

    private async Task SeedParcelsDatabase(List<Parcel> parcels)
    {
        if (parcels.Count == 0) return;

        await _context.Parcels.AddRangeAsync(parcels);
        await _context.SaveChangesAsync();
    }

    // Feature: parcel-analytics-summary-endpoints, Property 8: Delivery time calculation
    // Validates: Requirements 3.4
    [Property(MaxTest = 100)]
    public void Property8_DeliveryTimeCalculation(NonNegativeInt deliveryHoursOffset1, NonNegativeInt deliveryHoursOffset2, NonNegativeInt deliveryHoursOffset3)
    {
        // Arrange
        var serviceTypes = new[] { ServiceType.Economy, ServiceType.Standard, ServiceType.Express };
        var deliveryHours = new[]
        {
            (deliveryHoursOffset1.Get % 168) + 1, // 1-168 hours (1-7 days)
            (deliveryHoursOffset2.Get % 168) + 1,
            (deliveryHoursOffset3.Get % 168) + 1
        };

        var parcels = new List<Parcel>();
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < serviceTypes.Length; i++)
        {
            var createdAt = now.AddDays(-30);
            var actualDeliveryDate = createdAt.AddHours(deliveryHours[i]);

            parcels.Add(new Parcel
            {
                Id = i + 1,
                TrackingNumber = $"PKG-TEST-{i + 1:D6}",
                ServiceType = serviceTypes[i],
                Status = ParcelStatus.Delivered,
                ShipperAddressId = 1,
                RecipientAddressId = 2,
                CreatedAt = createdAt,
                ActualDeliveryDate = actualDeliveryDate,
                EstimatedDeliveryDate = createdAt.AddDays(3),
                UpdatedAt = now
            });
        }

        SeedParcelsDatabase(parcels).Wait();

        var from = now.AddDays(-31);
        var to = now;

        // Act
        var result = _service.GetServiceBreakdownAsync(from, to).Result;

        // Assert
        // For each delivered parcel with non-null ActualDeliveryDate,
        // the delivery time in hours should equal the difference between ActualDeliveryDate and CreatedAt
        for (int i = 0; i < serviceTypes.Length; i++)
        {
            var breakdown = result.FirstOrDefault(r => r.ServiceType == serviceTypes[i].ToString());
            breakdown.Should().NotBeNull($"Service type {serviceTypes[i]} should be in the results");

            var expectedDeliveryTime = deliveryHours[i];
            var actualDeliveryTime = breakdown!.AverageDeliveryTimeHours;

            // Since we only have one parcel per service type, the average equals the single delivery time
            actualDeliveryTime.Should().BeApproximately(expectedDeliveryTime, 0.1,
                $"Delivery time for {serviceTypes[i]} should equal the difference between " +
                $"ActualDeliveryDate and CreatedAt. Expected: {expectedDeliveryTime} hours, " +
                $"Actual: {actualDeliveryTime} hours");
        }

        // Clean up for next iteration
        CleanDatabase().Wait();
    }

    // Feature: parcel-analytics-summary-endpoints, Property 9: Pipeline status completeness
    // Validates: Requirements 4.1, 4.3, 4.5
    [Property(MaxTest = 100)]
    public void Property9_PipelineStatusCompleteness(NonNegativeInt labelCreatedCount, NonNegativeInt pickedUpCount, NonNegativeInt inTransitCount, NonNegativeInt outForDeliveryCount, NonNegativeInt deliveredCount, NonNegativeInt exceptionCount, NonNegativeInt returnedCount)
    {
        // Arrange
        var statusCounts = new[]
        {
            (ParcelStatus.LabelCreated, labelCreatedCount.Get % 31), // 0-30 parcels
            (ParcelStatus.PickedUp, pickedUpCount.Get % 31),
            (ParcelStatus.InTransit, inTransitCount.Get % 31),
            (ParcelStatus.OutForDelivery, outForDeliveryCount.Get % 31),
            (ParcelStatus.Delivered, deliveredCount.Get % 31),
            (ParcelStatus.Exception, exceptionCount.Get % 31),
            (ParcelStatus.Returned, returnedCount.Get % 31)
        };

        var parcels = new List<Parcel>();
        var parcelId = 1;

        foreach (var (status, count) in statusCounts)
        {
            for (int i = 0; i < count; i++)
            {
                parcels.Add(new Parcel
                {
                    Id = parcelId++,
                    TrackingNumber = $"PKG-TEST-{parcelId:D6}",
                    ServiceType = ServiceType.Standard,
                    Status = status,
                    ShipperAddressId = 1,
                    RecipientAddressId = 2,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-15),
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        SeedParcelsDatabase(parcels).Wait();

        // Act
        var result = _service.GetPipelineAsync().Result;

        // Assert
        // Response should include all ParcelStatus enum values
        var allStatuses = Enum.GetValues<ParcelStatus>();
        result.Should().HaveCount(allStatuses.Length,
            "Pipeline response should include all ParcelStatus enum values");

        foreach (var status in allStatuses)
        {
            var statusResponse = result.FirstOrDefault(r => r.Status == status.ToString());
            statusResponse.Should().NotBeNull(
                $"Pipeline response should include status {status}");

            // Verify the count matches what we seeded (including zero counts)
            var expectedCount = statusCounts.FirstOrDefault(sc => sc.Item1 == status).Item2;
            statusResponse!.Count.Should().Be(expectedCount,
                $"Count for status {status} should match the seeded count (including zero)");
        }

        // Verify statuses with zero parcels are included with count of zero
        foreach (var (status, count) in statusCounts)
        {
            if (count == 0)
            {
                var statusResponse = result.FirstOrDefault(r => r.Status == status.ToString());
                statusResponse.Should().NotBeNull(
                    $"Status {status} with zero parcels should still be included in the response");
                statusResponse!.Count.Should().Be(0,
                    $"Status {status} should have a count of zero");
            }
        }

        // Clean up for next iteration
        CleanDatabase().Wait();
    }

    // Feature: parcel-analytics-summary-endpoints, Property 10: Pipeline includes all parcels
    // Validates: Requirements 4.2
    [Property(MaxTest = 100)]
    public void Property10_PipelineIncludesAllParcels(NonNegativeInt totalParcels)
    {
        // Arrange
        var count = (totalParcels.Get % 100) + 1; // 1-100 parcels
        var statuses = Enum.GetValues<ParcelStatus>();
        var parcels = new List<Parcel>();

        for (int i = 0; i < count; i++)
        {
            parcels.Add(new Parcel
            {
                Id = i + 1,
                TrackingNumber = $"PKG-TEST-{i + 1:D6}",
                ServiceType = ServiceType.Standard,
                Status = statuses[i % statuses.Length], // Distribute across all statuses
                ShipperAddressId = 1,
                RecipientAddressId = 2,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-15),
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        SeedParcelsDatabase(parcels).Wait();

        // Act
        var result = _service.GetPipelineAsync().Result;

        // Assert
        // The sum of counts across all pipeline status entries should equal the total number of parcels
        var totalCountInPipeline = result.Sum(r => r.Count);
        totalCountInPipeline.Should().Be(count,
            $"Sum of counts across all pipeline status entries should equal the total number of parcels in the database. " +
            $"Expected: {count}, Actual: {totalCountInPipeline}");

        // Verify no parcels are double-counted or missing
        var expectedCountByStatus = parcels
            .GroupBy(p => p.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        foreach (var statusResponse in result)
        {
            var expectedCount = expectedCountByStatus.GetValueOrDefault(statusResponse.Status, 0);
            statusResponse.Count.Should().Be(expectedCount,
                $"Count for status {statusResponse.Status} should match the actual parcel count");
        }

        // Clean up for next iteration
        CleanDatabase().Wait();
    }

    // Feature: parcel-analytics-summary-endpoints, Property 12: Enum serialization format
    // Validates: Requirements 6.5
    [Property(MaxTest = 100)]
    public void Property12_EnumSerializationFormat(NonNegativeInt serviceTypeIndex1, NonNegativeInt serviceTypeIndex2, NonNegativeInt statusIndex1, NonNegativeInt statusIndex2)
    {
        // Arrange
        var serviceTypes = Enum.GetValues<ServiceType>();
        var statuses = Enum.GetValues<ParcelStatus>();

        // Generate random service types and statuses
        var serviceType1 = serviceTypes[serviceTypeIndex1.Get % serviceTypes.Length];
        var serviceType2 = serviceTypes[serviceTypeIndex2.Get % serviceTypes.Length];
        var status1 = statuses[statusIndex1.Get % statuses.Length];
        var status2 = statuses[statusIndex2.Get % statuses.Length];

        var parcels = new List<Parcel>
        {
            new()
            {
                Id = 1,
                TrackingNumber = "PKG-TEST-000001",
                ServiceType = serviceType1,
                Status = status1,
                ShipperAddressId = 1,
                RecipientAddressId = 2,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-15),
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = 2,
                TrackingNumber = "PKG-TEST-000002",
                ServiceType = serviceType2,
                Status = status2,
                ShipperAddressId = 1,
                RecipientAddressId = 2,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-15),
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        // Add exception tracking event for testing exception reasons
        var events = new List<TrackingEvent>
        {
            new()
            {
                Id = 1,
                ParcelId = 1,
                EventType = EventType.Exception,
                DelayReason = "RecipientUnavailable",
                Timestamp = DateTimeOffset.UtcNow.AddDays(-10),
                Description = "Exception event"
            }
        };

        SeedDatabase(events).Wait();

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;

        // Act: Get all analytics responses
        var serviceBreakdown = _service.GetServiceBreakdownAsync(from, to).Result;
        var pipeline = _service.GetPipelineAsync().Result;
        var exceptionReasons = _service.GetTopExceptionReasonsAsync(from, to).Result;

        // Assert: Verify enum values are represented as strings, not integers

        // Test ServiceBreakdownResponse.ServiceType
        foreach (var breakdown in serviceBreakdown)
        {
            // ServiceType should be a string representation of the enum
            breakdown.ServiceType.Should().NotBeNullOrEmpty(
                "ServiceType should be a non-empty string");

            // Verify it's a valid enum name (not a number)
            breakdown.ServiceType.Should().MatchRegex("^[A-Za-z]+$",
                "ServiceType should contain only letters (enum name), not numbers");

            // Verify it matches one of the actual enum values
            var isValidEnumName = Enum.TryParse<ServiceType>(breakdown.ServiceType, out _);
            isValidEnumName.Should().BeTrue(
                $"ServiceType '{breakdown.ServiceType}' should be a valid ServiceType enum name");

            // Verify it's not an integer representation
            var isInteger = int.TryParse(breakdown.ServiceType, out _);
            isInteger.Should().BeFalse(
                $"ServiceType '{breakdown.ServiceType}' should not be an integer representation");
        }

        // Test PipelineStatusResponse.Status
        foreach (var statusResponse in pipeline)
        {
            // Status should be a string representation of the enum
            statusResponse.Status.Should().NotBeNullOrEmpty(
                "Status should be a non-empty string");

            // Verify it's a valid enum name (not a number)
            statusResponse.Status.Should().MatchRegex("^[A-Za-z]+$",
                "Status should contain only letters (enum name), not numbers");

            // Verify it matches one of the actual enum values
            var isValidEnumName = Enum.TryParse<ParcelStatus>(statusResponse.Status, out _);
            isValidEnumName.Should().BeTrue(
                $"Status '{statusResponse.Status}' should be a valid ParcelStatus enum name");

            // Verify it's not an integer representation
            var isInteger = int.TryParse(statusResponse.Status, out _);
            isInteger.Should().BeFalse(
                $"Status '{statusResponse.Status}' should not be an integer representation");
        }

        // Test ExceptionReasonResponse.Reason (string field, but verify it's not numeric)
        foreach (var reason in exceptionReasons)
        {
            // Reason should be a string (not a number)
            reason.Reason.Should().NotBeNullOrEmpty(
                "Reason should be a non-empty string");

            // While Reason is not an enum, verify it's a meaningful string, not a number
            reason.Reason.Should().MatchRegex("^[A-Za-z]+$",
                "Reason should contain only letters (descriptive name), not numbers");
        }

        // Additional verification: Serialize to JSON and verify format
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        // Serialize service breakdown
        var serviceBreakdownJson = System.Text.Json.JsonSerializer.Serialize(serviceBreakdown, jsonOptions);
        serviceBreakdownJson.Should().NotContain("\"serviceType\":0",
            "ServiceType should not be serialized as integer 0");
        serviceBreakdownJson.Should().NotContain("\"serviceType\":1",
            "ServiceType should not be serialized as integer 1");
        serviceBreakdownJson.Should().NotContain("\"serviceType\":2",
            "ServiceType should not be serialized as integer 2");
        serviceBreakdownJson.Should().NotContain("\"serviceType\":3",
            "ServiceType should not be serialized as integer 3");

        // Verify it contains string enum values
        foreach (var breakdown in serviceBreakdown)
        {
            serviceBreakdownJson.Should().Contain($"\"serviceType\":\"{breakdown.ServiceType}\"",
                $"ServiceType should be serialized as string \"{breakdown.ServiceType}\"");
        }

        // Serialize pipeline
        var pipelineJson = System.Text.Json.JsonSerializer.Serialize(pipeline, jsonOptions);
        pipelineJson.Should().NotContain("\"status\":0",
            "Status should not be serialized as integer 0");
        pipelineJson.Should().NotContain("\"status\":1",
            "Status should not be serialized as integer 1");
        pipelineJson.Should().NotContain("\"status\":2",
            "Status should not be serialized as integer 2");

        // Verify it contains string enum values
        foreach (var statusResponse in pipeline)
        {
            pipelineJson.Should().Contain($"\"status\":\"{statusResponse.Status}\"",
                $"Status should be serialized as string \"{statusResponse.Status}\"");
        }

        // Clean up for next iteration
        CleanDatabase().Wait();
    }
}
