using Bogus;
using Microsoft.EntityFrameworkCore;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(ParcelTrackingDbContext db)
    {
        if (await db.Parcels.AnyAsync())
            return; // Already seeded

        Randomizer.Seed = new Random(42);

        // 1. Generate 50 addresses
        var addressFaker = new Faker<Address>()
            .RuleFor(a => a.Street1, f => f.Address.StreetAddress())
            .RuleFor(a => a.Street2, f => f.Random.Bool(0.3f)
                ? f.Address.SecondaryAddress() : null)
            .RuleFor(a => a.City, f => f.Address.City())
            .RuleFor(a => a.State, f => f.Address.StateAbbr())
            .RuleFor(a => a.PostalCode, f => f.Address.ZipCode())
            .RuleFor(a => a.CountryCode, f => "US")
            .RuleFor(a => a.IsResidential, f => f.Random.Bool(0.7f))
            .RuleFor(a => a.ContactName, f => f.Name.FullName())
            .RuleFor(a => a.CompanyName, (f, a) =>
                a.IsResidential ? null : f.Company.CompanyName())
            .RuleFor(a => a.Phone, f => f.Phone.PhoneNumber("###-###-####"))
            .RuleFor(a => a.Email, f => f.Internet.Email());

        var addresses = addressFaker.Generate(50);
        db.Addresses.AddRange(addresses);
        await db.SaveChangesAsync();

        // 2. Generate 200 parcels
        var parcelFaker = new Faker<Parcel>()
            .RuleFor(p => p.TrackingNumber, f =>
                $"PKG-{f.Date.Recent(30):yyyyMMdd}-{f.Random.AlphaNumeric(6).ToUpper()}")
            .RuleFor(p => p.Description, f => f.Commerce.ProductName())
            .RuleFor(p => p.ServiceType, f => f.PickRandom<ServiceType>())
            .RuleFor(p => p.Status, f => f.PickRandom<ParcelStatus>())
            .RuleFor(p => p.ShipperAddressId, f => f.PickRandom(addresses).Id)
            .RuleFor(p => p.RecipientAddressId, f => f.PickRandom(addresses).Id)
            .RuleFor(p => p.Weight, f => f.Random.Decimal(0.5m, 50m))
            .RuleFor(p => p.WeightUnit, f => WeightUnit.Lb)
            .RuleFor(p => p.Length, f => f.Random.Decimal(5m, 40m))
            .RuleFor(p => p.Width, f => f.Random.Decimal(5m, 30m))
            .RuleFor(p => p.Height, f => f.Random.Decimal(2m, 20m))
            .RuleFor(p => p.DimensionUnit, f => DimensionUnit.In)
            .RuleFor(p => p.DeclaredValue, f => f.Finance.Amount(10m, 2000m))
            .RuleFor(p => p.Currency, f => "USD")
            .RuleFor(p => p.EstimatedDeliveryDate, f => f.Date.SoonOffset(7))
            .RuleFor(p => p.DeliveryTimeZoneId, f => f.PickRandom(
                "America/New_York", "America/Chicago", "America/Los_Angeles", "America/Denver"))
            .RuleFor(p => p.CreatedAt, f => f.Date.RecentOffset(30))
            .RuleFor(p => p.UpdatedAt, (f, p) => p.CreatedAt)
            .RuleFor(p => p.DeliveryAttempts, f => 0);

        var parcels = parcelFaker.Generate(200);
        db.Parcels.AddRange(parcels);
        await db.SaveChangesAsync();

        // 3. Generate 2-5 tracking events per parcel
        var eventFaker = new Faker<TrackingEvent>()
            .RuleFor(te => te.EventType, f => f.PickRandom<EventType>())
            .RuleFor(te => te.Description, (f, te) => te.EventType switch
            {
                EventType.LabelCreated => "Shipping label created",
                EventType.PickedUp => "Package picked up from sender",
                EventType.ArrivedAtFacility => $"Arrived at {f.Address.City()} facility",
                EventType.DepartedFacility => $"Departed {f.Address.City()} facility",
                EventType.InTransit => "Package in transit",
                EventType.OutForDelivery => "Out for delivery",
                EventType.Delivered => "Delivered to recipient",
                EventType.DeliveryAttempted => "Delivery attempted - recipient unavailable",
                EventType.Exception => "Delivery exception occurred",
                EventType.CustomsClearance => "Cleared customs",
                _ => f.Lorem.Sentence()
            })
            .RuleFor(te => te.LocationCity, f => f.Address.City())
            .RuleFor(te => te.LocationState, f => f.Address.StateAbbr())
            .RuleFor(te => te.LocationCountry, f => "US");

        var random = new Randomizer();
        foreach (var parcel in parcels)
        {
            var count = random.Int(2, 5);
            var baseDate = parcel.CreatedAt;
            for (var i = 0; i < count; i++)
            {
                var evt = eventFaker.Generate();
                evt.ParcelId = parcel.Id;
                evt.Timestamp = baseDate.AddHours(i * 12);
                db.TrackingEvents.Add(evt);
            }
        }

        // 4. Generate 1-3 content items per parcel
        var hsCodePairs = new[]
        {
            ("8471.30", "Laptop computer"),
            ("8517.13", "Smartphone"),
            ("6110.20", "Cotton sweater"),
            ("9503.00", "Children's toy"),
            ("3304.99", "Skincare product"),
            ("8528.72", "LED monitor")
        };

        var contentFaker = new Faker<ParcelContentItem>()
            .RuleFor(ci => ci.HsCode, f => f.PickRandom(hsCodePairs).Item1)
            .RuleFor(ci => ci.Description, (f, ci) =>
                hsCodePairs.First(p => p.Item1 == ci.HsCode).Item2)
            .RuleFor(ci => ci.Quantity, f => f.Random.Int(1, 5))
            .RuleFor(ci => ci.UnitValue, f => f.Finance.Amount(10m, 500m))
            .RuleFor(ci => ci.Currency, f => "USD")
            .RuleFor(ci => ci.Weight, f => f.Random.Decimal(0.1m, 5m))
            .RuleFor(ci => ci.WeightUnit, f => WeightUnit.Lb)
            .RuleFor(ci => ci.CountryOfOrigin, f =>
                f.PickRandom("CN", "US", "DE", "JP", "KR"));

        foreach (var parcel in parcels)
        {
            var count = random.Int(1, 3);
            for (var i = 0; i < count; i++)
            {
                var item = contentFaker.Generate();
                item.ParcelId = parcel.Id;
                db.ParcelContentItems.Add(item);
            }
        }

        // 5. Generate delivery confirmations for delivered parcels
        var deliveredParcels = parcels.Where(p => p.Status == ParcelStatus.Delivered);
        var confirmationFaker = new Faker<DeliveryConfirmation>()
            .RuleFor(dc => dc.ReceivedBy, f => f.Name.FullName())
            .RuleFor(dc => dc.DeliveryLocation, f => f.PickRandom(
                "Front door", "Reception desk", "Mailroom",
                "Side gate", "Garage"))
            .RuleFor(dc => dc.SignatureImage, f =>
                Convert.ToBase64String(f.Random.Bytes(64)))
            .RuleFor(dc => dc.DeliveredAt, f => f.Date.RecentOffset(7));

        foreach (var parcel in deliveredParcels)
        {
            var confirmation = confirmationFaker.Generate();
            confirmation.ParcelId = parcel.Id;
            db.DeliveryConfirmations.Add(confirmation);
            parcel.ActualDeliveryDate = confirmation.DeliveredAt;
        }

        // 6. Generate 30 parcel watchers with random parcel assignments
        var watcherFaker = new Faker<ParcelWatcher>()
            .RuleFor(w => w.Email, f => f.Internet.Email())
            .RuleFor(w => w.Name, f => f.Random.Bool(0.8f)
                ? f.Name.FullName() : null)
            .RuleFor(w => w.CreatedAt, f => f.Date.RecentOffset(30));

        var watchers = watcherFaker.Generate(30);
        foreach (var watcher in watchers)
        {
            var watchCount = random.Int(1, 8);
            var watchedParcels = random.ListItems(parcels, watchCount);
            watcher.Parcels = watchedParcels;
        }

        db.ParcelWatchers.AddRange(watchers);

        await db.SaveChangesAsync();
    }
}
