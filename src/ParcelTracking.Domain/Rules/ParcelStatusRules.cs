using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Domain.Rules;

public static class ParcelStatusRules
{
    private static readonly Dictionary<ParcelStatus, HashSet<ParcelStatus>> AllowedTransitions = new()
    {
        [ParcelStatus.LabelCreated] = [ParcelStatus.PickedUp, ParcelStatus.Exception],
        [ParcelStatus.PickedUp] = [ParcelStatus.InTransit, ParcelStatus.Exception],
        [ParcelStatus.InTransit] = [ParcelStatus.OutForDelivery, ParcelStatus.Exception],
        [ParcelStatus.OutForDelivery] = [ParcelStatus.Delivered, ParcelStatus.Exception],
        [ParcelStatus.Exception] = [ParcelStatus.Returned],
        [ParcelStatus.Delivered] = [],
        [ParcelStatus.Returned] = [],
    };

    public static readonly HashSet<ParcelStatus> TerminalStates =
    [
        ParcelStatus.Delivered,
        ParcelStatus.Returned
    ];

    public static bool IsTerminal(ParcelStatus status) =>
        TerminalStates.Contains(status);

    public static bool CanTransition(ParcelStatus from, ParcelStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static IReadOnlySet<ParcelStatus> GetAllowedTransitions(ParcelStatus from) =>
        AllowedTransitions.TryGetValue(from, out var allowed)
            ? allowed
            : new HashSet<ParcelStatus>();
}
