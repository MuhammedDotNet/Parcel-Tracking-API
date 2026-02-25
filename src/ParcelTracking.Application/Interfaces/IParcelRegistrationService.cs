using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Interfaces;

public interface IParcelRegistrationService
{
    Task<ParcelResponse> RegisterAsync(RegisterParcelRequest request, CancellationToken ct);
}
