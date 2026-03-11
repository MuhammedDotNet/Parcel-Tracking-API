using ParcelTracking.Application.Interfaces;

namespace ParcelTracking.Infrastructure.Services;

/// <summary>
/// Generates human-readable, non-sequential tracking numbers in the format PKG-YYYYMMDD-XXXXXX.
/// Uses <see cref="Random.Shared"/> which is thread-safe and sufficient for non-cryptographic uniqueness.
/// A unique DB index on TrackingNumber provides the safety net for the astronomically-unlikely collision.
/// </summary>
public sealed class TrackingNumberGenerator : ITrackingNumberGenerator
{
    private const string Prefix = "PKG-";
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int RandomLength = 6;

    public string Generate()
    {
        var datePart = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        return $"{Prefix}{datePart}-{RandomString(RandomLength)}";
    }

    private static string RandomString(int length)
    {
        Span<char> buffer = stackalloc char[length];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        return new string(buffer);
    }
}
