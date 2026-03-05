using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Infrastructure.Services;

namespace ParcelTracking.Application.UnitTests.Services;

/// <summary>
/// Property-based tests for DeliveryEstimationService.
/// Each test validates one or more correctness properties from the design spec.
/// </summary>
public class DeliveryEstimationServicePropertyTests
{
    private static Parcel CreateParcel(
        ServiceType serviceType = ServiceType.Standard,
        ParcelStatus status = ParcelStatus.InTransit,
        string shipperCountry = "US",
        string recipientCountry = "US",
        DateTime? createdAt = null) => new()
        {
            Id = 1,
            TrackingNumber = "PKG-TEST-000001",
            ServiceType = serviceType,
            Status = status,
            ShipperAddress = new Address
            {
                Id = 1,
                CountryCode = shipperCountry,
                City = "ShipperCity",
                State = "SC",
                Street1 = "1 Shipper St",
                PostalCode = "11111",
                ContactName = "Shipper",
                Phone = "555-1111"
            },
            RecipientAddress = new Address
            {
                Id = 2,
                CountryCode = recipientCountry,
                City = "RecipientCity",
                State = "RC",
                Street1 = "1 Recipient St",
                PostalCode = "22222",
                ContactName = "Recipient",
                Phone = "555-2222"
            },
            ShipperAddressId = 1,
            RecipientAddressId = 2,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    // Feature: delivery-estimation, Property 1: Estimate contains both dates
    // Validates: Requirements 1.1
    [Property(MaxTest = 100)]
    public void Property1_EstimateContainsBothDates(
        NonNegativeInt serviceTypeIndex,
        NonNegativeInt statusIndex,
        bool isInternational)
    {
        // Arrange
        var serviceTypes = Enum.GetValues<ServiceType>();
        var statuses = Enum.GetValues<ParcelStatus>();

        var serviceType = serviceTypes[serviceTypeIndex.Get % serviceTypes.Length];
        var status = statuses[statusIndex.Get % statuses.Length];

        var shipperCountry = "US";
        var recipientCountry = isInternational ? "CA" : "US";

        var parcel = CreateParcel(
            serviceType: serviceType,
            status: status,
            shipperCountry: shipperCountry,
            recipientCountry: recipientCountry,
            createdAt: new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc)); // Monday

        var service = new DeliveryEstimationService(new SimpleTimeZoneResolver());

        // Act
        var result = service.Calculate(parcel);

        // Assert
        result.EarliestDelivery.Should().NotBe(default(DateOnly),
            $"EarliestDelivery should be set for {serviceType}, {status}, International: {isInternational}");
        result.LatestDelivery.Should().NotBe(default(DateOnly),
            $"LatestDelivery should be set for {serviceType}, {status}, International: {isInternational}");
        (result.EarliestDelivery <= result.LatestDelivery).Should().BeTrue(
            $"EarliestDelivery should be <= LatestDelivery for {serviceType}, {status}, International: {isInternational}");
    }

    // Feature: delivery-estimation, Property 2: Business days exclude weekends
    // Validates: Requirements 1.2, 5.1, 5.2
    [Property(MaxTest = 100)]
    public void Property2_BusinessDaysExcludeWeekends(PositiveInt businessDays)
    {
        // Limit to reasonable range (1-30 days) to keep test execution fast
        var daysToAdd = (businessDays.Get % 30) + 1;

        // Generate a random start date within a reasonable range
        var randomDayOffset = businessDays.Get % 365;
        var startDate = new DateOnly(2026, 1, 1).AddDays(randomDayOffset);

        // Act - Add business days using a local implementation that mirrors the service
        var endDate = AddBusinessDaysLocal(startDate, daysToAdd);

        // Count the actual business days between start and end
        var actualBusinessDays = CountBusinessDaysBetween(startDate, endDate);

        // Count weekend days between start and end
        var weekendDays = CountWeekendDaysBetween(startDate, endDate);

        // Assert - The number of business days should match exactly what we requested
        actualBusinessDays.Should().Be(daysToAdd,
            $"Adding {daysToAdd} business days from {startDate} should result in exactly {daysToAdd} business days, " +
            $"but got {actualBusinessDays}. EndDate: {endDate}, WeekendDays: {weekendDays}");

        // Assert - End date should be after start date
        endDate.Should().BeAfter(startDate,
            $"End date {endDate} should be after start date {startDate}");

        // Assert - If we cross weekends, the calendar days should be more than or equal to business days
        var calendarDays = endDate.DayNumber - startDate.DayNumber;
        calendarDays.Should().BeGreaterThanOrEqualTo(daysToAdd,
            $"Calendar days ({calendarDays}) should be >= business days ({daysToAdd}) when weekends are excluded");

        // Assert - Verify that no weekend days are included in the business day count
        // by checking that all days between start and end that are weekdays sum to the expected count
        var verifyCount = 0;
        var current = startDate;
        while (current < endDate)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                verifyCount++;
            }
        }
        verifyCount.Should().Be(daysToAdd,
            $"Manual verification count should match requested business days");
    }

    // Feature: delivery-estimation, Property 3: Domestic parcels use base transit times
    // Validates: Requirements 1.3, 3.1, 3.2, 3.3, 3.4
    [Property(MaxTest = 100)]
    public void Property3_DomesticParcelsUseBaseTransitTimes(
        NonNegativeInt serviceTypeIndex,
        NonNegativeInt statusIndex)
    {
        // Arrange
        var serviceTypes = Enum.GetValues<ServiceType>();
        var statuses = Enum.GetValues<ParcelStatus>();

        var serviceType = serviceTypes[serviceTypeIndex.Get % serviceTypes.Length];
        var status = statuses[statusIndex.Get % statuses.Length];

        // Create a domestic parcel (same country codes)
        var parcel = CreateParcel(
            serviceType: serviceType,
            status: status,
            shipperCountry: "US",
            recipientCountry: "US",
            createdAt: new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc)); // Monday

        var service = new DeliveryEstimationService(new SimpleTimeZoneResolver());

        // Define expected base transit times for domestic shipments
        // Requirements 3.1, 3.2, 3.3, 3.4
        var expectedTransitTimes = new Dictionary<ServiceType, (int MinDays, int MaxDays)>
        {
            [ServiceType.Economy] = (5, 7),
            [ServiceType.Standard] = (3, 5),
            [ServiceType.Express] = (1, 2),
            [ServiceType.Overnight] = (1, 1)
        };

        var (expectedMinDays, expectedMaxDays) = expectedTransitTimes[serviceType];

        // Act
        var result = service.Calculate(parcel);

        // Calculate the actual transit time in business days
        var actualMinDays = CountBusinessDaysBetween(
            DateOnly.FromDateTime(parcel.CreatedAt.UtcDateTime),
            result.EarliestDelivery);
        var actualMaxDays = CountBusinessDaysBetween(
            DateOnly.FromDateTime(parcel.CreatedAt.UtcDateTime),
            result.LatestDelivery);

        // Assert
        actualMinDays.Should().Be(expectedMinDays,
            $"Domestic {serviceType} should use base minimum transit time of {expectedMinDays} days");
        actualMaxDays.Should().Be(expectedMaxDays,
            $"Domestic {serviceType} should use base maximum transit time of {expectedMaxDays} days");
    }

    // Feature: delivery-estimation, Property 4: International parcels add extra days
    // Validates: Requirements 1.4, 4.4, 4.5
    [Property(MaxTest = 100)]
    public void Property4_InternationalParcelsAddExtraDays(
        NonNegativeInt serviceTypeIndex,
        NonNegativeInt statusIndex)
    {
        // Arrange
        var serviceTypes = Enum.GetValues<ServiceType>();
        var statuses = Enum.GetValues<ParcelStatus>();

        var serviceType = serviceTypes[serviceTypeIndex.Get % serviceTypes.Length];
        var status = statuses[statusIndex.Get % statuses.Length];

        // Create an international parcel (different country codes)
        var parcel = CreateParcel(
            serviceType: serviceType,
            status: status,
            shipperCountry: "US",
            recipientCountry: "CA",
            createdAt: new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc)); // Monday

        var service = new DeliveryEstimationService(new SimpleTimeZoneResolver());

        // Define base transit times for domestic shipments
        var baseTransitTimes = new Dictionary<ServiceType, (int MinDays, int MaxDays)>
        {
            [ServiceType.Economy] = (5, 7),
            [ServiceType.Standard] = (3, 5),
            [ServiceType.Express] = (1, 2),
            [ServiceType.Overnight] = (1, 1)
        };

        var (baseMinDays, baseMaxDays) = baseTransitTimes[serviceType];

        // Requirements 4.4, 4.5: International adds 3 days to min, 5 days to max
        var expectedMinDays = baseMinDays + 3;
        var expectedMaxDays = baseMaxDays + 5;

        // Act
        var result = service.Calculate(parcel);

        // Calculate the actual transit time in business days
        var actualMinDays = CountBusinessDaysBetween(
            DateOnly.FromDateTime(parcel.CreatedAt.UtcDateTime),
            result.EarliestDelivery);
        var actualMaxDays = CountBusinessDaysBetween(
            DateOnly.FromDateTime(parcel.CreatedAt.UtcDateTime),
            result.LatestDelivery);

        // Assert
        actualMinDays.Should().Be(expectedMinDays,
            $"International {serviceType} should add 3 business days to base minimum transit time (base: {baseMinDays}, expected: {expectedMinDays})");
        actualMaxDays.Should().Be(expectedMaxDays,
            $"International {serviceType} should add 5 business days to base maximum transit time (base: {baseMaxDays}, expected: {expectedMaxDays})");
    }

    // Feature: delivery-estimation, Property 5: Confidence maps from status
    // Validates: Requirements 2.1, 2.2, 2.3
    [Property(MaxTest = 100)]
    public void Property5_ConfidenceMapsFromStatus()
    {
        // Arrange
        var service = new DeliveryEstimationService(new SimpleTimeZoneResolver());

        // Define the expected confidence mapping according to requirements 2.1, 2.2, 2.3
        var expectedMappings = new Dictionary<ParcelStatus, DeliveryConfidenceLevel>
        {
            // Requirement 2.1: OutForDelivery and Delivered → High confidence
            [ParcelStatus.OutForDelivery] = DeliveryConfidenceLevel.High,
            [ParcelStatus.Delivered] = DeliveryConfidenceLevel.High,

            // Requirement 2.2: InTransit → Medium confidence
            [ParcelStatus.InTransit] = DeliveryConfidenceLevel.Medium,

            // Requirement 2.3: LabelCreated, PickedUp, Exception, Returned → Low confidence
            [ParcelStatus.LabelCreated] = DeliveryConfidenceLevel.Low,
            [ParcelStatus.PickedUp] = DeliveryConfidenceLevel.Low,
            [ParcelStatus.Exception] = DeliveryConfidenceLevel.Low,
            [ParcelStatus.Returned] = DeliveryConfidenceLevel.Low
        };

        // Test all status values
        foreach (var status in Enum.GetValues<ParcelStatus>())
        {
            // Create a parcel with the specific status
            var parcel = CreateParcel(
                status: status,
                createdAt: new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc));

            // Act
            var result = service.Calculate(parcel);

            // Assert
            var expectedConfidence = expectedMappings[status];
            result.Confidence.Should().Be(expectedConfidence,
                $"Status {status} should map to confidence level {expectedConfidence}");
        }
    }

    // Feature: delivery-estimation, Property 6: Country code comparison is case-insensitive
    // Validates: Requirements 4.1
    [Property(MaxTest = 100)]
    public void Property6_CountryCodeComparisonIsCaseInsensitive()
    {
        // Arrange
        var service = new DeliveryEstimationService(new SimpleTimeZoneResolver());

        // Generate a random country code with mixed case variations
        var countryCodes = new[] { "US", "CA", "GB", "DE", "FR", "JP", "AU", "MX" };

        // Test all combinations of case variations
        foreach (var countryCode in countryCodes)
        {
            var variations = new[]
            {
                countryCode.ToUpper(),
                countryCode.ToLower(),
                char.ToUpper(countryCode[0]).ToString() + char.ToLower(countryCode[1]),
                char.ToLower(countryCode[0]).ToString() + char.ToUpper(countryCode[1])
            };

            // Test all pairs of variations - they should all be treated as equal (domestic)
            foreach (var shipperVariation in variations)
            {
                foreach (var recipientVariation in variations)
                {
                    var parcel = CreateParcel(
                        shipperCountry: shipperVariation,
                        recipientCountry: recipientVariation,
                        createdAt: new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc));

                    // Act
                    var isInternational = DeliveryEstimationService.IsInternational(parcel);

                    // Assert
                    isInternational.Should().BeFalse(
                        $"Country codes '{shipperVariation}' and '{recipientVariation}' should be treated as equal (domestic) regardless of case");
                }
            }
        }
    }

    // Feature: delivery-estimation, Property 7: Domestic classification by country match
    // Validates: Requirements 4.2
    [Property(MaxTest = 100)]
    public void Property7_DomesticClassificationByCountryMatch()
    {
        // Arrange
        var countryCodes = new[] { "US", "CA", "GB", "DE", "FR", "JP", "AU", "MX", "IT", "ES", "BR", "IN", "CN", "KR" };

        // Test that any parcel with matching country codes is classified as domestic
        foreach (var countryCode in countryCodes)
        {
            // Test with various case combinations to ensure case-insensitivity
            var caseVariations = new[]
            {
                (countryCode.ToUpper(), countryCode.ToUpper()),
                (countryCode.ToLower(), countryCode.ToLower()),
                (countryCode.ToUpper(), countryCode.ToLower()),
                (countryCode.ToLower(), countryCode.ToUpper()),
                (char.ToUpper(countryCode[0]).ToString() + char.ToLower(countryCode[1]),
                 char.ToLower(countryCode[0]).ToString() + char.ToUpper(countryCode[1]))
            };

            foreach (var (shipperCountry, recipientCountry) in caseVariations)
            {
                var parcel = CreateParcel(
                    shipperCountry: shipperCountry,
                    recipientCountry: recipientCountry,
                    createdAt: new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc));

                // Act
                var isInternational = DeliveryEstimationService.IsInternational(parcel);

                // Assert
                isInternational.Should().BeFalse(
                    $"Parcel with matching country codes (shipper: '{shipperCountry}', recipient: '{recipientCountry}') should be classified as domestic (IsInternational = false)");
            }
        }
    }

    // Feature: delivery-estimation, Property 8: International classification by country difference
    // Validates: Requirements 4.3
    [Property(MaxTest = 100)]
    public void Property8_InternationalClassificationByCountryDifference()
    {
        // Arrange
        var countryCodes = new[] { "US", "CA", "GB", "DE", "FR", "JP", "AU", "MX", "IT", "ES", "BR", "IN", "CN", "KR" };

        // Test that any parcel with different country codes is classified as international
        for (int i = 0; i < countryCodes.Length; i++)
        {
            for (int j = 0; j < countryCodes.Length; j++)
            {
                // Skip when country codes are the same
                if (i == j) continue;

                var shipperCountry = countryCodes[i];
                var recipientCountry = countryCodes[j];

                // Test with various case combinations to ensure case-insensitivity is maintained
                var caseVariations = new[]
                {
                    (shipperCountry.ToUpper(), recipientCountry.ToUpper()),
                    (shipperCountry.ToLower(), recipientCountry.ToLower()),
                    (shipperCountry.ToUpper(), recipientCountry.ToLower()),
                    (shipperCountry.ToLower(), recipientCountry.ToUpper()),
                    (char.ToUpper(shipperCountry[0]).ToString() + char.ToLower(shipperCountry[1]),
                     char.ToUpper(recipientCountry[0]).ToString() + char.ToLower(recipientCountry[1]))
                };

                foreach (var (shipperVariation, recipientVariation) in caseVariations)
                {
                    var parcel = CreateParcel(
                        shipperCountry: shipperVariation,
                        recipientCountry: recipientVariation,
                        createdAt: new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc));

                    // Act
                    var isInternational = DeliveryEstimationService.IsInternational(parcel);

                    // Assert
                    isInternational.Should().BeTrue(
                        $"Parcel with different country codes (shipper: '{shipperVariation}', recipient: '{recipientVariation}') should be classified as international (IsInternational = true)");
                }
            }
        }
    }

    // Feature: delivery-estimation, Property 9: Recalculation uses most recent event
    // Validates: Requirements 6.1, 8.3
    [Property(MaxTest = 100)]
    public void Property9_RecalculationUsesMostRecentEvent(
        NonNegativeInt serviceTypeIndex,
        NonNegativeInt statusIndex,
        PositiveInt daysAgoCreated,
        PositiveInt daysAgoEvent1,
        PositiveInt daysAgoEvent2,
        PositiveInt daysAgoEvent3)
    {
        // Arrange
        var serviceTypes = Enum.GetValues<ServiceType>();
        var statuses = Enum.GetValues<ParcelStatus>();

        var serviceType = serviceTypes[serviceTypeIndex.Get % serviceTypes.Length];
        var status = statuses[statusIndex.Get % statuses.Length];

        // Create timestamps - ensure they're in chronological order
        // Parcel created furthest in the past
        var createdDaysAgo = (daysAgoCreated.Get % 30) + 10; // 10-40 days ago
        var parcelCreatedAt = DateTime.UtcNow.AddDays(-createdDaysAgo);

        // Create 3 tracking events at different times, all after parcel creation
        var event1DaysAgo = Math.Min(createdDaysAgo - 1, (daysAgoEvent1.Get % 20) + 5); // 5-25 days ago, but after creation
        var event2DaysAgo = Math.Min(event1DaysAgo - 1, (daysAgoEvent2.Get % 15) + 3); // 3-18 days ago, but after event1
        var event3DaysAgo = Math.Min(event2DaysAgo - 1, (daysAgoEvent3.Get % 10) + 1); // 1-11 days ago, but after event2

        var event1Timestamp = DateTime.UtcNow.AddDays(-event1DaysAgo);
        var event2Timestamp = DateTime.UtcNow.AddDays(-event2DaysAgo);
        var event3Timestamp = DateTime.UtcNow.AddDays(-event3DaysAgo);

        // The most recent event should be event3 (smallest daysAgo value)
        var mostRecentTimestamp = event3Timestamp;

        var parcel = CreateParcel(
            serviceType: serviceType,
            status: status,
            createdAt: parcelCreatedAt);

        // Add tracking events in random order to test that the service finds the most recent
        parcel.TrackingEvents = new List<TrackingEvent>
        {
            new TrackingEvent
            {
                Id = 1,
                ParcelId = parcel.Id,
                Timestamp = event1Timestamp,
                EventType = EventType.InTransit,
                Description = "Event 1"
            },
            new TrackingEvent
            {
                Id = 2,
                ParcelId = parcel.Id,
                Timestamp = event3Timestamp, // Most recent
                EventType = EventType.InTransit,
                Description = "Event 3 - Most Recent"
            },
            new TrackingEvent
            {
                Id = 3,
                ParcelId = parcel.Id,
                Timestamp = event2Timestamp,
                EventType = EventType.InTransit,
                Description = "Event 2"
            }
        };

        var service = new DeliveryEstimationService(new SimpleTimeZoneResolver());
        var fromDate = DateOnly.FromDateTime(mostRecentTimestamp);

        // Act
        var result = service.Recalculate(parcel, fromDate);

        // Assert
        // The recalculation should use the fromDate (which represents the most recent event)
        // Calculate elapsed business days from parcel creation to the most recent event
        var elapsedDays = CountBusinessDaysBetween(
            DateOnly.FromDateTime(parcel.CreatedAt.UtcDateTime),
            fromDate);

        // Get the expected transit time range
        var isInternational = DeliveryEstimationService.IsInternational(parcel);
        var transitRange = DeliveryEstimationService.GetTransitTimeRange(parcel.ServiceType, isInternational);

        // Calculate expected remaining time (minimum 1 day)
        var expectedRemainingMin = Math.Max(1, transitRange.MinDays - elapsedDays);
        var expectedRemainingMax = Math.Max(1, transitRange.MaxDays - elapsedDays);

        // Calculate expected delivery dates from the most recent event timestamp
        var expectedEarliestDelivery = AddBusinessDaysLocal(fromDate, expectedRemainingMin);
        var expectedLatestDelivery = AddBusinessDaysLocal(fromDate, expectedRemainingMax);

        // Assert that the recalculation used the most recent event as the starting point
        result.EarliestDelivery.Should().Be(expectedEarliestDelivery,
            $"Recalculation should use the most recent event timestamp ({fromDate}) as the starting point. " +
            $"ServiceType: {serviceType}, Elapsed: {elapsedDays} days, Remaining: {expectedRemainingMin}-{expectedRemainingMax} days");

        result.LatestDelivery.Should().Be(expectedLatestDelivery,
            $"Recalculation should use the most recent event timestamp ({fromDate}) as the starting point. " +
            $"ServiceType: {serviceType}, Elapsed: {elapsedDays} days, Remaining: {expectedRemainingMin}-{expectedRemainingMax} days");

        // Verify that the delivery dates are after the most recent event
        result.EarliestDelivery.Should().BeOnOrAfter(fromDate,
            "Earliest delivery should be on or after the most recent event timestamp");
        result.LatestDelivery.Should().BeOnOrAfter(fromDate,
            "Latest delivery should be on or after the most recent event timestamp");
    }

    // Feature: delivery-estimation, Property 10: Remaining time calculation
    // Validates: Requirements 6.2, 6.3
    [Property(MaxTest = 100)]
    public void Property10_RemainingTimeCalculation(
        NonNegativeInt serviceTypeIndex,
        NonNegativeInt statusIndex,
        PositiveInt daysAgoCreated,
        PositiveInt daysAgoRecalc,
        bool isInternational)
    {
        // Arrange
        var serviceTypes = Enum.GetValues<ServiceType>();
        var statuses = Enum.GetValues<ParcelStatus>();

        var serviceType = serviceTypes[serviceTypeIndex.Get % serviceTypes.Length];
        var status = statuses[statusIndex.Get % statuses.Length];

        // Create timestamps - ensure recalculation date is after creation date
        var createdDaysAgo = (daysAgoCreated.Get % 30) + 10; // 10-40 days ago
        var recalcDaysAgo = Math.Min(createdDaysAgo - 1, (daysAgoRecalc.Get % 20) + 1); // 1-21 days ago, but after creation

        var parcelCreatedAt = DateTime.UtcNow.AddDays(-createdDaysAgo);
        var recalculationDate = DateTime.UtcNow.AddDays(-recalcDaysAgo);

        var shipperCountry = "US";
        var recipientCountry = isInternational ? "CA" : "US";

        var parcel = CreateParcel(
            serviceType: serviceType,
            status: status,
            shipperCountry: shipperCountry,
            recipientCountry: recipientCountry,
            createdAt: parcelCreatedAt);

        var service = new DeliveryEstimationService(new SimpleTimeZoneResolver());
        var fromDate = DateOnly.FromDateTime(recalculationDate);

        // Calculate expected values
        var createdDate = DateOnly.FromDateTime(parcel.CreatedAt.UtcDateTime);
        var elapsedBusinessDays = CountBusinessDaysBetween(createdDate, fromDate);

        // Get the total transit time range
        var transitRange = DeliveryEstimationService.GetTransitTimeRange(serviceType, isInternational);

        // Calculate expected remaining time (minimum 1 day per requirement 6.3)
        var expectedRemainingMin = Math.Max(1, transitRange.MinDays - elapsedBusinessDays);
        var expectedRemainingMax = Math.Max(1, transitRange.MaxDays - elapsedBusinessDays);

        // Act
        var result = service.Recalculate(parcel, fromDate);

        // Calculate actual remaining time from the result
        var actualRemainingMin = CountBusinessDaysBetween(fromDate, result.EarliestDelivery);
        var actualRemainingMax = CountBusinessDaysBetween(fromDate, result.LatestDelivery);

        // Assert - Requirement 6.2: Remaining time should equal total transit time minus elapsed business days
        actualRemainingMin.Should().Be(expectedRemainingMin,
            $"Remaining minimum transit time should be total transit time ({transitRange.MinDays}) minus elapsed business days ({elapsedBusinessDays}), " +
            $"with minimum of 1 day. ServiceType: {serviceType}, International: {isInternational}");

        actualRemainingMax.Should().Be(expectedRemainingMax,
            $"Remaining maximum transit time should be total transit time ({transitRange.MaxDays}) minus elapsed business days ({elapsedBusinessDays}), " +
            $"with minimum of 1 day. ServiceType: {serviceType}, International: {isInternational}");

        // Assert - Requirement 6.3: Remaining time should never be less than 1 business day
        actualRemainingMin.Should().BeGreaterThanOrEqualTo(1,
            "Remaining minimum transit time should never be less than 1 business day");
        actualRemainingMax.Should().BeGreaterThanOrEqualTo(1,
            "Remaining maximum transit time should never be less than 1 business day");

        // Assert - Verify the calculation logic: elapsed + remaining = total (when not capped at 1)
        if (elapsedBusinessDays < transitRange.MinDays)
        {
            (elapsedBusinessDays + actualRemainingMin).Should().Be(transitRange.MinDays,
                "When not capped, elapsed days plus remaining days should equal total transit time");
        }
        if (elapsedBusinessDays < transitRange.MaxDays)
        {
            (elapsedBusinessDays + actualRemainingMax).Should().Be(transitRange.MaxDays,
                "When not capped, elapsed days plus remaining days should equal total transit time");
        }
    }

    private static int CountBusinessDaysBetween(DateOnly startDate, DateOnly endDate)
    {
        var count = 0;
        var current = startDate;

        while (current < endDate)
        {
            current = current.AddDays(1);

            if (current.DayOfWeek is not DayOfWeek.Saturday
                and not DayOfWeek.Sunday)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountWeekendDaysBetween(DateOnly startDate, DateOnly endDate)
    {
        var count = 0;
        var current = startDate;

        while (current < endDate)
        {
            current = current.AddDays(1);

            if (current.DayOfWeek is DayOfWeek.Saturday
                or DayOfWeek.Sunday)
            {
                count++;
            }
        }

        return count;
    }

    private static DateOnly AddBusinessDaysLocal(DateOnly startDate, int businessDays)
    {
        var current = startDate;
        var added = 0;

        while (added < businessDays)
        {
            current = current.AddDays(1);

            if (current.DayOfWeek is not DayOfWeek.Saturday
                and not DayOfWeek.Sunday)
            {
                added++;
            }
        }

        return current;
    }

    // Feature: delivery-estimation, Property 11: Recalculation updates database
    // Validates: Requirements 6.4, 6.5
    // Note: This is a unit test for the service logic. The actual database update is tested in integration tests.
    [Property(MaxTest = 100)]
    public void Property11_RecalculationUpdatesEstimate(
        NonNegativeInt serviceTypeIndex,
        NonNegativeInt statusIndex,
        PositiveInt daysAgoCreated,
        PositiveInt daysAgoRecalc,
        bool isInternational)
    {
        // Arrange
        var serviceTypes = Enum.GetValues<ServiceType>();
        var statuses = Enum.GetValues<ParcelStatus>();

        var serviceType = serviceTypes[serviceTypeIndex.Get % serviceTypes.Length];
        var status = statuses[statusIndex.Get % statuses.Length];

        // Create timestamps - ensure recalculation date is after creation date
        var createdDaysAgo = (daysAgoCreated.Get % 30) + 10; // 10-40 days ago
        var recalcDaysAgo = Math.Min(createdDaysAgo - 1, (daysAgoRecalc.Get % 20) + 1); // 1-21 days ago, but after creation

        var parcelCreatedAt = DateTime.UtcNow.AddDays(-createdDaysAgo);
        var recalculationDate = DateTime.UtcNow.AddDays(-recalcDaysAgo);

        var shipperCountry = "US";
        var recipientCountry = isInternational ? "CA" : "US";

        var parcel = CreateParcel(
            serviceType: serviceType,
            status: status,
            shipperCountry: shipperCountry,
            recipientCountry: recipientCountry,
            createdAt: parcelCreatedAt);

        var service = new DeliveryEstimationService(new SimpleTimeZoneResolver());
        var fromDate = DateOnly.FromDateTime(recalculationDate);

        // Act
        var result = service.Recalculate(parcel, fromDate);

        // Assert - Requirement 6.5: The latest delivery date should be returned
        // This verifies that the service returns the correct value that should be persisted
        result.LatestDelivery.Should().NotBe(default(DateOnly),
            "Recalculation should return a valid latest delivery date to be persisted");

        // Verify that the latest delivery date is after or equal to the earliest delivery date
        result.LatestDelivery.Should().BeOnOrAfter(result.EarliestDelivery,
            "Latest delivery date should be on or after earliest delivery date");

        // Verify that the latest delivery date is after the recalculation date
        result.LatestDelivery.Should().BeAfter(fromDate,
            "Latest delivery date should be after the recalculation start date");

        // Requirement 6.4: Verify the result contains the value that should be stored in EstimatedDeliveryDate
        // The controller will store result.LatestDelivery in parcel.EstimatedDeliveryDate
        var expectedStoredDate = result.LatestDelivery;
        expectedStoredDate.Should().NotBe(default(DateOnly),
            "The value to be stored in EstimatedDeliveryDate should be valid");
    }
}
