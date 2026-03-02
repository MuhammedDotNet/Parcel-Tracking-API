using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.DTOs;

public record ReportExceptionRequest
{
    public ExceptionReason Reason { get; init; }
}
