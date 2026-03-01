using FluentAssertions;
using FsCheck.Xunit;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Domain.Rules;

namespace ParcelTracking.Application.UnitTests.Rules;

public class ParcelStatusRulesTests
{
    [Theory]
    [InlineData(ParcelStatus.LabelCreated, ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.LabelCreated, ParcelStatus.Exception)]
    [InlineData(ParcelStatus.PickedUp, ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.PickedUp, ParcelStatus.Exception)]
    [InlineData(ParcelStatus.InTransit, ParcelStatus.OutForDelivery)]
    [InlineData(ParcelStatus.InTransit, ParcelStatus.Exception)]
    [InlineData(ParcelStatus.OutForDelivery, ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.OutForDelivery, ParcelStatus.Exception)]
    [InlineData(ParcelStatus.Exception, ParcelStatus.Returned)]
    public void CanTransition_ValidTransitions_ReturnsTrue(ParcelStatus from, ParcelStatus to)
    {
        var result = ParcelStatusRules.CanTransition(from, to);

        result.Should().BeTrue(
            because: $"transition from {from} to {to} is valid according to state machine rules");
    }

    [Theory]
    [InlineData(ParcelStatus.LabelCreated, ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.LabelCreated, ParcelStatus.OutForDelivery)]
    [InlineData(ParcelStatus.LabelCreated, ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.LabelCreated, ParcelStatus.Returned)]
    [InlineData(ParcelStatus.PickedUp, ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.PickedUp, ParcelStatus.OutForDelivery)]
    [InlineData(ParcelStatus.PickedUp, ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.PickedUp, ParcelStatus.Returned)]
    [InlineData(ParcelStatus.InTransit, ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.InTransit, ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.InTransit, ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.InTransit, ParcelStatus.Returned)]
    [InlineData(ParcelStatus.OutForDelivery, ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.OutForDelivery, ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.OutForDelivery, ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.OutForDelivery, ParcelStatus.Returned)]
    [InlineData(ParcelStatus.Exception, ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.Exception, ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.Exception, ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.Exception, ParcelStatus.OutForDelivery)]
    [InlineData(ParcelStatus.Exception, ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.Exception, ParcelStatus.Exception)]
    [InlineData(ParcelStatus.Delivered, ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.Delivered, ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.Delivered, ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.Delivered, ParcelStatus.OutForDelivery)]
    [InlineData(ParcelStatus.Delivered, ParcelStatus.Exception)]
    [InlineData(ParcelStatus.Delivered, ParcelStatus.Returned)]
    [InlineData(ParcelStatus.Delivered, ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.Returned, ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.Returned, ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.Returned, ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.Returned, ParcelStatus.OutForDelivery)]
    [InlineData(ParcelStatus.Returned, ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.Returned, ParcelStatus.Exception)]
    [InlineData(ParcelStatus.Returned, ParcelStatus.Returned)]
    public void CanTransition_InvalidTransitions_ReturnsFalse(ParcelStatus from, ParcelStatus to)
    {
        var result = ParcelStatusRules.CanTransition(from, to);

        result.Should().BeFalse(
            because: $"transition from {from} to {to} is not allowed by state machine rules");
    }

    [Theory]
    [InlineData(ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.Returned)]
    public void IsTerminal_TerminalStatuses_ReturnsTrue(ParcelStatus status)
    {
        var result = ParcelStatusRules.IsTerminal(status);

        result.Should().BeTrue(
            because: $"{status} is a terminal state");
    }

    [Theory]
    [InlineData(ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.OutForDelivery)]
    [InlineData(ParcelStatus.Exception)]
    public void IsTerminal_NonTerminalStatuses_ReturnsFalse(ParcelStatus status)
    {
        var result = ParcelStatusRules.IsTerminal(status);

        result.Should().BeFalse(
            because: $"{status} is not a terminal state");
    }

    [Fact]
    public void GetAllowedTransitions_LabelCreated_ReturnsPickedUpAndException()
    {
        var result = ParcelStatusRules.GetAllowedTransitions(ParcelStatus.LabelCreated);

        result.Should().BeEquivalentTo(new[] { ParcelStatus.PickedUp, ParcelStatus.Exception });
    }

    [Fact]
    public void GetAllowedTransitions_PickedUp_ReturnsInTransitAndException()
    {
        var result = ParcelStatusRules.GetAllowedTransitions(ParcelStatus.PickedUp);

        result.Should().BeEquivalentTo(new[] { ParcelStatus.InTransit, ParcelStatus.Exception });
    }

    [Fact]
    public void GetAllowedTransitions_InTransit_ReturnsOutForDeliveryAndException()
    {
        var result = ParcelStatusRules.GetAllowedTransitions(ParcelStatus.InTransit);

        result.Should().BeEquivalentTo(new[] { ParcelStatus.OutForDelivery, ParcelStatus.Exception });
    }

    [Fact]
    public void GetAllowedTransitions_OutForDelivery_ReturnsDeliveredAndException()
    {
        var result = ParcelStatusRules.GetAllowedTransitions(ParcelStatus.OutForDelivery);

        result.Should().BeEquivalentTo(new[] { ParcelStatus.Delivered, ParcelStatus.Exception });
    }

    [Fact]
    public void GetAllowedTransitions_Exception_ReturnsReturned()
    {
        var result = ParcelStatusRules.GetAllowedTransitions(ParcelStatus.Exception);

        result.Should().BeEquivalentTo(new[] { ParcelStatus.Returned });
    }

    [Fact]
    public void GetAllowedTransitions_Delivered_ReturnsEmpty()
    {
        var result = ParcelStatusRules.GetAllowedTransitions(ParcelStatus.Delivered);

        result.Should().BeEmpty(
            because: "Delivered is a terminal state with no allowed transitions");
    }

    [Fact]
    public void GetAllowedTransitions_Returned_ReturnsEmpty()
    {
        var result = ParcelStatusRules.GetAllowedTransitions(ParcelStatus.Returned);

        result.Should().BeEmpty(
            because: "Returned is a terminal state with no allowed transitions");
    }

    [Theory]
    [InlineData(ParcelStatus.LabelCreated)]
    [InlineData(ParcelStatus.PickedUp)]
    [InlineData(ParcelStatus.InTransit)]
    [InlineData(ParcelStatus.OutForDelivery)]
    public void CanTransition_FromActiveStatesToException_ReturnsTrue(ParcelStatus from)
    {
        var result = ParcelStatusRules.CanTransition(from, ParcelStatus.Exception);

        result.Should().BeTrue(
            because: $"Exception can be reached from all active states including {from}");
    }

    // Feature: parcel-status-lifecycle, Property 3: Terminal state check correctness
    // Validates: Requirements 3.3, 3.4
    [Property(MaxTest = 100)]
    public bool IsTerminal_ReturnsCorrectValueForAllStatuses(ParcelStatus status)
    {
        // Act
        var result = ParcelStatusRules.IsTerminal(status);

        // Assert - should return true only for Delivered and Returned
        var expectedResult = status == ParcelStatus.Delivered || status == ParcelStatus.Returned;
        return result == expectedResult;
    }
}
