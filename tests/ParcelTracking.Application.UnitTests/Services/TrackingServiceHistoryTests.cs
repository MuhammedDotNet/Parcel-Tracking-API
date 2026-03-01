using Bogus;
using FluentAssertions;
using Moq;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.Services;

public class TrackingServiceHistoryTests
{
    // Feature: tracking-events-history, Property 6: Complete history retrieval
    // Validates: Requirements 5.1
    [Fact]
    public async Task Property_CompleteHistoryRetrieval_ValidatesAllEventsReturned()
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

            repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(events);

            var result = await service.GetHistoryAsync(parcelId, null, null, CancellationToken.None);

            var resultList = result.ToList();
            resultList.Should().HaveCount(eventCount, 
                $"retrieving history without filters should return all {eventCount} events");

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
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

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

            var sortedEvents = events.OrderBy(e => e.Timestamp).ToList();

            repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sortedEvents);

            var result = await service.GetHistoryAsync(parcelId, null, null, CancellationToken.None);

            var resultList = result.ToList();
            resultList.Should().HaveCount(eventCount);

            for (int j = 0; j < resultList.Count - 1; j++)
            {
                resultList[j].Timestamp.Should().BeOnOrBefore(resultList[j + 1].Timestamp,
                    $"event at index {j} should have timestamp <= event at index {j + 1}");
            }

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
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            var repoMock = new Mock<IParcelRepository>();
            var service = new TrackingService(repoMock.Object);
            
            var parcelId = faker.Random.Int(1, 1000);
            
            repoMock.Setup(r => r.ParcelExistsAsync(parcelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var fromDate = faker.Date.RecentOffset(days: 30);

            var eventCount = faker.Random.Int(5, 20);
            var allEvents = new List<TrackingEvent>();

            for (int j = 0; j < eventCount; j++)
            {
                allEvents.Add(new TrackingEvent
                {
                    Id = faker.Random.Int(1, 10000),
                    ParcelId = parcelId,
                    Timestamp = faker.Date.RecentOffset(days: 60),
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence(),
                    LocationCity = faker.Address.City(),
                    LocationState = faker.Address.State(),
                    LocationCountry = faker.Address.Country()
                });
            }

            var filteredEvents = allEvents
                .Where(e => e.Timestamp >= fromDate)
                .OrderBy(e => e.Timestamp)
                .ToList();

            repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, fromDate, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(filteredEvents);

            var result = await service.GetHistoryAsync(parcelId, fromDate, null, CancellationToken.None);

            var resultList = result.ToList();
            
            foreach (var evt in resultList)
            {
                evt.Timestamp.Should().BeOnOrAfter(fromDate,
                    $"all events should have timestamp >= {fromDate}");
            }

            resultList.Should().HaveCount(filteredEvents.Count,
                "result should contain only events with timestamp >= fromDate");
        }
    }

    // Feature: tracking-events-history, Property 9: To date filtering
    // Validates: Requirements 6.2
    [Fact]
    public async Task Property_ToDateFiltering_ValidatesEventsBeforeToDate()
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

            var toDate = faker.Date.RecentOffset(days: 10);

            var eventCount = faker.Random.Int(5, 20);
            var allEvents = new List<TrackingEvent>();

            for (int j = 0; j < eventCount; j++)
            {
                allEvents.Add(new TrackingEvent
                {
                    Id = faker.Random.Int(1, 10000),
                    ParcelId = parcelId,
                    Timestamp = faker.Date.RecentOffset(days: 60),
                    EventType = faker.PickRandom<EventType>(),
                    Description = faker.Lorem.Sentence(),
                    LocationCity = faker.Address.City(),
                    LocationState = faker.Address.State(),
                    LocationCountry = faker.Address.Country()
                });
            }

            var filteredEvents = allEvents
                .Where(e => e.Timestamp <= toDate)
                .OrderBy(e => e.Timestamp)
                .ToList();

            repoMock.Setup(r => r.GetTrackingEventsAsync(parcelId, null, toDate, It.IsAny<CancellationToken>()))
                .ReturnsAsync(filteredEvents);

            var result = await service.GetHistoryAsync(parcelId, null, toDate, CancellationToken.None);

            var resultList = result.ToList();
            
            foreach (var evt in resultList)
            {
                evt.Timestamp.Should().BeOnOrBefore(toDate,
                    $"all events should have timestamp <= {toDate}");
            }

            resultList.Should().HaveCount(filteredEvents.Count,
                "result should contain only events with timestamp <= toDate");
        }
    }

    // Feature: tracking-events-history, Property 10: Invalid date range rejection
    // Validates: Requirements 6.5
    [Fact]
    public async Task Property_InvalidDateRangeRejection_ValidatesFromAfterToRejection()
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

            var toDate = faker.Date.RecentOffset(days: 30);
            var fromDate = toDate.AddDays(faker.Random.Int(1, 30));

            var act = async () => await service.GetHistoryAsync(parcelId, fromDate, toDate, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("'from' date must be earlier than or equal to 'to' date.");
        }
    }
}
